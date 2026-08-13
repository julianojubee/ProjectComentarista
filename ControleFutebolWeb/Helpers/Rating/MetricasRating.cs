using ControleFutebolWeb.Models;

namespace ControleFutebolWeb.Helpers.Rating
{
    // As famílias em que as métricas contínuas são agrupadas. O peso de cada
    // família muda por posição (PesosRating): é o que dá ao zagueiro um caminho
    // próprio até o 10, em vez de disputar a mesma régua do centroavante.
    public enum FamiliaRating
    {
        Finalizacao,
        Criacao,
        Passe,
        Duelo,
        Goleiro,
        Disciplina,
    }

    /// <summary>
    /// Uma métrica contínua do rating automático.
    /// </summary>
    /// <param name="PorNoventa">
    /// true = contagem, comparada por 90 minutos (desarmes, finalizações...).
    /// false = já é uma taxa em % (precisão de passe, aproveitamento de duelo),
    /// que não escala com o tempo em campo.
    /// </param>
    /// <param name="Sinal">+1 quando mais é melhor, -1 quando mais é pior.</param>
    /// <param name="Peso">
    /// Peso dentro da família. Não precisa somar 1: a contribuição da família é a
    /// média ponderada dos z-scores das métricas que puderam ser lidas naquele jogo.
    /// </param>
    public sealed record MetricaRating(
        string Id,
        string Label,
        FamiliaRating Familia,
        double Sinal,
        double Peso,
        bool PorNoventa,
        Func<EstatisticaJogador, double?> Valor);

    // Catálogo das métricas contínuas. Tudo o que é evento raro e decisivo (gol,
    // assistência, pênalti, vermelho) fica FORA daqui: evento não tem média nem
    // desvio úteis por 90 minutos — um gol não é "acima da média de gols", é um gol.
    // Esses entram como valor fixo com retorno decrescente, em RatingAutomaticoHelper.
    public static class MetricasRating
    {
        public static readonly IReadOnlyList<MetricaRating> Todas = new[]
        {
            // Finalização
            M("finalizacoes",      "Finalizações",          FamiliaRating.Finalizacao, +1, 0.35, e => e.FinalizacoesTotal),
            M("finalizacoes_alvo", "Finalizações no alvo",  FamiliaRating.Finalizacao, +1, 0.50, e => e.FinalizacoesNoGol),
            M("offsides",          "Impedimentos",          FamiliaRating.Finalizacao, -1, 0.15, e => e.Offsides),

            // Criação
            M("passes_chave",      "Passes-chave",          FamiliaRating.Criacao, +1, 0.50, e => e.PassesChave),
            M("dribles",           "Dribles tentados",      FamiliaRating.Criacao, +1, 0.25, e => e.DriblesTentados),
            T("dribles_precisao",  "Dribles certos (%)",    FamiliaRating.Criacao, +1, 0.10, Aproveitamento(e => e.DriblesCertos, e => e.DriblesTentados)),
            M("faltas_sofridas",   "Faltas sofridas",       FamiliaRating.Criacao, +1, 0.15, e => e.FaltasSofridas),

            // Passe
            M("passes",            "Passes tentados",       FamiliaRating.Passe, +1, 0.35, e => e.PassesTotal),
            T("passes_precisao",   "Precisão de passe (%)", FamiliaRating.Passe, +1, 0.65, e => e.PrecisaoPasses),

            // Duelo / defesa
            M("desarmes",          "Desarmes",              FamiliaRating.Duelo, +1, 0.25, e => e.Desarmes),
            M("interceptacoes",    "Interceptações",        FamiliaRating.Duelo, +1, 0.20, e => e.Interceptacoes),
            M("bloqueios",         "Bloqueios",             FamiliaRating.Duelo, +1, 0.10, e => e.Bloqueios),
            M("duelos",            "Duelos disputados",     FamiliaRating.Duelo, +1, 0.10, e => e.DuelosTotal),
            T("duelos_precisao",   "Duelos vencidos (%)",   FamiliaRating.Duelo, +1, 0.25, Aproveitamento(e => e.DuelosVencidos, e => e.DuelosTotal)),
            M("dribles_sofridos",  "Dribles sofridos",      FamiliaRating.Duelo, -1, 0.10, e => e.DriblesSofridos),

            // Goleiro
            M("defesas",           "Defesas",               FamiliaRating.Goleiro, +1, 0.50, e => e.Defesas),
            M("gols_sofridos",     "Gols sofridos",         FamiliaRating.Goleiro, -1, 0.50, e => e.GolsSofridos),

            // Disciplina
            M("faltas_cometidas",  "Faltas cometidas",      FamiliaRating.Disciplina, -1, 0.45, e => e.FaltasCometidas),
            M("cartoes_amarelos",  "Cartões amarelos",      FamiliaRating.Disciplina, -1, 0.55, e => e.CartoesAmarelos),
        };

        public static readonly IReadOnlyDictionary<string, MetricaRating> PorId =
            Todas.ToDictionary(m => m.Id);

        private static MetricaRating M(string id, string label, FamiliaRating familia, double sinal,
            double peso, Func<EstatisticaJogador, double?> valor)
            => new(id, label, familia, sinal, peso, PorNoventa: true, valor);

        private static MetricaRating T(string id, string label, FamiliaRating familia, double sinal,
            double peso, Func<EstatisticaJogador, double?> valor)
            => new(id, label, familia, sinal, peso, PorNoventa: false, valor);

        // Taxa de aproveitamento em %. Null sem tentativa: quem não tentou nenhum
        // drible não é "0% de aproveitamento" — é ausência de dado, e entrar como
        // zero puniria o zagueiro que nunca dribla.
        private static Func<EstatisticaJogador, double?> Aproveitamento(
            Func<EstatisticaJogador, int> certos, Func<EstatisticaJogador, int> tentados)
            => e =>
            {
                var t = tentados(e);
                return t > 0 ? certos(e) / (double)t * 100.0 : null;
            };
    }
}
