namespace ControleFutebolWeb.Models
{
    /// <summary>
    /// Origem de uma linha de <see cref="EstatisticaJogador"/> e o que essa origem
    /// consegue informar.
    ///
    /// Existe porque as duas fontes não cobrem os mesmos campos. A api-football manda
    /// a linha inteira; a ESPN (fallback quando a api-football devolve a partida vazia)
    /// publica só gols, assistências, finalizações, faltas, cartões, impedimentos e
    /// defesas — passes, desarmes, interceptações, bloqueios, duelos, dribles e
    /// pênaltis simplesmente não vêm.
    ///
    /// Como as colunas são int, um campo não informado fica gravado como 0, idêntico a
    /// um zero de verdade. Quem consome para gerar nota tem que perguntar aqui antes de
    /// ler o campo: o rating (BaselineRating.ValorNormalizado) e a soma por critérios
    /// (CriteriosNotaHelper) descartam o que a fonte não cobre, e o rating ainda
    /// redistribui o peso das famílias que sobraram. Sem isso, um volante importado da
    /// ESPN levaria z-score negativo em desarmes e duelos que ele pode ter feito.
    /// </summary>
    public static class FonteEstatistica
    {
        public const string ApiFootball = "apifootball";
        public const string Espn = "espn";

        // Allowlist, não blocklist: métrica nova nasce "não coberta pela ESPN" até
        // alguém conferir que a ESPN publica aquilo. Errar para o lado de descartar
        // dado só tira base da nota; errar para o outro lado inventa desempenho.
        //
        // Os ids são os dois vocabulários que leem EstatisticaJogador: o AcaoId dos
        // critérios de nota (CriteriosNotaHelper.Extratores) e o Id das métricas do
        // rating (MetricasRating.Todas) — mais os eventos de RatingAutomaticoHelper.
        private static readonly HashSet<string> CobertosPelaEspn = new(StringComparer.Ordinal)
        {
            // AcaoId dos critérios de nota
            "gol", "gol_sofrido", "assistencia", "defesa",
            "finalizacao", "finalizacao_gol", "offside",
            "falta_sofrida", "falta_cometida",
            "cartao_amarelo", "cartao_vermelho",

            // Id das métricas contínuas do rating
            "finalizacoes", "finalizacoes_alvo", "offsides",
            "faltas_sofridas", "faltas_cometidas",
            "defesas", "gols_sofridos", "cartoes_amarelos",

            // Id dos eventos raros do rating
            "evento_gol", "evento_assistencia", "evento_cartao_vermelho",
        };

        /// <summary>
        /// A fonte informa esse campo? Fonte desconhecida ou vazia é tratada como
        /// api-football: é o que as linhas gravadas antes desta coluna existir são.
        /// </summary>
        public static bool Cobre(string? fonte, string id) =>
            fonte != Espn || CobertosPelaEspn.Contains(id);
    }
}
