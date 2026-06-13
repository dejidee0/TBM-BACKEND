using TBM.Application.DTOs.Common;
using TBM.Application.DTOs.Orders;

namespace TBM.Application.Interfaces;

public interface ICartService
{
    Task<ApiResponse<CartDto>> GetCartAsync(Guid userId);
    Task<ApiResponse<CartDto>> GetGuestCartAsync(string guestSessionId);
    Task<ApiResponse<CartDto>> AddToCartAsync(Guid userId, AddToCartDto dto);
    Task<ApiResponse<CartDto>> AddToGuestCartAsync(string guestSessionId, AddToCartDto dto);
    Task<ApiResponse<CartDto>> UpdateCartItemAsync(Guid userId, Guid itemId, UpdateCartItemDto dto);
    Task<ApiResponse<CartDto>> UpdateGuestCartItemAsync(string guestSessionId, Guid itemId, int quantity);
    Task<ApiResponse<bool>> RemoveCartItemAsync(Guid userId, Guid itemId);
    Task<ApiResponse<bool>> RemoveGuestCartItemAsync(string guestSessionId, Guid itemId);
    Task<ApiResponse<bool>> ClearCartAsync(Guid userId);
    Task<ApiResponse<bool>> ClearGuestCartAsync(string guestSessionId);
    Task<ApiResponse<MergeCartResultDto>> MergeGuestCartAsync(Guid userId, MergeCartRequestDto dto);
}
