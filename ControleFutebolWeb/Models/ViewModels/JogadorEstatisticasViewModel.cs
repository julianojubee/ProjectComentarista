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

        // Posições em que o jogador foi escalado (agregado das escalações do usuário).
        public List<PosicaoJogadaItem> PosicoesJogadas { get; set; } = new();

        // Um ponto (PosicaoX/Y) por jogo em que o jogador foi titular com coordenada
        // real salva — base do mapa de calor de posições (histórico completo, não só
        // o agregado por categoria de PosicoesJogadas).
        public List<PontoHeatmap> PontosHeatmap { get; set; } = new();

        // Médias por jogo a partir das estatísticas importadas (null = sem dados).
        public MediasPorJogo? Medias { get; set; }
    }

    // Uma posição ocupada pelo jogador: rótulo, nº de jogos, % e o ponto médio
    // no campinho (coordenadas % — mesmo sistema de Escalacao.PosicaoX/Y).
    public class PosicaoJogadaItem
    {
        public string Posicao { get; set; } = "";
        public int Jogos { get; set; }
        public double Pct { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
    }

    public class PontoHeatmap
    {
        public double X { get; set; }
        public double Y { get; set; }
    }

    // Médias por jogo (estatísticas importadas da api-football). Os pares
    // "tentados/certos" viram donuts de aproveitamento na tela.
    public class MediasPorJogo
    {
        public int Jogos { get; set; }

        public double Passes { get; set; }
        public double PassesChave { get; set; }

        public double Finalizacoes { get; set; }
        public int FinalizacoesPct { get; set; }     // % no gol

        public double Dribles { get; set; }
        public int DriblesPct { get; set; }          // % certos

        public double Duelos { get; set; }
        public int DuelosPct { get; set; }           // % vencidos

        public double Desarmes { get; set; }
        public double Interceptacoes { get; set; }
        public double Bloqueios { get; set; }
        public double Defesas { get; set; }          // relevante para goleiros

        public double FaltasSofridas { get; set; }
        public double FaltasCometidas { get; set; }
    }

    public class NotaJogoItem
    {
        public Jogo Jogo { get; set; }
        public bool Analisado { get; set; }          // false = sem nota, só participou
        public double Nota { get; set; }
        public string Comentario { get; set; }
        public string? Posicao { get; set; }         // posição escalada nesse jogo (fase INICIAL, ou FINAL se só entrou depois)
        public int? Minutos { get; set; }            // minutos jogados (só quando há estatística importada)
        public int Gols { get; set; }
        public int Assistencias { get; set; }
        public int Cartoes { get; set; }
        public string Resultado { get; set; }        // "V", "E", "D", "?" (sem placar)

        // De que lado o jogador estava NESTE jogo (da escalação da época, não do
        // time atual — após uma transferência o time atual inverteria o histórico).
        public bool IsCasa { get; set; }
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
