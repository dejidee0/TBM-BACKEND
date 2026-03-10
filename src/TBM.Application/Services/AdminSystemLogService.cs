using System.Text;
using TBM.Application.DTOs.Admin;
using TBM.Core.Entities.Audit;
using TBM.Core.Interfaces;

namespace TBM.Application.Services;

public class AdminSystemLogService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly AuditService _audit;

    public AdminSystemLogService(IUnitOfWork unitOfWork, AuditService audit)
    {
        _unitOfWork = unitOfWork;
        _audit = audit;
    }

    public async Task<AdminSystemLogStatsDto> GetStatsAsync(string? dateRange = null)
    {
        var fromUtc = ResolveFromUtc(dateRange);

        var stats = await _unitOfWork.AuditLogs.GetStatsAsync(fromUtc, null);

        return new AdminSystemLogStatsDto
        {
            TotalLogs = stats.TotalLogs,
            ErrorCount = stats.ErrorCount,
            WarningCount = stats.WarningCount,
            InfoCount = stats.InfoCount,
            LastLogAt = stats.LastLogAt
        };
    }

    public async Task<AdminSystemLogListDto> GetLogsAsync(
        int page = 1,
        int limit = 20,
        string? search = null,
        string? severity = null,
        string? dateRange = null)
    {
        page = page < 1 ? 1 : page;
        limit = limit < 1 ? 20 : limit;

        var fromUtc = ResolveFromUtc(dateRange);
        var (items, totalCount) = await _unitOfWork.AuditLogs.GetPagedAsync(
            page,
            limit,
            search,
            severity,
            fromUtc,
            null);

        var logs = items.Select(MapLog).ToList();
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)limit);

        return new AdminSystemLogListDto
        {
            Logs = logs,
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
        string? severity = null,
        string? search = null,
        string? dateRange = null)
    {
        var fromUtc = ResolveFromUtc(dateRange);
        var (items, _) = await _unitOfWork.AuditLogs.GetPagedAsync(
            page: 1,
            pageSize: 5000,
            search: search,
            severity: severity,
            fromUtc: fromUtc,
            toUtc: null);

        var csv = new StringBuilder();
        csv.AppendLine("id,createdAt,severity,action,category,userId,ipAddress");

        foreach (var log in items)
        {
            csv.AppendLine(string.Join(',',
                log.Id,
                log.CreatedAt.ToString("O"),
                EscapeCsv(ClassifySeverity(log)),
                EscapeCsv(log.Action),
                EscapeCsv(log.Category),
                EscapeCsv(log.UserId),
                EscapeCsv(log.IpAddress)));
        }

        var fileName = $"admin-system-logs-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
        var content = Encoding.UTF8.GetBytes(csv.ToString());

        await _audit.LogAsync(
            action: "AdminExport",
            category: "SystemLogs",
            oldValue: null,
            newValue: new
            {
                fileName,
                rows = items.Count(),
                severity,
                search,
                dateRange
            });

        return (fileName, "text/csv", content);
    }

    private static AdminSystemLogDto MapLog(AuditLog log)
    {
        return new AdminSystemLogDto
        {
            Id = log.Id,
            Action = log.Action,
            Category = log.Category,
            Severity = ClassifySeverity(log),
            UserId = log.UserId,
            IpAddress = log.IpAddress,
            OldValue = log.OldValue,
            NewValue = log.NewValue,
            CreatedAt = log.CreatedAt
        };
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
            "24h" => now.AddHours(-24),
            "7d" => now.AddDays(-7),
            "30d" => now.AddDays(-30),
            "90d" => now.AddDays(-90),
            _ => null
        };
    }

    private static string ClassifySeverity(AuditLog log)
    {
        var text = $"{log.Action} {log.Category} {log.NewValue} {log.OldValue}".ToLowerInvariant();

        if (text.Contains("error") || text.Contains("exception") || text.Contains("failed"))
        {
            return "error";
        }

        if (text.Contains("warn") ||
            text.Contains("suspend") ||
            text.Contains("cancel") ||
            text.Contains("delete") ||
            text.Contains("refund") ||
            text.Contains("deactivate"))
        {
            return "warning";
        }

        return "info";
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
