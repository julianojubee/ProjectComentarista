using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ControleFutebolWeb.Services
{
    public record ResultadoOgol(bool Ok, string Mensagem, string? IdJogo = null);

    /// <summary>
    /// Uma partida como o calendário da edição no ogol a descreve. Só o que serve para
    /// localizar o jogo que já existe no nosso banco — o conteúdo (escalação, lances,
    /// estatística) sai da página da partida, não daqui.
    /// </summary>
    public record PartidaOgol(
        string Id, DateTime DataUtc, string Casa, string Visitante, string? Fase, bool Finalizado);

    /// <summary>
    /// DESATIVADA. Raspagem do ogol.com.br (a versão brasileira da plataforma zerozero)
    /// para o Brasileirão Feminino.
    ///
    /// Não está registrada em Program.cs e ninguém a chama: o código fica aqui porque
    /// funciona e a pesquisa custou caro, não porque está em uso. O que a derrubou não
    /// foi a raspagem — foi ONDE ela roda. Em 01/09/2026, o mesmo pedido, com os mesmos
    /// cabeçalhos, voltou 200 do IP residencial do analista e 403 do IP do VPS de
    /// produção: o Cloudflare barra a faixa de datacenter, não a requisição. As saídas
    /// (proxy residencial pago, WARP, liberação do IP junto ao site) não se pagam por
    /// uma competição só.
    ///
    /// Para religar são três passos: registrar OgolService, OgolEscalacaoService e
    /// OgolEventosService em Program.cs (com o handler de compressão e o proxy opcional
    /// de Ogol:Proxy); devolver o desvio para o ogol em JogosController, ao lado do que
    /// já existe para a FIFA (EhJogoDaFifaAsync/BuscarDadosDaFifaAsync); e garantir que
    /// a máquina que roda a importação seja atendida pelo site. O de-para de edições
    /// continua em wwwroot/data/ligas-ogol.json e os parsers continuam cobertos por
    /// ControleFutebolWeb.Tests/Services/OgolParsersTests.cs.
    ///
    /// Existe porque nesta competição as três fontes de dados do sistema chegam vazias:
    /// a api-football traz tabela e placar mas não escalação nem estatística, a ESPN não
    /// cataloga a liga (ver o comentário em wwwroot/data/ligas-espn.json) e o FotMob
    /// também não. O ogol publica o jogo inteiro — XI, banco, quem entrou e saiu com o
    /// minuto, cartões, gols e as estatísticas de time.
    ///
    /// O que ela NÃO é: uma fonte de calendário. Os jogos continuam nascendo da
    /// api-football (Competicao.LinkTransfermarket = "apifoot:74:2026"), e este serviço
    /// só completa o que falta neles. Por isso não há link "ogol:" na competição: o
    /// gatilho é o de-para wwwroot/data/ligas-ogol.json, que hoje tem uma liga só. Liga
    /// fora do arquivo nunca é consultada — é o que mantém a raspagem restrita ao
    /// campeonato feminino.
    ///
    /// Não existe API: o site responde HTML renderizado no servidor e as únicas
    /// requisições XHR da página são de anúncio. O robots.txt libera tudo menos um
    /// arquivo, mas o Cloudflare devolve 403 para requisição sem cara de navegador —
    /// daí os cabeçalhos no construtor. Para não virar abuso, todas as chamadas passam
    /// pela <see cref="_porta"/> (uma por vez, com intervalo mínimo) e por cache, no
    /// mesmo molde do FifaService.
    ///
    /// De ONDE a requisição sai importa tanto quanto o que ela leva: em 01/09/2026 o
    /// mesmo pedido, com os mesmos cabeçalhos, voltou 200 do IP residencial do analista
    /// e 403 do IP do VPS de produção — o Cloudflare barra a faixa, não a requisição.
    /// Por isso o cliente aceita um proxy opcional em Ogol:Proxy (ver Program.cs); sem
    /// ele configurado, num servidor bloqueado a fonte falha por completo, e a mensagem
    /// da tela de logs diz exatamente isso em vez de culpar a raspagem.
    /// </summary>
    public class OgolService
    {
        private const string Base = "https://www.ogol.com.br/";

        // Uma requisição por vez para o host, com intervalo mínimo entre elas.
        // Estático porque o serviço é transiente (AddHttpClient<T>) e o que precisa ser
        // serializado é o acesso ao host, não a instância. O intervalo é maior que o do
        // FifaService de propósito: ali é uma API pública, aqui é o HTML de um site com
        // Cloudflare na frente.
        private static readonly SemaphoreSlim _porta = new(1, 1);
        private static DateTimeOffset _ultimaChamada = DateTimeOffset.MinValue;
        private static readonly TimeSpan IntervaloMinimo = TimeSpan.FromMilliseconds(1200);

        // O calendário tem 50 linhas por página. O teto existe só para uma mudança de
        // formato do site não virar laço infinito — a parada normal é a página vazia.
        private const int MaximoPaginasCalendario = 10;

        // Fuso do horário que a tabela do calendário mostra. O ogol.com.br publica em
        // horário de Brasília (conferido contra o slug da partida, que é a data em UTC:
        // "2026-02-12 21:00" na tabela vira /jogo/2026-02-13-.../12005099). Sem
        // dependência de fuso do servidor: Jogo.Data é UTC e o Brasil não tem mais
        // horário de verão desde 2019, então a diferença é fixa.
        private static readonly TimeSpan FusoBrasilia = TimeSpan.FromHours(-3);

        // Quanto a data do ogol pode divergir da que a api-football gravou no jogo.
        // Adiamento de algumas horas e diferença de arredondamento cabem aqui; dois
        // confrontos do mesmo par (ida e volta do mata-mata) nunca acontecem dentro
        // desta janela, então ela não confunde um com o outro.
        private static readonly TimeSpan JanelaDeCasamento = TimeSpan.FromHours(36);

        private readonly HttpClient _http;
        private readonly ILogger<OgolService> _logger;
        private readonly IMemoryCache _cache;
        private readonly IWebHostEnvironment _env;

        /// <summary>
        /// Como a última requisição falhou ("HTTP 403", "tempo esgotado"). Existe para a
        /// mensagem da tela de logs poder dizer O QUE o site respondeu: "não respondeu o
        /// calendário" não distingue bloqueio do Cloudflare de queda do site nem de
        /// timeout, e as três pedem providências diferentes.
        /// </summary>
        private string? _ultimaFalhaHttp;

        public OgolService(
            HttpClient http, ILogger<OgolService> logger, IMemoryCache cache, IWebHostEnvironment env)
        {
            _http = http;
            _http.BaseAddress = new Uri(Base);
            _http.Timeout = TimeSpan.FromSeconds(30);
            // Sem isto o Cloudflare responde 403 com o desafio "Just a moment...".
            //
            // O conjunto inteiro está aqui, e não só o User-Agent, porque o que ele
            // avalia é o pedido COMO UM TODO: navegador nenhum manda um GET de página
            // sem Accept-Language, sem Sec-Fetch-* e sem anunciar compressão, e a
            // ausência desses cabeçalhos é justamente o que separa um cliente de
            // servidor de uma aba do Chrome. De um IP residencial o UA sozinho passava;
            // de um IP de datacenter (o servidor de produção é um VPS) a régua é outra.
            // A compressão vem do handler — ver AddHttpClient em Program.cs.
            _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
            _http.DefaultRequestHeaders.TryAddWithoutValidation("Accept",
                "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
            _http.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "pt-BR,pt;q=0.9,en-US;q=0.8");
            _http.DefaultRequestHeaders.TryAddWithoutValidation("Upgrade-Insecure-Requests", "1");
            _http.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Dest", "document");
            _http.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Mode", "navigate");
            _http.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Site", "none");
            _http.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-User", "?1");
            _logger = logger;
            _cache = cache;
            _env = env;
        }

        // ── Cobertura ────────────────────────────────────────────────────────

        /// <summary>
        /// O id da edição no ogol para esta liga da api-football nesta temporada, ou
        /// null quando a competição não é coberta. É o único portão da fonte: enquanto
        /// wwwroot/data/ligas-ogol.json tiver só a liga 74, só o Brasileirão Feminino
        /// chega ao site.
        /// </summary>
        public string? EdicaoDaLiga(int? idApiLiga, int temporada)
        {
            if (idApiLiga is not > 0 || temporada <= 0) return null;

            return MapaLigas()
                .GetValueOrDefault(idApiLiga.Value.ToString(CultureInfo.InvariantCulture))
                ?.GetValueOrDefault(temporada.ToString(CultureInfo.InvariantCulture));
        }

        public bool TemCobertura(int? idApiLiga, int temporada) =>
            EdicaoDaLiga(idApiLiga, temporada) != null;

        /// <summary>idApi da liga → temporada → id da edição no ogol.</summary>
        private Dictionary<string, Dictionary<string, string>> MapaLigas() =>
            _cache.GetOrCreate("ogol:ligas", e =>
            {
                e.Size = 4096;
                e.SlidingExpiration = TimeSpan.FromHours(6);

                var caminho = Path.Combine(_env.WebRootPath, "data", "ligas-ogol.json");
                if (!File.Exists(caminho)) return new Dictionary<string, Dictionary<string, string>>();

                var mapa = new Dictionary<string, Dictionary<string, string>>();
                try
                {
                    var bruto = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                        File.ReadAllText(caminho)) ?? new();

                    foreach (var (chave, valor) in bruto)
                    {
                        if (chave.StartsWith('_') || valor.ValueKind != JsonValueKind.Object) continue;
                        if (!valor.TryGetProperty("edicoes", out var edicoes) ||
                            edicoes.ValueKind != JsonValueKind.Object) continue;

                        var porTemporada = new Dictionary<string, string>();
                        foreach (var ano in edicoes.EnumerateObject())
                            if (ano.Value.GetString() is { Length: > 0 } id)
                                porTemporada[ano.Name] = id;

                        if (porTemporada.Count > 0) mapa[chave] = porTemporada;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[Ogol] Não consegui ler wwwroot/data/ligas-ogol.json.");
                }

                return mapa;
            }) ?? new Dictionary<string, Dictionary<string, string>>();

        // ── Localização da partida ───────────────────────────────────────────

        /// <summary>
        /// Abre a página da partida no ogol correspondente ao nosso jogo. Devolve o jogo
        /// carregado junto porque quem chama precisa dos dois times para saber de quem é
        /// cada coluna da escalação.
        ///
        /// A partida é localizada pelo calendário da edição (data + nome dos dois times),
        /// e não por um id guardado no jogo: Jogo.LinkDetalhes já é da api-football
        /// ("apifoot:1522441") e é por ele que a reimportação de lá volta à partida —
        /// sobrescrevê-lo quebraria aquele caminho. O calendário é uma consulta só para a
        /// competição inteira e fica em cache, então resolver na hora sai barato.
        /// </summary>
        internal async Task<(Jogo? Jogo, HtmlDocument? Doc, string? IdJogo, ResultadoOgol? Erro)>
            AbrirPartidaAsync(FutebolContext context, int jogoId, CancellationToken ct)
        {
            var jogo = await context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Include(j => j.Competicao)
                .FirstOrDefaultAsync(j => j.Id == jogoId, ct);

            ResultadoOgol Falha(string msg, string? id = null) => new(false, msg, id);

            if (jogo == null) return (null, null, null, Falha("Jogo não encontrado."));
            if (jogo.Data == null)
                return (jogo, null, null, Falha("Jogo sem data — não dá para localizar no ogol."));

            var edicao = EdicaoDaLiga(jogo.Competicao?.IdApi, jogo.Temporada);
            if (edicao == null)
                return (jogo, null, null, Falha(
                    $"A competição \"{jogo.Competicao?.Nome}\" não tem edição mapeada no ogol " +
                    "(ver wwwroot/data/ligas-ogol.json)."));

            var (partida, motivo) = await ResolverPartidaAsync(jogo, edicao, ct);
            if (partida == null) return (jogo, null, null, Falha(motivo!));

            var doc = await BuscarHtmlAsync($"jogo/-/{partida.Id}", TimeSpan.FromMinutes(10), ct);
            if (doc == null)
                return (jogo, null, partida.Id, Falha(
                    "O ogol não respondeu a página desta partida.", partida.Id));

            return (jogo, doc, partida.Id, null);
        }

        /// <summary>
        /// A ficha "performance" da partida, que é onde o ogol publica a POSIÇÃO de cada
        /// jogadora. A página da partida traz camisa e nome, mas não a posição — e sem
        /// ela o aplicador de escalação joga o XI inteiro na mesma linha do campinho.
        /// Null quando a página não existe ou não respondeu; nesse caso a escalação
        /// entra sem posição, que é o comportamento das outras fontes sem coordenada.
        /// </summary>
        internal Task<HtmlDocument?> AbrirPerformanceAsync(string idJogo, CancellationToken ct) =>
            BuscarHtmlAsync($"jogo/-/{idJogo}/performance", TimeSpan.FromMinutes(10), ct);

        /// <summary>
        /// Procura no calendário da edição a partida que é este jogo: os dois times
        /// casando pelo NOME (TimeNomeMatcher já sabe que "Cruzeiro W" e "Cruzeiro" são
        /// o mesmo time, e que "RB Bragantino W" é o "Red Bull Bragantino") e a data
        /// dentro de <see cref="JanelaDeCasamento"/>.
        ///
        /// O casamento exige o mandante do NOSSO lado como mandante no ogol. Aceitar o
        /// confronto invertido pareceria mais tolerante, mas gravaria a escalação de um
        /// time no lugar do outro em silêncio — e num mata-mata de ida e volta é
        /// justamente o par invertido que existe de verdade, em outra data.
        /// </summary>
        /// <returns>
        /// A partida, ou o motivo da recusa já escrito para a tela de logs. A mensagem
        /// separa os três casos porque a correção de cada um é outra: o calendário que
        /// não respondeu se resolve tentando de novo; o confronto que não existe na
        /// edição costuma ser Jogo.Temporada apontando para o ano errado (e portanto
        /// para a edição errada no de-para); e o confronto achado em outra data é a
        /// remarcação que ainda não chegou aqui — os três apareciam como um "não
        /// bateram" só, que não dizia por onde começar.
        /// </returns>
        private async Task<(PartidaOgol? Partida, string? Motivo)> ResolverPartidaAsync(
            Jogo jogo, string edicao, CancellationToken ct)
        {
            var calendario = await BuscarCalendarioAsync(edicao, ct);
            if (calendario == null || calendario.Count == 0)
                return (null,
                    $"O ogol não respondeu o calendário da edição {edicao}" +
                    (_ultimaFalhaHttp is { } falha ? $" ({falha})" : "") +
                    ". HTTP 403 aqui é o Cloudflare barrando a FAIXA DE IP desta máquina, " +
                    "não a requisição: o mesmo pedido passa de uma rede residencial. " +
                    "Configure uma saída em Ogol:Proxy (ver Program.cs) ou rode a " +
                    "importação de uma máquina que o site atenda.");

            var doConfronto = calendario
                .Where(p => TimeNomeMatcher.SaoMesmoTime(jogo.TimeCasa?.Nome, p.Casa)
                         && TimeNomeMatcher.SaoMesmoTime(jogo.TimeVisitante?.Nome, p.Visitante))
                .ToList();

            var confronto = $"{jogo.TimeCasa?.Nome} x {jogo.TimeVisitante?.Nome}";

            if (doConfronto.Count == 0)
            {
                _logger.LogInformation(
                    "[Ogol] Jogo {Id} ({Confronto}): a edição {Edicao} não tem esse confronto " +
                    "em nenhuma data ({N} partidas conferidas).",
                    jogo.Id, confronto, edicao, calendario.Count);

                return (null,
                    $"O ogol não tem {confronto} na edição {edicao} (conferi as {calendario.Count} " +
                    $"partidas dela). Confira se a temporada do jogo ({jogo.Temporada}) aponta para a " +
                    "edição certa em wwwroot/data/ligas-ogol.json.");
            }

            var maisProxima = doConfronto
                .OrderBy(p => (p.DataUtc - jogo.Data!.Value).Duration())
                .First();

            var distancia = (maisProxima.DataUtc - jogo.Data!.Value).Duration();
            if (distancia > JanelaDeCasamento)
            {
                _logger.LogInformation(
                    "[Ogol] Jogo {Id} ({Confronto}): o ogol marca a partida para {NoOgol:dd/MM/yyyy HH:mm} " +
                    "e aqui ela está em {Nossa:dd/MM/yyyy HH:mm} (UTC).",
                    jogo.Id, confronto, maisProxima.DataUtc, jogo.Data);

                return (null,
                    $"O ogol tem {confronto}, mas em {maisProxima.DataUtc.ToLocalTime():dd/MM/yyyy HH:mm} " +
                    $"— aqui o jogo está em {jogo.Data!.Value.ToLocalTime():dd/MM/yyyy HH:mm}. " +
                    "Reimporte a data pela api-football antes de tentar de novo.");
            }

            return (maisProxima, null);
        }

        /// <summary>
        /// O calendário inteiro da edição, percorrendo as páginas até vir uma sem
        /// partida. Fica em cache por poucos minutos: quem chama costuma pedir vários
        /// jogos seguidos (a reimportação em lote), e o placar da rodada de hoje muda
        /// justamente na hora em que interessa.
        /// </summary>
        internal async Task<List<PartidaOgol>?> BuscarCalendarioAsync(string edicao, CancellationToken ct)
        {
            var chave = $"ogol:calendario:{edicao}";
            if (_cache.TryGetValue(chave, out List<PartidaOgol>? emCache) && emCache != null)
                return emCache;

            var partidas = new List<PartidaOgol>();

            for (var pagina = 1; pagina <= MaximoPaginasCalendario; pagina++)
            {
                var rota = $"edicao/-/{edicao}/calendario?op=calendario&page={pagina}";
                var doc = await BuscarHtmlAsync(rota, TimeSpan.Zero, ct);

                // Página que NÃO respondeu não é fim de calendário. Tratá-la como fim
                // guardava meia tabela — e, pior, cacheava essa meia tabela por dez
                // minutos: as partidas das páginas seguintes (as últimas rodadas e todo
                // o mata-mata, que é onde o calendário cresce) apareciam como "não
                // localizadas" mesmo estando lá. Uma recusa passageira do Cloudflare
                // vira, aqui, uma falha explícita que a próxima tentativa resolve.
                if (doc == null)
                {
                    _logger.LogWarning(
                        "[Ogol] Edição {Edicao}: a página {Pagina} do calendário não respondeu — " +
                        "desisto do ciclo em vez de trabalhar com a tabela pela metade.",
                        edicao, pagina);
                    return null;
                }

                var daPagina = LerCalendario(doc).ToList();
                if (daPagina.Count == 0) break;

                partidas.AddRange(daPagina);
            }

            _logger.LogInformation("[Ogol] Edição {Edicao}: {N} partidas no calendário.",
                edicao, partidas.Count);

            if (partidas.Count > 0)
                _cache.Set(chave, partidas, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
                    // O MemoryCache global tem SizeLimit em bytes (ver Program.cs).
                    Size = partidas.Count * 200L,
                });

            return partidas;
        }

        /// <summary>
        /// Uma linha do calendário: id na própria &lt;tr&gt;, data e hora em colunas
        /// separadas, times em td.text.home / td.text.away e a fase em td.phase.
        /// </summary>
        private static IEnumerable<PartidaOgol> LerCalendario(HtmlDocument doc)
        {
            var linhas = doc.DocumentNode.SelectNodes(
                "//tr[@id and contains(concat(' ', normalize-space(@class), ' '), ' parent ')]");
            if (linhas == null) yield break;

            foreach (var linha in linhas)
            {
                var id = linha.GetAttributeValue("id", "");
                if (!id.All(char.IsDigit) || id.Length == 0) continue;

                var casa = Texto(linha.SelectSingleNode(".//td[contains(@class,'home')]"));
                var visitante = Texto(linha.SelectSingleNode(".//td[contains(@class,'away')]"));
                if (casa.Length == 0 || visitante.Length == 0) continue;

                var dia = Texto(linha.SelectSingleNode(".//td[contains(@class,'date')]"));
                if (!DateTime.TryParseExact(dia, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var data)) continue;

                // A hora é a coluna seguinte à data e vem vazia enquanto o jogo não tem
                // horário definido — aí vale a meia-noite, e a janela de casamento cobre
                // a diferença.
                var hora = Texto(linha.SelectSingleNode(".//td[contains(@class,'date')]/following-sibling::td[1]"));
                if (TimeSpan.TryParseExact(hora, @"hh\:mm", CultureInfo.InvariantCulture, out var h))
                    data = data.Add(h);

                var placar = Texto(linha.SelectSingleNode(".//td[contains(@class,'result')]"));

                yield return new PartidaOgol(
                    id,
                    DateTime.SpecifyKind(data - FusoBrasilia, DateTimeKind.Utc),
                    casa,
                    visitante,
                    Texto(linha.SelectSingleNode(".//td[contains(@class,'phase')]")) is { Length: > 0 } f
                        ? f : null,
                    placar.Contains('-'));
            }
        }

        // ── Estatísticas de time ─────────────────────────────────────────────

        // Rótulos do ogol traduzidos para os nomes que a api-football usa, que são os
        // que Jogo.EstatisticasJson guarda e que EstatisticaTimeCalculator, o painel de
        // Analisar e os relatórios já sabem ler. O que o ogol tem e não tem equivalente
        // por lá ("Bola Segura", "Divididas ganhas", "Tiros de meta") fica de fora: a
        // tela mostra "—" para chave ausente, e inventar chave nova só apareceria em
        // lugar nenhum.
        private static readonly (string Ogol, string ApiFootball, bool Percentual)[] ChavesTime =
        {
            ("Posse de Bola",             "Ball Possession",  true),
            ("Chutes",                    "Total Shots",      false),
            ("Chutes a gol",              "Shots on Goal",    false),
            ("Chutes Fora",               "Shots off Goal",   false),
            ("Chutes bloqueados",         "Blocked Shots",    false),
            ("Chutes de dentro da área",  "Shots insidebox",  false),
            ("Chutes de fora da área",    "Shots outsidebox", false),
            ("Escanteios",                "Corner Kicks",     false),
            ("Total Passes",              "Total passes",     false),
            ("Passes Certos",             "Passes accurate",  false),
            ("Faltas",                    "Fouls",            false),
            ("Impedimentos",              "Offsides",         false),
            ("Defesas",                   "Goalkeeper Saves", false),
        };

        /// <summary>
        /// Grava em Jogo.EstatisticasJson as estatísticas de time que o ogol publica.
        ///
        /// Não sobrescreve o que já existe, pela mesma razão da ESPN: o que está lá veio
        /// da api-football (mais completo) ou de uma importação anterior, e trocar por
        /// um conjunto menor seria uma piora.
        /// </summary>
        public async Task<ResultadoOgol> ImportarEstatisticasAsync(
            FutebolContext context, int jogoId, CancellationToken ct = default)
        {
            var (jogo, doc, idJogo, erro) = await AbrirPartidaAsync(context, jogoId, ct);
            if (erro != null) return erro;

            if (!string.IsNullOrWhiteSpace(jogo!.EstatisticasJson))
                return new ResultadoOgol(true, "As estatísticas deste jogo já estavam importadas.", idJogo);

            // EstatisticasJson é indexado pelo IdApi do time — sem ele a tela não sabe
            // de quem é cada coluna.
            if (jogo.TimeCasa is not { IdApi: > 0 } || jogo.TimeVisitante is not { IdApi: > 0 })
                return new ResultadoOgol(false,
                    "Um dos times está sem IdApi — as estatísticas de time são indexadas por esse id.", idJogo);

            var barras = LerEstatisticas(doc!);
            if (barras.Count == 0)
                return new ResultadoOgol(false, "O ogol não publicou as estatísticas desta partida.", idJogo);

            var casa = new Dictionary<string, string>();
            var visitante = new Dictionary<string, string>();

            foreach (var (ogol, apiFootball, percentual) in ChavesTime)
            {
                if (!barras.TryGetValue(ogol, out var valores)) continue;

                casa[apiFootball] = Formatar(valores.Casa, percentual);
                visitante[apiFootball] = Formatar(valores.Visitante, percentual);
            }

            // O ogol não publica a precisão de passe pronta; ela sai dos absolutos, como
            // na leitura da ESPN.
            AcrescentarPrecisaoDePasse(casa);
            AcrescentarPrecisaoDePasse(visitante);

            if (casa.Count == 0 && visitante.Count == 0)
                return new ResultadoOgol(false,
                    "O ogol publicou estatísticas, mas nenhuma delas tem equivalente no nosso painel.", idJogo);

            jogo.EstatisticasJson = JsonSerializer.Serialize(new[]
            {
                new { TimeId = jogo.TimeCasa!.IdApi,      Stats = casa },
                new { TimeId = jogo.TimeVisitante!.IdApi, Stats = visitante },
            });

            await context.SaveChangesAsync(ct);

            return new ResultadoOgol(true,
                $"Estatísticas do jogo importadas do ogol ({casa.Count} indicador(es) por time).", idJogo);
        }

        private static string Formatar(double valor, bool percentual) =>
            percentual
                ? $"{Math.Round(valor).ToString("0", CultureInfo.InvariantCulture)}%"
                : valor.ToString("0.##", CultureInfo.InvariantCulture);

        private static void AcrescentarPrecisaoDePasse(Dictionary<string, string> stats)
        {
            if (!stats.TryGetValue("Total passes", out var totalTexto) ||
                !stats.TryGetValue("Passes accurate", out var certosTexto)) return;

            if (!double.TryParse(totalTexto, NumberStyles.Float, CultureInfo.InvariantCulture, out var total) ||
                !double.TryParse(certosTexto, NumberStyles.Float, CultureInfo.InvariantCulture, out var certos) ||
                total <= 0) return;

            stats["Passes %"] = $"{Math.Round(certos / total * 100).ToString("0", CultureInfo.InvariantCulture)}%";
        }

        /// <summary>
        /// As barras de estatística da página: rótulo → (mandante, visitante). Cada
        /// barra é um .graph-bar com .bar-header contendo .num (casa), .bars-title e
        /// .num (visitante) — nessa ordem, que é a ordem em que a página desenha os dois
        /// times.
        ///
        /// O mesmo rótulo aparece mais de uma vez (o bloco "Destaque" repete quatro
        /// indicadores que também estão nas abas), então vale a PRIMEIRA ocorrência. O
        /// caso de "Ataques", que a página publica duas vezes com valores diferentes,
        /// não incomoda: ele não está no de-para.
        /// </summary>
        internal static Dictionary<string, (double Casa, double Visitante)> LerEstatisticas(HtmlDocument doc)
        {
            var barras = new Dictionary<string, (double, double)>(StringComparer.OrdinalIgnoreCase);

            var nos = doc.DocumentNode.SelectNodes(
                "//div[contains(concat(' ', normalize-space(@class), ' '), ' graph-bar ')]" +
                "//div[contains(concat(' ', normalize-space(@class), ' '), ' bar-header ')]");
            if (nos == null) return barras;

            foreach (var cabecalho in nos)
            {
                var rotulo = Texto(cabecalho.SelectSingleNode(
                    ".//div[contains(concat(' ', normalize-space(@class), ' '), ' bars-title ')]"));
                if (rotulo.Length == 0 || barras.ContainsKey(rotulo)) continue;

                var numeros = cabecalho.SelectNodes(
                    ".//div[contains(concat(' ', normalize-space(@class), ' '), ' num ')]");
                if (numeros == null || numeros.Count < 2) continue;

                if (Numero(numeros[0]) is { } casa && Numero(numeros[1]) is { } visitante)
                    barras[rotulo] = (casa, visitante);
            }

            return barras;
        }

        /// <summary>"47 %" e "6" viram 47 e 6; qualquer outra coisa vira null.</summary>
        private static double? Numero(HtmlNode? no)
        {
            var texto = Texto(no).Replace("%", "").Replace(" ", "").Replace(',', '.');
            return double.TryParse(texto, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
                ? v : null;
        }

        // ── Leitura do HTML ──────────────────────────────────────────────────

        /// <summary>
        /// Texto de um nó já sem entidades HTML e com os espaços normalizados. Devolve
        /// string vazia (e não null) para o nó ausente, porque toda a raspagem trata
        /// "não achei" e "veio vazio" da mesma forma.
        /// </summary>
        internal static string Texto(HtmlNode? no) =>
            no == null ? "" : HtmlEntity.DeEntitize(no.InnerText)?.Trim() ?? "";

        // Um minuto do ogol, com o acréscimo e a anotação que podem vir junto:
        // "80'", "90+5'", "68' (g.c.)", "52' (pen.)".
        private static readonly Regex MinutoRegex =
            new(@"(?<minuto>\d+)(?:\s*\+\s*\d+)?\s*'\s*(?:\((?<anotacao>[^)]*)\))?",
                RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        /// Os minutos escritos num rótulo de lance, cada um com a anotação que o
        /// acompanha (null quando não há).
        ///
        /// É plural porque o ogol NÃO repete o ícone: quem marcou duas vezes ganha um
        /// desenho de bola só, com os dois minutos no mesmo rótulo ("3' 34'"). Ler só o
        /// primeiro perdia o segundo gol em silêncio — o placar da ficha ficava menor
        /// que o do jogo.
        ///
        /// O acréscimo é descartado ("90+5'" vira 90), mesma convenção da api-football
        /// (Time.Elapsed, sem Extra) e o que o resto do sistema espera em Gol.Minuto e
        /// companhia.
        /// </summary>
        internal static IEnumerable<(int Minuto, string? Anotacao)> Minutos(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) yield break;

            foreach (Match m in MinutoRegex.Matches(texto))
                if (int.TryParse(m.Groups["minuto"].Value, out var minuto))
                    yield return (minuto,
                        m.Groups["anotacao"].Success ? m.Groups["anotacao"].Value : null);
        }

        /// <summary>
        /// O primeiro (ou único) minuto do rótulo; 0 quando não há nenhum. Ver
        /// <see cref="Minutos"/>.
        /// </summary>
        internal static int Minuto(string? texto) =>
            Minutos(texto).Select(m => m.Minuto).FirstOrDefault();

        // ── HTTP ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Busca a página e devolve o HTML já carregado. O slug da URL não importa: o
        /// site redireciona /jogo/-/12606699 para o endereço canônico, e é por isso que
        /// nada aqui precisa guardar o nome do confronto.
        /// </summary>
        /// <param name="ttl">Zero não cacheia — é o caso das páginas do calendário, que
        /// já são cacheadas juntas em <see cref="BuscarCalendarioAsync"/>.</param>
        private async Task<HtmlDocument?> BuscarHtmlAsync(string rota, TimeSpan ttl, CancellationToken ct)
        {
            var chave = "ogol:" + rota;
            if (ttl > TimeSpan.Zero && _cache.TryGetValue(chave, out string? emCache) && emCache != null)
                return Carregar(emCache);

            // Uma segunda tentativa depois de uma pausa. O que se quer alcançar aqui é
            // a recusa PASSAGEIRA — o 429/403 que o Cloudflare devolve quando várias
            // páginas do calendário chegam em sequência —, não o bloqueio de verdade,
            // que vai falhar de novo e sair no log com o status. Duas tentativas
            // bastam: se a segunda também não passa, insistir só empilha requisição
            // num host que já disse não.
            string? html = null;
            for (var tentativa = 1; tentativa <= 2 && html == null; tentativa++)
            {
                if (tentativa > 1) await Task.Delay(TimeSpan.FromSeconds(3), ct);
                html = await TentarAsync(rota, tentativa, ct);
            }

            if (string.IsNullOrWhiteSpace(html)) return null;

            if (ttl > TimeSpan.Zero)
                _cache.Set(chave, html, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = ttl,
                    Size = html.Length * 2L,
                });

            return Carregar(html);
        }

        private async Task<string?> TentarAsync(string rota, int tentativa, CancellationToken ct)
        {
            try
            {
                await AguardarPortaAsync(ct);
                var html = await _http.GetStringAsync(rota, ct);
                _ultimaFalhaHttp = null;
                return html;
            }
            catch (HttpRequestException ex)
            {
                // 403 aqui é o Cloudflare barrando a requisição, não página inexistente —
                // vale a pena aparecer no log com essa cara, porque a correção é outra.
                _ultimaFalhaHttp = ex.StatusCode is { } status
                    ? $"HTTP {(int)status}"
                    : "sem resposta";
                _logger.LogWarning(ex, "[Ogol] Falha ao buscar {Rota} ({Falha}, tentativa {N}/2).",
                    rota, _ultimaFalhaHttp, tentativa);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                _ultimaFalhaHttp = "tempo esgotado";
                _logger.LogWarning("[Ogol] Tempo esgotado ao buscar {Rota} (tentativa {N}/2).",
                    rota, tentativa);
            }
            catch (Exception ex)
            {
                _ultimaFalhaHttp = ex.GetType().Name;
                _logger.LogWarning(ex, "[Ogol] Falha ao buscar {Rota} (tentativa {N}/2).", rota, tentativa);
            }

            return null;
        }

        private static HtmlDocument Carregar(string html)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            return doc;
        }

        private static async Task AguardarPortaAsync(CancellationToken ct)
        {
            await _porta.WaitAsync(ct);
            try
            {
                var desde = DateTimeOffset.UtcNow - _ultimaChamada;
                if (desde < IntervaloMinimo)
                    await Task.Delay(IntervaloMinimo - desde, ct);
                _ultimaChamada = DateTimeOffset.UtcNow;
            }
            finally { _porta.Release(); }
        }
    }
}
