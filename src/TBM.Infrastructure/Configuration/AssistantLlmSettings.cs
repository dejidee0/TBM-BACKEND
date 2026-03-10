namespace TBM.Infrastructure.Configuration;

public class AssistantLlmSettings
{
    public bool Enabled { get; set; } = false;
    public string Provider { get; set; } = "OpenAICompatible";
    public string BaseUrl { get; set; } = "https://api.openai.com";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-4.1-mini";
    public decimal Temperature { get; set; } = 0.2m;
    public int MaxOutputTokens { get; set; } = 900;
    public int TimeoutSeconds { get; set; } = 45;
}
