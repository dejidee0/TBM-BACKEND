namespace TBM.Infrastructure.Configuration
{
    public class CloudinarySettings
    {
        public string CloudName { get; set; } = default!;
        public string ApiKey { get; set; } = default!;
        public string ApiSecret { get; set; } = default!;
        public string RoomFolder { get; set; } = "rooms/original";
        public string GeneratedFolder { get; set; } = "rooms/generated";
    }
}
