namespace TBM.Core.Models.AI
{
    public class AIVideoRequest
    {
        public string Prompt { get; set; } = default!;
        public string ImageUrl { get; set; } = default!;
        public int DurationSeconds { get; set; } = 5;
    }
}
