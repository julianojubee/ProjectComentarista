using ControleFutebolWeb.Models;

namespace ControleFutebolWeb.Models.ViewModels
{
    public class ScoutFiltro
    {
        public List<string> Posicoes { get; set; } = new();
        public int? IdadeMin { get; set; }
        public int? IdadeMax { get; set; }
        // Altura em centímetros e peso em quilos (Jogador.Altura/Peso)
        public int? AlturaMin { get; set; }
        public int? AlturaMax { get; set; }
        public int? PesoMin { get; set; }
        public int? PesoMax { get; set; }
        public List<int> TimeIds { get; set; } = new();
        public List<int> CompeticaoIds { get; set; } = new();
        public List<int> NacionalidadeIds { get; set; } = new();
        public int? Temporada { get; set; }

        public int? MinJogos { get; set; }
        public int? MinGols { get; set; }
        public int? MinAssistencias { get; set; }
        public int? MinPassesChave { get; set; }
        public int? MinDesarmes { get; set; }
        public int? MinBloqueios { get; set; }
        public int? MinInterceptacoes { get; set; }
        public int? MinDuelosVencidos { get; set; }
        public int? MinFinalizacoesNoGol { get; set; }
        public int? MinDrilesCertos { get; set; }
        public double? MinNota { get; set; }
        public int? MaxCartaoAmarelo { get; set; }
        public int? MaxCartaoVermelho { get; set; }

        // Médias por jogo (total da estatística / jogos disputados nos jogos filtrados)
        public double? MediaPassesChave { get; set; }
        public double? MediaDesarmes { get; set; }
        public double? MediaBloqueios { get; set; }
        public double? MediaInterceptacoes { get; set; }
        public double? MediaDuelosVencidos { get; set; }
        public double? MediaFinalizacoesNoGol { get; set; }
        public double? MediaDrilesCertos { get; set; }
    }

    public class ScoutResultItem
    {
        public Jogador Jogador { get; set; } = null!;
        public int Jogos { get; set; }
        public int Gols { get; set; }
        public int Assistencias { get; set; }
        public int CartaoAmarelo { get; set; }
        public int CartaoVermelho { get; set; }
        public double? NotaMedia { get; set; }
        public int PassesChave { get; set; }
        public int Desarmes { get; set; }
        public int Bloqueios { get; set; }
        public int Interceptacoes { get; set; }
        public int DuelosVencidos { get; set; }
        public int FinalizacoesNoGol { get; set; }
        public int DrilesCertos { get; set; }
    }

    public class ScoutViewModel
    {
        public ScoutFiltro Filtro { get; set; } = new();
        public List<ScoutResultItem> Resultados { get; set; } = new();
        public List<Competicao> Competicoes { get; set; } = new();
        public List<Time> Times { get; set; } = new();
        public List<Nacionalidade> Nacionalidades { get; set; } = new();
        public List<string> Posicoes { get; set; } = new();
        public List<int> Temporadas { get; set; } = new();
        public bool Pesquisou { get; set; }
    }
}
