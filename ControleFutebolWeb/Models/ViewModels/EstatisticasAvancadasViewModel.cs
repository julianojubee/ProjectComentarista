namespace ControleFutebolWeb.Models.ViewModels
{
    /// <summary>
    /// Tela /Jogadores/EstatisticasAvancadas/{id}: o que o FotMob publica sobre o
    /// jogador, buscado no clique e nunca gravado (ver FotMobPerfilService).
    ///
    /// Nada aqui vira registro no banco. É por isso que esta tela não pode criar
    /// divergência com o resto do sistema: não existe cópia nossa desses números para
    /// discordar deles depois.
    /// </summary>
    public class EstatisticasAvancadasViewModel
    {
        // Do NOSSO cadastro — só para o cabeçalho da página e o link de volta. Os dados
        // de identificação do FotMob (altura, país, clube) não entram na tela: já
        // divergem do nosso cadastro hoje e mostrá-los faria o mesmo jogador aparecer
        // diferente em duas telas do próprio sistema.
        public int JogadorId { get; set; }
        public string JogadorNome { get; set; } = "";

        public long IdFotMob { get; set; }

        /// <summary>Preenchido quando a busca falhou; a view mostra o aviso e mais nada.</summary>
        public string? Erro { get; set; }

        /// <summary>
        /// Como o FotMob chama este jogador. Aparece uma vez, discreto, para o usuário
        /// confirmar que o vínculo aponta para a pessoa certa — o id foi descoberto por
        /// casamento automático, e conferir de olho custa nada.
        /// </summary>
        public string? NomeNoFotMob { get; set; }

        /// <summary>
        /// Posição principal segundo eles. Não substitui a nossa: a página do jogador já
        /// mostra a distribuição real por jogo, e ver as duas juntas é que informa.
        /// </summary>
        public string? PosicaoPrincipal { get; set; }

        public string? ValorDeMercado { get; set; }

        /// <summary>
        /// Temporadas, cada uma com as competições que o jogador disputou nela. O
        /// agrupamento existe porque a lista crua é longa e repetitiva — um jogador com
        /// carreira comprida tem catorze linhas do tipo "2025/2026 · AFC Champions
        /// League", e escolher ano e competição em dois passos é bem mais legível do que
        /// varrer tudo de uma vez.
        /// </summary>
        public List<TemporadaAgrupada> Temporadas { get; set; } = new();

        /// <summary>Id (entryId) da competição em exibição.</summary>
        public string? CompeticaoSelecionada { get; set; }

        /// <summary>Nome da temporada em exibição — abre expandida na tela.</summary>
        public string? TemporadaSelecionada { get; set; }

        public List<GrupoMetricasFotMob> Grupos { get; set; } = new();
        public List<JogoFotMob> Jogos { get; set; } = new();
        public List<PassagemFotMob> Carreira { get; set; } = new();
        public List<TituloFotMob> Titulos { get; set; } = new();

        public bool TemDesempenho => Grupos.Count > 0;
    }

    /// <summary>Uma temporada e as competições disputadas nela.</summary>
    public class TemporadaAgrupada
    {
        /// <summary>"2025/2026" ou "2025", conforme a liga.</summary>
        public string Nome { get; set; } = "";
        public List<CompeticaoTemporada> Competicoes { get; set; } = new();
    }

    public class CompeticaoTemporada
    {
        /// <summary>
        /// O entryId ("1-1"), que é o que a fonte aceita para pedir as estatísticas —
        /// não o ano nem o id da liga.
        /// </summary>
        public string Id { get; set; } = "";
        public string Nome { get; set; } = "";

        /// <summary>
        /// False quando só existe o básico daquela competição. A tela marca essas para
        /// o usuário não clicar esperando o quadro completo e achar que quebrou.
        /// </summary>
        public bool TemDadosDetalhados { get; set; }
    }

    public class GrupoMetricasFotMob
    {
        public string Nome { get; set; } = "";
        public List<MetricaFotMob> Metricas { get; set; } = new();
    }

    public class MetricaFotMob
    {
        public string Nome { get; set; } = "";
        public string Valor { get; set; } = "";

        /// <summary>
        /// A métrica já é uma taxa (precisão de passe, duelos vencidos %). Nesse caso o
        /// valor sai com "%" e sem média por 90 — o FotMob repete a própria taxa no
        /// campo per90, e exibi-la como "84,8 por 90 minutos" seria absurdo.
        /// </summary>
        public bool EhPercentual { get; set; }

        /// <summary>
        /// Média por 90 minutos, para comparar quem jogou pouco com quem jogou muito.
        /// Null nas métricas percentuais.
        /// </summary>
        public double? Por90 { get; set; }

        /// <summary>
        /// Posição do jogador nessa métrica dentro da competição, de 0 a 100. É o dado
        /// mais valioso da tela e o único que não teríamos como calcular — depende de
        /// ter a liga inteira. Cálculo do FotMob, e a tela diz isso.
        /// </summary>
        public double? Percentil { get; set; }
    }

    /// <summary>
    /// Um jogo na base do FotMob. A lista é maior que o "Histórico por Jogo" da página
    /// do jogador porque inclui partidas que ninguém importou (seleção, outras
    /// competições) — a tela rotula a origem para que a diferença de contagem não passe
    /// por bug.
    ///
    /// Sem a nota do FotMob de propósito: a nota do site é a nota do site.
    /// </summary>
    public class JogoFotMob
    {
        public DateTime? Data { get; set; }
        public string? Competicao { get; set; }
        public string? Adversario { get; set; }

        /// <summary>Id do adversário, só para montar o escudo (/MediaProxy/Escudo/{id}).</summary>
        public long? AdversarioId { get; set; }
        public bool Mandante { get; set; }
        public string? Placar { get; set; }
        public int? Minutos { get; set; }
        public int Gols { get; set; }
        public int Assistencias { get; set; }
        public int CartoesAmarelos { get; set; }
        public int CartoesVermelhos { get; set; }
    }

    public class PassagemFotMob
    {
        public string Categoria { get; set; } = "";
        public string Time { get; set; } = "";

        /// <summary>Id do clube/seleção, só para montar o escudo.</summary>
        public long? TimeId { get; set; }
        public string Periodo { get; set; } = "";
        public string? Jogos { get; set; }
        public string? Gols { get; set; }
        public string? Assistencias { get; set; }
        public bool Atual { get; set; }
    }

    public class TituloFotMob
    {
        public string Time { get; set; } = "";

        /// <summary>Id do clube/seleção, só para montar o escudo.</summary>
        public long? TimeId { get; set; }
        public string Competicao { get; set; } = "";
        public string Temporadas { get; set; } = "";
        public int Quantidade { get; set; }
    }
}
