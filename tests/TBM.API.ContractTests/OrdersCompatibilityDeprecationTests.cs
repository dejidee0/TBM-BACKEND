using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TBM.API.Controllers.V1;
using TBM.Application.DTOs.Common;
using TBM.Application.DTOs.Orders;
using TBM.Application.DTOs.Products;
using TBM.Application.Interfaces;

namespace TBM.API.ContractTests;

public sealed class OrdersCompatibilityDeprecationTests
{
    [Fact]
    public async Task ApiOrdersCompatibility_AddsDeprecationAndSunsetHeaders()
    {
        var userId = Guid.NewGuid();
        var service = new StubOrderService
        {
            UserOrdersResult = ApiResponse<List<OrderDto>>.SuccessResponse(new List<OrderDto>())
        };

        var controller = BuildController(service, userId);

        var result = await controller.GetMyOrdersCompatibility();

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal("true", controller.Response.Headers["Deprecation"].ToString());
        Assert.Equal("Tue, 30 Jun 2026 23:59:59 GMT", controller.Response.Headers["Sunset"].ToString());
    }

    [Fact]
    public async Task ApiOrderByIdCompatibility_AddsDeprecationAndSunsetHeaders()
    {
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var service = new StubOrderService
        {
            OrderByIdResult = ApiResponse<OrderDto>.SuccessResponse(new OrderDto
            {
                Id = orderId,
                UserId = userId,
                OrderNumber = "TBM0001"
            })
        };

        var controller = BuildController(service, userId);

        var result = await controller.GetOrderCompatibility(orderId);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal("true", controller.Response.Headers["Deprecation"].ToString());
        Assert.Equal("Tue, 30 Jun 2026 23:59:59 GMT", controller.Response.Headers["Sunset"].ToString());
    }

    private static OrdersController BuildController(IOrderService service, Guid userId)
    {
        var controller = new OrdersController(service);
        var context = new DefaultHttpContext();
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
            authenticationType: "Test");
        context.User = new ClaimsPrincipal(identity);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = context
        };

        return controller;
    }

    private sealed class StubOrderService : IOrderService
    {
        public ApiResponse<OrderDto> OrderByIdResult { get; set; } =
            ApiResponse<OrderDto>.ErrorResponse("not configured");

        public ApiResponse<List<OrderDto>> UserOrdersResult { get; set; } =
            ApiResponse<List<OrderDto>>.ErrorResponse("not configured");

        public Task<ApiResponse<OrderDto>> GetOrderByIdAsync(Guid orderId, Guid? userId = null) =>
            Task.FromResult(OrderByIdResult);

        public Task<ApiResponse<OrderDto>> GetOrderByNumberAsync(string orderNumber, Guid? userId = null) =>
            Task.FromResult(ApiResponse<OrderDto>.ErrorResponse("not implemented"));

        public Task<ApiResponse<PagedResultDto<OrderDto>>> GetOrdersAsync(OrderFilterDto filter) =>
            Task.FromResult(ApiResponse<PagedResultDto<OrderDto>>.ErrorResponse("not implemented"));

        public Task<ApiResponse<List<OrderDto>>> GetUserOrdersAsync(Guid userId) =>
            Task.FromResult(UserOrdersResult);

        public Task<ApiResponse<OrderDto>> CreateOrderAsync(Guid userId, CreateOrderDto dto) =>
            Task.FromResult(ApiResponse<OrderDto>.ErrorResponse("not implemented"));

        public Task<ApiResponse<OrderDto>> UpdateOrderStatusAsync(Guid orderId, UpdateOrderStatusDto dto) =>
            Task.FromResult(ApiResponse<OrderDto>.ErrorResponse("not implemented"));

        public Task<ApiResponse<OrderDto>> UpdatePaymentStatusAsync(Guid orderId, UpdatePaymentStatusDto dto) =>
            Task.FromResult(ApiResponse<OrderDto>.ErrorResponse("not implemented"));

        public Task<ApiResponse<bool>> CancelOrderAsync(Guid orderId, Guid userId, string reason) =>
            Task.FromResult(ApiResponse<bool>.ErrorResponse("not implemented"));
    }
}
