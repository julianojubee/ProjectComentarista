using System.Globalization;
using System.Net;
using System.Text.Json;
using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ControleFutebolWeb.Services
{
    public record ResultadoFotMob(
        bool Ok,
        string Mensagem,
        long? PartidaFotMob = null,
        bool GravouEstatisticasTime = false,
        int JogadoresGravados = 0);

    /// <summary>
    /// Terceira fonte de estatísticas, para as partidas que nem a api-football nem a
    /// ESPN cobrem. O caso que motivou: a Qatar Stars League volta da api-football só
    /// com o placar (players/statistics vazios) e a ESPN não tem a liga no catálogo
    /// dela, então aqueles jogos ficavam sem estatística nenhuma.
    ///
    /// Consome a API JSON interna do fotmob.com (www.fotmob.com/api/data), a mesma que
    /// o front-end deles usa. Ela responde JSON direto, sem token, sem cookie e sem o
    /// header assinado "x-mas" que o FotMob já exigiu no passado — nada aqui contorna
    /// autenticação ou verificação de robô. Se um dia voltarem a assinar as chamadas,
    /// esta fonte simplesmente para de responder e os jogos continuam como estavam.
    ///
    /// PROFUNDIDADE: é a fonte mais completa das três. Por jogador vêm passes,
    /// desarmes, interceptações, bloqueios, duelos, dribles e minutos em campo, que a
    /// ESPN não publica — ver FonteEstatistica, que é quem diz o que cada fonte cobre.
    ///
    /// O QUE NÃO É IMPORTADO DE PROPÓSITO: a nota do jogador (rating_title) e as
    /// "grandes chances" (big_chance*). Não é limitação técnica — os dois campos vêm
    /// preenchidos. São métricas PROPRIETÁRIAS: a nota sai de um modelo do próprio
    /// FotMob e a grande chance é uma classificação subjetiva da Opta. Reexibi-las com
    /// o mesmo valor tornaria a origem do dado óbvia na tela, e nenhuma delas é fato da
    /// partida — é opinião de terceiro. Os números objetivos (o que aconteceu em campo)
    /// entram; as opiniões, não. Ver RiscoDeExposicao, logo abaixo.
    ///
    /// Para não virar abuso, todas as chamadas passam por <see cref="_porta"/>: uma de
    /// cada vez, com intervalo mínimo entre elas, resposta em cache e recuo (com
    /// espera) em 429/503 — o mesmo tratamento que EspnEstatisticasService dá à ESPN.
    /// </summary>
    public class FotMobService
    {
        /// <summary>
        /// Nota sobre o uso desta fonte, deixada no código porque a decisão é de
        /// produto e não de implementação:
        ///
        /// Os Termos de Uso do FotMob não autorizam uso comercial dos dados. Enquanto
        /// o Comentarista for de uso pessoal isso não tem consequência prática, mas se
        /// virar produto público a decisão precisa ser revista — e não pelo lado do
        /// tráfego (uma chamada por jogo finalizado é indistinguível de alguém
        /// navegando no site), e sim pelo lado da tela: dado exibido é dado auditável
        /// por qualquer um que abra as duas páginas lado a lado.
        ///
        /// Por isso duas escolhas ficam registradas aqui: só entram métricas objetivas
        /// (as proprietárias ficam de fora, ver acima) e toda linha gravada carrega
        /// Fonte = FonteEstatistica.FotMob, para que "o que veio daqui?" seja um WHERE
        /// e não uma escavação.
        /// </summary>
        private const string RiscoDeExposicao =
            "Fonte de terceiro sem licença de uso comercial — ver comentário em FotMobService.";

        // Um jogo custa 1 chamada de matchDetails; a lista de jogos da liga fica em
        // cache e é compartilhada por todos os jogos da mesma competição, então uma
        // rodada inteira de 10 jogos gasta ~11 chamadas.
        public const int LimitePorLote = 20;

        private const string Base = "https://www.fotmob.com/api/data/";

        // Uma requisição por vez para o host, com no mínimo esse intervalo entre elas.
        // Estático porque o serviço é transiente (AddHttpClient<T>) e o que precisa ser
        // serializado é o acesso ao host, não a instância.
        private static readonly SemaphoreSlim _porta = new(1, 1);
        private static DateTimeOffset _ultimaChamada = DateTimeOffset.MinValue;
        private static readonly TimeSpan IntervaloMinimo = TimeSpan.FromMilliseconds(1500);

        private readonly HttpClient _http;
        private readonly ILogger<FotMobService> _logger;
        private readonly IMemoryCache _cache;
        private readonly IWebHostEnvironment _env;

        public FotMobService(
            HttpClient http,
            ILogger<FotMobService> logger,
            IMemoryCache cache,
            IWebHostEnvironment env)
        {
            _http = http;
            _http.BaseAddress = new Uri(Base);
            _http.Timeout = TimeSpan.FromSeconds(25);
            _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
            _http.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
            _logger = logger;
            _cache = cache;
            _env = env;
        }

        // ── De-para de liga ───────────────────────────────────────────────────

        /// <summary>
        /// Ids de liga do FotMob para o id de liga da api-football. Mais de um quando o
        /// FotMob separa o que o nosso cadastro junta (a fase preliminar da Champions é
        /// uma liga própria, a 10611). Vazio = liga sem cobertura.
        /// </summary>
        public IReadOnlyList<int> LigasDaCompeticao(int? idApiLiga)
        {
            if (idApiLiga is null) return Array.Empty<int>();
            return MapaLigas().GetValueOrDefault(
                idApiLiga.Value.ToString(CultureInfo.InvariantCulture), Array.Empty<int>());
        }

        /// <summary>A competição tem correspondente no FotMob? Rotula o botão na tela.</summary>
        public bool TemCobertura(int? idApiLiga) => LigasDaCompeticao(idApiLiga).Count > 0;

        private Dictionary<string, int[]> MapaLigas() =>
            _cache.GetOrCreate("fotmob:ligas", e =>
            {
                e.Size = 4096;
                e.SlidingExpiration = TimeSpan.FromHours(6);

                var caminho = Path.Combine(_env.WebRootPath, "data", "ligas-fotmob.json");
                if (!File.Exists(caminho)) return new Dictionary<string, int[]>();

                var bruto = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(caminho))
                            ?? new Dictionary<string, JsonElement>();

                var mapa = new Dictionary<string, int[]>();
                foreach (var (chave, valor) in bruto)
                {
                    // As chaves "_comentario*" documentam o arquivo e não são ligas.
                    if (chave.StartsWith('_')) continue;

                    // Aceita 535 e [42, 10611] — a forma de um id só é mais legível
                    // para a maioria das ligas, que é o caso comum.
                    var ids = valor.ValueKind switch
                    {
                        JsonValueKind.Number => new[] { valor.GetInt32() },
                        JsonValueKind.Array => valor.EnumerateArray()
                            .Where(x => x.ValueKind == JsonValueKind.Number)
                            .Select(x => x.GetInt32()).ToArray(),
                        _ => Array.Empty<int>(),
                    };
                    if (ids.Length > 0) mapa[chave] = ids;
                }
                return mapa;
            }) ?? new Dictionary<string, int[]>();

        // ── Importação ────────────────────────────────────────────────────────

        /// <summary>
        /// Preenche as estatísticas do jogo com o que o FotMob tem. Não apaga nada que
        /// já exista: se o jogo já tem estatísticas de time, elas são mantidas e só as
        /// de jogador são complementadas (e vice-versa).
        /// </summary>
        public async Task<ResultadoFotMob> ImportarAsync(
            FutebolContext context, int jogoId, CancellationToken ct = default)
        {
            var (jogo, doc, partida, erro) = await AbrirPartidaAsync(context, jogoId, exigeIdApi: true, ct);

            // Toda tentativa vai para a tela de logs, inclusive (e principalmente) a que
            // não achou o jogo — ver LogFonteExterna.
            async Task<ResultadoFotMob> RegistrarAsync(ResultadoFotMob r)
            {
                await LogFonteExterna.RegistrarAsync(
                    context, LogFonteExterna.TipoFotMob, "Importar estatísticas", r.Ok, jogo,
                    r.PartidaFotMob is long id ? $"{r.Mensagem} [partida {id}]" : r.Mensagem, ct);
                return r;
            }

            if (erro != null) return await RegistrarAsync(erro);

            using var _ = doc!;
            var raiz = doc!.RootElement;

            var gravouTime = await GravarEstatisticasTimeAsync(context, jogo!, raiz, ct);
            var jogadores = await GravarEstatisticasJogadoresAsync(context, jogo!, raiz, ct);

            if (!gravouTime && jogadores == 0)
                return await RegistrarAsync(new ResultadoFotMob(false,
                    "O FotMob localizou a partida, mas também não tem estatísticas dela.", partida));

            await context.SaveChangesAsync(ct);

            var partes = new List<string>();
            if (gravouTime) partes.Add("estatísticas de time");
            if (jogadores > 0) partes.Add($"{jogadores} jogador(es)");

            return await RegistrarAsync(new ResultadoFotMob(true,
                $"Importado do FotMob: {string.Join(" e ", partes)}.",
                partida, gravouTime, jogadores));
        }

        /// <summary>
        /// Localiza a partida no FotMob e devolve o matchDetails dela. Separado da
        /// importação porque a conferência de escalação (o equivalente ao que
        /// EspnEscalacaoService faz com a ESPN) parte do mesmo documento e das mesmas
        /// checagens de pré-requisito.
        /// </summary>
        /// <param name="exigeIdApi">
        /// Só a importação precisa: EstatisticasJson é indexado pelo IdApi do time.
        /// </param>
        internal async Task<(Jogo? Jogo, JsonDocument? Doc, long? Partida, ResultadoFotMob? Erro)> AbrirPartidaAsync(
            FutebolContext context, int jogoId, bool exigeIdApi, CancellationToken ct)
        {
            var jogo = await context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Include(j => j.Competicao)
                .FirstOrDefaultAsync(j => j.Id == jogoId, ct);

            ResultadoFotMob Falha(string msg, long? partida = null) => new(false, msg, partida);

            if (jogo == null) return (null, null, null, Falha("Jogo não encontrado."));
            if (jogo.Data == null)
                return (jogo, null, null, Falha("Jogo sem data — não dá para localizar no FotMob."));

            var ligas = LigasDaCompeticao(jogo.Competicao?.IdApi);
            if (ligas.Count == 0)
                return (jogo, null, null, Falha(
                    $"A competição \"{jogo.Competicao?.Nome}\" não tem correspondente no FotMob (ver wwwroot/data/ligas-fotmob.json)."));

            if (exigeIdApi && (jogo.TimeCasa is not { IdApi: > 0 } || jogo.TimeVisitante is not { IdApi: > 0 }))
                return (jogo, null, null, Falha(
                    "Um dos times está sem IdApi — as estatísticas de time são indexadas por esse id."));

            var partida = await ResolverPartidaAsync(jogo, ligas, ct);
            if (partida == null)
                return (jogo, null, null, Falha(
                    "Partida não localizada no FotMob (liga, data ou nome dos times não bateram)."));

            var doc = await BuscarJsonAsync($"matchDetails?matchId={partida}", TimeSpan.FromDays(7), ct);
            if (doc == null)
                return (jogo, null, partida, Falha("O FotMob não respondeu os detalhes da partida.", partida));

            return (jogo, doc, partida, null);
        }

        // ── Localização da partida ────────────────────────────────────────────

        /// <summary>
        /// Acha o id da partida no FotMob pela tabela de jogos da liga. Uma chamada de
        /// /leagues por liga (em cache por 6h) resolve a competição inteira, o que é
        /// bem mais barato que consultar o calendário por data: /matches?date= traz
        /// todos os jogos do planeta naquele dia.
        ///
        /// Casa por nome dos DOIS times e por data. A data entra com tolerância de um
        /// dia para cada lado porque o FotMob publica em UTC e um jogo das 21h de
        /// Brasília cai no dia seguinte lá.
        /// </summary>
        private async Task<long?> ResolverPartidaAsync(
            Jogo jogo, IReadOnlyList<int> ligas, CancellationToken ct)
        {
            var dia = DateOnly.FromDateTime(jogo.Data!.Value.ToUniversalTime().Date);

            foreach (var liga in ligas)
            {
                // Temporada corrente primeiro: é onde estão os jogos que faltam
                // estatística na esmagadora maioria das vezes.
                var achado = await ProcurarNaLigaAsync(jogo, liga, temporada: null, dia, ct);
                if (achado != null) return achado;

                // Não achou: o jogo pode ser de uma temporada anterior à que o FotMob
                // abre por padrão. allAvailableSeasons lista as temporadas no formato
                // que o parâmetro season aceita ("2025/2026" ou "2025", conforme a liga
                // seja de temporada cruzada ou de ano-calendário).
                var temporada = await ResolverTemporadaAsync(liga, jogo.Temporada, ct);
                if (temporada == null) continue;

                achado = await ProcurarNaLigaAsync(jogo, liga, temporada, dia, ct);
                if (achado != null) return achado;
            }

            return null;
        }

        private async Task<long?> ProcurarNaLigaAsync(
            Jogo jogo, int liga, string? temporada, DateOnly dia, CancellationToken ct)
        {
            var url = $"leagues?id={liga}";
            if (temporada != null) url += $"&season={Uri.EscapeDataString(temporada)}";

            using var doc = await BuscarJsonAsync(url, TimeSpan.FromHours(6), ct);
            if (doc == null) return null;

            if (!doc.RootElement.TryGetProperty("fixtures", out var fixtures) ||
                !fixtures.TryGetProperty("allMatches", out var jogos)) return null;

            foreach (var m in jogos.EnumerateArray())
            {
                if (!m.TryGetProperty("home", out var casa) ||
                    !m.TryGetProperty("away", out var fora)) continue;

                if (!TimeNomeMatcher.SaoMesmoTime(jogo.TimeCasa?.Nome, casa.GetProperty("name").GetString()) ||
                    !TimeNomeMatcher.SaoMesmoTime(jogo.TimeVisitante?.Nome, fora.GetProperty("name").GetString()))
                    continue;

                // O nome dos dois times já é bem específico, mas a data é o que separa
                // o jogo de ida do de volta entre os mesmos adversários.
                if (!m.TryGetProperty("status", out var status) ||
                    !status.TryGetProperty("utcTime", out var quando) ||
                    !DateTimeOffset.TryParse(quando.GetString(), CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal, out var utc)) continue;

                if (Math.Abs(DateOnly.FromDateTime(utc.UtcDateTime.Date).DayNumber - dia.DayNumber) > 1) continue;

                if (long.TryParse(m.GetProperty("id").GetString(), out var id)) return id;
            }

            return null;
        }

        /// <summary>
        /// Acha, entre as temporadas que a liga oferece, a que corresponde ao ano
        /// gravado no jogo. Aceita tanto "2025" quanto "2025/2026" — nos dois casos o
        /// ano de início é o que importa, porque é assim que Jogo.Temporada é gravado.
        /// </summary>
        private async Task<string?> ResolverTemporadaAsync(int liga, int temporadaJogo, CancellationToken ct)
        {
            using var doc = await BuscarJsonAsync($"leagues?id={liga}", TimeSpan.FromHours(6), ct);
            if (doc == null) return null;

            if (!doc.RootElement.TryGetProperty("allAvailableSeasons", out var temporadas) ||
                temporadas.ValueKind != JsonValueKind.Array) return null;

            var ano = temporadaJogo.ToString(CultureInfo.InvariantCulture);

            foreach (var t in temporadas.EnumerateArray())
            {
                var nome = t.GetString();
                if (nome == null) continue;
                if (nome == ano || nome.StartsWith(ano + "/", StringComparison.Ordinal)) return nome;
            }

            return null;
        }

        // ── Estatísticas de time ──────────────────────────────────────────────

        // Chaves do FotMob traduzidas para os nomes que a api-football usa, que são os
        // que Jogo.EstatisticasJson guarda e que EstatisticaTimeCalculator, o painel de
        // Analisar e os relatórios já sabem ler.
        //
        // As chaves do FotMob são ids internos estáveis ("BallPossesion", com o erro de
        // digitação deles mesmo) — o title ao lado varia com o idioma da resposta e por
        // isso não serve para casar.
        private static readonly (string FotMob, string ApiFootball, bool Percentual)[] ChavesTime =
        {
            ("BallPossesion",              "Ball Possession",  true),
            ("total_shots",                "Total Shots",      false),
            ("ShotsOnTarget",              "Shots on Goal",    false),
            ("ShotsOffTarget",             "Shots off Goal",   false),
            ("blocked_shots",              "Blocked Shots",    false),
            ("shots_inside_box",           "Shots insidebox",  false),
            ("shots_outside_box",          "Shots outsidebox", false),
            ("corners",                    "Corner Kicks",     false),
            ("Offsides",                   "Offsides",         false),
            ("fouls",                      "Fouls",            false),
            ("keeper_saves",               "Goalkeeper Saves", false),
            ("passes",                     "Total passes",     false),
        };

        private async Task<bool> GravarEstatisticasTimeAsync(
            FutebolContext context, Jogo jogo, JsonElement raiz, CancellationToken ct)
        {
            if (!string.IsNullOrWhiteSpace(jogo.EstatisticasJson)) return false;

            // content.stats.Periods.All.stats[] são os GRUPOS ("Top stats", "Shots",
            // "Passes"...); cada um traz as linhas. FirstHalf/SecondHalf existem ao
            // lado e são ignorados: o resto do sistema trabalha com o jogo inteiro.
            if (!raiz.TryGetProperty("content", out var conteudo) ||
                !conteudo.TryGetProperty("stats", out var stats) ||
                !stats.TryGetProperty("Periods", out var periodos) ||
                !periodos.TryGetProperty("All", out var todos) ||
                !todos.TryGetProperty("stats", out var grupos)) return false;

            // Toda linha vem como par [casa, visitante], na ordem de general.homeTeam /
            // awayTeam. Confere contra o nosso cadastro em vez de confiar na ordem: se
            // os times estiverem invertidos aqui, é melhor não gravar nada do que
            // gravar a posse de bola trocada.
            if (!raiz.TryGetProperty("general", out var geral)) return false;
            var nomeCasa = geral.GetProperty("homeTeam").GetProperty("name").GetString();
            var nomeFora = geral.GetProperty("awayTeam").GetProperty("name").GetString();

            if (!TimeNomeMatcher.SaoMesmoTime(jogo.TimeCasa?.Nome, nomeCasa) ||
                !TimeNomeMatcher.SaoMesmoTime(jogo.TimeVisitante?.Nome, nomeFora))
            {
                _logger.LogWarning(
                    "[FotMob] Jogo {Id}: mandante do FotMob ({Casa}) não bate com o cadastro ({Nosso}) — não gravei.",
                    jogo.Id, nomeCasa, jogo.TimeCasa?.Nome);
                return false;
            }

            // Índice 0 = casa, 1 = visitante.
            var brutos = new Dictionary<string, (double? Valor, double? Total)>[]
            {
                new(StringComparer.Ordinal), new(StringComparer.Ordinal),
            };

            foreach (var grupo in grupos.EnumerateArray())
            {
                if (!grupo.TryGetProperty("stats", out var linhas)) continue;

                foreach (var linha in linhas.EnumerateArray())
                {
                    var chave = linha.TryGetProperty("key", out var k) ? k.GetString() : null;
                    if (chave == null) continue;

                    // type "title" é cabeçalho de seção e vem com [null, null].
                    if (linha.TryGetProperty("type", out var tipo) && tipo.GetString() == "title") continue;
                    if (!linha.TryGetProperty("stats", out var par) || par.GetArrayLength() < 2) continue;

                    for (var lado = 0; lado < 2; lado++)
                    {
                        var lido = LerValorTime(par[lado]);
                        if (lido != null) brutos[lado][chave] = lido.Value;
                    }
                }
            }

            var lista = new List<object>();

            for (var lado = 0; lado < 2; lado++)
            {
                if (brutos[lado].Count == 0) continue;

                var idApi = lado == 0 ? jogo.TimeCasa!.IdApi : jogo.TimeVisitante!.IdApi;
                var convertidos = new Dictionary<string, string>();

                foreach (var (fotmob, apiFootball, pct) in ChavesTime)
                {
                    if (!brutos[lado].TryGetValue(fotmob, out var v) || v.Valor is not double n) continue;
                    convertidos[apiFootball] = pct
                        ? $"{Math.Round(n).ToString("0", CultureInfo.InvariantCulture)}%"
                        : n.ToString("0.##", CultureInfo.InvariantCulture);
                }

                // Passes certos vêm como "291 (80%)": LerValorTime já separou o 291 no
                // Valor. O percentual é recalculado dos absolutos em vez de aproveitado,
                // para ficar igual ao que a api-football e a ESPN gravam.
                if (brutos[lado].TryGetValue("accurate_passes", out var certos) && certos.Valor is double c)
                {
                    convertidos["Passes accurate"] = c.ToString("0.##", CultureInfo.InvariantCulture);

                    if (brutos[lado].TryGetValue("passes", out var tot) && tot.Valor is double t && t > 0)
                        convertidos["Passes %"] =
                            $"{Math.Round(c / t * 100).ToString("0", CultureInfo.InvariantCulture)}%";
                }

                // Cartões saem dos eventos e não da tabela: yellow_cards/red_cards
                // existem no FotMob, mas quando a partida não teve nenhum a linha some
                // do payload em vez de vir zerada — e "0 cartões" é informação, não
                // ausência de informação.
                var (amarelos, vermelhos) = ContarCartoesDoTime(raiz, ehCasa: lado == 0);
                convertidos["Yellow Cards"] = amarelos.ToString(CultureInfo.InvariantCulture);
                convertidos["Red Cards"] = vermelhos.ToString(CultureInfo.InvariantCulture);

                if (convertidos.Count > 0)
                    lista.Add(new { TimeId = idApi, Stats = convertidos });
            }

            if (lista.Count == 0) return false;

            jogo.EstatisticasJson = JsonSerializer.Serialize(lista);
            await Task.CompletedTask;
            return true;
        }

        /// <summary>
        /// Lê uma célula da tabela de estatísticas de time. O FotMob mistura três
        /// formatos na mesma estrutura: número puro (21), fração com percentual como
        /// texto ("291 (80%)") e null nas linhas de cabeçalho.
        /// </summary>
        internal static (double? Valor, double? Total)? LerValorTime(JsonElement celula) => celula.ValueKind switch
        {
            JsonValueKind.Number => (celula.GetDouble(), null),
            JsonValueKind.String => LerTexto(celula.GetString()),
            _ => null,
        };

        private static (double? Valor, double? Total)? LerTexto(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return null;

            // "291 (80%)" -> 291. O percentual entre parênteses é descartado: ele vem
            // arredondado e o valor absoluto permite recalcular com mais precisão.
            var corte = texto.IndexOf(' ');
            var cabeca = corte > 0 ? texto[..corte] : texto;

            return double.TryParse(cabeca, NumberStyles.Float, CultureInfo.InvariantCulture, out var n)
                ? (n, null) : null;
        }

        // ── Estatísticas de jogador ───────────────────────────────────────────

        // Chaves do FotMob por jogador. O nome ao lado é só documentação: o casamento é
        // pela key, que é id interno e não muda com o idioma.
        private const string KeyMinutos       = "minutes_played";
        private const string KeyGols          = "goals";
        private const string KeyAssistencias  = "assists";
        private const string KeyFinalizacoes  = "total_shots";
        private const string KeyNoGol         = "ShotsOnTarget";
        private const string KeyOffsides      = "Offsides";
        private const string KeyPasses        = "accurate_passes";      // value = certos, total = tentados
        private const string KeyChancesCriadas = "chances_created";     // = passes-chave da api-football
        private const string KeyDesarmes      = "matchstats.headers.tackles";
        private const string KeyBloqueios     = "shot_blocks";
        private const string KeyInterceptacoes = "interceptions";
        private const string KeyDuelosGanhos  = "duel_won";
        private const string KeyDuelosPerdidos = "duel_lost";
        private const string KeyDribles       = "dribbles_succeeded";   // value = certos, total = tentados
        private const string KeyDriblado      = "dribbled_past";
        private const string KeyFaltaSofrida  = "was_fouled";
        private const string KeyFaltaCometida = "fouls";
        private const string KeyDefesas       = "saves";
        private const string KeyGolsSofridos  = "goals_conceded";
        private const string KeyPenaltiSofrido = "penalties_won";
        private const string KeyPenaltiCometido = "conceded_penalties";

        private async Task<int> GravarEstatisticasJogadoresAsync(
            FutebolContext context, Jogo jogo, JsonElement raiz, CancellationToken ct)
        {
            // Dado da api-football é mais completo e nunca é substituído. O que a ESPN
            // gravou PODE ser substituído: o FotMob cobre tudo que ela cobre e ainda
            // acrescenta passes, desarmes, duelos e dribles, então a troca só melhora a
            // linha. O que o próprio FotMob gravou antes também pode ser refeito.
            var existentes = await context.EstatisticasJogador
                .Where(e => e.JogoId == jogo.Id)
                .ToListAsync(ct);
            if (existentes.Any(e => e.Fonte != FonteEstatistica.Espn && e.Fonte != FonteEstatistica.FotMob))
                return 0;

            if (!raiz.TryGetProperty("content", out var conteudo) ||
                !conteudo.TryGetProperty("playerStats", out var porJogador) ||
                porJogador.ValueKind != JsonValueKind.Object) return 0;

            // Lado e titularidade não estão em playerStats — vêm da escalação, que é
            // onde o FotMob separa starters de subs.
            var (ladoPorFotMobId, titularPorFotMobId) = MapearElenco(raiz, jogo);
            if (ladoPorFotMobId.Count == 0) return 0;

            // Candidatos restritos aos escalados do jogo — NomeJogadorHelper.Corresponde
            // tolera abreviação e só é seguro dentro de um conjunto pequeno.
            var escaladosPorLado = await context.Escalacoes
                .Where(e => e.JogoId == jogo.Id && e.JogadorId != null)
                .Select(e => new { e.IsTimeCasa, e.Jogador!.Id, e.Jogador.Nome, e.Jogador.NumeroCamisa })
                .Distinct()
                .ToListAsync(ct);

            var cartoes = MapearCartoes(raiz);

            // Vínculo jogador-nosso -> jogador-FotMob descoberto neste jogo. O casamento
            // já é feito aqui de qualquer forma (por nome e camisa, dentro do elenco da
            // partida); guardá-lo é o que permite abrir as estatísticas avançadas depois
            // sem ter de adivinhar o jogador por busca de nome, que não funciona.
            // Ver Jogador.IdFotMob.
            var vinculos = new Dictionary<int, long>();

            // As linhas novas são montadas ANTES de mexer no que já existe, para que uma
            // regravação que não casa ninguém não deixe o jogo sem estatística alguma.
            var novas = new List<EstatisticaJogador>();
            var jaGravados = new HashSet<int>();

            foreach (var entrada in porJogador.EnumerateObject())
            {
                if (!long.TryParse(entrada.Name, out var idFotMob)) continue;
                if (!ladoPorFotMobId.TryGetValue(idFotMob, out var ehCasa)) continue;

                var atleta = entrada.Value;
                var nome = atleta.TryGetProperty("name", out var n) ? n.GetString() : null;
                if (string.IsNullOrWhiteSpace(nome)) continue;

                var valores = LerEstatisticasDoJogador(atleta);
                if (valores.Count == 0) continue;

                // Sem minutos em campo o jogador não entrou. Gravar linha para ele
                // criaria uma partida disputada que não existiu, poluindo média de
                // jogos, ranking e o bônus de "não sofreu gol".
                var minutos = Inteiro(valores, KeyMinutos);
                if (minutos is null or <= 0) continue;

                var camisa = atleta.TryGetProperty("shirtNumber", out var c)
                    ? (c.ValueKind == JsonValueKind.String
                        ? (int.TryParse(c.GetString(), out var ci) ? ci : (int?)null)
                        : (c.ValueKind == JsonValueKind.Number ? c.GetInt32() : null))
                    : null;

                var candidatos = escaladosPorLado.Where(e => e.IsTimeCasa == ehCasa).ToList();
                if (candidatos.Count == 0) continue;

                var casaram = candidatos.Where(x => NomeJogadorHelper.Corresponde(x.Nome, nome)).ToList();

                // O nome falha mais do que parece, e em clube árabe falha ainda mais: a
                // transliteração muda de fonte para fonte ("Mohammed" x "Mohamed"). A
                // camisa resolve esse caso e é estável dentro da partida.
                if (casaram.Count == 0 && camisa != null)
                    casaram = candidatos.Where(x => x.NumeroCamisa == camisa).ToList();

                if (casaram.Count == 0) continue;

                // Mais de um candidato é duplicata no cadastro, não ambiguidade real.
                var escolhido = casaram.Count == 1
                    ? casaram[0]
                    : casaram.OrderByDescending(x => NomeJogadorHelper.Normalizar(x.Nome) == NomeJogadorHelper.Normalizar(nome))
                             .ThenByDescending(x => camisa != null && x.NumeroCamisa == camisa)
                             .ThenBy(x => x.Id)
                             .First();

                // Dois jogadores do FotMob casando com o mesmo cadastrado geraria duas
                // linhas para a mesma pessoa no mesmo jogo. Fica a primeira.
                if (!jaGravados.Add(escolhido.Id)) continue;

                vinculos[escolhido.Id] = idFotMob;

                // O FotMob publica goals_conceded só para o goleiro (ao contrário da
                // ESPN, que atribui o gol a todo mundo que estava em campo), mas a
                // checagem fica de pé mesmo assim: o critério "gol sofrido" pesa -1,0 e
                // a família Goleiro do rating lê esse campo.
                var goleiro = atleta.TryGetProperty("isGoalkeeper", out var g) &&
                              g.ValueKind == JsonValueKind.True;

                var duelosGanhos = Inteiro(valores, KeyDuelosGanhos) ?? 0;
                var duelosPerdidos = Inteiro(valores, KeyDuelosPerdidos) ?? 0;

                var (amarelos, vermelhos) = cartoes.GetValueOrDefault(idFotMob);

                novas.Add(new EstatisticaJogador
                {
                    JogoId            = jogo.Id,
                    JogadorId         = escolhido.Id,
                    Fonte             = FonteEstatistica.FotMob,
                    Minutos           = minutos,

                    // Rating fica null de propósito — ver o comentário de classe sobre
                    // métricas proprietárias. A tela mostra "—", que é honesto.
                    Rating            = null,

                    Gols              = Inteiro(valores, KeyGols) ?? 0,
                    Assistencias      = Inteiro(valores, KeyAssistencias) ?? 0,
                    FinalizacoesTotal = Inteiro(valores, KeyFinalizacoes) ?? 0,
                    FinalizacoesNoGol = Inteiro(valores, KeyNoGol) ?? 0,
                    Offsides          = Inteiro(valores, KeyOffsides) ?? 0,

                    PassesCertos      = Inteiro(valores, KeyPasses) ?? 0,
                    PassesTotal       = Total(valores, KeyPasses) ?? 0,
                    PassesChave       = Inteiro(valores, KeyChancesCriadas) ?? 0,

                    Desarmes          = Inteiro(valores, KeyDesarmes) ?? 0,
                    Bloqueios         = Inteiro(valores, KeyBloqueios) ?? 0,
                    Interceptacoes    = Inteiro(valores, KeyInterceptacoes) ?? 0,

                    DuelosVencidos    = duelosGanhos,
                    DuelosTotal       = duelosGanhos + duelosPerdidos,

                    DriblesCertos     = Inteiro(valores, KeyDribles) ?? 0,
                    DriblesTentados   = Total(valores, KeyDribles) ?? 0,
                    DriblesSofridos   = Inteiro(valores, KeyDriblado) ?? 0,

                    FaltasSofridas    = Inteiro(valores, KeyFaltaSofrida) ?? 0,
                    FaltasCometidas   = Inteiro(valores, KeyFaltaCometida) ?? 0,

                    CartoesAmarelos   = amarelos,
                    CartoesVermelhos  = vermelhos,

                    Defesas           = goleiro ? Inteiro(valores, KeyDefesas) ?? 0 : 0,
                    GolsSofridos      = goleiro ? Inteiro(valores, KeyGolsSofridos) ?? 0 : 0,

                    PenaltiSofrido    = Inteiro(valores, KeyPenaltiSofrido) ?? 0,
                    PenaltiCometido   = Inteiro(valores, KeyPenaltiCometido) ?? 0,

                    EntrouDoBanco     = !titularPorFotMobId.GetValueOrDefault(idFotMob, true),
                });
            }

            // Nada casou: preserva o que já estava lá em vez de zerar o jogo.
            if (novas.Count == 0) return 0;

            await GravarVinculosAsync(context, vinculos, ct);

            // Troca atômica — o SaveChanges de ImportarAsync grava remoção e inserção na
            // mesma transação.
            // As estatísticas alimentam a nota automática de quem não avaliou o jogo à
            // mão, então a eleição do craque de TODOS os analistas ficou velha. Derruba
            // aqui e deixa cada um refazer a dele na próxima leitura — recalcular usuário
            // por usuário dentro da importação sairia caro. Vai junto no SaveChanges de
            // quem chamou, na mesma transação da troca das estatísticas.
            context.CraquesDaPartida.RemoveRange(
                await context.CraquesDaPartida.Where(c => c.JogoId == jogo.Id).ToListAsync(ct));

            context.EstatisticasJogador.RemoveRange(existentes);
            context.EstatisticasJogador.AddRange(novas);
            return novas.Count;
        }

        /// <summary>
        /// Guarda em Jogador.IdFotMob os vínculos que o casamento deste jogo revelou.
        ///
        /// Só preenche quem está sem id — nunca sobrescreve. Um vínculo já gravado veio
        /// de outra partida e foi conferido do mesmo jeito; trocá-lo por causa de um
        /// casamento por camisa num jogo qualquer só criaria oscilação. Se um dia o
        /// vínculo estiver errado, o certo é corrigir a origem, não deixar a última
        /// importação vencer.
        ///
        /// Não chama SaveChanges: entra na mesma transação da importação.
        /// </summary>
        private static async Task GravarVinculosAsync(
            FutebolContext context, Dictionary<int, long> vinculos, CancellationToken ct)
        {
            if (vinculos.Count == 0) return;

            var ids = vinculos.Keys.ToList();
            var semVinculo = await context.Jogadores
                .Where(j => ids.Contains(j.Id) && j.IdFotMob == null)
                .ToListAsync(ct);

            foreach (var jogador in semVinculo)
                jogador.IdFotMob = vinculos[jogador.Id];
        }

        /// <summary>
        /// Achata os grupos de estatística de um jogador ("Top stats", "Attack",
        /// "Defense", "Duels") num dicionário key -> (valor, total). O mesmo campo pode
        /// aparecer em mais de um grupo com o mesmo conteúdo, então repetição é
        /// esperada e a primeira ocorrência vence.
        /// </summary>
        internal static Dictionary<string, (double? Valor, double? Total)> LerEstatisticasDoJogador(JsonElement atleta)
        {
            var mapa = new Dictionary<string, (double?, double?)>(StringComparer.Ordinal);

            if (!atleta.TryGetProperty("stats", out var grupos) || grupos.ValueKind != JsonValueKind.Array)
                return mapa;

            foreach (var grupo in grupos.EnumerateArray())
            {
                if (!grupo.TryGetProperty("stats", out var linhas) || linhas.ValueKind != JsonValueKind.Object)
                    continue;

                foreach (var linha in linhas.EnumerateObject())
                {
                    var chave = linha.Value.TryGetProperty("key", out var k) ? k.GetString() : null;
                    if (chave == null || mapa.ContainsKey(chave)) continue;

                    if (!linha.Value.TryGetProperty("stat", out var stat)) continue;

                    double? valor = stat.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.Number
                        ? v.GetDouble() : null;
                    double? total = stat.TryGetProperty("total", out var t) && t.ValueKind == JsonValueKind.Number
                        ? t.GetDouble() : null;

                    mapa[chave] = (valor, total);
                }
            }

            return mapa;
        }

        private static int? Inteiro(Dictionary<string, (double? Valor, double? Total)> mapa, string chave) =>
            mapa.TryGetValue(chave, out var v) && v.Valor is double d ? (int)Math.Round(d) : null;

        private static int? Total(Dictionary<string, (double? Valor, double? Total)> mapa, string chave) =>
            mapa.TryGetValue(chave, out var v) && v.Total is double d ? (int)Math.Round(d) : null;

        /// <summary>
        /// Lado (casa/visitante) e titularidade de cada jogador, pelo id do FotMob.
        /// playerStats é indexado por esse id e não diz de que time o jogador é.
        /// </summary>
        private static (Dictionary<long, bool> Lado, Dictionary<long, bool> Titular) MapearElenco(
            JsonElement raiz, Jogo jogo)
        {
            var lado = new Dictionary<long, bool>();
            var titular = new Dictionary<long, bool>();

            if (!raiz.TryGetProperty("content", out var conteudo) ||
                !conteudo.TryGetProperty("lineup", out var escalacao)) return (lado, titular);

            foreach (var (propriedade, ehCasa) in new[] { ("homeTeam", true), ("awayTeam", false) })
            {
                if (!escalacao.TryGetProperty(propriedade, out var time)) continue;

                // Confere o nome antes de confiar em qual lado é qual — mesmo motivo da
                // conferência em GravarEstatisticasTimeAsync.
                var nome = time.TryGetProperty("name", out var n) ? n.GetString() : null;
                var nosso = ehCasa ? jogo.TimeCasa?.Nome : jogo.TimeVisitante?.Nome;
                if (!TimeNomeMatcher.SaoMesmoTime(nosso, nome)) continue;

                foreach (var (grupo, ehTitular) in new[] { ("starters", true), ("subs", false) })
                {
                    if (!time.TryGetProperty(grupo, out var jogadores) ||
                        jogadores.ValueKind != JsonValueKind.Array) continue;

                    foreach (var j in jogadores.EnumerateArray())
                    {
                        if (!j.TryGetProperty("id", out var id)) continue;
                        var idFotMob = id.ValueKind == JsonValueKind.Number ? id.GetInt64()
                            : long.TryParse(id.GetString(), out var parsed) ? parsed : 0;
                        if (idFotMob == 0) continue;

                        lado[idFotMob] = ehCasa;
                        titular[idFotMob] = ehTitular;
                    }
                }
            }

            return (lado, titular);
        }

        /// <summary>
        /// Cartões por jogador, tirados da linha do tempo da partida. Não vêm em
        /// playerStats — lá o campo simplesmente não existe.
        /// </summary>
        internal static Dictionary<long, (int Amarelos, int Vermelhos)> MapearCartoes(JsonElement raiz)
        {
            var mapa = new Dictionary<long, (int, int)>();

            foreach (var (idFotMob, cartao, _) in EnumerarCartoes(raiz))
            {
                if (idFotMob == 0) continue;
                var atual = mapa.GetValueOrDefault(idFotMob);
                mapa[idFotMob] = cartao switch
                {
                    "Yellow" => (atual.Item1 + 1, atual.Item2),
                    "Red" or "YellowRed" => (atual.Item1, atual.Item2 + 1),
                    _ => atual,
                };
            }

            return mapa;
        }

        internal static (int Amarelos, int Vermelhos) ContarCartoesDoTime(JsonElement raiz, bool ehCasa)
        {
            int amarelos = 0, vermelhos = 0;

            foreach (var (_, cartao, deCasa) in EnumerarCartoes(raiz))
            {
                if (deCasa != ehCasa) continue;
                if (cartao == "Yellow") amarelos++;
                else if (cartao is "Red" or "YellowRed") vermelhos++;
            }

            return (amarelos, vermelhos);
        }

        /// <summary>
        /// Eventos de cartão da partida: (id do jogador no FotMob, cor, é do mandante).
        /// O id vem 0 para jogador que o FotMob não tem cadastrado — acontece com
        /// técnico e com reserva de clube pequeno, e nesse caso o cartão ainda conta
        /// para o time.
        /// </summary>
        private static IEnumerable<(long IdFotMob, string? Cartao, bool DeCasa)> EnumerarCartoes(JsonElement raiz)
        {
            if (!raiz.TryGetProperty("content", out var conteudo) ||
                !conteudo.TryGetProperty("matchFacts", out var fatos) ||
                !fatos.TryGetProperty("events", out var bloco) ||
                !bloco.TryGetProperty("events", out var eventos) ||
                eventos.ValueKind != JsonValueKind.Array) yield break;

            foreach (var e in eventos.EnumerateArray())
            {
                if (!e.TryGetProperty("type", out var tipo) || tipo.GetString() != "Card") continue;
                if (!e.TryGetProperty("card", out var cartao)) continue;
                if (!e.TryGetProperty("isHome", out var casa) ||
                    casa.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) continue;

                var id = e.TryGetProperty("playerId", out var p) && p.ValueKind == JsonValueKind.Number
                    ? p.GetInt64() : 0;

                yield return (id, cartao.GetString(), casa.GetBoolean());
            }
        }

        // ── Perfil do jogador ─────────────────────────────────────────────────

        /// <summary>
        /// Perfil completo do jogador (posições, carreira, títulos, jogos recentes e o
        /// índice statSeasons). Chamado só quando alguém abre as estatísticas avançadas
        /// — ver FotMobPerfilService, que é quem interpreta isto.
        ///
        /// Cache de 6h: o perfil muda pouco (valor de mercado, um jogo novo) e quem abre
        /// a tela duas vezes seguidas não deve gerar duas visitas ao FotMob.
        /// </summary>
        public Task<JsonDocument?> BuscarPerfilJogadorAsync(long idFotMob, CancellationToken ct = default) =>
            BuscarJsonAsync($"playerData?id={idFotMob}", TimeSpan.FromHours(6), ct);

        /// <summary>
        /// Desempenho do jogador numa temporada/competição.
        /// </summary>
        /// <param name="temporadaId">
        /// O entryId de statSeasons ("1-1"), e não o ano: essa rota só aceita esse
        /// formato e devolve 500 sem ele.
        /// </param>
        public Task<JsonDocument?> BuscarEstatisticasJogadorAsync(
            long idFotMob, string temporadaId, CancellationToken ct = default) =>
            BuscarJsonAsync(
                $"playerStats?playerId={idFotMob}&seasonId={Uri.EscapeDataString(temporadaId)}",
                TimeSpan.FromHours(6), ct);

        /// <summary>
        /// Candidatos para o vínculo manual de um jogador (ver Jogador.IdFotMob).
        ///
        /// Existe porque o vínculo automático não alcança todo mundo: ele nasce ao
        /// importar um jogo do FotMob, e quem só tem partidas cobertas pela
        /// api-football nunca passa por lá. Este é o caminho para esses.
        ///
        /// É uma LISTA para escolher, nunca uma escolha automática, e a razão está no
        /// próprio dado: o nome completo do nosso cadastro costuma não achar nada
        /// ("Edmilson Junior Paulo da Silva" devolve zero) enquanto o nome curto devolve
        /// homônimos ("Edmilson" traz cinco). Deixar o código decidir aqui seria vincular
        /// o jogador errado em silêncio — o pior desfecho possível para esta função.
        /// </summary>
        public async Task<IReadOnlyList<(long Id, string Nome, string? Time)>> BuscarJogadoresAsync(
            string termo, CancellationToken ct = default)
        {
            var achados = new List<(long, string, string?)>();
            if (string.IsNullOrWhiteSpace(termo)) return achados;

            using var doc = await BuscarJsonAsync(
                $"search/suggest?term={Uri.EscapeDataString(termo.Trim())}&hits=10&lang=pt-BR",
                TimeSpan.FromHours(1), ct);
            if (doc == null) return achados;

            // A resposta vem como lista de blocos ("Melhores resultados", "Jogadores"),
            // e o mesmo jogador aparece em mais de um — daí o controle de repetidos.
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return achados;

            var vistos = new HashSet<long>();

            foreach (var bloco in doc.RootElement.EnumerateArray())
            {
                if (!bloco.TryGetProperty("suggestions", out var sugestoes) ||
                    sugestoes.ValueKind != JsonValueKind.Array) continue;

                foreach (var s in sugestoes.EnumerateArray())
                {
                    if (!s.TryGetProperty("type", out var tipo) || tipo.GetString() != "player") continue;

                    // Técnico entra na mesma busca com type "player"; não é jogador.
                    if (s.TryGetProperty("isCoach", out var coach) && coach.ValueKind == JsonValueKind.True)
                        continue;

                    if (!s.TryGetProperty("id", out var idEl) ||
                        !long.TryParse(idEl.GetString(), out var id) || !vistos.Add(id)) continue;

                    var nome = s.TryGetProperty("name", out var n) ? n.GetString() : null;
                    if (string.IsNullOrWhiteSpace(nome)) continue;

                    achados.Add((id, nome,
                        s.TryGetProperty("teamName", out var t) ? t.GetString() : null));
                }
            }

            return achados;
        }

        // ── HTTP ──────────────────────────────────────────────────────────────

        /// <summary>
        /// GET com cache, uma chamada por vez e intervalo mínimo entre elas. Devolve
        /// null (sem estourar exceção) em qualquer falha: importação de lote não pode
        /// morrer porque um jogo deu 404.
        /// </summary>
        private async Task<JsonDocument?> BuscarJsonAsync(string url, TimeSpan validade, CancellationToken ct)
        {
            var chave = "fotmob:" + url;
            if (_cache.TryGetValue<string>(chave, out var cacheado) && cacheado != null)
                return JsonDocument.Parse(cacheado);

            var corpo = await BaixarComEsperaAsync(url, ct);
            if (corpo == null) return null;

            _cache.Set(chave, corpo, new MemoryCacheEntryOptions
            {
                Size = corpo.Length,
                AbsoluteExpirationRelativeToNow = validade,
            });

            return JsonDocument.Parse(corpo);
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

                        // 429/503: o host está pedindo para desacelerar. Respeita o
                        // Retry-After quando vem, senão espera progressivamente.
                        if (resp.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable)
                        {
                            var espera = resp.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(5 * tentativa);
                            _logger.LogWarning("[FotMob] {Status} em {Url} — aguardando {Espera}s.",
                                (int)resp.StatusCode, url, espera.TotalSeconds);
                            await Task.Delay(espera, ct);
                            continue;
                        }

                        // 403 é o sinal de que o FotMob voltou a exigir chamada
                        // assinada. Não há o que tentar: a fonte deixou de estar
                        // disponível e o log precisa deixar isso legível.
                        if (resp.StatusCode == HttpStatusCode.Forbidden)
                        {
                            _logger.LogWarning(
                                "[FotMob] 403 em {Url} — a API passou a exigir autenticação. {Nota}",
                                url, RiscoDeExposicao);
                            return null;
                        }

                        // 400/404 = liga ou partida que não existe lá; repetir não ajuda.
                        _logger.LogInformation("[FotMob] {Status} em {Url}.", (int)resp.StatusCode, url);
                        return null;
                    }
                    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
                    {
                        _ultimaChamada = DateTimeOffset.UtcNow;
                        _logger.LogWarning(ex, "[FotMob] Falha de rede em {Url} (tentativa {N}).", url, tentativa);
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
