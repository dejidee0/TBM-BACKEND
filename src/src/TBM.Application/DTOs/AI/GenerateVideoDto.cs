namespace TBM.Application.DTOs.AI
{
    public class GenerateVideoDto
    {
        public Guid ProjectId { get; set; }
        public string Prompt { get; set; } = default!;
        public string? SourceImageUrl { get; set; }
        public int DurationSeconds { get; set; } = 9; // ✅ Changed default from 5 to 9
    }
}