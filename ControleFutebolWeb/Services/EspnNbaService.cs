using System.Globalization;
using System.Net;
using System.Text.Json;
using ControleFutebolWeb.Models.Nba;
using Microsoft.Extensions.Caching.Memory;

namespace ControleFutebolWeb.Services
{
    /// <summary>
    /// Dados da NBA para a página /NBA, lidos da mesma API JSON pública da ESPN que o
    /// <see cref="EspnEstatisticasService"/> usa para o futebol (só troca o esporte no
    /// caminho). Nada é gravado no banco: cada tela monta o modelo direto da resposta,
    /// que fica em cache por um tempo que depende de quão rápido o dado muda.
    ///
    /// Mesma disciplina do serviço de futebol: uma chamada por vez, intervalo mínimo
    /// entre elas, espera em 429/503 e null (nunca exceção) em qualquer falha — a tela
    /// mostra "não foi possível carregar" em vez de quebrar.
    /// </summary>
    public partial class EspnNbaService
    {
        private const string Base = "https://site.web.api.espn.com/apis/";
        private const string Idioma = "lang=pt&region=br";

        // Jogo em andamento muda a cada posse; classificação e estatísticas, uma vez
        // por rodada. Elenco quase nunca.
        private static readonly TimeSpan CacheAoVivo = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan CacheDia = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan CacheTabela = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan CacheElenco = TimeSpan.FromHours(6);

        private static readonly SemaphoreSlim _porta = new(1, 1);
        private static DateTimeOffset _ultimaChamada = DateTimeOffset.MinValue;
        private static readonly TimeSpan IntervaloMinimo = TimeSpan.FromMilliseconds(1200);

        private readonly HttpClient _http;
        private readonly ILogger<EspnNbaService> _logger;
        private readonly IMemoryCache _cache;

        public EspnNbaService(HttpClient http, ILogger<EspnNbaService> logger, IMemoryCache cache)
        {
            _http = http;
            _http.BaseAddress = new Uri(Base);
            _http.Timeout = TimeSpan.FromSeconds(20);
            _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
            _http.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
            _http.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "pt-BR,pt;q=0.9");
            _logger = logger;
            _cache = cache;
        }

        /// <summary>
        /// Ano da temporada como a ESPN numera: a 2026-27 é "2027". A temporada vira
        /// em outubro, quando começa a fase regular.
        /// </summary>
        public static int TemporadaAtual(DateTime hoje) => hoje.Month >= 10 ? hoje.Year + 1 : hoje.Year;

        public static string RotuloTemporada(int ano) => $"{ano - 1}-{ano % 100:00}";

        // ── Jogos do dia ──────────────────────────────────────────────────────

        /// <summary>
        /// Jogos de uma data. A data é a da rodada nos EUA (como a ESPN agrupa): o jogo
        /// das 22h30 de Nova York já é madrugada aqui, mas continua sendo "da rodada".
        /// </summary>
        public Task<IReadOnlyList<NbaJogo>?> ObterJogosAsync(DateTime dia, CancellationToken ct = default) =>
            ComCacheAsync($"nba:jogos:{dia:yyyyMMdd}",
                $"site/v2/sports/basketball/nba/scoreboard?dates={dia:yyyyMMdd}&{Idioma}",
                jogos => jogos.Any(j => j.Situacao == NbaSituacao.AoVivo) ? CacheAoVivo : CacheDia,
                raiz => (IReadOnlyList<NbaJogo>)Lista(raiz, "events").Select(LerJogo).ToList(),
                ct);

        // ── Classificação ─────────────────────────────────────────────────────

        /// <summary>Nível: "conferencia" (padrão), "divisao" ou "liga".</summary>
        public Task<NbaClassificacao?> ObterClassificacaoAsync(string nivel, int temporada, CancellationToken ct = default)
        {
            var level = nivel switch { "liga" => 1, "divisao" => 3, _ => 2 };
            return ComCacheAsync($"nba:classif:{level}:{temporada}",
                $"v2/sports/basketball/nba/standings?level={level}&season={temporada}&{Idioma}",
                _ => CacheTabela,
                raiz => LerClassificacao(raiz, temporada, porSeed: level == 2),
                ct);
        }

        private static NbaClassificacao LerClassificacao(JsonElement raiz, int temporada, bool porSeed)
        {
            var grupos = new List<NbaGrupoClassificacao>();

            // Raiz (liga) → conferências → divisões (só no nível 3). Quem tem
            // "standings" é um grupo; a conferência de uma divisão é o nó de cima.
            void Visitar(JsonElement no, string? conferencia, int profundidade)
            {
                if (no.TryGetProperty("standings", out var st) && st.TryGetProperty("entries", out _))
                {
                    // A ESPN não entrega ordenado. Na conferência o seed é a posição
                    // oficial (desempates já aplicados); na liga e na divisão o seed
                    // continua sendo o da conferência, então ordena pela campanha.
                    var linhas = Lista(st, "entries").Select(LerLinhaClassificacao).ToList();
                    linhas = porSeed
                        ? linhas.OrderBy(l => l.Seed == 0 ? int.MaxValue : l.Seed).ThenByDescending(l => l.Vitorias).ToList()
                        : linhas.OrderByDescending(l => l.Vitorias + l.Derrotas == 0 ? 0 : (double)l.Vitorias / (l.Vitorias + l.Derrotas))
                                .ThenByDescending(l => l.Vitorias).ThenBy(l => l.Seed).ToList();
                    grupos.Add(new NbaGrupoClassificacao(Texto(no, "name") ?? "", conferencia, linhas));
                }
                foreach (var filho in Lista(no, "children"))
                    Visitar(filho, profundidade == 0 ? null : Texto(no, "name"), profundidade + 1);
            }

            Visitar(raiz, null, 0);
            return new NbaClassificacao(RotuloTemporada(temporada), grupos);
        }

        private static NbaLinhaClassificacao LerLinhaClassificacao(JsonElement e)
        {
            // As estatísticas vêm numa lista de {name, displayValue}; os registros de
            // casa/fora/divisão usam "name" com espaço e acento ("x Div.", "visitante"),
            // por isso a busca também olha o "type".
            var stats = Lista(e, "stats").ToList();
            string S(params string[] nomes)
            {
                foreach (var s in stats)
                {
                    var nome = Texto(s, "name");
                    var tipo = Texto(s, "type");
                    if (nomes.Any(n => string.Equals(n, nome, StringComparison.OrdinalIgnoreCase)
                                    || string.Equals(n, tipo, StringComparison.OrdinalIgnoreCase)))
                        return Texto(s, "displayValue") ?? "";
                }
                return "";
            }
            int N(string nome) => int.TryParse(S(nome), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;

            var clincher = S("clincher");
            return new NbaLinhaClassificacao(
                LerTime(e.GetProperty("team")),
                N("playoffSeed"),
                N("wins"),
                N("losses"),
                S("winPercent"),
                S("gamesBehind"),
                S("home", "Casa"),
                S("road", "away", "visitante"),
                S("vsdiv", "x Div."),
                S("vsconf", "X Conf"),
                S("avgPointsFor"),
                S("avgPointsAgainst"),
                S("differential"),
                Sequencia(S("streak")),
                S("lasttengames", "Últimos 10 jogos"),
                string.IsNullOrWhiteSpace(clincher) || clincher == "-" ? null : clincher);
        }

        // A ESPN em pt devolve "V3"/"D2"; em en, "W3"/"L2". Normaliza para o português.
        private static string Sequencia(string s) =>
            s.StartsWith('W') ? "V" + s[1..] : s.StartsWith('L') ? "D" + s[1..] : s;

        // ── Estatísticas ──────────────────────────────────────────────────────

        // Categorias de líderes que a tela mostra, com o rótulo em português. A ESPN
        // manda 16; as de "rating" e faltas ficam de fora, como na página deles.
        private static readonly (string Chave, string Nome, string Sigla)[] CategoriasLideres =
        {
            ("pointsPerGame", "Pontos", "PPJ"),
            ("reboundsPerGame", "Rebotes", "RPJ"),
            ("assistsPerGame", "Assistências", "APJ"),
            ("stealsPerGame", "Roubos de bola", "RBJ"),
            ("blocksPerGame", "Tocos", "TPJ"),
            ("3PointsMadePerGame", "Bolas de 3", "3PJ"),
            ("fieldGoalPercentage", "Aproveitamento de quadra", "FG%"),
            ("3PointPct", "Aproveitamento de 3", "3P%"),
            ("FreeThrowPct", "Lances livres", "LL%"),
            ("minutesPerGame", "Minutos", "MPJ"),
            ("doubleDouble", "Duplos-duplos", "DD"),
            ("PER", "Eficiência (PER)", "PER"),
        };

        public Task<NbaLideres?> ObterLideresAsync(int temporada, CancellationToken ct = default) =>
            ComCacheAsync($"nba:lideres:{temporada}",
                $"site/v3/sports/basketball/nba/leaders?season={temporada}&seasontype=2&limit=5&{Idioma}",
                _ => CacheTabela,
                raiz =>
                {
                    var porChave = Lista(Prop(raiz, "leaders"), "categories")
                        .Where(c => Texto(c, "name") != null)
                        .GroupBy(c => Texto(c, "name")!)
                        .ToDictionary(g => g.Key, g => g.First());

                    var categorias = new List<NbaLiderCategoria>();
                    foreach (var (chave, nome, sigla) in CategoriasLideres)
                    {
                        if (!porChave.TryGetValue(chave, out var c)) continue;
                        var top = Lista(c, "leaders").Take(5).Select(l =>
                        {
                            var a = Prop(l, "athlete");
                            return new NbaLiderLinha(
                                Texto(a, "id") ?? "",
                                Texto(a, "displayName") ?? "",
                                Href(Prop(a, "headshot")),
                                Prop(l, "team") is { ValueKind: JsonValueKind.Object } t ? LerTime(t) : null,
                                Texto(l, "displayValue") ?? "");
                        }).ToList();
                        if (top.Count > 0) categorias.Add(new NbaLiderCategoria(chave, nome, sigla, top));
                    }
                    return new NbaLideres(Texto(Prop(raiz, "requestedSeason"), "displayName") ?? RotuloTemporada(temporada), categorias);
                },
                ct);

        /// <summary>
        /// Ordenações aceitas pela tabela de jogadores: chave da tela → campo da ESPN.
        /// Lista fechada para a query string não virar parâmetro livre para a API.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> OrdensJogadores = new Dictionary<string, string>
        {
            ["pts"] = "offensive.avgPoints",
            ["reb"] = "general.avgRebounds",
            ["ast"] = "offensive.avgAssists",
            ["stl"] = "defensive.avgSteals",
            ["blk"] = "defensive.avgBlocks",
            ["fg"] = "offensive.fieldGoalPct",
            ["3p"] = "offensive.threePointFieldGoalPct",
            ["ft"] = "offensive.freeThrowPct",
            ["3pm"] = "offensive.avgThreePointFieldGoalsMade",
            ["min"] = "general.avgMinutes",
            ["to"] = "offensive.avgTurnovers",
        };

        public const int JogadoresPorPagina = 50;

        /// <summary>
        /// Tabela de jogadores. Sem time, é a paginação da própria ESPN. Com time, a
        /// ESPN não filtra (team/teamId são ignorados no byathlete): busca a liga
        /// inteira numa chamada só, filtra e ordena aqui — ver <see cref="TodosJogadoresAsync"/>.
        /// </summary>
        public async Task<NbaTabelaJogadores?> ObterJogadoresAsync(
            int temporada, string ordem, int pagina, string? timeId = null, CancellationToken ct = default)
        {
            var campo = OrdensJogadores.GetValueOrDefault(ordem, OrdensJogadores["pts"]);

            if (!string.IsNullOrEmpty(timeId))
            {
                var todos = await TodosJogadoresAsync(temporada, ct);
                if (todos == null) return null;
                // A chave do valor é o nome sem a categoria: "offensive.avgPoints" → "avgPoints".
                var chave = campo[(campo.IndexOf('.') + 1)..];
                var doTime = todos.Linhas
                    .Where(l => l.TimeId == timeId)
                    .OrderByDescending(l => double.TryParse(l.Valores.GetValueOrDefault(chave), NumberStyles.Any,
                        CultureInfo.InvariantCulture, out var v) ? v : double.MinValue)
                    .ToList();
                return todos with { Pagina = 1, TotalPaginas = 1, Total = doTime.Count, Linhas = doTime };
            }

            pagina = Math.Max(1, pagina);
            return await ComCacheAsync($"nba:jogadores:{temporada}:{campo}:{pagina}",
                $"common/v3/sports/basketball/nba/statistics/byathlete?season={temporada}&seasontype=2" +
                $"&limit={JogadoresPorPagina}&page={pagina}&sort={campo}:desc&{Idioma}",
                _ => CacheTabela,
                raiz => LerTabelaJogadores(raiz, temporada, pagina),
                ct);
        }

        // A liga inteira (~580 jogadores) numa resposta de ~9 MB. Pesado para buscar,
        // leve depois de convertido; fica 1 h em cache e serve qualquer time e ordem.
        private Task<NbaTabelaJogadores?> TodosJogadoresAsync(int temporada, CancellationToken ct) =>
            ComCacheAsync($"nba:jogadores-todos:{temporada}",
                $"common/v3/sports/basketball/nba/statistics/byathlete?season={temporada}&seasontype=2" +
                $"&limit=1000&sort=offensive.avgPoints:desc&{Idioma}",
                _ => TimeSpan.FromHours(1),
                raiz => LerTabelaJogadores(raiz, temporada, 1),
                ct);

        private static NbaTabelaJogadores LerTabelaJogadores(JsonElement raiz, int temporada, int pagina)
        {
            // Os valores de cada atleta vêm em arrays ("totals") alinhados com a
            // lista de nomes da categoria, que fica uma vez só na raiz.
            var nomes = Lista(raiz, "categories")
                .ToDictionary(c => Texto(c, "name") ?? "", c => Lista(c, "names").Select(n => n.GetString() ?? "").ToList());

            var linhas = Lista(raiz, "athletes").Select(item =>
            {
                var a = Prop(item, "athlete");
                var valores = new Dictionary<string, string>();
                foreach (var cat in Lista(item, "categories"))
                {
                    if (!nomes.TryGetValue(Texto(cat, "name") ?? "", out var colunas)) continue;
                    var totais = Lista(cat, "totals").Select(t => t.GetString() ?? "").ToList();
                    for (var i = 0; i < Math.Min(colunas.Count, totais.Count); i++)
                        valores.TryAdd(colunas[i], totais[i]);
                }
                return new NbaLinhaJogador(
                    Texto(a, "id") ?? "",
                    Texto(a, "displayName") ?? "",
                    Href(Prop(a, "headshot")),
                    Texto(Prop(a, "position"), "abbreviation"),
                    Texto(a, "teamId"),
                    Texto(a, "teamShortName"),
                    Lista(a, "teamLogos").Select(l => Texto(l, "href")).FirstOrDefault(),
                    valores);
            }).ToList();

            var pag = Prop(raiz, "pagination");
            return new NbaTabelaJogadores(
                Texto(Prop(raiz, "requestedSeason"), "displayName") ?? RotuloTemporada(temporada),
                Inteiro(pag, "page") ?? pagina,
                Inteiro(pag, "pages") ?? 1,
                Inteiro(pag, "count") ?? linhas.Count,
                linhas);
        }

        /// <summary>Os 30 times, em ordem alfabética, para o seletor da tabela de jogadores.</summary>
        public Task<IReadOnlyList<NbaTimeResumo>?> ObterTimesAsync(CancellationToken ct = default) =>
            ComCacheAsync("nba:times",
                $"site/v2/sports/basketball/nba/teams?{Idioma}",
                _ => CacheElenco,
                raiz => (IReadOnlyList<NbaTimeResumo>)Lista(raiz, "sports")
                    .SelectMany(s => Lista(s, "leagues"))
                    .SelectMany(l => Lista(l, "teams"))
                    .Select(t => LerTime(Prop(t, "team")))
                    .OrderBy(t => t.Nome)
                    .ToList(),
                ct);

        public Task<NbaTabelaTimes?> ObterTimesEstatisticasAsync(int temporada, CancellationToken ct = default) =>
            ComCacheAsync($"nba:times-stats:{temporada}",
                $"common/v3/sports/basketball/nba/statistics/byteam?season={temporada}&seasontype=2&{Idioma}",
                _ => CacheTabela,
                raiz =>
                {
                    // Cada categoria aparece duas vezes: a primeira é o próprio time e a
                    // segunda, o que os adversários fizeram contra ele. Os nomes das
                    // colunas da segunda ganham o prefixo "opp.".
                    var categorias = Lista(raiz, "categories").ToList();
                    var nomes = new List<List<string>>();
                    var vistas = new HashSet<string>();
                    foreach (var c in categorias)
                    {
                        var prefixo = vistas.Add(Texto(c, "name") ?? "") ? "" : "opp.";
                        nomes.Add(Lista(c, "names").Select(n => prefixo + n.GetString()).ToList());
                    }

                    var linhas = Lista(raiz, "teams").Select(item =>
                    {
                        var valores = new Dictionary<string, string>();
                        var cats = Lista(item, "categories").ToList();
                        for (var i = 0; i < Math.Min(cats.Count, nomes.Count); i++)
                        {
                            var totais = Lista(cats[i], "totals").Select(t => t.GetString() ?? "").ToList();
                            for (var k = 0; k < Math.Min(nomes[i].Count, totais.Count); k++)
                                valores.TryAdd(nomes[i][k], totais[k]);
                        }
                        return new NbaLinhaTime(LerTime(Prop(item, "team")), valores);
                    }).ToList();

                    return new NbaTabelaTimes(
                        Texto(Prop(raiz, "requestedSeason"), "displayName") ?? RotuloTemporada(temporada), linhas);
                },
                ct);

        // ── Jogo ──────────────────────────────────────────────────────────────

        public Task<NbaDetalheJogo?> ObterJogoAsync(string eventoId, CancellationToken ct = default) =>
            ComCacheAsync($"nba:jogo:{eventoId}",
                $"site/v2/sports/basketball/nba/summary?event={Uri.EscapeDataString(eventoId)}&{Idioma}",
                d => d.Jogo.Situacao == NbaSituacao.AoVivo ? CacheAoVivo
                     : d.Jogo.Situacao == NbaSituacao.Encerrado ? CacheElenco : CacheDia,
                LerDetalheJogo,
                ct);

        private static NbaDetalheJogo LerDetalheJogo(JsonElement raiz)
        {
            var header = Prop(raiz, "header");
            var comp = Lista(header, "competitions").FirstOrDefault();
            var gameInfo = Prop(raiz, "gameInfo");
            var venue = Prop(gameInfo, "venue");

            // O header do summary tem quase o mesmo formato do evento do scoreboard;
            // ginásio e transmissão moram em outros nós e entram por cima.
            var jogo = LerCompeticao(Texto(header, "id") ?? "", comp) with
            {
                Ginasio = Texto(venue, "fullName"),
                Cidade = Texto(Prop(venue, "address"), "city"),
                Transmissao = Lista(raiz, "broadcasts")
                    .Select(b => Texto(Prop(b, "media"), "shortName"))
                    .Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n!).Distinct().ToList(),
            };

            // Líderes do jogo ficam fora do header no summary.
            var lideres = Lista(raiz, "leaders").ToDictionary(
                l => Texto(Prop(l, "team"), "id") ?? "",
                l => (IReadOnlyList<NbaLider>)Lista(l, "leaders").Select(LerLider).Where(x => x != null).Select(x => x!).ToList());
            jogo = jogo with
            {
                Casa = jogo.Casa with { Lideres = lideres.GetValueOrDefault(jogo.Casa.Time.Id, jogo.Casa.Lideres) },
                Visitante = jogo.Visitante with { Lideres = lideres.GetValueOrDefault(jogo.Visitante.Time.Id, jogo.Visitante.Lideres) },
            };

            var box = Lista(Prop(raiz, "boxscore"), "players").Select(p =>
            {
                var bloco = Lista(p, "statistics").FirstOrDefault();
                var atletas = Lista(bloco, "athletes").Select(a =>
                {
                    var at = Prop(a, "athlete");
                    return new NbaAtletaBox(
                        Texto(at, "id") ?? "",
                        Texto(at, "displayName") ?? "",
                        Texto(Prop(at, "position"), "abbreviation"),
                        Texto(at, "jersey"),
                        Href(Prop(at, "headshot")),
                        Bool(a, "starter"),
                        Bool(a, "didNotPlay"),
                        Texto(a, "reason"),
                        Lista(a, "stats").Select(s => s.GetString() ?? "").ToList());
                }).ToList();
                return new NbaBoxTime(
                    LerTime(Prop(p, "team")),
                    Lista(bloco, "labels").Select(s => s.GetString() ?? "").ToList(),
                    atletas,
                    Lista(bloco, "totals").Select(s => s.GetString() ?? "").ToList());
            }).ToList();
            // Casa por último, como no placar ("visitante @ casa").
            box = box.OrderBy(b => b.Time.Id == jogo.Casa.Time.Id).ToList();

            var times = Lista(Prop(raiz, "boxscore"), "teams").ToList();
            var estatCasa = times.FirstOrDefault(t => Texto(t, "homeAway") == "home");
            var estatFora = times.FirstOrDefault(t => Texto(t, "homeAway") == "away");
            var foraPorNome = Lista(estatFora, "statistics")
                .GroupBy(s => Texto(s, "name") ?? "").ToDictionary(g => g.Key, g => Texto(g.First(), "displayValue") ?? "");
            var estatisticas = Lista(estatCasa, "statistics")
                // "teamTurnovers"/"totalTechnicalFouls" repetem o rótulo de outra linha
                // e só confundem a comparação lado a lado.
                .Where(s => Texto(s, "name") is not ("teamTurnovers" or "totalTechnicalFouls"))
                .Select(s => new NbaEstatTime(
                    RotuloEstatTime(Texto(s, "name"), Texto(s, "label")),
                    Texto(s, "displayValue") ?? "",
                    foraPorNome.GetValueOrDefault(Texto(s, "name") ?? "", "")))
                .ToList();

            var lances = Lista(raiz, "plays").Select(p => new NbaLance(
                Inteiro(Prop(p, "period"), "number") ?? 0,
                Texto(Prop(p, "clock"), "displayValue") ?? "",
                Texto(p, "text") ?? "",
                Inteiro(p, "homeScore") ?? 0,
                Inteiro(p, "awayScore") ?? 0,
                Texto(Prop(p, "team"), "id"),
                Bool(p, "scoringPlay"))).ToList();

            var lesoes = Lista(raiz, "injuries").SelectMany(t =>
            {
                var timeId = Texto(Prop(t, "team"), "id") ?? "";
                return Lista(t, "injuries").Select(i => new NbaLesao(
                    timeId,
                    Texto(Prop(i, "athlete"), "displayName") ?? "",
                    Texto(i, "status") ?? "",
                    Texto(Prop(i, "details"), "type")));
            }).ToList();

            return new NbaDetalheJogo(
                jogo,
                box,
                estatisticas,
                lances,
                lesoes,
                Lista(raiz, "seasonseries").Select(s => Texto(s, "summary")).FirstOrDefault(),
                Inteiro(gameInfo, "attendance"),
                Lista(gameInfo, "officials").Select(o => Texto(o, "displayName") ?? "").Where(n => n != "").ToList());
        }

        // Alguns rótulos ainda chegam em inglês mesmo com lang=pt.
        private static string RotuloEstatTime(string? nome, string? rotulo) => nome switch
        {
            "fieldGoalsMade-fieldGoalsAttempted" => "Arremessos de quadra",
            "fieldGoalPct" => "% de quadra",
            "threePointFieldGoalsMade-threePointFieldGoalsAttempted" => "Bolas de 3",
            "threePointFieldGoalPct" => "% de 3",
            "freeThrowsMade-freeThrowsAttempted" => "Lances livres",
            "freeThrowPct" => "% de lance livre",
            "totalTurnovers" => "Erros (total)",
            "turnoverPoints" => "Pontos após erro",
            "fastBreakPoints" => "Pontos de contra-ataque",
            _ => rotulo ?? nome ?? "",
        };

        // ── Time ──────────────────────────────────────────────────────────────

        public async Task<NbaDetalheTime?> ObterTimeAsync(string timeId, int temporada, CancellationToken ct = default)
        {
            var id = Uri.EscapeDataString(timeId);
            var time = await ComCacheAsync($"nba:time:{timeId}",
                $"site/v2/sports/basketball/nba/teams/{id}?{Idioma}",
                _ => CacheDia,
                raiz =>
                {
                    var t = Prop(raiz, "team");
                    var registro = Lista(Prop(t, "record"), "items").FirstOrDefault();
                    return new CabecalhoTime(LerTime(t), Texto(registro, "summary"), Texto(t, "standingSummary"));
                },
                ct);
            if (time == null) return null;

            var elenco = await ComCacheAsync($"nba:elenco:{timeId}",
                $"site/v2/sports/basketball/nba/teams/{id}/roster?{Idioma}",
                _ => CacheElenco,
                raiz =>
                {
                    var treinador = Lista(raiz, "coach").Select(c => $"{Texto(c, "firstName")} {Texto(c, "lastName")}".Trim()).FirstOrDefault();
                    var atletas = Lista(raiz, "athletes").Select(a => new NbaAtletaElenco(
                        Texto(a, "id") ?? "",
                        Texto(a, "displayName") ?? "",
                        Texto(a, "jersey"),
                        Texto(Prop(a, "position"), "abbreviation"),
                        Altura(Prop(a, "height")) ?? Texto(a, "displayHeight"),
                        Peso(Prop(a, "weight")) ?? Texto(a, "displayWeight"),
                        Inteiro(a, "age"),
                        Inteiro(Prop(a, "experience"), "years"),
                        Texto(Prop(a, "college"), "name"),
                        Href(Prop(a, "headshot")),
                        Lista(a, "injuries").Select(i => Texto(i, "status")).FirstOrDefault()))
                        .OrderBy(a => int.TryParse(a.Camisa, out var n) ? n : 999)
                        .ToList();
                    return new ElencoTime(treinador, atletas);
                },
                ct);

            var calendario = await ComCacheAsync($"nba:calendario:{timeId}:{temporada}",
                $"site/v2/sports/basketball/nba/teams/{id}/schedule?season={temporada}&{Idioma}",
                _ => CacheDia,
                raiz => (IReadOnlyList<NbaJogo>)Lista(raiz, "events").Select(LerJogo).ToList(),
                ct);

            return new NbaDetalheTime(
                time.Time,
                time.Campanha,
                time.Posicao,
                elenco?.Treinador,
                elenco?.Atletas ?? Array.Empty<NbaAtletaElenco>(),
                calendario ?? Array.Empty<NbaJogo>());
        }

        private record CabecalhoTime(NbaTimeResumo Time, string? Campanha, string? Posicao);
        private record ElencoTime(string? Treinador, IReadOnlyList<NbaAtletaElenco> Atletas);

        // A ESPN dá altura em polegadas e peso em libras; aqui a gente usa metro e kg.
        private static string? Altura(JsonElement el) =>
            el.ValueKind == JsonValueKind.Number ? (el.GetDouble() * 0.0254).ToString("0.00", CultureInfo.GetCultureInfo("pt-BR")) + " m" : null;

        private static string? Peso(JsonElement el) =>
            el.ValueKind == JsonValueKind.Number ? Math.Round(el.GetDouble() * 0.4536).ToString(CultureInfo.InvariantCulture) + " kg" : null;

        // ── Leitura do evento (scoreboard, schedule e header do summary) ──────

        private static NbaJogo LerJogo(JsonElement evento)
        {
            var comp = Lista(evento, "competitions").FirstOrDefault();
            var jogo = LerCompeticao(Texto(evento, "id") ?? "", comp);
            // No scoreboard o status fica no evento; no header do summary, na competição.
            if (evento.TryGetProperty("status", out var status))
                jogo = jogo with { Situacao = Situacao(status), Status = TextoStatus(status) };
            return jogo;
        }

        private static NbaJogo LerCompeticao(string id, JsonElement comp)
        {
            var competidores = Lista(comp, "competitors").ToList();
            var casa = competidores.FirstOrDefault(c => Texto(c, "homeAway") == "home");
            var fora = competidores.FirstOrDefault(c => Texto(c, "homeAway") == "away");
            var status = Prop(comp, "status");
            var venue = Prop(comp, "venue");

            var inicio = DateTime.TryParse(Texto(comp, "date"), CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var d) ? d : DateTime.MinValue;

            // "broadcasts" (lista de nomes) é a grade dos EUA; "geoBroadcasts" traz o
            // tipo de mídia. Junta as duas sem repetir.
            var transmissao = Lista(comp, "broadcasts").SelectMany(b => Lista(b, "names").Select(n => n.GetString()))
                .Concat(Lista(comp, "geoBroadcasts").Select(g => Texto(Prop(g, "media"), "shortName")))
                .Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n!).Distinct().ToList();

            return new NbaJogo(
                id,
                inicio,
                Situacao(status),
                TextoStatus(status),
                LerLado(casa, true),
                LerLado(fora, false),
                Texto(venue, "fullName"),
                Texto(Prop(venue, "address"), "city"),
                transmissao,
                Lista(comp, "headlines").Select(h => Texto(h, "shortLinkText") ?? Texto(h, "description")).FirstOrDefault(),
                Lista(comp, "notes").Select(n => Texto(n, "headline")).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)));
        }

        private static NbaLadoJogo LerLado(JsonElement c, bool casa)
        {
            // score é string no scoreboard e objeto {value, displayValue} no schedule.
            var score = Prop(c, "score");
            int? placar = score.ValueKind switch
            {
                JsonValueKind.String => int.TryParse(score.GetString(), out var p) ? p : null,
                JsonValueKind.Object => Inteiro(score, "value"),
                _ => null,
            };

            // "records" (scoreboard) ou "record" (summary/schedule); o total é o primeiro.
            var campanha = Lista(c, "records").Concat(Lista(c, "record"))
                .Select(r => Texto(r, "summary") ?? Texto(r, "displayValue")).FirstOrDefault();

            return new NbaLadoJogo(
                c.ValueKind == JsonValueKind.Object ? LerTime(Prop(c, "team")) : new NbaTimeResumo("", "A definir", "A definir", "?", null, null),
                casa,
                placar,
                Bool(c, "winner"),
                campanha,
                Lista(c, "linescores").Select(l => Inteiro(l, "value") ?? Inteiro(l, "displayValue") ?? 0).ToList(),
                Lista(c, "leaders").Select(LerLider).Where(l => l != null && l.Categoria != "rating").Select(l => l!).ToList());
        }

        private static NbaLider? LerLider(JsonElement categoria)
        {
            var l = Lista(categoria, "leaders").FirstOrDefault();
            if (l.ValueKind != JsonValueKind.Object) return null;
            var a = Prop(l, "athlete");
            return new NbaLider(
                Texto(categoria, "name") ?? "",
                Texto(a, "id") ?? "",
                Texto(a, "shortName") ?? Texto(a, "displayName") ?? "",
                Texto(l, "displayValue") ?? "",
                Href(Prop(a, "headshot")));
        }

        private static NbaTimeResumo LerTime(JsonElement t)
        {
            // "logo" (string) no scoreboard; "logos" (lista) no resto.
            var logo = Texto(t, "logo") ?? Lista(t, "logos").Select(l => Texto(l, "href")).FirstOrDefault();
            var cor = Texto(t, "color");
            return new NbaTimeResumo(
                Texto(t, "id") ?? "",
                Texto(t, "displayName") ?? Texto(t, "name") ?? "",
                Texto(t, "shortDisplayName") ?? Texto(t, "name") ?? "",
                Texto(t, "abbreviation") ?? "",
                logo,
                string.IsNullOrEmpty(cor) ? null : "#" + cor);
        }

        private static NbaSituacao Situacao(JsonElement status)
        {
            var tipo = Prop(status, "type");
            return Texto(tipo, "state") switch
            {
                "in" => NbaSituacao.AoVivo,
                "post" => Texto(tipo, "name") is "STATUS_POSTPONED" or "STATUS_CANCELED" ? NbaSituacao.Adiado : NbaSituacao.Encerrado,
                _ => Texto(tipo, "name") is "STATUS_POSTPONED" or "STATUS_CANCELED" ? NbaSituacao.Adiado : NbaSituacao.Agendado,
            };
        }

        /// <summary>
        /// Texto do relógio montado aqui: o shortDetail da ESPN mistura idiomas
        /// ("FIM" num, "10/21 - 7:00 PM EDT" noutro) e usa o fuso dos EUA.
        /// </summary>
        private static string TextoStatus(JsonElement status)
        {
            var tipo = Prop(status, "type");
            var nome = Texto(tipo, "name");
            var periodo = Inteiro(status, "period") ?? 0;
            var relogio = Texto(status, "displayClock");

            string Periodo(int p) => p <= 4 ? $"{p}º quarto" : p == 5 ? "Prorrogação" : $"{p - 4}ª prorrogação";

            return nome switch
            {
                "STATUS_FINAL" => periodo > 4 ? (periodo == 5 ? "Fim (prorr.)" : $"Fim ({periodo - 4} prorr.)") : "Fim",
                "STATUS_HALFTIME" => "Intervalo",
                "STATUS_END_PERIOD" => $"Fim do {Periodo(periodo)}",
                "STATUS_POSTPONED" => "Adiado",
                "STATUS_CANCELED" => "Cancelado",
                "STATUS_DELAYED" => "Atrasado",
                _ when Texto(tipo, "state") == "in" => $"{Periodo(periodo)} · {relogio}",
                _ => "",
            };
        }

        // ── JSON ──────────────────────────────────────────────────────────────

        private static JsonElement Prop(JsonElement el, string nome) =>
            el.ValueKind == JsonValueKind.Object && el.TryGetProperty(nome, out var v) ? v : default;

        private static IEnumerable<JsonElement> Lista(JsonElement el, string nome) =>
            Prop(el, nome) is { ValueKind: JsonValueKind.Array } a ? a.EnumerateArray() : Enumerable.Empty<JsonElement>();

        private static string? Texto(JsonElement el, string nome)
        {
            var v = Prop(el, nome);
            return v.ValueKind switch
            {
                JsonValueKind.String => v.GetString(),
                JsonValueKind.Number => v.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null,
            };
        }

        private static int? Inteiro(JsonElement el, string nome)
        {
            var v = Prop(el, nome);
            if (v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d)) return (int)Math.Round(d);
            if (v.ValueKind == JsonValueKind.String && double.TryParse(v.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var s)) return (int)Math.Round(s);
            return null;
        }

        private static bool Bool(JsonElement el, string nome) => Prop(el, nome).ValueKind == JsonValueKind.True;

        // headshot vem como string em alguns endpoints e como {href} em outros.
        private static string? Href(JsonElement el) => el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Object => Texto(el, "href"),
            _ => null,
        };

        // ── HTTP com cache ────────────────────────────────────────────────────

        /// <summary>
        /// Busca, converte e guarda o modelo pronto (não o JSON cru: o summary passa de
        /// 400 KB e seria reconvertido a cada F5). A validade é decidida olhando o
        /// próprio resultado — jogo ao vivo expira bem mais rápido.
        /// </summary>
        private async Task<T?> ComCacheAsync<T>(
            string chave, string url, Func<T, TimeSpan> validade, Func<JsonElement, T> converter, CancellationToken ct)
        {
            if (_cache.TryGetValue<T>(chave, out var cacheado) && cacheado != null)
                return cacheado;

            var corpo = await BaixarComEsperaAsync(url, ct);
            if (corpo == null) return default;

            T resultado;
            try
            {
                using var doc = JsonDocument.Parse(corpo);
                resultado = converter(doc.RootElement);
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "[ESPN NBA] Resposta inesperada em {Url}.", url);
                return default;
            }

            _cache.Set(chave, resultado, new MemoryCacheEntryOptions
            {
                // O modelo convertido ocupa bem menos que o JSON; o tamanho do corpo é
                // um teto seguro para o SizeLimit do cache.
                Size = Math.Max(1, corpo.Length / 4),
                AbsoluteExpirationRelativeToNow = validade(resultado),
            });
            return resultado;
        }

        private async Task<string?> BaixarComEsperaAsync(string url, CancellationToken ct)
        {
            await _porta.WaitAsync(ct);
            try
            {
                for (var tentativa = 1; tentativa <= 3; tentativa++)
                {
                    var desde = DateTimeOffset.UtcNow - _ultimaChamada;
                    if (desde < IntervaloMinimo)
                        await Task.Delay(IntervaloMinimo - desde, ct);

                    try
                    {
                        using var resp = await _http.GetAsync(url, ct);
                        _ultimaChamada = DateTimeOffset.UtcNow;

                        if (resp.IsSuccessStatusCode)
                            return await resp.Content.ReadAsStringAsync(ct);

                        if (resp.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)
                        {
                            var espera = resp.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(5 * tentativa);
                            _logger.LogWarning("[ESPN NBA] {Status} em {Url} — aguardando {Espera}s.",
                                (int)resp.StatusCode, url, espera.TotalSeconds);
                            await Task.Delay(espera, ct);
                            continue;
                        }

                        _logger.LogInformation("[ESPN NBA] {Status} em {Url}.", (int)resp.StatusCode, url);
                        return null;
                    }
                    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
                    {
                        _ultimaChamada = DateTimeOffset.UtcNow;
                        _logger.LogWarning(ex, "[ESPN NBA] Falha de rede em {Url} (tentativa {N}).", url, tentativa);
                        if (tentativa == 3) return null;
                        await Task.Delay(TimeSpan.FromSeconds(2 * tentativa), ct);
                    }
                }

                return null;
            }
            finally
            {
                _porta.Release();
            }
        }
    }
}
