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

        // Resultado contado apenas quando o time era o mandante. Somados sobre
        // todos os times, dão o recorte mandante × visitante × empate da
        // competição sem contar nenhum jogo duas vezes — cada partida tem
        // exatamente um mandante.
        public int VCasa { get; set; }
        public int ECasa { get; set; }
        public int DerrotasCasa => JCasa - VCasa - ECasa;
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
        // Gols − xG, ambos por jogo. Gp é o total da temporada e Xg é média por
        // partida, então sem dividir Gp por J a conta comparava grandezas
        // diferentes (um time com 122 gols aparecia como "+119 acima do xG").
        public double? DifXg => Xg.HasValue && J > 0 ? Math.Round(Gp / (double)J - Xg.Value, 2) : null;

        // Demais chaves de Jogo.EstatisticasJson que a api-football já grava e que
        // até então não eram lidas. Equivalem às abas Ataque/Distribuição/Disciplina/
        // Goleiro das estatísticas de seleção da FIFA. Também médias por jogo.
        public double? ChutesFora { get; set; }        // Shots off Goal
        public double? ChutesArea { get; set; }        // Shots insidebox
        public double? ChutesForaArea { get; set; }    // Shots outsidebox
        public double? ChutesBloqueados { get; set; }  // Blocked Shots
        public double? Passes { get; set; }            // Total passes
        public double? PrecisaoPasses { get; set; }    // Passes %
        public double? Faltas { get; set; }            // Fouls
        public double? Impedimentos { get; set; }      // Offsides
        public double? DefesasGoleiro { get; set; }    // Goalkeeper Saves
        public double? GolsEvitados { get; set; }      // goals_prevented

        // Soma acumulada de cada estatística de partida, na mesma nomenclatura das
        // médias acima (fin, chutesArea, passes, …). Serve ao seletor "Totais ×
        // Por jogo" do painel: a média não dá pra multiplicar por J no cliente
        // porque o denominador é o nº de partidas que tinham o dado, não o de
        // partidas disputadas. Só estatísticas de contagem entram — percentuais
        // (posse, precisão) e razões (eficiência em xG) não têm total que faça
        // sentido e continuam iguais nos dois modos.
        public Dictionary<string, double> Totais { get; set; } = new();

        // Totais (não médias) usados nos derivados abaixo, junto dos gols marcados
        // apenas nas partidas em que a estatística correspondente veio importada —
        // sem isso o denominador cobriria jogos que o numerador não cobre.
        public double? FinTotal { get; set; }
        public int GolsComFin { get; set; }
        public double? XgTotal { get; set; }
        public int GolsComXg { get; set; }

        // "Finalizações convertidas (%)" da FIFA: gols ÷ finalizações.
        public double? Conversao => FinTotal > 0
            ? Math.Round(GolsComFin / FinTotal.Value * 100, 1) : null;

        // "Eficiência em GE" da FIFA, exibida como 1.33x: gols ÷ gols esperados.
        // Acima de 1 o time converte mais do que o xG previa.
        public double? EficienciaXg => XgTotal > 0
            ? Math.Round(GolsComXg / XgTotal.Value, 2) : null;

        public int Amarelos { get; set; }
        public int Vermelhos { get; set; }
        public int CleanSheets { get; set; }

        // Gols marcados por intervalo de 15min: [0-15,16-30,31-45,46-60,61-75,76-90,91+]
        public int[] GolsIntervalo { get; set; } = new int[7];
    }
}
