namespace ControleFutebolWeb.Models.ViewModels
{
    // Estatísticas agregadas de um time na temporada, usadas pela aba "Estatísticas"
    // da tabela do Brasileirão (gráficos client-side em Views/Tabela/Brasileirao.cshtml).
    // Posse/Fin/FinGol/Esc/PassesCertos/Xg vêm de Jogo.EstatisticasJson (api-football)
    // e ficam nulas quando nenhuma partida do time tem esse dado importado.
    public class EstatisticaTimeViewModel
    {
        public int TimeId { get; set; }
        public string Nome { get; set; } = "";
        public string Sigla { get; set; } = "";
        public string? EscudoUrl { get; set; }

        public int J { get; set; }
        public int V { get; set; }
        public int E { get; set; }
        public int D { get; set; }
        public int Pts { get; set; }
        public int Gp { get; set; }
        public int Gc { get; set; }
        public int Sg => Gp - Gc;
        public double Aprov => J > 0 ? Math.Round(Pts / (double)(J * 3) * 100, 1) : 0;

        public int JCasa { get; set; }
        public int JFora { get; set; }
        public int PtsCasa { get; set; }
        public int PtsFora { get; set; }
        public int GpCasa { get; set; }
        public int GpFora { get; set; }
        public int GcCasa { get; set; }
        public int GcFora { get; set; }
        public double AprovCasa => JCasa > 0 ? Math.Round(PtsCasa / (double)(JCasa * 3) * 100, 1) : 0;
        public double AprovFora => JFora > 0 ? Math.Round(PtsFora / (double)(JFora * 3) * 100, 1) : 0;

        // Médias por jogo vindas da api-football; null = sem dado importado para este time.
        public double? Posse { get; set; }
        public double? Fin { get; set; }
        public double? FinGol { get; set; }
        public double? Esc { get; set; }
        public double? PassesCertos { get; set; }
        public double? Xg { get; set; }
        public double? Precisao => Fin.HasValue && Fin > 0 && FinGol.HasValue
            ? Math.Round(FinGol.Value / Fin.Value * 100, 1) : null;
        public double? DifXg => Xg.HasValue ? Math.Round(Gp - Xg.Value, 1) : null;

        public int Amarelos { get; set; }
        public int Vermelhos { get; set; }
        public int CleanSheets { get; set; }

        // Gols marcados por intervalo de 15min: [0-15,16-30,31-45,46-60,61-75,76-90,91+]
        public int[] GolsIntervalo { get; set; } = new int[7];
    }
}
