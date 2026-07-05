using Microsoft.AspNetCore.Mvc.Rendering;

namespace ControleFutebolWeb.Models.ViewModels
{
    /// <summary>
    /// Dados da tela de listagem de jogadores (/Jogadores). Substitui o uso de ViewBag.
    /// </summary>
    public class JogadoresIndexViewModel
    {
        public List<Jogador> Itens { get; set; } = new();

        // Cabeçalhos de ordenação (não usados pela view atual, mantidos para
        // preservar comportamento exato do estado anterior baseado em ViewBag).
        public string? NomeSortParam { get; set; }
        public string? PosicaoSortParam { get; set; }
        public string? IdadeSortParam { get; set; }
        public string? NacionalidadeSortParam { get; set; }
        public string? TimeSortParam { get; set; }
        public string? CurrentSort { get; set; }

        // Filtros atuais (para repopular o formulário)
        public string? Nome { get; set; }
        public int? IdadeMin { get; set; }
        public int? IdadeMax { get; set; }
        public bool SemIdade { get; set; }
        public string? FiltroPosicao { get; set; }
        public string? FiltroNacionalidade { get; set; }
        public int? FiltroTimeId { get; set; }
        public string? FiltroSortOrder { get; set; }

        // Combos de filtro
        public SelectList Posicoes { get; set; } = null!;
        public SelectList Nacionalidades { get; set; } = null!;
        public SelectList Times { get; set; } = null!;

        // Paginação
        public int PaginaAtual { get; set; }
        public int TotalPaginas { get; set; }
        public int TotalJogadores { get; set; }
        public int PageSize { get; set; }

        // KPIs do hero (totais globais, sem filtro aplicado)
        public int TotalJogadoresGlobal { get; set; }
        public int TotalTimesComJogadores { get; set; }
        public int TotalNacionalidadesGlobal { get; set; }
    }
}
