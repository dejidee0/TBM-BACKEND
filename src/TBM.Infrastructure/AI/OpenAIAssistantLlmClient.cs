using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TBM.Application.DTOs.AI;
using TBM.Application.Interfaces;
using TBM.Infrastructure.Configuration;

namespace TBM.Infrastructure.AI;

public class OpenAIAssistantLlmClient : IAssistantLlmClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly AssistantLlmSettings _settings;
    private readonly ILogger<OpenAIAssistantLlmClient> _logger;

    public OpenAIAssistantLlmClient(
        HttpClient httpClient,
        IOptions<AssistantLlmSettings> settings,
        ILogger<OpenAIAssistantLlmClient> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(_settings.BaseUrl))
        {
            _httpClient.BaseAddress = new Uri(_settings.BaseUrl.TrimEnd('/'));
        }

        _httpClient.Timeout = TimeSpan.FromSeconds(
            _settings.TimeoutSeconds <= 0 ? 45 : _settings.TimeoutSeconds);
    }

    public async Task<AssistantLlmResponseDto?> GenerateAsync(
        AssistantLlmRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled || string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            return null;
        }

        try
        {
            var messages = request.Messages
                .Select(m => new Dictionary<string, object?>
                {
                    ["role"] = NormalizeRole(m.Role),
                    ["content"] = m.Content
                })
                .ToList();

            messages.Insert(0, new Dictionary<string, object?>
            {
                ["role"] = "system",
                ["content"] = request.SystemPrompt
            });

            var payload = new Dictionary<string, object?>
            {
                ["model"] = _settings.Model,
                ["messages"] = messages,
                ["temperature"] = (double)_settings.Temperature,
                ["max_tokens"] = _settings.MaxOutputTokens,
                ["response_format"] = new Dictionary<string, object?>
                {
                    ["type"] = "json_object"
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
                    "Assistant LLM call failed with status {StatusCode}. Body: {Body}",
                    (int)response.StatusCode,
                    Truncate(raw, 1500));
                return null;
            }

            var content = ExtractAssistantContent(raw);
            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("Assistant LLM response content was empty.");
                return null;
            }

            var parsed = ParseStructuredResponse(content);
            if (parsed == null)
            {
                _logger.LogWarning("Assistant LLM response JSON parsing failed. Content: {Content}", Truncate(content, 1500));
            }

            return parsed;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Assistant LLM call threw an exception.");
            return null;
        }
    }

    private static AssistantLlmResponseDto? ParseStructuredResponse(string content)
    {
        var tryDirect = DeserializeOrNull(content);
        if (tryDirect != null)
        {
            NormalizeResponse(tryDirect);
            return tryDirect;
        }

        var trimmed = content.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var unwrapped = trimmed
                .Replace("```json", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("```", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Trim();

            var tryUnwrapped = DeserializeOrNull(unwrapped);
            if (tryUnwrapped != null)
            {
                NormalizeResponse(tryUnwrapped);
                return tryUnwrapped;
            }
        }

        return null;
    }

    private static AssistantLlmResponseDto? DeserializeOrNull(string raw)
    {
        try
        {
            return JsonSerializer.Deserialize<AssistantLlmResponseDto>(raw, JsonOptions);
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

    private static void NormalizeResponse(AssistantLlmResponseDto response)
    {
        response.Intent = string.IsNullOrWhiteSpace(response.Intent) ? "general" : response.Intent.Trim().ToLowerInvariant();
        response.Reply = response.Reply?.Trim() ?? string.Empty;
        response.Links ??= new List<AssistantLinkDto>();
        response.Tasks ??= new List<AssistantLlmTaskPlanDto>();
        response.ToolActions ??= new List<AssistantLlmToolActionPlanDto>();

        foreach (var link in response.Links)
        {
            link.Method = NormalizeMethod(link.Method);
            link.Label = (link.Label ?? string.Empty).Trim();
            link.Url = (link.Url ?? string.Empty).Trim();
            link.Description = string.IsNullOrWhiteSpace(link.Description) ? null : link.Description.Trim();
        }

        foreach (var task in response.Tasks)
        {
            task.ActionMethod = NormalizeMethod(task.ActionMethod);
            task.Title = (task.Title ?? string.Empty).Trim();
            task.Description = (task.Description ?? string.Empty).Trim();
            task.ActionUrl = string.IsNullOrWhiteSpace(task.ActionUrl) ? null : task.ActionUrl.Trim();
        }

        foreach (var action in response.ToolActions)
        {
            action.ActionMethod = NormalizeMethod(action.ActionMethod);
            action.Name = (action.Name ?? string.Empty).Trim();
            action.Description = (action.Description ?? string.Empty).Trim();
            action.ActionUrl = (action.ActionUrl ?? string.Empty).Trim();
        }
    }

    private static string NormalizeRole(string role)
    {
        var normalized = role?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "assistant" => "assistant",
            "system" => "system",
            _ => "user"
        };
    }

    private static string NormalizeMethod(string? method)
    {
        var normalized = method?.Trim().ToUpperInvariant();
        return normalized switch
        {
            "GET" => "GET",
            "POST" => "POST",
            "PUT" => "PUT",
            "PATCH" => "PATCH",
            "DELETE" => "DELETE",
            _ => "GET"
        };
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
