namespace ControleFutebolWeb.Models.ViewModels
{
    public class CompeticaoDetalhesViewModel
    {
        public Competicao Competicao { get; set; }
        public string Tipo { get; set; }
        public List<Classificacao> Classificacao { get; set; } = new();
        public List<GrupoViewModel> Grupos { get; set; } = new();
        public List<FaseMataMataViewModel> FasesMataMata { get; set; } = new();
        public List<Jogo> ProximosJogos { get; set; } = new();
        public List<Jogo> JogosRealizados { get; set; } = new();

        // Preenchido quando a competição tem fases declaradas (CompeticaoFase);
        // vazio = comportamento de fase única guiado por Tipo.
        public List<FaseDetalheViewModel> Fases { get; set; } = new();
    }

    // Conteúdo de uma fase declarada — só a coleção do Tipo da fase é preenchida.
    public class FaseDetalheViewModel
    {
        public CompeticaoFase Fase { get; set; } = null!;
        public List<Classificacao> Classificacao { get; set; } = new();       // PONTOS_CORRIDOS
        public List<GrupoViewModel> Grupos { get; set; } = new();             // GRUPOS
        public List<FaseMataMataViewModel> FasesMataMata { get; set; } = new(); // MATA_MATA / JOGO_UNICO
    }

    public class GrupoViewModel
    {
        public string Nome { get; set; }
        public List<Classificacao> Times { get; set; } = new();
    }

    public class FaseMataMataViewModel
    {
        public string Nome { get; set; }
        public int Ordem { get; set; }
        public List<ConfrontoViewModel> Confrontos { get; set; } = new();

        // Fase que decide o título (competição/fase de JOGO_UNICO): o vencedor
        // é anunciado como campeão em vez de "classificado".
        public bool DecideTitulo { get; set; }
    }

    public class ConfrontoViewModel
    {
        public Jogo? JogoIda { get; set; }
        public Jogo? JogoVolta { get; set; }
        public Time? TimeA { get; set; }
        public Time? TimeB { get; set; }

        public int GolsAIda => JogoIda?.TimeCasaId == TimeA?.Id
            ? JogoIda?.PlacarCasa ?? 0
            : JogoIda?.PlacarVisitante ?? 0;

        public int GolsBIda => JogoIda?.TimeCasaId == TimeB?.Id
            ? JogoIda?.PlacarCasa ?? 0
            : JogoIda?.PlacarVisitante ?? 0;

        public int GolsAVolta => JogoVolta?.TimeCasaId == TimeA?.Id
            ? JogoVolta?.PlacarCasa ?? 0
            : JogoVolta?.PlacarVisitante ?? 0;

        public int GolsBVolta => JogoVolta?.TimeCasaId == TimeB?.Id
            ? JogoVolta?.PlacarCasa ?? 0
            : JogoVolta?.PlacarVisitante ?? 0;

        public int TotalA => GolsAIda + GolsAVolta;
        public int TotalB => GolsBIda + GolsBVolta;

        // Confronto de jogo único (final única, mata-mata sem volta) fecha com a ida:
        // exigir JogoVolta deixaria o vencedor sem ser anunciado.
        public bool Completo => JogoIda?.PlacarCasa != null
            && (JogoVolta == null || JogoVolta.PlacarCasa != null);
        public bool SoIda => JogoVolta == null;

        // Jogo que decide o confronto (a volta quando há duas partidas, senão a única):
        // é dele que vêm os pênaltis do confronto.
        private Jogo? JogoDecisivo => JogoVolta ?? JogoIda;

        public int? PenaltisA => JogoDecisivo == null ? null
            : JogoDecisivo.TimeCasaId == TimeA?.Id ? JogoDecisivo.PenaltisCasa : JogoDecisivo.PenaltisVisitante;

        public int? PenaltisB => JogoDecisivo == null ? null
            : JogoDecisivo.TimeCasaId == TimeB?.Id ? JogoDecisivo.PenaltisCasa : JogoDecisivo.PenaltisVisitante;

        // Empate no agregado (ou na partida única) é resolvido nos pênaltis.
        public bool DecididoNosPenaltis => Completo && TotalA == TotalB
            && PenaltisA.HasValue && PenaltisB.HasValue && PenaltisA != PenaltisB;

        public bool VenceA => Completo && (TotalA > TotalB || (TotalA == TotalB && PenaltisA > PenaltisB));
        public bool VenceB => Completo && (TotalB > TotalA || (TotalA == TotalB && PenaltisB > PenaltisA));
    }
}
