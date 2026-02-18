using TBM.Core.Entities.Orders;

namespace TBM.Core.Interfaces.Repositories;

public interface ICartRepository
{
    Task<Cart?> GetByUserIdAsync(Guid userId);
    Task<Cart?> GetByIdAsync(Guid id);
    Task<Cart> CreateAsync(Cart cart);
    Task UpdateAsync(Cart cart);
    Task DeleteAsync(Guid id);
    Task<CartItem?> GetCartItemAsync(Guid cartId, Guid productId);
    Task AddItemAsync(CartItem item);
    Task UpdateItemAsync(CartItem item);
    Task RemoveItemAsync(Guid itemId);
    Task ClearCartAsync(Guid cartId);
}