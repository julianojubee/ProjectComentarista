namespace ControleFutebolWeb.Models.ViewModels
{
    /// <summary>
    /// Estatísticas (vitórias/empates/derrotas) do treinador à frente do clube atual,
    /// calculadas a partir dos jogos do time desde o início da passagem (TreinadorHistorico
    /// aberto ou, na ausência de histórico importado, a data de cadastro do treinador).
    /// </summary>
    public class TreinadorCardStats
    {
        public DateTime? Desde { get; set; }
        public int Vitorias { get; set; }
        public int Empates { get; set; }
        public int Derrotas { get; set; }
        public int TotalJogos => Vitorias + Empates + Derrotas;
        public int PercentualAproveitamento => TotalJogos == 0 ? 0 : (int)Math.Round(Vitorias * 100.0 / TotalJogos);
    }

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
        public string? NomeFiltro { get; set; }
        public List<int> CompeticaoIdsFiltro { get; set; } = new();
        public List<int> TimeIdsFiltro { get; set; } = new();
        public List<string> NacionalidadesFiltro { get; set; } = new();

        // Estatísticas por treinador (chave = Treinador.Id)
        public Dictionary<int, TreinadorCardStats> Stats { get; set; } = new();

        // Paginação
        public int PaginaAtual { get; set; }
        public int TotalPaginas { get; set; }
        public int TotalTreinadores { get; set; }
        public int PageSize { get; set; }
    }
}
