namespace ControleFutebolWeb.Models.ViewModels
{
    /// <summary>
    /// Tela /Admin/Acessos: o movimento das páginas públicas de /creators no
    /// período escolhido, somado por ferramenta e aberto dia a dia.
    /// </summary>
    public class AcessosPublicosViewModel
    {
        public int Dias { get; set; }
        public DateOnly De { get; set; }
        public DateOnly Ate { get; set; }

        public List<AcessoFerramentaViewModel> PorFerramenta { get; set; } = new();

        /// <summary>Um item por dia do período, inclusive os dias sem nenhum acesso.</summary>
        public List<AcessoDiaViewModel> PorDia { get; set; } = new();

        public int TotalVisitas => PorFerramenta.Sum(f => f.Visitas);
        public int TotalVisitantes => PorFerramenta.Sum(f => f.Visitantes);

        /// <summary>Maior número de visitas num único dia — escala das barras do gráfico.</summary>
        public int PicoDiario => PorDia.Count == 0 ? 0 : PorDia.Max(d => d.Visitas);
    }

    public class AcessoFerramentaViewModel
    {
        public string Ferramenta { get; set; } = string.Empty;
        public int Visitas { get; set; }
        public int Visitantes { get; set; }
    }

    public class AcessoDiaViewModel
    {
        public DateOnly Dia { get; set; }
        public int Visitas { get; set; }
        public int Visitantes { get; set; }

        /// <summary>Quebra do dia por ferramenta, da mais aberta para a menos.</summary>
        public List<(string Ferramenta, int Visitas)> Detalhe { get; set; } = new();
    }
}
