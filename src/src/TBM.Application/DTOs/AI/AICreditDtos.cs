namespace TBM.Application.DTOs.AI;

public class AICreditBalanceDto
{
    public Guid UserId { get; set; }
    public int Balance { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public class AICreditAdjustmentRequestDto
{
    public Guid UserId { get; set; }
    public int Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class AICreditAdjustmentResultDto
{
    public Guid UserId { get; set; }
    public int PreviousBalance { get; set; }
    public int NewBalance { get; set; }
    public int Delta { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public DateTime TimestampUtc { get; set; }
}
