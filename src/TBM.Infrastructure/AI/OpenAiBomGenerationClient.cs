using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TBM.Application.DTOs.DesignFlow;
using TBM.Application.Interfaces;
using TBM.Infrastructure.Configuration;

namespace TBM.Infrastructure.AI;

public class OpenAiBomGenerationClient : IBomGenerationClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly AssistantLlmSettings _settings;
    private readonly ILogger<OpenAiBomGenerationClient> _logger;

    public OpenAiBomGenerationClient(
        HttpClient httpClient,
        IOptions<AssistantLlmSettings> settings,
        ILogger<OpenAiBomGenerationClient> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(_settings.BaseUrl))
        {
            _httpClient.BaseAddress = new Uri(_settings.BaseUrl.TrimEnd('/'));
        }

        _httpClient.Timeout = TimeSpan.FromSeconds(
            _settings.TimeoutSeconds <= 0 ? 60 : _settings.TimeoutSeconds);
    }

    public async Task<BomGenerationResultDto?> GenerateAsync(
        BomGenerationRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled || string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            return null;
        }

        try
        {
            var systemPrompt = BuildSystemPrompt();
            var userPrompt = BuildUserPrompt(request);

            var payload = new Dictionary<string, object?>
            {
                ["model"] = string.IsNullOrWhiteSpace(_settings.Model) ? "gpt-4o-mini" : _settings.Model,
                ["temperature"] = 0.2d,
                ["max_tokens"] = _settings.MaxOutputTokens <= 0 ? 1800 : _settings.MaxOutputTokens,
                ["messages"] = new object[]
                {
                    new Dictionary<string, object?>
                    {
                        ["role"] = "system",
                        ["content"] = systemPrompt
                    },
                    new Dictionary<string, object?>
                    {
                        ["role"] = "user",
                        ["content"] = userPrompt
                    }
                },
                ["response_format"] = new Dictionary<string, object?>
                {
                    ["type"] = "json_schema",
                    ["json_schema"] = new Dictionary<string, object?>
                    {
                        ["name"] = "bom_generation",
                        ["strict"] = true,
                        ["schema"] = new Dictionary<string, object?>
                        {
                            ["type"] = "object",
                            ["additionalProperties"] = false,
                            ["properties"] = new Dictionary<string, object?>
                            {
                                ["items"] = new Dictionary<string, object?>
                                {
                                    ["type"] = "array",
                                    ["items"] = new Dictionary<string, object?>
                                    {
                                        ["type"] = "object",
                                        ["additionalProperties"] = false,
                                        ["properties"] = new Dictionary<string, object?>
                                        {
                                            ["sku"] = new Dictionary<string, object?> { ["type"] = "string" },
                                            ["quantity"] = new Dictionary<string, object?> { ["type"] = "number" },
                                            ["reason"] = new Dictionary<string, object?> { ["type"] = "string" },
                                            ["leadTimeDays"] = new Dictionary<string, object?> { ["type"] = "integer" }
                                        },
                                        ["required"] = new[] { "sku", "quantity", "reason" }
                                    }
                                },
                                ["notes"] = new Dictionary<string, object?> { ["type"] = "string" }
                            },
                            ["required"] = new[] { "items" }
                        }
                    }
                }
            };

            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions");
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
            requestMessage.Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            using var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "BOM LLM call failed with status {StatusCode}. Body: {Body}",
                    (int)response.StatusCode,
                    Truncate(raw, 1500));
                return null;
            }

            var content = ExtractAssistantContent(raw);
            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            var parsed = ParseBomResponse(content);
            if (parsed == null)
            {
                _logger.LogWarning("BOM LLM response parsing failed. Content: {Content}", Truncate(content, 1500));
            }

            return parsed;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "BOM LLM call failed.");
            return null;
        }
    }

    private static string BuildSystemPrompt()
    {
        return """
You are TBM's inventory-constrained bill of materials planner.
Only recommend products that appear in the provided inventory.
Never invent SKUs, product names, or suppliers.
Return only JSON that matches the schema.
""";
    }

    private static string BuildUserPrompt(BomGenerationRequestDto request)
    {
        var inventoryLines = request.Inventory.Select(item =>
            $"- SKU: {item.SKU}; Name: {item.Name}; Price: {(item.Price.HasValue ? item.Price.Value.ToString("N2") : "N/A")}; Stock: {(item.StockQuantity?.ToString() ?? "N/A")}; Tier: {item.QualityTier ?? "N/A"}; Material: {item.MaterialType ?? "N/A"}; Keywords: {item.AIKeywords ?? "N/A"}; RecommendedFor: {item.RecommendedFor ?? "N/A"}");

        return
            $"Project: {request.ProjectName}\n" +
            $"Room Type: {request.RoomType}\n" +
            $"Tier: {request.Tier}\n" +
            $"Vision: {request.VisionText}\n" +
            $"Dimensions: {request.RoomLength}m x {request.RoomWidth}m x {request.RoomHeight}m\n" +
            "Inventory:\n" +
            string.Join(Environment.NewLine, inventoryLines) +
            "\n\nChoose only from the inventory above and estimate quantities suitable for the room dimensions.";
    }

    private static BomGenerationResultDto? ParseBomResponse(string content)
    {
        var raw = UnwrapCodeFence(content.Trim());

        try
        {
            var parsed = JsonSerializer.Deserialize<BomGenerationResultDto>(raw, JsonOptions);
            if (parsed == null)
            {
                return null;
            }

            parsed.Items ??= new List<BomGenerationItemDto>();
            parsed.Items = parsed.Items
                .Where(x => !string.IsNullOrWhiteSpace(x.SKU))
                .Select(x => new BomGenerationItemDto
                {
                    SKU = x.SKU.Trim(),
                    Quantity = x.Quantity <= 0 ? 1 : x.Quantity,
                    Reason = string.IsNullOrWhiteSpace(x.Reason) ? "Inventory-constrained selection" : x.Reason.Trim(),
                    LeadTimeDays = x.LeadTimeDays
                })
                .ToList();

            parsed.Notes = string.IsNullOrWhiteSpace(parsed.Notes) ? null : parsed.Notes.Trim();
            return parsed;
        }
        catch
        {
            return null;
        }
    }

    private static string ExtractAssistantContent(string rawApiResponse)
    {
        using var doc = JsonDocument.Parse(rawApiResponse);
        var root = doc.RootElement;

        if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
        {
            return string.Empty;
        }

        var firstChoice = choices[0];
        if (!firstChoice.TryGetProperty("message", out var message))
        {
            return string.Empty;
        }

        if (!message.TryGetProperty("content", out var contentElement))
        {
            return string.Empty;
        }

        return contentElement.GetString() ?? string.Empty;
    }

    private static string UnwrapCodeFence(string content)
    {
        var trimmed = content.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        return trimmed
            .Replace("```json", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("```", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return $"{value[..maxLength]}...";
    }
}
