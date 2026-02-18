using System.Text.Json.Serialization;

namespace TBM.Infrastructure.AI.Models
{
    public class ReplicatePredictionResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = default!;

        [JsonPropertyName("status")]
        public string Status { get; set; } = default!;

        [JsonPropertyName("output")]
        public object? Output { get; set; } //CHANGED: Use object instead of List<string>

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("logs")]
        public string? Logs { get; set; }
    }
}