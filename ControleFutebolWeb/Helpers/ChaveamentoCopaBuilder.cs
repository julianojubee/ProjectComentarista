using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.ViewModels;

namespace ControleFutebolWeb.Helpers
{
    /// <summary>
    /// Monta o chaveamento (mata-mata) da Copa do Mundo 2026 a partir do template
    /// oficial da FIFA, preenchendo os slots com as seleções previstas pelos grupos
    /// e sobrescrevendo com os jogos reais conforme vão sendo importados/realizados.
    ///
    /// Template oficial (nºs de partida 73–104):
    ///   https://en.wikipedia.org/wiki/2026_FIFA_World_Cup_knockout_stage
    /// </summary>
    public static class ChaveamentoCopaBuilder
    {
        // Especificação de cada slot:
        //   W:<g>      → 1º colocado do grupo <g>
        //   RU:<g>     → 2º colocado do grupo <g>
        //   3:<g,g,..> → melhor 3º colocado vindo de um daqueles grupos
        //   WIN:<n>    → vencedor da partida nº <n>
        //   LOSE:<n>   → perdedor da partida nº <n>
        private record ConfrontoSpec(int Numero, string Casa, string Visitante);

        private record Fase(string Chave, string Nome, string NomeCurto, ConfrontoSpec[] Confrontos);

        private static readonly Fase[] Template =
        {
            new("R32", "Dezesseis-avos de Final", "1/16", new[]
            {
                new ConfrontoSpec(73, "RU:A", "RU:B"),
                new ConfrontoSpec(74, "W:E",  "3:A,B,C,D,F"),
                new ConfrontoSpec(75, "W:F",  "RU:C"),
                new ConfrontoSpec(76, "W:C",  "RU:F"),
                new ConfrontoSpec(77, "W:I",  "3:C,D,F,G,H"),
                new ConfrontoSpec(78, "RU:E", "RU:I"),
                new ConfrontoSpec(79, "W:A",  "3:C,E,F,H,I"),
                new ConfrontoSpec(80, "W:L",  "3:E,H,I,J,K"),
                new ConfrontoSpec(81, "W:D",  "3:B,E,F,I,J"),
                new ConfrontoSpec(82, "W:G",  "3:A,E,H,I,J"),
                new ConfrontoSpec(83, "RU:K", "RU:L"),
                new ConfrontoSpec(84, "W:H",  "RU:J"),
                new ConfrontoSpec(85, "W:B",  "3:E,F,G,I,J"),
                new ConfrontoSpec(86, "W:J",  "RU:H"),
                new ConfrontoSpec(87, "W:K",  "3:D,E,I,J,L"),
                new ConfrontoSpec(88, "RU:D", "RU:G"),
            }),
            new("R16", "Oitavas de Final", "1/8", new[]
            {
                new ConfrontoSpec(89, "WIN:74", "WIN:77"),
                new ConfrontoSpec(90, "WIN:73", "WIN:75"),
                new ConfrontoSpec(91, "WIN:76", "WIN:78"),
                new ConfrontoSpec(92, "WIN:79", "WIN:80"),
                new ConfrontoSpec(93, "WIN:83", "WIN:84"),
                new ConfrontoSpec(94, "WIN:81", "WIN:82"),
                new ConfrontoSpec(95, "WIN:86", "WIN:88"),
                new ConfrontoSpec(96, "WIN:85", "WIN:87"),
            }),
            new("QF", "Quartas de Final", "Quartas", new[]
            {
                new ConfrontoSpec(97,  "WIN:89", "WIN:90"),
                new ConfrontoSpec(98,  "WIN:93", "WIN:94"),
                new ConfrontoSpec(99,  "WIN:91", "WIN:92"),
                new ConfrontoSpec(100, "WIN:95", "WIN:96"),
            }),
            new("SF", "Semifinais", "Semis", new[]
            {
                new ConfrontoSpec(101, "WIN:97", "WIN:98"),
                new ConfrontoSpec(102, "WIN:99", "WIN:100"),
            }),
            new("TP", "Disputa de 3º Lugar", "3º Lugar", new[]
            {
                new ConfrontoSpec(103, "LOSE:101", "LOSE:102"),
            }),
            new("F", "Final", "Final", new[]
            {
                new ConfrontoSpec(104, "WIN:101", "WIN:102"),
            }),
        };

        public static ChaveamentoCopaViewModel Construir(
            List<GrupoViewModel> grupos,
            List<Classificacao> terceiros,
            Dictionary<string, bool> gruposCompletos,
            List<Jogo> jogosMataMata)
        {
            var gruposPorLetra = grupos
                .GroupBy(g => g.Nome)
                .ToDictionary(g => g.Key, g => g.First());

            // Todos os 12 grupos com as 3 rodadas concluídas → 3ºs colocados definitivos.
            bool terceirosFinais = terceiros.Count >= 12 &&
                gruposCompletos.Count >= 12 && gruposCompletos.Values.All(v => v);

            // Aloca os 8 melhores 3ºs aos slots respeitando a elegibilidade por grupo.
            var alocacaoTerceiros = AlocarTerceiros(terceiros);

            // Jogos importados agrupados por fase do chaveamento.
            var jogosPorFase = jogosMataMata
                .Where(j => j.TimeCasa != null && j.TimeVisitante != null)
                .GroupBy(j => NormalizarFase(j.Grupo))
                .Where(g => g.Key != null)
                .ToDictionary(g => g.Key!, g => g.OrderBy(j => j.Data).ToList());

            var jogosUsados = new HashSet<int>();
            // Resultado de cada partida já decidida: nº → (vencedor, perdedor).
            var resultado = new Dictionary<int, (Time? vencedor, Time? perdedor)>();

            var vm = new ChaveamentoCopaViewModel();

            foreach (var fase in Template)
            {
                var faseVm = new FaseCopaViewModel
                {
                    Chave = fase.Chave,
                    Nome = fase.Nome,
                    NomeCurto = fase.NomeCurto
                };

                jogosPorFase.TryGetValue(fase.Chave, out var jogosDaFase);

                var confrontos = fase.Confrontos.Select(spec =>
                {
                    var casa = ResolverSlot(spec.Numero, spec.Casa, gruposPorLetra, gruposCompletos,
                        alocacaoTerceiros, terceirosFinais, resultado);
                    var visit = ResolverSlot(spec.Numero, spec.Visitante, gruposPorLetra, gruposCompletos,
                        alocacaoTerceiros, terceirosFinais, resultado);
                    return new
                    {
                        spec,
                        casa,
                        visit,
                        vm = new ConfrontoCopaViewModel { Numero = spec.Numero, Casa = casa, Visitante = visit }
                    };
                }).ToList();

                // Passo 1: casa o jogo real quando os DOIS times batem com a previsão.
                foreach (var c in confrontos)
                {
                    var jogo = EncontrarJogoExato(jogosDaFase, jogosUsados, c.casa.Time, c.visit.Time);
                    if (jogo != null)
                        AplicarJogoReal(c.vm, jogo, c.casa, c.visit, jogosUsados, resultado);
                }

                // Passo 2: confrontos ainda sem jogo — ancora no time de grupo (1º/2º, sempre
                // confiável) e usa o adversário real, resolvendo o caso do "melhor 3º" que
                // diverge da previsão do template.
                foreach (var c in confrontos)
                {
                    if (c.vm.JogoId.HasValue) continue;
                    var jogo = EncontrarJogoPorAncora(jogosDaFase, jogosUsados, c.casa, c.visit);
                    if (jogo != null)
                        AplicarJogoReal(c.vm, jogo, c.casa, c.visit, jogosUsados, resultado);
                }

                foreach (var c in confrontos)
                    faseVm.Confrontos.Add(c.vm);

                vm.Fases.Add(faseVm);
            }

            return vm;
        }

        // ── Resolução de slots ────────────────────────────────────────────────

        private static SlotChaveamento ResolverSlot(
            int numero,
            string spec,
            Dictionary<string, GrupoViewModel> grupos,
            Dictionary<string, bool> gruposCompletos,
            Dictionary<int, Classificacao> alocacaoTerceiros,
            bool terceirosFinais,
            Dictionary<int, (Time? vencedor, Time? perdedor)> resultado)
        {
            var partes = spec.Split(':', 2);
            var tipo = partes[0];
            var arg = partes.Length > 1 ? partes[1] : "";

            switch (tipo)
            {
                case "W":
                case "RU":
                {
                    int pos = tipo == "W" ? 0 : 1;
                    string letra = arg;
                    string rotulo = (tipo == "W" ? "1º " : "2º ") + "Grupo " + letra;
                    if (grupos.TryGetValue(letra, out var g) && g.Times.Count > pos)
                    {
                        bool completo = gruposCompletos.TryGetValue(letra, out var c) && c;
                        return new SlotChaveamento
                        {
                            Time = g.Times[pos].Time,
                            Rotulo = rotulo,
                            Provisorio = !completo,
                            Ancora = true
                        };
                    }
                    return new SlotChaveamento { Rotulo = rotulo, Provisorio = true };
                }

                case "3":
                {
                    string rotulo = "Melhor 3º (" + arg.Replace(",", "/") + ")";
                    if (alocacaoTerceiros.TryGetValue(numero, out var terceiro))
                        return new SlotChaveamento
                        {
                            Time = terceiro.Time,
                            Rotulo = rotulo,
                            Provisorio = !terceirosFinais
                        };
                    return new SlotChaveamento { Rotulo = rotulo, Provisorio = true };
                }

                case "WIN":
                case "LOSE":
                {
                    int n = int.Parse(arg);
                    string rotulo = (tipo == "WIN" ? "Vencedor " : "Perdedor ") + n;
                    if (resultado.TryGetValue(n, out var r))
                    {
                        var time = tipo == "WIN" ? r.vencedor : r.perdedor;
                        if (time != null)
                            return new SlotChaveamento { Time = time, Rotulo = rotulo, Provisorio = false };
                    }
                    return new SlotChaveamento { Rotulo = rotulo, Provisorio = true };
                }
            }

            return new SlotChaveamento { Rotulo = spec, Provisorio = true };
        }

        // ── Alocação dos 8 melhores 3ºs colocados ─────────────────────────────

        // Slots de 3º colocado: nº da partida → grupos elegíveis (template oficial).
        private static readonly (int Numero, string[] Grupos)[] SlotsTerceiros =
        {
            (74, new[] { "A", "B", "C", "D", "F" }),
            (77, new[] { "C", "D", "F", "G", "H" }),
            (79, new[] { "C", "E", "F", "H", "I" }),
            (80, new[] { "E", "H", "I", "J", "K" }),
            (81, new[] { "B", "E", "F", "I", "J" }),
            (82, new[] { "A", "E", "H", "I", "J" }),
            (85, new[] { "E", "F", "G", "I", "J" }),
            (87, new[] { "D", "E", "I", "J", "L" }),
        };

        // Retorna nº da partida → 3º colocado alocado. Vazio se ainda não há 8 classificados.
        private static Dictionary<int, Classificacao> AlocarTerceiros(List<Classificacao> terceiros)
        {
            var resultado = new Dictionary<int, Classificacao>();

            var classificados = terceiros.Where(t => t.Posicao >= 1 && t.Posicao <= 8).ToList();
            if (classificados.Count < 8) return resultado;

            // Matching perfeito por backtracking respeitando a elegibilidade de cada slot.
            var atribuicao = new Classificacao?[SlotsTerceiros.Length];
            var usados = new bool[classificados.Count];

            bool Backtrack(int slotIdx)
            {
                if (slotIdx == SlotsTerceiros.Length) return true;
                var elegiveis = SlotsTerceiros[slotIdx].Grupos;
                for (int i = 0; i < classificados.Count; i++)
                {
                    if (usados[i]) continue;
                    if (!elegiveis.Contains(classificados[i].Grupo)) continue;
                    usados[i] = true;
                    atribuicao[slotIdx] = classificados[i];
                    if (Backtrack(slotIdx + 1)) return true;
                    usados[i] = false;
                    atribuicao[slotIdx] = null;
                }
                return false;
            }

            if (Backtrack(0))
                for (int i = 0; i < SlotsTerceiros.Length; i++)
                    resultado[SlotsTerceiros[i].Numero] = atribuicao[i]!;

            return resultado;
        }

        // ── Casamento de jogo importado com um confronto ──────────────────────

        // Jogo real cujos DOIS times batem com a previsão do confronto.
        private static Jogo? EncontrarJogoExato(
            List<Jogo>? jogosDaFase, HashSet<int> usados, Time? timeA, Time? timeB)
        {
            if (jogosDaFase == null || timeA == null || timeB == null) return null;
            return jogosDaFase.FirstOrDefault(j =>
                !usados.Contains(j.Id) &&
                ((j.TimeCasaId == timeA.Id && j.TimeVisitanteId == timeB.Id) ||
                 (j.TimeCasaId == timeB.Id && j.TimeVisitanteId == timeA.Id)));
        }

        // Jogo real que contém ao menos um time-âncora (1º/2º de grupo) do confronto.
        // Usado quando o adversário previsto (melhor 3º) não corresponde ao real.
        private static Jogo? EncontrarJogoPorAncora(
            List<Jogo>? jogosDaFase, HashSet<int> usados, SlotChaveamento casa, SlotChaveamento visit)
        {
            if (jogosDaFase == null) return null;
            var ancoras = new[] { casa, visit }
                .Where(s => s.Ancora && s.Time != null)
                .Select(s => s.Time!.Id)
                .ToHashSet();
            if (ancoras.Count == 0) return null;
            return jogosDaFase.FirstOrDefault(j =>
                !usados.Contains(j.Id) &&
                (ancoras.Contains(j.TimeCasaId) || ancoras.Contains(j.TimeVisitanteId)));
        }

        // Sobrescreve o confronto com os dados do jogo real, mantendo os rótulos
        // previstos alinhados aos times reais (inclusive em troca de mando/3º divergente).
        private static void AplicarJogoReal(
            ConfrontoCopaViewModel confronto, Jogo jogo,
            SlotChaveamento casa, SlotChaveamento visit,
            HashSet<int> usados,
            Dictionary<int, (Time? vencedor, Time? perdedor)> resultado)
        {
            usados.Add(jogo.Id);
            confronto.JogoId = jogo.Id;
            confronto.Data = jogo.Data;

            var previstos = new List<SlotChaveamento> { casa, visit };
            confronto.Casa = MontarSlotReal(jogo.TimeCasa, previstos);
            confronto.Visitante = MontarSlotReal(jogo.TimeVisitante, previstos);

            confronto.PlacarCasa = jogo.PlacarCasa;
            confronto.PlacarVisitante = jogo.PlacarVisitante;
            confronto.PenaltisCasa = jogo.PenaltisCasa;
            confronto.PenaltisVisitante = jogo.PenaltisVisitante;

            if (jogo.PlacarCasa.HasValue && jogo.PlacarVisitante.HasValue)
            {
                if (jogo.PlacarCasa > jogo.PlacarVisitante)
                    resultado[confronto.Numero] = (jogo.TimeCasa, jogo.TimeVisitante);
                else if (jogo.PlacarCasa < jogo.PlacarVisitante)
                    resultado[confronto.Numero] = (jogo.TimeVisitante, jogo.TimeCasa);
                // Empate no tempo normal/prorrogação → decide nos pênaltis.
                else if (jogo.PenaltisCasa.HasValue && jogo.PenaltisVisitante.HasValue)
                {
                    if (jogo.PenaltisCasa > jogo.PenaltisVisitante)
                        resultado[confronto.Numero] = (jogo.TimeCasa, jogo.TimeVisitante);
                    else if (jogo.PenaltisCasa < jogo.PenaltisVisitante)
                        resultado[confronto.Numero] = (jogo.TimeVisitante, jogo.TimeCasa);
                }
                // Sem pênaltis registrados num empate → vencedor ainda indefinido.
            }
        }

        // Monta o slot do time real, consumindo o rótulo previsto que corresponde a ele
        // (por time); se nenhum corresponder, usa o rótulo previsto restante.
        private static SlotChaveamento MontarSlotReal(Time? real, List<SlotChaveamento> previstosRestantes)
        {
            SlotChaveamento? escolhido = null;
            if (real != null)
                escolhido = previstosRestantes.FirstOrDefault(s => s.Time != null && s.Time.Id == real.Id);
            escolhido ??= previstosRestantes.FirstOrDefault();
            if (escolhido != null) previstosRestantes.Remove(escolhido);

            return new SlotChaveamento
            {
                Time = real,
                Rotulo = escolhido?.Rotulo ?? "",
                Provisorio = false
            };
        }

        // ── Identificação da fase de mata-mata a partir do campo Grupo/Round ──

        public static string? NormalizarFase(string? grupo)
        {
            if (string.IsNullOrWhiteSpace(grupo)) return null;
            var g = grupo.ToLowerInvariant();

            if (g.Contains("round of 32") || g.Contains("16-avos") || g.Contains("16 avos")
                || g.Contains("dezesseis") || g.Contains("1/16")) return "R32";
            if (g.Contains("round of 16") || g.Contains("oitavas") || g.Contains("1/8")) return "R16";
            if (g.Contains("quarter") || g.Contains("quartas") || g.Contains("1/4")) return "QF";
            // Disputa de 3º lugar deve ser testada antes de "final" genérico.
            bool ehTerceiro = g.Contains("3º lugar") || g.Contains("3o lugar")
                || g.Contains("third place") || g.Contains("3rd place")
                || g.Contains("disputa de terceiro") || g.Contains("play-off for third");
            if (ehTerceiro) return "TP";
            if (g.Contains("semi")) return "SF";
            if (g.Contains("final")) return "F";
            return null;
        }
    }
}
