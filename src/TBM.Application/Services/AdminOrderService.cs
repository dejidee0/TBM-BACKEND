using TBM.Application.Common;
using TBM.Application.DTOs.Admin;
using TBM.Application.DTOs.Common;
using TBM.Core.Entities.Orders;
using TBM.Core.Enums;
using TBM.Core.Interfaces;

namespace TBM.Application.Services;

public class AdminOrderService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaystackService _paystackService;

    public AdminOrderService(
        IUnitOfWork unitOfWork,
        PaystackService paystackService)
    {
        _unitOfWork = unitOfWork;
        _paystackService = paystackService;
    }

    public async Task<PagedResult<AdminOrderListDto>> GetOrdersAsync(
        int page,
        int pageSize,
        OrderStatus? status,
        string? search)
    {
        var (items, totalCount) = await _unitOfWork.Orders.GetPagedAsync(
            page,
            pageSize,
            null,       // userId
            status,
            null,       // paymentStatus
            null,
            null,
            search
        );

        var mapped = items.Select(o => new AdminOrderListDto
        {
            Id = o.Id,
            OrderNumber = o.OrderNumber,
            CustomerName = o.User.FirstName + " " + o.User.LastName,
            Total = o.Total,
            Status = o.Status.ToString(),
            CreatedAt = o.CreatedAt
        }).ToList();

        return new PagedResult<AdminOrderListDto>
        {
            Items = mapped,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task UpdateStatusAsync(
        Guid orderId,
        OrderStatus newStatus,
        Guid adminId,
        string? note)
    {
        var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
        if (order == null)
            throw new Exception("Order not found");

        var oldStatus = order.Status;

        if (!OrderStatusValidator.CanTransition(oldStatus, newStatus))
            throw new Exception($"Invalid transition from {oldStatus} to {newStatus}");

        order.Status = newStatus;

        if (newStatus == OrderStatus.Cancelled)
        {
            order.CancelledAt = DateTime.UtcNow;
            order.CancellationReason = note;
        }

        await _unitOfWork.OrderStatusHistories.AddAsync(new OrderStatusHistory
        {
            OrderId = orderId,
            OldStatus = oldStatus.ToString(),
            NewStatus = newStatus.ToString(),
            UpdatedBy = adminId.ToString(),
            Note = note
        });

        await _unitOfWork.SaveChangesAsync();
    }

    public async Task CancelOrderAsync(Guid orderId, Guid adminId, string reason)
    {
        await UpdateStatusAsync(orderId, OrderStatus.Cancelled, adminId, reason);
    }

    public async Task RefundOrderAsync(Guid orderId, Guid adminId, string reason)
    {
        var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
        if (order == null)
            throw new Exception("Order not found");

        if (order.Status != OrderStatus.Completed)
            throw new Exception("Only completed orders can be refunded");

        if (string.IsNullOrEmpty(order.PaymentReference))
            throw new Exception("No payment reference found");

        var success = await _paystackService.RefundAsync(
            order.PaymentReference,
            order.Total);

        if (!success)
            throw new Exception("Refund failed via Paystack");

        await UpdateStatusAsync(orderId, OrderStatus.Refunded, adminId, reason);
    }

    public async Task UpdateTrackingAsync(Guid orderId, string trackingNumber)
    {
        var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
        if (order == null)
            throw new Exception("Order not found");

        order.TrackingNumber = trackingNumber;
        order.ShippedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync();
    }
}
