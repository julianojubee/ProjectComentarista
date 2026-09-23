using ControleFutebolWeb.Models.Campeonatos;

namespace ControleFutebolWeb.Helpers.Campeonatos
{
    /// <summary>
    /// Gera as partidas de uma fase de campeonato próprio: todos contra todos
    /// (pontos corridos e cada grupo) e o chaveamento completo do mata-mata.
    ///
    /// Não toca no banco: recebe ids de CampeonatoParticipante e devolve partidas
    /// sem CampeonatoId/FaseId, que o CampeonatoService preenche e grava.
    /// </summary>
    public static class GeradorPartidasCampeonato
    {
        // ── Todos contra todos ──────────────────────────────────────────────

        /// <summary>
        /// Método do círculo: o primeiro participante fica parado e os outros giram,
        /// o que dá n-1 rodadas sem ninguém jogar duas vezes na mesma rodada. Com
        /// número ímpar entra uma "folga" (null) e quem cai contra ela não joga.
        ///
        /// Mando: quem está na metade de cima da roda joga em casa, e o jogo do
        /// participante parado alterna a cada rodada. Assim cada um termina o turno
        /// com mandos que diferem em no máximo um.
        /// </summary>
        public static List<PartidaCampeonato> TodosContraTodos(
            IReadOnlyList<int> participantes, bool idaEVolta, int rodadaInicial, string? grupo = null)
        {
            var partidas = new List<PartidaCampeonato>();
            if (participantes.Count < 2) return partidas;

            var roda = participantes.Select(p => (int?)p).ToList();
            if (roda.Count % 2 == 1) roda.Add(null);

            var n = roda.Count;
            var rodadasPorTurno = n - 1;

            for (var r = 0; r < rodadasPorTurno; r++)
            {
                for (var i = 0; i < n / 2; i++)
                {
                    var a = roda[i];
                    var b = roda[n - 1 - i];
                    if (a == null || b == null) continue;

                    var inverte = i == 0 && r % 2 == 1;
                    partidas.Add(Nova(rodadaInicial + r, inverte ? b : a, inverte ? a : b, grupo));
                }

                // Gira todos menos o primeiro: o último passa para a segunda posição.
                var ultimo = roda[n - 1];
                roda.RemoveAt(n - 1);
                roda.Insert(1, ultimo);
            }

            if (idaEVolta)
            {
                // Returno: mesma sequência de confrontos, mando invertido.
                partidas.AddRange(partidas.ToList().Select(p => Nova(
                    p.Rodada + rodadasPorTurno, p.ParticipanteVisitanteId, p.ParticipanteCasaId, grupo)));
            }

            return partidas;
        }

        /// <summary>
        /// Uma tabela de todos contra todos por grupo, com as rodadas alinhadas: a
        /// rodada 1 de todos os grupos é a mesma rodada do campeonato.
        /// </summary>
        public static List<PartidaCampeonato> Grupos(
            IReadOnlyDictionary<string, List<int>> participantesPorGrupo, bool idaEVolta, int rodadaInicial)
        {
            return participantesPorGrupo
                .OrderBy(g => g.Key, StringComparer.CurrentCulture)
                .SelectMany(g => TodosContraTodos(g.Value, idaEVolta, rodadaInicial, RotuloGrupo(g.Key)))
                .ToList();
        }

        public static string RotuloGrupo(string letra) => $"Grupo {letra}";

        /// <summary>
        /// Sorteio por potes, sem aleatoriedade: a lista já vem na ordem de força
        /// (cabeças de chave primeiro) e cada "pote" de N participantes se espalha
        /// um por grupo. Os cabeças de chave nunca caem juntos.
        /// </summary>
        public static Dictionary<string, List<int>> DistribuirEmGrupos(IReadOnlyList<int> participantesPorForca, int quantidadeGrupos)
        {
            if (quantidadeGrupos < 1) throw new ArgumentOutOfRangeException(nameof(quantidadeGrupos));

            var grupos = Enumerable.Range(0, quantidadeGrupos)
                .ToDictionary(i => ((char)('A' + i)).ToString(), _ => new List<int>());

            for (var i = 0; i < participantesPorForca.Count; i++)
                grupos[((char)('A' + i % quantidadeGrupos)).ToString()].Add(participantesPorForca[i]);

            return grupos;
        }

        // ── Mata-mata ───────────────────────────────────────────────────────

        /// <summary>
        /// Monta o chaveamento inteiro de uma vez: a primeira etapa com os
        /// confrontos e as seguintes com vagas em aberto (participante null), que
        /// <see cref="AvancarVencedores"/> preenche conforme os resultados.
        ///
        /// A lista vem na ordem de força. A chave é a do tênis (1º x último, e 1º e
        /// 2º só se cruzam na final). Quando o número não é potência de 2, os mais
        /// fortes passam direto para a segunda etapa (bye) e já aparecem nela.
        ///
        /// ChaveOrdem numera os confrontos de cada etapa a partir de 1; o vencedor do
        /// confronto c vai para o confronto (c+1)/2 da etapa seguinte, como mandante
        /// da ida se c for ímpar.
        /// </summary>
        public static List<PartidaCampeonato> MataMata(IReadOnlyList<int> participantesPorForca, bool idaEVolta, int rodadaInicial)
        {
            var partidas = new List<PartidaCampeonato>();
            var n = participantesPorForca.Count;
            if (n < 2) return partidas;

            var tamanho = 1;
            while (tamanho < n) tamanho *= 2;

            // Vagas da etapa atual, na ordem da chave. Null = bye (ninguém nessa vaga).
            var vagas = OrdemDaChave(tamanho)
                .Select(semente => semente <= n ? (int?)participantesPorForca[semente - 1] : null)
                .ToList();

            var rodada = rodadaInicial;
            var primeiraEtapa = true;

            while (vagas.Count >= 2)
            {
                var confrontos = vagas.Count / 2;
                var nome = NomeEtapa(confrontos);
                var proximas = new List<int?>();

                for (var c = 1; c <= confrontos; c++)
                {
                    var a = vagas[2 * (c - 1)];
                    var b = vagas[2 * (c - 1) + 1];

                    // Bye só existe na primeira etapa: quem não tem adversário já
                    // ocupa a vaga dele na segunda, e esse confronto nem é criado.
                    if (primeiraEtapa && (a == null || b == null))
                    {
                        proximas.Add(a ?? b);
                        continue;
                    }

                    partidas.Add(Nova(rodada, a, b, nome, c));
                    if (idaEVolta) partidas.Add(Nova(rodada + 1, b, a, nome, c));
                    proximas.Add(null);
                }

                vagas = proximas;
                rodada += idaEVolta ? 2 : 1;
                primeiraEtapa = false;
            }

            return partidas;
        }

        /// <summary>
        /// Ordem das sementes na chave (8 → 1,8,4,5,2,7,3,6). Cada par consecutivo é um
        /// confronto e o favorito só encontra outro favorito o mais tarde possível.
        /// </summary>
        public static List<int> OrdemDaChave(int tamanho)
        {
            var ordem = new List<int> { 1 };
            var atual = 1;
            while (atual < tamanho)
            {
                atual *= 2;
                ordem = ordem.SelectMany(s => new[] { s, atual + 1 - s }).ToList();
            }
            return ordem;
        }

        // Nomes que o CompeticaoPainelBuilder já sabe ordenar e o FaseJogoClassifier
        // reconhece como mata-mata.
        public static string NomeEtapa(int confrontos) => confrontos switch
        {
            1 => "Final",
            2 => "Semifinal",
            4 => "Quartas",
            8 => "Oitavas",
            16 => "16avos",
            32 => "32avos",
            _ => $"Rodada de {confrontos * 2}"
        };

        /// <summary>
        /// Vencedor de um confronto (um jogo, ou ida e volta): mais gols no agregado;
        /// empatado, os pênaltis do último jogo. Null enquanto falta placar, falta um
        /// dos lados ou o empate não foi decidido nos pênaltis. Sem gol fora de casa.
        /// </summary>
        public static int? Vencedor(IReadOnlyCollection<PartidaCampeonato> jogosDoConfronto)
        {
            if (jogosDoConfronto.Count == 0) return null;
            if (jogosDoConfronto.Any(p => p.ParticipanteCasaId == null || p.ParticipanteVisitanteId == null
                                          || p.PlacarCasa == null || p.PlacarVisitante == null))
                return null;

            var gols = new Dictionary<int, int>();
            foreach (var p in jogosDoConfronto)
            {
                gols[p.ParticipanteCasaId!.Value] = gols.GetValueOrDefault(p.ParticipanteCasaId.Value) + p.PlacarCasa!.Value;
                gols[p.ParticipanteVisitanteId!.Value] = gols.GetValueOrDefault(p.ParticipanteVisitanteId.Value) + p.PlacarVisitante!.Value;
            }
            if (gols.Count != 2) return null;

            var lados = gols.OrderByDescending(g => g.Value).ToList();
            if (lados[0].Value != lados[1].Value) return lados[0].Key;

            var ultimo = jogosDoConfronto.OrderBy(p => p.Rodada).ThenBy(p => p.Id).Last();
            if (ultimo.PenaltisCasa == null || ultimo.PenaltisVisitante == null
                || ultimo.PenaltisCasa == ultimo.PenaltisVisitante)
                return null;

            return ultimo.PenaltisCasa > ultimo.PenaltisVisitante
                ? ultimo.ParticipanteCasaId
                : ultimo.ParticipanteVisitanteId;
        }

        /// <summary>
        /// Leva o vencedor de cada confronto decidido para a vaga dele na etapa
        /// seguinte — e tira de lá quem não é mais vencedor (placar corrigido ou
        /// apagado). Uma vaga só muda enquanto o confronto de destino não tem nenhum
        /// placar: com a bola já rolando lá, corrigir o passado fica por conta do
        /// organizador. Devolve quantas partidas mudaram.
        /// </summary>
        public static int AvancarVencedores(IReadOnlyCollection<PartidaCampeonato> partidasDoMataMata)
        {
            // Etapas em ordem de rodada — e não pela quantidade de confrontos: com bye,
            // a primeira etapa pode ter menos jogos que a segunda (5 participantes dão
            // 1 jogo antes da semifinal).
            var etapas = partidasDoMataMata
                .Where(p => p.ChaveOrdem != null)
                .GroupBy(p => p.Grupo ?? "")
                .OrderBy(g => g.Min(p => p.Rodada))
                .Select(g => g.GroupBy(p => p.ChaveOrdem!.Value)
                              .ToDictionary(c => c.Key, c => c.OrderBy(p => p.Rodada).ThenBy(p => p.Id).ToList()))
                .ToList();

            var alteradas = 0;

            for (var e = 0; e < etapas.Count - 1; e++)
            {
                foreach (var (chave, jogos) in etapas[e])
                {
                    if (!etapas[e + 1].TryGetValue((chave + 1) / 2, out var destino)) continue;
                    if (destino.Any(p => p.PlacarCasa != null || p.PlacarVisitante != null)) continue;

                    var vencedor = Vencedor(jogos);
                    var vagaA = chave % 2 == 1;

                    for (var i = 0; i < destino.Count; i++)
                    {
                        // Na ida a vaga A é o mandante; na volta, o visitante.
                        var noMandante = vagaA == (i % 2 == 0);
                        var partida = destino[i];

                        if (noMandante && partida.ParticipanteCasaId != vencedor)
                        {
                            partida.ParticipanteCasaId = vencedor;
                            alteradas++;
                        }
                        else if (!noMandante && partida.ParticipanteVisitanteId != vencedor)
                        {
                            partida.ParticipanteVisitanteId = vencedor;
                            alteradas++;
                        }
                    }
                }
            }

            return alteradas;
        }

        private static PartidaCampeonato Nova(int rodada, int? casa, int? visitante, string? grupo, int? chaveOrdem = null) => new()
        {
            Rodada = rodada,
            ParticipanteCasaId = casa,
            ParticipanteVisitanteId = visitante,
            Grupo = grupo,
            ChaveOrdem = chaveOrdem
        };
    }
}
