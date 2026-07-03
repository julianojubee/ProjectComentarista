namespace ControleFutebolWeb.Models.ViewModels
{
    /// <summary>
    /// Dados da tela de listagem de treinadores (/Treinadores). Substitui o uso de ViewBag.
    /// </summary>
    public class TreinadoresIndexViewModel
    {
        public List<Treinador> Itens { get; set; } = new();

        // Listas completas para os tag selectors (multi-seleção)
        public List<Competicao> Competicoes { get; set; } = new();
        public List<Time> Times { get; set; } = new();
        public List<Nacionalidade> NacionalidadesLista { get; set; } = new();

        // Filtros atualmente aplicados
        public List<int> CompeticaoIdsFiltro { get; set; } = new();
        public List<int> TimeIdsFiltro { get; set; } = new();
        public List<string> NacionalidadesFiltro { get; set; } = new();

        // Paginação
        public int PaginaAtual { get; set; }
        public int TotalPaginas { get; set; }
        public int TotalTreinadores { get; set; }
        public int PageSize { get; set; }
    }
}
