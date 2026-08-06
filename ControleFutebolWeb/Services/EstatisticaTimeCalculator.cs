using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.ViewModels;
using System.Text.Json;

namespace ControleFutebolWeb.Services
{
    // Agrega, por time, os dados usados na aba "Estatísticas" das telas de
    // competição (Tabela do Brasileirão, Competicoes/Detalhes, Libertadores,
    // Copa do Brasil): resultado casa×fora, posse/finalizações/escanteios/passes/xG
    // (vindos de Jogo.EstatisticasJson, importado da api-football), cartões e gols
    // por intervalo de 15min (com o gol contra atribuído a quem se beneficiou dele).
    // Extraído de TabelaBrasileiraoController para ser reaproveitado por qualquer
    // tela que já tenha a lista de jogos realizados da competição/temporada.
    public static class EstatisticaTimeCalculator
    {
        public static List<EstatisticaTimeViewModel> Calcular(
            List<Jogo> jogos, List<Gol> gols, List<Cartao> cartoes)
        {
            int IndiceBucket(int minuto) => minuto switch
            {
                <= 15 => 0,
                <= 30 => 1,
                <= 45 => 2,
                <= 60 => 3,
                <= 75 => 4,
                <= 90 => 5,
                _ => 6
            };

            int? TimeDoAutor(Jogo j, Jogador? autor)
            {
                if (autor == null) return null;
                if (autor.TimeId == j.TimeCasaId || autor.SelecaoId == j.TimeCasaId) return j.TimeCasaId;
                if (autor.TimeId == j.TimeVisitanteId || autor.SelecaoId == j.TimeVisitanteId) return j.TimeVisitanteId;
                return null;
            }

            var mapa = new Dictionary<int, EstatisticaTimeViewModel>();
            EstatisticaTimeViewModel Get(Time? t)
            {
                if (t == null) return new EstatisticaTimeViewModel();
                if (!mapa.TryGetValue(t.Id, out var vm))
                {
                    vm = new EstatisticaTimeViewModel { TimeId = t.Id, Nome = t.Nome ?? "—", Sigla = SiglaDe(t.Nome), EscudoUrl = t.EscudoUrl };
                    mapa[t.Id] = vm;
                }
                return vm;
            }

            // Acumuladores das médias por jogo vindas do JSON (soma + quantas partidas tinham o dado).
            var somaPosse = new Dictionary<int, (double soma, int n)>();
            var somaFin = new Dictionary<int, (double soma, int n)>();
            var somaFinGol = new Dictionary<int, (double soma, int n)>();
            var somaEsc = new Dictionary<int, (double soma, int n)>();
            var somaPasses = new Dictionary<int, (double soma, int n)>();
            var somaXg = new Dictionary<int, (double soma, int n)>();

            void Acumular(Dictionary<int, (double soma, int n)> dic, int timeId, double v)
            {
                var cur = dic.GetValueOrDefault(timeId);
                dic[timeId] = (cur.soma + v, cur.n + 1);
            }

            double? Ler(JsonElement stats, string chave, bool isPct)
            {
                if (!stats.TryGetProperty(chave, out var el)) return null;
                if (el.ValueKind == JsonValueKind.Null) return null;
                var raw = el.GetString();
                if (string.IsNullOrEmpty(raw)) return null;
                raw = isPct ? raw.Replace("%", "").Trim() : raw.Trim();
                return double.TryParse(raw, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : (double?)null;
            }

            foreach (var jogo in jogos)
            {
                if (jogo.TimeCasa == null || jogo.TimeVisitante == null) continue;
                var pc = jogo.PlacarCasa ?? 0;
                var pv = jogo.PlacarVisitante ?? 0;

                var vmCasa = Get(jogo.TimeCasa);
                var vmVis = Get(jogo.TimeVisitante);

                vmCasa.J++; vmCasa.JCasa++; vmCasa.Gp += pc; vmCasa.Gc += pv; vmCasa.GpCasa += pc; vmCasa.GcCasa += pv;
                vmVis.J++; vmVis.JFora++; vmVis.Gp += pv; vmVis.Gc += pc; vmVis.GpFora += pv; vmVis.GcFora += pc;

                if (pc > pv) { vmCasa.V++; vmCasa.Pts += 3; vmCasa.PtsCasa += 3; vmVis.D++; }
                else if (pc < pv) { vmVis.V++; vmVis.Pts += 3; vmVis.PtsFora += 3; vmCasa.D++; }
                else { vmCasa.E++; vmCasa.Pts += 1; vmCasa.PtsCasa += 1; vmVis.E++; vmVis.Pts += 1; vmVis.PtsFora += 1; }

                if (pv == 0) vmCasa.CleanSheets++;
                if (pc == 0) vmVis.CleanSheets++;

                if (!string.IsNullOrEmpty(jogo.EstatisticasJson))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(jogo.EstatisticasJson);
                        foreach (var entry in doc.RootElement.EnumerateArray())
                        {
                            if (!entry.TryGetProperty("TimeId", out var tidEl)) continue;
                            int apiId = tidEl.GetInt32();

                            int internalId;
                            if (jogo.TimeCasa.IdApi == apiId) internalId = jogo.TimeCasaId;
                            else if (jogo.TimeVisitante.IdApi == apiId) internalId = jogo.TimeVisitanteId;
                            else continue;

                            if (!entry.TryGetProperty("Stats", out var stats)) continue;

                            var posse = Ler(stats, "Ball Possession", isPct: true);
                            if (posse.HasValue) Acumular(somaPosse, internalId, posse.Value);
                            var fin = Ler(stats, "Total Shots", isPct: false);
                            if (fin.HasValue) Acumular(somaFin, internalId, fin.Value);
                            var finGol = Ler(stats, "Shots on Goal", isPct: false);
                            if (finGol.HasValue) Acumular(somaFinGol, internalId, finGol.Value);
                            var esc = Ler(stats, "Corner Kicks", isPct: false);
                            if (esc.HasValue) Acumular(somaEsc, internalId, esc.Value);
                            var passes = Ler(stats, "Passes accurate", isPct: false);
                            if (passes.HasValue) Acumular(somaPasses, internalId, passes.Value);
                            var xg = Ler(stats, "expected_goals", isPct: false);
                            if (xg.HasValue) Acumular(somaXg, internalId, xg.Value);
                        }
                    }
                    catch { /* ignora JSON malformado */ }
                }
            }

            var jogoPorId = jogos.ToDictionary(j => j.Id);

            foreach (var g in gols)
            {
                // Gol contra: quem se beneficia é o adversário do time do autor — sem o
                // time do autor não dá pra resolver o beneficiado com segurança, então ignora.
                if (g.Contra) continue;
                if (!jogoPorId.TryGetValue(g.JogoId, out var jogo)) continue;
                var timeId = TimeDoAutor(jogo, g.Jogador);
                if (timeId == null || !mapa.TryGetValue(timeId.Value, out var vm)) continue;
                vm.GolsIntervalo[IndiceBucket(g.Minuto)]++;
            }

            foreach (var c in cartoes)
            {
                if (!jogoPorId.TryGetValue(c.JogoId, out var jogo)) continue;
                var timeId = TimeDoAutor(jogo, c.Jogador);
                if (timeId == null || !mapa.TryGetValue(timeId.Value, out var vm)) continue;
                if (c.Tipo != null && c.Tipo.StartsWith("Verm", StringComparison.OrdinalIgnoreCase)) vm.Vermelhos++;
                else vm.Amarelos++;
            }

            double? Media(Dictionary<int, (double soma, int n)> dic, int timeId) =>
                dic.TryGetValue(timeId, out var v) && v.n > 0 ? Math.Round(v.soma / v.n, 1) : (double?)null;

            foreach (var vm in mapa.Values)
            {
                vm.Posse = Media(somaPosse, vm.TimeId);
                vm.Fin = Media(somaFin, vm.TimeId);
                vm.FinGol = Media(somaFinGol, vm.TimeId);
                vm.Esc = Media(somaEsc, vm.TimeId);
                vm.PassesCertos = Media(somaPasses, vm.TimeId);
                vm.Xg = Media(somaXg, vm.TimeId);
            }

            return mapa.Values.OrderByDescending(v => v.Pts).ThenByDescending(v => v.Sg).ToList();
        }

        public static string SiglaDe(string? nome)
        {
            if (string.IsNullOrWhiteSpace(nome)) return "—";
            var palavras = nome.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (palavras.Length >= 2)
                return (palavras[0].Substring(0, Math.Min(1, palavras[0].Length)) +
                        palavras[1].Substring(0, Math.Min(2, palavras[1].Length))).ToUpperInvariant();
            return nome.Substring(0, Math.Min(3, nome.Length)).ToUpperInvariant();
        }
    }
}
