namespace Concurso.Notification.Services;

public interface IEmailSender
{
    Task SendAsync(string toEmail, string toName, string subject, string bodyHtml, string plainText = "");
}
