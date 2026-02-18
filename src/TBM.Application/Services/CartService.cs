using TBM.Application.DTOs.Common;
using TBM.Application.DTOs.Orders;
using TBM.Application.Interfaces;
using TBM.Core.Entities.Orders;
using TBM.Core.Interfaces;

namespace TBM.Application.Services;

public class CartService : ICartService
{
    private readonly IUnitOfWork _unitOfWork;
    
    public CartService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }
    
    public async Task<ApiResponse<CartDto>> GetCartAsync(Guid userId)
    {
        var cart = await _unitOfWork.Carts.GetByUserIdAsync(userId);
        
        if (cart == null)
        {
            // Create new cart if doesn't exist
            cart = new Cart
            {
                UserId = userId,
                ExpiresAt = DateTime.UtcNow.AddDays(30)
            };
            
            await _unitOfWork.Carts.CreateAsync(cart);
            await _unitOfWork.SaveChangesAsync();
        }
        
        return ApiResponse<CartDto>.SuccessResponse(MapCartToDto(cart));
    }
    
    public async Task<ApiResponse<CartDto>> AddToCartAsync(Guid userId, AddToCartDto dto)
    {
        // Validate quantity
        if (dto.Quantity <= 0)
        {
            return ApiResponse<CartDto>.ErrorResponse("Quantity must be greater than zero");
        }
        
        // Get or create cart
        var cart = await _unitOfWork.Carts.GetByUserIdAsync(userId);
        
        if (cart == null)
        {
            cart = new Cart
            {
                UserId = userId,
                ExpiresAt = DateTime.UtcNow.AddDays(30)
            };
            
            await _unitOfWork.Carts.CreateAsync(cart);
            await _unitOfWork.SaveChangesAsync();
            
            // Reload to get ID
            cart = await _unitOfWork.Carts.GetByUserIdAsync(userId);
        }
        
        // Get product
        var product = await _unitOfWork.Products.GetByIdAsync(dto.ProductId);
        
        if (product == null)
        {
            return ApiResponse<CartDto>.ErrorResponse("Product not found");
        }
        
        if (!product.IsActive)
        {
            return ApiResponse<CartDto>.ErrorResponse("Product is not available");
        }
        
        // Check stock for physical products
        if (product.TrackInventory && product.StockQuantity.HasValue)
        {
            if (product.StockQuantity.Value < dto.Quantity)
            {
                return ApiResponse<CartDto>.ErrorResponse($"Only {product.StockQuantity.Value} items available in stock");
            }
        }
        
        // Check if item already exists in cart
        var existingItem = await _unitOfWork.Carts.GetCartItemAsync(cart.Id, dto.ProductId);
        
        if (existingItem != null)
        {
            // Update quantity
            var newQuantity = existingItem.Quantity + dto.Quantity;
            
            // Check stock again for new quantity
            if (product.TrackInventory && product.StockQuantity.HasValue)
            {
                if (product.StockQuantity.Value < newQuantity)
                {
                    return ApiResponse<CartDto>.ErrorResponse($"Cannot add more items. Only {product.StockQuantity.Value} available in stock");
                }
            }
            
            existingItem.Quantity = newQuantity;
            await _unitOfWork.Carts.UpdateItemAsync(existingItem);
        }
        else
        {
            // Add new item
            var cartItem = new CartItem
            {
                CartId = cart.Id,
                ProductId = dto.ProductId,
                Quantity = dto.Quantity,
                UnitPrice = product.Price ?? 0,
                AddedAt = DateTime.UtcNow
            };
            
            await _unitOfWork.Carts.AddItemAsync(cartItem);
        }
        
        await _unitOfWork.SaveChangesAsync();
        
        // Reload cart with items
        cart = await _unitOfWork.Carts.GetByUserIdAsync(userId);
        
        return ApiResponse<CartDto>.SuccessResponse(
            MapCartToDto(cart!),
            "Item added to cart successfully"
        );
    }
    
    public async Task<ApiResponse<CartDto>> UpdateCartItemAsync(Guid userId, Guid itemId, UpdateCartItemDto dto)
    {
        if (dto.Quantity <= 0)
        {
            return ApiResponse<CartDto>.ErrorResponse("Quantity must be greater than zero");
        }
        
        var cart = await _unitOfWork.Carts.GetByUserIdAsync(userId);
        
        if (cart == null)
        {
            return ApiResponse<CartDto>.ErrorResponse("Cart not found");
        }
        
        var item = cart.Items.FirstOrDefault(i => i.Id == itemId);
        
        if (item == null)
        {
            return ApiResponse<CartDto>.ErrorResponse("Item not found in cart");
        }
        
        // Check stock
        var product = await _unitOfWork.Products.GetByIdAsync(item.ProductId);
        
        if (product != null && product.TrackInventory && product.StockQuantity.HasValue)
        {
            if (product.StockQuantity.Value < dto.Quantity)
            {
                return ApiResponse<CartDto>.ErrorResponse($"Only {product.StockQuantity.Value} items available in stock");
            }
        }
        
        item.Quantity = dto.Quantity;
        await _unitOfWork.Carts.UpdateItemAsync(item);
        await _unitOfWork.SaveChangesAsync();
        
        // Reload cart
        cart = await _unitOfWork.Carts.GetByUserIdAsync(userId);
        
        return ApiResponse<CartDto>.SuccessResponse(
            MapCartToDto(cart!),
            "Cart updated successfully"
        );
    }
    
    public async Task<ApiResponse<bool>> RemoveCartItemAsync(Guid userId, Guid itemId)
    {
        var cart = await _unitOfWork.Carts.GetByUserIdAsync(userId);
        
        if (cart == null)
        {
            return ApiResponse<bool>.ErrorResponse("Cart not found");
        }
        
        var item = cart.Items.FirstOrDefault(i => i.Id == itemId);
        
        if (item == null)
        {
            return ApiResponse<bool>.ErrorResponse("Item not found in cart");
        }
        
        await _unitOfWork.Carts.RemoveItemAsync(itemId);
        await _unitOfWork.SaveChangesAsync();
        
        return ApiResponse<bool>.SuccessResponse(true, "Item removed from cart");
    }
    
    public async Task<ApiResponse<bool>> ClearCartAsync(Guid userId)
    {
        var cart = await _unitOfWork.Carts.GetByUserIdAsync(userId);
        
        if (cart == null)
        {
            return ApiResponse<bool>.ErrorResponse("Cart not found");
        }
        
        await _unitOfWork.Carts.ClearCartAsync(cart.Id);
        await _unitOfWork.SaveChangesAsync();
        
        return ApiResponse<bool>.SuccessResponse(true, "Cart cleared successfully");
    }
    
    private CartDto MapCartToDto(Cart cart)
    {
        var items = cart.Items.Select(i => new CartItemDto
        {
            Id = i.Id,
            ProductId = i.ProductId,
            ProductName = i.Product.Name,
            ProductSKU = i.Product.SKU,
            ProductImageUrl = i.Product.Images.FirstOrDefault(img => img.IsPrimary)?.ImageUrl 
                ?? i.Product.Images.FirstOrDefault()?.ImageUrl,
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice,
            SubTotal = i.Quantity * i.UnitPrice,
            InStock = !i.Product.TrackInventory || (i.Product.StockQuantity ?? 0) > 0,
            StockQuantity = i.Product.StockQuantity,
            AddedAt = i.AddedAt
        }).ToList();
        
        return new CartDto
        {
            Id = cart.Id,
            UserId = cart.UserId,
            Items = items,
            TotalItems = items.Sum(i => i.Quantity),
            SubTotal = items.Sum(i => i.SubTotal),
            CreatedAt = cart.CreatedAt,
            UpdatedAt = cart.UpdatedAt ?? cart.CreatedAt
        };
    }
}