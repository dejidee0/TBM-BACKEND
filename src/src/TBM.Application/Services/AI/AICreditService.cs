using System.Globalization;
using System.Text.Json;
using TBM.Application.DTOs.AI;
using TBM.Core.Entities;
using TBM.Core.Enums;
using TBM.Core.Interfaces;

namespace TBM.Application.Services;

public class AICreditService
{
    private const string BalanceCategory = "AICredits";
    private const string LedgerCategory = "AICreditLedger";
    private const int DefaultInitialCredits = 100;

    private readonly IUnitOfWork _unitOfWork;
    private readonly AuditService _auditService;

    public AICreditService(IUnitOfWork unitOfWork, AuditService auditService)
    {
        _unitOfWork = unitOfWork;
        _auditService = auditService;
    }

    public int GetCreditsRequired(AIGenerationType generationType)
    {
        return generationType switch
        {
            AIGenerationType.ImageToImage => 5,
            AIGenerationType.ImageToVideo => 20,
            _ => 10
        };
    }

    public async Task<AICreditBalanceDto> GetBalanceAsync(Guid userId)
    {
        var setting = await EnsureBalanceSettingAsync(userId);
        return new AICreditBalanceDto
        {
            UserId = userId,
            Balance = ParseBalance(setting.Value),
            UpdatedAtUtc = setting.UpdatedAt ?? setting.CreatedAt
        };
    }

    public async Task<AICreditAdjustmentResultDto> DeductForGenerationAsync(
        Guid userId,
        AIGenerationType generationType,
        Guid projectId)
    {
        var credits = GetCreditsRequired(generationType);
        var reason = $"AI generation charge ({generationType})";
        return await AdjustAsync(
            userId,
            -credits,
            reason,
            "generation_debit",
            $"project:{projectId:N}",
            actorUserId: null,
            writeAudit: false);
    }

    public async Task<AICreditAdjustmentResultDto> RefundGenerationAsync(
        Guid userId,
        AIGenerationType generationType,
        Guid projectId,
        string reason)
    {
        var credits = GetCreditsRequired(generationType);
        return await AdjustAsync(
            userId,
            credits,
            reason,
            "generation_refund",
            $"project:{projectId:N}",
            actorUserId: null,
            writeAudit: false);
    }

    public async Task<AICreditAdjustmentResultDto> AdjustByAdminAsync(
        Guid adminUserId,
        Guid userId,
        int amount,
        string reason)
    {
        if (amount == 0)
        {
            throw new InvalidOperationException("Credit adjustment amount cannot be zero.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidOperationException("Credit adjustment reason is required.");
        }

        return await AdjustAsync(
            userId,
            amount,
            reason.Trim(),
            amount > 0 ? "admin_topup" : "admin_reverse",
            reference: null,
            actorUserId: adminUserId,
            writeAudit: true);
    }

    private async Task<AICreditAdjustmentResultDto> AdjustAsync(
        Guid userId,
        int delta,
        string reason,
        string actionType,
        string? reference,
        Guid? actorUserId,
        bool writeAudit)
    {
        var setting = await EnsureBalanceSettingAsync(userId);
        var previous = ParseBalance(setting.Value);
        var next = previous + delta;

        if (next < 0)
        {
            throw new InvalidOperationException("Insufficient AI credits for this generation.");
        }

        setting.Value = next.ToString(CultureInfo.InvariantCulture);
        setting.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.Settings.UpdateAsync(setting);

        var ledgerEntry = new CreditLedgerEntry
        {
            UserId = userId,
            Delta = delta,
            PreviousBalance = previous,
            NewBalance = next,
            Reason = reason,
            ActionType = actionType,
            Reference = reference,
            ActorUserId = actorUserId,
            TimestampUtc = DateTime.UtcNow
        };

        await _unitOfWork.Settings.AddAsync(new Setting
        {
            Category = LedgerCategory,
            Key = BuildLedgerKey(),
            Value = JsonSerializer.Serialize(ledgerEntry),
            Description = "AI credit ledger entry"
        });

        await _unitOfWork.SaveChangesAsync();

        if (writeAudit && actorUserId.HasValue)
        {
            await _auditService.LogAsync(
                "AI.Credits.Adjust",
                "AI",
                new { userId, previousBalance = previous },
                new { userId, newBalance = next, delta, reason, actionType, actorUserId, reference });
        }

        return new AICreditAdjustmentResultDto
        {
            UserId = userId,
            PreviousBalance = previous,
            NewBalance = next,
            Delta = delta,
            Reason = reason,
            ActionType = actionType,
            Reference = reference,
            TimestampUtc = ledgerEntry.TimestampUtc
        };
    }

    private async Task<Setting> EnsureBalanceSettingAsync(Guid userId)
    {
        var key = BuildBalanceKey(userId);
        var setting = await _unitOfWork.Settings.GetByKeyAsync(BalanceCategory, key);

        if (setting != null)
        {
            return setting;
        }

        setting = new Setting
        {
            Category = BalanceCategory,
            Key = key,
            Value = DefaultInitialCredits.ToString(CultureInfo.InvariantCulture),
            Description = "AI generation credit balance"
        };

        await _unitOfWork.Settings.AddAsync(setting);
        await _unitOfWork.SaveChangesAsync();
        return setting;
    }

    private static int ParseBalance(string? value)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return DefaultInitialCredits;
    }

    private static string BuildBalanceKey(Guid userId) => $"balance:{userId:N}";

    private static string BuildLedgerKey() =>
        $"{DateTime.UtcNow:yyyyMMddHHmmssfff}:{Guid.NewGuid():N}";

    private sealed class CreditLedgerEntry
    {
        public Guid UserId { get; set; }
        public int Delta { get; set; }
        public int PreviousBalance { get; set; }
        public int NewBalance { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string ActionType { get; set; } = string.Empty;
        public string? Reference { get; set; }
        public Guid? ActorUserId { get; set; }
        public DateTime TimestampUtc { get; set; }
    }
}
