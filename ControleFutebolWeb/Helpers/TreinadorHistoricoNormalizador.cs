namespace ControleFutebolWeb.Helpers
{
    /// <summary>
    /// Corrige passagens de treinador que a api-football devolve "em aberto" (end = null)
    /// mesmo já tendo terminado.
    ///
    /// Isso acontece porque <c>UnirCarreiras</c> junta dois registros do mesmo técnico: o
    /// stub, preso ao clube atual, e o registro completo. Quando o técnico troca de clube e
    /// a API não fecha a passagem anterior no registro completo, as duas ficam sem data de
    /// fim — e o sistema mostrava o técnico dirigindo dois times ao mesmo tempo.
    ///
    /// Regra: só a passagem mais recente pode estar aberta. Qualquer passagem aberta que
    /// tenha outra depois dela é fechada na data em que a seguinte começou — a melhor
    /// aproximação disponível para quando ele saiu.
    /// </summary>
    public static class TreinadorHistoricoNormalizador
    {
        /// <param name="passagens">
        /// Passagens ordenadas da mais recente para a mais antiga (mesma ordem que
        /// <c>ApiFootballService.UnirCarreiras</c> devolve).
        /// </param>
        /// <returns>As datas de fim corrigidas, na mesma ordem da entrada.</returns>
        public static List<DateTime?> FecharPassagensAbertasAntigas(
            IReadOnlyList<(DateTime? Inicio, DateTime? Fim)> passagens)
        {
            var fins = passagens.Select(p => p.Fim).ToList();

            for (var i = 1; i < passagens.Count; i++)
            {
                if (fins[i] != null) continue;

                // Início da passagem imediatamente mais recente que esta. Se ele for
                // desconhecido, não dá pra inferir nada — melhor deixar aberta do que
                // inventar uma data.
                var inicioDaSeguinte = passagens[i - 1].Inicio;
                if (inicioDaSeguinte == null) continue;

                fins[i] = inicioDaSeguinte;
            }

            return fins;
        }
    }
}
