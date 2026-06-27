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
    }
}
