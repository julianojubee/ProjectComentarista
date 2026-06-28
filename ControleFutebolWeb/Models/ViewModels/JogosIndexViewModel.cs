using Microsoft.AspNetCore.Mvc.Rendering;

namespace ControleFutebolWeb.Models.ViewModels
{
    /// <summary>
    /// Lista de jogos + dados dos filtros da tela /Jogos (Index).
    /// Substitui o uso de ViewBag.
    /// </summary>
    public class JogosIndexViewModel
    {
        public List<Jogo> Jogos { get; set; } = new();

        public SelectList? TimeList { get; set; }
        public SelectList? CompeticaoList { get; set; }
        public SelectList? StatusList { get; set; }
        public SelectList? LocationList { get; set; }

        public string? StartDate { get; set; }
        public string? EndDate { get; set; }

        public Dictionary<int, string> CompeticoesMap { get; set; } = new();

        // Paginação
        public int PaginaAtual { get; set; } = 1;
        public int TotalPaginas { get; set; } = 1;
        public int TotalJogos { get; set; }
        public int TotalFinalizados { get; set; }
        public int TotalAgendados => TotalJogos - TotalFinalizados;
        public int PageSize { get; set; } = 50;

        // Filtros atuais (para preservar nos links de paginação)
        public int? TeamIdFiltro { get; set; }
        public string? LocationFiltro { get; set; }
        public int? CompeticaoIdFiltro { get; set; }
        public string? StatusFiltro { get; set; }
    }
}
