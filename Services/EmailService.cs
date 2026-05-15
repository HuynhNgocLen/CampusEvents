namespace school_event_management.Services
{
    /// <summary>
    /// Cửa gọi tĩnh tương thích ngược — controller vẫn dùng EmailService.SendEmail.
    /// </summary>
    public static class EmailService
    {
        private static readonly IEmailSender Sender = new SmtpEmailSender();

        public static void SendEmail(string toEmail, string subject, string body)
            => Sender.SendEmail(toEmail, subject, body);
    }
}
