
namespace TBM.Application.DTOs.Settings;
public class AISettingsDto
{
    public int RateLimit { get; set; }
    public int TimeoutSeconds { get; set; }
    public List<AIModelDto> Models { get; set; } = new();
}

public class AIModelDto
{
    public string Id { get; set; } = null!;
    public bool Enabled { get; set; }
    public string ApiKey { get; set; } = null!;
    public int MaxTokens { get; set; }
}
