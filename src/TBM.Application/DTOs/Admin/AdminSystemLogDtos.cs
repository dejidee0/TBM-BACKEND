namespace TBM.Application.DTOs.Admin;

public class AdminSystemLogDto
{
    public Guid Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Severity { get; set; } = "info";
    public string UserId { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AdminSystemLogStatsDto
{
    public int TotalLogs { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public int InfoCount { get; set; }
    public DateTime? LastLogAt { get; set; }
}

public class AdminPaginationDto
{
    public int Page { get; set; }
    public int Limit { get; set; }
    public int Total { get; set; }
    public int TotalPages { get; set; }
}

public class AdminSystemLogListDto
{
    public List<AdminSystemLogDto> Logs { get; set; } = new();
    public AdminPaginationDto Pagination { get; set; } = new();
}
