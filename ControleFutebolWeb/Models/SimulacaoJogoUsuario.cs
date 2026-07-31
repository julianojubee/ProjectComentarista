namespace ControleFutebolWeb.Models
{
    /// <summary>
    /// Palpite de placar de um usuário para um jogo ainda não realizado (tela /Simulador).
    ///
    /// O Jogo NUNCA é alterado: a simulação é uma camada por usuário aplicada por cima
    /// dos resultados reais só na hora de montar a classificação. Assim o resultado
    /// oficial que chegar depois pela api-football sobrepõe o palpite naturalmente.
    /// </summary>
    public class SimulacaoJogoUsuario
    {
        public int Id { get; set; }

        public int JogoId { get; set; }
        public Jogo Jogo { get; set; } = null!;

        public string UsuarioId { get; set; } = "";
        public ApplicationUser Usuario { get; set; } = null!;

        // Competição/temporada ficam repetidas aqui (já estão no Jogo) para que
        // "limpar simulação" apague tudo de uma competição sem join com jogos.
        public int CompeticaoId { get; set; }
        public int Temporada { get; set; }

        public int PlacarCasa { get; set; }
        public int PlacarVisitante { get; set; }

        public DateTime AtualizadoEm { get; set; }
    }
}
