
namespace TBM.Application.DTOs.Settings;
public class GeneralSettingsDto
{
    public string PlatformName { get; set; } = null!;
    public string SupportEmail { get; set; } = null!;
    public bool MaintenanceMode { get; set; }
    public int ApiRateLimit { get; set; }
}
