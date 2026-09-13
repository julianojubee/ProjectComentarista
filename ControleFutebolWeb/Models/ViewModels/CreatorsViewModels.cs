using ControleFutebolWeb.Models;

namespace ControleFutebolWeb.Models.ViewModels
{
    // Telas públicas de /creators. Só o que a página precisa desenhar antes de o
    // JavaScript entrar em ação — o que o creator monta na tela não volta para
    // o servidor, então não há modelo de entrada aqui.

    public class CreatorsEscalacaoViewModel
    {
        // Competições que têm escalação registrada; filtrar por uma delas encurta
        // a lista de times, que sozinha passa de quatrocentos nomes.
        public List<Competicao> Competicoes { get; set; } = new();
        public int? CompeticaoId { get; set; }

        public List<Time> Times { get; set; } = new();
    }

    public class CreatorsSelecaoViewModel
    {
        public List<Competicao> Competicoes { get; set; } = new();
        public int? CompeticaoId { get; set; }

        public List<int> Temporadas { get; set; } = new();
        public int Temporada { get; set; }

        // Times que disputaram a competição na temporada: são as fontes de
        // jogadores para a barra lateral.
        public List<Time> Times { get; set; } = new();

        public List<Formacao> Formacoes { get; set; } = new();
        public int? FormacaoId { get; set; }

        public Formacao? Formacao => Formacoes.FirstOrDefault(f => f.Id == FormacaoId);
    }

    public class CreatorsTabelaViewModel
    {
        public List<Competicao> Competicoes { get; set; } = new();
        public int? CompeticaoId { get; set; }
        public Competicao? Competicao { get; set; }

        public List<int> Temporadas { get; set; } = new();
        public int Temporada { get; set; }

        // Painel montado pelo formato da competição (pontos corridos, grupos,
        // mata-mata ou fases declaradas) — o mesmo builder da tela interna.
        public Helpers.CompeticaoPainelBuilder.Painel Painel { get; set; } = new();
        public int TotalJogos { get; set; }

        public bool TemAlgoParaMostrar =>
            Painel.Fases.Any() || Painel.Classificacao.Any()
            || Painel.Grupos.Any() || Painel.FasesMataMata.Any();
    }

    // Corpo do POST /creators/simulador/tabela: a competição, a temporada e todos
    // os placares imaginados pelo visitante. Vai e volta a cada mudança porque na
    // área pública não há onde guardar palpite — ele vive no navegador.
    public class SimuladorPalpitesRequest
    {
        public int Competicao { get; set; }
        public int? Temporada { get; set; }
        public List<SimuladorPalpiteDto> Palpites { get; set; } = new();
    }

    public class SimuladorPalpiteDto
    {
        public int JogoId { get; set; }
        // Nulos = jogo sem palpite; o par incompleto é descartado.
        public int? Casa { get; set; }
        public int? Visitante { get; set; }
    }
}
