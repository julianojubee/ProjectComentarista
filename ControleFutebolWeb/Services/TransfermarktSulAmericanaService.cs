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

        // ── DICIONÁRIO DE NOMES: Transfermarkt → banco local ──────────────────
        // Adicione linhas sempre que o nome no site divergir do cadastrado localmente.
        private static readonly Dictionary<string, string> _mapaTimesNomes =
            new(StringComparer.OrdinalIgnoreCase)
        {
            // Brasil
            { "Athletico Paranaense",        "Athletico Paranaense" },
            { "Athletico-PR",                "Athletico Paranaense" },
            { "Club Athletico Paranaense",   "Athletico Paranaense" },
            { "Fortaleza EC",                "Fortaleza" },
            { "Clube de Regatas do Flamengo","Flamengo" },
            { "Fluminense FC",               "Fluminense" },
            { "SC Internacional",            "Internacional" },
            { "Grêmio FBPA",                 "Grêmio" },
            { "Santos FC",                   "Santos" },
            { "Ceará SC",                    "Ceará" },
            { "Sport Club Corinthians",      "Corinthians" },
            { "SE Palmeiras",                "Palmeiras" },
            // Argentina
            { "Club Atlético Independiente", "Independiente" },
            { "Racing Club",                 "Racing Club" },
            { "River Plate",                 "River Plate" },
            { "Boca Juniors",                "Boca Juniors" },
            // Colômbia
            { "Deportivo Independiente Medellín", "Ind. Medellín" },
            { "Atlético Nacional",           "Atlético Nacional" },
            // Chile
            { "Club Universidad de Chile",   "Universidad de Chile" },
            { "Colo-Colo",                   "Colo-Colo" },
            // Bolívia
            { "Club Guabirá",                "Guabira" },
            { "Club Bolívar",                "Bolívar" },
            // Equador
            { "Liga Deportiva Universitaria","LDU Quito" },
            { "Club Deportivo El Nacional",  "El Nacional" },
            // Peru
            { "Club Universitario de Deportes","Universitario" },
            { "Alianza Lima",                "Alianza Lima" },
            // Uruguai
            { "Club Nacional de Football",   "Nacional" },
            { "Peñarol",                     "Peñarol" },
            // Venezuela
            { "Caracas FC",                  "Caracas" },
            { "Deportivo Táchira FC",        "Táchira" },
            // Paraguai
            { "Olimpia",                     "Olimpia" },
            { "Cerro Porteño",               "Cerro Porteño" },
        };

        // ── DICIONÁRIO DE JOGADORES: nome no site → nome no banco ─────────────
        private static readonly Dictionary<string, string> _mapaJogadoresNomes =
            new(StringComparer.OrdinalIgnoreCase)
        {
            // Exemplos – expanda conforme encontrar divergências nos logs
            { "J. E. Cristaldo",  "Jonathan Cristaldo" },
            { "G. Cristaldo",     "Gustavo Cristaldo" },
            { "Thiago Ribeiro",   "Thiago Ribeiro" },
        };

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
                .Where(j => j.CompeticaoId == competicaoIdNoBanco && j.Atualizado == 0 )
                .ToListAsync(ct);

            _log.LogInformation("[SulAmericana] {Total} jogos no banco para verificar.", jogosBanco.Count);

            // 3. Processa cada jogo encontrado no site
            foreach (var jogoWeb in jogosWeb.Where(j => j.PlacarCasa.HasValue))
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    await ProcessarJogoAsync(context, jogoWeb, jogosBanco,
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
            return resultado;
        }

        // ─────────────────────────────────────────────────────────────────────
        // SCRAPING DO CALENDÁRIO (grupos e rodadas)
        // ─────────────────────────────────────────────────────────────────────

        public async Task<List<JogoTM>> BuscarCalendarioAsync(
            int ano = 2025, CancellationToken ct = default)
        {
            var jogos = new List<JogoTM>();
            var url = $"https://www.transfermarkt.com.br/copa-sudamericana/" +
                      $"gesamtspielplan/pokalwettbewerb/CS/saison_id/{ano}";

            _log.LogInformation("[SulAmericana] Calendário: {Url}", url);

            string html;
            try { html = await _http.GetStringAsync(url, ct); }
            catch (Exception ex)
            {
                _log.LogError(ex, "[SulAmericana] Falha ao acessar calendário.");
                return jogos;
            }

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Cada fase/grupo aparece em uma tabela class="items"
            var tabelas = doc.DocumentNode
                .SelectNodes("//table[contains(@class,'items')]");

            if (tabelas == null || !tabelas.Any())
            {
                _log.LogWarning("[SulAmericana] Nenhuma tabela encontrada no calendário.");
                return jogos;
            }

            foreach (var tabela in tabelas)
            {
                var grupo = ExtrairNomeGrupo(tabela);
                var linhas = tabela.SelectNodes(
                    ".//tbody/tr[not(contains(@class,'thead'))]");
                if (linhas == null) continue;

                foreach (var linha in linhas)
                {
                    var jogo = ParseLinhJogo(linha, grupo);
                    if (jogo != null) jogos.Add(jogo);
                }
            }

            _log.LogInformation("[SulAmericana] {Total} jogos no calendário.", jogos.Count);
            return jogos;
        }

        private string ExtrairNomeGrupo(HtmlNode tabela)
        {
            var no = tabela.ParentNode;
            for (int i = 0; i < 6 && no != null; i++)
            {
                var h = no.SelectSingleNode(
                    ".//h2 | .//h3 | .//div[contains(@class,'content-box-headline')]");
                if (h != null)
                {
                    var txt = HtmlEntity.DeEntitize(h.InnerText.Trim());
                    if (!string.IsNullOrWhiteSpace(txt)) return txt;
                }
                no = no.ParentNode;
            }
            return "";
        }

        private JogoTM? ParseLinhJogo(HtmlNode linha, string grupo)
        {
            try
            {
                var cols = linha.SelectNodes(".//td");
                if (cols == null || cols.Count < 4) return null;

                // ── Data ────────────────────────────────────────────────────
                DateTime? data = null;
                foreach (var col in cols)
                {
                    var t = col.InnerText.Trim();
                    if (Regex.IsMatch(t, @"\d{2}[./]\d{2}[./]\d{2,4}"))
                    { data = ParseData(t); break; }
                }

                // ── Times (links /verein/) ───────────────────────────────────
                var linksTime = linha.SelectNodes(
                    ".//a[contains(@href,'/verein/') or contains(@href,'/spielplan/')]");
                if (linksTime == null || linksTime.Count < 2) return null;

                var nomes = linksTime
                    .Select(l => HtmlEntity.DeEntitize(l.InnerText.Trim()))
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct().ToList();
                if (nomes.Count < 2) return null;

                // ── Placar ───────────────────────────────────────────────────
                int? pc = null, pv = null;
                var linkRes = linha.SelectSingleNode(
                    ".//a[contains(@href,'/spielbericht/') or " +
                    "contains(@href,'/begegnung_detail/')]");

                if (linkRes != null)
                {
                    var m = Regex.Match(
                        HtmlEntity.DeEntitize(linkRes.InnerText.Trim()),
                        @"(\d+)\s*[:\-x×]\s*(\d+)");
                    if (m.Success)
                    { pc = int.Parse(m.Groups[1].Value); pv = int.Parse(m.Groups[2].Value); }
                }
                // fallback: célula com "N:N"
                if (pc == null)
                {
                    foreach (var col in cols)
                    {
                        var m = Regex.Match(col.InnerText.Trim(),
                            @"^(\d+)\s*[:\-x×]\s*(\d+)$");
                        if (m.Success)
                        { pc = int.Parse(m.Groups[1].Value); pv = int.Parse(m.Groups[2].Value); break; }
                    }
                }

                // ── Link detalhes ────────────────────────────────────────────
                var href = linkRes?.GetAttributeValue("href", "") ?? "";
                if (!string.IsNullOrEmpty(href) && !href.StartsWith("http"))
                    href = "https://www.transfermarkt.com.br" + href;

                // ── Rodada ───────────────────────────────────────────────────
                int.TryParse(cols[0].InnerText.Trim(), out int rodada);

                return new JogoTM
                {
                    NomeTimeCasa = nomes[0],
                    NomeTimeVisitante = nomes[1],
                    PlacarCasa = pc,
                    PlacarVisitante = pv,
                    Data = data,
                    Grupo = grupo,
                    Rodada = rodada,
                    LinkDetalhes = href
                };
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "[SulAmericana] Falha ao parsear linha do calendário.");
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

        private async Task ProcessarJogoAsync(
            FutebolContext context,
            JogoTM jogoWeb,
            List<Jogo> jogosBanco,
            bool importarEscalacoes,
            SincronizacaoResultado resultado,
            CancellationToken ct)
        {
            // 1. Resolve times
            var timeCasa = ResolverTime(context, jogoWeb.NomeTimeCasa);
            var timeVis = ResolverTime(context, jogoWeb.NomeTimeVisitante);

            if (timeCasa == null || timeVis == null)
            {
                resultado.JogosNaoEncontrados++;
                resultado.Avisos.Add(
                    $"Times não mapeados: '{jogoWeb.NomeTimeCasa}' x " +
                    $"'{jogoWeb.NomeTimeVisitante}' — adicione ao dicionário.");
                return;
            }

            // 2. Localiza no banco
            var jogoBanco = LocalizarJogoBanco(
                jogosBanco, timeCasa.Id, timeVis.Id, jogoWeb.Data);

            if (jogoBanco == null)
            {
                resultado.JogosNaoEncontrados++;
                resultado.Avisos.Add(
                    $"Jogo não cadastrado: {timeCasa.Nome} x {timeVis.Nome} " +
                    $"({jogoWeb.Data?.ToString("dd/MM/yyyy") ?? "sem data"})");
                return;
            }

            bool alterado = false;

            // 3. Atualiza placar
            if (jogoWeb.PlacarCasa.HasValue && jogoWeb.PlacarVisitante.HasValue)
            {
                if (jogoBanco.PlacarCasa != jogoWeb.PlacarCasa ||
                    jogoBanco.PlacarVisitante != jogoWeb.PlacarVisitante)
                {
                    _log.LogInformation(
                        "[SulAmericana] Placar {H}x{A}: banco={OC}-{OV} → site={NC}-{NV}",
                        timeCasa.Nome, timeVis.Nome,
                        jogoBanco.PlacarCasa, jogoBanco.PlacarVisitante,
                        jogoWeb.PlacarCasa, jogoWeb.PlacarVisitante);

                    jogoBanco.PlacarCasa = jogoWeb.PlacarCasa;
                    jogoBanco.PlacarVisitante = jogoWeb.PlacarVisitante;
                    resultado.PlacaresAtualizados++;
                    alterado = true;
                }
            }

            // 4. Atualiza grupo se estava vazio
            if (!string.IsNullOrWhiteSpace(jogoWeb.Grupo) &&
                string.IsNullOrWhiteSpace(jogoBanco.Grupo))
            {
                jogoBanco.Grupo = jogoWeb.Grupo;
                alterado = true;
            }

            // 5. Busca detalhes (gols + escalações)
            if (importarEscalacoes && !string.IsNullOrWhiteSpace(jogoWeb.LinkDetalhes))
            {
                var detalhes = await BuscarDetalhesAsync(jogoWeb.LinkDetalhes, ct);
                if (detalhes != null)
                {
                    // Atualiza placar pelo detalhe (mais confiável)
                    if (detalhes.PlacarCasa.HasValue)
                    {
                        jogoBanco.PlacarCasa = detalhes.PlacarCasa;
                        jogoBanco.PlacarVisitante = detalhes.PlacarVisitante;
                    }

                    // Gols
                    await ImportarGolsAsync(
                        context, jogoBanco, detalhes.Gols,
                        timeCasa, timeVis, resultado, ct);

                    // Escalações
                    await ImportarEscalacoesAsync(
                        context, jogoBanco, detalhes,
                        timeCasa, timeVis, resultado, ct);

                    alterado = true;
                }
            }

            // 6. Marca como atualizado para não reprocessar
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
            // Normaliza pelo dicionário
            if (_mapaJogadoresNomes.TryGetValue(nomeTM.Trim(), out var nomeMapeado))
                nomeTM = nomeMapeado;

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

            var nome = nomeWeb.Trim();
            if (_mapaTimesNomes.TryGetValue(nome, out var mapped)) nome = mapped;

            return context.Times.AsEnumerable().FirstOrDefault(t =>
                string.Equals(t.Nome, nome, StringComparison.OrdinalIgnoreCase) ||
                NormalizarTexto(t.Nome) == NormalizarTexto(nome) ||
                NormalizarTexto(t.Nome).Contains(NormalizarTexto(nome)) ||
                NormalizarTexto(nome).Contains(NormalizarTexto(t.Nome)));
        }

        private static Jogo? LocalizarJogoBanco(
            List<Jogo> jogosBanco, int casaId, int visId, DateTime? dataWeb)
        {
            if (dataWeb.HasValue)
            {
                var j = jogosBanco.FirstOrDefault(jg =>
                    jg.TimeCasaId == casaId && jg.TimeVisitanteId == visId &&
                    Math.Abs((jg.Data.Date - dataWeb.Value.Date).TotalDays) <= 1);
                if (j != null) return j;
            }
            return jogosBanco.FirstOrDefault(jg =>
                jg.TimeCasaId == casaId && jg.TimeVisitanteId == visId);
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