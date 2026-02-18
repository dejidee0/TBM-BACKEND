

namespace TBM.Application.DTOs.Settings;
public class PaymentSettingsDto
{
    public decimal BasePlatformFee { get; set; }
    public decimal FixedFeePerTransaction { get; set; }
    public string DefaultCurrency { get; set; } = "USD";
    public List<PaymentGatewayDto> Gateways { get; set; } = new();
}

public class PaymentGatewayDto
{
    public string Id { get; set; } = null!;
    public bool Enabled { get; set; }
    public string PublicKey { get; set; } = null!;
    public string SecretKey { get; set; } = null!;
}
