using System.Text;
using TBM.Application.DTOs.Admin;
using TBM.Application.Helpers;
using TBM.Core.Enums;
using TBM.Core.Interfaces;

namespace TBM.Application.Services;

public class AdminFinancialService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly AuditService _audit;

    public AdminFinancialService(IUnitOfWork unitOfWork, AuditService audit)
    {
        _unitOfWork = unitOfWork;
        _audit = audit;
    }

    public async Task<AdminFinancialStatsDto> GetStatsAsync()
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1);

        var totalRevenue = await _unitOfWork.Orders.GetTotalSalesAsync();
        var revenueThisMonth = await _unitOfWork.Orders.GetTotalSalesAsync(monthStart, now);
        var totalTransactions = await _unitOfWork.Orders.GetTotalOrdersCountAsync();
        var successfulTransactions = await _unitOfWork.Orders.GetCountByPaymentStatusAsync(PaymentStatus.Paid);
        var refundedTransactions = await _unitOfWork.Orders.GetCountByPaymentStatusAsync(PaymentStatus.Refunded);
        var refundedAmount = await _unitOfWork.Orders.GetRevenueByPaymentStatusAsync(PaymentStatus.Refunded);

        return new AdminFinancialStatsDto
        {
            TotalRevenue = totalRevenue,
            RevenueThisMonth = revenueThisMonth,
            TotalTransactions = totalTransactions,
            SuccessfulTransactions = successfulTransactions,
            RefundedTransactions = refundedTransactions,
            RefundedAmount = refundedAmount
        };
    }

    public async Task<AdminFinancialMonthlyRevenueDto> GetMonthlyRevenueAsync(int months = 12)
    {
        months = Math.Clamp(months, 1, 24);
        var data = await _unitOfWork.Orders.GetMonthlyRevenueAsync(months);

        var now = DateTime.UtcNow;
        var endMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var startMonth = endMonth.AddMonths(-(months - 1));

        var lookup = data.ToDictionary(x => (x.Year, x.Month), x => x.Revenue);
        var filled = DateRangeHelper.GetMonthStartsUtc(startMonth, endMonth)
            .Select(m => new TBM.Core.DTOs.Admin.MonthlyRevenueDto
            {
                Year = m.Year,
                Month = m.Month,
                Revenue = lookup.TryGetValue((m.Year, m.Month), out var revenue) ? revenue : 0m
            })
            .ToList();

        return new AdminFinancialMonthlyRevenueDto { Data = filled };
    }

    public async Task<List<TBM.Core.DTOs.Admin.RevenueByServiceDto>> GetRevenueByServiceAsync(
        string? dateRange = null)
    {
        var fromUtc = ResolveFromUtc(dateRange);
        var rows = await _unitOfWork.Orders.GetRevenueByServiceAsync(fromUtc, null);
        var enumNames = Enum.GetNames(typeof(ProductType));
        var enumSet = new HashSet<string>(enumNames, StringComparer.OrdinalIgnoreCase);
        var map = rows.ToDictionary(x => x.Service, StringComparer.OrdinalIgnoreCase);

        var merged = new List<TBM.Core.DTOs.Admin.RevenueByServiceDto>();
        foreach (var name in enumNames)
        {
            if (map.TryGetValue(name, out var row))
            {
                merged.Add(new TBM.Core.DTOs.Admin.RevenueByServiceDto
                {
                    Service = string.IsNullOrWhiteSpace(row.Service) ? name : row.Service,
                    Revenue = row.Revenue,
                    Orders = row.Orders
                });
            }
            else
            {
                merged.Add(new TBM.Core.DTOs.Admin.RevenueByServiceDto
                {
                    Service = name,
                    Revenue = 0m,
                    Orders = 0
                });
            }
        }

        foreach (var row in rows)
        {
            if (!enumSet.Contains(row.Service))
            {
                merged.Add(row);
            }
        }

        return merged
            .OrderByDescending(x => x.Revenue)
            .ThenBy(x => x.Service)
            .ToList();
    }

    public async Task<AdminFinancialTransactionListDto> GetTransactionsAsync(
        int page = 1,
        int limit = 20,
        string? search = null,
        string? filter = null)
    {
        page = page < 1 ? 1 : page;
        limit = limit < 1 ? 20 : limit;

        ParseFilter(filter, out var orderStatus, out var paymentStatus);

        var (items, totalCount) = await _unitOfWork.Orders.GetPagedAsync(
            pageNumber: page,
            pageSize: limit,
            userId: null,
            status: orderStatus,
            paymentStatus: paymentStatus,
            fromDate: null,
            toDate: null,
            searchTerm: search);

        var mapped = items.Select(o => new AdminFinancialTransactionDto
        {
            Id = o.Id,
            OrderNumber = o.OrderNumber,
            UserId = o.UserId,
            IsGuestOrder = o.IsGuestOrder,
            CustomerName = o.User == null ? o.ShippingFullName : $"{o.User.FirstName} {o.User.LastName}".Trim(),
            CustomerEmail = o.User?.Email ?? o.GuestEmail ?? string.Empty,
            Amount = o.Total,
            PaymentStatus = o.PaymentStatus.ToString(),
            PaymentMethod = o.PaymentMethod?.ToString(),
            PaymentReference = o.PaymentReference,
            OrderStatus = o.Status.ToString(),
            CreatedAt = o.CreatedAt,
            PaidAt = o.PaidAt
        }).ToList();

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)limit);

        return new AdminFinancialTransactionListDto
        {
            Transactions = mapped,
            Pagination = new AdminPaginationDto
            {
                Page = page,
                Limit = limit,
                Total = totalCount,
                TotalPages = totalPages
            }
        };
    }

    public async Task<(string FileName, string ContentType, byte[] Content)> ExportAsync(
        string? search = null,
        string? filter = null)
    {
        var list = await GetTransactionsAsync(
            page: 1,
            limit: 5000,
            search: search,
            filter: filter);

        var csv = new StringBuilder();
        csv.AppendLine("id,orderNumber,userId,customerName,customerEmail,amount,paymentStatus,paymentMethod,paymentReference,orderStatus,createdAt,paidAt");

        foreach (var item in list.Transactions)
        {
            csv.AppendLine(string.Join(',',
                item.Id,
                EscapeCsv(item.OrderNumber),
                item.UserId,
                EscapeCsv(item.CustomerName),
                EscapeCsv(item.CustomerEmail),
                item.Amount,
                EscapeCsv(item.PaymentStatus),
                EscapeCsv(item.PaymentMethod),
                EscapeCsv(item.PaymentReference),
                EscapeCsv(item.OrderStatus),
                item.CreatedAt.ToString("O"),
                item.PaidAt?.ToString("O")));
        }

        var fileName = $"admin-financial-transactions-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
        var content = Encoding.UTF8.GetBytes(csv.ToString());

        await _audit.LogAsync(
            action: "AdminExport",
            category: "Financial",
            oldValue: null,
            newValue: new
            {
                fileName,
                rows = list.Transactions.Count,
                filter,
                search
            });

        return (fileName, "text/csv", content);
    }

    private static DateTime? ResolveFromUtc(string? dateRange)
    {
        if (string.IsNullOrWhiteSpace(dateRange))
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var normalized = dateRange.Trim().ToLowerInvariant();

        return normalized switch
        {
            "7d" => now.AddDays(-7),
            "30d" => now.AddDays(-30),
            "90d" => now.AddDays(-90),
            "6m" => now.AddMonths(-6),
            "12m" => now.AddMonths(-12),
            _ => null
        };
    }

    private static void ParseFilter(
        string? filter,
        out OrderStatus? orderStatus,
        out PaymentStatus? paymentStatus)
    {
        orderStatus = null;
        paymentStatus = null;

        if (string.IsNullOrWhiteSpace(filter))
        {
            return;
        }

        var normalized = filter.Trim().ToLowerInvariant();

        if (Enum.TryParse<PaymentStatus>(normalized, true, out var parsedPayment))
        {
            paymentStatus = parsedPayment;
            return;
        }

        if (Enum.TryParse<OrderStatus>(normalized, true, out var parsedOrder))
        {
            orderStatus = parsedOrder;
        }
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
