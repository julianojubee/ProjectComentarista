namespace ControleFutebolWeb.Models
{

    public class TimeDetalhesViewModel
    {
        public Time Time { get; set; } // entidade do banco
        public List<Jogador> Elenco { get; set; } // jogadores vinculados ao time
        public List<Jogo> Jogos { get; set; } // todos os jogos do time
        public List<Jogo> JogosPassados { get; set; } // últimos jogos já realizados
        public List<Jogo> JogosFuturos { get; set; } // próximos jogos agendados
        public ICollection<TimeEscalacaoPadrao> TimeEscalacaoPadrao { get; set; }
        public IEnumerable<Formacao> Formacoes { get; set; } // lista de formações disponíveis
        public Treinador? Treinador { get; set; }
        // Competições com link apifoot: configurado (para o painel de estatísticas da temporada)
        public List<CompeticaoApiItem> CompeticoesApi { get; set; } = new();
        // Listas para o modal de vincular/cadastrar treinador
        public List<Treinador> TodosTreinadores { get; set; } = new();
        public List<Nacionalidade> Nacionalidades { get; set; } = new();
        // Aba "Estatísticas do Elenco": números agregados de cada jogador na temporada
        // (minutos, contribuição, defesa, disciplina) + o retrato do elenco em Resumo.
        public List<ViewModels.JogadorElencoStatViewModel> EstatisticasElenco { get; set; } = new();
        public ViewModels.ElencoResumoViewModel ElencoResumo { get; set; } = new();
        // Títulos conquistados pelo time, da temporada mais recente para a mais antiga
        // (calculados dos jogos por Helpers.TitulosHelper — não há cadastro de campeões).
        public List<Helpers.TitulosHelper.Titulo> Titulos { get; set; } = new();
        // Temporadas com jogos realizados; o painel do elenco mostra uma de cada vez.
        public List<int> TemporadasElenco { get; set; } = new();
        public int TemporadaElencoSelecionada { get; set; }
        // Jogo de onde veio a escalação exibida no campinho (a última usada pelo
        // clube). Null = está sendo mostrada a escalação padrão salva/os slots
        // vazios da formação padrão.
        public Jogo? EscalacaoUltimoJogo { get; set; }
        // Formação do campinho: a do último jogo quando há um, senão a padrão.
        public int FormacaoExibidaId { get; set; }
        public string? FormacaoExibidaNome { get; set; }
    }

    public class CompeticaoApiItem
    {
        public string Nome     { get; set; } = "";
        public int    LeagueId { get; set; }
        public int    Season   { get; set; }
    }
}