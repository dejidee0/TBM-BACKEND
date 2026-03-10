namespace TBM.Application.DTOs.AI;

public class AssistantMessageRequestDto
{
    public Guid? SessionId { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool EnableToolPlanning { get; set; } = true;
}

public class AssistantMessageResponseDto
{
    public Guid SessionId { get; set; }
    public Guid UserMessageId { get; set; }
    public Guid AssistantMessageId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string Intent { get; set; } = string.Empty;
    public string AssistantReply { get; set; } = string.Empty;
    public List<AssistantLinkDto> Links { get; set; } = new();
    public List<AssistantTaskDto> SuggestedTasks { get; set; } = new();
    public List<AssistantToolActionDto> ToolActions { get; set; } = new();
}

public class AssistantSessionSummaryDto
{
    public Guid SessionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime LastUpdatedAtUtc { get; set; }
    public int MessageCount { get; set; }
    public int PendingTaskCount { get; set; }
}

public class AssistantSessionDetailsDto
{
    public Guid SessionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime LastUpdatedAtUtc { get; set; }
    public List<AssistantChatMessageDto> Messages { get; set; } = new();
    public List<AssistantTaskDto> Tasks { get; set; } = new();
    public List<AssistantToolActionDto> ToolActions { get; set; } = new();
}

public class AssistantChatMessageDto
{
    public Guid MessageId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string? Intent { get; set; }
    public List<AssistantLinkDto> Links { get; set; } = new();
}

public class AssistantTaskDto
{
    public Guid TaskId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public string? ActionUrl { get; set; }
    public string ActionMethod { get; set; } = "GET";
    public bool RequiresApproval { get; set; }
    public Guid? ToolActionId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}

public class AssistantLinkDto
{
    public string Label { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Method { get; set; } = "GET";
    public string? Description { get; set; }
}

public class UpdateAssistantTaskStatusRequestDto
{
    public string Status { get; set; } = "completed";
}

public class AssistantToolActionDto
{
    public Guid ActionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ActionUrl { get; set; } = string.Empty;
    public string ActionMethod { get; set; } = "GET";
    public bool RequiresApproval { get; set; }
    public string Status { get; set; } = "pending_approval";
    public DateTime CreatedAtUtc { get; set; }
    public AssistantToolApprovalDto? LatestApproval { get; set; }
    public AssistantToolExecutionDto? LatestExecution { get; set; }
}

public class AssistantToolApprovalDto
{
    public Guid ApprovalId { get; set; }
    public Guid ActionId { get; set; }
    public string Status { get; set; } = "pending";
    public string? Reason { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
}

public class AssistantToolExecutionDto
{
    public Guid ExecutionId { get; set; }
    public Guid ActionId { get; set; }
    public string Status { get; set; } = "succeeded";
    public string? ResultJson { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime ExecutedAtUtc { get; set; }
}

public class ApproveAssistantToolActionRequestDto
{
    public bool Approve { get; set; }
    public string? Reason { get; set; }
}

public class ExecuteAssistantToolActionRequestDto
{
    public bool DryRun { get; set; }
}
