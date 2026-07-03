namespace ControleFutebolWeb.Services
{
    public interface IEmailSender
    {
        Task EnviarAsync(string destinatario, string assunto, string corpoHtml);
    }
}
