using TBM.Application.DTOs.Common;
using TBM.Application.DTOs.Orders;

namespace TBM.Application.Interfaces;

public interface ICartService
{
    Task<ApiResponse<CartDto>> GetCartAsync(Guid userId);
    Task<ApiResponse<CartDto>> AddToCartAsync(Guid userId, AddToCartDto dto);
    Task<ApiResponse<CartDto>> UpdateCartItemAsync(Guid userId, Guid itemId, UpdateCartItemDto dto);
    Task<ApiResponse<bool>> RemoveCartItemAsync(Guid userId, Guid itemId);
    Task<ApiResponse<bool>> ClearCartAsync(Guid userId);
}