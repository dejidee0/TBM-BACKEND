using TBM.Application.DTOs.Common;
using TBM.Application.DTOs.Orders;
using TBM.Application.DTOs.Products;
using TBM.Application.Interfaces;
using TBM.Application.Services.DesignFlow;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TBM.Core.Entities.Orders;
using TBM.Core.Entities.DesignFlow;
using TBM.Core.Enums;
using TBM.Core.Interfaces;

namespace TBM.Application.Services;

public class OrderService : IOrderService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ProjectService _projectService;
    private readonly ILogger<OrderService> _logger;
    
    public OrderService(
        IUnitOfWork unitOfWork,
        ProjectService projectService,
        ILogger<OrderService> logger)
    {
        _unitOfWork = unitOfWork;
        _projectService = projectService;
        _logger = logger;
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
    
    public async Task<ApiResponse<OrderDto>> CreateOrderAsync(Guid? userId, CreateOrderDto dto)
    {
        var isGuest = !userId.HasValue;

        if (isGuest)
        {
            if (string.IsNullOrWhiteSpace(dto.GuestSessionId))
            {
                return ApiResponse<OrderDto>.ErrorResponse("Guest session is required");
            }

            if (string.IsNullOrWhiteSpace(dto.GuestEmail))
            {
                return ApiResponse<OrderDto>.ErrorResponse("Guest email is required");
            }

            if (string.IsNullOrWhiteSpace(dto.GuestPhone))
            {
                return ApiResponse<OrderDto>.ErrorResponse("Guest phone is required");
            }

            if (dto.DesignSessionId.HasValue)
            {
                return ApiResponse<OrderDto>.ErrorResponse("Design session checkout requires authentication");
            }
        }

        if (!string.IsNullOrWhiteSpace(dto.PaymentReference))
        {
            var existingOrder = await _unitOfWork.Orders.GetByPaymentReferenceAsync(dto.PaymentReference, userId);
            if (existingOrder != null)
            {
                _logger.LogWarning(
                    "Duplicate order creation attempt for payment reference {Reference}. Returning existing order {OrderId}",
                    dto.PaymentReference,
                    existingOrder.Id);

                return ApiResponse<OrderDto>.SuccessResponse(
                    MapOrderToDto(existingOrder),
                    "Order already exists for this payment reference");
            }
        }

        DesignSession? designSession = null;
        if (dto.DesignSessionId.HasValue)
        {
            designSession = await _unitOfWork.DesignSessions.GetByIdAsync(dto.DesignSessionId.Value);
            if (designSession == null || !userId.HasValue || designSession.UserId != userId.Value)
            {
                return ApiResponse<OrderDto>.ErrorResponse("Design session not found");
            }

            if (designSession.OrderId.HasValue)
            {
                var existingOrder = await _unitOfWork.Orders.GetByIdAsync(designSession.OrderId.Value);
                if (existingOrder != null)
                {
                    return ApiResponse<OrderDto>.SuccessResponse(
                        MapOrderToDto(existingOrder),
                        "Order already exists for this design session");
                }
            }
        }

        // Get cart
        var cart = userId.HasValue
            ? await _unitOfWork.Carts.GetByUserIdAsync(userId.Value)
            : await _unitOfWork.Carts.GetByGuestSessionIdAsync(dto.GuestSessionId!.Trim());
        
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
        
        // Calculate totals
        var subTotal = cart.Items.Sum(i => i.Quantity * i.UnitPrice);
        var shippingCost = Math.Max(0m, dto.ShippingCost ?? 0m);
        var tax = Math.Max(0m, dto.Tax ?? 0m);
        var discount = Math.Max(0m, dto.Discount ?? 0m);

        if (discount > subTotal + shippingCost + tax)
        {
            return ApiResponse<OrderDto>.ErrorResponse("Discount cannot exceed order amount");
        }

        var total = subTotal + shippingCost + tax - discount;

        var customerNotes = dto.CustomerNotes;
        if (!string.IsNullOrWhiteSpace(dto.PromoCode))
        {
            customerNotes = string.IsNullOrWhiteSpace(customerNotes)
                ? $"Promo: {dto.PromoCode.Trim().ToUpperInvariant()}"
                : $"{customerNotes}{Environment.NewLine}Promo: {dto.PromoCode.Trim().ToUpperInvariant()}";
        }

        var orderId = await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            // Generate order number
            var orderNumber = await _unitOfWork.Orders.GenerateOrderNumberAsync();

            // Create order
            var order = new Order
            {
                OrderNumber = orderNumber,
                UserId = userId,
                DesignSessionId = dto.DesignSessionId,
                IsGuestOrder = isGuest,
                GuestEmail = isGuest ? dto.GuestEmail!.Trim() : null,
                GuestPhone = isGuest ? dto.GuestPhone!.Trim() : null,
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
                CustomerNotes = customerNotes,
                // Persist the payment reference at creation so a duplicate submit
                // (same deterministic key) is matched by GetByPaymentReferenceAsync
                // immediately, rather than only after the payment provider step
                // sets it later.
                PaymentReference = string.IsNullOrWhiteSpace(dto.PaymentReference)
                    ? null
                    : dto.PaymentReference.Trim()
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

                // Update product stock using atomic operation to avoid concurrency issues
                if (cartItem.Product.TrackInventory)
                {
                    var rowsAffected = await _unitOfWork.Products.UpdateStockAtomicAsync(
                        cartItem.ProductId,
                        cartItem.Quantity);
                    
                    if (rowsAffected == 0)
                    {
                        // Stock update failed - product may not exist, not track inventory, or have insufficient stock
                        throw new InvalidOperationException(
                            $"Failed to update stock for product '{cartItem.Product.Name}'. The product may no longer be available or has insufficient stock.");
                    }
                }
            }

            await _unitOfWork.SaveChangesAsync();

            if (designSession != null)
            {
                designSession.OrderId = order.Id;
                designSession.Status = DesignSessionStatus.Ordered;
                designSession.CurrentStep = "Awaiting payment";

                if (designSession.BOMId.HasValue)
                {
                    var bom = await _unitOfWork.BillsOfMaterials.GetByIdAsync(designSession.BOMId.Value);
                    if (bom != null)
                    {
                        bom.Status = BillOfMaterialsStatus.Ordered;
                        await _unitOfWork.BillsOfMaterials.UpdateAsync(bom);
                    }
                }

                await _unitOfWork.DesignSessions.UpdateAsync(designSession);
                await _unitOfWork.SaveChangesAsync();
            }

            // Clear cart
            try
            {
                await _unitOfWork.Carts.ClearCartAsync(cart.Id);
                await _unitOfWork.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Concurrency exception while clearing cart {CartId} for customer {CustomerId}. Cart may already be empty.",
                    cart.Id,
                    userId?.ToString() ?? dto.GuestSessionId);
            }

            return order.Id;
        });

        // Reload order with all details
        var createdOrder = await _unitOfWork.Orders.GetByIdAsync(orderId);

        return ApiResponse<OrderDto>.SuccessResponse(
            MapOrderToDto(createdOrder!),
            "Order created successfully"
        );
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

        if (order.PaymentStatus == PaymentStatus.Paid)
        {
            await _projectService.CreateProjectFromOrderAsync(order.Id);
        }
        
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
            DesignSessionId = order.DesignSessionId,
            GuestEmail = order.GuestEmail,
            GuestPhone = order.GuestPhone,
            IsGuestOrder = order.IsGuestOrder,
            UserEmail = order.User?.Email ?? order.GuestEmail ?? string.Empty,
            UserFullName = order.User == null
                ? order.ShippingFullName
                : $"{order.User.FirstName} {order.User.LastName}",
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
