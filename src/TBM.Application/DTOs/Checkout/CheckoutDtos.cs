namespace TBM.Application.DTOs.Checkout;

public class PromoValidationRequestDto
{
    public string Code { get; set; } = string.Empty;
}

public class PromoValidationResultDto
{
    public bool Success { get; set; }
    public string? Code { get; set; }
    public decimal Discount { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal DiscountAmount { get; set; }
    public string? Message { get; set; }
}

public class CheckoutItemDto
{
    public Guid ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal Subtotal { get; set; }
    public string? Image { get; set; }
}

public class CheckoutAddressDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? DeliveryNotes { get; set; }
    public bool IsDefault { get; set; }
}

public class CheckoutSummaryDto
{
    public List<CheckoutItemDto> Items { get; set; } = new();
    public decimal Subtotal { get; set; }
    public decimal Shipping { get; set; }
    public decimal Tax { get; set; }
    public decimal Discount { get; set; }
    public decimal Total { get; set; }
    public List<CheckoutAddressDto> SavedAddresses { get; set; } = new();
    public CheckoutAddressDto? DefaultAddress { get; set; }
    public PromoValidationResultDto? Promo { get; set; }
}

public class CheckoutDeliveryDto
{
    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Notes { get; set; }
    public string? CustomerNotes { get; set; }
}

public class CheckoutPaymentDetailsDto
{
    public string? Method { get; set; }
    public string? Reference { get; set; }
}

public class CheckoutPaymentRequestDto
{
    public CheckoutDeliveryDto Delivery { get; set; } = new();
    public CheckoutPaymentDetailsDto Payment { get; set; } = new();
    public decimal Total { get; set; }
    public string? PromoCode { get; set; }
    public string? IdempotencyKey { get; set; }
}

public class CheckoutPaymentResultDto
{
    public bool Success { get; set; }
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsIdempotent { get; set; }
}
