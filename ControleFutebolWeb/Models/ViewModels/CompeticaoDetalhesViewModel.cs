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
