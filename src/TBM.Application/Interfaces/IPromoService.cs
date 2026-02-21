using TBM.Application.DTOs.Checkout;

namespace TBM.Application.Interfaces;

public interface IPromoService
{
    Task<PromoValidationResultDto> ValidateAsync(Guid userId, decimal subTotal, string code);
}
