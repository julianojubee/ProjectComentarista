namespace ControleFutebolWeb.Models
{
    // Link público e SOMENTE LEITURA de uma análise de jogo (/analise/{token}).
    // Serve para mostrar a análise a quem não tem conta no sistema: o token
    // carrega o par (jogo, dono da análise), já que notas, escalação e
    // observações são todas por usuário.
    //
    // O token é a única credencial do visitante — por isso é opaco (24 bytes
    // aleatórios em base64url) e nunca derivado do JogoId.
    public class AnaliseCompartilhada
    {
        public int Id { get; set; }

        public int JogoId { get; set; }
        public Jogo Jogo { get; set; } = null!;

        // Dono da análise: é o UsuarioId usado para filtrar notas/escalação/
        // observações ao montar a página pública.
        public string UsuarioId { get; set; } = "";
        public ApplicationUser Usuario { get; set; } = null!;

        public string Token { get; set; } = "";

        public DateTime CriadoEm { get; set; }

        // null = sem prazo. Serve para links temporários.
        public DateTime? ExpiraEm { get; set; }

        // Revogação é soft (mantém o histórico de acessos); o link passa a 404.
        public DateTime? RevogadoEm { get; set; }

        public int Visualizacoes { get; set; }
        public DateTime? UltimoAcessoEm { get; set; }

        public bool EstaAtivo(DateTime agoraUtc) =>
            RevogadoEm == null && (ExpiraEm == null || ExpiraEm > agoraUtc);
    }
}
