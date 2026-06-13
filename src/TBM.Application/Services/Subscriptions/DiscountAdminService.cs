using TBM.Application.DTOs.Subscriptions;
using TBM.Core.Entities.Subscriptions;
using TBM.Core.Enums;
using TBM.Core.Interfaces;

namespace TBM.Application.Services.Subscriptions;

public sealed class DiscountAdminService
{
    private readonly IUnitOfWork _uow;

    public DiscountAdminService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<List<DiscountDto>> GetAllAsync()
    {
        var discounts = await _uow.Discounts.GetAllActiveAsync();
        return discounts.Select(MapToDto).ToList();
    }

    public async Task<DiscountDto?> GetByIdAsync(Guid id)
    {
        var d = await _uow.Discounts.GetByIdAsync(id);
        return d == null ? null : MapToDto(d);
    }

    public async Task<DiscountDto> CreateAsync(CreateDiscountDto dto)
    {
        if (!Enum.TryParse<DiscountType>(dto.Type, true, out var discountType))
        {
            throw new ArgumentException($"Invalid discount type: {dto.Type}. Use 'Percentage' or 'FixedAmount'.");
        }

        SubscriptionTier? tier = null;
        if (!string.IsNullOrWhiteSpace(dto.ApplicableTier) &&
            Enum.TryParse<SubscriptionTier>(dto.ApplicableTier, true, out var parsedTier))
        {
            tier = parsedTier;
        }

        BillingCycle? cycle = null;
        if (!string.IsNullOrWhiteSpace(dto.ApplicableBillingCycle) &&
            Enum.TryParse<BillingCycle>(dto.ApplicableBillingCycle, true, out var parsedCycle))
        {
            cycle = parsedCycle;
        }

        var discount = new Discount
        {
            Code = dto.Code.Trim().ToUpperInvariant(),
            Name = dto.Name.Trim(),
            Description = dto.Description,
            Type = discountType,
            Value = dto.Value,
            ApplicableTier = tier,
            ApplicableBillingCycle = cycle,
            StartsAt = dto.StartsAt,
            ExpiresAt = dto.ExpiresAt,
            MaxRedemptions = dto.MaxRedemptions,
            SingleUsePerUser = dto.SingleUsePerUser,
            IsActive = true
        };

        await _uow.Discounts.CreateAsync(discount);
        await _uow.SaveChangesAsync();

        return MapToDto(discount);
    }

    public async Task<DiscountDto?> UpdateAsync(Guid id, UpdateDiscountDto dto)
    {
        var discount = await _uow.Discounts.GetByIdAsync(id);
        if (discount == null)
        {
            return null;
        }

        discount.Name = dto.Name.Trim();
        discount.Description = dto.Description;
        discount.Value = dto.Value;
        discount.ExpiresAt = dto.ExpiresAt;
        discount.MaxRedemptions = dto.MaxRedemptions;
        discount.IsActive = dto.IsActive;
        discount.UpdatedAt = DateTime.UtcNow;

        await _uow.Discounts.UpdateAsync(discount);
        await _uow.SaveChangesAsync();

        return MapToDto(discount);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var discount = await _uow.Discounts.GetByIdAsync(id);
        if (discount == null)
        {
            return false;
        }

        await _uow.Discounts.DeleteAsync(id);
        await _uow.SaveChangesAsync();
        return true;
    }

    private static DiscountDto MapToDto(Discount d) => new()
    {
        Id = d.Id,
        Code = d.Code,
        Name = d.Name,
        Description = d.Description,
        Type = d.Type.ToString(),
        Value = d.Value,
        ApplicableTier = d.ApplicableTier?.ToString(),
        ApplicableBillingCycle = d.ApplicableBillingCycle?.ToString(),
        StartsAt = d.StartsAt,
        ExpiresAt = d.ExpiresAt,
        IsActive = d.IsActive,
        CurrentRedemptions = d.CurrentRedemptions,
        MaxRedemptions = d.MaxRedemptions,
        CreatedAt = d.CreatedAt
    };
}
