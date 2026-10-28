using ControleFutebolWeb.Models.Nba;

namespace ControleFutebolWeb.Models.ViewModels
{
    public class NbaJogosViewModel
    {
        /// <summary>Dia da rodada (data dos EUA, como a ESPN agrupa os jogos).</summary>
        public DateTime Dia { get; set; }
        public bool EhHoje { get; set; }

        /// <summary>Null = a ESPN não respondeu; lista vazia = dia sem jogos.</summary>
        public IReadOnlyList<NbaJogo>? Jogos { get; set; }

        /// <summary>Canais no Brasil (futnatv) por id do evento; jogo sem entrada = a confirmar.</summary>
        public IReadOnlyDictionary<string, IReadOnlyList<string>> CanaisBrasil { get; set; }
            = new Dictionary<string, IReadOnlyList<string>>();
    }

    public class NbaClassificacaoViewModel
    {
        public string Nivel { get; set; } = "conferencia";
        public int Temporada { get; set; }
        public IReadOnlyList<int> Temporadas { get; set; } = Array.Empty<int>();
        public NbaClassificacao? Classificacao { get; set; }
    }

    public class NbaEstatisticasViewModel
    {
        public string Aba { get; set; } = "lideres";
        public int Temporada { get; set; }
        public IReadOnlyList<int> Temporadas { get; set; } = Array.Empty<int>();
        public string Ordem { get; set; } = "pts";
        /// <summary>Filtro da aba Jogadores (id do time na ESPN); null = liga inteira.</summary>
        public string? TimeId { get; set; }
        /// <summary>Opções do filtro de time (só na aba Jogadores).</summary>
        public IReadOnlyList<NbaTimeResumo> TimesFiltro { get; set; } = Array.Empty<NbaTimeResumo>();
        public NbaLideres? Lideres { get; set; }
        public NbaTabelaJogadores? Jogadores { get; set; }
        public NbaTabelaTimes? Times { get; set; }
    }

    public class NbaJogadorViewModel
    {
        public NbaPerfilJogador Perfil { get; set; } = null!;
        /// <summary>"geral", "jogos", "carreira" ou "recortes".</summary>
        public string Aba { get; set; } = "geral";
        public int Temporada { get; set; }
        public bool Playoffs { get; set; }

        // Só a da aba aberta vem preenchida; null nela = a ESPN não respondeu.
        public NbaVisaoGeralJogador? Visao { get; set; }
        public NbaGameLog? Jogos { get; set; }
        public IReadOnlyList<NbaTabelaCarreira>? Carreira { get; set; }
        public NbaRecortes? Recortes { get; set; }
    }

    public class NbaTimeViewModel
    {
        public int Temporada { get; set; }
        public NbaDetalheTime Detalhe { get; set; } = null!;
    }
}
