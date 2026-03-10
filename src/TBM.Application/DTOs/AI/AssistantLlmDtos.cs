namespace TBM.Application.DTOs.AI;

public class AssistantLlmRequestDto
{
    public string SystemPrompt { get; set; } = string.Empty;
    public List<AssistantLlmMessageDto> Messages { get; set; } = new();
}

public class AssistantLlmMessageDto
{
    public string Role { get; set; } = "user";
    public string Content { get; set; } = string.Empty;
}

public class AssistantLlmResponseDto
{
    public string Intent { get; set; } = "general";
    public string Reply { get; set; } = string.Empty;
    public List<AssistantLinkDto> Links { get; set; } = new();
    public List<AssistantLlmTaskPlanDto> Tasks { get; set; } = new();
    public List<AssistantLlmToolActionPlanDto> ToolActions { get; set; } = new();
}

public class AssistantLlmTaskPlanDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ActionUrl { get; set; }
    public string ActionMethod { get; set; } = "GET";
    public bool RequiresApproval { get; set; }
}

public class AssistantLlmToolActionPlanDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ActionUrl { get; set; } = string.Empty;
    public string ActionMethod { get; set; } = "GET";
    public bool RequiresApproval { get; set; }
    public string? PayloadJson { get; set; }
}
