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

            // Chaves lidas de Jogo.EstatisticasJson (nomes vindos da api-football).
            // Todas já são gravadas por ApiFootballService.MontarEstatisticasJson —
            // basta lê-las aqui. isPct só indica que o valor vem como "48%".
            var CHAVES = new (string Json, bool Pct)[]
            {
                ("Ball Possession", true),  ("Total Shots", false),     ("Shots on Goal", false),
                ("Shots off Goal", false),  ("Shots insidebox", false), ("Shots outsidebox", false),
                ("Blocked Shots", false),   ("Corner Kicks", false),    ("Total passes", false),
                ("Passes accurate", false), ("Passes %", true),         ("Fouls", false),
                ("Offsides", false),        ("Goalkeeper Saves", false),
                ("expected_goals", false),  ("goals_prevented", false)
            };

            // chave -> timeId -> (soma dos valores, nº de partidas que tinham o dado).
            var somas = new Dictionary<string, Dictionary<int, (double soma, int n)>>();

            // Gols marcados pelo time apenas nas partidas em que a chave veio
            // importada — denominador e numerador precisam cobrir os mesmos jogos
            // para "conversão" e "eficiência em GE" fazerem sentido.
            var golsComChave = new Dictionary<string, Dictionary<int, int>>();

            void Acumular(string chave, int timeId, double v, int golsNaPartida)
            {
                if (!somas.TryGetValue(chave, out var dic))
                    somas[chave] = dic = new Dictionary<int, (double, int)>();
                var cur = dic.GetValueOrDefault(timeId);
                dic[timeId] = (cur.soma + v, cur.n + 1);

                if (!golsComChave.TryGetValue(chave, out var g))
                    golsComChave[chave] = g = new Dictionary<int, int>();
                g[timeId] = g.GetValueOrDefault(timeId) + golsNaPartida;
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

                if (pc > pv) { vmCasa.V++; vmCasa.VCasa++; vmCasa.Pts += 3; vmCasa.PtsCasa += 3; vmVis.D++; }
                else if (pc < pv) { vmVis.V++; vmVis.Pts += 3; vmVis.PtsFora += 3; vmCasa.D++; }
                else { vmCasa.E++; vmCasa.ECasa++; vmCasa.Pts += 1; vmCasa.PtsCasa += 1; vmVis.E++; vmVis.Pts += 1; vmVis.PtsFora += 1; }

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

                            int internalId, golsNaPartida;
                            if (jogo.TimeCasa.IdApi == apiId) { internalId = jogo.TimeCasaId; golsNaPartida = pc; }
                            else if (jogo.TimeVisitante.IdApi == apiId) { internalId = jogo.TimeVisitanteId; golsNaPartida = pv; }
                            else continue;

                            if (!entry.TryGetProperty("Stats", out var stats)) continue;

                            foreach (var (chave, pct) in CHAVES)
                            {
                                var v = Ler(stats, chave, isPct: pct);
                                if (v.HasValue) Acumular(chave, internalId, v.Value, golsNaPartida);
                            }
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

            double? Media(string chave, int timeId) =>
                somas.TryGetValue(chave, out var dic) && dic.TryGetValue(timeId, out var v) && v.n > 0
                    ? Math.Round(v.soma / v.n, 1) : (double?)null;

            double? Total(string chave, int timeId) =>
                somas.TryGetValue(chave, out var dic) && dic.TryGetValue(timeId, out var v) && v.n > 0
                    ? v.soma : (double?)null;

            int GolsCom(string chave, int timeId) =>
                golsComChave.TryGetValue(chave, out var dic) ? dic.GetValueOrDefault(timeId) : 0;

            // Estatísticas de contagem que ganham versão "total" no painel. As de
            // percentual/razão ficam de fora de propósito (ver Totais no ViewModel).
            var CONTAGEM = new (string Json, string Nome)[]
            {
                ("Total Shots", "fin"),          ("Shots on Goal", "finGol"),
                ("Shots off Goal", "chutesFora"), ("Shots insidebox", "chutesArea"),
                ("Shots outsidebox", "chutesForaArea"), ("Blocked Shots", "chutesBloqueados"),
                ("Corner Kicks", "esc"),         ("Total passes", "passes"),
                ("Passes accurate", "passesCertos"), ("Fouls", "faltas"),
                ("Offsides", "impedimentos"),    ("Goalkeeper Saves", "defesasGoleiro"),
                ("expected_goals", "xg"),        ("goals_prevented", "golsEvitados")
            };

            foreach (var vm in mapa.Values)
            {
                vm.Posse = Media("Ball Possession", vm.TimeId);
                vm.Fin = Media("Total Shots", vm.TimeId);
                vm.FinGol = Media("Shots on Goal", vm.TimeId);
                vm.Esc = Media("Corner Kicks", vm.TimeId);
                vm.PassesCertos = Media("Passes accurate", vm.TimeId);
                vm.Xg = Media("expected_goals", vm.TimeId);

                vm.ChutesFora = Media("Shots off Goal", vm.TimeId);
                vm.ChutesArea = Media("Shots insidebox", vm.TimeId);
                vm.ChutesForaArea = Media("Shots outsidebox", vm.TimeId);
                vm.ChutesBloqueados = Media("Blocked Shots", vm.TimeId);
                vm.Passes = Media("Total passes", vm.TimeId);
                vm.PrecisaoPasses = Media("Passes %", vm.TimeId);
                vm.Faltas = Media("Fouls", vm.TimeId);
                vm.Impedimentos = Media("Offsides", vm.TimeId);
                vm.DefesasGoleiro = Media("Goalkeeper Saves", vm.TimeId);
                vm.GolsEvitados = Media("goals_prevented", vm.TimeId);

                vm.FinTotal = Total("Total Shots", vm.TimeId);
                vm.GolsComFin = GolsCom("Total Shots", vm.TimeId);
                vm.XgTotal = Total("expected_goals", vm.TimeId);
                vm.GolsComXg = GolsCom("expected_goals", vm.TimeId);

                foreach (var (json, nome) in CONTAGEM)
                {
                    var t = Total(json, vm.TimeId);
                    if (t.HasValue) vm.Totais[nome] = Math.Round(t.Value, 1);
                }
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
