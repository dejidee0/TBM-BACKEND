using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TBM.Application.DTOs.AI;
using TBM.Application.Services;

namespace TBM.API.Controllers.V1.AI;

[ApiController]
[Route("api/v1/ai/assistant")]
[Authorize]
[EnableRateLimiting("DynamicPolicy")]
public class AIAssistantController : ControllerBase
{
    private readonly AIPersonalAssistantService _assistantService;

    public AIAssistantController(AIPersonalAssistantService assistantService)
    {
        _assistantService = assistantService;
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
