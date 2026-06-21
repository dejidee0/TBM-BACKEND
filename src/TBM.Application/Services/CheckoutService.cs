using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TBM.Application.DTOs.Checkout;
using TBM.Application.DTOs.Common;
using TBM.Application.DTOs.Orders;
using TBM.Application.Interfaces;
using TBM.Application.Services.DesignFlow;
using TBM.Core.Entities.Orders;
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
    private readonly ProjectService _projectService;
    private readonly IPromoService _promoService;
    private readonly AuditService _auditService;
    private readonly PaystackService _paystackService;
    private readonly ILogger<CheckoutService> _logger;

    public CheckoutService(
        IUnitOfWork unitOfWork,
        IOrderService orderService,
        ProjectService projectService,
        IPromoService promoService,
        AuditService auditService,
        PaystackService paystackService,
        ILogger<CheckoutService> logger)
    {
        _unitOfWork = unitOfWork;
        _orderService = orderService;
        _projectService = projectService;
        _promoService = promoService;
        _auditService = auditService;
        _paystackService = paystackService;
        _logger = logger;
    }

    public async Task<ApiResponse<CheckoutSummaryDto>> GetCheckoutSummaryAsync(Guid userId, string? promoCode = null)
    {
        return await GetCheckoutSummaryAsync(userId, null, promoCode);
    }

    public async Task<ApiResponse<CheckoutSummaryDto>> GetCheckoutSummaryAsync(
        Guid? userId,
        string? guestSessionId,
        string? promoCode = null)
    {
        var cart = userId.HasValue
            ? await _unitOfWork.Carts.GetByUserIdAsync(userId.Value)
            : string.IsNullOrWhiteSpace(guestSessionId)
                ? null
                : await _unitOfWork.Carts.GetByGuestSessionIdAsync(guestSessionId.Trim());

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
            promoResult = await _promoService.ValidateAsync(userId ?? Guid.Empty, subtotal, promoCode);
            if (!promoResult.Success)
            {
                return ApiResponse<CheckoutSummaryDto>.ErrorResponse(
                    promoResult.Message ?? "Promo code is invalid");
            }

            discount = promoResult.DiscountAmount;
        }

        var total = Math.Max(0, subtotal + shipping + tax - discount);
        var (addresses, defaultAddress) = userId.HasValue
            ? await GetAddressDataAsync(userId.Value)
            : (new List<CheckoutAddressDto>(), null);

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
        return await ValidatePromoAsync(userId, null, code);
    }

    public async Task<ApiResponse<PromoValidationResultDto>> ValidatePromoAsync(Guid? userId, string? guestSessionId, string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return ApiResponse<PromoValidationResultDto>.ErrorResponse("Promo code is required");
        }

        var cart = userId.HasValue
            ? await _unitOfWork.Carts.GetByUserIdAsync(userId.Value)
            : string.IsNullOrWhiteSpace(guestSessionId)
                ? null
                : await _unitOfWork.Carts.GetByGuestSessionIdAsync(guestSessionId.Trim());
        if (cart == null || !cart.Items.Any())
        {
            return ApiResponse<PromoValidationResultDto>.ErrorResponse("Cart is empty");
        }

        var subTotal = cart.Items.Sum(i => i.Quantity * i.UnitPrice);
        var result = await _promoService.ValidateAsync(userId ?? Guid.Empty, subTotal, code);

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
        return await ProcessPaymentAsync((Guid?)userId, dto, idempotencyKey);
    }

    public async Task<ApiResponse<CheckoutPaymentResultDto>> ProcessPaymentAsync(
        Guid? userId,
        CheckoutPaymentRequestDto dto,
        string? idempotencyKey = null)
    {
        var paymentMethod = ParsePaymentMethod(dto.Payment.Method) ?? PaymentMethod.Paystack;
        var effectiveIdempotencyKey = ResolveIdempotencyKey(idempotencyKey, dto)
            ?? await BuildDeterministicReferenceAsync(userId, dto.GuestSessionId)
            ?? GeneratePaymentReference();
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
                return await HandleExistingPaystackOrderAsync(existingOrder, userId, effectiveIdempotencyKey, dto.Payment.CallbackUrl, dto.GuestEmail);
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

            if (existingOrder.PaymentStatus == PaymentStatus.Paid)
            {
                await _projectService.CreateProjectFromOrderAsync(existingOrder.Id);
            }

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

        var summaryResult = await GetCheckoutSummaryAsync(userId, dto.GuestSessionId, dto.PromoCode);
        if (!summaryResult.Success || summaryResult.Data == null)
        {
            return ApiResponse<CheckoutPaymentResultDto>.ErrorResponse(summaryResult.Message);
        }

        if (dto.Total > 0 && Math.Abs(summaryResult.Data.Total - dto.Total) > AmountTolerance)
        {
            return ApiResponse<CheckoutPaymentResultDto>.ErrorResponse(
                $"Checkout amount mismatch. Expected {summaryResult.Data.Total:N2}, received {dto.Total:N2}.");
        }

        var delivery = BuildDelivery(dto.Delivery, summaryResult.Data.DefaultAddress, dto.GuestPhone);
        if (delivery == null)
        {
            return ApiResponse<CheckoutPaymentResultDto>.ErrorResponse(
                "Delivery details are incomplete and no default address is available.");
        }

        var createOrderDto = new CreateOrderDto
        {
            DesignSessionId = dto.DesignSessionId,
            PaymentReference = effectiveIdempotencyKey,
            GuestEmail = dto.GuestEmail,
            GuestPhone = dto.GuestPhone,
            GuestSessionId = dto.GuestSessionId,
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

        // Detach every entity the CreateOrderAsync transaction left in the tracker
        // (Order, OrderItems, CartItems, DesignSession, etc.).
        // Reason: ExecuteInTransactionAsync clears the tracker at the START of the
        // transaction only, not at the end. Entities tracked after the commit can
        // be in inconsistent states that cause spurious DbUpdateConcurrencyException
        // when a later SaveChangesAsync flushes them unexpectedly.
        // After Clear(), GetByIdAsync reloads a guaranteed-Unchanged Order from DB.
        _unitOfWork.ClearChangeTracker();

        var order = await _unitOfWork.Orders.GetByIdAsync(orderResult.Data.Id);
        if (order == null)
        {
            return ApiResponse<CheckoutPaymentResultDto>.ErrorResponse("Order was created but could not be retrieved.");
        }

        // Audit BEFORE touching order properties.
        // Reason: setting any property on a tracked EF entity immediately marks it
        // as Modified via snapshot tracking. AuditService.LogAsync uses the same
        // DbContext and calls SaveChangesAsync, which would silently save the Modified
        // order. InitializePaystackForOrderAsync then saves it again → second UPDATE
        // on the same row → 0 rows affected → DbUpdateConcurrencyException.
        // Solution: log first (order is Unchanged → only AuditLog is saved), then
        // let each payment branch own exactly one save of the order.
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
                paymentMethod = paymentMethod.ToString()
            });

        if (paymentMethod == PaymentMethod.Paystack)
        {
            // InitializePaystackForOrderAsync sets PaymentReference + PaymentMethod
            // and calls SaveChangesAsync exactly once.
            var initResult = await InitializePaystackForOrderAsync(order, userId, effectiveIdempotencyKey, dto.Payment.CallbackUrl, dto.GuestEmail);
            if (!initResult.Success)
            {
                return ApiResponse<CheckoutPaymentResultDto>.ErrorResponse(initResult.Message);
            }

            return ApiResponse<CheckoutPaymentResultDto>.SuccessResponse(
                BuildPaystackResult(order, initResult, isIdempotent: false),
                "Paystack payment initialized successfully.");
        }

        try
        {
            order = await SaveOrderWithConcurrencyRetryAsync(
                order,
                freshOrder =>
                {
                    freshOrder.PaymentReference = effectiveIdempotencyKey;
                    freshOrder.PaymentMethod = paymentMethod;
                },
                "saving checkout payment info");
        }
        catch (DbUpdateConcurrencyException)
        {
            return ApiResponse<CheckoutPaymentResultDto>.ErrorResponse(
                "Concurrency error updating order payment. Please retry the request.");
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
            await _projectService.CreateProjectFromOrderAsync(order.Id);

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

        order = await SaveOrderWithConcurrencyRetryAsync(
            order,
            freshOrder => ApplyVerificationResultToOrder(freshOrder, verificationResult),
            "verifying Paystack payment");

        if (order.PaymentStatus == PaymentStatus.Paid)
        {
            await _projectService.CreateProjectFromOrderAsync(order.Id);
        }

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
        Guid? userId,
        string reference,
        string? callbackUrl,
        string? guestEmail)
    {
        if (order.PaymentStatus == PaymentStatus.Paid)
        {
            await _projectService.CreateProjectFromOrderAsync(order.Id);

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
            order = await SaveOrderWithConcurrencyRetryAsync(
                order,
                freshOrder => ApplyVerificationResultToOrder(freshOrder, verifyResult),
                "saving existing Paystack order verification");

            if (order.PaymentStatus == PaymentStatus.Paid)
            {
                await _projectService.CreateProjectFromOrderAsync(order.Id);
            }

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

        var initResult = await InitializePaystackForOrderAsync(order, userId, reference, callbackUrl, guestEmail);
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
        Guid? userId,
        string reference,
        string? callbackUrl,
        string? guestEmail)
    {
        var email = await ResolvePaymentEmailAsync(order, userId, guestEmail);
        if (string.IsNullOrWhiteSpace(email))
        {
            return PaystackInitializeResult.Failure("Unable to resolve user email for Paystack payment.");
        }

        var initResult = await _paystackService.InitializeTransactionAsync(new PaystackInitializeRequest
        {
            Email = email,
            Amount = order.Total,
            Reference = reference,
            CallbackUrl = callbackUrl,
            Currency = _paystackService.GetCurrency(),
            Metadata = new
            {
                orderId = order.Id,
                orderNumber = order.OrderNumber,
                userId,
                guestEmail = userId.HasValue ? null : email
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

        try
        {
            order = await SaveOrderWithConcurrencyRetryAsync(
                order,
                freshOrder =>
                {
                    freshOrder.PaymentReference = initResult.Reference;
                    freshOrder.PaymentMethod = PaymentMethod.Paystack;
                },
                "initializing Paystack for order",
                freshOrder => SavePaystackInitializationSnapshotAsync(freshOrder, initResult));
        }
        catch (DbUpdateConcurrencyException)
        {
            return PaystackInitializeResult.Failure(
                "Concurrency error initializing Paystack for order. Please retry the request.");
        }

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

    private async Task<Order> SaveOrderWithConcurrencyRetryAsync(
        Order order,
        Action<Order> applyChanges,
        string operation,
        Func<Order, Task>? beforeSave = null,
        int maxAttempts = 3)
    {
        var orderId = order.Id;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            applyChanges(order);
            order.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _unitOfWork.Orders.UpdateAsync(order);

                if (beforeSave != null)
                {
                    await beforeSave(order);
                }

                await _unitOfWork.SaveChangesAsync();
                return order;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Concurrency conflict while {Operation} for order {OrderId}, attempt {Attempt}.",
                    operation,
                    orderId,
                    attempt);

                if (attempt >= maxAttempts)
                {
                    throw;
                }

                await Task.Delay(100 * attempt);
                _unitOfWork.ClearChangeTracker();

                order = await _unitOfWork.Orders.GetByIdAsync(orderId)
                    ?? throw new InvalidOperationException($"Order {orderId} not found during concurrency retry.");
            }
        }

        throw new DbUpdateConcurrencyException($"Concurrency error while {operation} for order {orderId}.");
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

    // When the caller supplies no idempotency key / payment reference, derive a
    // stable one from the cart owner + cart id + current line items (including
    // each item's AddedAt). Two rapid submits of the SAME cart then resolve to
    // the same reference, so the existing-order check returns the first order
    // instead of creating a duplicate.
    //
    // Including AddedAt keeps a genuine re-order distinct: after an order is
    // placed the cart items are deleted, so re-adding the same products produces
    // new AddedAt values -> a different reference -> a new order. The previous
    // behaviour (a random GUID per request) made every retry look like a brand
    // new payment, which is the duplicate-order hole this closes.
    //
    // Returns null when there is no cart to hash; the caller then falls back to a
    // generated reference and the empty cart is reported downstream.
    private async Task<string?> BuildDeterministicReferenceAsync(Guid? userId, string? guestSessionId)
    {
        var cart = userId.HasValue
            ? await _unitOfWork.Carts.GetByUserIdAsync(userId.Value)
            : string.IsNullOrWhiteSpace(guestSessionId)
                ? null
                : await _unitOfWork.Carts.GetByGuestSessionIdAsync(guestSessionId.Trim());

        if (cart == null || !cart.Items.Any())
        {
            return null;
        }

        var identity = userId.HasValue
            ? $"u:{userId.Value}"
            : $"g:{guestSessionId?.Trim()}";

        var items = string.Join("|", cart.Items
            .OrderBy(i => i.ProductId)
            .Select(i => $"{i.ProductId}:{i.Quantity}:{i.UnitPrice}:{i.AddedAt.Ticks}"));

        var raw = $"{identity};cart:{cart.Id};items:{items}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));

        return $"TBM-CART-{hash[..32]}";
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

    private async Task<string?> ResolvePaymentEmailAsync(
        TBM.Core.Entities.Orders.Order order,
        Guid? userId,
        string? guestEmail)
    {
        if (userId.HasValue)
        {
            var user = await _unitOfWork.Users.GetByIdAsync(userId.Value);
            return user?.Email;
        }

        return FirstNonEmpty(guestEmail, order.GuestEmail);
    }

    private static CheckoutDeliveryDto? BuildDelivery(
        CheckoutDeliveryDto source,
        CheckoutAddressDto? fallback,
        string? guestPhone = null)
    {
        var fullName = FirstNonEmpty(source.FullName, fallback?.FullName);
        var phone = FirstNonEmpty(source.Phone, fallback?.Phone, guestPhone);
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
