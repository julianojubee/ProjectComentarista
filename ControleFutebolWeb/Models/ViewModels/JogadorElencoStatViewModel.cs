namespace ControleFutebolWeb.Models.ViewModels
{
    // Linha do painel "Estatísticas do Elenco" (Times/Details): tudo o que o sistema
    // guarda de um jogador nos jogos já realizados do time, agregado na temporada.
    // Os somatórios vêm de EstatisticaJogador (importado da api-football); as
    // titularidades vêm de Escalacao. Só entram os jogos com placar definido.
    public class JogadorElencoStatViewModel
    {
        public int JogadorId { get; set; }
        // Nome curto (Jogador.Nome, como vem da api-football) — mesmo rótulo da tela
        // de análise do jogo. Cabe nos eixos dos gráficos; o completo fica no tooltip.
        public string Nome { get; set; } = "";
        public string NomeCompleto { get; set; } = "";
        public string Posicao { get; set; } = "";
        // GOL | DEF | MEI | ATA (PerfilJogadorService.GrupoPosicao)
        public string Grupo { get; set; } = "ATA";
        public int Idade { get; set; }
        public int? Altura { get; set; }
        public string? Nacionalidade { get; set; }
        public string? FotoUrl { get; set; }

        public int MinutosJogados { get; set; }
        public double PercMinutos { get; set; }
        public int Jogos { get; set; }         // partidas com estatística importada
        public int Titularidades { get; set; }
        public double? RatingMedio { get; set; }

        public int Gols { get; set; }
        public int Assistencias { get; set; }
        public int FinalizacoesTotal { get; set; }
        public int FinalizacoesNoGol { get; set; }
        public int PassesTotal { get; set; }
        public int PassesChave { get; set; }

        public int Desarmes { get; set; }
        public int Interceptacoes { get; set; }
        public int Bloqueios { get; set; }

        public int DuelosTotal { get; set; }
        public int DuelosVencidos { get; set; }
        public int DriblesTentados { get; set; }
        public int DriblesCertos { get; set; }

        public int FaltasCometidas { get; set; }
        public int FaltasSofridas { get; set; }
        public int CartoesAmarelos { get; set; }
        public int CartoesVermelhos { get; set; }

        public int Defesas { get; set; }
        public int GolsSofridos { get; set; }

        // Normalização por 90 minutos: única forma justa de comparar quem joga
        // sempre com quem entra no segundo tempo.
        public double Por90(int total) =>
            MinutosJogados > 0 ? Math.Round(total * 90.0 / MinutosJogados, 2) : 0;

        public double ContribuicaoPor90 => Por90(Gols + Assistencias);
        public double AcoesDefensivasPor90 => Por90(Desarmes + Interceptacoes + Bloqueios);
        public double CartoesPor90 => Por90(CartoesAmarelos + CartoesVermelhos);

        public static int Pct(int parte, int total) =>
            total > 0 ? (int)Math.Round(100.0 * parte / total) : 0;

        public int PctDuelos => Pct(DuelosVencidos, DuelosTotal);
        public int PctDribles => Pct(DriblesCertos, DriblesTentados);
        public int PctFinalizacoesNoGol => Pct(FinalizacoesNoGol, FinalizacoesTotal);
    }

    // Cabeçalho do painel: o retrato do elenco em números únicos.
    public class ElencoResumoViewModel
    {
        public int JogosConsiderados { get; set; }   // partidas com placar definido
        public int TamanhoElenco { get; set; }       // jogadores vinculados ao time
        public int JogadoresUtilizados { get; set; } // com pelo menos 1 minuto

        public double IdadeMedia { get; set; }
        // Idade média ponderada pelos minutos: diz a idade do time que entra em campo,
        // não a do cadastro (um elenco jovem no papel pode escalar só veteranos).
        public double IdadeMediaPonderada { get; set; }
        public int? AlturaMedia { get; set; }
        public int Nacionalidades { get; set; }

        // Concentração de minutos nos 11 mais usados — dependência do time titular.
        public double PercMinutosTop11 { get; set; }
        public double? RatingMedio { get; set; }

        public int Gols { get; set; }
        public int Assistencias { get; set; }
        public int CartoesAmarelos { get; set; }
        public int CartoesVermelhos { get; set; }

        // Minutos por setor (GOL/DEF/MEI/ATA) — para o donut de distribuição.
        public Dictionary<string, int> MinutosPorGrupo { get; set; } = new();
        // Idade média por setor — onde o elenco é jovem e onde envelheceu.
        public Dictionary<string, double> IdadeMediaPorGrupo { get; set; } = new();
    }
}
