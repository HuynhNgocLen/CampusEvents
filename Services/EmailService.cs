using System.Net;
using System.Net.Mail;
using System.Configuration;

namespace school_event_management.Services
{
    public class EmailService
    {
        public static void SendEmail(string toEmail, string subject, string body)
        {
            // Đọc từ Web.config
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