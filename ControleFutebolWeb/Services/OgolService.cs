using ControleFutebolWeb.Data;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Models;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Web;

namespace ControleFutebolWeb.Services
{
    public class OgolService
    {
        private const string BaseUrl = "https://www.ogol.com.br";

        private readonly HttpClient _httpClient;
        private readonly ILogger<OgolService> _logger;
        private readonly FutebolContext _context;

        public OgolService(HttpClient httpClient, ILogger<OgolService> logger, FutebolContext context)
        {
            _httpClient = httpClient;
            _logger = logger;
            _context = context;

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                "(KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept",
                "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "pt-BR,pt;q=0.9,en;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Referer", "https://www.ogol.com.br/");
        }

        // ══════════════════════════════════════════════════════════════
        // SCRAPING ─ Lista de jogos da competição
        // URL esperada: https://www.ogol.com.br/edicao/brasileirao-serie-a-2025/ID
        // ══════════════════════════════════════════════════════════════

        public async Task<List<TransfermarktJogoInfo>> BuscarJogosCompeticaoPorLink(
            string linkCompeticao, CancellationToken ct = default)
        {
            var jogos = new List<TransfermarktJogoInfo>();
            if (string.IsNullOrWhiteSpace(linkCompeticao)) return jogos;

            if (!linkCompeticao.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                linkCompeticao = BaseUrl + linkCompeticao;

            string html;
            try
            {
                await Task.Delay(1000, ct);
                html = await _httpClient.GetStringAsync(linkCompeticao, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Ogol] Erro ao buscar competição: {Url}", linkCompeticao);
                return jogos;
            }

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var processados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // ── Estratégia 1: tabela #fixture_games (Copa do Mundo, Brasileirão, páginas /competicao/) ──
            var fixtureDivs = doc.DocumentNode.SelectNodes("//div[@id='fixture_games']");
            if (fixtureDivs != null && fixtureDivs.Count > 0)
            {
                foreach (var fd in fixtureDivs)
                    ExtrairJogosDeFixtureGames(fd, processados, jogos);

                // Busca páginas adicionais: outras jornadas + fases eliminatórias
                var urlsAdicionais = ExtrairUrlsPaginasAdicionais(doc, linkCompeticao);
                _logger.LogInformation("[Ogol] {N} página(s) adicional(is) detectadas.", urlsAdicionais.Count);

                foreach (var urlAdd in urlsAdicionais)
                {
                    if (ct.IsCancellationRequested) break;
                    try
                    {
                        await Task.Delay(1500, ct);
                        var htmlAdd = await _httpClient.GetStringAsync(urlAdd, ct);
                        var docAdd = new HtmlDocument();
                        docAdd.LoadHtml(htmlAdd);

                        // Extrai jornada_in da URL para fallback de rodada caso o HTML não indique
                        int rodadaUrlFallback = 0;
                        try
                        {
                            var qsAdd = HttpUtility.ParseQueryString(new Uri(urlAdd).Query);
                            if (int.TryParse(qsAdd["jornada_in"], out var jr)) rodadaUrlFallback = jr;
                        }
                        catch { }

                        var fixtureDivsAdd = docAdd.DocumentNode.SelectNodes("//div[@id='fixture_games']");
                        if (fixtureDivsAdd != null)
                            foreach (var fd in fixtureDivsAdd)
                                ExtrairJogosDeFixtureGames(fd, processados, jogos, rodadaUrlFallback);

                        _logger.LogInformation("[Ogol] Pág adicional processada: {Url} | acumulado={N}", urlAdd, jogos.Count);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[Ogol] Erro ao buscar página adicional: {Url}", urlAdd);
                    }
                }

                _logger.LogInformation("[Ogol] fixture_games total: {N} jogos ({P} com placar)",
                    jogos.Count, jogos.Count(j => j.PlacarCasa.HasValue));
                return jogos;
            }

            // ── Estratégia 2: fallback para páginas /edicao/ (Brasileirão etc.) ──
            // Exclui a barra de agenda (ul#zz-matchbox-ul-agenda) que mostra jogos de outras competições
            var linksJogo = doc.DocumentNode.SelectNodes(
                "//a[contains(@href,'/jogo/') and contains(@href,'-')" +
                " and not(ancestor::ul[@id='zz-matchbox-ul-agenda'])]");

            if (linksJogo == null)
            {
                _logger.LogWarning("[Ogol] Nenhum link de jogo encontrado em: {Url}", linkCompeticao);
                return jogos;
            }

            int rodadaAtual = 0;
            string? grupoAtual = null;

            foreach (var linkNode in linksJogo)
            {
                var href = linkNode.GetAttributeValue("href", "").Trim();

                if (!Regex.IsMatch(href, @"/jogo/\d{4}-\d{2}-\d{2}-")) continue;
                if (!processados.Add(href)) continue;

                // ── Rodada e Grupo (sobe na DOM) ──────────────────────────────
                int rodadaEncontrada = 0;
                string? grupoEncontrado = null;
                var ancestral = linkNode.ParentNode;
                for (int i = 0; i < 15 && ancestral != null; i++)
                {
                    var textoAnc = HtmlEntity.DeEntitize(ancestral.InnerText ?? "");
                    if (rodadaEncontrada == 0)
                    {
                        var rm = Regex.Match(textoAnc, @"[Rr]odada\s*(\d+)");
                        if (rm.Success) rodadaEncontrada = int.Parse(rm.Groups[1].Value);
                    }
                    if (grupoEncontrado == null)
                    {
                        var gm = Regex.Match(textoAnc, @"[Gg]rupo\s+([A-Za-z])");
                        if (gm.Success)
                            grupoEncontrado = "Grupo " + gm.Groups[1].Value.ToUpper();
                        else
                        {
                            var lg = ancestral.SelectSingleNode(".//a[contains(@href,'grupo=')]");
                            if (lg != null)
                            {
                                var letra = HtmlEntity.DeEntitize(lg.InnerText.Trim());
                                if (Regex.IsMatch(letra, @"^[A-Za-z]$"))
                                    grupoEncontrado = "Grupo " + letra.ToUpper();
                            }
                        }
                    }
                    if (rodadaEncontrada > 0 && grupoEncontrado != null) break;
                    ancestral = ancestral.ParentNode;
                }
                if (rodadaEncontrada > 0) rodadaAtual = rodadaEncontrada;
                if (grupoEncontrado != null) grupoAtual = grupoEncontrado;

                // ── Data ──────────────────────────────────────────────────────
                DateTime? dataJogo = null;
                var dataMatch = Regex.Match(href, @"/jogo/(\d{4}-\d{2}-\d{2})-");
                if (dataMatch.Success &&
                    DateTime.TryParseExact(dataMatch.Groups[1].Value, "yyyy-MM-dd",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                    dataJogo = DateTime.SpecifyKind(dt, DateTimeKind.Utc);

                // ── Times (span.title dentro de div.team) ─────────────────────
                var nomes = new List<string>();
                var titleSpans = linkNode.SelectNodes(
                    ".//div[contains(@class,'team')]//span[contains(@class,'title')]");
                if (titleSpans != null)
                {
                    foreach (var sp in titleSpans)
                    {
                        var nome = HtmlEntity.DeEntitize(sp.InnerText.Trim());
                        if (!string.IsNullOrWhiteSpace(nome) && !nomes.Contains(nome))
                            nomes.Add(nome);
                        if (nomes.Count == 2) break;
                    }
                }
                if (nomes.Count < 2)
                {
                    var hm = Regex.Match(href,
                        @"/jogo/\d{4}-\d{2}-\d{2}-([a-z0-9-]+)-([a-z0-9-]+)/\d+",
                        RegexOptions.IgnoreCase);
                    if (hm.Success)
                    {
                        nomes.Add(CapitalizarSlug(hm.Groups[1].Value));
                        nomes.Add(CapitalizarSlug(hm.Groups[2].Value));
                    }
                }
                if (nomes.Count < 2) continue;

                // ── Placar ────────────────────────────────────────────────────
                int? pc = null, pv = null;
                var textoLink = Regex.Replace(
                    HtmlEntity.DeEntitize(linkNode.InnerText ?? ""), @"\b\d{1,2}:\d{2}\b", "");
                var scoreMatch = Regex.Match(textoLink, @"\b(\d{1,2})\s*[-:]\s*(\d{1,2})\b");
                if (scoreMatch.Success)
                {
                    int s1 = int.Parse(scoreMatch.Groups[1].Value);
                    int s2 = int.Parse(scoreMatch.Groups[2].Value);
                    if (s1 <= 20 && s2 <= 20) { pc = s1; pv = s2; }
                }

                var urlAbsoluta = href.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? href : BaseUrl + href;

                jogos.Add(new TransfermarktJogoInfo
                {
                    NomeTimeCasa = nomes[0],
                    NomeTimeVisitante = nomes[1],
                    PlacarCasa = pc,
                    PlacarVisitante = pv,
                    Data = dataJogo,
                    Rodada = rodadaAtual,
                    Grupo = grupoAtual,
                    LinkDetalhes = urlAbsoluta
                });

                _logger.LogInformation("[Ogol] Jogo: {Casa} x {Vis} | Rod={R} | Grupo={G} | {Placar} | {Data}",
                    nomes[0], nomes[1], rodadaAtual, grupoAtual ?? "–",
                    pc.HasValue ? $"{pc}:{pv}" : "–",
                    dataJogo?.ToString("dd/MM/yyyy") ?? "–");
            }

            _logger.LogInformation("[Ogol] Total: {N} jogos ({P} com placar)",
                jogos.Count, jogos.Count(j => j.PlacarCasa.HasValue));

            return jogos;
        }

        // ── Extrai jogos de um nó #fixture_games já localizado ──
        private void ExtrairJogosDeFixtureGames(
            HtmlNode fixtureDiv,
            HashSet<string> processados,
            List<TransfermarktJogoInfo> jogos,
            int rodadaUrlFallback = 0)
        {
            // Extrai a rodada do "RODADA X" próximo ao div; usa rodadaUrlFallback se não achar.
            int rodadaFallback = ExtrairRodadaDaSecao(fixtureDiv);
            if (rodadaFallback == 0) rodadaFallback = rodadaUrlFallback;

            // 'result' = jogo realizado, 'vs' = jogo futuro
            var rows = fixtureDiv.SelectNodes(
                ".//tr[.//td[contains(@class,'result') or @class='vs']]");
            if (rows == null) return;

            int rodadaAtualFix = rodadaFallback;
            string? grupoAtualFix = null;

            foreach (var row in rows)
            {
                var tds = row.SelectNodes("td");
                if (tds == null || tds.Count < 7) continue;

                // Link e placar (td com class='result' ou 'vs')
                var tdResult = tds.FirstOrDefault(t => {
                    var cls = t.GetAttributeValue("class", "");
                    return cls.Contains("result") || cls == "vs";
                });
                if (tdResult == null) continue;

                var jogoA = tdResult.SelectSingleNode(".//a[contains(@href,'/jogo/')]");
                if (jogoA == null) continue;

                var href = jogoA.GetAttributeValue("href", "").Trim();
                if (!Regex.IsMatch(href, @"/jogo/\d{4}-\d{2}-\d{2}-")) continue;
                if (!processados.Add(href)) continue;

                var scoreText = HtmlEntity.DeEntitize(jogoA.InnerText.Trim());
                int? pc = null, pv = null;
                var sm = Regex.Match(scoreText, @"^(\d{1,2})\s*-\s*(\d{1,2})$");
                if (sm.Success)
                {
                    int s1 = int.Parse(sm.Groups[1].Value);
                    int s2 = int.Parse(sm.Groups[2].Value);
                    if (s1 <= 20 && s2 <= 20) { pc = s1; pv = s2; }
                }

                // Data (do href do jogo)
                DateTime? dataJogo = null;
                var dataMatch = Regex.Match(href, @"/jogo/(\d{4}-\d{2}-\d{2})-");
                if (dataMatch.Success &&
                    DateTime.TryParseExact(dataMatch.Groups[1].Value, "yyyy-MM-dd",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                {
                    dataJogo = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                }

                // Grupo (td[1]) – letra do grupo ("A","B") ou número de rodada
                var grupoTexto = HtmlEntity.DeEntitize(tds[1].InnerText.Trim());
                string? grupoLinha = null;
                int rodadaLinha = 0;
                if (Regex.IsMatch(grupoTexto, @"^[A-Za-z]$"))
                    grupoLinha = "Grupo " + grupoTexto.ToUpper();
                else
                {
                    var rodadaM = Regex.Match(grupoTexto, @"[Rr]?\s*(\d+)");
                    if (rodadaM.Success) rodadaLinha = int.Parse(rodadaM.Groups[1].Value);
                }
                if (grupoLinha != null) grupoAtualFix = grupoLinha;
                if (rodadaLinha > 0) rodadaAtualFix = rodadaLinha;

                int resultIdx = tds.IndexOf(tdResult);

                // Time casa
                string nomeCasa = "";
                string? linkCasaRaw = null;
                string? escudoCasa = null;
                if (resultIdx >= 2)
                {
                    var tdNomeCasa = tds[resultIdx - 2];
                    var aNomeCasa = tdNomeCasa.SelectSingleNode(".//a");
                    if (aNomeCasa != null)
                    {
                        nomeCasa = HtmlEntity.DeEntitize(
                            (aNomeCasa.SelectSingleNode(".//b") ?? aNomeCasa).InnerText.Trim());
                        linkCasaRaw = aNomeCasa.GetAttributeValue("href", "");
                    }
                    var imgCasa = tds[resultIdx - 1].SelectSingleNode(".//img[@src]");
                    if (imgCasa != null)
                    {
                        var src = imgCasa.GetAttributeValue("src", "").Trim();
                        escudoCasa = src.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                            ? src : BaseUrl + src;
                    }
                }

                // Time visitante
                string nomeVis = "";
                string? linkVisRaw = null;
                string? escudoVis = null;
                if (resultIdx + 2 < tds.Count)
                {
                    var imgVis = tds[resultIdx + 1].SelectSingleNode(".//img[@src]");
                    if (imgVis != null)
                    {
                        var src = imgVis.GetAttributeValue("src", "").Trim();
                        escudoVis = src.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                            ? src : BaseUrl + src;
                    }
                    var aNomeVis = tds[resultIdx + 2].SelectSingleNode(".//a");
                    if (aNomeVis != null)
                    {
                        nomeVis = HtmlEntity.DeEntitize(aNomeVis.InnerText.Trim());
                        linkVisRaw = aNomeVis.GetAttributeValue("href", "");
                    }
                }

                if (string.IsNullOrWhiteSpace(nomeCasa) || string.IsNullOrWhiteSpace(nomeVis))
                    continue;

                // Ignora placeholders de fases eliminatórias ainda não definidas
                // Exemplos: "1A", "2B", "Winner Match 99", "Loser Match 101", "3 D/E/I/J/L"
                if (EhPlaceholderEliminatoria(nomeCasa) || EhPlaceholderEliminatoria(nomeVis))
                {
                    _logger.LogDebug("[Ogol] Ignorando jogo com placeholder: {Casa} x {Vis}", nomeCasa, nomeVis);
                    continue;
                }

                string? linkCasaNorm = NormalizarEquipeHref(linkCasaRaw);
                string? linkVisNorm = NormalizarEquipeHref(linkVisRaw);
                string? linkCasaEdicao = !string.IsNullOrWhiteSpace(linkCasaRaw)
                    ? (linkCasaRaw.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? linkCasaRaw : BaseUrl + linkCasaRaw)
                    : null;
                string? linkVisEdicao = !string.IsNullOrWhiteSpace(linkVisRaw)
                    ? (linkVisRaw.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? linkVisRaw : BaseUrl + linkVisRaw)
                    : null;

                var urlAbsoluta = href.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? href : BaseUrl + href;

                jogos.Add(new TransfermarktJogoInfo
                {
                    NomeTimeCasa = nomeCasa,
                    NomeTimeVisitante = nomeVis,
                    LinkTimeCasa = linkCasaNorm,
                    LinkTimeVisitante = linkVisNorm,
                    LinkTimeCasaComEdicao = linkCasaEdicao,
                    LinkTimeVisitanteComEdicao = linkVisEdicao,
                    EscudoTimeCasa = escudoCasa,
                    EscudoTimeVisitante = escudoVis,
                    PlacarCasa = pc,
                    PlacarVisitante = pv,
                    Data = dataJogo,
                    Rodada = rodadaAtualFix,
                    Grupo = grupoAtualFix,
                    LinkDetalhes = urlAbsoluta
                });

                _logger.LogInformation(
                    "[Ogol] Jogo: {Casa} x {Vis} | Grupo={G} | {Placar} | {Data}",
                    nomeCasa, nomeVis, grupoAtualFix ?? "–",
                    pc.HasValue ? $"{pc}:{pv}" : "–",
                    dataJogo?.ToString("dd/MM/yyyy") ?? "–");
            }
        }

        // ── Detecta URLs adicionais de jornadas e fases no #page_submenu ──
        private List<string> ExtrairUrlsPaginasAdicionais(HtmlDocument doc, string urlInicial)
        {
            var urls = new List<string>();
            var submenu = doc.DocumentNode.SelectSingleNode("//*[@id='page_submenu']");
            if (submenu == null) return urls;

            Uri? uri = null;
            try { uri = new Uri(urlInicial); } catch { return urls; }
            var qs = HttpUtility.ParseQueryString(uri.Query);
            var faseAtual = qs["fase"];
            var jornada_atual = qs["jornada_in"] ?? "1";
            var baseCompUrl = uri.GetLeftPart(UriPartial.Path);

            // 1. Outras jornadas
            if (faseAtual != null)
            {
                var ehPontosCorridos = string.IsNullOrEmpty(qs["grupo"]);

                if (ehPontosCorridos)
                {
                    // Pontos corridos (ex.: Brasileirão): gera rodadas 01–38 diretamente,
                    // pois o dropdown ogol só lista rodadas já jogadas.
                    for (int r = 1; r <= 38; r++)
                    {
                        var val = r.ToString("D2");
                        if (val == jornada_atual) continue;
                        urls.Add($"{baseCompUrl}?fase={faseAtual}&jornada_in={val}");
                    }
                }
                else
                {
                    // Fase de grupos com grupos (ex.: Copa do Mundo)
                    var opcoes = submenu.SelectNodes(".//select[@name='jornada_in']/option");
                    if (opcoes != null)
                    {
                        foreach (var opt in opcoes)
                        {
                            var val = opt.GetAttributeValue("value", "").Trim();
                            if (string.IsNullOrEmpty(val) || val == "0" || val == jornada_atual) continue;
                            urls.Add($"{baseCompUrl}?fase={faseAtual}&jornada_in={val}&grupo=0");
                        }
                    }
                }
            }

            // 2. Outras fases (eliminatórias) via links com fase= no submenu
            var fasesVistas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (faseAtual != null) fasesVistas.Add(faseAtual);

            var faseLinks = submenu.SelectNodes(".//a[contains(@href,'fase=')]");
            if (faseLinks != null)
            {
                foreach (var a in faseLinks)
                {
                    var href = a.GetAttributeValue("href", "").Trim();
                    var m = Regex.Match(href, @"fase=(\d+)");
                    if (!m.Success) continue;
                    var fase = m.Groups[1].Value;
                    if (!fasesVistas.Add(fase)) continue;
                    urls.Add(href.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                        ? href : BaseUrl + href);
                }
            }

            return urls;
        }

        // Extrai o número da rodada do "RODADA X" que precede o #fixture_games.
        // Sobe na DOM até achar um nó com texto "RODADA N" em qualquer h2/h3/h4 ou div.round.
        private static int ExtrairRodadaDaSecao(HtmlNode fixtureDiv)
        {
            var ancestor = fixtureDiv.ParentNode;
            int depth = 0;
            while (ancestor != null && ancestor.Name != "#document" && depth < 6)
            {
                // Tenta via div.round (estrutura do Brasileirão ogol)
                var roundDiv = ancestor.SelectSingleNode(".//div[contains(@class,'round')]");
                if (roundDiv != null)
                {
                    var mRound = Regex.Match(
                        HtmlEntity.DeEntitize(roundDiv.InnerText), @"RODADA\s+(\d+)", RegexOptions.IgnoreCase);
                    if (mRound.Success) return int.Parse(mRound.Groups[1].Value);
                }

                // Tenta em qualquer h2/h3/h4 no escopo atual
                foreach (var tag in new[] { "h3", "h2", "h4" })
                {
                    var nodes = ancestor.SelectNodes($".//{tag}");
                    if (nodes == null) continue;
                    foreach (var node in nodes)
                    {
                        var mH = Regex.Match(
                            HtmlEntity.DeEntitize(node.InnerText), @"RODADA\s+(\d+)", RegexOptions.IgnoreCase);
                        if (mH.Success) return int.Parse(mH.Groups[1].Value);
                    }
                }

                ancestor = ancestor.ParentNode;
                depth++;
            }
            return 0;
        }

        // Detecta nomes de times que são placeholders de fases eliminatórias ainda não definidas
        // Ex.: "1A", "2B", "1K", "Winner Match 99", "Loser Match 101", "3 D/E/I/J/L"
        private static bool EhPlaceholderEliminatoria(string nome)
        {
            if (string.IsNullOrWhiteSpace(nome)) return false;
            // "1A", "2B", "1K" — posição no grupo
            if (Regex.IsMatch(nome, @"^\d+[A-Z]$")) return true;
            // "Winner Match 99", "Loser Match 101"
            if (Regex.IsMatch(nome, @"^(Winner|Loser)\s+Match\s+\d+$", RegexOptions.IgnoreCase)) return true;
            // "3 D/E/I/J/L", "3 A/B/C/D/F" — terceiro colocado de múltiplos grupos
            if (Regex.IsMatch(nome, @"^\d+\s+[A-Z](/[A-Z])+$")) return true;
            return false;
        }

        private string? NormalizarEquipeHref(string? href)
        {
            if (string.IsNullOrWhiteSpace(href)) return null;
            var clean = href.Contains('?') ? href.Substring(0, href.IndexOf('?')) : href;
            return clean.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? clean : BaseUrl + clean;
        }

        // ══════════════════════════════════════════════════════════════
        // SCRAPING ─ Detalhes do jogo (escalação + gols + cartões)
        // URL esperada: https://www.ogol.com.br/jogo/YYYY-MM-DD-t1-t2/ID
        // ══════════════════════════════════════════════════════════════

        public async Task<DetalhesJogoTM?> BuscarDetalhesJogoAsync(
            string url, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                url = BaseUrl + url;

            string html;
            try
            {
                await Task.Delay(1500, ct);
                html = await _httpClient.GetStringAsync(url, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Ogol] Erro ao buscar detalhes: {Url}", url);
                return null;
            }

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var detalhes = new DetalhesJogoTM
            {
                FormacaoCasa = ExtrairFormacaoDoPitch(doc, casa: true),
                FormacaoVisitante = ExtrairFormacaoDoPitch(doc, casa: false)
            };

            // ── Placar ─────────────────────────────────────────────────────
            // ogol: <div class="match-header-vs"><a href="/jogo/...">2-0</a></div>
            var vsNode = doc.DocumentNode.SelectSingleNode(
                "//div[contains(@class,'match-header-vs')]//a");
            if (vsNode != null)
            {
                var m = Regex.Match(vsNode.InnerText.Trim(), @"^(\d{1,2})-(\d{1,2})$");
                if (m.Success)
                {
                    detalhes.PlacarCasa = int.Parse(m.Groups[1].Value);
                    detalhes.PlacarVisitante = int.Parse(m.Groups[2].Value);
                }
            }

            // ── Escalações — só tenta se o jogo foi realizado ──────────────
            // Jogo futuro não tem escalação; tentar gera dados inválidos
            if (!detalhes.PlacarCasa.HasValue)
            {
                _logger.LogInformation("[Ogol] Jogo sem placar (ainda não realizado), escalação não importada.");
                return detalhes;
            }

            // Treinadores
            (detalhes.TreinadorCasaNome, detalhes.TreinadorCasaLink) = ExtrairTreinadorDaEquipe(doc, casa: true);
            (detalhes.TreinadorVisitanteNome, detalhes.TreinadorVisitanteLink) = ExtrairTreinadorDaEquipe(doc, casa: false);

            detalhes.EscalacaoInicialCasa = ExtrairEscalacaoDoBloco(doc, casa: true);
            detalhes.EscalacaoInicialVisitante = ExtrairEscalacaoDoBloco(doc, casa: false);

            // Copia INICIAL → FINAL (ogol não separa explicitamente)
            detalhes.EscalacaoFinalCasa = detalhes.EscalacaoInicialCasa
                .Select(j => j with { Fase = "FINAL" }).ToList();
            detalhes.EscalacaoFinalVisitante = detalhes.EscalacaoInicialVisitante
                .Select(j => j with { Fase = "FINAL" }).ToList();

            // ── Gols e Eventos ─────────────────────────────────────────────
            detalhes.Gols = ExtrairGolsOgol(doc);
            detalhes.Eventos = ExtrairEventosOgol(doc);

            _logger.LogInformation(
                "[Ogol] Detalhes OK: Casa={C}t+{CR}r | Vis={V}t+{VR}r | Gols={G} | Eventos={E}",
                detalhes.EscalacaoInicialCasa.Count(j => j.Titular),
                detalhes.EscalacaoInicialCasa.Count(j => !j.Titular),
                detalhes.EscalacaoInicialVisitante.Count(j => j.Titular),
                detalhes.EscalacaoInicialVisitante.Count(j => !j.Titular),
                detalhes.Gols.Count,
                detalhes.Eventos.Count);

            return detalhes;
        }

        private List<JogadorEscalacaoTM> ExtrairEscalacaoDoBloco(HtmlDocument doc, bool casa)
        {
            // ogol: div#game_report > div.zz-tpl-row.game_report > div.zz-tpl-col.is-6.fl-c[1|2]
            // Coluna 1 = time da casa (right no header), coluna 2 = visitante (left no header)
            int colIdx = casa ? 1 : 2;
            var seletor = $"//div[@id='game_report']//div[contains(@class,'zz-tpl-col')][{colIdx}]//div[contains(@class,'player')]";

            var playerNodes = doc.DocumentNode.SelectNodes(seletor);
            if (playerNodes == null || playerNodes.Count == 0)
            {
                _logger.LogWarning("[Ogol] game_report não encontrado para {Lado}", casa ? "CASA" : "VISITANTE");
                return new List<JogadorEscalacaoTM>();
            }

            var lista = new List<JogadorEscalacaoTM>();
            var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var playerDiv in playerNodes)
            {
                // Nome: div.text > a[href*=/jogador/]
                var linkNode = playerDiv.SelectSingleNode(
                    ".//div[contains(@class,'text')]//a[contains(@href,'/jogador/')]");
                if (linkNode == null) continue;

                var href = linkNode.GetAttributeValue("href", "").Trim();
                if (!vistos.Add(href)) continue;

                var nome = HtmlEntity.DeEntitize(linkNode.InnerText.Trim());
                if (string.IsNullOrWhiteSpace(nome) || Regex.IsMatch(nome, @"^\d+$")) continue;

                // Número da camisa e ID externo: div.number[data-player-id]
                int? numero = null;
                long? idExterno = null;
                var numDiv = playerDiv.SelectSingleNode(".//div[contains(@class,'number')]");
                if (numDiv != null)
                {
                    if (int.TryParse(numDiv.InnerText.Trim(), out var n)) numero = n;
                    if (long.TryParse(numDiv.GetAttributeValue("data-player-id", ""), out var id)) idExterno = id;
                }

                lista.Add(new JogadorEscalacaoTM
                {
                    Nome = nome,
                    Numero = numero,
                    Posicao = "",
                    Titular = lista.Count < 11,
                    IdExterno = idExterno,
                    JogadorLink = UrlAbsoluta(href),
                    Fase = "INICIAL"
                });
            }

            _logger.LogInformation("[Ogol] Escalação {Lado}: {N} jogadores ({T} titulares)",
                casa ? "CASA" : "VISITANTE", lista.Count, lista.Count(j => j.Titular));

            return lista;
        }

        private static List<JogadorEscalacaoTM> LinksParaJogadores(
            IEnumerable<HtmlNode> nos, bool titular)
        {
            var lista = new List<JogadorEscalacaoTM>();
            var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var no in nos)
            {
                var href = no.GetAttributeValue("href", "").Trim();
                if (!Regex.IsMatch(href, @"/jogador/[^/]+/\d+")) continue;
                if (!vistos.Add(href)) continue;

                // Texto visível do link: ignora se for só número (camisa) ou vazio
                var textoNo = HtmlEntity.DeEntitize(no.InnerText.Trim());
                if (string.IsNullOrWhiteSpace(textoNo)) continue;
                if (Regex.IsMatch(textoNo, @"^\d+$")) continue;

                // Se o texto inclui número+nome (ex: "7 Vinicius Jr"), separa o nome
                var nome = Regex.Replace(textoNo, @"^\d+\s+", "").Trim();
                if (string.IsNullOrWhiteSpace(nome)) continue;

                // Número de camisa (procura irmão ou pai com número)
                int? numero = null;
                if (int.TryParse(Regex.Match(textoNo, @"^(\d+)").Groups[1].Value, out var nFromText))
                    numero = nFromText;
                if (numero == null)
                {
                    var numNode = no.ParentNode?.SelectSingleNode(
                        ".//*[contains(@class,'number') or contains(@class,'numero') or contains(@class,'shirt')]");
                    if (numNode != null && int.TryParse(numNode.InnerText.Trim(), out var n))
                        numero = n;
                }

                var idMatch = Regex.Match(href, @"/jogador/[^/]+/(\d+)");
                var urlAbsoluta = href.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? href : BaseUrl + href;

                lista.Add(new JogadorEscalacaoTM
                {
                    Nome = nome,
                    Numero = numero,
                    Posicao = "",
                    Titular = titular,
                    IdExterno = idMatch.Success ? long.Parse(idMatch.Groups[1].Value) : null,
                    JogadorLink = urlAbsoluta,
                    Fase = "INICIAL"
                });
            }

            return lista;
        }

        private static void SepararTitularesEReservas(List<JogadorEscalacaoTM> jogadores)
        {
            // Marca como reserva tudo além dos 11 primeiros
            for (int i = 0; i < jogadores.Count; i++)
            {
                var j = jogadores[i];
                bool deveSerTitular = i < 11;
                if (j.Titular != deveSerTitular)
                    jogadores[i] = j with { Titular = deveSerTitular };
            }
        }

        // Verifica se o texto após o span de minuto contém "(g.c.)" (gol contra)
        private static bool DetectarGolContra(HtmlNode link, HtmlNode? timeSpan)
        {
            var start = timeSpan ?? link;
            var node = start.NextSibling;
            for (int i = 0; i < 6 && node != null; i++)
            {
                // Para ao encontrar o próximo link de jogador (próximo gol)
                if (node.Name == "a" && node.GetAttributeValue("href", "").Contains("/jogador/"))
                    break;

                var txt = HtmlEntity.DeEntitize(
                    node.NodeType == HtmlAgilityPack.HtmlNodeType.Text
                        ? node.InnerText
                        : node.InnerText);

                if (txt.Contains("g.c.", StringComparison.OrdinalIgnoreCase))
                    return true;

                node = node.NextSibling;
            }
            return false;
        }

        private List<GolTM> ExtrairGolsOgol(HtmlDocument doc)
        {
            var gols = new List<GolTM>();
            var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // ogol: div.match-header-scorers.right = CASA, .left = VISITANTE
            // Formato: <a href="/jogador/...">Nome</a> <span class="time">9'</span>
            foreach (var (lado, isCasa) in new[] { ("right", true), ("left", false) })
            {
                var bloco = doc.DocumentNode.SelectSingleNode(
                    $"//div[contains(@class,'match-header-scorers') and contains(@class,'{lado}')]");
                if (bloco == null) continue;

                var links = bloco.SelectNodes(".//a[contains(@href,'/jogador/')]");
                if (links == null) continue;

                foreach (var link in links)
                {
                    var nome = HtmlEntity.DeEntitize(link.InnerText.Trim());
                    var href = link.GetAttributeValue("href", "");

                    // Minuto: span.time imediatamente após o link
                    var timeSpan = link.SelectSingleNode("following-sibling::span[contains(@class,'time')]");
                    int minuto = 0;
                    if (timeSpan != null)
                    {
                        var m = Regex.Match(HtmlEntity.DeEntitize(timeSpan.InnerText), @"(\d+)");
                        if (m.Success) minuto = int.Parse(m.Groups[1].Value);
                    }

                    bool contra = DetectarGolContra(link, timeSpan);
                    var chave = $"{NormalizarLinkOgol(href)}|{minuto}";
                    if (!vistos.Add(chave)) continue;

                    gols.Add(new GolTM
                    {
                        NomeJogador = nome,
                        IdExterno = ExtrairIdOgol(href),
                        Minuto = minuto,
                        IsTimeCasa = isCasa,
                        Contra = contra
                    });
                }
            }

            return gols;
        }

        private List<TransfermarktEventoInfo> ExtrairEventosOgol(HtmlDocument doc)
        {
            var eventos = new List<TransfermarktEventoInfo>();

            // ── Gols (via match-header-scorers) ────────────────────────────
            // right = CASA, left = VISITANTE
            // Gol contra (g.c.) aparece nos dois blocos — deduplicar por (href, minuto)
            var golsVistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (lado, isCasa) in new[] { ("right", true), ("left", false) })
            {
                var bloco = doc.DocumentNode.SelectSingleNode(
                    $"//div[contains(@class,'match-header-scorers') and contains(@class,'{lado}')]");
                if (bloco == null) continue;

                var links = bloco.SelectNodes(".//a[contains(@href,'/jogador/')]");
                if (links == null) continue;

                foreach (var link in links)
                {
                    var nome = HtmlEntity.DeEntitize(link.InnerText.Trim());
                    var href = link.GetAttributeValue("href", "");
                    var timeSpan = link.SelectSingleNode("following-sibling::span[contains(@class,'time')]");
                    int minuto = 0;
                    if (timeSpan != null)
                    {
                        var m = Regex.Match(HtmlEntity.DeEntitize(timeSpan.InnerText), @"(\d+)");
                        if (m.Success) minuto = int.Parse(m.Groups[1].Value);
                    }

                    // Detecta gol contra: "(g.c.)" nos nós de texto após o span de minuto
                    bool contra = DetectarGolContra(link, timeSpan);

                    // Deduplicação: mesmo jogador no mesmo minuto não entra duas vezes
                    var chave = $"{NormalizarLinkOgol(href)}|{minuto}";
                    if (!golsVistos.Add(chave)) continue;

                    eventos.Add(new TransfermarktEventoInfo
                    {
                        Tipo = "Gol",
                        JogadorNome = nome,
                        JogadorLink = UrlAbsoluta(href),
                        Minuto = minuto,
                        IsTimeCasa = isCasa,
                        Contra = contra
                    });
                }
            }

            // ── Cartões (via div.events de cada jogador na escalação) ───────
            // ogol: span.icn_zerozero.yellow = amarelo, .red = vermelho
            // Minuto: próxima div irmã após o span
            // Coluna 1 = CASA, coluna 2 = VISITANTE
            foreach (var (colIdx, isCasa) in new[] { (1, true), (2, false) })
            {
                var playerSel = $"//div[@id='game_report']//div[contains(@class,'zz-tpl-col')][{colIdx}]//div[contains(@class,'player')]";
                var players = doc.DocumentNode.SelectNodes(playerSel);
                if (players == null) continue;

                foreach (var playerDiv in players)
                {
                    var linkNode = playerDiv.SelectSingleNode(
                        ".//div[contains(@class,'text')]//a[contains(@href,'/jogador/')]");
                    if (linkNode == null) continue;

                    var playerHref = linkNode.GetAttributeValue("href", "");
                    var playerNome = HtmlEntity.DeEntitize(linkNode.InnerText.Trim());

                    // Cartões dentro de div.events
                    var cardSpans = playerDiv.SelectNodes(
                        ".//div[@class='events']//span[contains(@class,'icn_zerozero') and (contains(@class,'yellow') or contains(@class,'red'))]");
                    if (cardSpans == null) continue;

                    foreach (var cardSpan in cardSpans)
                    {
                        bool isVermelho = cardSpan.GetAttributeValue("class", "").Contains("red");

                        var proximo = cardSpan.NextSibling;
                        while (proximo != null && proximo.NodeType != HtmlAgilityPack.HtmlNodeType.Element)
                            proximo = proximo.NextSibling;

                        int minuto = 0;
                        if (proximo != null)
                        {
                            var mMin = Regex.Match(HtmlEntity.DeEntitize(proximo.InnerText), @"(\d+)");
                            if (mMin.Success) minuto = int.Parse(mMin.Groups[1].Value);
                        }

                        eventos.Add(new TransfermarktEventoInfo
                        {
                            Tipo = isVermelho ? "CartaoVermelho" : "CartaoAmarelo",
                            JogadorNome = playerNome,
                            JogadorLink = UrlAbsoluta(playerHref),
                            Minuto = minuto,
                            Detalhe = isVermelho ? "Vermelho" : "Amarelo",
                            IsTimeCasa = isCasa
                        });
                    }

                    // ── Assistências (ícone chuteira com title="publico") ────────
                    // ogol usa span.icn_zerozero com title="publico" para assistências
                    var assistSpans = playerDiv.SelectNodes(
                        ".//div[@class='events']//span[@title='publico' or contains(@class,'boot') or contains(@class,'shoe')]");
                    if (assistSpans != null)
                    {
                        foreach (var assistSpan in assistSpans)
                        {
                            // Não confundir com cartões (que também são icn_zerozero)
                            var cls = assistSpan.GetAttributeValue("class", "");
                            if (cls.Contains("yellow") || cls.Contains("red")) continue;

                            var proximo = assistSpan.NextSibling;
                            while (proximo != null && proximo.NodeType != HtmlAgilityPack.HtmlNodeType.Element)
                                proximo = proximo.NextSibling;

                            int minuto = 0;
                            if (proximo != null)
                            {
                                var mMin = Regex.Match(HtmlEntity.DeEntitize(proximo.InnerText), @"(\d+)");
                                if (mMin.Success) minuto = int.Parse(mMin.Groups[1].Value);
                            }

                            eventos.Add(new TransfermarktEventoInfo
                            {
                                Tipo = "Assistencia",
                                JogadorNome = playerNome,
                                JogadorLink = UrlAbsoluta(playerHref),
                                Minuto = minuto,
                                IsTimeCasa = isCasa
                            });
                        }
                    }
                }
            }

            return eventos;
        }

        // ══════════════════════════════════════════════════════════════
        // SCRAPING ─ Perfil do jogador
        // URL esperada: https://www.ogol.com.br/jogador/nome/ID
        // ══════════════════════════════════════════════════════════════

        private async Task<InfoPerfilJogadorTM?> BuscarInfoPerfilJogador(
            string profileUrl, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(profileUrl)) return null;
            if (!profileUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                profileUrl = BaseUrl + profileUrl;

            try
            {
                await Task.Delay(800, ct);
                var html = await _httpClient.GetStringAsync(profileUrl, ct);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // ── Foto ───────────────────────────────────────────────────
                string? fotoUrl =
                    doc.DocumentNode
                        .SelectSingleNode("//img[contains(@src,'staticzz.com/img/jogadores')]")
                        ?.GetAttributeValue("src", "")?.Trim()
                    ?? doc.DocumentNode
                        .SelectSingleNode("//img[contains(@src,'/img/jogadores/')]")
                        ?.GetAttributeValue("src", "")?.Trim()
                    ?? doc.DocumentNode
                        .SelectSingleNode("//meta[@property='og:image']")
                        ?.GetAttributeValue("content", "")?.Trim();

                // ── Posição ────────────────────────────────────────────────
                string? posicao = null;
                var posicaoPadrao = new[]
                {
                    "//dt[contains(translate(text(),'POSIÇÃO','posição'),'posição') or contains(text(),'Posição')]/following-sibling::dd[1]",
                    "//*[contains(@class,'posicao') or contains(@class,'position')]",
                    "//*[text()='Posição']/following-sibling::*[1]",
                    "//*[contains(text(),'Posição:')]",
                };
                foreach (var s in posicaoPadrao)
                {
                    var no = doc.DocumentNode.SelectSingleNode(s);
                    if (no == null) continue;
                    var v = HtmlEntity.DeEntitize(Regex.Replace(no.InnerText, @"\s+", " ").Trim());
                    v = Regex.Replace(v, @"Posição:?\s*", "", RegexOptions.IgnoreCase).Trim();
                    if (!string.IsNullOrWhiteSpace(v)) { posicao = v; break; }
                }

                // ── Nacionalidade e Data de nascimento ────────────────────
                // Estratégia 1: JSON-LD — mais confiável, não confunde logos de competição com países
                string? nacionalidade = null;
                DateTime? dataNasc = null;
                var jsonLdNodes = doc.DocumentNode.SelectNodes(
                    "//script[@type='application/ld+json']");
                if (jsonLdNodes != null)
                {
                    foreach (var scriptNode in jsonLdNodes)
                    {
                        var jt = scriptNode.InnerText ?? "";

                        // Nacionalidade
                        if (nacionalidade == null)
                        {
                            var nm = Regex.Match(jt, @"""nationality""\s*:\s*""([^""]+)""");
                            if (nm.Success)
                                nacionalidade = NacionalidadesHelper.Normalizar(nm.Groups[1].Value.Trim());
                        }

                        // Data de nascimento
                        if (dataNasc == null)
                        {
                            var bm = Regex.Match(jt,
                                @"""birthDate""\s*:\s*""(\d{4}-\d{2}-\d{2})""");
                            if (bm.Success &&
                                DateTime.TryParseExact(bm.Groups[1].Value, "yyyy-MM-dd",
                                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var dn))
                                dataNasc = dn;
                        }

                        if (nacionalidade != null && dataNasc != null) break;
                    }
                }

                // Estratégia 2 (fallback): img de bandeira real — exclui logos de competições (alt="Logo: ...")
                if (nacionalidade == null)
                {
                    // Bandeiras de país têm alt sem "Logo:" — ex: alt="Rep. Tcheca", alt="Brasil"
                    var flagImg = doc.DocumentNode.SelectSingleNode(
                        "//img[(contains(@src,'bandeiras') or contains(@src,'flags'))" +
                        " and not(contains(@alt,'Logo:')) and string-length(@alt) > 1]");
                    if (flagImg != null)
                    {
                        var alt = HtmlEntity.DeEntitize(
                            (flagImg.GetAttributeValue("title", "").Trim()
                             is { Length: > 0 } t ? t
                             : flagImg.GetAttributeValue("alt", "").Trim()));
                        if (!string.IsNullOrWhiteSpace(alt))
                            nacionalidade = NacionalidadesHelper.Normalizar(alt);
                    }
                }

                // Estratégia 3: elementos HTML com texto de nascimento (dd/MM/yyyy ou yyyy-MM-dd)
                if (dataNasc == null)
                {
                    var nascPadrao = new[]
                    {
                        "//dt[contains(text(),'Nascimento') or contains(text(),'nascimento')]/following-sibling::dd[1]",
                        "//*[contains(@class,'nascimento') or contains(@class,'birthday')]",
                        "//*[contains(text(),'Nascimento:')]",
                    };
                    foreach (var s in nascPadrao)
                    {
                        var no = doc.DocumentNode.SelectSingleNode(s);
                        if (no == null) continue;
                        var texto = HtmlEntity.DeEntitize(no.InnerText.Trim());
                        var m = Regex.Match(texto, @"(\d{4}-\d{2}-\d{2})");
                        if (m.Success &&
                            DateTime.TryParseExact(m.Groups[1].Value, "yyyy-MM-dd",
                                CultureInfo.InvariantCulture, DateTimeStyles.None, out var dn))
                        { dataNasc = dn; break; }
                        m = Regex.Match(texto, @"(\d{2}/\d{2}/\d{4})");
                        if (m.Success &&
                            DateTime.TryParseExact(m.Groups[1].Value, "dd/MM/yyyy",
                                CultureInfo.InvariantCulture, DateTimeStyles.None, out dn))
                        { dataNasc = dn; break; }
                    }
                }

                _logger.LogInformation(
                    "[OgolPerfil] {Url} → Pos={P} | Nac={N} | Nasc={D} | Foto={F}",
                    profileUrl,
                    posicao ?? "–",
                    nacionalidade ?? "–",
                    dataNasc?.ToString("dd/MM/yyyy") ?? "–",
                    string.IsNullOrEmpty(fotoUrl) ? "–" : "ok");

                return new InfoPerfilJogadorTM(posicao, nacionalidade, fotoUrl, dataNasc);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[OgolPerfil] Erro: {Url}", profileUrl);
                return null;
            }
        }

        // ══════════════════════════════════════════════════════════════
        // SCRAPING ─ Perfil do treinador
        // URL esperada: https://www.ogol.com.br/treinador/nome/ID
        // ══════════════════════════════════════════════════════════════

        public async Task<InfoPerfilJogadorTM?> BuscarDadosTreinadorAsync(
            string profileUrl, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(profileUrl)) return null;
            if (!profileUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                profileUrl = BaseUrl + profileUrl;

            try
            {
                await Task.Delay(800, ct);
                var html = await _httpClient.GetStringAsync(profileUrl, ct);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // Foto — treinadores usam /img/treinadores/ ou /img/jogadores/
                string? fotoUrl =
                    doc.DocumentNode
                        .SelectSingleNode("//img[contains(@src,'staticzz.com/img/treinadores')]")
                        ?.GetAttributeValue("src", "")?.Trim()
                    ?? doc.DocumentNode
                        .SelectSingleNode("//img[contains(@src,'/img/treinadores/')]")
                        ?.GetAttributeValue("src", "")?.Trim()
                    ?? doc.DocumentNode
                        .SelectSingleNode("//img[contains(@src,'staticzz.com/img/jogadores')]")
                        ?.GetAttributeValue("src", "")?.Trim()
                    ?? doc.DocumentNode
                        .SelectSingleNode("//meta[@property='og:image']")
                        ?.GetAttributeValue("content", "")?.Trim();

                // Filtra imagens genéricas/placeholder
                if (fotoUrl != null && (fotoUrl.Contains("no_photo") || fotoUrl.Contains("default")))
                    fotoUrl = null;

                string? nacionalidade = null;
                DateTime? dataNasc = null;

                // JSON-LD (mais confiável)
                var jsonLdNodes = doc.DocumentNode.SelectNodes("//script[@type='application/ld+json']");
                if (jsonLdNodes != null)
                {
                    foreach (var scriptNode in jsonLdNodes)
                    {
                        var jt = scriptNode.InnerText ?? "";
                        if (nacionalidade == null)
                        {
                            var nm = Regex.Match(jt, @"""nationality""\s*:\s*""([^""]+)""");
                            if (nm.Success)
                                nacionalidade = NacionalidadesHelper.Normalizar(nm.Groups[1].Value.Trim());
                        }
                        if (dataNasc == null)
                        {
                            var bm = Regex.Match(jt, @"""birthDate""\s*:\s*""(\d{4}-\d{2}-\d{2})""");
                            if (bm.Success &&
                                DateTime.TryParseExact(bm.Groups[1].Value, "yyyy-MM-dd",
                                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var dn))
                                dataNasc = dn;
                        }
                        if (nacionalidade != null && dataNasc != null) break;
                    }
                }

                // Fallback: bandeira
                if (nacionalidade == null)
                {
                    var flagImg = doc.DocumentNode.SelectSingleNode(
                        "//img[(contains(@src,'bandeiras') or contains(@src,'flags'))" +
                        " and not(contains(@alt,'Logo:')) and string-length(@alt) > 1]");
                    if (flagImg != null)
                    {
                        var alt = HtmlEntity.DeEntitize(
                            (flagImg.GetAttributeValue("title", "").Trim() is { Length: > 0 } t
                                ? t : flagImg.GetAttributeValue("alt", "").Trim()));
                        if (!string.IsNullOrWhiteSpace(alt))
                            nacionalidade = NacionalidadesHelper.Normalizar(alt);
                    }
                }

                // Fallback: data de nascimento em HTML
                if (dataNasc == null)
                {
                    foreach (var s in new[]
                    {
                        "//dt[contains(text(),'Nascimento') or contains(text(),'nascimento')]/following-sibling::dd[1]",
                        "//*[contains(@class,'nascimento') or contains(@class,'birthday')]",
                    })
                    {
                        var no = doc.DocumentNode.SelectSingleNode(s);
                        if (no == null) continue;
                        var texto = HtmlEntity.DeEntitize(no.InnerText.Trim());
                        var m = Regex.Match(texto, @"(\d{4}-\d{2}-\d{2})");
                        if (m.Success && DateTime.TryParseExact(m.Groups[1].Value, "yyyy-MM-dd",
                                CultureInfo.InvariantCulture, DateTimeStyles.None, out var dn))
                        { dataNasc = dn; break; }
                        m = Regex.Match(texto, @"(\d{2}/\d{2}/\d{4})");
                        if (m.Success && DateTime.TryParseExact(m.Groups[1].Value, "dd/MM/yyyy",
                                CultureInfo.InvariantCulture, DateTimeStyles.None, out dn))
                        { dataNasc = dn; break; }
                    }
                }

                _logger.LogInformation(
                    "[OgolTreinador] {Url} → Nac={N} | Nasc={D} | Foto={F}",
                    profileUrl, nacionalidade ?? "–",
                    dataNasc?.ToString("dd/MM/yyyy") ?? "–",
                    string.IsNullOrEmpty(fotoUrl) ? "–" : "ok");

                return new InfoPerfilJogadorTM(null, nacionalidade, fotoUrl, dataNasc);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[OgolTreinador] Erro: {Url}", profileUrl);
                return null;
            }
        }

        // ══════════════════════════════════════════════════════════════
        // SCRAPING ─ Elenco do time pela página /equipe/SLUG?edicao_id=X
        // Usado para importar jogadores de times que ainda não jogaram
        // ══════════════════════════════════════════════════════════════

        public async Task<List<JogadorEscalacaoTM>> BuscarElencoTimePorLink(
            string linkEquipe, CancellationToken ct = default)
        {
            var jogadores = new List<JogadorEscalacaoTM>();
            if (string.IsNullOrWhiteSpace(linkEquipe)) return jogadores;
            if (!linkEquipe.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                linkEquipe = BaseUrl + linkEquipe;

            string html;
            try
            {
                await Task.Delay(1000, ct);
                html = await _httpClient.GetStringAsync(linkEquipe, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[OgolElenco] Erro ao buscar elenco: {Url}", linkEquipe);
                return jogadores;
            }

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var squadDiv = doc.DocumentNode.SelectSingleNode("//div[@id='team_squad']");
            if (squadDiv == null)
            {
                _logger.LogWarning("[OgolElenco] #team_squad não encontrado em: {Url}", linkEquipe);
                return jogadores;
            }

            string posicaoAtual = "";

            // Percorre todos os nós filhos diretos do squad: div.section (posição) e div.staff_line
            foreach (var node in squadDiv.ChildNodes)
            {
                if (node.NodeType != HtmlNodeType.Element) continue;

                // Cabeçalho de posição
                if (node.GetAttributeValue("class", "") == "section")
                {
                    posicaoAtual = HtmlEntity.DeEntitize(node.InnerText.Trim());
                    continue;
                }

                // Linha com múltiplos jogadores (staff_line contém vários div.staff)
                var staffNodes = node.SelectNodes(".//div[contains(@class,'staff') and not(contains(@class,'staff_line'))]");
                if (staffNodes == null) continue;

                foreach (var staff in staffNodes)
                {
                    // Número de camisa
                    var numNode = staff.SelectSingleNode(".//div[contains(@class,'number')]");
                    int numero = 0;
                    if (numNode != null)
                        int.TryParse(HtmlEntity.DeEntitize(numNode.InnerText.Trim()), out numero);

                    // Foto (background-image da div.photo)
                    string? fotoUrl = null;
                    var photoDiv = staff.SelectSingleNode(".//div[contains(@class,'photo')]");
                    if (photoDiv != null)
                    {
                        var style = photoDiv.GetAttributeValue("style", "");
                        var m = Regex.Match(style, @"url\('([^']+)'\)");
                        if (m.Success) fotoUrl = m.Groups[1].Value;
                    }

                    // Nome e link
                    var linkNode = staff.SelectSingleNode(
                        ".//div[contains(@class,'text')]//a[contains(@href,'/jogador/')]");
                    if (linkNode == null) continue;

                    var nome = HtmlEntity.DeEntitize(linkNode.InnerText.Trim());
                    if (string.IsNullOrWhiteSpace(nome)) continue;

                    var jogadorHref = linkNode.GetAttributeValue("href", "");
                    // Remove edicao_id do link do jogador para armazenamento consistente
                    var jogadorLink = jogadorHref.Contains('?')
                        ? jogadorHref.Substring(0, jogadorHref.IndexOf('?')) : jogadorHref;
                    if (!jogadorLink.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                        jogadorLink = BaseUrl + jogadorLink;

                    // ID externo do ogol (último segmento numérico do path)
                    var idMatch = Regex.Match(jogadorLink, @"/(\d+)$");
                    int idExterno = idMatch.Success ? int.Parse(idMatch.Groups[1].Value) : 0;

                    jogadores.Add(new JogadorEscalacaoTM
                    {
                        Nome = nome,
                        Numero = numero,
                        Posicao = posicaoAtual,
                        JogadorLink = jogadorLink,
                        IdExterno = idExterno,
                        FotoUrl = fotoUrl
                    });
                }
            }

            _logger.LogInformation("[OgolElenco] {Url} → {N} jogadores encontrados", linkEquipe, jogadores.Count);
            return jogadores;
        }

        public async Task<string?> BuscarFotoJogador(Jogador jogador)
        {
            var link = jogador.linktransfermarket;
            if (string.IsNullOrWhiteSpace(link)) return null;

            try
            {
                var perfil = await BuscarInfoPerfilJogador(link, default);
                return perfil?.FotoUrl;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[OgolFoto] Erro: {Nome}", jogador.Nome);
                return null;
            }
        }

        // Sem formação no ogol → retorna null (chamador usa fallback 4-3-3)
        public Task<string?> BuscarGrupoDoJogoAsync(string linkDetalhes, CancellationToken ct = default)
            => Task.FromResult<string?>(null);

        // Acesso público ao perfil do jogador (usado em JogadoresController e AtualizarJogadoresSemDataService)
        public async Task<TransfermarktPlayerInfo?> BuscarJogadorPorLink(string? link)
        {
            if (string.IsNullOrWhiteSpace(link)) return null;
            var perfil = await BuscarInfoPerfilJogador(link, default);
            if (perfil == null) return null;
            return new TransfermarktPlayerInfo
            {
                DataNascimento = perfil.DataNascimento,
                Nacionalidade = perfil.Nacionalidade,
                Posicao = perfil.Posicao,
                LinkPerfil = link
            };
        }

        // Normaliza links ogol para armazenamento consistente
        public string NormalizarLink(string? url) => NormalizarLinkOgol(url ?? "");

        // ══════════════════════════════════════════════════════════════
        // PERSISTÊNCIA ─ Inclui ou atualiza jogo + escalação + eventos
        // (mesma lógica de TransfermarktService, adaptada para links ogol)
        // ══════════════════════════════════════════════════════════════

        public async Task IncluirOuAtualizarJogo(
            FutebolContext context,
            Competicao competicao,
            TransfermarktJogoInfo jogoWeb,
            Time timeCasa,
            Time timeVisitante,
            Guid cicloId,
            CancellationToken ct)
        {
            var dataJogo = jogoWeb.Data.HasValue
                ? DateTime.SpecifyKind(jogoWeb.Data.Value, DateTimeKind.Utc)
                : (DateTime?)null;

            // ── Anti-duplicata por rodada ──────────────────────────────────
            var jogosBanco = await context.Jogos
                .Where(j => j.CompeticaoId == competicao.Id &&
                            j.TimeCasaId == timeCasa.Id &&
                            j.TimeVisitanteId == timeVisitante.Id)
                .ToListAsync(ct);

            Jogo? jogoExistente = null;

            if (jogoWeb.Rodada > 0)
                jogoExistente = jogosBanco.FirstOrDefault(j => j.Rodada == jogoWeb.Rodada);

            if (jogoExistente == null && dataJogo.HasValue)
                jogoExistente = jogosBanco.FirstOrDefault(j =>
                    j.Data.HasValue &&
                    Math.Abs((j.Data.Value.Date - dataJogo.Value.Date).TotalDays) <= 2);

            if (jogoExistente != null)
            {
                // Atualiza dados existentes
                if (dataJogo.HasValue) jogoExistente.Data = dataJogo;
                if (!string.IsNullOrWhiteSpace(jogoWeb.LinkDetalhes))
                    jogoExistente.LinkDetalhes = jogoWeb.LinkDetalhes;
                jogoExistente.Rodada = jogoWeb.Rodada > 0 ? jogoWeb.Rodada : jogoExistente.Rodada;

                if (jogoWeb.PlacarCasa.HasValue &&
                    (jogoExistente.PlacarCasa != jogoWeb.PlacarCasa ||
                     jogoExistente.PlacarVisitante != jogoWeb.PlacarVisitante))
                {
                    jogoExistente.PlacarCasa = jogoWeb.PlacarCasa;
                    jogoExistente.PlacarVisitante = jogoWeb.PlacarVisitante;
                    jogoExistente.Status = "Finalizado";
                    jogoExistente.Atualizado = 1;
                }

                // Reimporta escalação se jogo finalizado e visitante ainda sem jogadores
                if (jogoWeb.PlacarCasa.HasValue &&
                    !string.IsNullOrWhiteSpace(jogoWeb.LinkDetalhes))
                {
                    var temVisitante = await context.Escalacoes
                        .AnyAsync(e => e.JogoId == jogoExistente.Id &&
                                       !e.IsTimeCasa && e.JogadorId != null, ct);
                    if (!temVisitante)
                        await ReimportarEscalacoesOgol(context, jogoExistente,
                            jogoWeb.LinkDetalhes, timeCasa, timeVisitante, cicloId, ct);
                }

                await context.SaveChangesAsync(ct);
                return;
            }

            // ── Busca detalhes para jogos finalizados ──────────────────────
            DetalhesJogoTM? detalhes = null;
            if (jogoWeb.PlacarCasa.HasValue && !string.IsNullOrWhiteSpace(jogoWeb.LinkDetalhes))
            {
                await Task.Delay(1500, ct);
                detalhes = await BuscarDetalhesJogoAsync(jogoWeb.LinkDetalhes, ct);
            }

            // ── Formações (sempre fallback 4-3-3 pois ogol não expõe) ──────
            var formacaoCasa = await ObterOuCriarFormacao(context,
                detalhes?.FormacaoCasa, ct);
            var formacaoVisitante = await ObterOuCriarFormacao(context,
                detalhes?.FormacaoVisitante, ct);

            // ── Cria o jogo ────────────────────────────────────────────────
            var jogo = new Jogo
            {
                CompeticaoId = competicao.Id,
                TimeCasa = timeCasa,
                TimeVisitante = timeVisitante,
                Data = dataJogo,
                Rodada = jogoWeb.Rodada,
                PlacarCasa = detalhes?.PlacarCasa ?? jogoWeb.PlacarCasa,
                PlacarVisitante = detalhes?.PlacarVisitante ?? jogoWeb.PlacarVisitante,
                Grupo = jogoWeb.Grupo,
                Status = jogoWeb.PlacarCasa.HasValue ? "Finalizado" : "Agendado",
                Atualizado = jogoWeb.PlacarCasa.HasValue ? 1 : 0,
                FormacaoCasaId = formacaoCasa.Id,
                FormacaoVisitanteId = formacaoVisitante.Id,
                LinkDetalhes = jogoWeb.LinkDetalhes
            };

            context.Jogos.Add(jogo);
            await context.SaveChangesAsync(ct);

            RegistrarLog(context, cicloId, "Jogo", "Criado",
                competicaoNome: competicao.Nome,
                timeNome: $"{timeCasa.Nome} × {timeVisitante.Nome}",
                jogoDescricao: jogoWeb.Rodada > 0
                    ? $"Rodada {jogoWeb.Rodada} | {dataJogo?.ToString("dd/MM/yyyy") ?? "sem data"}"
                    : dataJogo?.ToString("dd/MM/yyyy") ?? "sem data",
                detalhes: jogo.PlacarCasa.HasValue
                    ? $"Placar: {jogo.PlacarCasa}×{jogo.PlacarVisitante}"
                    : "Agendado");

            // ── Treinadores ────────────────────────────────────────────────────
            if (detalhes != null)
            {
                await ResolverOuCriarTreinadorAsync(context,
                    detalhes.TreinadorCasaNome, detalhes.TreinadorCasaLink, timeCasa.Id, ct);
                await ResolverOuCriarTreinadorAsync(context,
                    detalhes.TreinadorVisitanteNome, detalhes.TreinadorVisitanteLink, timeVisitante.Id, ct);
            }

            // ── Escalações ─────────────────────────────────────────────────
            if (detalhes != null &&
                (detalhes.EscalacaoInicialCasa.Any() || detalhes.EscalacaoInicialVisitante.Any()))
            {
                var posicoesCasa = await CarregarOuGerarPosicoes(context, formacaoCasa, detalhes.FormacaoCasa, ct);
                var posicoesVis  = await CarregarOuGerarPosicoes(context, formacaoVisitante, detalhes.FormacaoVisitante, ct);

                await AdicionarEscalacoesComJogadoresAsync(context, jogo,
                    detalhes.EscalacaoInicialCasa, timeCasa, true, "INICIAL", posicoesCasa, cicloId, ct);
                await AdicionarEscalacoesComJogadoresAsync(context, jogo,
                    detalhes.EscalacaoInicialVisitante, timeVisitante, false, "INICIAL", posicoesVis, cicloId, ct);

                var finalCasa = detalhes.EscalacaoFinalCasa.Any()
                    ? detalhes.EscalacaoFinalCasa
                    : detalhes.EscalacaoInicialCasa.Select(j => j with { Fase = "FINAL" }).ToList();
                var finalVis = detalhes.EscalacaoFinalVisitante.Any()
                    ? detalhes.EscalacaoFinalVisitante
                    : detalhes.EscalacaoInicialVisitante.Select(j => j with { Fase = "FINAL" }).ToList();

                await AdicionarEscalacoesComJogadoresAsync(context, jogo,
                    finalCasa, timeCasa, true, "FINAL", posicoesCasa, cicloId, ct);
                await AdicionarEscalacoesComJogadoresAsync(context, jogo,
                    finalVis, timeVisitante, false, "FINAL", posicoesVis, cicloId, ct);

                // Eventos (gols, cartões)
                if (detalhes.Eventos.Any() && !string.IsNullOrWhiteSpace(jogo.LinkDetalhes))
                {
                    var (g, a, c) = await ImportarEventosPorLinkAsync(context, jogo, jogo.LinkDetalhes!, ct);
                    _logger.LogInformation("[Ogol] Eventos jogo {Id}: {G} gols, {A} assists, {C} cartões",
                        jogo.Id, g, a, c);
                }
            }
            else
            {
                // Sem escalação: cria slots vazios pela formação
                AdicionarEscalacaoComPosicoes(context, jogo, formacaoCasa, true);
                AdicionarEscalacaoComPosicoes(context, jogo, formacaoVisitante, false);
            }

            await context.SaveChangesAsync(ct);

            _logger.LogInformation("[Ogol] Jogo incluído: {Casa} x {Vis} | Rod {R} | {Data}",
                timeCasa.Nome, timeVisitante.Nome, jogoWeb.Rodada,
                dataJogo?.ToString("dd/MM/yyyy") ?? "sem data");
        }

        private async Task ReimportarEscalacoesOgol(
            FutebolContext context,
            Jogo jogo,
            string linkDetalhes,
            Time timeCasa,
            Time timeVisitante,
            Guid cicloId,
            CancellationToken ct)
        {
            await Task.Delay(1500, ct);
            var detalhes = await BuscarDetalhesJogoAsync(linkDetalhes, ct);
            if (detalhes == null) return;

            // Remove slots vazios
            var vazios = await context.Escalacoes
                .Where(e => e.JogoId == jogo.Id && e.JogadorId == null)
                .ToListAsync(ct);
            context.Escalacoes.RemoveRange(vazios);

            var formCasaId = jogo.FormacaoCasaId ?? 0;
            var formVisId  = jogo.FormacaoVisitanteId ?? 0;

            var posicoesCasa = await CarregarOuGerarPosicoes(context,
                await context.Formacoes.Include(f => f.Posicoes).FirstOrDefaultAsync(f => f.Id == formCasaId, ct),
                detalhes.FormacaoCasa, ct);
            var posicoesVis = await CarregarOuGerarPosicoes(context,
                await context.Formacoes.Include(f => f.Posicoes).FirstOrDefaultAsync(f => f.Id == formVisId, ct),
                detalhes.FormacaoVisitante, ct);

            if (detalhes.EscalacaoInicialCasa.Any())
            {
                await AdicionarEscalacoesComJogadoresAsync(context, jogo,
                    detalhes.EscalacaoInicialCasa, timeCasa, true, "INICIAL", posicoesCasa, cicloId, ct);
                var fc = detalhes.EscalacaoFinalCasa.Any()
                    ? detalhes.EscalacaoFinalCasa
                    : detalhes.EscalacaoInicialCasa.Select(j => j with { Fase = "FINAL" }).ToList();
                await AdicionarEscalacoesComJogadoresAsync(context, jogo,
                    fc, timeCasa, true, "FINAL", posicoesCasa, cicloId, ct);
            }

            await AdicionarEscalacoesComJogadoresAsync(context, jogo,
                detalhes.EscalacaoInicialVisitante, timeVisitante, false, "INICIAL", posicoesVis, cicloId, ct);
            var fv = detalhes.EscalacaoFinalVisitante.Any()
                ? detalhes.EscalacaoFinalVisitante
                : detalhes.EscalacaoInicialVisitante.Select(j => j with { Fase = "FINAL" }).ToList();
            await AdicionarEscalacoesComJogadoresAsync(context, jogo,
                fv, timeVisitante, false, "FINAL", posicoesVis, cicloId, ct);

            await context.SaveChangesAsync(ct);
        }

        public async Task<(bool Ok, string Mensagem)> ForcarReimportarEscalacaoAsync(
            FutebolContext context, int jogoId, CancellationToken ct = default)
        {
            var jogo = await context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .FirstOrDefaultAsync(j => j.Id == jogoId, ct);

            if (jogo == null)
                return (false, $"Jogo {jogoId} não encontrado.");

            if (string.IsNullOrWhiteSpace(jogo.LinkDetalhes))
                return (false, "Jogo sem link do ogol — re-importação não disponível.");

            await Task.Delay(1500, ct);
            var detalhes = await BuscarDetalhesJogoAsync(jogo.LinkDetalhes, ct);
            if (detalhes == null)
                return (false, "Não foi possível obter detalhes do ogol.");

            if (!detalhes.EscalacaoInicialCasa.Any() && !detalhes.EscalacaoInicialVisitante.Any())
                return (false, "ogol não retornou escalação para este jogo.");

            // Remove tudo e reimporta do zero
            var escalacoes = await context.Escalacoes.Where(e => e.JogoId == jogoId).ToListAsync(ct);
            context.Escalacoes.RemoveRange(escalacoes);
            var gols = await context.Gols.Where(g => g.JogoId == jogoId).ToListAsync(ct);
            context.Gols.RemoveRange(gols);
            var cartoes = await context.Cartoes.Where(c => c.JogoId == jogoId).ToListAsync(ct);
            context.Cartoes.RemoveRange(cartoes);
            var assists = await context.Assistencias.Where(a => a.JogoId == jogoId).ToListAsync(ct);
            context.Assistencias.RemoveRange(assists);
            await context.SaveChangesAsync(ct);

            if (detalhes.PlacarCasa.HasValue)
            {
                jogo.PlacarCasa = detalhes.PlacarCasa;
                jogo.PlacarVisitante = detalhes.PlacarVisitante;
                jogo.Status = "Finalizado";
            }

            var formCasaId = jogo.FormacaoCasaId ?? 0;
            var formVisId  = jogo.FormacaoVisitanteId ?? 0;
            var posicoesCasa = await CarregarOuGerarPosicoes(context,
                await context.Formacoes.Include(f => f.Posicoes).FirstOrDefaultAsync(f => f.Id == formCasaId, ct),
                null, ct);
            var posicoesVis = await CarregarOuGerarPosicoes(context,
                await context.Formacoes.Include(f => f.Posicoes).FirstOrDefaultAsync(f => f.Id == formVisId, ct),
                null, ct);

            var cicloId = Guid.NewGuid();

            await AdicionarEscalacoesComJogadoresAsync(context, jogo,
                detalhes.EscalacaoInicialCasa, jogo.TimeCasa!, true, "INICIAL", posicoesCasa, cicloId, ct);
            await AdicionarEscalacoesComJogadoresAsync(context, jogo,
                detalhes.EscalacaoInicialVisitante, jogo.TimeVisitante!, false, "INICIAL", posicoesVis, cicloId, ct);

            var fc = detalhes.EscalacaoFinalCasa.Any()
                ? detalhes.EscalacaoFinalCasa
                : detalhes.EscalacaoInicialCasa.Select(j => j with { Fase = "FINAL" }).ToList();
            var fv = detalhes.EscalacaoFinalVisitante.Any()
                ? detalhes.EscalacaoFinalVisitante
                : detalhes.EscalacaoInicialVisitante.Select(j => j with { Fase = "FINAL" }).ToList();

            await AdicionarEscalacoesComJogadoresAsync(context, jogo,
                fc, jogo.TimeCasa!, true, "FINAL", posicoesCasa, cicloId, ct);
            await AdicionarEscalacoesComJogadoresAsync(context, jogo,
                fv, jogo.TimeVisitante!, false, "FINAL", posicoesVis, cicloId, ct);

            await context.SaveChangesAsync(ct);

            if (detalhes.Eventos.Any())
            {
                var (g, a, c2) = await ImportarEventosPorLinkAsync(context, jogo, jogo.LinkDetalhes!, ct);
                _logger.LogInformation("[Ogol] Re-import eventos: {G} gols, {A} assists, {C} cartões", g, a, c2);
                await context.SaveChangesAsync(ct);
            }

            return (true,
                $"Re-importação concluída — " +
                $"{detalhes.EscalacaoInicialCasa.Count(x => x.Titular)} titulares casa, " +
                $"{detalhes.EscalacaoInicialVisitante.Count(x => x.Titular)} titulares visitante.");
        }

        // ── Importa gols e cartões resolvendo jogadores pelo link ogol ──────
        public async Task<(int gols, int assistencias, int cartoes)> ImportarEventosPorLinkAsync(
            FutebolContext context, Jogo jogo, string urlDetalhes, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(urlDetalhes)) return (0, 0, 0);
            if (!urlDetalhes.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                urlDetalhes = BaseUrl + urlDetalhes;

            string html;
            try
            {
                await Task.Delay(1500, ct);
                html = await _httpClient.GetStringAsync(urlDetalhes, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[OgolEventos] Erro ao buscar: {Url}", urlDetalhes);
                return (0, 0, 0);
            }

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            int totalGols = 0, totalAssist = 0, totalCartoes = 0;

            var eventos = ExtrairEventosOgol(doc);

            foreach (var ev in eventos)
            {
                // Resolve jogador pelo link ogol (salvo em linktransfermarket)
                var result = await ResolverJogadorPorLinkOgolAsync(
                    context, ev.JogadorLink ?? ev.AssistenteLink,
                    jogo.TimeCasaId, jogo.TimeVisitanteId, ct);

                if (result == null) continue;
                var (jogador, isCasa) = result.Value;

                int minuto = ev.Minuto;

                switch (ev.Tipo)
                {
                    case "Gol":
                        bool golExiste = await context.Gols.AnyAsync(g =>
                            g.JogoId == jogo.Id && g.JogadorId == jogador.Id && g.Minuto == minuto, ct);
                        if (!golExiste)
                        {
                            context.Gols.Add(new Gol
                            {
                                JogoId = jogo.Id,
                                JogadorId = jogador.Id,
                                Minuto = minuto,
                                Contra = ev.Contra
                            });
                            totalGols++;
                        }
                        break;

                    case "Assistencia":
                        bool assistExiste = await context.Assistencias.AnyAsync(a =>
                            a.JogoId == jogo.Id && a.JogadorId == jogador.Id && a.Minuto == minuto, ct);
                        if (!assistExiste)
                        {
                            context.Assistencias.Add(new Assistencia
                            {
                                JogoId = jogo.Id,
                                JogadorId = jogador.Id,
                                Minuto = minuto
                            });
                            totalAssist++;
                        }
                        break;

                    case "CartaoAmarelo":
                    case "CartaoVermelho":
                        bool cartaoExiste = await context.Cartoes.AnyAsync(c =>
                            c.JogoId == jogo.Id && c.JogadorId == jogador.Id &&
                            c.Minuto == minuto && c.Tipo == ev.Detalhe, ct);
                        if (!cartaoExiste)
                        {
                            context.Cartoes.Add(new Cartao
                            {
                                JogoId = jogo.Id,
                                JogadorId = jogador.Id,
                                Minuto = minuto,
                                Tipo = ev.Detalhe ?? "Amarelo"
                            });
                            totalCartoes++;
                        }
                        break;
                }
            }

            await context.SaveChangesAsync(ct);

            _logger.LogInformation("[OgolEventos] Jogo {Id}: {G} gols, {A} assists, {C} cartões",
                jogo.Id, totalGols, totalAssist, totalCartoes);

            return (totalGols, totalAssist, totalCartoes);
        }

        // ══════════════════════════════════════════════════════════════
        // PERSISTÊNCIA ─ Helpers
        // ══════════════════════════════════════════════════════════════

        public async Task<Formacao> ObterOuCriarFormacao(
            FutebolContext context, string? nomeFormacao, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(nomeFormacao)) nomeFormacao = "4-3-3";

            var formacao = await context.Formacoes
                .Include(f => f.Posicoes)
                .FirstOrDefaultAsync(f => f.Nome == nomeFormacao, ct);

            if (formacao != null) return formacao;

            _logger.LogInformation("[Ogol] Formação {F} não existe, criando.", nomeFormacao);

            formacao = new Formacao { Nome = nomeFormacao };
            context.Formacoes.Add(formacao);
            await context.SaveChangesAsync(ct);

            var posicoes = GerarPosicoesNomeadas(nomeFormacao, formacao.Id);
            context.PosicoesFormacao.AddRange(posicoes);
            await context.SaveChangesAsync(ct);

            formacao.Posicoes = posicoes;
            return formacao;
        }

        public void AdicionarEscalacaoComPosicoes(
            FutebolContext context, Jogo jogo, Formacao formacao, bool isTimeCasa)
        {
            foreach (var pos in formacao.Posicoes.OrderBy(p => p.Ordem))
            {
                context.Escalacoes.Add(new Escalacao
                {
                    Jogo = jogo,
                    IsTimeCasa = isTimeCasa,
                    Titular = true,
                    Posicao = pos.NomePosicao,
                    PosicaoX = pos.PosicaoX,
                    PosicaoY = pos.PosicaoY,
                    FaseEscalacao = "INICIAL"
                });
            }
        }

        private async Task AdicionarEscalacoesComJogadoresAsync(
            FutebolContext context,
            Jogo jogo,
            List<JogadorEscalacaoTM> jogadores,
            Time time,
            bool isTimeCasa,
            string fase,
            List<PosicaoFormacao> posicoes,
            Guid cicloId,
            CancellationToken ct)
        {
            var titulares = jogadores.Where(j => j.Titular).ToList();
            var reservas  = jogadores.Where(j => !j.Titular).ToList();
            var slots = posicoes.OrderBy(p => p.Ordem).ToList();
            var slotsDisp = slots.ToList();

            for (int i = 0; i < titulares.Count; i++)
            {
                var j = titulares[i];

                // Busca perfil no ogol para posição, foto, nacionalidade
                InfoPerfilJogadorTM? perfil = null;
                if (!string.IsNullOrWhiteSpace(j.JogadorLink))
                    perfil = await BuscarInfoPerfilJogador(j.JogadorLink, ct);

                var posicaoParaCriacao = perfil?.Posicao ?? j.Posicao;

                var jogadorBanco = await ResolverJogadorAsync(
                    context, j.Nome, j.JogadorLink, time.Id, ct,
                    posicaoParaCriacao, j.Numero, j.IdExterno, cicloId);
                if (jogadorBanco == null) continue;

                // Atualiza posição se veio do perfil
                if (!string.IsNullOrWhiteSpace(perfil?.Posicao) &&
                    jogadorBanco.Posicao != perfil.Posicao)
                    jogadorBanco.Posicao = perfil.Posicao;

                await AtualizarNacionalidadeFoto(context, jogadorBanco, perfil, ct);

                // Slot de formação (round-robin sequencial)
                PosicaoFormacao? slot = slotsDisp.FirstOrDefault();
                if (slot != null) slotsDisp.Remove(slot);

                context.Escalacoes.Add(new Escalacao
                {
                    JogoId = jogo.Id,
                    JogadorId = jogadorBanco.Id,
                    IsTimeCasa = isTimeCasa,
                    Titular = true,
                    Posicao = perfil?.Posicao ?? j.Posicao,
                    PosicaoX = slot?.PosicaoX ?? 50,
                    PosicaoY = slot?.PosicaoY ?? 50,
                    FaseEscalacao = fase
                });
            }

            // Reservas sem slot de formação
            foreach (var j in reservas)
            {
                InfoPerfilJogadorTM? perfil = null;
                if (!string.IsNullOrWhiteSpace(j.JogadorLink))
                    perfil = await BuscarInfoPerfilJogador(j.JogadorLink, ct);

                var jogadorBanco = await ResolverJogadorAsync(
                    context, j.Nome, j.JogadorLink, time.Id, ct,
                    perfil?.Posicao ?? j.Posicao, j.Numero, j.IdExterno, cicloId);
                if (jogadorBanco == null) continue;

                if (!string.IsNullOrWhiteSpace(perfil?.Posicao) &&
                    jogadorBanco.Posicao != perfil.Posicao)
                    jogadorBanco.Posicao = perfil.Posicao;

                await AtualizarNacionalidadeFoto(context, jogadorBanco, perfil, ct);

                context.Escalacoes.Add(new Escalacao
                {
                    JogoId = jogo.Id,
                    JogadorId = jogadorBanco.Id,
                    IsTimeCasa = isTimeCasa,
                    Titular = false,
                    Posicao = perfil?.Posicao ?? j.Posicao,
                    PosicaoX = 0,
                    PosicaoY = 0,
                    FaseEscalacao = fase
                });
            }
        }

        // Quando o jogador já existe em outro time, vincula como seleção se necessário
        private static void VincularSelecaoSeNecessario(Jogador jogador, int timeId)
        {
            if (jogador.TimeId != timeId && jogador.SelecaoId != timeId)
                jogador.SelecaoId = timeId;
        }

        private async Task<Jogador?> ResolverJogadorAsync(
            FutebolContext context,
            string nome,
            string? linkOgol,
            int timeId,
            CancellationToken ct,
            string? posicao = null,
            int? numeroCamisa = null,
            long? idExterno = null,
            Guid? cicloId = null)
        {
            Jogador? jogador = null;

            // 1. Pelo link ogol — busca em qualquer time (jogador pode estar em clube E seleção)
            if (!string.IsNullOrWhiteSpace(linkOgol))
            {
                var linkNorm = NormalizarLinkOgol(linkOgol);
                jogador = await context.Jogadores
                    .FirstOrDefaultAsync(j => j.linktransfermarket == linkNorm, ct);
                if (jogador != null)
                {
                    VincularSelecaoSeNecessario(jogador, timeId);
                    return jogador;
                }
            }

            // 2. Pelo IdApi ogol — busca em qualquer time
            if (idExterno.HasValue && idExterno.Value > 0)
            {
                jogador = await context.Jogadores
                    .FirstOrDefaultAsync(j => j.IdApi == idExterno.Value, ct);
                if (jogador != null)
                {
                    if (string.IsNullOrWhiteSpace(jogador.linktransfermarket) && !string.IsNullOrWhiteSpace(linkOgol))
                        jogador.linktransfermarket = NormalizarLinkOgol(linkOgol);
                    VincularSelecaoSeNecessario(jogador, timeId);
                    return jogador;
                }
            }

            // 3. Pelo nome exato — primeiro no próprio time, depois em qualquer time
            if (!string.IsNullOrWhiteSpace(nome))
            {
                var nomeN = nome.Trim().ToLowerInvariant();

                // 3a. No time correto (clube ou seleção)
                jogador = await context.Jogadores
                    .FirstOrDefaultAsync(j => j.Nome.ToLower() == nomeN &&
                        (j.TimeId == timeId || j.SelecaoId == timeId), ct);
                if (jogador != null) return jogador;

                // 3b. Em qualquer time — evita duplicar jogador de clube convocado para seleção
                jogador = await context.Jogadores
                    .FirstOrDefaultAsync(j => j.Nome.ToLower() == nomeN, ct);
                if (jogador != null)
                {
                    VincularSelecaoSeNecessario(jogador, timeId);
                    return jogador;
                }
            }

            // 4. Nome aproximado — apenas no time correto (evita falsos positivos globais)
            if (!string.IsNullOrWhiteSpace(nome))
            {
                var candidatos = await context.Jogadores
                    .Where(j => j.TimeId == timeId || j.SelecaoId == timeId)
                    .ToListAsync(ct);
                jogador = candidatos.FirstOrDefault(j =>
                    j.Nome.Contains(nome, StringComparison.InvariantCultureIgnoreCase));
                if (jogador != null) return jogador;
            }

            // 5. Cria novo
            bool nomeInvalido = string.IsNullOrWhiteSpace(nome) ||
                nome.StartsWith("/") || nome.StartsWith("http") || nome == "Indefinida" ||
                Regex.IsMatch(nome, @"^\d+$");
            if (nomeInvalido)
            {
                _logger.LogWarning("[OgolResolver] Nome inválido: '{N}' | Link={L}", nome, linkOgol);
                return null;
            }

            jogador = new Jogador
            {
                Nome = nome,
                TimeId = timeId,
                linktransfermarket = NormalizarLinkOgol(linkOgol ?? ""),
                Posicao = MapearPosicaoParaNome(posicao),
                NumeroCamisa = numeroCamisa,
                IdApi = idExterno,
                DataNascimento = null,
                DtInc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
                Atualizado = false
            };

            context.Jogadores.Add(jogador);
            await context.SaveChangesAsync(ct);

            _logger.LogInformation("[OgolResolver] Criado: {Nome} ({TimeId}) | Pos={P} | Link={L}",
                jogador.Nome, timeId, jogador.Posicao, jogador.linktransfermarket);

            if (cicloId.HasValue)
            {
                var timeNome = context.Times.Local.FirstOrDefault(t => t.Id == timeId)?.Nome
                    ?? $"TimeId:{timeId}";
                RegistrarLog(context, cicloId.Value, "Jogador", "Criado",
                    timeNome: timeNome,
                    detalhes: $"{jogador.Nome} | Pos: {jogador.Posicao ?? "–"} | Nº: {jogador.NumeroCamisa?.ToString() ?? "–"}");
            }

            return jogador;
        }

        private async Task<(Jogador jogador, bool isCasa)?> ResolverJogadorPorLinkOgolAsync(
            FutebolContext context,
            string? href,
            int timeCasaId,
            int timeVisitanteId,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(href)) return null;

            var linkNorm = NormalizarLinkOgol(href);

            // Pelo link exato
            var porLink = await context.Jogadores
                .FirstOrDefaultAsync(j => j.linktransfermarket == linkNorm, ct);
            if (porLink != null)
            {
                bool isCasaLink = porLink.TimeId == timeCasaId || porLink.SelecaoId == timeCasaId;
                return (porLink, isCasaLink);
            }

            // Por ID ogol no campo linktransfermarket
            var idMatch = Regex.Match(href, @"/jogador/[^/]+/(\d+)");
            if (idMatch.Success)
            {
                var idOgol = long.Parse(idMatch.Groups[1].Value);
                var porId = await context.Jogadores
                    .FirstOrDefaultAsync(j => j.IdApi == idOgol, ct);
                if (porId != null)
                {
                    bool isCasaId = porId.TimeId == timeCasaId || porId.SelecaoId == timeCasaId;
                    return (porId, isCasaId);
                }
            }

            _logger.LogWarning("[OgolResolver] Jogador não encontrado pelo link: {Href}", href);
            return null;
        }

        private async Task AtualizarNacionalidadeFoto(
            FutebolContext context, Jogador jogador,
            InfoPerfilJogadorTM? perfil, CancellationToken ct)
        {
            if (perfil == null) return;

            if (!string.IsNullOrWhiteSpace(perfil.Nacionalidade) && jogador.NacionalidadeId == null)
            {
                var nac = await context.Nacionalidades
                    .FirstOrDefaultAsync(n => n.Nome.ToLower() == perfil.Nacionalidade.ToLower(), ct);
                if (nac == null)
                {
                    nac = new Nacionalidade { Nome = perfil.Nacionalidade };
                    context.Nacionalidades.Add(nac);
                    await context.SaveChangesAsync(ct);
                }
                jogador.NacionalidadeId = nac.Id;
            }

            if (!string.IsNullOrEmpty(perfil.FotoUrl) && string.IsNullOrEmpty(jogador.FotoUrl))
                jogador.FotoUrl = perfil.FotoUrl;

            if (perfil.DataNascimento.HasValue && jogador.DataNascimento == null)
                jogador.DataNascimento = DateTime.SpecifyKind(
                    perfil.DataNascimento.Value, DateTimeKind.Unspecified);
        }

        private async Task<List<PosicaoFormacao>> CarregarOuGerarPosicoes(
            FutebolContext context,
            Formacao? formacao,
            string? nomeFormacao,
            CancellationToken ct)
        {
            if (formacao != null)
            {
                var pos = await context.PosicoesFormacao
                    .Where(p => p.FormacaoId == formacao.Id)
                    .OrderBy(p => p.Ordem)
                    .ToListAsync(ct);
                if (pos.Any()) return pos;
            }

            return GerarPosicoesNomeadas(nomeFormacao ?? "4-3-3", formacao?.Id ?? 0);
        }

        // ══════════════════════════════════════════════════════════════
        // TREINADOR ─ Extração do ogol e persistência
        // ══════════════════════════════════════════════════════════════

        // ogol: link /treinador/ aparece no game_report abaixo dos jogadores
        private (string? Nome, string? Link) ExtrairTreinadorDaEquipe(HtmlDocument doc, bool casa)
        {
            int colIdx = casa ? 1 : 2;
            var node = doc.DocumentNode.SelectSingleNode(
                $"//div[@id='game_report']//div[contains(@class,'zz-tpl-col')][{colIdx}]" +
                $"//a[contains(@href,'/treinador/')]");

            if (node == null) return (null, null);

            var nome = HtmlEntity.DeEntitize(node.InnerText.Trim());
            var href = node.GetAttributeValue("href", "");
            return (string.IsNullOrWhiteSpace(nome) ? null : nome, UrlAbsoluta(href));
        }

        private async Task<Treinador?> ResolverOuCriarTreinadorAsync(
            FutebolContext context, string? nome, string? linkOgol, int timeId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(nome)) return null;

            var linkNorm = NormalizarLinkOgol(linkOgol ?? "");

            // 1. Pelo link ogol
            if (!string.IsNullOrWhiteSpace(linkNorm))
            {
                var porLink = await context.Treinadores
                    .FirstOrDefaultAsync(t => t.LinkOgol == linkNorm, ct);
                if (porLink != null)
                {
                    if (porLink.TimeId != timeId) porLink.TimeId = timeId;
                    return porLink;
                }
            }

            // 2. Pelo nome + time
            var nomeN = nome.Trim().ToLowerInvariant();
            var porNome = await context.Treinadores
                .FirstOrDefaultAsync(t => t.Nome.ToLower() == nomeN && t.TimeId == timeId, ct);
            if (porNome != null)
            {
                if (!string.IsNullOrWhiteSpace(linkNorm) && string.IsNullOrWhiteSpace(porNome.LinkOgol))
                    porNome.LinkOgol = linkNorm;
                return porNome;
            }

            // 3. Cria novo
            var novo = new Treinador
            {
                Nome = nome.Trim(),
                TimeId = timeId,
                LinkOgol = linkNorm,
                DataNascimento = new DateTime(1900, 1, 1),
                DtInc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
            };
            context.Treinadores.Add(novo);
            await context.SaveChangesAsync(ct);

            _logger.LogInformation("[Ogol] Treinador criado: {Nome} (TimeId={T})", nome, timeId);
            return novo;
        }

        // ══════════════════════════════════════════════════════════════
        // FORMAÇÃO ─ Extração do pitch ogol e criação de posições
        // ══════════════════════════════════════════════════════════════

        // Extrai a formação de uma equipe contando colunas de jogadores no pitch.
        // ogol: div.pitch_eleven_horizontal com tabela de colunas por linha tática.
        // Primeiro índice = casa, segundo = visitante.
        private string? ExtrairFormacaoDoPitch(HtmlDocument doc, bool casa)
        {
            int idx = casa ? 0 : 1;

            // Tenta pitch separado por time (dois divs pitch_eleven_horizontal)
            var pitchDivs = doc.DocumentNode.SelectNodes(
                "//div[contains(@class,'pitch_eleven_horizontal')]");
            if (pitchDivs != null && pitchDivs.Count > idx)
                return ContarFormacaoDeDiv(pitchDivs[idx]);

            // Fallback: div único com dois sub-campos (campo_onze indexados)
            var campos = doc.DocumentNode.SelectNodes(
                "//div[contains(@class,'campo_onze') and not(contains(@class,'bloco'))]");
            if (campos != null && campos.Count > idx)
                return ContarFormacaoDeDiv(campos[idx]);

            return null;
        }

        private string? ContarFormacaoDeDiv(HtmlNode div)
        {
            // Cada td da primeira linha da tabela = uma linha tática do campo
            var colunas = div.SelectNodes(".//tr[1]/td");
            if (colunas == null || colunas.Count == 0) return null;

            var linhas = new List<int>();
            bool gkIgnorado = false;

            foreach (var col in colunas)
            {
                // Conta apenas divs com data-player-id preenchido (jogadores reais)
                var players = col.SelectNodes(
                    ".//*[@data-player-id and string-length(@data-player-id) > 0]");
                int count = players?.Count ?? 0;
                if (count == 0) continue;

                if (!gkIgnorado)
                {
                    gkIgnorado = true; // primeira coluna não vazia = goleiro
                    continue;
                }

                linhas.Add(count);
            }

            if (!linhas.Any() || linhas.Sum() + 1 != 11) return null;

            return string.Join("-", linhas);
        }

        // Gera posições nomeadas para uma formação (ex: 4-2-3-1 → Lateral Esq, Zagueiro...).
        private static List<PosicaoFormacao> GerarPosicoesNomeadas(string nomeFormacao, int formacaoId)
        {
            var linhas = new List<int>();
            foreach (var parte in nomeFormacao.Trim().Split('-'))
                if (int.TryParse(parte.Trim(), out int n)) linhas.Add(n);

            if (!linhas.Any() || linhas.Sum() + 1 != 11)
                linhas = new List<int> { 4, 3, 3 };

            var posicoes = new List<PosicaoFormacao>();
            int ordem = 1;
            int totalLinhas = linhas.Count;

            // Goleiro
            posicoes.Add(new PosicaoFormacao
            {
                FormacaoId = formacaoId,
                NomePosicao = "Goleiro",
                PosicaoX = 50,
                PosicaoY = 92,
                Ordem = ordem++,
                PosicaoId = 0
            });

            for (int l = 0; l < totalLinhas; l++)
            {
                int n = linhas[l];
                double y = totalLinhas > 1
                    ? Math.Round(80.0 - l * (80.0 - 14.0) / (totalLinhas - 1), 1)
                    : 45.0;

                for (int c = 0; c < n; c++)
                {
                    double x = Math.Round((c + 1.0) / (n + 1) * 100.0, 1);
                    posicoes.Add(new PosicaoFormacao
                    {
                        FormacaoId = formacaoId,
                        NomePosicao = ObterNomePosicao(l, c, n, totalLinhas),
                        PosicaoX = x,
                        PosicaoY = y,
                        Ordem = ordem++,
                        PosicaoId = 0
                    });
                }
            }

            return posicoes;
        }

        private static string ObterNomePosicao(int linhaIdx, int colIdx, int totalNaLinha, int totalLinhas)
        {
            bool isDefesa = linhaIdx == 0;
            bool isAtaque = linhaIdx == totalLinhas - 1;

            if (isDefesa)
            {
                return totalNaLinha switch
                {
                    1 => "Zagueiro Central",
                    2 => colIdx == 0 ? "Zagueiro Esquerdo" : "Zagueiro Direito",
                    3 => colIdx switch { 0 => "Zagueiro Esquerdo", 1 => "Zagueiro Central", _ => "Zagueiro Direito" },
                    4 => colIdx switch { 0 => "Lateral Esquerdo", 1 => "Zagueiro", 2 => "Zagueiro", _ => "Lateral Direito" },
                    5 => colIdx switch { 0 => "Ala Esquerdo", 1 => "Zagueiro", 2 => "Zagueiro Central", 3 => "Zagueiro", _ => "Ala Direito" },
                    _ => $"Defensor {colIdx + 1}"
                };
            }

            if (isAtaque)
            {
                return totalNaLinha switch
                {
                    1 => "Centroavante",
                    2 => colIdx == 0 ? "Centroavante Esquerdo" : "Centroavante Direito",
                    3 => colIdx switch { 0 => "Ponta Esquerda", 1 => "Centroavante", _ => "Ponta Direita" },
                    _ => $"Atacante {colIdx + 1}"
                };
            }

            // Linhas intermediárias — linha 1 tende a ser mais defensiva (volante), demais mais ofensivas
            bool isMaisDefensiva = linhaIdx <= totalLinhas / 2;
            return totalNaLinha switch
            {
                1 => isMaisDefensiva ? "Volante" : "Meia Ofensivo",
                2 => colIdx == 0
                    ? (isMaisDefensiva ? "Volante Esquerdo" : "Meia Esquerdo")
                    : (isMaisDefensiva ? "Volante Direito" : "Meia Direito"),
                3 => colIdx switch
                {
                    0 => "Meia Esquerdo",
                    1 => isMaisDefensiva ? "Volante" : "Meia Central",
                    _ => "Meia Direito"
                },
                4 => colIdx switch { 0 => "Ala Esquerdo", 1 => "Meia Esquerdo", 2 => "Meia Direito", _ => "Ala Direito" },
                _ => $"Meia {colIdx + 1}"
            };
        }

        // ══════════════════════════════════════════════════════════════
        // UTILIDADES
        // ══════════════════════════════════════════════════════════════

        private static string NormalizarLinkOgol(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return string.Empty;
            url = url.Trim();
            if (url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return url;
            if (!url.StartsWith("/")) url = "/" + url;
            return BaseUrl + url;
        }

        private static string UrlAbsoluta(string href)
        {
            if (string.IsNullOrWhiteSpace(href)) return string.Empty;
            href = href.Trim();
            return href.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? href : BaseUrl + href;
        }

        private static long? ExtrairIdOgol(string? href)
        {
            if (string.IsNullOrWhiteSpace(href)) return null;
            var m = Regex.Match(href, @"/jogador/[^/]+/(\d+)");
            return m.Success ? long.Parse(m.Groups[1].Value) : null;
        }

        private static bool NoOuAncestralContémClasse(HtmlNode node, params string[] classes)
        {
            for (var atual = node; atual != null; atual = atual.ParentNode)
            {
                var cls = atual.GetAttributeValue("class", "").ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(cls)) continue;
                if (classes.Any(c => cls.Contains(c))) return true;
            }
            return false;
        }

        private static string CapitalizarSlug(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug)) return slug;
            return string.Join(" ",
                slug.Split('-').Select(p => p.Length > 0
                    ? char.ToUpperInvariant(p[0]) + p[1..]
                    : p));
        }

        private static string MapearPosicaoParaNome(string? p) => p?.ToLower() switch
        {
            var s when s?.Contains("gol") == true || s?.Contains("keeper") == true => "Goleiro",
            var s when s?.Contains("zagueiro") == true || s?.Contains("defesa") == true => "Zagueiro",
            var s when s?.Contains("lateral") == true => "Lateral",
            var s when s?.Contains("volante") == true => "Volante",
            var s when s?.Contains("meio") == true || s?.Contains("meia") == true => "Meio-campo",
            var s when s?.Contains("atacante") == true || s?.Contains("centroavante") == true => "Atacante",
            var s when s?.Contains("ponta") == true => "Ponta",
            _ => "Meio-campo"
        };

        private static void RegistrarLog(
            FutebolContext context,
            Guid cicloId,
            string tipo,
            string acao,
            string? competicaoNome = null,
            string? timeNome = null,
            string? jogoDescricao = null,
            string? detalhes = null)
        {
            context.TransfermarktSincronizacaoLogs.Add(new TransfermarktSincronizacaoLog
            {
                CicloId = cicloId,
                Data = DateTime.UtcNow,
                Tipo = tipo,
                Acao = acao,
                CompeticaoNome = competicaoNome,
                TimeNome = timeNome,
                JogoDescricao = jogoDescricao,
                Detalhes = detalhes
            });
        }

        // ══════════════════════════════════════════════════════════════
        // SINCRONIZAÇÃO DE UMA COMPETIÇÃO (chamável via controller)
        // ══════════════════════════════════════════════════════════════

        public record SincronizacaoResultado(
            int JogosProcessados,
            int TimesCreados,
            int Erros,
            List<string> Avisos);

        public async Task<SincronizacaoResultado> SincronizarCompeticaoAsync(
            FutebolContext context,
            Competicao competicao,
            CancellationToken ct = default)
        {
            int jogosProcessados = 0, timesCreados = 0, erros = 0;
            var avisos = new List<string>();
            var cicloId = Guid.NewGuid();

            if (string.IsNullOrWhiteSpace(competicao.linktransfermarket))
                return new SincronizacaoResultado(0, 0, 0, new() { "Competição sem link ogol configurado." });

            _logger.LogInformation("[SincComp] Iniciando: {Nome}", competicao.Nome);

            var jogosWeb = await BuscarJogosCompeticaoPorLink(competicao.linktransfermarket, ct);
            _logger.LogInformation("[SincComp] {N} jogos encontrados.", jogosWeb.Count);

            foreach (var jogoWeb in jogosWeb)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    var timeCasa = await ResolverOuCriarTimeInterno(
                        context, jogoWeb.NomeTimeCasa, jogoWeb.LinkTimeCasa, jogoWeb.EscudoTimeCasa, ct);
                    if (timeCasa.criado) timesCreados++;

                    var timeVisitante = await ResolverOuCriarTimeInterno(
                        context, jogoWeb.NomeTimeVisitante, jogoWeb.LinkTimeVisitante, jogoWeb.EscudoTimeVisitante, ct);
                    if (timeVisitante.criado) timesCreados++;

                    await IncluirOuAtualizarJogo(
                        context, competicao, jogoWeb, timeCasa.time, timeVisitante.time, cicloId, ct);

                    await context.SaveChangesAsync(ct);
                    jogosProcessados++;

                    await Task.Delay(500, ct);
                }
                catch (Exception ex)
                {
                    erros++;
                    avisos.Add($"{jogoWeb.NomeTimeCasa} x {jogoWeb.NomeTimeVisitante}: {ex.Message}");
                    _logger.LogWarning(ex, "[SincComp] Erro ao processar jogo.");
                }
            }

            _logger.LogInformation("[SincComp] Concluído: {J} jogos, {T} times criados, {E} erros.",
                jogosProcessados, timesCreados, erros);

            return new SincronizacaoResultado(jogosProcessados, timesCreados, erros, avisos);
        }

        private async Task<(Time time, bool criado)> ResolverOuCriarTimeInterno(
            FutebolContext context, string nome, string? linkOgol, string? escudoUrl,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(nome)) nome = $"Time_{Guid.NewGuid()}";

            var urlNorm = !string.IsNullOrWhiteSpace(linkOgol) ? NormalizarLink(linkOgol) : null;

            if (!string.IsNullOrWhiteSpace(urlNorm))
            {
                var porUrl = await context.Times
                    .FirstOrDefaultAsync(t => t.linktransfermarket == urlNorm, ct);
                if (porUrl != null)
                {
                    if (string.IsNullOrWhiteSpace(porUrl.EscudoUrl) && !string.IsNullOrWhiteSpace(escudoUrl))
                        porUrl.EscudoUrl = escudoUrl;
                    return (porUrl, false);
                }
            }

            var porNome = await context.Times.FirstOrDefaultAsync(t => t.Nome == nome, ct);
            if (porNome != null)
            {
                if (string.IsNullOrWhiteSpace(porNome.linktransfermarket) && !string.IsNullOrWhiteSpace(urlNorm))
                    porNome.linktransfermarket = urlNorm;
                if (string.IsNullOrWhiteSpace(porNome.EscudoUrl) && !string.IsNullOrWhiteSpace(escudoUrl))
                    porNome.EscudoUrl = escudoUrl;
                return (porNome, false);
            }

            var formacaoPadraoId = (await context.Formacoes.FirstOrDefaultAsync(ct))?.Id ?? 1;
            var novoTime = new Time
            {
                Nome = nome,
                Cidade = "Importado",
                IdApi = 0,
                EscudoUrl = escudoUrl ?? "",
                CorPrincipal = "#000000",
                CorSecundaria = "#FFFFFF",
                linktransfermarket = urlNorm,
                FormacaoPadraoId = formacaoPadraoId
            };

            await context.Times.AddAsync(novoTime, ct);
            await context.SaveChangesAsync(ct);
            return (novoTime, true);
        }
    }
}
