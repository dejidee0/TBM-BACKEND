using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using TBM.Application.DTOs.AI;
using TBM.Application.Interfaces;
using TBM.Application.Services;
using TBM.Infrastructure.Configuration;

namespace TBM.API.Controllers.V1.AI;

[ApiController]
[Route("api/v1/ai/assistant")]
[Authorize]
[EnableRateLimiting("DynamicPolicy")]
public class AIAssistantController : ControllerBase
{
    private readonly AIPersonalAssistantService _assistantService;
    private readonly IAssistantLlmClient _llmClient;
    private readonly AssistantLlmSettings _llmSettings;

    public AIAssistantController(
        AIPersonalAssistantService assistantService,
        IAssistantLlmClient llmClient,
        IOptions<AssistantLlmSettings> llmSettings)
    {
        _assistantService = assistantService;
        _llmClient = llmClient;
        _llmSettings = llmSettings.Value;
    }

    /// <summary>Diagnose ZIORA LLM connectivity — returns the raw HTTP status and error body from the provider.</summary>
    [HttpGet("health")]
    [AllowAnonymous]
    public async Task<IActionResult> Health([FromServices] IHttpClientFactory httpClientFactory)
    {
        var config = new
        {
            enabled = _llmSettings.Enabled,
            model = _llmSettings.Model,
            baseUrl = _llmSettings.BaseUrl,
            apiKeySet = !string.IsNullOrWhiteSpace(_llmSettings.ApiKey),
            apiKeyPrefix = string.IsNullOrWhiteSpace(_llmSettings.ApiKey) ? "MISSING" : _llmSettings.ApiKey[..Math.Min(12, _llmSettings.ApiKey.Length)] + "...",
            temperature = _llmSettings.Temperature,
            maxTokens = _llmSettings.MaxOutputTokens,
            timeoutSeconds = _llmSettings.TimeoutSeconds
        };

        if (!_llmSettings.Enabled || string.IsNullOrWhiteSpace(_llmSettings.ApiKey))
            return Ok(new { status = "disabled", config, error = "Enabled=false or ApiKey is empty in appsettings on this server." });

        // Raw HTTP test — bypasses the LLM client's error swallowing so we see the actual response
        try
        {
            var http = httpClientFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(20);

            var payload = System.Text.Json.JsonSerializer.Serialize(new
            {
                model = _llmSettings.Model,
                messages = new[]
                {
                    new { role = "system", content = "You are a test assistant. Always respond with valid json: {\"ok\":true}" },
                    new { role = "user", content = "ping, respond in json" }
                },
                max_tokens = 20,
                response_format = new { type = "json_object" }
            });

            using var req = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post,
                $"{_llmSettings.BaseUrl.TrimEnd('/')}/chat/completions");
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _llmSettings.ApiKey);
            req.Content = new System.Net.Http.StringContent(payload, System.Text.Encoding.UTF8, "application/json");

            using var resp = await http.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
            {
                // Also run a second test that mirrors exactly what the Ziora service sends
                // (long system prompt with uppercase JSON — this is what was failing)
                var zioraPayload = System.Text.Json.JsonSerializer.Serialize(new
                {
                    model = _llmSettings.Model,
                    messages = new object[]
                    {
                        new { role = "system", content = "You are ZIORA, TBM assistant. You always respond in valid json format. ## OUTPUT FORMAT — return ONLY valid JSON, no markdown fences\n{\"intent\":\"general\",\"reply\":\"\",\"links\":[],\"tasks\":[],\"toolActions\":[]}" },
                        new { role = "user", content = "hello" }
                    },
                    max_tokens = 80,
                    response_format = new { type = "json_object" }
                });

                using var req2 = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post,
                    $"{_llmSettings.BaseUrl.TrimEnd('/')}/chat/completions");
                req2.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _llmSettings.ApiKey);
                req2.Content = new System.Net.Http.StringContent(zioraPayload, System.Text.Encoding.UTF8, "application/json");

                using var resp2 = await http.SendAsync(req2);
                var body2 = await resp2.Content.ReadAsStringAsync();

                string? zioraReply = null;
                try
                {
                    using var d = System.Text.Json.JsonDocument.Parse(body2);
                    zioraReply = d.RootElement
                        .GetProperty("choices")[0]
                        .GetProperty("message")
                        .GetProperty("content")
                        .GetString();
                }
                catch { }

                return Ok(new
                {
                    status = resp2.IsSuccessStatusCode ? "ok" : "ziora_format_test_failed",
                    config,
                    httpStatus = (int)resp.StatusCode,
                    zioraFormatTestStatus = (int)resp2.StatusCode,
                    zioraFormatTestReply = zioraReply,
                    zioraFormatTestError = resp2.IsSuccessStatusCode ? null : body2
                });
            }

            return Ok(new
            {
                status = "llm_call_failed",
                config,
                httpStatus = (int)resp.StatusCode,
                rawError = body,
                hint = (int)resp.StatusCode switch
                {
                    401 => "Invalid API key — key rejected by the provider.",
                    403 => "Forbidden — key valid but no access to this model or plan.",
                    429 => "Rate limit or quota exceeded.",
                    _ => "Check rawError above for the provider's exact message."
                }
            });
        }
        catch (System.Net.Http.HttpRequestException ex)
        {
            return Ok(new
            {
                status = "network_error",
                config,
                error = ex.Message,
                hint = "The server cannot reach the AI provider. This is likely a FIREWALL or outbound HTTP restriction on the hosting server. The host (site4now.net) may be blocking outbound HTTPS to external APIs. Contact your hosting provider or use a VPS."
            });
        }
        catch (TaskCanceledException)
        {
            return Ok(new
            {
                status = "timeout",
                config,
                hint = "Connection timed out after 20s. Server may be blocked from reaching the AI provider."
            });
        }
    }

    [HttpPost("message")]
    public async Task<IActionResult> SendMessage([FromBody] AssistantMessageRequestDto request)
    {
        try
        {
            var response = await _assistantService.HandleMessageAsync(GetCurrentUserId(), request);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            var logger = HttpContext.RequestServices.GetRequiredService<ILogger<AIAssistantController>>();
            logger.LogError(ex, "ZIORA unhandled error. UserId={UserId} Message={Message}",
                User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                request.Message?.Length > 100 ? request.Message[..100] : request.Message);
            return StatusCode(500, new { success = false, message = "ZIORA encountered an unexpected error. Please try again." });
        }
    }

    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions()
    {
        var sessions = await _assistantService.GetSessionsAsync(GetCurrentUserId());
        return Ok(new { sessions });
    }

    [HttpGet("sessions/{sessionId:guid}")]
    public async Task<IActionResult> GetSession(Guid sessionId)
    {
        var session = await _assistantService.GetSessionAsync(GetCurrentUserId(), sessionId);
        if (session == null)
        {
            return NotFound(new { success = false, message = "Session not found" });
        }

        return Ok(session);
    }

    [HttpDelete("sessions/{sessionId:guid}")]
    public async Task<IActionResult> ClearSession(Guid sessionId)
    {
        var deleted = await _assistantService.ClearSessionAsync(GetCurrentUserId(), sessionId);
        if (!deleted)
            return NotFound(new { success = false, message = "Session not found." });

        return Ok(new { success = true, message = "Session cleared." });
    }

    [HttpPatch("tasks/{taskId:guid}")]
    public async Task<IActionResult> UpdateTask(Guid taskId, [FromBody] UpdateAssistantTaskStatusRequestDto request)
    {
        try
        {
            var task = await _assistantService.UpdateTaskStatusAsync(GetCurrentUserId(), taskId, request.Status);
            if (task == null)
            {
                return NotFound(new { success = false, message = "Task not found" });
            }

            return Ok(new { success = true, task });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpGet("tool-actions/{actionId:guid}")]
    public async Task<IActionResult> GetToolAction(Guid actionId)
    {
        var action = await _assistantService.GetToolActionAsync(GetCurrentUserId(), actionId);
        if (action == null)
        {
            return NotFound(new { success = false, message = "Tool action not found" });
        }

        return Ok(action);
    }

    [HttpPost("tool-actions/{actionId:guid}/approval")]
    public async Task<IActionResult> ApproveToolAction(Guid actionId, [FromBody] ApproveAssistantToolActionRequestDto request)
    {
        var action = await _assistantService.ApproveToolActionAsync(
            GetCurrentUserId(),
            actionId,
            request.Approve,
            request.Reason);

        if (action == null)
        {
            return NotFound(new { success = false, message = "Tool action not found" });
        }

        return Ok(new { success = true, action });
    }

    [HttpPost("tool-actions/{actionId:guid}/execute")]
    public async Task<IActionResult> ExecuteToolAction(Guid actionId, [FromBody] ExecuteAssistantToolActionRequestDto request)
    {
        var execution = await _assistantService.ExecuteToolActionAsync(
            GetCurrentUserId(),
            actionId,
            request.DryRun);

        if (execution == null)
        {
            return NotFound(new { success = false, message = "Tool action not found" });
        }

        return Ok(new { success = true, execution });
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("User ID claim missing.");
        }

        return userId;
    }
}
