namespace TBM.Core.Models.AI
{
    public class AIImageRequest
    {
        public string Prompt { get; set; } = default!;
        public string? NegativePrompt { get; set; } // ✅ ADD THIS
        public string ImageUrl { get; set; } = default!;
        public int Width { get; set; } = 1024;
        public int Height { get; set; } = 1024;
    }
}