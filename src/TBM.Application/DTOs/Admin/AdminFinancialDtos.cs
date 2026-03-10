using TBM.Core.DTOs.Admin;

namespace TBM.Application.DTOs.Admin;

public class AdminFinancialStatsDto
{
    public decimal TotalRevenue { get; set; }
    public decimal RevenueThisMonth { get; set; }
    public int TotalTransactions { get; set; }
    public int SuccessfulTransactions { get; set; }
    public int RefundedTransactions { get; set; }
    public decimal RefundedAmount { get; set; }
}

public class AdminFinancialTransactionDto
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
    public string? PaymentMethod { get; set; }
    public string? PaymentReference { get; set; }
    public string OrderStatus { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? PaidAt { get; set; }
}

public class AdminFinancialTransactionListDto
{
    public List<AdminFinancialTransactionDto> Transactions { get; set; } = new();
    public AdminPaginationDto Pagination { get; set; } = new();
}

public class AdminFinancialMonthlyRevenueDto
{
    public List<TBM.Core.DTOs.Admin.MonthlyRevenueDto> Data { get; set; } = new();
}
