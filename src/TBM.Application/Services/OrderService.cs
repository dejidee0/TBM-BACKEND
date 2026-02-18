using TBM.Application.DTOs.Common;
using TBM.Application.DTOs.Orders;
using TBM.Application.DTOs.Products;
using TBM.Application.Interfaces;
using TBM.Core.Entities.Orders;
using TBM.Core.Enums;
using TBM.Core.Interfaces;

namespace TBM.Application.Services;

public class OrderService : IOrderService
{
    private readonly IUnitOfWork _unitOfWork;
    
    public OrderService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }
    
    public async Task<ApiResponse<OrderDto>> GetOrderByIdAsync(Guid orderId, Guid? userId = null)
    {
        var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
        
        if (order == null)
        {
            return ApiResponse<OrderDto>.ErrorResponse("Order not found");
        }
        
        // If userId is provided, ensure user owns the order
        if (userId.HasValue && order.UserId != userId.Value)
        {
            return ApiResponse<OrderDto>.ErrorResponse("Unauthorized access to order");
        }
        
        return ApiResponse<OrderDto>.SuccessResponse(MapOrderToDto(order));
    }
    
    public async Task<ApiResponse<OrderDto>> GetOrderByNumberAsync(string orderNumber, Guid? userId = null)
    {
        var order = await _unitOfWork.Orders.GetByOrderNumberAsync(orderNumber);
        
        if (order == null)
        {
            return ApiResponse<OrderDto>.ErrorResponse("Order not found");
        }
        
        // If userId is provided, ensure user owns the order
        if (userId.HasValue && order.UserId != userId.Value)
        {
            return ApiResponse<OrderDto>.ErrorResponse("Unauthorized access to order");
        }
        
        return ApiResponse<OrderDto>.SuccessResponse(MapOrderToDto(order));
    }
    
    public async Task<ApiResponse<PagedResultDto<OrderDto>>> GetOrdersAsync(OrderFilterDto filter)
    {
        var (items, totalCount) = await _unitOfWork.Orders.GetPagedAsync(
            filter.PageNumber,
            filter.PageSize,
            filter.UserId,
            filter.Status.HasValue ? (OrderStatus)filter.Status.Value : null,
            filter.PaymentStatus.HasValue ? (PaymentStatus)filter.PaymentStatus.Value : null,
            filter.FromDate,
            filter.ToDate,
            filter.SearchTerm
        );
        
        var result = new PagedResultDto<OrderDto>
        {
            Items = items.Select(MapOrderToDto).ToList(),
            TotalCount = totalCount,
            PageNumber = filter.PageNumber,
            PageSize = filter.PageSize
        };
        
        return ApiResponse<PagedResultDto<OrderDto>>.SuccessResponse(result);
    }
    
    public async Task<ApiResponse<List<OrderDto>>> GetUserOrdersAsync(Guid userId)
    {
        var orders = await _unitOfWork.Orders.GetUserOrdersAsync(userId);
        var orderDtos = orders.Select(MapOrderToDto).ToList();
        
        return ApiResponse<List<OrderDto>>.SuccessResponse(orderDtos);
    }
    
    public async Task<ApiResponse<OrderDto>> CreateOrderAsync(Guid userId, CreateOrderDto dto)
    {
        // Get user's cart
        var cart = await _unitOfWork.Carts.GetByUserIdAsync(userId);
        
        if (cart == null || !cart.Items.Any())
        {
            return ApiResponse<OrderDto>.ErrorResponse("Cart is empty");
        }
        
        // Validate all items are still available and in stock
        foreach (var item in cart.Items)
        {
            var product = await _unitOfWork.Products.GetByIdAsync(item.ProductId);
            
            if (product == null || !product.IsActive)
            {
                return ApiResponse<OrderDto>.ErrorResponse($"Product '{item.Product.Name}' is no longer available");
            }
            
            if (product.TrackInventory && product.StockQuantity.HasValue)
            {
                if (product.StockQuantity.Value < item.Quantity)
                {
                    return ApiResponse<OrderDto>.ErrorResponse(
                        $"Insufficient stock for '{product.Name}'. Only {product.StockQuantity.Value} available"
                    );
                }
            }
        }
        
        // Begin transaction
        await _unitOfWork.BeginTransactionAsync();
        
        try
        {
            // Generate order number
            var orderNumber = await _unitOfWork.Orders.GenerateOrderNumberAsync();
            
            // Calculate totals
            var subTotal = cart.Items.Sum(i => i.Quantity * i.UnitPrice);
            var shippingCost = 0m; // TODO: Implement shipping calculation
            var tax = 0m; // TODO: Implement tax calculation
            var discount = 0m;
            var total = subTotal + shippingCost + tax - discount;
            
            // Create order
            var order = new Order
            {
                OrderNumber = orderNumber,
                UserId = userId,
                Status = OrderStatus.Pending,
                PaymentStatus = PaymentStatus.Pending,
                SubTotal = subTotal,
                ShippingCost = shippingCost,
                Tax = tax,
                Discount = discount,
                Total = total,
                ShippingFullName = dto.ShippingFullName,
                ShippingPhone = dto.ShippingPhone,
                ShippingAddress = dto.ShippingAddress,
                ShippingCity = dto.ShippingCity,
                ShippingState = dto.ShippingState,
                ShippingNotes = dto.ShippingNotes,
                CustomerNotes = dto.CustomerNotes
            };
            
            await _unitOfWork.Orders.CreateAsync(order);
            await _unitOfWork.SaveChangesAsync();
            
            // Create order items
            foreach (var cartItem in cart.Items)
            {
                var orderItem = new OrderItem
                {
                    OrderId = order.Id,
                    ProductId = cartItem.ProductId,
                    ProductName = cartItem.Product.Name,
                    ProductSKU = cartItem.Product.SKU,
                    ProductImageUrl = cartItem.Product.Images.FirstOrDefault(i => i.IsPrimary)?.ImageUrl
                        ?? cartItem.Product.Images.FirstOrDefault()?.ImageUrl,
                    Quantity = cartItem.Quantity,
                    UnitPrice = cartItem.UnitPrice,
                    SubTotal = cartItem.Quantity * cartItem.UnitPrice
                };
                
                order.Items.Add(orderItem);
                
                // Update product stock
                if (cartItem.Product.TrackInventory)
                {
                    await _unitOfWork.Products.UpdateStockAsync(
                        cartItem.ProductId,
                        (cartItem.Product.StockQuantity ?? 0) - cartItem.Quantity
                    );
                }
            }
            
            await _unitOfWork.SaveChangesAsync();
            
            // Clear cart
            await _unitOfWork.Carts.ClearCartAsync(cart.Id);
            await _unitOfWork.SaveChangesAsync();
            
            // Commit transaction
            await _unitOfWork.CommitTransactionAsync();
            
            // Reload order with all details
            order = await _unitOfWork.Orders.GetByIdAsync(order.Id);
            
            return ApiResponse<OrderDto>.SuccessResponse(
                MapOrderToDto(order!),
                "Order created successfully"
            );
        }
        catch (Exception)
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }
    
    public async Task<ApiResponse<OrderDto>> UpdateOrderStatusAsync(Guid orderId, UpdateOrderStatusDto dto)
    {
        var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
        
        if (order == null)
        {
            return ApiResponse<OrderDto>.ErrorResponse("Order not found");
        }
        
        var newStatus = (OrderStatus)dto.Status;
        
        // Validate status transition
        if (!IsValidStatusTransition(order.Status, newStatus))
        {
            return ApiResponse<OrderDto>.ErrorResponse($"Cannot change status from {order.Status} to {newStatus}");
        }
        
        order.Status = newStatus;
        order.AdminNotes = dto.AdminNotes;
        
        if (newStatus == OrderStatus.Shipped && !string.IsNullOrWhiteSpace(dto.TrackingNumber))
        {
            order.TrackingNumber = dto.TrackingNumber;
            order.ShippedAt = DateTime.UtcNow;
        }
        
        if (newStatus == OrderStatus.Delivered)
        {
            order.DeliveredAt = DateTime.UtcNow;
        }
        
        if (newStatus == OrderStatus.Cancelled)
        {
            order.CancelledAt = DateTime.UtcNow;
        }
        
        await _unitOfWork.Orders.UpdateAsync(order);
        await _unitOfWork.SaveChangesAsync();
        
        // Reload
        order = await _unitOfWork.Orders.GetByIdAsync(orderId);
        
        return ApiResponse<OrderDto>.SuccessResponse(
            MapOrderToDto(order!),
            "Order status updated successfully"
        );
    }
    
    public async Task<ApiResponse<OrderDto>> UpdatePaymentStatusAsync(Guid orderId, UpdatePaymentStatusDto dto)
    {
        var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
        
        if (order == null)
        {
            return ApiResponse<OrderDto>.ErrorResponse("Order not found");
        }
        
        order.PaymentStatus = (PaymentStatus)dto.PaymentStatus;
        
        if (dto.PaymentMethod.HasValue)
        {
            order.PaymentMethod = (PaymentMethod)dto.PaymentMethod.Value;
        }
        
        if (!string.IsNullOrWhiteSpace(dto.PaymentReference))
        {
            order.PaymentReference = dto.PaymentReference;
        }
        
        if (order.PaymentStatus == PaymentStatus.Paid && order.PaidAt == null)
        {
            order.PaidAt = DateTime.UtcNow;
            
            // Automatically move to Processing if payment is confirmed
            if (order.Status == OrderStatus.Pending)
            {
                order.Status = OrderStatus.PaymentReceived;
            }
        }
        
        await _unitOfWork.Orders.UpdateAsync(order);
        await _unitOfWork.SaveChangesAsync();
        
        // Reload
        order = await _unitOfWork.Orders.GetByIdAsync(orderId);
        
        return ApiResponse<OrderDto>.SuccessResponse(
            MapOrderToDto(order!),
            "Payment status updated successfully"
        );
    }
    
    public async Task<ApiResponse<bool>> CancelOrderAsync(Guid orderId, Guid userId, string reason)
    {
        var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
        
        if (order == null)
        {
            return ApiResponse<bool>.ErrorResponse("Order not found");
        }
        
        if (order.UserId != userId)
        {
            return ApiResponse<bool>.ErrorResponse("Unauthorized access to order");
        }
        
        // Can only cancel if status is Pending or PaymentReceived
        if (order.Status != OrderStatus.Pending && order.Status != OrderStatus.PaymentReceived)
        {
            return ApiResponse<bool>.ErrorResponse("Order cannot be cancelled at this stage");
        }
        
        order.Status = OrderStatus.Cancelled;
        order.CancelledAt = DateTime.UtcNow;
        order.CancellationReason = reason;
        
        // Restore stock
        foreach (var item in order.Items)
        {
            var product = await _unitOfWork.Products.GetByIdAsync(item.ProductId);
            if (product != null && product.TrackInventory)
            {
                await _unitOfWork.Products.UpdateStockAsync(
                    item.ProductId,
                    (product.StockQuantity ?? 0) + item.Quantity
                );
            }
        }
        
        await _unitOfWork.Orders.UpdateAsync(order);
        await _unitOfWork.SaveChangesAsync();
        
        return ApiResponse<bool>.SuccessResponse(true, "Order cancelled successfully");
    }
    
    private bool IsValidStatusTransition(OrderStatus currentStatus, OrderStatus newStatus)
    {
        // Define valid transitions
        return (currentStatus, newStatus) switch
        {
            (OrderStatus.Pending, OrderStatus.PaymentReceived) => true,
            (OrderStatus.Pending, OrderStatus.Cancelled) => true,
            (OrderStatus.PaymentReceived, OrderStatus.Processing) => true,
            (OrderStatus.PaymentReceived, OrderStatus.Cancelled) => true,
            (OrderStatus.Processing, OrderStatus.Shipped) => true,
            (OrderStatus.Shipped, OrderStatus.Delivered) => true,
            _ => currentStatus == newStatus // Allow same status update
        };
    }
    
    private OrderDto MapOrderToDto(Order order)
    {
        return new OrderDto
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            UserId = order.UserId,
            UserEmail = order.User.Email,
            UserFullName = $"{order.User.FirstName} {order.User.LastName}",
            Status = (int)order.Status,
            StatusName = order.Status.ToString(),
            PaymentStatus = (int)order.PaymentStatus,
            PaymentStatusName = order.PaymentStatus.ToString(),
            PaymentMethod = order.PaymentMethod.HasValue ? (int)order.PaymentMethod.Value : null,
            PaymentMethodName = order.PaymentMethod?.ToString(),
            SubTotal = order.SubTotal,
            ShippingCost = order.ShippingCost,
            Tax = order.Tax,
            Discount = order.Discount,
            Total = order.Total,
            ShippingFullName = order.ShippingFullName,
            ShippingPhone = order.ShippingPhone,
            ShippingAddress = order.ShippingAddress,
            ShippingCity = order.ShippingCity,
            ShippingState = order.ShippingState,
            ShippingNotes = order.ShippingNotes,
            PaymentReference = order.PaymentReference,
            PaidAt = order.PaidAt,
            TrackingNumber = order.TrackingNumber,
            ShippedAt = order.ShippedAt,
            DeliveredAt = order.DeliveredAt,
            CancelledAt = order.CancelledAt,
            CancellationReason = order.CancellationReason,
            CustomerNotes = order.CustomerNotes,
            AdminNotes = order.AdminNotes,
            Items = order.Items.Select(i => new OrderItemDto
            {
                Id = i.Id,
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                ProductSKU = i.ProductSKU,
                ProductImageUrl = i.ProductImageUrl,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                SubTotal = i.SubTotal
            }).ToList(),
            CreatedAt = order.CreatedAt,
            UpdatedAt = order.UpdatedAt ?? order.CreatedAt
        };
    }
}