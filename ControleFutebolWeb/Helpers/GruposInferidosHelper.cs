namespace ControleFutebolWeb.Helpers
{
    /// <summary>
    /// Fase de grupos que a api-football não identifica: algumas competições (Libertadores
    /// Feminina, Mundial Feminino Sub-17...) vêm sem standings e sem fixture.league.group,
    /// com round genérico ("Group Stage") ou com o nome do torneio ("World Cup Women U17 - 1").
    /// Os grupos são deduzidos dos confrontos: num grupo, os times só jogam entre si e todos
    /// se enfrentam, então cada grupo é um componente conexo de times em turno completo.
    ///
    /// As letras seguem a ordem do primeiro jogo de cada grupo (desempate pelo menor id de
    /// fixture). A API não dá como saber a letra oficial, então ela pode não bater com o
    /// sorteio; a sincronização não sobrescreve um "Group X" já gravado, então a letra
    /// corrigida à mão permanece.
    /// </summary>
    public static class GruposInferidosHelper
    {
        public record Partida(long FixtureId, int CasaId, int VisitanteId, DateTimeOffset? Data);

        public record Inferencia(string Grupo, int Rodada);

        // Grupo de verdade tem de 3 a 6 times; um componente maior é liga/fase de liga
        // (Champions "League Stage"), que não deve virar grupo.
        private const int MinTimes = 3;
        private const int MaxTimes = 6;

        /// <summary>
        /// FixtureId → grupo ("Group A"...) e rodada (1, 2, 3...: o n-ésimo jogo dos times
        /// dentro do grupo). Vazio quando os confrontos não formam ao menos dois grupos em
        /// turno completo — aí a competição não é de grupos e nada é deduzido.
        /// </summary>
        public static Dictionary<long, Inferencia> Inferir(IReadOnlyCollection<Partida> partidas)
        {
            var resultado = new Dictionary<long, Inferencia>();
            if (partidas.Count == 0) return resultado;

            // Union-find dos times pelos confrontos.
            var pai = new Dictionary<int, int>();
            int Raiz(int t)
            {
                if (!pai.TryGetValue(t, out var p)) { pai[t] = t; return t; }
                if (p == t) return t;
                var r = Raiz(p);
                pai[t] = r;
                return r;
            }
            foreach (var p in partidas)
            {
                var a = Raiz(p.CasaId);
                var b = Raiz(p.VisitanteId);
                if (a != b) pai[a] = b;
            }

            var componentes = partidas
                .GroupBy(p => Raiz(p.CasaId))
                .Select(g => g.ToList())
                .ToList();
            if (componentes.Count < 2) return resultado;

            foreach (var jogos in componentes)
            {
                var times = jogos.SelectMany(j => new[] { j.CasaId, j.VisitanteId }).Distinct().ToList();
                if (times.Count < MinTimes || times.Count > MaxTimes) return new();

                // Turno completo: todo par de times se enfrenta ao menos uma vez.
                var confrontos = jogos
                    .Select(j => (Math.Min(j.CasaId, j.VisitanteId), Math.Max(j.CasaId, j.VisitanteId)))
                    .ToHashSet();
                if (confrontos.Count != times.Count * (times.Count - 1) / 2) return new();
            }

            var ordenados = componentes
                .OrderBy(c => c.Min(j => j.Data ?? DateTimeOffset.MaxValue))
                .ThenBy(c => c.Min(j => j.FixtureId))
                .ToList();

            for (var i = 0; i < ordenados.Count; i++)
            {
                var grupo = $"Group {(char)('A' + i)}";
                var jogosDoTime = new Dictionary<int, int>();
                foreach (var j in ordenados[i].OrderBy(j => j.Data ?? DateTimeOffset.MaxValue).ThenBy(j => j.FixtureId))
                {
                    var rodada = Math.Max(jogosDoTime.GetValueOrDefault(j.CasaId),
                                          jogosDoTime.GetValueOrDefault(j.VisitanteId)) + 1;
                    jogosDoTime[j.CasaId] = rodada;
                    jogosDoTime[j.VisitanteId] = rodada;
                    resultado[j.FixtureId] = new Inferencia(grupo, rodada);
                }
            }

            return resultado;
        }
    }
}
