using System.Globalization;
using System.Text.Json;
using ControleFutebolWeb.Models.Nba;

namespace ControleFutebolWeb.Services
{
    // Página do jogador (/NBA/Jogador/{id}). Cada aba usa o seu endpoint de
    // common/v3/.../athletes/{id}, e só o da aba aberta é buscado: a página inteira
    // de uma vez seriam 5 chamadas (~6 s com o intervalo mínimo entre elas).
    public partial class EspnNbaService
    {
        private static string Atleta(string id) =>
            $"common/v3/sports/basketball/nba/athletes/{Uri.EscapeDataString(id)}";

        // ── Perfil (cabeçalho, presente em todas as abas) ─────────────────────

        public Task<NbaPerfilJogador?> ObterPerfilJogadorAsync(string id, CancellationToken ct = default) =>
            ComCacheAsync($"nba:jogador:{id}",
                $"{Atleta(id)}?{Idioma}",
                _ => CacheTabela,
                raiz =>
                {
                    var a = Prop(raiz, "athlete");
                    var time = Prop(a, "team");
                    var resumo = Prop(a, "statsSummary");
                    return new NbaPerfilJogador(
                        Texto(a, "id") ?? id,
                        Texto(a, "displayName") ?? "",
                        Texto(a, "jersey"),
                        Posicao(Prop(a, "position")),
                        time.ValueKind == JsonValueKind.Object && Texto(time, "id") != null ? LerTime(time) : null,
                        Href(Prop(a, "headshot")),
                        Texto(a, "displayHeight"),
                        Texto(a, "displayWeight"),
                        Inteiro(a, "age"),
                        Texto(a, "displayDOB"),
                        Texto(a, "displayBirthPlace"),
                        Texto(a, "displayDraft"),
                        Texto(a, "displayExperience"),
                        Bool(a, "active"),
                        Texto(resumo, "displayName"),
                        Lista(resumo, "statistics").Select(s => new NbaEstatResumo(
                            Texto(s, "abbreviation") ?? "",
                            Texto(s, "displayName") ?? "",
                            Texto(s, "displayValue") ?? "",
                            Texto(s, "rankDisplayValue"))).ToList());
                },
                ct);

        // A ESPN em pt traduz "Forward" como "Atacante"; no basquete é ala.
        private static string? Posicao(JsonElement pos) => Texto(pos, "name") switch
        {
            "Guard" => "Armador",
            "Point Guard" => "Armador",
            "Shooting Guard" => "Ala-armador",
            "Forward" => "Ala",
            "Small Forward" => "Ala",
            "Power Forward" => "Ala-pivô",
            "Center" => "Pivô",
            "Guard-Forward" => "Ala-armador",
            "Forward-Center" => "Ala-pivô",
            _ => Texto(pos, "displayName"),
        };

        // ── Visão geral ───────────────────────────────────────────────────────

        public async Task<NbaVisaoGeralJogador?> ObterVisaoGeralJogadorAsync(string id, CancellationToken ct = default)
        {
            var geral = await ComCacheAsync($"nba:jogador:geral:{id}",
                $"{Atleta(id)}/overview?{Idioma}",
                _ => CacheDia,
                raiz =>
                {
                    var est = Prop(raiz, "statistics");
                    var medias = Lista(est, "splits")
                        .Select(s => new NbaLinhaRotulada(Texto(s, "displayName") ?? "", Valores(s, "stats")))
                        .ToList();

                    var log = Prop(raiz, "gameLog");
                    var bloco = Lista(log, "statistics").FirstOrDefault();
                    var eventos = Prop(log, "events");
                    var ultimos = Lista(bloco, "events")
                        .Select(e => LerJogoAtleta(Texto(e, "eventId") ?? "", Prop(eventos, Texto(e, "eventId") ?? ""), Valores(e, "stats")))
                        .OrderByDescending(j => j.DataUtc)
                        .ToList();

                    return new VisaoBruta(Valores(est, "labels"), medias, Valores(bloco, "labels"), ultimos);
                },
                ct);
            if (geral == null) return null;

            // Prêmios e times por onde passou moram no /bio, que muda quase nunca.
            var bio = await ComCacheAsync($"nba:jogador:bio:{id}",
                $"{Atleta(id)}/bio?{Idioma}",
                _ => CacheElenco,
                raiz => new BioBruta(
                    Lista(raiz, "awards").Select(p => new NbaPremio(
                        Texto(p, "name") ?? "",
                        Texto(p, "displayCount") ?? "",
                        Valores(p, "seasons"))).ToList(),
                    Lista(raiz, "teamHistory").Select(t => new NbaPassagem(
                        Texto(t, "id"),
                        Texto(t, "displayName") ?? "",
                        Texto(t, "logo"),
                        (Texto(t, "seasons") ?? "").Replace("ATUAL", "atual"))).ToList()),
                ct);

            return new NbaVisaoGeralJogador(
                geral.Colunas.Select(RotuloColuna).ToList(),
                geral.Medias.Select(m => m with { Rotulo = RotuloRecorte(m.Rotulo) }).ToList(),
                geral.ColunasUltimos.Select(RotuloColuna).ToList(),
                geral.Ultimos,
                bio?.Premios ?? Array.Empty<NbaPremio>(),
                bio?.Times ?? Array.Empty<NbaPassagem>());
        }

        // Classes (e não tuplas) porque o T? de ComCacheAsync só vira nulável com tipo de referência.
        private record VisaoBruta(IReadOnlyList<string> Colunas, IReadOnlyList<NbaLinhaRotulada> Medias,
            IReadOnlyList<string> ColunasUltimos, IReadOnlyList<NbaJogoAtleta> Ultimos);
        private record BioBruta(IReadOnlyList<NbaPremio> Premios, IReadOnlyList<NbaPassagem> Times);

        // ── Jogos (game log) ──────────────────────────────────────────────────

        public Task<NbaGameLog?> ObterGameLogAsync(string id, int temporada, CancellationToken ct = default) =>
            ComCacheAsync($"nba:jogador:log:{id}:{temporada}",
                $"{Atleta(id)}/gamelog?season={temporada}&{Idioma}",
                _ => CacheDia,
                raiz =>
                {
                    var eventos = Prop(raiz, "events");
                    var meses = new List<NbaMesJogos>();
                    var resumoTemporada = new List<NbaLinhaRotulada>();
                    var resumoPlayoffs = new List<NbaLinhaRotulada>();

                    foreach (var tipo in Lista(raiz, "seasonTypes"))
                    {
                        var nome = Texto(tipo, "displayName") ?? "";
                        // Pré-temporada não conta, como na ESPN. Os playoffs vêm sem
                        // jogo a jogo (as categorias chegam vazias): só o resumo serve.
                        if (nome.StartsWith("Pré", StringComparison.OrdinalIgnoreCase)) continue;
                        var playoffs = nome.StartsWith("Pós", StringComparison.OrdinalIgnoreCase);

                        var resumo = Lista(Prop(tipo, "summary"), "stats")
                            .Select(s => new NbaLinhaRotulada(Texto(s, "displayName") ?? "", Valores(s, "stats")))
                            .ToList();
                        (playoffs ? resumoPlayoffs : resumoTemporada).AddRange(resumo);
                        if (playoffs) continue;

                        foreach (var cat in Lista(tipo, "categories"))
                        {
                            var jogos = Lista(cat, "events")
                                .Select(e => LerJogoAtleta(Texto(e, "eventId") ?? "", Prop(eventos, Texto(e, "eventId") ?? ""), Valores(e, "stats")))
                                .OrderByDescending(j => j.DataUtc)
                                .ToList();
                            if (jogos.Count > 0)
                                meses.Add(new NbaMesJogos(Mes(Texto(cat, "displayName")), jogos, Valores(cat, "totals")));
                        }
                    }

                    return new NbaGameLog(
                        RotuloTemporada(temporada),
                        Valores(raiz, "labels").Select(RotuloColuna).ToList(),
                        meses,
                        resumoTemporada,
                        resumoPlayoffs,
                        OpcoesTemporada(raiz));
                },
                ct);

        /// <summary>
        /// Jogo do game log. O mesmo evento vem em dois formatos: no /overview o
        /// adversário é um objeto com logo; no /gamelog é só a sigla ("UTAH") e o
        /// time do jogador também vira sigla.
        /// </summary>
        private static NbaJogoAtleta LerJogoAtleta(string eventoId, JsonElement e, IReadOnlyList<string> valores)
        {
            var emCasa = Texto(e, "atVs") is "vs" or "x";
            var adv = Prop(e, "opponent");
            string? advId;
            string sigla;
            string? logo;
            if (adv.ValueKind == JsonValueKind.Object)
            {
                advId = Texto(adv, "id");
                sigla = Texto(adv, "abbreviation") ?? "";
                logo = Texto(adv, "logo");
            }
            else
            {
                advId = emCasa ? Texto(e, "awayTeamId") : Texto(e, "homeTeamId");
                sigla = adv.ValueKind == JsonValueKind.String ? adv.GetString() ?? "" : "";
                logo = sigla == "" ? null : $"https://a.espncdn.com/i/teamlogos/nba/500/{sigla.ToLowerInvariant()}.png";
            }

            var data = DateTime.TryParse(Texto(e, "gameDate"), CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var d) ? d : DateTime.MinValue;

            // "score" vem "vencedor-perdedor"; o placar mostrado é sempre do jogador primeiro.
            string? placar = null;
            if (Inteiro(e, "homeTeamScore") is int casa && Inteiro(e, "awayTeamScore") is int fora)
                placar = emCasa ? $"{casa}-{fora}" : $"{fora}-{casa}";

            return new NbaJogoAtleta(eventoId, data, emCasa, advId, sigla, logo,
                Texto(e, "gameResult") switch { "W" => "V", "L" => "D", var r => r },
                placar, valores);
        }

        private static string Mes(string? nome) => nome?.ToLowerInvariant() switch
        {
            "january" => "Janeiro", "february" => "Fevereiro", "march" => "Março",
            "april" => "Abril", "may" => "Maio", "june" => "Junho", "july" => "Julho",
            "august" => "Agosto", "september" => "Setembro", "october" => "Outubro",
            "november" => "Novembro", "december" => "Dezembro",
            _ => nome ?? "",
        };

        // ── Carreira ──────────────────────────────────────────────────────────

        /// <summary>playoffs=false: temporada regular; true: pós-temporada.</summary>
        public Task<IReadOnlyList<NbaTabelaCarreira>?> ObterCarreiraAsync(string id, bool playoffs, CancellationToken ct = default) =>
            ComCacheAsync($"nba:jogador:carreira:{id}:{(playoffs ? 3 : 2)}",
                $"{Atleta(id)}/stats?seasontype={(playoffs ? 3 : 2)}&{Idioma}",
                _ => CacheTabela,
                raiz =>
                {
                    // Times da carreira indexados pelo slug, que é o que cada linha traz.
                    var times = new Dictionary<string, NbaTimeResumo>();
                    if (Prop(raiz, "teams") is { ValueKind: JsonValueKind.Object } ts)
                        foreach (var t in ts.EnumerateObject())
                            times[t.Name] = LerTime(t.Value);

                    return (IReadOnlyList<NbaTabelaCarreira>)Lista(raiz, "categories").Select(c =>
                    {
                        var chave = Texto(c, "name") ?? "";
                        var linhas = Lista(c, "statistics").Select(l => new NbaLinhaCarreira(
                                Texto(Prop(l, "season"), "displayName") ?? "",
                                times.GetValueOrDefault(Texto(l, "teamSlug") ?? ""),
                                Valores(l, "stats")))
                            // Mais recente primeiro, como as outras tabelas da página.
                            .Reverse()
                            .ToList();
                        return new NbaTabelaCarreira(
                            chave,
                            chave switch { "averages" => "Médias", "totals" => "Totais", "miscellaneous" => "Diversos", _ => Texto(c, "displayName") ?? chave },
                            Valores(c, "labels").Select(RotuloColuna).ToList(),
                            linhas,
                            Valores(c, "totals"));
                    }).Where(t => t.Linhas.Count > 0).ToList();
                },
                ct);

        // ── Recortes (splits) ─────────────────────────────────────────────────

        public Task<NbaRecortes?> ObterRecortesAsync(string id, int temporada, CancellationToken ct = default) =>
            ComCacheAsync($"nba:jogador:recortes:{id}:{temporada}",
                $"{Atleta(id)}/splits?season={temporada}&{Idioma}",
                _ => CacheDia,
                raiz => new NbaRecortes(
                    RotuloTemporada(temporada),
                    Valores(raiz, "labels").Select(RotuloColuna).ToList(),
                    Lista(raiz, "splitCategories").Select(g => new NbaGrupoRecorte(
                        RotuloGrupoRecorte(Texto(g, "displayName")),
                        Lista(g, "splits")
                            .Select(s => new NbaLinhaRotulada(RotuloRecorte(Texto(s, "displayName") ?? ""), Valores(s, "stats")))
                            .ToList()))
                        .Where(g => g.Linhas.Count > 0)
                        .ToList(),
                    OpcoesTemporada(raiz)),
                ct);

        // Rótulos que a ESPN deixa em inglês (ou traduz mal) mesmo com lang=pt.
        private static string RotuloRecorte(string r) => r switch
        {
            "All Splits" => "Geral",
            "Home" => "Em casa",
            "Regular Season" => "Temporada regular",
            "Career" => "Carreira",
            _ => r,
        };

        private static string RotuloGrupoRecorte(string? g) => g switch
        {
            "split" => "Geral",
            null => "",
            _ => g,
        };

        // Com lang=pt a ESPN rotula assistências como "V" no game log e nos totais
        // (tradução automática de "A"); "OR/DR" são os rebotes ofensivo/defensivo e
        // "GS" os jogos como titular.
        private static string RotuloColuna(string c) => c switch
        {
            "V" => "AST",
            "OR" => "RO",
            "DR" => "RD",
            "GS" => "JT",
            "J" => "J",
            _ => c,
        };

        private static IReadOnlyList<NbaOpcaoTemporada> OpcoesTemporada(JsonElement raiz) =>
            Lista(raiz, "filters")
                .Where(f => Texto(f, "name") == "season")
                .SelectMany(f => Lista(f, "options"))
                .Select(o => int.TryParse(Texto(o, "value"), out var ano) ? new NbaOpcaoTemporada(ano, Texto(o, "displayValue") ?? RotuloTemporada(ano)) : null)
                .Where(o => o != null).Select(o => o!)
                .ToList();

        private static IReadOnlyList<string> Valores(JsonElement el, string nome) =>
            Lista(el, nome).Select(v => v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : v.GetRawText()).ToList();
    }
}
