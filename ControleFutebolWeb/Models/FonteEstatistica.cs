namespace ControleFutebolWeb.Models
{
    /// <summary>
    /// Origem de uma linha de <see cref="EstatisticaJogador"/> e o que essa origem
    /// consegue informar.
    ///
    /// Existe porque as fontes não cobrem os mesmos campos. A api-football manda a
    /// linha inteira; a ESPN (fallback quando a api-football devolve a partida vazia)
    /// publica só gols, assistências, finalizações, faltas, cartões, impedimentos e
    /// defesas — passes, desarmes, interceptações, bloqueios, duelos, dribles e
    /// pênaltis simplesmente não vêm. O FotMob (fallback para o que nem a ESPN tem,
    /// como a liga do Catar) fica no meio: cobre tudo que a ESPN cobre e mais passes,
    /// desarmes, interceptações, bloqueios, duelos e dribles, mas não distingue pênalti
    /// perdido de defendido.
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
        public const string FotMob = "fotmob";

        /// <summary>
        /// Marcação manual sobre o vídeo da partida (/MarcacaoVideo/Marcar). Usada
        /// nas competições que fonte nenhuma cobre — o Mundial Sub-20 feminino é o
        /// caso que motivou a tela. É a única fonte em que o dado é produzido aqui
        /// dentro, tecla a tecla, e não importado.
        /// </summary>
        public const string Video = "video";

        /// <summary>
        /// Estatística oficial da FIFA por jogadora (fdh-api.fifa.com, a mesma do match
        /// centre do fifa.com) — ver FifaEstatisticasService. Só existe para as
        /// competições que a própria FIFA importa.
        /// </summary>
        public const string Fifa = "fifa";

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

        // Mesma allowlist, mesmo motivo, para o FotMob (ver FotMobService): é tudo que
        // a ESPN cobre MAIS o que ela não publica por jogador e o FotMob publica —
        // passes, passes-chave, desarmes, bloqueios, interceptações, duelos e dribles.
        //
        // Fica de fora só a família de pênaltis além de sofrido/cometido: o FotMob traz
        // penalties_won e conceded_penalties, mas não separa pênalti perdido de pênalti
        // defendido, e sem essa distinção os dois lados do lance ficariam indistinguíveis.
        //
        // "baseline_rating" também fica de fora, mas ele nunca chega aqui: é o critério
        // reservado que guarda a régua calibrada do rating (ver BaselineRating), não um
        // campo de EstatisticaJogador. Está ausente por não ser métrica de fonte nenhuma.
        //
        // A NOTA do jogador é outra coisa e não passa por esta lista: o FotMob publica a
        // dele, FotMobService não a importa (métrica proprietária) e EstatisticaJogador.Rating
        // fica null — que é o campo lido como "Rating médio (api)". A nota que o site
        // mostra sai de RatingAutomaticoHelper sobre as métricas objetivas acima.
        private static readonly HashSet<string> CobertosPeloFotMob =
            new(CobertosPelaEspn, StringComparer.Ordinal)
            {
                // AcaoId dos critérios de nota
                "passe_chave", "desarme", "bloqueio", "interceptacao",
                "duelo_vencido", "drible_certo", "drible_sofrido",
                "penalti_sofrido", "penalti_cometido",

                // Id das métricas contínuas do rating
                "passes", "passes_precisao", "passes_chave",
                "dribles", "dribles_precisao", "dribles_sofridos",
                "desarmes", "interceptacoes", "bloqueios",
                "duelos", "duelos_precisao",
            };

        // A marcação por vídeo cobre tudo que o FotMob cobre MAIS a família inteira
        // de pênaltis: quem marca vê o lance e sabe separar pênalti perdido (a
        // cobrança foi para fora) de pênalti defendido (a goleira pegou) — a
        // distinção que o FotMob não publica e por isso fica de fora lá.
        //
        // A lista continua sendo allowlist pelo mesmo motivo das outras: métrica
        // nova nasce descoberta aqui até ganhar uma tecla no catálogo
        // (AcoesMarcacaoVideo.Todas). Sem tecla ninguém marca aquilo, a coluna
        // fica 0 e o rating leria esse 0 como desempenho ruim de verdade.
        private static readonly HashSet<string> CobertosPeloVideo =
            new(CobertosPeloFotMob, StringComparer.Ordinal)
            {
                // AcaoId dos critérios de nota
                "penalti_perdido", "penalti_defendido",

                // Id dos eventos raros do rating
                "evento_penalti_defendido", "evento_penalti_sofrido",
                "evento_penalti_perdido", "evento_penalti_cometido",
            };

        // A FIFA cobre tudo que a ESPN cobre MAIS passes (tentados e certos), dribles
        // certos e pênalti perdido. Conferido contra players.json do BRA×TAN do
        // Sub-20 Feminino 2026, que traz 112 métricas por jogadora.
        //
        // O bloco defensivo fica de fora porque não existe lá: nada de desarme,
        // interceptação, bloqueio ou duelo — o mais próximo são "pressões defensivas"
        // e "perdas forçadas", que medem outra coisa. Passe-chave também não vem, e
        // drible só vem o COMPLETADO (TakeOnsCompleted): sem os tentados, "dribles" e
        // "dribles_precisao" leriam 0 de volume e aproveitamento que não existem.
        //
        // Pênalti: Penalties − PenaltiesScored é a cobrança desperdiçada, seja para fora
        // ou defendida — do lado de quem bate, as duas são pênalti perdido. O que não dá
        // para saber é o lado da goleira (defendido) nem quem sofreu ou cometeu.
        private static readonly HashSet<string> CobertosPelaFifa =
            new(CobertosPelaEspn, StringComparer.Ordinal)
            {
                // AcaoId dos critérios de nota
                "drible_certo", "penalti_perdido",

                // Id das métricas contínuas do rating
                "passes", "passes_precisao",

                // Id dos eventos raros do rating
                "evento_penalti_perdido",
            };

        /// <summary>
        /// A fonte informa esse campo? Fonte desconhecida ou vazia é tratada como
        /// api-football: é o que as linhas gravadas antes desta coluna existir são.
        /// </summary>
        public static bool Cobre(string? fonte, string id) => fonte switch
        {
            Espn => CobertosPelaEspn.Contains(id),
            FotMob => CobertosPeloFotMob.Contains(id),
            Video => CobertosPeloVideo.Contains(id),
            Fifa => CobertosPelaFifa.Contains(id),
            _ => true,
        };
    }
}
