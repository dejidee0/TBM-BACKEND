namespace TBM.Core.Models.AI
{
    public class AIProviderResult
    {
        public bool Success { get; set; }
        public string OutputUrl { get; set; } = string.Empty;
        public decimal Cost { get; set; }
        public string? RawResponse { get; set; }
        public string? ErrorMessage { get; set; }  // ✅ ADD THIS
    }
}