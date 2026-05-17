// ============================================================
// ControleFutebolWeb/Services/TransfermarktSulAmericanaService.cs
// ============================================================
using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Web;
using ControleFutebolWeb.Helpers;

namespace ControleFutebolWeb.Services
{
    /// <summary>
    /// Scraping do Transfermarkt para a Copa Sul-Americana.
    ///
    /// Fluxo:
    ///   1. Acessa o calendário completo (grupos e rodadas)
    ///      https://www.transfermarkt.com.br/copa-sudamericana/gesamtspielplan/pokalwettbewerb/CS/saison_id/{ano}
    ///   2. Para cada jogo com placar no site, localiza o registro no banco
    ///      comparando times (com dicionário de nomes) + data
    ///   3. Atualiza placares divergentes
    ///   4. Acessa a página de detalhe do jogo, extrai:
    ///      - Gols (marcador, minuto)
    ///      - Escalação inicial e final (titulares + reservas)
    ///      - Formação (usa FormacaoPadraoId = 20 como fallback)
    ///   5. Marca jogo.Atualizado = true para não reprocessar
    /// </summary>
    public class TransfermarktSulAmericanaService
    {
        private readonly HttpClient _http;
        private readonly ILogger<TransfermarktSulAmericanaService> _log;
        // FormacaoPadraoId fixo solicitado pelo usuário
        private const int FORMACAO_PADRAO_ID = 20;

        // ── CONSTRUTOR ────────────────────────────────────────────────────────
        public TransfermarktSulAmericanaService(
            HttpClient httpClient,
            ILogger<TransfermarktSulAmericanaService> logger)
        {
            _http = httpClient;
            _log = logger;

            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                "(KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
            _http.DefaultRequestHeaders.Add("Accept",
                "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            _http.DefaultRequestHeaders.Add("Accept-Language",
                "pt-BR,pt;q=0.9,en;q=0.8");
            _http.DefaultRequestHeaders.Add("Referer",
                "https://www.transfermarkt.com.br/");
        }

        // ─────────────────────────────────────────────────────────────────────
        // PONTO DE ENTRADA PRINCIPAL
        // ─────────────────────────────────────────────────────────────────────

        /// <param name="context">DbContext</param>
        /// <param name="competicaoIdNoBanco">ID da competição Sul-Americana no banco</param>
        /// <param name="anoTemporada">Ex.: 2025</param>
        /// <param name="importarEscalacoes">Se true, busca escalações dos jogos realizados</param>
        public async Task<SincronizacaoResultado> SincronizarAsync(
            FutebolContext context,
            int competicaoIdNoBanco,
            int anoTemporada = 2025,
            bool importarEscalacoes = true,
            CancellationToken ct = default)
        {
            var resultado = new SincronizacaoResultado();

            // 1. Busca o calendário completo no Transfermarkt
            var jogosWeb = await BuscarCalendarioAsync(anoTemporada, ct);
            resultado.JogosEncontradosNaSite = jogosWeb.Count;

            if (!jogosWeb.Any())
            {
                resultado.Avisos.Add("Nenhum jogo encontrado no calendário do Transfermarkt.");
                return resultado;
            }

            // 2. Carrega jogos do banco para comparação (só os não atualizados)
            var jogosBanco = await context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Include(j => j.Escalacoes)
                .Include(j => j.Gols)
                .Where(j => j.CompeticaoId == competicaoIdNoBanco && j.Atualizado != 1)
                .ToListAsync(ct);

            _log.LogInformation("[SulAmericana] {Total} jogos no banco para verificar.", jogosBanco.Count);

            // 3. Processa cada jogo encontrado no site
            foreach (var jogoWeb in jogosWeb.Where(j => j.PlacarCasa.HasValue))
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    await ProcessarJogoAsync_V2(context, jogoWeb, jogosBanco,
                        importarEscalacoes, resultado, ct);
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "[SulAmericana] Erro: {H} x {A}",
                        jogoWeb.NomeTimeCasa, jogoWeb.NomeTimeVisitante);
                    resultado.Avisos.Add(
                        $"Erro em {jogoWeb.NomeTimeCasa} x {jogoWeb.NomeTimeVisitante}: {ex.Message}");
                }

                // Pausa para não ser bloqueado pelo Transfermarkt
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }

            await context.SaveChangesAsync(ct);
            await AtualizarGruposAsync(context, competicaoIdNoBanco, anoTemporada, ct);
            return resultado;
        }

        public async Task AtualizarGruposAsync(
        FutebolContext context,
        int competicaoId,
        int ano = 2025,
        CancellationToken ct = default)
        {
            var saison = ano - 1;
            var url = $"https://www.transfermarkt.com.br/copa-sudamericana/gesamtspielplan/" +
                      $"pokalwettbewerb/CS/saison_id/{saison}";

            _log.LogInformation("[SulAmericana] Buscando grupos: {U}", url);

            string html;
            try
            {
                html = await _http.GetStringAsync(url, ct);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "[SulAmericana] Erro ao buscar grupos.");
                return;
            }

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // A página gesamtspielplan tem tabelas de classificação por grupo.
            // Cada tabela fica dentro de um box com h2/h3 indicando o nome do grupo.
            // Os links de time têm href="/slug/spielplan/verein/ID/saison_id/ANO"
            // e title="Nome do Time".

            // Monta dicionário: nomeNoBanco → grupo
            var mapaTimeGrupo = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var boxes = doc.DocumentNode.SelectNodes(
                "//div[contains(@class,'box')] | //div[contains(@class,'content-box')]");

            if (boxes == null)
            {
                _log.LogWarning("[SulAmericana] Nenhum box encontrado para grupos.");
                return;
            }

            foreach (var box in boxes)
            {
                // Nome do grupo (h2 ou h3 dentro do box)
                var hNode = box.SelectSingleNode(".//h2 | .//h3 | .//div[contains(@class,'headline')]");
                if (hNode == null) continue;

                var nomeGrupo = HtmlEntity.DeEntitize(hNode.InnerText.Trim());

                // Ignora boxes que não são grupos (artilheiros, rodadas, etc.)
                if (!nomeGrupo.Contains("Grupo") && !nomeGrupo.Contains("Group") &&
                    !nomeGrupo.Contains("Gruppe") && !Regex.IsMatch(nomeGrupo, @"^[A-H]$"))
                    continue;

                // Todos os links de times dentro do box
                var linksTime = box.SelectNodes(
                    ".//a[contains(@href,'/verein/') and not(contains(@href,'/spieler/'))]");

                if (linksTime == null) continue;

                foreach (var lt in linksTime)
                {
                    var title = HtmlEntity.DeEntitize(lt.GetAttributeValue("title", "").Trim());
                    if (string.IsNullOrWhiteSpace(title)) continue;

                    // Resolve para nome no banco
                    var nomeBanco = TimesSulamericanaHelper.NormalizarNome(title);
                    mapaTimeGrupo[nomeBanco] = nomeGrupo;
                }
            }

            if (!mapaTimeGrupo.Any())
            {
                _log.LogWarning("[SulAmericana] Nenhum grupo extraído da página.");
                return;
            }

            _log.LogInformation("[SulAmericana] Grupos mapeados: {N}", mapaTimeGrupo.Count);
            foreach (var kv in mapaTimeGrupo)
                _log.LogInformation("[SulAmericana]   {Time} → {Grupo}", kv.Key, kv.Value);

            // Atualiza jogos no banco: se TimeCasa ou TimeVisitante estiver no mapa,
            // usa o grupo correspondente
            var jogos = await context.Jogos
                .Include(j => j.TimeCasa)
                .Include(j => j.TimeVisitante)
                .Where(j => j.CompeticaoId == competicaoId)
                .ToListAsync(ct);

            int atualizados = 0;
            foreach (var jogo in jogos)
            {
                string? grupo = null;

                if (jogo.TimeCasa?.Nome != null &&
                    mapaTimeGrupo.TryGetValue(jogo.TimeCasa.Nome, out var g1))
                    grupo = g1;
                else if (jogo.TimeVisitante?.Nome != null &&
                         mapaTimeGrupo.TryGetValue(jogo.TimeVisitante.Nome, out var g2))
                    grupo = g2;

                if (grupo != null && jogo.Grupo != grupo)
                {
                    jogo.Grupo = grupo;
                    atualizados++;
                }
            }

            await context.SaveChangesAsync(ct);
            _log.LogInformation("[SulAmericana] Grupos atualizados em {N} jogos.", atualizados);
        }

        // ─────────────────────────────────────────────────────────────────────
        // SCRAPING DO CALENDÁRIO (grupos e rodadas)
        // ─────────────────────────────────────────────────────────────────────

        public async Task<string> BuscarHtmlBrutoAsync(string url, CancellationToken ct = default)
        {
            try
            {
                return await _http.GetStringAsync(url, ct);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "[SulAmericana] Erro ao buscar HTML bruto: {Url}", url);
                return $"ERRO: {ex.Message}";
            }
        }

        public async Task<List<JogoTM>> BuscarCalendarioAsync(
            int ano = 2025, CancellationToken ct = default)
        {
            var jogos = new List<JogoTM>();
            var saison = ano - 1; // Sul-Americana 2026 → saison_id=2025

            int rodadaAtual = 1;
            int rodadasSemJogos = 0;
            const int maxRodadas = 20; // fase de grupos tem até 8 rodadas; mata-mata mais algumas

            while (rodadaAtual <= maxRodadas && rodadasSemJogos < 2)
            {
                if (ct.IsCancellationRequested) break;

                var url = $"https://www.transfermarkt.com.br/copa-sudamericana/spieltag/" +
                          $"pokalwettbewerb/CS/saison_id/{saison}/spieltag/{rodadaAtual}";

                _log.LogInformation("[SulAmericana] Buscando rodada {R}: {U}", rodadaAtual, url);

                string html;
                try
                {
                    var response = await _http.GetAsync(url, ct);
                    if (!response.IsSuccessStatusCode)
                    {
                        _log.LogWarning("[SulAmericana] Rodada {R}: status {S}", rodadaAtual, (int)response.StatusCode);
                        rodadasSemJogos++;
                        rodadaAtual++;
                        continue;
                    }
                    html = await response.Content.ReadAsStringAsync(ct);
                }
                catch (Exception ex)
                {
                    _log.LogWarning("[SulAmericana] Rodada {R} erro: {M}", rodadaAtual, ex.Message);
                    rodadasSemJogos++;
                    rodadaAtual++;
                    continue;
                }

                if (!html.Contains("/spielbericht/") && !html.Contains("/begegnung_detail/"))
                {
                    _log.LogInformation("[SulAmericana] Rodada {R}: sem jogos, encerrando.", rodadaAtual);
                    rodadasSemJogos++;
                    rodadaAtual++;
                    continue;
                }

                var jogosRodada = ExtrairJogosDoPagina(html, rodadaAtual);
                _log.LogInformation("[SulAmericana] Rodada {R}: {N} jogos ({P} com placar)",
                    rodadaAtual, jogosRodada.Count, jogosRodada.Count(j => j.PlacarCasa.HasValue));

                if (jogosRodada.Any())
                {
                    jogos.AddRange(jogosRodada);
                    rodadasSemJogos = 0;
                }
                else
                {
                    rodadasSemJogos++;
                }

                rodadaAtual++;
                await Task.Delay(1500, ct); // pausa para não ser bloqueado
            }

            _log.LogInformation("[SulAmericana] Total: {N} | Com placar: {P} | Rodadas verificadas: {R}",
                jogos.Count, jogos.Count(j => j.PlacarCasa.HasValue), rodadaAtual - 1);

            return jogos;
        }
        // ── Extrai jogos de uma página de rodada ─────────────────────────────────────
        private List<JogoTM> ExtrairJogosDoPagina(string html, int rodada)
        {
            var jogos = new List<JogoTM>();
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var linksJogo = doc.DocumentNode.SelectNodes(
                "//a[contains(@href,'/spielbericht/') or contains(@href,'/begegnung_detail/')]");

            if (linksJogo == null) return jogos;

            var processados = new HashSet<string>();

            foreach (var linkJogo in linksJogo)
            {
                var href = linkJogo.GetAttributeValue("href", "");
                if (processados.Contains(href)) continue;
                processados.Add(href);

                if (!href.StartsWith("http"))
                    href = "https://www.transfermarkt.com.br" + href;

                // Placar está no texto do link: "2:1" ou "2 : 1"
                var scoreText = HtmlEntity.DeEntitize(linkJogo.InnerText.Trim());
                var scoreMatch = Regex.Match(scoreText, @"(\d+)\s*[:\-]\s*(\d+)");
                int? pc = null, pv = null;
                if (scoreMatch.Success)
                {
                    pc = int.Parse(scoreMatch.Groups[1].Value);
                    pv = int.Parse(scoreMatch.Groups[2].Value);
                }

                // Sobe na DOM para encontrar o bloco do jogo (tr ou div pai)
                var bloco = linkJogo.ParentNode;
                for (int i = 0; i < 6; i++)
                {
                    if (bloco == null) break;
                    if (bloco.Name == "tr") break;
                    if (bloco.Name == "div" && (
                        bloco.GetAttributeValue("class", "").Contains("spieltag") ||
                        bloco.GetAttributeValue("class", "").Contains("ergebnis") ||
                        bloco.GetAttributeValue("class", "").Contains("match")))
                        break;
                    bloco = bloco.ParentNode;
                }

                if (bloco == null) continue;

                // Times: links com /verein/ no bloco
                var linksTime = bloco.SelectNodes(
                    ".//a[contains(@href,'/verein/') and not(contains(@href,'/spielbericht/'))]");

                if (linksTime == null || linksTime.Count < 2)
                {
                    linksTime = bloco.SelectNodes(
                        ".//a[@title and string-length(@title) > 2 and " +
                        "not(contains(@href,'/spielbericht/')) and " +
                        "not(contains(@href,'/begegnung_detail/'))]");
                }

                if (linksTime == null || linksTime.Count < 2) continue;

                var nomesTime = new List<string>();
                foreach (var lt in linksTime)
                {
                    var nome = lt.GetAttributeValue("title", "").Trim();
                    if (string.IsNullOrWhiteSpace(nome))
                        nome = HtmlEntity.DeEntitize(lt.InnerText.Trim());
                    nome = nome.Split('\n')[0].Trim();
                    if (!string.IsNullOrWhiteSpace(nome) && !nomesTime.Contains(nome))
                        nomesTime.Add(nome);
                    if (nomesTime.Count == 2) break;
                }

                if (nomesTime.Count < 2) continue;

                // Data: procura em células <td> ou <span> com padrão de data
                // O Transfermarkt coloca a data em célula separada do placar
                DateTime? data = null;

                // Estratégia 1: busca qualquer nó com texto de data no bloco pai (até 3 níveis acima)
                var buscaData = bloco.ParentNode;
                for (int i = 0; i < 3 && buscaData != null; i++)
                {
                    var textoNos = buscaData.SelectNodes(
                        ".//td | .//span[contains(@class,'datum')] | .//span[contains(@class,'date')]");
                    if (textoNos != null)
                    {
                        foreach (var tn in textoNos)
                        {
                            var t = tn.InnerText.Trim();
                            var dm = Regex.Match(t, @"\d{2}[./]\d{2}[./]\d{2,4}");
                            if (dm.Success) { data = ParseData(dm.Value); break; }
                        }
                    }
                    if (data.HasValue) break;
                    buscaData = buscaData.ParentNode;
                }

                // Estratégia 2: busca em qualquer <td> da linha que tenha data
                if (!data.HasValue)
                {
                    var tds = bloco.SelectNodes(".//td");
                    if (tds != null)
                    {
                        foreach (var td in tds)
                        {
                            var dm = Regex.Match(td.InnerText.Trim(), @"\d{2}[./]\d{2}[./]\d{2,4}");
                            if (dm.Success) { data = ParseData(dm.Value); break; }
                        }
                    }
                }

                // Grupo: sobe na DOM buscando h2/h3
                string grupo = $"Rodada {rodada}";
                var no = bloco.ParentNode;
                for (int i = 0; i < 8 && no != null; i++)
                {
                    var h = no.SelectSingleNode(
                        ".//h2 | .//h3 | .//div[contains(@class,'content-box-headline')]");
                    if (h != null)
                    {
                        var txt = HtmlEntity.DeEntitize(h.InnerText.Trim());
                        if (!string.IsNullOrWhiteSpace(txt) && txt.Length < 80)
                        { grupo = txt; break; }
                    }
                    no = no.ParentNode;
                }

                jogos.Add(new JogoTM
                {
                    NomeTimeCasa = nomesTime[0],
                    NomeTimeVisitante = nomesTime[1],
                    PlacarCasa = pc,
                    PlacarVisitante = pv,
                    Data = data,
                    Grupo = grupo,
                    Rodada = rodada,
                    LinkDetalhes = href
                });
            }

            return jogos;
        }

        private async Task<List<JogoTM>> BuscarRodadaAsync(
             int saison, int rodada, CancellationToken ct)
        {
            var jogos = new List<JogoTM>();

            var url = $"https://www.transfermarkt.com.br/copa-sudamericana/spieltag/pokalwettbewerb/CS/saison_id/{saison}/spieltag/{rodada}";
            _log.LogInformation("[SulAmericana] Rodada {R}: {U}", rodada, url);

            string html;
            try
            {
                var response = await _http.GetAsync(url, ct);
                if (!response.IsSuccessStatusCode)
                {
                    _log.LogWarning("[SulAmericana] Rodada {R}: status {S}", rodada, (int)response.StatusCode);
                    return jogos;
                }
                html = await response.Content.ReadAsStringAsync(ct);
            }
            catch (Exception ex)
            {
                _log.LogWarning("[SulAmericana] Rodada {R} erro: {M}", rodada, ex.Message);
                return jogos;
            }

            if (!html.Contains("/spielbericht/") && !html.Contains("/begegnung_detail/"))
            {
                _log.LogDebug("[SulAmericana] Rodada {R}: sem links de jogo.", rodada);
                return jogos;
            }

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var tabelas = doc.DocumentNode.SelectNodes(
                "//table[contains(@class,'items')] | //table[@id='yw1']");

            if (tabelas == null) return jogos;

            foreach (var tabela in tabelas)
            {
                var temLink = tabela.SelectSingleNode(
                    ".//a[contains(@href,'/spielbericht/') or contains(@href,'/begegnung_detail/')]");
                if (temLink == null) continue;

                var linhas = tabela.SelectNodes(
                    ".//tbody/tr[not(contains(@class,'thead'))]");
                if (linhas == null) continue;

                foreach (var linha in linhas)
                {
                    var jogo = ParseLinhaJogo(linha, $"Rodada {rodada}");
                    if (jogo != null)
                    {
                        jogo.Rodada = rodada;
                        jogos.Add(jogo);
                    }
                }
            }

            _log.LogInformation("[SulAmericana] Rodada {R}: {N} jogos ({P} com placar)",
                rodada, jogos.Count, jogos.Count(j => j.PlacarCasa.HasValue));

            return jogos;
        }
        // ─── SUBSTITUIR ParseLinhJogo ─────────────────────────────────────────
        private JogoTM? ParseLinhaJogo(HtmlNode linha, string grupo)
        {
            try
            {
                var cols = linha.SelectNodes(".//td");
                if (cols == null || cols.Count < 3) return null;

                // Descarta linhas de cabeçalho repetido ou separador
                var linhaClasse = linha.GetAttributeValue("class", "");
                if (linhaClasse.Contains("thead") || linhaClasse.Contains("head")) return null;

                // ── Data ───────────────────────────────────────────────────────
                DateTime? data = null;
                foreach (var col in cols)
                {
                    var t = col.InnerText.Trim();
                    // dd/MM/yyyy ou dd.MM.yyyy
                    var dm = Regex.Match(t, @"\d{2}[./]\d{2}[./]\d{2,4}");
                    if (dm.Success) { data = ParseData(dm.Value); break; }
                }

                // ── Busca links de times ─────────────────────────────────────
                // Transfermarkt usa /verein/NNN/spielplan/saison_id/YYYY
                // OU /verein/NNN (sem mais nada)
                // Exclui /spieler/ (jogadores) e /wettbewerbe/ (competições)
                var linksTime = linha.SelectNodes(
                    ".//a[contains(@href,'/verein/') and " +
                    "not(contains(@href,'/spieler/')) and " +
                    "not(contains(@href,'/wettbewerbe/'))]");

                // Fallback: pega os links com texto que pareçam nomes de time
                if (linksTime == null || linksTime.Count < 2)
                {
                    linksTime = linha.SelectNodes(".//a[@title and string-length(@title) > 2]");
                }

                if (linksTime == null || linksTime.Count < 2)
                {
                    // Log para diagnóstico
                    _log.LogDebug("[SulAmericana] Linha sem links de time suficientes: {Html}",
                        linha.InnerHtml.Length > 300
                            ? linha.InnerHtml[..300]
                            : linha.InnerHtml);
                    return null;
                }

                // Extrai nomes únicos na ordem de aparição
                var nomesTimeTmp = new List<string>();
                foreach (var lnk in linksTime)
                {
                    // Preferir o atributo title se disponível (mais limpo)
                    var nome = lnk.GetAttributeValue("title", "").Trim();
                    if (string.IsNullOrWhiteSpace(nome))
                        nome = HtmlEntity.DeEntitize(lnk.InnerText.Trim());
                    if (!string.IsNullOrWhiteSpace(nome) && !nomesTimeTmp.Contains(nome))
                        nomesTimeTmp.Add(nome);
                    if (nomesTimeTmp.Count == 2) break;
                }

                if (nomesTimeTmp.Count < 2) return null;

                // ── Placar ────────────────────────────────────────────────────
                int? pc = null, pv = null;
                string linkDetalhes = "";

                // Estratégia 1: link com href /spielbericht/ ou /begegnung_detail/
                var linkRes = linha.SelectSingleNode(
                    ".//a[contains(@href,'/spielbericht/') or " +
                    "contains(@href,'/begegnung_detail/') or " +
                    "contains(@href,'/ergebnis')]");

                if (linkRes != null)
                {
                    var scoreText = HtmlEntity.DeEntitize(linkRes.InnerText.Trim());
                    var m = Regex.Match(scoreText, @"(\d+)\s*[:\-xX×]\s*(\d+)");
                    if (m.Success)
                    {
                        pc = int.Parse(m.Groups[1].Value);
                        pv = int.Parse(m.Groups[2].Value);
                    }
                    linkDetalhes = linkRes.GetAttributeValue("href", "");
                    if (!string.IsNullOrEmpty(linkDetalhes) && !linkDetalhes.StartsWith("http"))
                        linkDetalhes = "https://www.transfermarkt.com.br" + linkDetalhes;
                }

                // Estratégia 2: qualquer célula com padrão "N:N" ou "N-N"
                if (pc == null)
                {
                    foreach (var col in cols)
                    {
                        var ct2 = col.InnerText.Trim();
                        var m = Regex.Match(ct2, @"^(\d+)\s*[:\-]\s*(\d+)$");
                        if (m.Success)
                        {
                            pc = int.Parse(m.Groups[1].Value);
                            pv = int.Parse(m.Groups[2].Value);

                            // Se o link de detalhes ainda não foi encontrado, procura na célula
                            if (string.IsNullOrEmpty(linkDetalhes))
                            {
                                var lnkCell = col.SelectSingleNode(".//a[@href]");
                                if (lnkCell != null)
                                {
                                    linkDetalhes = lnkCell.GetAttributeValue("href", "");
                                    if (!linkDetalhes.StartsWith("http"))
                                        linkDetalhes = "https://www.transfermarkt.com.br" + linkDetalhes;
                                }
                            }
                            break;
                        }
                    }
                }

                // Estratégia 3: span/div com classe de resultado
                if (pc == null)
                {
                    var spanResult = linha.SelectSingleNode(
                        ".//*[contains(@class,'ergebnis') or " +
                        "contains(@class,'result') or " +
                        "contains(@class,'score')]");
                    if (spanResult != null)
                    {
                        var m = Regex.Match(
                            HtmlEntity.DeEntitize(spanResult.InnerText.Trim()),
                            @"(\d+)\s*[:\-]\s*(\d+)");
                        if (m.Success)
                        {
                            pc = int.Parse(m.Groups[1].Value);
                            pv = int.Parse(m.Groups[2].Value);
                        }
                    }
                }

                // ── Rodada ────────────────────────────────────────────────────
                int rodada = 0;
                // Tenta a primeira célula numérica
                foreach (var col in cols)
                {
                    if (int.TryParse(col.InnerText.Trim(), out rodada) && rodada > 0) break;
                }

                var jogoTM = new JogoTM
                {
                    NomeTimeCasa = nomesTimeTmp[0],
                    NomeTimeVisitante = nomesTimeTmp[1],
                    PlacarCasa = pc,
                    PlacarVisitante = pv,
                    Data = data,
                    Grupo = grupo,
                    Rodada = rodada,
                    LinkDetalhes = linkDetalhes
                };

                _log.LogDebug("[SulAmericana] Jogo parseado: {Casa} {Pc}-{Pv} {Vis} | {Data} | Link={Link}",
                    jogoTM.NomeTimeCasa, jogoTM.PlacarCasa, jogoTM.PlacarVisitante,
                    jogoTM.NomeTimeVisitante, jogoTM.Data?.ToString("dd/MM/yy"),
                    string.IsNullOrEmpty(linkDetalhes) ? "—" : "sim");

                return jogoTM;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "[SulAmericana] Erro ao parsear linha.");
                return null;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // SCRAPING DA PÁGINA DE DETALHES DO JOGO
        // ─────────────────────────────────────────────────────────────────────

        public async Task<DetalhesJogoTM?> BuscarDetalhesAsync(
            string url, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            _log.LogInformation("[SulAmericana] Detalhes: {Url}", url);

            string html;
            try
            {
                await Task.Delay(2000, ct); // rate-limit
                html = await _http.GetStringAsync(url, ct);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "[SulAmericana] Falha ao buscar detalhes: {Url}", url);
                return null;
            }

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var detalhes = new DetalhesJogoTM();

            // ── Placar (sb-endstand ou resultado final) ─────────────────────
            var placarNode = doc.DocumentNode.SelectSingleNode(
                "//div[contains(@class,'sb-endstand')] | " +
                "//p[contains(@class,'sb-endstand')] | " +
                "//span[contains(@class,'matchresult')]");
            if (placarNode != null)
            {
                var m = Regex.Match(placarNode.InnerText.Trim(),
                    @"(\d+)\s*[:\-]\s*(\d+)");
                if (m.Success)
                {
                    detalhes.PlacarCasa = int.Parse(m.Groups[1].Value);
                    detalhes.PlacarVisitante = int.Parse(m.Groups[2].Value);
                }
            }

            // ── Formações (ex.: "4-3-3") ────────────────────────────────────
            var formNodes = doc.DocumentNode.SelectNodes(
                "//div[contains(@class,'aufstellung-spielfeld-info')] | " +
                "//span[contains(@class,'aufstellung-formation')]");
            if (formNodes?.Count >= 1)
                detalhes.FormacaoCasa = Regex.Match(
                    formNodes[0].InnerText.Trim(), @"\d-\d-\d(-\d)?").Value;
            if (formNodes?.Count >= 2)
                detalhes.FormacaoVisitante = Regex.Match(
                    formNodes[1].InnerText.Trim(), @"\d-\d-\d(-\d)?").Value;

            // ── Gols ────────────────────────────────────────────────────────
            detalhes.Gols = ExtrairGols(doc);

            // ── Escalações ──────────────────────────────────────────────────
            detalhes.EscalacaoInicialCasa = ExtrairEscalacao(doc, lado: 0, fase: "INICIAL");
            detalhes.EscalacaoInicialVisitante = ExtrairEscalacao(doc, lado: 1, fase: "INICIAL");
            detalhes.EscalacaoFinalCasa = ExtrairEscalacaoFinal(doc, lado: 0);
            detalhes.EscalacaoFinalVisitante = ExtrairEscalacaoFinal(doc, lado: 1);

            // Se não houver escalação final explícita, copia a inicial
            if (!detalhes.EscalacaoFinalCasa.Any())
                detalhes.EscalacaoFinalCasa = detalhes.EscalacaoInicialCasa
                    .Select(j => j with { Fase = "FINAL" }).ToList();
            if (!detalhes.EscalacaoFinalVisitante.Any())
                detalhes.EscalacaoFinalVisitante = detalhes.EscalacaoInicialVisitante
                    .Select(j => j with { Fase = "FINAL" }).ToList();

            return detalhes;
        }

        // ── Gols ───────────────────────────────────────────────────────────
        private List<GolTM> ExtrairGols(HtmlDocument doc)
        {
            var gols = new List<GolTM>();

            // Gols ficam em divs com class "sb-action" ou dentro da timeline sb-spielbericht
            var eventos = doc.DocumentNode.SelectNodes(
                "//div[contains(@class,'sb-action')] | " +
                "//div[contains(@class,'sb-aktion')]");

            if (eventos == null) return gols;

            foreach (var ev in eventos)
            {
                var txt = HtmlEntity.DeEntitize(ev.InnerText);

                // Só processa eventos de gol
                bool isGol = ev.OuterHtml.Contains("Gol") ||
                             ev.OuterHtml.Contains("gol") ||
                             ev.OuterHtml.Contains("Tor") ||
                             ev.SelectSingleNode(".//span[contains(@class,'sb-aktion-gol')]") != null;
                if (!isGol) continue;

                var minMatch = Regex.Match(txt, @"(\d+)'");
                var nomeMatch = ev.SelectSingleNode(
                    ".//a[contains(@href,'/profil/spieler/')]");

                if (!minMatch.Success || nomeMatch == null) continue;

                var nomeGolador = HtmlEntity.DeEntitize(nomeMatch.InnerText.Trim());
                var href = nomeMatch.GetAttributeValue("href", "");
                var idMatch = Regex.Match(href, @"/spieler/(\d+)");

                bool isCasa = ev.GetAttributeValue("class", "").Contains("links") ||
                              ev.GetAttributeValue("class", "").Contains("left") ||
                              ev.ParentNode?.GetAttributeValue("class", "")
                                            .Contains("home") == true;

                bool isContra = txt.Contains("(PE)") || txt.Contains("contra") ||
                                ev.OuterHtml.Contains("Eigentor");

                gols.Add(new GolTM
                {
                    NomeJogador = nomeGolador,
                    IdExterno = idMatch.Success ? long.Parse(idMatch.Groups[1].Value) : null,
                    Minuto = int.Parse(minMatch.Groups[1].Value),
                    IsTimeCasa = isCasa,
                    Contra = isContra
                });
            }

            return gols;
        }

        // ── Escalação inicial (titulares + banco) ──────────────────────────
        private List<JogadorEscalacaoTM> ExtrairEscalacao(
            HtmlDocument doc, int lado, string fase)
        {
            var jogadores = new List<JogadorEscalacaoTM>();

            var blocos = doc.DocumentNode.SelectNodes(
                "//div[contains(@class,'aufstellung-box')] | " +
                "//div[contains(@class,'large-6 columns')] | " +
                "//div[contains(@class,'aufstellung-vereinsseite')]");

            if (blocos == null || blocos.Count <= lado) return jogadores;

            var bloco = blocos[lado];
            var tabelas = bloco.SelectNodes(".//table");
            if (tabelas == null) return jogadores;

            for (int t = 0; t < Math.Min(tabelas.Count, 2); t++)
            {
                bool titular = (t == 0);
                var linhas = tabelas[t].SelectNodes(
                    ".//tr[not(contains(@class,'thead'))]");
                if (linhas == null) continue;

                foreach (var linha in linhas)
                {
                    var j = ParseJogadorLinha(linha, titular, fase);
                    if (j != null) jogadores.Add(j);
                }
            }

            return jogadores;
        }

        // ── Escalação final (considera substituições) ──────────────────────
        private List<JogadorEscalacaoTM> ExtrairEscalacaoFinal(
            HtmlDocument doc, int lado)
        {
            // A escalação final é a inicial com substituições aplicadas.
            // O Transfermarkt lista substituições em div class="sb-wechsel" ou similar.
            var iniciais = ExtrairEscalacao(doc, lado, "FINAL");

            // Busca substituições para este lado
            var substituicoes = ExtrairSubstituicoes(doc, lado);

            // Aplica: remove jogador que saiu, adiciona quem entrou
            foreach (var sub in substituicoes)
            {
                iniciais.RemoveAll(j => j.IdExterno == sub.IdSaiu ||
                                        NomesSimilares(j.Nome, sub.NomeSaiu));
                iniciais.Add(new JogadorEscalacaoTM
                {
                    Nome = sub.NomeEntrou,
                    IdExterno = sub.IdEntrou,
                    Titular = true,
                    Posicao = "",
                    Fase = "FINAL"
                });
            }

            return iniciais;
        }

        private List<SubstituicaoTM> ExtrairSubstituicoes(HtmlDocument doc, int lado)
        {
            var subs = new List<SubstituicaoTM>();

            // Substituições ficam em divs com ícone de flecha ou class sb-wechsel
            var eventos = doc.DocumentNode.SelectNodes(
                "//div[contains(@class,'sb-wechsel')] | " +
                "//div[contains(@class,'sb-aktion-wechsel')]");

            if (eventos == null) return subs;

            // Filtra pelo lado (home/away)
            var eventosFiltrados = eventos
                .Where(e =>
                {
                    var cls = e.GetAttributeValue("class", "") +
                              (e.ParentNode?.GetAttributeValue("class", "") ?? "");
                    return lado == 0
                        ? cls.Contains("home") || cls.Contains("links") || cls.Contains("left")
                        : cls.Contains("away") || cls.Contains("rechts") || cls.Contains("right");
                }).ToList();

            // Se não filtrou por lado, tenta dividir pelo índice (metade)
            if (!eventosFiltrados.Any())
            {
                var todos = eventos.ToList();
                int metade = todos.Count / 2;
                eventosFiltrados = lado == 0
                    ? todos.Take(metade).ToList()
                    : todos.Skip(metade).ToList();
            }

            foreach (var ev in eventosFiltrados)
            {
                var links = ev.SelectNodes(
                    ".//a[contains(@href,'/profil/spieler/')]");
                if (links == null || links.Count < 2) continue;

                string NomeLink(HtmlNode l) =>
                    HtmlEntity.DeEntitize(l.InnerText.Trim());

                long? IdLink(HtmlNode l)
                {
                    var m = Regex.Match(l.GetAttributeValue("href", ""), @"/spieler/(\d+)");
                    return m.Success ? long.Parse(m.Groups[1].Value) : null;
                }

                subs.Add(new SubstituicaoTM
                {
                    NomeEntrou = NomeLink(links[0]),
                    IdEntrou = IdLink(links[0]),
                    NomeSaiu = NomeLink(links[1]),
                    IdSaiu = IdLink(links[1])
                });
            }

            return subs;
        }

        private JogadorEscalacaoTM? ParseJogadorLinha(
            HtmlNode linha, bool titular, string fase)
        {
            try
            {
                var link = linha.SelectSingleNode(
                    ".//a[contains(@href,'/profil/spieler/') or " +
                    "contains(@href,'/spieler/')]");
                if (link == null) return null;

                var nome = HtmlEntity.DeEntitize(link.InnerText.Trim());
                if (string.IsNullOrWhiteSpace(nome)) return null;

                // Número
                var numCel = linha.SelectSingleNode(
                    ".//div[contains(@class,'rn_nummer')] | " +
                    ".//td[contains(@class,'rn_nummer')] | " +
                    ".//td[@class='zentriert'][1]");
                int? numero = null;
                if (numCel != null &&
                    int.TryParse(numCel.InnerText.Trim(), out int n))
                    numero = n;

                // Posição
                var posImg = linha.SelectSingleNode(".//img[@title]");
                var posicao = posImg?.GetAttributeValue("title", "") ?? "";

                // ID externo
                var href = link.GetAttributeValue("href", "");
                var idMatch = Regex.Match(href, @"/spieler/(\d+)");
                long? id = idMatch.Success
                    ? long.Parse(idMatch.Groups[1].Value) : null;

                return new JogadorEscalacaoTM
                {
                    Nome = nome,
                    Numero = numero,
                    Posicao = posicao,
                    Titular = titular,
                    IdExterno = id,
                    Fase = fase
                };
            }
            catch { return null; }
        }

        // ─────────────────────────────────────────────────────────────────────
        // LÓGICA DE SINCRONIZAÇÃO COM O BANCO
        // ─────────────────────────────────────────────────────────────────────

        private async Task ProcessarJogoAsync_V2(
            FutebolContext context,
            JogoTM jogoWeb,
            List<Jogo> jogosBanco,
            bool importarEscalacoes,
            SincronizacaoResultado resultado,
            CancellationToken ct)
        {
            var timeCasa = ResolverTime(context, jogoWeb.NomeTimeCasa);
            var timeVis = ResolverTime(context, jogoWeb.NomeTimeVisitante);

            if (timeCasa == null || timeVis == null)
            {
                resultado.JogosNaoEncontrados++;
                resultado.Avisos.Add(
                    $"Times não mapeados: '{jogoWeb.NomeTimeCasa}' ({(timeCasa == null ? "NÃO ACHADO" : "ok")}) " +
                    $"x '{jogoWeb.NomeTimeVisitante}' ({(timeVis == null ? "NÃO ACHADO" : "ok")})");
                _log.LogWarning("[SulAmericana] Time não mapeado — Casa='{C}' Vis='{V}'",
                    jogoWeb.NomeTimeCasa, jogoWeb.NomeTimeVisitante);
                return;
            }

            var jogoBanco = LocalizarJogoBanco_V2(
                jogosBanco, timeCasa.Id, timeVis.Id,
                jogoWeb.Data, timeCasa.Nome, timeVis.Nome);

            if (jogoBanco == null)
            {
                resultado.JogosNaoEncontrados++;
                resultado.Avisos.Add(
                    $"Jogo não encontrado no banco: {timeCasa.Nome} x {timeVis.Nome} " +
                    $"({jogoWeb.Data?.ToString("dd/MM/yyyy") ?? "sem data"}) " +
                    $"— verifique se foi importado com o JSON correto.");
                return;
            }

            bool alterado = false;

            if (jogoWeb.PlacarCasa.HasValue && jogoWeb.PlacarVisitante.HasValue)
            {
                if (jogoBanco.PlacarCasa != jogoWeb.PlacarCasa ||
                    jogoBanco.PlacarVisitante != jogoWeb.PlacarVisitante)
                {
                    _log.LogInformation(
                        "[SulAmericana] ✅ Atualizando placar: {H} x {A} | {OC}-{OV} → {NC}-{NV}",
                        timeCasa.Nome, timeVis.Nome,
                        jogoBanco.PlacarCasa, jogoBanco.PlacarVisitante,
                        jogoWeb.PlacarCasa, jogoWeb.PlacarVisitante);

                    jogoBanco.PlacarCasa = jogoWeb.PlacarCasa;
                    jogoBanco.PlacarVisitante = jogoWeb.PlacarVisitante;
                    resultado.PlacaresAtualizados++;
                    alterado = true;
                }
                else
                {
                    _log.LogInformation(
                        "[SulAmericana] Placar já atualizado: {H} {C}-{V} {A}",
                        timeCasa.Nome, jogoBanco.PlacarCasa, jogoBanco.PlacarVisitante, timeVis.Nome);
                }
            }

            if (!string.IsNullOrWhiteSpace(jogoWeb.Grupo) &&
                string.IsNullOrWhiteSpace(jogoBanco.Grupo))
            {
                jogoBanco.Grupo = jogoWeb.Grupo;
                alterado = true;
            }

            if (importarEscalacoes && !string.IsNullOrWhiteSpace(jogoWeb.LinkDetalhes))
            {
                var detalhes = await BuscarDetalhesAsync(jogoWeb.LinkDetalhes, ct);
                if (detalhes != null)
                {
                    if (detalhes.PlacarCasa.HasValue)
                    {
                        jogoBanco.PlacarCasa = detalhes.PlacarCasa;
                        jogoBanco.PlacarVisitante = detalhes.PlacarVisitante;
                    }

                    await ImportarGolsAsync(context, jogoBanco, detalhes.Gols,
                        timeCasa, timeVis, resultado, ct);
                    await ImportarEscalacoesAsync(context, jogoBanco, detalhes,
                        timeCasa, timeVis, resultado, ct);

                    alterado = true;
                }
            }

            if (alterado)
            {
                jogoBanco.Atualizado = 1;
                resultado.JogosAtualizados++;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // IMPORTAÇÃO DE GOLS
        // ─────────────────────────────────────────────────────────────────────

        private async Task ImportarGolsAsync(
            FutebolContext context,
            Jogo jogo,
            List<GolTM> golsTM,
            Time timeCasa,
            Time timeVisitante,
            SincronizacaoResultado resultado,
            CancellationToken ct)
        {
            if (!golsTM.Any()) return;

            // Remove gols anteriores para reimportar
            if (jogo.Gols?.Any() == true)
                context.Gols.RemoveRange(jogo.Gols);

            foreach (var golTM in golsTM)
            {
                var time = golTM.IsTimeCasa ? timeCasa : timeVisitante;
                var jogador = await ResolverJogadorAsync(
                    context, golTM.NomeJogador, golTM.IdExterno, time.Id, ct);

                if (jogador == null)
                {
                    resultado.Avisos.Add(
                        $"Gol: jogador '{golTM.NomeJogador}' ({time.Nome}) " +
                        $"não encontrado — adicione ao dicionário.");
                    continue;
                }

                context.Gols.Add(new Gol
                {
                    JogoId = jogo.Id,
                    JogadorId = jogador.Id,
                    Minuto = golTM.Minuto,
                    Contra = golTM.Contra
                });
                resultado.GolsImportados++;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // IMPORTAÇÃO DE ESCALAÇÕES (INICIAL + FINAL)
        // ─────────────────────────────────────────────────────────────────────

        private async Task ImportarEscalacoesAsync(
            FutebolContext context,
            Jogo jogo,
            DetalhesJogoTM detalhes,
            Time timeCasa,
            Time timeVisitante,
            SincronizacaoResultado resultado,
            CancellationToken ct)
        {
            // Remove escalações existentes antes de reimportar
            if (jogo.Escalacoes?.Any() == true)
                context.Escalacoes.RemoveRange(jogo.Escalacoes);

            // Busca posições da formação padrão (id = 20)
            var posicoes = await context.PosicoesFormacao
                .Where(p => p.FormacaoId == FORMACAO_PADRAO_ID)
                .OrderBy(p => p.Ordem)
                .ToListAsync(ct);

            // Fallback: qualquer formação disponível
            if (!posicoes.Any())
            {
                posicoes = await context.PosicoesFormacao
                    .OrderBy(p => p.FormacaoId).ThenBy(p => p.Ordem)
                    .ToListAsync(ct);
            }

            // Atualiza FormacaoCasaId / FormacaoVisitanteId no jogo
            if (!string.IsNullOrWhiteSpace(detalhes.FormacaoCasa))
            {
                var f = await context.Formacoes
                    .FirstOrDefaultAsync(x => x.Nome == detalhes.FormacaoCasa, ct);
                if (f != null) jogo.FormacaoCasaId = f.Id;
            }
            if (jogo.FormacaoCasaId == null || jogo.FormacaoCasaId == 0)
                jogo.FormacaoCasaId = FORMACAO_PADRAO_ID;

            if (!string.IsNullOrWhiteSpace(detalhes.FormacaoVisitante))
            {
                var f = await context.Formacoes
                    .FirstOrDefaultAsync(x => x.Nome == detalhes.FormacaoVisitante, ct);
                if (f != null) jogo.FormacaoVisitanteId = f.Id;
            }
            if (jogo.FormacaoVisitanteId == null || jogo.FormacaoVisitanteId == 0)
                jogo.FormacaoVisitanteId = FORMACAO_PADRAO_ID;

            // Importa as quatro listas
            await ImportarListaEscalacaoAsync(context, jogo,
                detalhes.EscalacaoInicialCasa, timeCasa,
                isTimeCasa: true, fase: "INICIAL", posicoes, resultado, ct);

            await ImportarListaEscalacaoAsync(context, jogo,
                detalhes.EscalacaoInicialVisitante, timeVisitante,
                isTimeCasa: false, fase: "INICIAL", posicoes, resultado, ct);

            await ImportarListaEscalacaoAsync(context, jogo,
                detalhes.EscalacaoFinalCasa, timeCasa,
                isTimeCasa: true, fase: "FINAL", posicoes, resultado, ct);

            await ImportarListaEscalacaoAsync(context, jogo,
                detalhes.EscalacaoFinalVisitante, timeVisitante,
                isTimeCasa: false, fase: "FINAL", posicoes, resultado, ct);

            resultado.EscalacoesImportadas++;
        }

        private async Task ImportarListaEscalacaoAsync(
            FutebolContext context,
            Jogo jogo,
            List<JogadorEscalacaoTM> lista,
            Time time,
            bool isTimeCasa,
            string fase,
            List<PosicaoFormacao> posicoes,
            SincronizacaoResultado resultado,
            CancellationToken ct)
        {
            int posIdx = 0;

            foreach (var jogTM in lista)
            {
                var jogador = await ResolverJogadorAsync(
                    context, jogTM.Nome, jogTM.IdExterno, time.Id, ct);

                double posX = posIdx < posicoes.Count
                    ? posicoes[posIdx].PosicaoX : 50;
                double posY = posIdx < posicoes.Count
                    ? posicoes[posIdx].PosicaoY : 50;

                context.Escalacoes.Add(new Escalacao
                {
                    JogoId = jogo.Id,
                    JogadorId = jogador?.Id,
                    Titular = jogTM.Titular,
                    IsTimeCasa = isTimeCasa,
                    Posicao = MapearPosicao(jogTM.Posicao),
                    PosicaoX = posX,
                    PosicaoY = posY,
                    FaseEscalacao = fase
                });

                if (jogador == null)
                    resultado.Avisos.Add(
                        $"[{fase}] Jogador '{jogTM.Nome}' ({time.Nome}) " +
                        $"não localizado — slot criado sem vínculo.");

                if (jogTM.Titular) posIdx++;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // RESOLUÇÃO DE JOGADOR NO BANCO
        // ─────────────────────────────────────────────────────────────────────

        private async Task<Jogador?> ResolverJogadorAsync(
            FutebolContext context,
            string nomeTM,
            long? idExterno,
            int timeId,
            CancellationToken ct)
        {
            // Normaliza pelo helper
            nomeTM = JogadoresSulamericanaHelper.NormalizarNome(nomeTM);


            // 1. Por IdApi (se importado antes)
            if (idExterno.HasValue)
            {
                var j = await context.Jogadores.FirstOrDefaultAsync(
                    x => x.IdApi == idExterno && x.TimeId == timeId, ct);
                if (j != null) return j;
            }

            // 2. Nome exato
            {
                var j = await context.Jogadores.FirstOrDefaultAsync(
                    x => x.Nome == nomeTM && x.TimeId == timeId, ct);
                if (j != null) return j;
            }

            // 3. ILike (case-insensitive)
            {
                var j = await context.Jogadores.FirstOrDefaultAsync(
                    x => x.TimeId == timeId &&
                         EF.Functions.ILike(x.Nome, nomeTM), ct);
                if (j != null) return j;
            }

            // 4. Por sobrenome (último token)
            var sob = nomeTM.Split(' ').Last().Trim();
            if (sob.Length > 3)
            {
                var j = await context.Jogadores.FirstOrDefaultAsync(
                    x => x.TimeId == timeId &&
                         EF.Functions.ILike(x.Nome, $"%{sob}%"), ct);
                if (j != null) return j;
            }

            // 5. Nome normalizado (sem acentos)
            var nomeNorm = NormalizarTexto(nomeTM);
            {
                var candidatos = await context.Jogadores
                    .Where(x => x.TimeId == timeId)
                    .ToListAsync(ct);
                var j = candidatos.FirstOrDefault(x =>
                    NormalizarTexto(x.Nome) == nomeNorm ||
                    NormalizarTexto(x.Nome).Contains(nomeNorm) ||
                    nomeNorm.Contains(NormalizarTexto(x.Nome)));
                if (j != null) return j;
            }

            return null;
        }

        // ─────────────────────────────────────────────────────────────────────
        // HELPERS
        // ─────────────────────────────────────────────────────────────────────

        private Time? ResolverTime(FutebolContext context, string nomeWeb)
        {
            if (string.IsNullOrWhiteSpace(nomeWeb)) return null;

            var nome = TimesSulamericanaHelper.NormalizarNome(nomeWeb);
            return context.Times.AsEnumerable().FirstOrDefault(t =>
                string.Equals(t.Nome, nome, StringComparison.OrdinalIgnoreCase) ||
                NormalizarTexto(t.Nome) == NormalizarTexto(nome) ||
                NormalizarTexto(t.Nome).Contains(NormalizarTexto(nome)) ||
                NormalizarTexto(nome).Contains(NormalizarTexto(t.Nome)));
        }

        // Versão com log detalhado para ajudar no diagnóstico
        private Jogo? LocalizarJogoBanco_V2(
            List<Jogo> jogosBanco,
            int casaId, int visId,
            DateTime? dataWeb,
            string nomeCasa, string nomeVis)
        {
            // Tentativa 1: times corretos + data próxima (±2 dias)
            if (dataWeb.HasValue)
            {
                var j = jogosBanco.FirstOrDefault(jg =>
                    jg.TimeCasaId == casaId && jg.TimeVisitanteId == visId &&
                    Math.Abs((jg.Data.Date - dataWeb.Value.Date).TotalDays) <= 2);
                if (j != null)
                {
                    _log.LogInformation("[SulAmericana] Jogo localizado (data): {H} x {V} em {D}",
                        nomeCasa, nomeVis, j.Data.ToString("dd/MM/yyyy"));
                    return j;
                }
            }

            // Tentativa 2: times corretos sem data (qualquer jogo entre esses dois times)
            var jSemData = jogosBanco.FirstOrDefault(jg =>
                jg.TimeCasaId == casaId && jg.TimeVisitanteId == visId);
            if (jSemData != null)
            {
                _log.LogWarning(
                    "[SulAmericana] Jogo localizado SEM data: {H} x {V} " +
                    "(site={DS}, banco={DB}) — pode ser o jogo errado",
                    nomeCasa, nomeVis,
                    dataWeb?.ToString("dd/MM/yyyy") ?? "?",
                    jSemData.Data.ToString("dd/MM/yyyy"));
                return jSemData;
            }

            // Nada encontrado — log com context para facilitar diagnóstico
            _log.LogWarning(
                "[SulAmericana] ❌ Jogo NÃO encontrado: {H}(id={CId}) x {V}(id={VId}) " +
                "data={D} | Total no banco: {Total} | " +
                "Com CasaId={CId2}: {CC} | Com VisId={VId2}: {VC}",
                nomeCasa, casaId, nomeVis, visId,
                dataWeb?.ToString("dd/MM/yyyy") ?? "?",
                jogosBanco.Count,
                casaId, jogosBanco.Count(jg => jg.TimeCasaId == casaId),
                visId, jogosBanco.Count(jg => jg.TimeVisitanteId == visId));

            return null;
        }

        private static bool NomesSimilares(string a, string b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
                return false;
            return NormalizarTexto(a) == NormalizarTexto(b) ||
                   NormalizarTexto(a).Contains(NormalizarTexto(b)) ||
                   NormalizarTexto(b).Contains(NormalizarTexto(a));
        }

        private static string NormalizarTexto(string s) =>
            Regex.Replace(
                s.ToLowerInvariant()
                 .Replace("á", "a").Replace("é", "e").Replace("í", "i")
                 .Replace("ó", "o").Replace("ú", "u").Replace("ã", "a")
                 .Replace("ê", "e").Replace("â", "a").Replace("ô", "o")
                 .Replace("ç", "c").Replace("ñ", "n"),
                @"\b(cr|fc|sc|ec|ac|se|de|do|da|dos|las|los|club|club)\b|\s+", "");

        private static string MapearPosicao(string? p) => p?.ToLower() switch
        {
            var s when s?.Contains("gol") == true ||
                       s?.Contains("keeper") == true => "GL",
            var s when s?.Contains("zagueiro") == true ||
                       s?.Contains("lateral") == true ||
                       s?.Contains("defesa") == true ||
                       s?.Contains("defensor") == true => "ZG",
            var s when s?.Contains("meio") == true ||
                       s?.Contains("volante") == true ||
                       s?.Contains("meia") == true => "MC",
            var s when s?.Contains("atacante") == true ||
                       s?.Contains("centroavante") == true ||
                       s?.Contains("ponta") == true => "AT",
            _ => "MC"
        };

        private static DateTime? ParseData(string texto)
        {
            var m = Regex.Match(texto.Trim(), @"\d{2}[./]\d{2}[./]\d{2,4}");
            if (!m.Success) return null;
            string[] fmts = { "dd/MM/yy", "dd/MM/yyyy", "dd.MM.yyyy", "dd.MM.yy" };
            foreach (var f in fmts)
                if (DateTime.TryParseExact(m.Value, f,
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                    return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            return null;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DTOs
    // ─────────────────────────────────────────────────────────────────────────

    public class JogoTM
    {
        public string NomeTimeCasa { get; set; } = "";
        public string NomeTimeVisitante { get; set; } = "";
        public int? PlacarCasa { get; set; }
        public int? PlacarVisitante { get; set; }
        public DateTime? Data { get; set; }
        public string Grupo { get; set; } = "";
        public int Rodada { get; set; }
        public string LinkDetalhes { get; set; } = "";
    }

    public class DetalhesJogoTM
    {
        public int? PlacarCasa { get; set; }
        public int? PlacarVisitante { get; set; }
        public string FormacaoCasa { get; set; } = "";
        public string FormacaoVisitante { get; set; } = "";
        public List<GolTM> Gols { get; set; } = new();
        public List<JogadorEscalacaoTM> EscalacaoInicialCasa { get; set; } = new();
        public List<JogadorEscalacaoTM> EscalacaoInicialVisitante { get; set; } = new();
        public List<JogadorEscalacaoTM> EscalacaoFinalCasa { get; set; } = new();
        public List<JogadorEscalacaoTM> EscalacaoFinalVisitante { get; set; } = new();
    }

    public record JogadorEscalacaoTM
    {
        public string Nome { get; init; } = "";
        public int? Numero { get; init; }
        public string Posicao { get; init; } = "";
        public bool Titular { get; init; }
        public long? IdExterno { get; init; }
        public string Fase { get; init; } = "INICIAL";
    }

    public class GolTM
    {
        public string NomeJogador { get; set; } = "";
        public long? IdExterno { get; set; }
        public int Minuto { get; set; }
        public bool IsTimeCasa { get; set; }
        public bool Contra { get; set; }
    }

    public class SubstituicaoTM
    {
        public string NomeEntrou { get; set; } = "";
        public long? IdEntrou { get; set; }
        public string NomeSaiu { get; set; } = "";
        public long? IdSaiu { get; set; }
    }

    public class SincronizacaoResultado
    {
        public int JogosEncontradosNaSite { get; set; }
        public int JogosAtualizados { get; set; }
        public int JogosNaoEncontrados { get; set; }
        public int PlacaresAtualizados { get; set; }
        public int EscalacoesImportadas { get; set; }
        public int GolsImportados { get; set; }
        public List<string> Avisos { get; } = new();

        public override string ToString() =>
            $"Site:{JogosEncontradosNaSite} | Atualizados:{JogosAtualizados} | " +
            $"Placares:{PlacaresAtualizados} | Escalações:{EscalacoesImportadas} | " +
            $"Gols:{GolsImportados} | NãoEncontrados:{JogosNaoEncontrados} | " +
            $"Avisos:{Avisos.Count}";
    }
}