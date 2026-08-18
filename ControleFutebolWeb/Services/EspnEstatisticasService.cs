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
    public record ResultadoEspn(
        bool Ok,
        string Mensagem,
        long? EventoEspn = null,
        bool GravouEstatisticasTime = false,
        int JogadoresGravados = 0);

    /// <summary>
    /// Fonte alternativa de estatísticas quando a api-football não tem os dados da
    /// partida — acontece com alguma frequência fora das ligas europeias principais
    /// (ex.: o fixture 1492337, São Paulo x Coritiba de 15/08/2026, volta com
    /// players/statistics/lineups vazios e só os eventos preenchidos).
    ///
    /// Consome a API JSON pública da ESPN (site.web.api.espn.com), a mesma que o
    /// site espn.com.br usa para montar a página da partida. NÃO raspa o HTML de
    /// www.espn.com.br de propósito: aquele host está atrás de um desafio de
    /// JavaScript do AWS WAF, e contornar verificação de robô não é opção. Os
    /// endpoints usados aqui respondem JSON direto, sem desafio nenhum.
    ///
    /// Para não virar abuso mesmo assim, todas as chamadas passam por
    /// <see cref="_porta"/>: uma de cada vez, com intervalo mínimo entre elas,
    /// resposta em cache e desistência (com espera) em 429/503.
    /// </summary>
    public class EspnEstatisticasService
    {
        // Um jogo custa 2 chamadas (scoreboard do dia + summary do evento); o
        // scoreboard fica em cache, então uma rodada de 30 jogos da mesma data
        // gasta ~31 chamadas.
        public const int LimitePorLote = 30;

        private const string Base = "https://site.web.api.espn.com/apis/site/v2/sports/soccer/";

        // Uma requisição por vez para o host, com no mínimo esse intervalo entre
        // elas. Estático porque o serviço é transiente (AddHttpClient<T>) e o que
        // precisa ser serializado é o acesso ao host, não a instância.
        private static readonly SemaphoreSlim _porta = new(1, 1);
        private static DateTimeOffset _ultimaChamada = DateTimeOffset.MinValue;
        private static readonly TimeSpan IntervaloMinimo = TimeSpan.FromMilliseconds(1200);

        private readonly HttpClient _http;
        private readonly ILogger<EspnEstatisticasService> _logger;
        private readonly IMemoryCache _cache;
        private readonly IWebHostEnvironment _env;
        private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

        public EspnEstatisticasService(
            HttpClient http,
            ILogger<EspnEstatisticasService> logger,
            IMemoryCache cache,
            IWebHostEnvironment env)
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
            _env = env;
        }

        // ── De-para de liga ───────────────────────────────────────────────────

        /// <summary>
        /// Slugs da ESPN para o id de liga da api-football. Mais de um quando a ESPN
        /// separa o que o nosso cadastro junta (a fase preliminar da Champions fica em
        /// "uefa.champions_qual"). Vazio = liga sem cobertura.
        /// </summary>
        public IReadOnlyList<string> SlugsDaLiga(int? idApiLiga)
        {
            if (idApiLiga is null) return Array.Empty<string>();
            return MapaLigas().GetValueOrDefault(
                idApiLiga.Value.ToString(CultureInfo.InvariantCulture), Array.Empty<string>());
        }

        /// <summary>Slug principal, para rotular a competição na tela. Null sem cobertura.</summary>
        public string? SlugDaLiga(int? idApiLiga) => SlugsDaLiga(idApiLiga).FirstOrDefault();

        private Dictionary<string, string[]> MapaLigas() =>
            _cache.GetOrCreate("espn:ligas", e =>
            {
                e.Size = 4096;
                e.SlidingExpiration = TimeSpan.FromHours(6);

                var caminho = Path.Combine(_env.WebRootPath, "data", "ligas-espn.json");
                if (!File.Exists(caminho)) return new Dictionary<string, string[]>();

                var bruto = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(caminho))
                            ?? new Dictionary<string, JsonElement>();

                var mapa = new Dictionary<string, string[]>();
                foreach (var (chave, valor) in bruto)
                {
                    if (chave.StartsWith('_')) continue;
                    // Aceita "bra.1" e ["uefa.champions", "uefa.champions_qual"] — o
                    // formato de um slug só é mais legível para a maioria das ligas.
                    var slugs = valor.ValueKind switch
                    {
                        JsonValueKind.String => new[] { valor.GetString()! },
                        JsonValueKind.Array => valor.EnumerateArray()
                            .Select(x => x.GetString()).Where(x => x != null).Select(x => x!).ToArray(),
                        _ => Array.Empty<string>(),
                    };
                    if (slugs.Length > 0) mapa[chave] = slugs;
                }
                return mapa;
            }) ?? new Dictionary<string, string[]>();

        // ── Importação ────────────────────────────────────────────────────────

        /// <summary>
        /// Preenche as estatísticas do jogo com o que a ESPN tem. Não apaga nada que
        /// já exista: se o jogo já tem estatísticas de jogador, elas são mantidas e
        /// só as de time são complementadas (e vice-versa).
        /// </summary>
        public async Task<ResultadoEspn> ImportarAsync(
            FutebolContext context, int jogoId, CancellationToken ct = default)
        {
            var (jogo, doc, evento, erro) = await AbrirResumoAsync(context, jogoId, exigeIdApi: true, ct);
            if (erro != null) return erro;

            using var _ = doc!;
            var raiz = doc!.RootElement;

            var gravouTime = await GravarEstatisticasTimeAsync(context, jogo!, raiz, ct);
            var jogadores = await GravarEstatisticasJogadoresAsync(context, jogo!, raiz, ct);

            if (!gravouTime && jogadores == 0)
                return new ResultadoEspn(false,
                    "A ESPN localizou a partida, mas também não tem estatísticas dela.", evento);

            await context.SaveChangesAsync(ct);

            var partes = new List<string>();
            if (gravouTime) partes.Add("estatísticas de time");
            if (jogadores > 0) partes.Add($"{jogadores} jogador(es)");

            return new ResultadoEspn(true, $"Importado da ESPN: {string.Join(" e ", partes)}.",
                evento, gravouTime, jogadores);
        }

        /// <summary>
        /// Localiza a partida na ESPN e devolve o resumo dela. Compartilhado pela
        /// importação de estatísticas e pela conferência de escalação — as duas partem
        /// do mesmo documento e das mesmas checagens de pré-requisito.
        /// </summary>
        /// <param name="exigeIdApi">
        /// Só a importação precisa: EstatisticasJson é indexado pelo IdApi do time. A
        /// conferência de escalação trabalha com o elenco e não depende disso.
        /// </param>
        internal async Task<(Jogo? Jogo, JsonDocument? Doc, long? Evento, ResultadoEspn? Erro)> AbrirResumoAsync(
            FutebolContext context, int jogoId, bool exigeIdApi, CancellationToken ct)
        {
            var jogo = await context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Include(j => j.Competicao)
                .FirstOrDefaultAsync(j => j.Id == jogoId, ct);

            ResultadoEspn Falha(string msg, long? evento = null) => new(false, msg, evento);

            if (jogo == null) return (null, null, null, Falha("Jogo não encontrado."));
            if (jogo.Data == null)
                return (jogo, null, null, Falha("Jogo sem data — não dá para localizar na ESPN."));

            var slugs = SlugsDaLiga(jogo.Competicao?.IdApi);
            if (slugs.Count == 0)
                return (jogo, null, null, Falha(
                    $"A competição \"{jogo.Competicao?.Nome}\" não tem correspondente na ESPN (ver wwwroot/data/ligas-espn.json)."));

            if (exigeIdApi && (jogo.TimeCasa is not { IdApi: > 0 } || jogo.TimeVisitante is not { IdApi: > 0 }))
                return (jogo, null, null, Falha(
                    "Um dos times está sem IdApi — as estatísticas de time são indexadas por esse id."));

            var achado = await ResolverEventoAsync(jogo, slugs, ct);
            if (achado == null)
                return (jogo, null, null, Falha(
                    "Partida não localizada na ESPN (liga, data ou nome dos times não bateram)."));

            var (slug, evento) = achado.Value;
            // lang/region não mudam as CHAVES das estatísticas (são ids fixos), mas
            // trazem a posição de cada jogador em português — e é do texto da posição
            // que EspnEscalacaoService deduz a linha do jogador em campo.
            var doc = await BuscarJsonAsync($"{slug}/summary?event={evento}&lang=pt&region=br", TimeSpan.FromDays(7), ct);
            if (doc == null)
                return (jogo, null, evento, Falha("A ESPN não respondeu o resumo da partida.", evento));

            return (jogo, doc, evento, null);
        }

        /// <summary>
        /// Acha o id do evento na ESPN pelo scoreboard da liga na data do jogo. Olha o
        /// dia anterior e o seguinte também: a ESPN publica a data em UTC e um jogo das
        /// 21h de Brasília cai no dia seguinte lá.
        /// </summary>
        private async Task<(string Slug, long Evento)?> ResolverEventoAsync(
            Jogo jogo, IReadOnlyList<string> slugs, CancellationToken ct)
        {
            var dia = DateOnly.FromDateTime(jogo.Data!.Value.ToLocalTime().Date);

            foreach (var slug in slugs)
            foreach (var deslocamento in new[] { 0, 1, -1 })
            {
                var data = dia.AddDays(deslocamento).ToString("yyyyMMdd", CultureInfo.InvariantCulture);
                var sb = await BuscarJsonAsync($"{slug}/scoreboard?dates={data}", TimeSpan.FromHours(12), ct);
                if (sb == null) continue;

                using (sb)
                {
                    if (!sb.RootElement.TryGetProperty("events", out var eventos)) continue;

                    foreach (var ev in eventos.EnumerateArray())
                    {
                        var comp = ev.GetProperty("competitions")[0];
                        string? casa = null, visitante = null;
                        foreach (var c in comp.GetProperty("competitors").EnumerateArray())
                        {
                            var nome = c.GetProperty("team").GetProperty("displayName").GetString();
                            if (c.GetProperty("homeAway").GetString() == "home") casa = nome;
                            else visitante = nome;
                        }

                        if (TimeNomeMatcher.SaoMesmoTime(jogo.TimeCasa?.Nome, casa) &&
                            TimeNomeMatcher.SaoMesmoTime(jogo.TimeVisitante?.Nome, visitante) &&
                            long.TryParse(ev.GetProperty("id").GetString(), out var id))
                            return (slug, id);
                    }
                }
            }

            return null;
        }

        // ── Estatísticas de time ──────────────────────────────────────────────

        // Chaves da ESPN traduzidas para os nomes que a api-football usa, que são os
        // que Jogo.EstatisticasJson guarda e que EstatisticaTimeCalculator, o painel
        // de Analisar e os relatórios já sabem ler. O que a ESPN não tem (xG,
        // finalizações dentro/fora da área) simplesmente não entra no JSON — a tela
        // mostra "—" para chave ausente, que é honesto; zero seria mentira.
        private static readonly (string Espn, string ApiFootball, bool Percentual)[] ChavesTime =
        {
            ("possessionPct",   "Ball Possession",   true),
            ("totalShots",      "Total Shots",       false),
            ("shotsOnTarget",   "Shots on Goal",     false),
            ("blockedShots",    "Blocked Shots",     false),
            ("wonCorners",      "Corner Kicks",      false),
            ("totalPasses",     "Total passes",      false),
            ("accuratePasses",  "Passes accurate",   false),
            ("foulsCommitted",  "Fouls",             false),
            ("offsides",        "Offsides",          false),
            ("saves",           "Goalkeeper Saves",  false),
            ("yellowCards",     "Yellow Cards",      false),
            ("redCards",        "Red Cards",         false),
        };

        private async Task<bool> GravarEstatisticasTimeAsync(
            FutebolContext context, Jogo jogo, JsonElement raiz, CancellationToken ct)
        {
            if (!string.IsNullOrWhiteSpace(jogo.EstatisticasJson)) return false;
            if (!raiz.TryGetProperty("boxscore", out var box) ||
                !box.TryGetProperty("teams", out var times)) return false;

            var lista = new List<object>();

            foreach (var t in times.EnumerateArray())
            {
                var nome = t.GetProperty("team").GetProperty("displayName").GetString();
                var idApi = TimeNomeMatcher.SaoMesmoTime(jogo.TimeCasa?.Nome, nome) ? jogo.TimeCasa!.IdApi
                          : TimeNomeMatcher.SaoMesmoTime(jogo.TimeVisitante?.Nome, nome) ? jogo.TimeVisitante!.IdApi
                          : 0;
                if (idApi == 0) continue;

                if (!t.TryGetProperty("statistics", out var stats)) continue;

                var brutos = new Dictionary<string, double>();
                foreach (var s in stats.EnumerateArray())
                {
                    var chave = s.GetProperty("name").GetString();
                    if (chave == null) continue;
                    var valor = s.TryGetProperty("displayValue", out var dv) ? dv.GetString() : null;
                    if (double.TryParse(valor, NumberStyles.Float, CultureInfo.InvariantCulture, out var n))
                        brutos[chave] = n;
                }
                if (brutos.Count == 0) continue;

                var convertidos = new Dictionary<string, string>();
                foreach (var (espn, apiFootball, pct) in ChavesTime)
                {
                    if (!brutos.TryGetValue(espn, out var v)) continue;
                    convertidos[apiFootball] = pct
                        ? $"{Math.Round(v).ToString("0", CultureInfo.InvariantCulture)}%"
                        : v.ToString("0.##", CultureInfo.InvariantCulture);
                }

                // A ESPN manda passPct arredondado como fração ("0.9" para 86,9%), o que
                // perde precisão demais para a tela — recalcula dos absolutos.
                if (brutos.TryGetValue("accuratePasses", out var certos) &&
                    brutos.TryGetValue("totalPasses", out var totalPasses) && totalPasses > 0)
                    convertidos["Passes %"] = $"{Math.Round(certos / totalPasses * 100).ToString("0", CultureInfo.InvariantCulture)}%";

                if (convertidos.Count > 0)
                    lista.Add(new { TimeId = idApi, Stats = convertidos });
            }

            if (lista.Count == 0) return false;

            jogo.EstatisticasJson = JsonSerializer.Serialize(lista);
            await Task.CompletedTask;
            return true;
        }

        // ── Estatísticas de jogador ───────────────────────────────────────────

        private async Task<int> GravarEstatisticasJogadoresAsync(
            FutebolContext context, Jogo jogo, JsonElement raiz, CancellationToken ct)
        {
            // Dado da api-football é mais completo e nunca é substituído. O que a
            // própria ESPN gravou antes pode ser refeito: é como uma reimportação
            // corrige uma linha que ficou sem minutos ou com jogador não casado.
            var existentes = await context.EstatisticasJogador
                .Where(e => e.JogoId == jogo.Id)
                .ToListAsync(ct);
            if (existentes.Any(e => e.Fonte != FonteEstatistica.Espn)) return 0;

            if (!raiz.TryGetProperty("rosters", out var rosters)) return 0;

            // Candidatos restritos aos escalados do jogo — NomeJogadorHelper.Corresponde
            // tolera abreviação e só é seguro dentro de um conjunto pequeno.
            var escaladosPorLado = await context.Escalacoes
                .Where(e => e.JogoId == jogo.Id && e.JogadorId != null)
                .Select(e => new { e.IsTimeCasa, e.Jogador!.Id, e.Jogador.Nome, e.Jogador.NumeroCamisa })
                .Distinct()
                .ToListAsync(ct);

            var minutos = await CalcularMinutosAsync(context, jogo.Id, ct);

            // As linhas novas são montadas ANTES de mexer no que já existe. A versão
            // anterior removia e commitava primeiro: quando a regravação não casava
            // ninguém, o jogo ficava sem estatística alguma e o dado anterior estava
            // perdido. Agora só troca quando há algo com que trocar.
            var novas = new List<EstatisticaJogador>();

            foreach (var r in rosters.EnumerateArray())
            {
                var nomeTime = r.GetProperty("team").GetProperty("displayName").GetString();
                bool? ehCasa = TimeNomeMatcher.SaoMesmoTime(jogo.TimeCasa?.Nome, nomeTime) ? true
                             : TimeNomeMatcher.SaoMesmoTime(jogo.TimeVisitante?.Nome, nomeTime) ? false
                             : null;
                if (ehCasa == null) continue;

                var candidatos = escaladosPorLado.Where(e => e.IsTimeCasa == ehCasa.Value).ToList();
                if (candidatos.Count == 0) continue;

                if (!r.TryGetProperty("roster", out var atletas)) continue;

                foreach (var a in atletas.EnumerateArray())
                {
                    var nome = a.GetProperty("athlete").GetProperty("displayName").GetString();
                    if (string.IsNullOrWhiteSpace(nome)) continue;

                    int? camisa = a.TryGetProperty("jersey", out var camisaEl)
                                  && int.TryParse(camisaEl.GetString(), out var c) ? c : null;

                    var casaram = candidatos.Where(x => NomeJogadorHelper.Corresponde(x.Nome, nome)).ToList();

                    // O nome falha mais do que parece: a ESPN chama de "Joao Victor" o
                    // que o cadastro tem como "Victor Sá". A camisa resolve esse caso e
                    // é estável dentro da partida.
                    if (casaram.Count == 0 && camisa != null)
                        casaram = candidatos.Where(x => x.NumeroCamisa == camisa).ToList();

                    if (casaram.Count == 0) continue;

                    // Mais de um candidato é duplicata no cadastro ("Keo Boets" e
                    // "K. Boets", ambos camisa 71), não ambiguidade real. Descartar a
                    // linha deixava o jogador sem estatística nenhuma no jogo.
                    var escolhido = casaram.Count == 1
                        ? casaram[0]
                        : casaram.OrderByDescending(x => NomeJogadorHelper.Normalizar(x.Nome) == NomeJogadorHelper.Normalizar(nome))
                                 .ThenByDescending(x => camisa != null && x.NumeroCamisa == camisa)
                                 .ThenBy(x => x.Id)
                                 .First();

                    if (!a.TryGetProperty("stats", out var stats)) continue;

                    var s = new Dictionary<string, int>();
                    foreach (var st in stats.EnumerateArray())
                    {
                        var chave = st.GetProperty("name").GetString();
                        if (chave == null) continue;
                        if (st.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.Number)
                            s[chave] = (int)Math.Round(v.GetDouble());
                    }
                    if (s.Count == 0) continue;

                    // appearances = 0 é o reserva que sequer entrou. Gravar linha para
                    // ele criaria uma partida disputada que não existiu, poluindo média
                    // de jogos, ranking e o bônus de "não sofreu gol".
                    if (s.GetValueOrDefault("appearances") <= 0) continue;

                    var titular = a.TryGetProperty("starter", out var t) && t.GetBoolean();
                    var jogadorId = escolhido.Id;

                    // A ESPN publica goalsConceded para o time inteiro (todo mundo que
                    // estava em campo "sofreu" o gol), enquanto a api-football só
                    // preenche para o goleiro — e o resto do sistema conta com isso: o
                    // critério "gol sofrido" pesa -1,0 e a família Goleiro do rating lê
                    // esse campo. Copiar direto derrubava a nota de todo jogador de
                    // linha de um time que levou gol. Só o goleiro carrega o campo.
                    var goleiro = a.TryGetProperty("position", out var pos)
                                  && ((pos.TryGetProperty("abbreviation", out var abrev) && abrev.GetString() == "G")
                                      || (pos.TryGetProperty("displayName", out var pnome)
                                          && EspnEscalacaoService.RankLinha(pnome.GetString() ?? "") == 0));

                    // A ESPN não publica minutos em campo nem nota do jogador. Os
                    // minutos vêm das substituições que a api-football já gravou (ver
                    // CalcularMinutosAsync); Rating fica null e a tela mostra "—".
                    // Passes, desarmes, duelos, dribles e pênaltis não vêm por jogador
                    // (só no total do time) e ficam zerados — quem lê para gerar nota
                    // descarta esses campos pela Fonte, ver FonteEstatistica.
                    novas.Add(new EstatisticaJogador
                    {
                        JogoId            = jogo.Id,
                        JogadorId         = jogadorId,
                        Fonte             = FonteEstatistica.Espn,
                        Minutos           = minutos.TryGetValue(jogadorId, out var m) ? m : null,
                        Gols              = s.GetValueOrDefault("totalGoals"),
                        Assistencias      = s.GetValueOrDefault("goalAssists"),
                        FinalizacoesTotal = s.GetValueOrDefault("totalShots"),
                        FinalizacoesNoGol = s.GetValueOrDefault("shotsOnTarget"),
                        FaltasCometidas   = s.GetValueOrDefault("foulsCommitted"),
                        FaltasSofridas    = s.GetValueOrDefault("foulsSuffered"),
                        CartoesAmarelos   = s.GetValueOrDefault("yellowCards"),
                        CartoesVermelhos  = s.GetValueOrDefault("redCards"),
                        Offsides          = s.GetValueOrDefault("offsides"),
                        Defesas           = goleiro ? s.GetValueOrDefault("saves") : 0,
                        GolsSofridos      = goleiro ? s.GetValueOrDefault("goalsConceded") : 0,
                        EntrouDoBanco     = !titular,
                    });
                }
            }

            // Nada casou: preserva o que já estava lá em vez de zerar o jogo.
            if (novas.Count == 0) return 0;

            // Troca atômica — o SaveChanges de ImportarAsync grava a remoção e a
            // inserção na mesma transação.
            context.EstatisticasJogador.RemoveRange(existentes);
            context.EstatisticasJogador.AddRange(novas);
            return novas.Count;
        }

        // ── Minutos em campo ──────────────────────────────────────────────────

        /// <summary>
        /// Minutos de cada jogador, deduzidos das substituições que a api-football já
        /// gravou. Vale a pena mesmo quando ela não manda estatística nenhuma: nesses
        /// jogos o bloco de eventos costuma vir preenchido, e é dele que saem as
        /// substituições. Conferido contra a ESPN no jogo 15874 — os dois lados dão
        /// as mesmas trocas aos 63' e 78'.
        ///
        /// Quem não aparece no dicionário fica com minutos desconhecidos (null), não
        /// com zero: o rating precisa distinguir "não deu para saber" de "não jogou".
        /// </summary>
        private static async Task<Dictionary<int, int>> CalcularMinutosAsync(
            FutebolContext context, int jogoId, CancellationToken ct)
        {
            var substituicoes = await context.Substituicoes
                .Where(s => s.JogoId == jogoId)
                .Select(s => new { s.JogadorEntrouId, s.JogadorSaiuId, s.Minuto })
                .ToListAsync(ct);

            var vermelhos = await context.Cartoes
                .Where(c => c.JogoId == jogoId && c.Tipo == "Vermelho")
                .Select(c => new { c.JogadorId, c.Minuto })
                .ToListAsync(ct);

            var gols = await context.Gols.Where(g => g.JogoId == jogoId).Select(g => g.Minuto).ToListAsync(ct);
            var amarelos = await context.Cartoes.Where(c => c.JogoId == jogoId).Select(c => c.Minuto).ToListAsync(ct);

            // Prorrogação: acréscimo passa de 90 mas raramente de 100, enquanto jogo que
            // foi para a prorrogação tem evento depois disso. Sem nenhum evento tardio
            // o padrão continua sendo 90, que é a mesma convenção da api-football.
            var ultimoEvento = substituicoes.Select(s => s.Minuto)
                .Concat(vermelhos.Select(v => v.Minuto))
                .Concat(gols).Concat(amarelos)
                .DefaultIfEmpty(0).Max();
            var duracao = ultimoEvento > 100 ? 120 : 90;

            // Só a escalação inicial diz quem começou jogando. Distinct porque o mesmo
            // titular pode ter mais de uma linha (escalação gravada em duplicidade e
            // variações táticas gravadas pelo usuário).
            var titulares = (await context.Escalacoes
                .Where(e => e.JogoId == jogoId && e.Titular && e.FaseEscalacao == "INICIAL" && e.JogadorId != null)
                .Select(e => e.JogadorId!.Value)
                .Distinct()
                .ToListAsync(ct)).ToHashSet();

            // Em vez de somar e subtrair trechos, monta entrada e saída de cada jogador
            // e faz a diferença no fim — assim o reserva que entrou e depois foi
            // substituído (ou expulso) cai no mesmo caminho de todo mundo.
            var entrada = new Dictionary<int, int>();
            var saida = new Dictionary<int, int>();

            foreach (var id in titulares) { entrada[id] = 0; saida[id] = duracao; }

            foreach (var s in substituicoes)
            {
                if (s.JogadorEntrouId is int entrou)
                {
                    entrada[entrou] = s.Minuto;
                    saida.TryAdd(entrou, duracao);
                }
                if (s.JogadorSaiuId is int saiu)
                {
                    saida[saiu] = s.Minuto;
                    entrada.TryAdd(saiu, 0);
                }
            }

            // Expulso para de jogar na hora — não há substituição registrando a saída.
            foreach (var v in vermelhos)
            {
                if (!entrada.ContainsKey(v.JogadorId)) continue;
                saida[v.JogadorId] = Math.Min(saida.GetValueOrDefault(v.JogadorId, duracao), v.Minuto);
            }

            return entrada.ToDictionary(
                kv => kv.Key,
                kv => Math.Clamp(saida.GetValueOrDefault(kv.Key, duracao) - kv.Value, 0, duracao));
        }

        // ── HTTP ──────────────────────────────────────────────────────────────

        /// <summary>
        /// GET com cache, uma chamada por vez e intervalo mínimo entre elas. Devolve
        /// null (sem estourar exceção) em qualquer falha: importação de lote não pode
        /// morrer porque um jogo deu 404.
        /// </summary>
        private async Task<JsonDocument?> BuscarJsonAsync(string url, TimeSpan validade, CancellationToken ct)
        {
            var chave = "espn:" + url;
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
                            var espera = resp.Headers.RetryAfter?.Delta
                                         ?? TimeSpan.FromSeconds(5 * tentativa);
                            _logger.LogWarning("[ESPN] {Status} em {Url} — aguardando {Espera}s.",
                                (int)resp.StatusCode, url, espera.TotalSeconds);
                            await Task.Delay(espera, ct);
                            continue;
                        }

                        // 400/404 = liga ou evento que não existe lá; repetir não ajuda.
                        _logger.LogInformation("[ESPN] {Status} em {Url}.", (int)resp.StatusCode, url);
                        return null;
                    }
                    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
                    {
                        _ultimaChamada = DateTimeOffset.UtcNow;
                        _logger.LogWarning(ex, "[ESPN] Falha de rede em {Url} (tentativa {N}).", url, tentativa);
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
