namespace TBM.Application.DTOs.Settings;

public class AdminNotificationSettingsDto
{
    public bool EmailEnabled { get; set; } = true;
    public bool SmsEnabled { get; set; }
    public bool PushEnabled { get; set; } = true;
    public bool WebhookEnabled { get; set; }
    public bool LowStockAlerts { get; set; } = true;
    public bool HighValueOrderAlerts { get; set; } = true;
    public bool PaymentFailureAlerts { get; set; } = true;
}

public class ToggleEnabledDto
{
    public bool Enabled { get; set; }
}
