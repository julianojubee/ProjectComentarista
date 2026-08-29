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
    }
}
