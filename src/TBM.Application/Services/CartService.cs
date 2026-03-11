using Microsoft.Extensions.Logging;
using TBM.Application.DTOs.Common;
using TBM.Application.DTOs.Orders;
using TBM.Application.Interfaces;
using TBM.Core.Entities.Orders;
using TBM.Core.Interfaces;

namespace TBM.Application.Services;

public class CartService : ICartService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CartService> _logger;
    
    public CartService(IUnitOfWork unitOfWork, ILogger<CartService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
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

    public async Task<ApiResponse<MergeCartResultDto>> MergeGuestCartAsync(Guid userId, MergeCartRequestDto dto)
    {
        _logger.LogInformation("MergeGuestCartAsync started for userId: {UserId}", userId);
        _logger.LogInformation("MergeGuestCartAsync dto: Items={ItemsCount}, GuestCartItems={GuestCount}, CartItems={CartCount}", 
            dto.Items?.Count ?? 0, dto.GuestCartItems?.Count ?? 0, dto.CartItems?.Count ?? 0);

        var warnings = new List<MergeCartWarningDto>();
        List<MergeCartItemDto> requestedItems = new();

        if (dto.Items?.Any() == true)
        {
            requestedItems = dto.Items;
        }
        else if (dto.GuestCartItems?.Any() == true)
        {
            requestedItems = dto.GuestCartItems;
        }
        else if (dto.CartItems?.Any() == true)
        {
            requestedItems = dto.CartItems;
        }

        _logger.LogInformation("Total requestedItems count: {Count}", requestedItems.Count);

        if (!requestedItems.Any())
        {
            var existingCartResult = await GetCartAsync(userId);
            if (!existingCartResult.Success || existingCartResult.Data == null)
            {
                return ApiResponse<MergeCartResultDto>.ErrorResponse(existingCartResult.Message);
            }

            return ApiResponse<MergeCartResultDto>.SuccessResponse(new MergeCartResultDto
            {
                Cart = existingCartResult.Data,
                Warnings = warnings
            });
        }

        try
        {
            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
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
                    cart = await _unitOfWork.Carts.GetByUserIdAsync(userId);
                }

                if (cart == null)
                {
                    throw new InvalidOperationException("Unable to initialize cart.");
                }

                var groupedItems = requestedItems
                    .Where(x => x.ProductId != Guid.Empty)
                    .GroupBy(x => x.ProductId)
                    .Select(group => new
                    {
                        ProductId = group.Key,
                        Quantity = group.Sum(x => x.Quantity)
                    })
                    .ToList();

                foreach (var item in groupedItems)
                {
                    if (item.Quantity <= 0)
                    {
                        warnings.Add(new MergeCartWarningDto
                        {
                            ProductId = item.ProductId,
                            Code = "INVALID_QUANTITY",
                            Message = "Quantity must be greater than zero.",
                            RequestedQuantity = item.Quantity
                        });
                        continue;
                    }

                    var product = await _unitOfWork.Products.GetByIdAsync(item.ProductId);
                    if (product == null || !product.IsActive)
                    {
                        warnings.Add(new MergeCartWarningDto
                        {
                            ProductId = item.ProductId,
                            Code = "PRODUCT_UNAVAILABLE",
                            Message = "Product is unavailable and was skipped.",
                            RequestedQuantity = item.Quantity
                        });
                        continue;
                    }

                    var existingItem = await _unitOfWork.Carts.GetCartItemAsync(cart.Id, item.ProductId);
                    var existingQuantity = existingItem?.Quantity ?? 0;
                    var requestedQuantity = item.Quantity;
                    var mergedQuantity = existingQuantity + requestedQuantity;
                    var appliedQuantity = mergedQuantity;

                    if (product.TrackInventory && product.StockQuantity.HasValue)
                    {
                        appliedQuantity = Math.Min(mergedQuantity, product.StockQuantity.Value);

                        if (appliedQuantity <= existingQuantity)
                        {
                            warnings.Add(new MergeCartWarningDto
                            {
                                ProductId = item.ProductId,
                                Code = "OUT_OF_STOCK",
                                Message = "No additional stock available for this product.",
                                RequestedQuantity = requestedQuantity,
                                AppliedQuantity = existingQuantity
                            });
                            continue;
                        }

                        if (appliedQuantity < mergedQuantity)
                        {
                            warnings.Add(new MergeCartWarningDto
                            {
                                ProductId = item.ProductId,
                                Code = "QUANTITY_CAPPED",
                                Message = $"Quantity capped by stock limit ({product.StockQuantity.Value}).",
                                RequestedQuantity = mergedQuantity,
                                AppliedQuantity = appliedQuantity
                            });
                        }
                    }

                    if (existingItem == null)
                    {
                        if (appliedQuantity <= 0)
                        {
                            continue;
                        }

                        await _unitOfWork.Carts.AddItemAsync(new CartItem
                        {
                            CartId = cart.Id,
                            ProductId = item.ProductId,
                            Quantity = appliedQuantity,
                            UnitPrice = product.Price ?? 0m,
                            AddedAt = DateTime.UtcNow
                        });
                    }
                    else
                    {
                        existingItem.Quantity = appliedQuantity;
                        existingItem.UnitPrice = product.Price ?? existingItem.UnitPrice;
                        await _unitOfWork.Carts.UpdateItemAsync(existingItem);
                    }
                }

                await _unitOfWork.SaveChangesAsync();
            });

            var mergedCart = await _unitOfWork.Carts.GetByUserIdAsync(userId);
            if (mergedCart == null)
            {
                return ApiResponse<MergeCartResultDto>.ErrorResponse("Unable to load merged cart.");
            }

            return ApiResponse<MergeCartResultDto>.SuccessResponse(
                new MergeCartResultDto
                {
                    Cart = MapCartToDto(mergedCart),
                    Warnings = warnings
                },
                "Guest cart merged successfully.");
        }
        catch (Exception ex)
        {
            return ApiResponse<MergeCartResultDto>.ErrorResponse($"Failed to merge guest cart: {ex.Message}");
        }
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
