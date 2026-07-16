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
    }
}
