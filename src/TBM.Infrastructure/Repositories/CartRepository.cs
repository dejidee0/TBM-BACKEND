using Microsoft.EntityFrameworkCore;
using TBM.Core.Entities.Orders;
using TBM.Core.Interfaces.Repositories;
using TBM.Infrastructure.Data;

namespace TBM.Infrastructure.Repositories;

public class CartRepository : ICartRepository
{
    private readonly ApplicationDbContext _context;
    
    public CartRepository(ApplicationDbContext context)
    {
        _context = context;
    }
    
    public async Task<Cart?> GetByUserIdAsync(Guid userId)
    {
        return await _context.Carts
            .Include(c => c.Items)
                .ThenInclude(i => i.Product)
                    .ThenInclude(p => p.Images.OrderBy(img => img.DisplayOrder))
            .Include(c => c.Items)
                .ThenInclude(i => i.Product)
                    .ThenInclude(p => p.Category)
            .FirstOrDefaultAsync(c => c.UserId == userId);
    }
    
    public async Task<Cart?> GetByIdAsync(Guid id)
    {
        return await _context.Carts
            .Include(c => c.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(c => c.Id == id);
    }
    
    public async Task<Cart> CreateAsync(Cart cart)
    {
        await _context.Carts.AddAsync(cart);
        return cart;
    }
    
    public Task UpdateAsync(Cart cart)
    {
        _context.Carts.Update(cart);
        return Task.CompletedTask;
    }
    
    public async Task DeleteAsync(Guid id)
    {
        var cart = await _context.Carts.FindAsync(id);
        if (cart != null)
        {
            cart.IsDeleted = true;
            cart.DeletedAt = DateTime.UtcNow;
            _context.Carts.Update(cart);
        }
    }
    
    public async Task<CartItem?> GetCartItemAsync(Guid cartId, Guid productId)
    {
        return await _context.CartItems
            .FirstOrDefaultAsync(i => i.CartId == cartId && i.ProductId == productId);
    }
    
    public async Task AddItemAsync(CartItem item)
    {
        await _context.CartItems.AddAsync(item);
    }
    
    public Task UpdateItemAsync(CartItem item)
    {
        _context.CartItems.Update(item);
        return Task.CompletedTask;
    }
    
    public async Task RemoveItemAsync(Guid itemId)
    {
        var item = await _context.CartItems.FindAsync(itemId);
        if (item != null)
        {
            _context.CartItems.Remove(item);
        }
    }
    
    public async Task ClearCartAsync(Guid cartId)
    {
        var items = await _context.CartItems
            .Where(i => i.CartId == cartId)
            .ToListAsync();
        
        _context.CartItems.RemoveRange(items);
    }
}