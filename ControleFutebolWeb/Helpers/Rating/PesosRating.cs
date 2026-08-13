namespace ControleFutebolWeb.Helpers.Rating
{
    // Quanto cada família de ação pesa na nota de cada posição. É a peça que dá
    // "chance igual de tirar 10" a todo mundo: o zagueiro chega lá pelo Duelo,
    // o centroavante pela Finalização, e nenhum dos dois disputa a régua do outro.
    //
    // Cada linha soma 1,0 — assim a escala da nota não muda de posição para posição.
    public static class PesosRating
    {
        public static readonly IReadOnlyDictionary<FamiliaRating, double> Neutro = Linha(0.20, 0.20, 0.20, 0.20, 0.00, 0.20);

        private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<FamiliaRating, double>> PorGrupo =
            new Dictionary<string, IReadOnlyDictionary<FamiliaRating, double>>
            {
                //                        Final. Criação Passe  Duelo  Goleiro Disc.
                ["GOLEIRO"]  = Linha(0.00, 0.00, 0.10, 0.00, 0.75, 0.15),
                ["ZAGUEIRO"] = Linha(0.05, 0.05, 0.25, 0.50, 0.00, 0.15),
                ["LATERAL"]  = Linha(0.10, 0.20, 0.20, 0.35, 0.00, 0.15),
                ["ALA"]      = Linha(0.10, 0.20, 0.20, 0.35, 0.00, 0.15),
                ["VOLANTE"]  = Linha(0.05, 0.15, 0.30, 0.35, 0.00, 0.15),
                ["MEIA"]     = Linha(0.15, 0.35, 0.25, 0.15, 0.00, 0.10),
                ["PONTA"]    = Linha(0.30, 0.30, 0.10, 0.20, 0.00, 0.10),
                ["ATACANTE"] = Linha(0.40, 0.20, 0.10, 0.20, 0.00, 0.10),
            };

        /// <summary>
        /// Pesos do grupo de posição. Posição desconhecida cai no <see cref="Neutro"/>,
        /// que distribui igualmente entre as famílias de jogador de linha — nota menos
        /// precisa, mas nunca uma nota que premia o jogador pela régua errada.
        /// </summary>
        public static IReadOnlyDictionary<FamiliaRating, double> Do(string? grupo)
            => grupo != null && PorGrupo.TryGetValue(grupo, out var pesos) ? pesos : Neutro;

        /// <summary>Posições em que o resultado defensivo do time entra na nota.</summary>
        public static bool ResponsavelDefensivo(string? grupo)
            => grupo is "GOLEIRO" or "ZAGUEIRO" or "LATERAL" or "ALA";

        private static IReadOnlyDictionary<FamiliaRating, double> Linha(
            double finalizacao, double criacao, double passe, double duelo, double goleiro, double disciplina)
            => new Dictionary<FamiliaRating, double>
            {
                [FamiliaRating.Finalizacao] = finalizacao,
                [FamiliaRating.Criacao] = criacao,
                [FamiliaRating.Passe] = passe,
                [FamiliaRating.Duelo] = duelo,
                [FamiliaRating.Goleiro] = goleiro,
                [FamiliaRating.Disciplina] = disciplina,
            };
    }
}
