using TBM.Application.DTOs.Common;
using TBM.Application.DTOs.Orders;
using TBM.Application.DTOs.Products;

namespace TBM.Application.Interfaces;

public interface IOrderService
{
    Task<ApiResponse<OrderDto>> GetOrderByIdAsync(Guid orderId, Guid? userId = null);
    Task<ApiResponse<OrderDto>> GetOrderByNumberAsync(string orderNumber, Guid? userId = null);
    Task<ApiResponse<PagedResultDto<OrderDto>>> GetOrdersAsync(OrderFilterDto filter);
    Task<ApiResponse<List<OrderDto>>> GetUserOrdersAsync(Guid userId);
    Task<ApiResponse<OrderDto>> CreateOrderAsync(Guid userId, CreateOrderDto dto);
    Task<ApiResponse<OrderDto>> UpdateOrderStatusAsync(Guid orderId, UpdateOrderStatusDto dto);
    Task<ApiResponse<OrderDto>> UpdatePaymentStatusAsync(Guid orderId, UpdatePaymentStatusDto dto);
    Task<ApiResponse<bool>> CancelOrderAsync(Guid orderId, Guid userId, string reason);
}