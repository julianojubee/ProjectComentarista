namespace ControleFutebolWeb.Models
{
    // Estatísticas individuais de um jogador numa partida, vindas da api-football
    // (fixtures?id=X -> players[].players[].statistics[]). Usadas para pré-preencher
    // a nota manual com os pesos definidos pelo usuário.
    public class EstatisticaJogador
    {
        public int Id { get; set; }

        public int JogoId { get; set; }
        public Jogo Jogo { get; set; } = null!;

        public int JogadorId { get; set; }
        public Jogador Jogador { get; set; } = null!;

        public int? Minutos { get; set; }
        public double? Rating { get; set; }
        public bool Capitao { get; set; }

        public int Offsides { get; set; }

        public int FinalizacoesTotal { get; set; }
        public int FinalizacoesNoGol { get; set; }

        public int Gols { get; set; }
        public int GolsSofridos { get; set; }
        public int Assistencias { get; set; }
        public int Defesas { get; set; }

        public int PassesTotal { get; set; }
        public int PassesChave { get; set; }

        // Passes concluídos (api-football manda em passes.accuracy, que é
        // contagem e não percentual). A precisão em % é derivada daqui com
        // PassesTotal — ver PrecisaoPasses.
        public int PassesCertos { get; set; }

        // "Precisão dos passes (%)" no padrão das estatísticas da FIFA. Null
        // quando o jogador não tentou nenhum passe na partida — e também quando
        // acertou zero com passes tentados, que na prática só acontece em jogo
        // importado antes de PassesCertos existir. Sem isso a tela mostraria 0%
        // e o dado ausente ficaria igual a um desempenho péssimo.
        public double? PrecisaoPasses => PassesTotal > 0 && PassesCertos > 0
            ? Math.Round(PassesCertos / (double)PassesTotal * 100, 1) : null;

        public int Desarmes { get; set; }
        public int Bloqueios { get; set; }
        public int Interceptacoes { get; set; }

        public int DuelosTotal { get; set; }
        public int DuelosVencidos { get; set; }

        public int DriblesTentados { get; set; }
        public int DriblesCertos { get; set; }
        public int DriblesSofridos { get; set; }

        public int FaltasSofridas { get; set; }
        public int FaltasCometidas { get; set; }

        public int CartoesAmarelos { get; set; }
        public int CartoesVermelhos { get; set; }

        public int PenaltiSofrido { get; set; }
        public int PenaltiCometido { get; set; }
        public int PenaltiPerdido { get; set; }
        public int PenaltiDefendido { get; set; }
        public int PenaltiConvertido { get; set; }

        // Conversão de pênaltis (%): convertidos ÷ cobrados. Null sem cobrança.
        public double? ConversaoPenaltis => (PenaltiConvertido + PenaltiPerdido) > 0
            ? Math.Round(PenaltiConvertido / (double)(PenaltiConvertido + PenaltiPerdido) * 100, 1) : null;

        // Entrou no decorrer do jogo (games.substitute da api-football). Evita
        // cruzar com Escalacao só para saber se foi titular.
        public bool EntrouDoBanco { get; set; }
    }
}
