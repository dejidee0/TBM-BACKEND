namespace TBM.Infrastructure.Configuration
{
    public class SmtpSettings
    {
        public string Host { get; set; } = default!;
        public int Port { get; set; }
        public string Username { get; set; } = default!;
        public string Password { get; set; } = default!;
        public string FromEmail { get; set; } = default!;
        public string FromName { get; set; } = default!;
    }
}
