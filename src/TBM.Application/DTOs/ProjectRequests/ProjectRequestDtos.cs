namespace TBM.Application.DTOs.ProjectRequests;

public class CreateProjectRequestDto
{
    public Guid? EstimateId { get; set; }
    public string? ProjectDescription { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string? AdditionalNotes { get; set; }
}

public class ProjectRequestResponseDto
{
    public bool Success { get; set; }
    public Guid RequestId { get; set; }
    public string RequestType { get; set; } = string.Empty;
    public string Status { get; set; } = "Received";
    public string Message { get; set; } = string.Empty;
}
