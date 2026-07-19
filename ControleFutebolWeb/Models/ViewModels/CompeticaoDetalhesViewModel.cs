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
        public List<FaseMataMataViewModel> FasesMataMata { get; set; } = new(); // MATA_MATA
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

        public bool Completo => JogoIda?.PlacarCasa != null && JogoVolta?.PlacarCasa != null;
        public bool SoIda => JogoVolta == null;
    }
}
