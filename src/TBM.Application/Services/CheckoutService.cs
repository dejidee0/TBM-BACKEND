using System.Text.Json;
using Microsoft.Extensions.Logging;
using TBM.Application.DTOs.Checkout;
using TBM.Application.DTOs.Common;
using TBM.Application.DTOs.Orders;
using TBM.Application.Interfaces;
using TBM.Core.Entities.Payments;
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
    private const string PaystackInitializationEventType = "transaction.initialize";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IOrderService _orderService;
    private readonly IPromoService _promoService;
    private readonly AuditService _auditService;
    private readonly PaystackService _paystackService;
    private readonly ILogger<CheckoutService> _logger;

    public CheckoutService(
        IUnitOfWork unitOfWork,
        IOrderService orderService,
        IPromoService promoService,
        AuditService auditService,
        PaystackService paystackService,
        ILogger<CheckoutService> logger)
    {
        _unitOfWork = unitOfWork;
        _orderService = orderService;
        _promoService = promoService;
        _auditService = auditService;
        _paystackService = paystackService;
        _logger = logger;
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
        var paymentMethod = ParsePaymentMethod(dto.Payment.Method) ?? PaymentMethod.Paystack;
        var effectiveIdempotencyKey = ResolveIdempotencyKey(idempotencyKey, dto) ?? GeneratePaymentReference();
        effectiveIdempotencyKey = effectiveIdempotencyKey.Trim();

        var existingOrder = await _unitOfWork.Orders.GetByPaymentReferenceAsync(effectiveIdempotencyKey, userId);
        if (existingOrder != null)
        {
            if (dto.Total > 0 && Math.Abs(existingOrder.Total - dto.Total) > AmountTolerance)
            {
                return ApiResponse<CheckoutPaymentResultDto>.ErrorResponse(
                    "Idempotency key was already used with a different payment amount.");
            }

            if (paymentMethod == PaymentMethod.Paystack)
            {
                return await HandleExistingPaystackOrderAsync(existingOrder, userId, effectiveIdempotencyKey, dto.Payment.CallbackUrl);
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
                    IsIdempotent = true,
                    PaymentProvider = existingOrder.PaymentMethod?.ToString(),
                    PaymentReference = existingOrder.PaymentReference,
                    PaymentStatus = existingOrder.PaymentStatus.ToString()
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
        order.PaymentMethod = paymentMethod;

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

        if (paymentMethod == PaymentMethod.Paystack)
        {
            var initResult = await InitializePaystackForOrderAsync(order, userId, effectiveIdempotencyKey, dto.Payment.CallbackUrl);
            if (!initResult.Success)
            {
                return ApiResponse<CheckoutPaymentResultDto>.ErrorResponse(initResult.Message);
            }

            return ApiResponse<CheckoutPaymentResultDto>.SuccessResponse(
                BuildPaystackResult(order, initResult, isIdempotent: false),
                "Paystack payment initialized successfully.");
        }

        return ApiResponse<CheckoutPaymentResultDto>.SuccessResponse(
            new CheckoutPaymentResultDto
            {
                Success = true,
                OrderId = order.Id,
                OrderNumber = order.OrderNumber,
                Message = "Checkout payment request accepted.",
                IsIdempotent = false,
                PaymentProvider = paymentMethod.ToString(),
                PaymentReference = order.PaymentReference,
                PaymentStatus = order.PaymentStatus.ToString()
            },
            "Checkout payment completed successfully");
    }

    public async Task<ApiResponse<CheckoutPaymentResultDto>> VerifyPaystackPaymentAsync(Guid userId, string reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return ApiResponse<CheckoutPaymentResultDto>.ErrorResponse("Payment reference is required.");
        }

        reference = reference.Trim();

        var order = await _unitOfWork.Orders.GetByPaymentReferenceAsync(reference, userId);
        if (order == null)
        {
            return ApiResponse<CheckoutPaymentResultDto>.ErrorResponse("Order not found for this payment reference.");
        }

        if (order.PaymentStatus == PaymentStatus.Paid)
        {
            return ApiResponse<CheckoutPaymentResultDto>.SuccessResponse(
                new CheckoutPaymentResultDto
                {
                    Success = true,
                    OrderId = order.Id,
                    OrderNumber = order.OrderNumber,
                    Message = "Payment already verified.",
                    IsIdempotent = true,
                    PaymentProvider = PaymentMethod.Paystack.ToString(),
                    PaymentReference = order.PaymentReference,
                    PaymentStatus = order.PaymentStatus.ToString(),
                    PublicKey = _paystackService.GetPublicKey()
                });
        }

        var verificationResult = await _paystackService.VerifyTransactionAsync(reference);
        if (!verificationResult.Success)
        {
            return ApiResponse<CheckoutPaymentResultDto>.ErrorResponse(verificationResult.Message);
        }

        ApplyVerificationResultToOrder(order, verificationResult);

        await _unitOfWork.Orders.UpdateAsync(order);
        await _unitOfWork.SaveChangesAsync();

        await _auditService.LogAsync(
            action: "Checkout.Payment.Verified",
            category: "Commerce",
            oldValue: null,
            newValue: new
            {
                userId,
                orderId = order.Id,
                orderNumber = order.OrderNumber,
                paymentReference = reference,
                paymentStatus = order.PaymentStatus.ToString(),
                gatewayStatus = verificationResult.Status
            });

        return ApiResponse<CheckoutPaymentResultDto>.SuccessResponse(
            new CheckoutPaymentResultDto
            {
                Success = order.PaymentStatus == PaymentStatus.Paid,
                OrderId = order.Id,
                OrderNumber = order.OrderNumber,
                Message = verificationResult.Message,
                IsIdempotent = false,
                PaymentProvider = PaymentMethod.Paystack.ToString(),
                PaymentReference = order.PaymentReference,
                PaymentStatus = order.PaymentStatus.ToString(),
                PublicKey = _paystackService.GetPublicKey()
            });
    }

    private async Task<ApiResponse<CheckoutPaymentResultDto>> HandleExistingPaystackOrderAsync(
        TBM.Core.Entities.Orders.Order order,
        Guid userId,
        string reference,
        string? callbackUrl)
    {
        if (order.PaymentStatus == PaymentStatus.Paid)
        {
            return ApiResponse<CheckoutPaymentResultDto>.SuccessResponse(
                new CheckoutPaymentResultDto
                {
                    Success = true,
                    OrderId = order.Id,
                    OrderNumber = order.OrderNumber,
                    Message = "Order already paid.",
                    IsIdempotent = true,
                    PaymentProvider = PaymentMethod.Paystack.ToString(),
                    PaymentReference = order.PaymentReference,
                    PaymentStatus = order.PaymentStatus.ToString(),
                    PublicKey = _paystackService.GetPublicKey()
                });
        }

        var initializeEvent = await _unitOfWork.WebhookEvents
            .GetByReferenceAndEventTypeAsync(reference, PaystackInitializationEventType);

        if (initializeEvent != null &&
            TryReadInitializationSnapshot(initializeEvent.Payload, out var snapshot) &&
            !string.IsNullOrWhiteSpace(snapshot.AuthorizationUrl))
        {
            return ApiResponse<CheckoutPaymentResultDto>.SuccessResponse(
                new CheckoutPaymentResultDto
                {
                    Success = true,
                    OrderId = order.Id,
                    OrderNumber = order.OrderNumber,
                    Message = "Order already exists for this payment request.",
                    IsIdempotent = true,
                    PaymentProvider = PaymentMethod.Paystack.ToString(),
                    PaymentReference = snapshot.Reference,
                    PaymentStatus = order.PaymentStatus.ToString(),
                    AuthorizationUrl = snapshot.AuthorizationUrl,
                    AccessCode = snapshot.AccessCode,
                    PublicKey = snapshot.PublicKey
                });
        }

        var verifyResult = await _paystackService.VerifyTransactionAsync(reference);
        if (verifyResult.Success && verifyResult.Status.Equals("success", StringComparison.OrdinalIgnoreCase))
        {
            ApplyVerificationResultToOrder(order, verifyResult);
            await _unitOfWork.Orders.UpdateAsync(order);
            await _unitOfWork.SaveChangesAsync();

            return ApiResponse<CheckoutPaymentResultDto>.SuccessResponse(
                new CheckoutPaymentResultDto
                {
                    Success = true,
                    OrderId = order.Id,
                    OrderNumber = order.OrderNumber,
                    Message = "Payment verified successfully.",
                    IsIdempotent = true,
                    PaymentProvider = PaymentMethod.Paystack.ToString(),
                    PaymentReference = order.PaymentReference,
                    PaymentStatus = order.PaymentStatus.ToString(),
                    PublicKey = _paystackService.GetPublicKey()
                });
        }

        var initResult = await InitializePaystackForOrderAsync(order, userId, reference, callbackUrl);
        if (!initResult.Success)
        {
            return ApiResponse<CheckoutPaymentResultDto>.ErrorResponse(initResult.Message);
        }

        return ApiResponse<CheckoutPaymentResultDto>.SuccessResponse(
            BuildPaystackResult(order, initResult, isIdempotent: true),
            "Order already exists for this payment request.");
    }

    private async Task<PaystackInitializeResult> InitializePaystackForOrderAsync(
        TBM.Core.Entities.Orders.Order order,
        Guid userId,
        string reference,
        string? callbackUrl)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        if (user == null || string.IsNullOrWhiteSpace(user.Email))
        {
            return PaystackInitializeResult.Failure("Unable to resolve user email for Paystack payment.");
        }

        var initResult = await _paystackService.InitializeTransactionAsync(new PaystackInitializeRequest
        {
            Email = user.Email,
            Amount = order.Total,
            Reference = reference,
            CallbackUrl = callbackUrl,
            Currency = _paystackService.GetCurrency(),
            Metadata = new
            {
                orderId = order.Id,
                orderNumber = order.OrderNumber,
                userId
            }
        });

        if (!initResult.Success)
        {
            _logger.LogWarning(
                "Paystack initialization failed. Order={OrderNumber} Reference={Reference} Message={Message}",
                order.OrderNumber,
                reference,
                initResult.Message);
            return initResult;
        }

        order.PaymentReference = initResult.Reference;
        order.PaymentMethod = PaymentMethod.Paystack;

        await _unitOfWork.Orders.UpdateAsync(order);
        await SavePaystackInitializationSnapshotAsync(order, initResult);
        await _unitOfWork.SaveChangesAsync();

        await _auditService.LogAsync(
            action: "Checkout.Payment.PaystackInitialized",
            category: "Commerce",
            oldValue: null,
            newValue: new
            {
                userId,
                orderId = order.Id,
                orderNumber = order.OrderNumber,
                reference = initResult.Reference
            });

        return initResult;
    }

    private async Task SavePaystackInitializationSnapshotAsync(
        TBM.Core.Entities.Orders.Order order,
        PaystackInitializeResult initializeResult)
    {
        var snapshot = new PaystackInitializationSnapshot
        {
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            Reference = initializeResult.Reference,
            AuthorizationUrl = initializeResult.AuthorizationUrl,
            AccessCode = initializeResult.AccessCode,
            PublicKey = _paystackService.GetPublicKey(),
            CreatedAtUtc = DateTime.UtcNow
        };

        var existing = await _unitOfWork.WebhookEvents
            .GetByReferenceAndEventTypeAsync(initializeResult.Reference, PaystackInitializationEventType);

        if (existing == null)
        {
            await _unitOfWork.WebhookEvents.AddAsync(new WebhookEvent
            {
                Provider = "Paystack",
                EventType = PaystackInitializationEventType,
                Reference = initializeResult.Reference,
                Payload = JsonSerializer.Serialize(snapshot),
                Processed = true,
                ProcessedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            });
            return;
        }

        existing.Payload = JsonSerializer.Serialize(snapshot);
        existing.Processed = true;
        existing.ProcessedAt = DateTime.UtcNow;
    }

    private static bool TryReadInitializationSnapshot(string payload, out PaystackInitializationSnapshot snapshot)
    {
        snapshot = new PaystackInitializationSnapshot();

        try
        {
            var parsed = JsonSerializer.Deserialize<PaystackInitializationSnapshot>(payload);
            if (parsed == null || string.IsNullOrWhiteSpace(parsed.Reference))
            {
                return false;
            }

            snapshot = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static void ApplyVerificationResultToOrder(
        TBM.Core.Entities.Orders.Order order,
        PaystackVerificationResult verificationResult)
    {
        var status = verificationResult.Status.Trim().ToLowerInvariant();
        order.PaymentMethod = PaymentMethod.Paystack;
        order.PaymentReference = verificationResult.Reference;

        if (status == "success")
        {
            order.PaymentStatus = PaymentStatus.Paid;
            order.PaidAt ??= verificationResult.PaidAtUtc ?? DateTime.UtcNow;

            if (order.Status == OrderStatus.Pending)
            {
                order.Status = OrderStatus.PaymentReceived;
            }

            return;
        }

        if (status is "failed" or "abandoned" or "reversed")
        {
            order.PaymentStatus = PaymentStatus.Failed;
            return;
        }

        order.PaymentStatus = PaymentStatus.Pending;
    }

    private CheckoutPaymentResultDto BuildPaystackResult(
        TBM.Core.Entities.Orders.Order order,
        PaystackInitializeResult initializeResult,
        bool isIdempotent)
    {
        return new CheckoutPaymentResultDto
        {
            Success = true,
            OrderId = order.Id,
            OrderNumber = order.OrderNumber,
            Message = initializeResult.Message,
            IsIdempotent = isIdempotent,
            PaymentProvider = PaymentMethod.Paystack.ToString(),
            PaymentReference = initializeResult.Reference,
            PaymentStatus = order.PaymentStatus.ToString(),
            AuthorizationUrl = initializeResult.AuthorizationUrl,
            AccessCode = initializeResult.AccessCode,
            PublicKey = _paystackService.GetPublicKey()
        };
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

        if (!string.IsNullOrWhiteSpace(dto.Payment.Reference))
        {
            return dto.Payment.Reference;
        }

        return null;
    }

    private static string GeneratePaymentReference()
    {
        return $"TBM-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
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

    private sealed class PaystackInitializationSnapshot
    {
        public Guid OrderId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string Reference { get; set; } = string.Empty;
        public string? AuthorizationUrl { get; set; }
        public string? AccessCode { get; set; }
        public string? PublicKey { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }
}
