namespace ControleFutebolWeb.Models
{
    /// <summary>
    /// O craque da partida: o jogador de maior nota naquele jogo — a coroa que aparece
    /// no campinho da análise e alimenta o ranking "Craques da partida" dos relatórios.
    ///
    /// É por USUÁRIO porque a nota é por usuário: quem avaliou o jogo à mão elege o
    /// craque pela régua dele, e quem não avaliou recebe o eleito pela nota automática
    /// (mesmo motor de <see cref="Helpers.CalculadoraNotaAutomatica"/>). Dois analistas
    /// podem coroar jogadores diferentes na mesma partida, e é assim que deve ser.
    ///
    /// A linha é derivada — dá para recalcular a qualquer momento a partir de notas e
    /// estatísticas. Ela existe gravada porque o relatório precisa CONTAR craques por
    /// jogador ao longo de uma temporada inteira, e refazer a eleição de centenas de
    /// jogos a cada abertura da tela seria caro. Quem escreve nota ou importa estatística
    /// apaga/recalcula a linha do jogo (ver <see cref="Services.CraqueDaPartidaService"/>);
    /// o que estiver faltando é calculado e gravado na primeira leitura.
    ///
    /// Os números do desempate ficam guardados junto para a tela explicar POR QUE aquele
    /// jogador levou a coroa quando dois empataram na nota.
    /// </summary>
    public class CraqueDaPartida
    {
        public int Id { get; set; }

        public int JogoId { get; set; }
        public Jogo Jogo { get; set; } = null!;

        public int JogadorId { get; set; }
        public Jogador Jogador { get; set; } = null!;

        public string UsuarioId { get; set; } = "";
        public ApplicationUser? Usuario { get; set; }

        /// <summary>Nota final do jogo que elegeu o craque (0–10).</summary>
        public double Nota { get; set; }

        // Critérios de desempate no momento da eleição — ver a ordem em
        // CraqueDaPartidaService.Eleger.
        public int Gols { get; set; }
        public int Assistencias { get; set; }
        public int ChancesCriadas { get; set; }

        /// <summary>Quantos jogadores empataram na nota mais alta (1 = ganhou sozinho).</summary>
        public int Empatados { get; set; }

        public DateTime CalculadoEm { get; set; }
    }
}
