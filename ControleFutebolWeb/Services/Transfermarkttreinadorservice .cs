using HtmlAgilityPack;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Web;
using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Services
{
    // ─── DTOs ────────────────────────────────────────────────────────────────

    public class TreinadorTMInfo
    {
        public string? NomeCompleto { get; set; }
        public string? FotoUrl { get; set; }
        public DateTime? DataNascimento { get; set; }
        public string? Nacionalidade { get; set; }
        public string? ProfileUrl { get; set; }
        public List<HistoricoTMItem> Historico { get; set; } = new();
    }

    public class HistoricoTMItem
    {
        public string NomeTime { get; set; } = "";
        public string? LogoUrl { get; set; }
        public string? Funcao { get; set; }       // ex: "Treinador", "Treinador interino"
        public DateTime? DtInicio { get; set; }
        public DateTime? DtFim { get; set; }      // null = cargo atual
        public int? Jogos { get; set; }
        public double? PpjMedia { get; set; }     // PPJ (pontos por jogo)
    }

    public class ImportacaoHistoricoResultado
    {
        public int RegistrosSalvos { get; set; }
        public int TimesNaoEncontrados { get; set; }
        public int TimesCreados { get; set; }
        public List<string> Avisos { get; set; } = new();
    }

    // ─── Serviço ─────────────────────────────────────────────────────────────

    public class TransfermarktTreinadorService
    {
        private readonly HttpClient _http;
        private readonly ILogger<TransfermarktTreinadorService> _log;

        // Mapeamento de meses em português/espanhol/alemão → número
        private static readonly Dictionary<string, int> _meses =
            new(StringComparer.OrdinalIgnoreCase)
        {
            { "jan", 1 }, { "fev", 2 }, { "mar", 3 }, { "abr", 4 },
            { "mai", 5 }, { "jun", 6 }, { "jul", 7 }, { "ago", 8 },
            { "set", 9 }, { "out", 10 }, { "nov", 11 }, { "dez", 12 },
            { "ene", 1 }, { "feb", 2 }, { "may", 5 }, { "sep", 9 }, { "oct", 10 }, { "dic", 12 },
            { "jan.", 1 }, { "feb.", 2 }, { "mar.", 3 }, { "apr.", 4 },
            { "may.", 5 }, { "jun.", 6 }, { "jul.", 7 }, { "aug.", 8 },
            { "sep.", 9 }, { "oct.", 10 }, { "nov.", 11 }, { "dec.", 12 },
        };

        public TransfermarktTreinadorService(
            HttpClient httpClient,
            ILogger<TransfermarktTreinadorService> logger)
        {
            _http = httpClient;
            _log = logger;

            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                "(KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
            _http.DefaultRequestHeaders.Add("Accept",
                "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            _http.DefaultRequestHeaders.Add("Accept-Language", "pt-BR,pt;q=0.9,en;q=0.8");
            _http.DefaultRequestHeaders.Add("Referer", "https://www.transfermarkt.com.br/");
        }

        // ─── Busca perfil de treinador pelo nome (pesquisa rápida) ─────────────

        /// <summary>
        /// Busca o perfil de treinador no Transfermarkt.
        /// Se nomeClube for informado, tenta selecionar o resultado correto.
        /// </summary>
        public async Task<TreinadorTMInfo?> BuscarTreinadorAsync(
            string nome, string? nomeClube = null, CancellationToken ct = default)
        {
            try
            {
                var query = HttpUtility.UrlEncode(nome.Trim());
                var searchUrl =
                    $"https://www.transfermarkt.com.br/schnellsuche/ergebnis/schnellsuche?query={query}";

                _log.LogInformation("[TreinadorTM] Buscando: '{Nome}' | Clube: '{Clube}'",
                    nome, nomeClube);

                var html = await _http.GetStringAsync(searchUrl, ct);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // O Transfermarkt retorna a seção de treinadores em tabela com id "yw3" ou class "items"
                // Tenta primeiro pelo cabeçalho "Treinadores"
                var profileUrl = LocalizarUrlPerfilTreinador(doc, nome, nomeClube);

                if (string.IsNullOrWhiteSpace(profileUrl))
                {
                    _log.LogWarning("[TreinadorTM] Nenhum perfil encontrado para '{Nome}'", nome);
                    return null;
                }

                await Task.Delay(1500, ct);
                return await BuscarPerfilAsync(profileUrl, ct);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "[TreinadorTM] Erro ao buscar treinador '{Nome}'", nome);
                return null;
            }
        }

        // ─── Busca perfil direto pela URL ──────────────────────────────────────

        /// <summary>
        /// Acessa diretamente a URL de perfil do treinador e extrai dados + histórico.
        /// Ex.: https://www.transfermarkt.com.br/fernando-diniz/profil/trainer/41120
        /// </summary>
        public async Task<TreinadorTMInfo?> BuscarPerfilAsync(
            string profileUrl, CancellationToken ct = default)
        {
            try
            {
                _log.LogInformation("[TreinadorTM] Acessando perfil: {Url}", profileUrl);
                await Task.Delay(1200, ct);

                var html = await _http.GetStringAsync(profileUrl, ct);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var info = new TreinadorTMInfo { ProfileUrl = profileUrl };

                // ── Nome ──────────────────────────────────────────────────────
                info.NomeCompleto = doc.DocumentNode
                    .SelectSingleNode("//h1[contains(@class,'data-header__headline')]")
                    ?.InnerText?.Trim()
                    ?? doc.DocumentNode.SelectSingleNode("//h1")?.InnerText?.Trim();

                // ── Foto ──────────────────────────────────────────────────────
                var ogImage = doc.DocumentNode
                    .SelectSingleNode("//meta[@property='og:image']");
                info.FotoUrl = ogImage?.GetAttributeValue("content", "")?.Trim();

                // ── Dados pessoais (data de nascimento, nacionalidade) ─────────
                ExtrairDadosPessoais(doc, info);

                // ── Histórico de trabalhos ────────────────────────────────────
                info.Historico = ExtrairHistorico(doc);

                _log.LogInformation(
                    "[TreinadorTM] Perfil carregado: {Nome} | Histórico: {N} itens",
                    info.NomeCompleto, info.Historico.Count);

                return info;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "[TreinadorTM] Erro ao buscar perfil: {Url}", profileUrl);
                return null;
            }
        }

        // ─── Salva histórico no banco ──────────────────────────────────────────

        public async Task<ImportacaoHistoricoResultado> SalvarHistoricoAsync(
            FutebolContext context,
            int treinadorId,
            List<HistoricoTMItem> historico,
            CancellationToken ct = default)
        {
            var resultado = new ImportacaoHistoricoResultado();

            // Remove histórico anterior para reimportar
            var antigosRegistros = await context.TreinadoresHistorico
                .Where(h => h.TreinadorId == treinadorId)
                .ToListAsync(ct);

            if (antigosRegistros.Any())
                context.TreinadoresHistorico.RemoveRange(antigosRegistros);

            foreach (var item in historico)
            {
                if (string.IsNullOrWhiteSpace(item.NomeTime)) continue;

                // Tenta localizar o time no banco
                var time = await ResolverTimeAsync(context, item.NomeTime, ct);

                if (time == null)
                {
                    // Cria time genérico apenas com o nome
                    time = new Time
                    {
                        Nome = item.NomeTime.Trim(),
                        Cidade = "Desconhecida",
                        EscudoUrl = item.LogoUrl ?? "",
                        CorPrincipal = "#000000",
                        CorSecundaria = "#FFFFFF",
                        FormacaoPadraoId = await ObterFormacaoPadraoIdAsync(context, ct)
                    };
                    context.Times.Add(time);
                    await context.SaveChangesAsync(ct);

                    resultado.TimesCreados++;
                    resultado.Avisos.Add(
                        $"Time '{item.NomeTime}' não encontrado — criado como genérico (Id={time.Id}).");
                }

               
                var historicoBanco = new TreinadorHistorico
                {
                    TreinadorId = treinadorId,
                    TimeId = time.Id,
                    DtInicio = item.DtInicio.HasValue
                    ? DateTime.SpecifyKind(item.DtInicio.Value, DateTimeKind.Utc)
                    : DateTime.SpecifyKind(new DateTime(2000, 1, 1), DateTimeKind.Utc),
                                DtFim = item.DtFim.HasValue
                    ? DateTime.SpecifyKind(item.DtFim.Value, DateTimeKind.Utc)
                    : null
                            };

                context.TreinadoresHistorico.Add(historicoBanco);
                resultado.RegistrosSalvos++;
            }

            await context.SaveChangesAsync(ct);
            return resultado;
        }

        // ─── Helpers privados ─────────────────────────────────────────────────

        private string? LocalizarUrlPerfilTreinador(
            HtmlDocument doc, string nome, string? nomeClube)
        {
            // O Transfermarkt organiza os resultados por categorias (Jogadores, Treinadores, Clubes…)
            // Treinadores ficam num bloco com título "Treinadores" ou id "yw3"
            // Estratégia: pegar todos os links para /profil/trainer/ e escolher o mais relevante

            var links = doc.DocumentNode
                .SelectNodes("//a[contains(@href,'/profil/trainer/')]");

            if (links == null || !links.Any())
            {
                _log.LogWarning("[TreinadorTM] Nenhum link /profil/trainer/ encontrado.");
                return null;
            }

            var nomeNorm = NormalizarTexto(nome);
            var clubeNorm = string.IsNullOrWhiteSpace(nomeClube)
                ? null : NormalizarTexto(nomeClube);

            string? melhorUrl = null;
            int melhorScore = -1;

            foreach (var link in links)
            {
                var href = link.GetAttributeValue("href", "");
                if (!href.Contains("/profil/trainer/")) continue;

                var nomeLink = NormalizarTexto(
                    link.GetAttributeValue("title", "") +
                    " " + link.InnerText.Trim());

                bool nomeOk = nomeLink.Contains(nomeNorm)
                           || nomeNorm.Contains(nomeLink.Trim())
                           || PrimeiroTokenIgual(nomeLink, nomeNorm);

                if (!nomeOk) continue;

                int score = 1;

                // Verifica clube na linha pai
                if (clubeNorm != null)
                {
                    var linha = link.ParentNode?.ParentNode?.ParentNode;
                    var textoLinha = NormalizarTexto(linha?.InnerText ?? "");
                    if (textoLinha.Contains(clubeNorm))
                        score += 10;
                }

                if (score > melhorScore)
                {
                    melhorScore = score;
                    melhorUrl = href.StartsWith("http")
                        ? href
                        : "https://www.transfermarkt.com.br" + href;
                }
            }

            return melhorUrl;
        }

        private void ExtrairDadosPessoais(HtmlDocument doc, TreinadorTMInfo info)
        {
            var infoNodes = doc.DocumentNode
                .SelectNodes("//span[@class='info-table__content info-table__content--bold']");
            var labelNodes = doc.DocumentNode
                .SelectNodes("//span[@class='info-table__content info-table__content--regular']");

            if (infoNodes == null || labelNodes == null) return;

            for (int i = 0; i < Math.Min(infoNodes.Count, labelNodes.Count); i++)
            {
                var label = labelNodes[i].InnerText.Trim().ToLower();
                var valor = HtmlAgilityPack.HtmlEntity.DeEntitize(
                    infoNodes[i].InnerText.Trim());

                if (label.Contains("nascimento") && info.DataNascimento == null)
                    info.DataNascimento = ParseData(valor);

                else if (label.Contains("nacionalidade") || label.Contains("nation"))
                {
                    var img = infoNodes[i].SelectSingleNode(".//img");
                    info.Nacionalidade = img?.GetAttributeValue("title", "") ?? valor;
                }
            }
        }

        private List<HistoricoTMItem> ExtrairHistorico(HtmlDocument doc)
        {
            var lista = new List<HistoricoTMItem>();

            // Localiza a seção de Trabalhos desenvolvidos pelo id "trainer-stationen"
            var boxTrabalhos = doc.DocumentNode
                .SelectSingleNode("//h2[@id='trainer-stationen']/ancestor::div[contains(@class,'box')]");

            if (boxTrabalhos != null)
            {
                var tabela = boxTrabalhos.SelectSingleNode(".//table[contains(@class,'items')]");
                if (tabela != null)
                {
                    var linhas = tabela.SelectNodes(".//tbody/tr[not(contains(@class,'thead'))]");
                    if (linhas != null)
                    {
                        foreach (var linha in linhas)
                        {
                            var item = ParseLinhaHistorico(linha);
                            if (item != null) lista.Add(item);
                        }
                    }
                }
            }

            // Fallback: se não encontrar nada, tenta qualquer linha com link de clube
            if (!lista.Any())
            {
                var todasLinhas = doc.DocumentNode
                    .SelectNodes("//tr[.//a[contains(@href,'/verein/')]]");

                if (todasLinhas != null)
                {
                    foreach (var linha in todasLinhas)
                    {
                        var item = ParseLinhaHistorico(linha);
                        if (item != null) lista.Add(item);
                    }
                }
            }

            _log.LogInformation("[TreinadorTM] Histórico extraído: {N} itens", lista.Count);
            return lista;
        }


        private HistoricoTMItem? ParseLinhaHistorico(HtmlNode linha)
        {
            try
            {
                // Nome do time: link para /verein/
                var linkTime = linha.SelectSingleNode(
                    ".//a[contains(@href,'/verein/') and not(contains(@href,'/spielplan/'))]")
                ?? linha.SelectSingleNode(".//a[@title and string-length(@title) > 1]");

                if (linkTime == null) return null;

                var nomeTime = HtmlAgilityPack.HtmlEntity.DeEntitize(
                    linkTime.GetAttributeValue("title", "").Trim());
                if (string.IsNullOrWhiteSpace(nomeTime))
                    nomeTime = HtmlAgilityPack.HtmlEntity.DeEntitize(
                        linkTime.InnerText.Trim());

                if (string.IsNullOrWhiteSpace(nomeTime)) return null;

                // Logo
                var imgLogo = linkTime.SelectSingleNode(".//img")
                    ?? linha.SelectSingleNode(".//img[contains(@src,'/vereinslogo/')]")
                    ?? linha.SelectSingleNode(".//img[contains(@class,'tiny')]");
                var logoUrl = imgLogo?.GetAttributeValue("src", "")?.Trim();

                // Função (ex: "Treinador", "Treinador interino")
                var funcaoNode = linha.SelectSingleNode(
                    ".//td[contains(@class,'zentriert') or @class='']//a[@class='grey']")
                ?? linha.SelectSingleNode(".//a[contains(@class,'grey')]");
                var funcao = funcaoNode != null
                    ? HtmlAgilityPack.HtmlEntity.DeEntitize(funcaoNode.InnerText.Trim())
                    : "Treinador";

                // Datas: pega todas as células de texto e procura padrões de data
                var celulas = linha.SelectNodes(".//td");
                var textos = celulas?
                    .Select(c => HtmlAgilityPack.HtmlEntity.DeEntitize(c.InnerText.Trim()))
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .ToList() ?? new List<string>();

                DateTime? dtInicio = null, dtFim = null;
                int? jogos = null;
                double? ppj = null;

                foreach (var txt in textos)
                {
                    // Padrão: "24/25 (09/05/2025)" ou "24/25(09/05/2025)"
                    var mData = Regex.Match(txt,
                        @"\d{2}/\d{2}/\d{4}");
                    if (mData.Success)
                    {
                        var dt = ParseData(mData.Value);
                        if (dtInicio == null) dtInicio = dt;
                        else if (dtFim == null && dt != dtInicio) dtFim = dt;
                        continue;
                    }

                    // Número de jogos (apenas número inteiro isolado)
                    if (int.TryParse(txt, out int j) && j >= 0 && j <= 999 && jogos == null)
                    {
                        jogos = j;
                        continue;
                    }

                    // PPJ (double)
                    if (txt.Contains('.') &&
                        double.TryParse(txt, NumberStyles.Any,
                            CultureInfo.InvariantCulture, out double p) &&
                        p >= 0 && p <= 3 && ppj == null)
                    {
                        ppj = p;
                    }
                }

                // Verifica se é cargo atual (sem data de saída)
                bool cargAtual = textos.Any(t =>
                    t == "-" ||
                    t.Equals("presente", StringComparison.OrdinalIgnoreCase) ||
                    t.Equals("atual", StringComparison.OrdinalIgnoreCase));

                return new HistoricoTMItem
                {
                    NomeTime = nomeTime,
                    LogoUrl = logoUrl,
                    Funcao = funcao,
                    DtInicio = dtInicio,
                    DtFim = cargAtual ? null : dtFim,
                    Jogos = jogos,
                    PpjMedia = ppj
                };
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "[TreinadorTM] Erro ao parsear linha de histórico.");
                return null;
            }
        }

        private async Task<Time?> ResolverTimeAsync(
            FutebolContext context, string nomeTime, CancellationToken ct)
        {
            var nomeNorm = NormalizarTexto(nomeTime);

            // 1. Exato
            var t = await context.Times.FirstOrDefaultAsync(
                x => x.Nome == nomeTime.Trim(), ct);
            if (t != null) return t;

            // 2. ILike
            t = await context.Times.FirstOrDefaultAsync(
                x => EF.Functions.ILike(x.Nome, nomeTime.Trim()), ct);
            if (t != null) return t;

            // 3. Normalizado em memória
            var todos = await context.Times.ToListAsync(ct);
            return todos.FirstOrDefault(x =>
                NormalizarTexto(x.Nome) == nomeNorm ||
                NormalizarTexto(x.Nome).Contains(nomeNorm) ||
                nomeNorm.Contains(NormalizarTexto(x.Nome)));
        }

        private async Task<int> ObterFormacaoPadraoIdAsync(
            FutebolContext context, CancellationToken ct)
        {
            var f = await context.Formacoes.FirstOrDefaultAsync(ct);
            return f?.Id ?? 1;
        }

        private static DateTime? ParseData(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return null;

            if (DateTime.TryParseExact(texto.Trim(), "dd/MM/yyyy",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt1))
                return DateTime.SpecifyKind(dt1, DateTimeKind.Utc);

            if (DateTime.TryParseExact(texto.Trim(), "dd.MM.yyyy",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt2))
                return DateTime.SpecifyKind(dt2, DateTimeKind.Utc);

            if (DateTime.TryParse(texto.Trim(), out var dt3))
                return DateTime.SpecifyKind(dt3, DateTimeKind.Utc);

            return null;
        }


        private static string NormalizarTexto(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            return Regex.Replace(
                s.ToLowerInvariant()
                 .Replace("á", "a").Replace("é", "e").Replace("í", "i")
                 .Replace("ó", "o").Replace("ú", "u").Replace("ã", "a")
                 .Replace("ê", "e").Replace("â", "a").Replace("ô", "o")
                 .Replace("ç", "c").Replace("ñ", "n"),
                @"\b(cr|fc|sc|ec|ac|se|club|futebol|football|de|do|da|dos)\b|\s+", "");
        }

        private static bool PrimeiroTokenIgual(string a, string b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
            return a.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
                == b.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        }
    }
}