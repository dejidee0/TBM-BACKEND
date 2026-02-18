public class OrderStatusHistoryDto
{
    public string OldStatus { get; set; } = null!;
    public string NewStatus { get; set; } = null!;
    public DateTime ChangedAt { get; set; }
    public string? Note { get; set; }
}
