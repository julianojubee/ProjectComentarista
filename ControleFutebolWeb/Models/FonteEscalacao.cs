namespace ControleFutebolWeb.Models
{
    /// <summary>
    /// Quem publicou a escalação importada de um jogo — o valor de
    /// <see cref="Escalacao.Fonte"/>.
    ///
    /// Só as linhas COMPARTILHADAS (UsuarioId == null) carregam fonte: são elas que
    /// representam "o que aconteceu na partida". As cópias pessoais nascem delas e
    /// depois recebem a edição do analista, então perguntar a origem de uma linha
    /// pessoal não faz sentido — para saber se ele mexeu existe
    /// AnalisarViewModel.EscalacaoEditada.
    ///
    /// Fonte null = importação anterior a esta coluna, ou escalação que a tela montou
    /// sozinha (última escalação do time / slots vazios). Nos dois casos não dá para
    /// afirmar que aquilo é o XI do jogo, e o selo avisa.
    /// </summary>
    public static class FonteEscalacao
    {
        public const string ApiFootball = "apifootball";
        public const string Espn = "espn";
        public const string Transfermarkt = "transfermarkt";

        // Terceira fonte de reserva, para as ligas que a ESPN não cataloga — ver
        // FotMobEscalacaoService.
        public const string FotMob = "fotmob";

        // Fonte única das competições que só a FIFA publica (Mundial Sub-20 Feminino e
        // as demais de base/feminino fora do catálogo da api-football e da ESPN) — ver
        // FifaEscalacaoService. Não é fallback de ninguém: nessas competições ela é a
        // primeira e a última fonte.
        public const string Fifa = "fifa";

        // Fonte única do Brasileirão Feminino, raspada do ogol.com.br — ver
        // OgolEscalacaoService. Como a da FIFA, não é fallback de ninguém: naquela
        // competição a api-football traz o jogo sem escalação e nem a ESPN nem o FotMob
        // catalogam a liga.
        public const string Ogol = "ogol";
    }
}
