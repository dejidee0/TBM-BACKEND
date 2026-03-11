namespace TBM.Application.DTOs.Orders;

public class MergeCartRequestDto
{
    public List<MergeCartItemDto> Items { get; set; } = new();
    public List<MergeCartItemDto> GuestCartItems { get; set; } = new();
    public List<MergeCartItemDto> CartItems { get; set; } = new();
}

public class MergeCartItemDto
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; } = 1;
}

public class MergeCartWarningDto
{
    public Guid? ProductId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int? RequestedQuantity { get; set; }
    public int? AppliedQuantity { get; set; }
}

public class MergeCartResultDto
{
    public CartDto Cart { get; set; } = new();
    public List<MergeCartWarningDto> Warnings { get; set; } = new();
}
