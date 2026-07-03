using System.Net;
using System.Net.Mail;

namespace ControleFutebolWeb.Services
{
    public class SmtpEmailSender : IEmailSender
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SmtpEmailSender> _logger;

        public SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task EnviarAsync(string destinatario, string assunto, string corpoHtml)
        {
            var host = _configuration["Smtp:Host"];
            var from = _configuration["Smtp:From"];

            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from))
            {
                _logger.LogWarning(
                    "Smtp:Host/Smtp:From não configurados — e-mail para {Destinatario} não foi enviado.",
                    destinatario);
                return;
            }

            var port = int.TryParse(_configuration["Smtp:Port"], out var p) ? p : 587;
            var enableSsl = !bool.TryParse(_configuration["Smtp:EnableSsl"], out var ssl) || ssl;
            var user = _configuration["Smtp:User"];
            var password = _configuration["Smtp:Password"];

            using var client = new SmtpClient(host, port)
            {
                EnableSsl = enableSsl
            };
            if (!string.IsNullOrWhiteSpace(user))
                client.Credentials = new NetworkCredential(user, password);

            using var mensagem = new MailMessage(from, destinatario, assunto, corpoHtml)
            {
                IsBodyHtml = true
            };

            await client.SendMailAsync(mensagem);
        }
    }
}
