using TBM.Application.DTOs.Checkout;
using TBM.Application.DTOs.Common;

namespace TBM.Application.Interfaces;

public interface ICheckoutService
{
    Task<ApiResponse<CheckoutSummaryDto>> GetCheckoutSummaryAsync(Guid userId, string? promoCode = null);
    Task<ApiResponse<PromoValidationResultDto>> ValidatePromoAsync(Guid userId, string code);
    Task<ApiResponse<CheckoutPaymentResultDto>> ProcessPaymentAsync(
        Guid userId,
        CheckoutPaymentRequestDto dto,
        string? idempotencyKey = null);
}
