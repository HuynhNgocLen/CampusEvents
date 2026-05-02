namespace school_event_management.Services
{
    /// <summary>
    /// Gửi email qua SMTP — tách interface để sau này inject/mock khi test hoặc đổi nhà cung cấp.
    /// </summary>
    public interface IEmailSender
    {
        void SendEmail(string toEmail, string subject, string body);
    }
}
