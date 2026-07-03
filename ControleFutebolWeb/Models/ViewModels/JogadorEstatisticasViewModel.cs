namespace ControleFutebolWeb.Models.ViewModels
{
    public class JogadorEstatisticasViewModel
    {
        public Jogador Jogador { get; set; }
        public double MediaNotas { get; set; }
        public int TotalJogos { get; set; }          // jogos analisados
        public int TotalJogosParticipados { get; set; } // total com escalação
        public int TotalGols { get; set; }
        public int TotalAssistencias { get; set; }
        public List<NotaJogoItem> NotasPorJogo { get; set; }
    }

    public class NotaJogoItem
    {
        public Jogo Jogo { get; set; }
        public bool Analisado { get; set; }          // false = sem nota, só participou
        public double Nota { get; set; }
        public string Comentario { get; set; }
        public int Gols { get; set; }
        public int Assistencias { get; set; }
        public int Cartoes { get; set; }
        public string Resultado { get; set; }        // "V", "E", "D", "?" (sem placar)
        public double BonusResultado { get; set; }
        public double NotaFinal { get; set; }
        public List<Notadetalhe> Detalhes { get; set; } = new();
        public int GolsPro { get; set; }
        public int GolsContra { get; set; }
        public double NotaBaseFixa { get; set; }     // 4.0 (base) — nota mínima 4.0
        public bool OrigemManual { get; set; }       // true = nota dada por um analista
        public double? NotaManual { get; set; }      // nota final informada manualmente (override)

        // Observações marcadas com a tag "Jogador" (ObservacaoJogoTag) para esse jogo.
        public List<string> ObservacoesJogador { get; set; } = new();
    }
}
