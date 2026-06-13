using TBM.Application.DTOs.Checkout;
using TBM.Application.DTOs.Common;

namespace TBM.Application.Interfaces;

public interface ICheckoutService
{
    Task<ApiResponse<CheckoutSummaryDto>> GetCheckoutSummaryAsync(Guid userId, string? promoCode = null);
    Task<ApiResponse<CheckoutSummaryDto>> GetCheckoutSummaryAsync(Guid? userId, string? guestSessionId, string? promoCode = null);
    Task<ApiResponse<PromoValidationResultDto>> ValidatePromoAsync(Guid userId, string code);
    Task<ApiResponse<PromoValidationResultDto>> ValidatePromoAsync(Guid? userId, string? guestSessionId, string code);
    Task<ApiResponse<CheckoutPaymentResultDto>> ProcessPaymentAsync(
        Guid userId,
        CheckoutPaymentRequestDto dto,
        string? idempotencyKey = null);
    Task<ApiResponse<CheckoutPaymentResultDto>> ProcessPaymentAsync(
        Guid? userId,
        CheckoutPaymentRequestDto dto,
        string? idempotencyKey = null);
    Task<ApiResponse<CheckoutPaymentResultDto>> VerifyPaystackPaymentAsync(Guid userId, string reference);
}
