namespace ControleFutebolWeb.Models
{
    // Dados do recebedor para o QR de PIX estático exibido na tela de bloqueio
    // (seção "Pix" do appsettings/env). Sem Chave configurada, a tela de
    // bloqueio não mostra QR — só a mensagem de contato com o administrador.
    public class PixOptions
    {
        public string Chave { get; set; } = "";
        public string NomeRecebedor { get; set; } = "";
        public string Cidade { get; set; } = "";
        // Valor cobrado de quem não tem mensalidade própria (ApplicationUser.ValorMensalidade).
        public decimal ValorPadrao { get; set; }
    }
}
