using TBM.Application.DTOs.AI;
using TBM.Core.Entities.AI;
using TBM.Core.Enums;
using TBM.Core.Interfaces;

namespace TBM.Application.Services;

public class AIUsageService
{
    private readonly IUnitOfWork _unitOfWork;

    public AIUsageService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task RecordGenerationAsync(
        Guid userId,
        Guid projectId,
        AIGenerationType generationType,
        int creditsUsed,
        decimal estimatedCost,
        string provider,
        bool succeeded)
    {
        var usage = new AIUsage
        {
            UserId = userId,
            AIProjectId = projectId,
            GenerationType = generationType,
            CreditsUsed = creditsUsed,
            EstimatedCost = estimatedCost,
            Provider = succeeded ? provider : $"{provider}:failed"
        };

        await _unitOfWork.AIUsages.CreateAsync(usage);
        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<AIUsageSummaryResponseDto> GetUserSummaryAsync(
        Guid userId,
        int? year,
        int? month)
    {
        var (fromUtc, toUtc) = ResolveUserWindow(year, month);
        var summary = await _unitOfWork.AIUsages.GetUserSummaryAsync(userId, fromUtc, toUtc);

        return new AIUsageSummaryResponseDto
        {
            FromUtc = summary.FromUtc,
            ToUtc = summary.ToUtc,
            TotalGenerations = summary.TotalGenerations,
            TotalCreditsUsed = summary.TotalCreditsUsed,
            TotalEstimatedCost = summary.TotalEstimatedCost
        };
    }

    public async Task<List<AIUsageMonthlySpendResponseDto>> GetMonthlySpendAsync(int months)
    {
        if (months < 1)
        {
            months = 1;
        }

        if (months > 36)
        {
            months = 36;
        }

        var now = DateTime.UtcNow;
        var fromUtc = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-(months - 1));
        var toUtc = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1);

        var rows = await _unitOfWork.AIUsages.GetMonthlySpendAsync(fromUtc, toUtc);
        return rows.Select(x => new AIUsageMonthlySpendResponseDto
        {
            Year = x.Year,
            Month = x.Month,
            TotalGenerations = x.TotalGenerations,
            TotalCreditsUsed = x.TotalCreditsUsed,
            TotalEstimatedCost = x.TotalEstimatedCost,
            DistinctUsers = x.DistinctUsers
        }).ToList();
    }

    private static (DateTime fromUtc, DateTime toUtc) ResolveUserWindow(int? year, int? month)
    {
        var now = DateTime.UtcNow;

        if (year.HasValue && month.HasValue)
        {
            var from = new DateTime(year.Value, month.Value, 1, 0, 0, 0, DateTimeKind.Utc);
            return (from, from.AddMonths(1));
        }

        if (year.HasValue)
        {
            var from = new DateTime(year.Value, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return (from, from.AddYears(1));
        }

        var currentMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return (currentMonthStart, currentMonthStart.AddMonths(1));
    }
}
