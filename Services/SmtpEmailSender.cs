using System.Configuration;
using System.Net;
using System.Net.Mail;

namespace school_event_management.Services
{
    /// <summary>
    /// Triển khai SMTP (hiện dùng Gmail) — logic gửi tách khỏi controller.
    /// </summary>
    public sealed class SmtpEmailSender : IEmailSender
    {
        public void SendEmail(string toEmail, string subject, string body)
        {
            var fromEmail = ConfigurationManager.AppSettings["SmtpEmail"];
            var password = ConfigurationManager.AppSettings["SmtpPassword"];
            var fromName = ConfigurationManager.AppSettings["SmtpDisplayName"] ?? "CampusEvents";

            var smtp = new SmtpClient
            {
                Host = "smtp.gmail.com",
                Port = 587,
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(fromEmail, password),
                TargetName = "STARTTLS/smtp.gmail.com"
            };

            using (var message = new MailMessage()
            {
                From = new MailAddress(fromEmail, fromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            })
            {
                message.To.Add(toEmail);
                smtp.Send(message);
            }
        }
    }
}
