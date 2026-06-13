using TBM.Application.DTOs.Checkout;
using TBM.Application.DTOs.Common;
using TBM.Application.DTOs.Orders;
using TBM.Application.Interfaces;
using TBM.Core.Entities.Users;
using TBM.Core.Enums;
using TBM.Core.Interfaces;

namespace TBM.Application.Services;

public class CheckoutService : ICheckoutService
{
    private const decimal ShippingFee = 5000m;
    private const decimal FreeShippingThreshold = 500000m;
    private const decimal TaxRate = 0.075m;
    private const decimal AmountTolerance = 1.00m;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IOrderService _orderService;
    private readonly IPromoService _promoService;
    private readonly AuditService _auditService;

    public CheckoutService(
        IUnitOfWork unitOfWork,
        IOrderService orderService,
        IPromoService promoService,
        AuditService auditService)
    {
        _unitOfWork = unitOfWork;
        _orderService = orderService;
        _promoService = promoService;
        _auditService = auditService;
    }

    public async Task<ApiResponse<CheckoutSummaryDto>> GetCheckoutSummaryAsync(Guid userId, string? promoCode = null)
    {
        var cart = await _unitOfWork.Carts.GetByUserIdAsync(userId);

        if (cart == null || !cart.Items.Any())
        {
            return ApiResponse<CheckoutSummaryDto>.ErrorResponse("Cart is empty");
        }

        var items = cart.Items.Select(i => new CheckoutItemDto
        {
            ProductId = i.ProductId,
            Name = i.Product.Name,
            UnitPrice = i.UnitPrice,
            Quantity = i.Quantity,
            Subtotal = i.UnitPrice * i.Quantity,
            Image = i.Product.Images.FirstOrDefault(img => img.IsPrimary)?.ImageUrl
                ?? i.Product.Images.FirstOrDefault()?.ImageUrl
        }).ToList();

        var subtotal = items.Sum(i => i.Subtotal);
        var shipping = CalculateShipping(subtotal);
        var tax = Math.Round(subtotal * TaxRate, 2, MidpointRounding.AwayFromZero);

        PromoValidationResultDto? promoResult = null;
        decimal discount = 0m;

        if (!string.IsNullOrWhiteSpace(promoCode))
        {
            promoResult = await _promoService.ValidateAsync(userId, subtotal, promoCode);
            if (!promoResult.Success)
            {
                return ApiResponse<CheckoutSummaryDto>.ErrorResponse(
                    promoResult.Message ?? "Promo code is invalid");
            }

            discount = promoResult.DiscountAmount;
        }

        var total = Math.Max(0, subtotal + shipping + tax - discount);
        var (addresses, defaultAddress) = await GetAddressDataAsync(userId);

        var summary = new CheckoutSummaryDto
        {
            Items = items,
            Subtotal = subtotal,
            Shipping = shipping,
            Tax = tax,
            Discount = discount,
            Total = total,
            SavedAddresses = addresses,
            DefaultAddress = defaultAddress,
            Promo = promoResult
        };

        return ApiResponse<CheckoutSummaryDto>.SuccessResponse(summary);
    }

    public async Task<ApiResponse<PromoValidationResultDto>> ValidatePromoAsync(Guid userId, string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return ApiResponse<PromoValidationResultDto>.ErrorResponse("Promo code is required");
        }

        var cart = await _unitOfWork.Carts.GetByUserIdAsync(userId);
        if (cart == null || !cart.Items.Any())
        {
            return ApiResponse<PromoValidationResultDto>.ErrorResponse("Cart is empty");
        }

        var subTotal = cart.Items.Sum(i => i.Quantity * i.UnitPrice);
        var result = await _promoService.ValidateAsync(userId, subTotal, code);

        if (!result.Success)
        {
            return ApiResponse<PromoValidationResultDto>.ErrorResponse(result.Message ?? "Promo code is invalid");
        }

        return ApiResponse<PromoValidationResultDto>.SuccessResponse(result, "Promo code is valid");
    }

    public async Task<ApiResponse<CheckoutPaymentResultDto>> ProcessPaymentAsync(
        Guid userId,
        CheckoutPaymentRequestDto dto,
        string? idempotencyKey = null)
    {
        var effectiveIdempotencyKey = ResolveIdempotencyKey(idempotencyKey, dto);
        if (string.IsNullOrWhiteSpace(effectiveIdempotencyKey))
        {
            return ApiResponse<CheckoutPaymentResultDto>.ErrorResponse(
                "Idempotency key is required. Provide Idempotency-Key header or payment reference.");
        }

        effectiveIdempotencyKey = effectiveIdempotencyKey.Trim();

        var existingOrder = await _unitOfWork.Orders.GetByPaymentReferenceAsync(effectiveIdempotencyKey, userId);
        if (existingOrder != null)
        {
            if (dto.Total > 0 && Math.Abs(existingOrder.Total - dto.Total) > AmountTolerance)
            {
                return ApiResponse<CheckoutPaymentResultDto>.ErrorResponse(
                    "Idempotency key was already used with a different payment amount.");
            }

            await _auditService.LogAsync(
                action: "Checkout.Payment.IdempotentReplay",
                category: "Commerce",
                oldValue: null,
                newValue: new
                {
                    userId,
                    orderId = existingOrder.Id,
                    idempotencyKey = effectiveIdempotencyKey
                });

            return ApiResponse<CheckoutPaymentResultDto>.SuccessResponse(
                new CheckoutPaymentResultDto
                {
                    Success = true,
                    OrderId = existingOrder.Id,
                    OrderNumber = existingOrder.OrderNumber,
                    Message = "Order already exists for this payment request.",
                    IsIdempotent = true
                });
        }

        var summaryResult = await GetCheckoutSummaryAsync(userId, dto.PromoCode);
        if (!summaryResult.Success || summaryResult.Data == null)
        {
            return ApiResponse<CheckoutPaymentResultDto>.ErrorResponse(summaryResult.Message);
        }

        if (dto.Total > 0 && Math.Abs(summaryResult.Data.Total - dto.Total) > AmountTolerance)
        {
            return ApiResponse<CheckoutPaymentResultDto>.ErrorResponse(
                $"Checkout amount mismatch. Expected {summaryResult.Data.Total:N2}, received {dto.Total:N2}.");
        }

        var delivery = BuildDelivery(dto.Delivery, summaryResult.Data.DefaultAddress);
        if (delivery == null)
        {
            return ApiResponse<CheckoutPaymentResultDto>.ErrorResponse(
                "Delivery details are incomplete and no default address is available.");
        }

        var createOrderDto = new CreateOrderDto
        {
            ShippingFullName = delivery.FullName!,
            ShippingPhone = delivery.Phone!,
            ShippingAddress = delivery.Address!,
            ShippingCity = delivery.City!,
            ShippingState = delivery.State!,
            ShippingNotes = delivery.Notes,
            CustomerNotes = dto.Delivery.CustomerNotes,
            PromoCode = dto.PromoCode,
            ShippingCost = summaryResult.Data.Shipping,
            Tax = summaryResult.Data.Tax,
            Discount = summaryResult.Data.Discount
        };

        var orderResult = await _orderService.CreateOrderAsync(userId, createOrderDto);
        if (!orderResult.Success || orderResult.Data == null)
        {
            return ApiResponse<CheckoutPaymentResultDto>.ErrorResponse(orderResult.Message);
        }

        var order = await _unitOfWork.Orders.GetByIdAsync(orderResult.Data.Id);
        if (order == null)
        {
            return ApiResponse<CheckoutPaymentResultDto>.ErrorResponse("Order was created but could not be retrieved.");
        }

        order.PaymentReference = effectiveIdempotencyKey;

        var paymentMethod = ParsePaymentMethod(dto.Payment.Method);
        if (paymentMethod.HasValue)
        {
            order.PaymentMethod = paymentMethod.Value;
        }

        await _unitOfWork.Orders.UpdateAsync(order);
        await _unitOfWork.SaveChangesAsync();

        await _auditService.LogAsync(
            action: "Checkout.Payment.Created",
            category: "Commerce",
            oldValue: null,
            newValue: new
            {
                userId,
                orderId = order.Id,
                orderNumber = order.OrderNumber,
                total = order.Total,
                idempotencyKey = effectiveIdempotencyKey,
                paymentMethod = order.PaymentMethod?.ToString()
            });

        return ApiResponse<CheckoutPaymentResultDto>.SuccessResponse(
            new CheckoutPaymentResultDto
            {
                Success = true,
                OrderId = order.Id,
                OrderNumber = order.OrderNumber,
                Message = "Checkout payment request accepted.",
                IsIdempotent = false
            },
            "Checkout payment completed successfully");
    }

    private async Task<(List<CheckoutAddressDto> addresses, CheckoutAddressDto? defaultAddress)> GetAddressDataAsync(Guid userId)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId);

        var addresses = user?.Addresses
            .Select(MapAddress)
            .ToList() ?? new List<CheckoutAddressDto>();

        var defaultAddress = addresses.FirstOrDefault(a => a.IsDefault) ?? addresses.FirstOrDefault();
        return (addresses, defaultAddress);
    }

    private static CheckoutAddressDto MapAddress(UserAddress address)
    {
        return new CheckoutAddressDto
        {
            Id = address.Id,
            FullName = address.FullName,
            Street = address.Street,
            City = address.City,
            State = address.State,
            PostalCode = address.PostalCode,
            Country = address.Country,
            Phone = address.Phone,
            DeliveryNotes = address.DeliveryNotes,
            IsDefault = address.IsDefault
        };
    }

    private static decimal CalculateShipping(decimal subtotal)
    {
        return subtotal >= FreeShippingThreshold ? 0m : ShippingFee;
    }

    private static string? ResolveIdempotencyKey(string? headerIdempotencyKey, CheckoutPaymentRequestDto dto)
    {
        if (!string.IsNullOrWhiteSpace(headerIdempotencyKey))
        {
            return headerIdempotencyKey;
        }

        if (!string.IsNullOrWhiteSpace(dto.IdempotencyKey))
        {
            return dto.IdempotencyKey;
        }

        return dto.Payment.Reference;
    }

    private static PaymentMethod? ParsePaymentMethod(string? method)
    {
        if (string.IsNullOrWhiteSpace(method))
        {
            return null;
        }

        return method.Trim().ToLowerInvariant() switch
        {
            "paystack" => PaymentMethod.Paystack,
            "flutterwave" => PaymentMethod.Flutterwave,
            "banktransfer" => PaymentMethod.BankTransfer,
            "bank_transfer" => PaymentMethod.BankTransfer,
            "bank-transfer" => PaymentMethod.BankTransfer,
            "cash" => PaymentMethod.Cash,
            _ => null
        };
    }

    private static CheckoutDeliveryDto? BuildDelivery(CheckoutDeliveryDto source, CheckoutAddressDto? fallback)
    {
        var fullName = FirstNonEmpty(source.FullName, fallback?.FullName);
        var phone = FirstNonEmpty(source.Phone, fallback?.Phone);
        var address = FirstNonEmpty(source.Address, fallback?.Street);
        var city = FirstNonEmpty(source.City, fallback?.City);
        var state = FirstNonEmpty(source.State, fallback?.State);

        if (string.IsNullOrWhiteSpace(fullName) ||
            string.IsNullOrWhiteSpace(phone) ||
            string.IsNullOrWhiteSpace(address) ||
            string.IsNullOrWhiteSpace(city) ||
            string.IsNullOrWhiteSpace(state))
        {
            return null;
        }

        return new CheckoutDeliveryDto
        {
            FullName = fullName,
            Phone = phone,
            Address = address,
            City = city,
            State = state,
            Notes = source.Notes ?? fallback?.DeliveryNotes
        };
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
    }
}
