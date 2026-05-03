using HtmlAgilityPack;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Web;

namespace ControleFutebolWeb.Services
{
    public class TransfermarktPlayerInfo
    {
        public DateTime? DataNascimento { get; set; }
        public string? Nacionalidade { get; set; }
        public string? NomeCompleto { get; set; }
        public string? Clube { get; set; }
    }

    public class TransfermarktService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<TransfermarktService> _logger;

        // Mapeamento de nacionalidades (inglês/alemão → português)
        private static readonly Dictionary<string, string> _mapaFlags = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Brazil", "Brasil" }, { "Brasil", "Brasil" },
            { "Argentina", "Argentina" },
            { "Uruguay", "Uruguai" },
            { "Chile", "Chile" },
            { "Paraguay", "Paraguai" },
            { "Bolivia", "Bolívia" }, { "Bolivien", "Bolívia" },
            { "Peru", "Peru" },
            { "Ecuador", "Equador" },
            { "Colombia", "Colômbia" }, { "Kolumbien", "Colômbia" },
            { "Venezuela", "Venezuela" },
            { "Portugal", "Portugal" },
            { "Spain", "Espanha" }, { "Spanien", "Espanha" },
            { "France", "França" }, { "Frankreich", "França" },
            { "Germany", "Alemanha" }, { "Deutschland", "Alemanha" },
            { "Italy", "Itália" }, { "Italien", "Itália" },
            { "England", "Inglaterra" },
            { "Netherlands", "Holanda" }, { "Niederlande", "Holanda" },
            { "Belgium", "Bélgica" }, { "Belgien", "Bélgica" },
            { "Switzerland", "Suíça" }, { "Schweiz", "Suíça" },
            { "Croatia", "Croácia" }, { "Kroatien", "Croácia" },
            { "Mexico", "México" }, { "Mexiko", "México" },
            { "United States", "Estados Unidos" }, { "USA", "Estados Unidos" },
            { "Canada", "Canadá" }, { "Kanada", "Canadá" },
            { "Morocco", "Marrocos" }, { "Marokko", "Marrocos" },
            { "Senegal", "Senegal" },
            { "Ghana", "Gana" },
            { "Ivory Coast", "Costa do Marfim" },
            { "Nigeria", "Nigéria" },
            { "Cameroon", "Camarões" }, { "Kamerun", "Camarões" },
            { "Democratic Republic of Congo", "República Democrática do Congo" },
            { "Angola", "Angola" },
            { "Ukraine", "Ucrânia" },
            { "Serbia", "Sérvia" }, { "Serbien", "Sérvia" },
            { "Denmark", "Dinamarca" }, { "Dänemark", "Dinamarca" },
            { "Greece", "Grécia" }, { "Griechenland", "Grécia" },
            { "Panama", "Panamá" },
            { "Guinea", "Guiné" },
        };

        public TransfermarktService(HttpClient httpClient, ILogger<TransfermarktService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;

            // Headers obrigatórios para não receber 403 do Transfermarkt
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                "(KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept",
                "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "pt-BR,pt;q=0.9,en;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Referer", "https://www.transfermarkt.com.br/");
        }

        /// <summary>
        /// Busca dados do jogador no Transfermarkt pelo nome.
        /// Quando há múltiplos resultados, compara com o nome do clube para pegar o correto.
        /// </summary>
        public async Task<TransfermarktPlayerInfo?> BuscarJogador(string nomeJogador, string? nomeClube = null)
        {
            try
            {
                var query = HttpUtility.UrlEncode(nomeJogador);
                var url = $"https://www.transfermarkt.com.br/schnellsuche/ergebnis/schnellsuche?query={query}";

                _logger.LogInformation("[Transfermarkt] Buscando: {Nome} (clube: {Clube})", nomeJogador, nomeClube);

                var html = await _httpClient.GetStringAsync(url);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var tabelas = doc.DocumentNode.SelectNodes("//table[contains(@class,'items')]");
                if (tabelas == null || !tabelas.Any())
                {
                    _logger.LogWarning("[Transfermarkt] Nenhum resultado para: {Nome}", nomeJogador);
                    return null;
                }

                var linhas = tabelas[0].SelectNodes(".//tbody/tr[not(contains(@class,'thead'))]");
                if (linhas == null || !linhas.Any())
                    return null;

                // 🔹 Log de todos os candidatos (sem idade aqui, porque não é confiável na lista)
                foreach (var linha in linhas)
                {
                    var nomeCell = linha.SelectSingleNode(".//td[@class='hauptlink']/a")?.InnerText?.Trim();
                    var clubeCell = linha.SelectSingleNode(".//td[@class='zentriert']/a")?.InnerText?.Trim();
                    var nacCell = linha.SelectSingleNode(".//td[@class='zentriert']/img")?.GetAttributeValue("title", "");

                    _logger.LogInformation("[Transfermarkt] Candidato: Nome={Nome}, Clube={Clube}, Nac={Nac}",
                        nomeCell, clubeCell, nacCell);
                }

                HtmlNode? linhaSelecionada = null;

                // 🔹 1. Tenta pelo clube
                if (!string.IsNullOrWhiteSpace(nomeClube) && linhas.Count > 1)
                {
                    foreach (var linha in linhas)
                    {
                        var clubeCell = linha.SelectSingleNode(".//td[@class='zentriert']/a");
                        var clubeTexto = HtmlEntity.DeEntitize(clubeCell?.InnerText?.Trim() ?? "");

                        if (NomesParecidos(clubeTexto, nomeClube))
                        {
                            linhaSelecionada = linha;
                            _logger.LogInformation("[Transfermarkt] Selecionado pelo clube: {Clube}", clubeTexto);
                            break;
                        }
                    }
                }

                // 🔹 2. Se não achou pelo clube, tenta pela nacionalidade
                if (linhaSelecionada == null && linhas.Count > 1)
                {
                    foreach (var linha in linhas)
                    {
                        var nacCell = linha.SelectSingleNode(".//td[@class='zentriert']/img");
                        var nacTexto = nacCell?.GetAttributeValue("title", "") ?? "";

                        if (!string.IsNullOrWhiteSpace(nacTexto) &&
                            nacTexto.Equals("Brasil", StringComparison.OrdinalIgnoreCase))
                        {
                            linhaSelecionada = linha;
                            _logger.LogInformation("[Transfermarkt] Selecionado pela nacionalidade: {Nac}", nacTexto);
                            break;
                        }
                    }
                }

                // 🔹 3. Fallback final
                linhaSelecionada ??= linhas[0];
                _logger.LogInformation("[Transfermarkt] Selecionado fallback: {Nome}",
                    linhaSelecionada.SelectSingleNode(".//td[@class='hauptlink']/a")?.InnerText?.Trim());

                // 🔹 Extrai dados completos do perfil (onde idade e nascimento são confiáveis)
                return await ExtrairDadosDaLinha(linhaSelecionada);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Transfermarkt] Erro inesperado ao buscar {Nome}", nomeJogador);
                return null;
            }
        }

        private async Task<TransfermarktPlayerInfo?> ExtrairDadosDaLinha(HtmlNode linha)
        {
            try
            {
                // Link do perfil
                var linkNode = linha.SelectSingleNode(".//td[@class='hauptlink']/a")
                             ?? linha.SelectSingleNode(".//a[contains(@href,'/profil/spieler/')]");

                if (linkNode == null) return null;

                var href = linkNode.GetAttributeValue("href", "");
                if (string.IsNullOrWhiteSpace(href)) return null;

                var profileUrl = href.StartsWith("http")
                    ? href
                    : "https://www.transfermarkt.com.br" + href;

                _logger.LogInformation("[Transfermarkt] Acessando perfil: {Url}", profileUrl);

                await Task.Delay(TimeSpan.FromSeconds(1.5)); // respeita rate limit

                var profileHtml = await _httpClient.GetStringAsync(profileUrl);
                var profileDoc = new HtmlDocument();
                profileDoc.LoadHtml(profileHtml);

                var info = new TransfermarktPlayerInfo();

                // Nome completo
                info.NomeCompleto = profileDoc.DocumentNode
                    .SelectSingleNode("//h1[contains(@class,'data-header__headline')]")?.InnerText?.Trim()
                    ?? profileDoc.DocumentNode.SelectSingleNode("//h1")?.InnerText?.Trim();

                // 🔹 Primeiro tenta pegar pelo itemprop birthDate
                var birthNode = profileDoc.DocumentNode.SelectSingleNode("//span[@itemprop='birthDate']");
                if (birthNode != null)
                {
                    var texto = birthNode.InnerText.Trim();
                    var partes = texto.Split('(')[0].Trim(); // remove idade entre parênteses
                    var dt = ParseDataNascimento(partes);
                    if (dt.HasValue)
                    {
                        info.DataNascimento = dt;
                    }
                }

                // Extrai dados da tabela (fallback)
                var infoNodes = profileDoc.DocumentNode
                    .SelectNodes("//span[@class='info-table__content info-table__content--bold']");
                var labelNodes = profileDoc.DocumentNode
                    .SelectNodes("//span[@class='info-table__content info-table__content--regular']");

                if (infoNodes != null && labelNodes != null)
                {
                    for (int i = 0; i < Math.Min(infoNodes.Count, labelNodes.Count); i++)
                    {
                        var label = labelNodes[i].InnerText.Trim().ToLower();
                        var valor = HtmlEntity.DeEntitize(infoNodes[i].InnerText.Trim());

                        if ((label.Contains("nascimento") || label.Contains("geboren") || label.Contains("date of birth"))
                            && info.DataNascimento == null) // só se não achou antes
                        {
                            info.DataNascimento = ParseDataNascimento(valor);
                            if (info.DataNascimento.HasValue)
                            {
                                if (info.DataNascimento.Value.Year < 1900 ||
                                    info.DataNascimento.Value > DateTime.Today)
                                {
                                    _logger.LogWarning("[Transfermarkt] Data inválida detectada: {Data}", info.DataNascimento);
                                    info.DataNascimento = null;
                                }
                            }
                        }
                        else if (label.Contains("nacionalidade") || label.Contains("nationalität") || label.Contains("nation"))
                        {
                            var imgAlt = infoNodes[i].SelectSingleNode(".//img")
                                ?.GetAttributeValue("title", "")
                                ?? infoNodes[i].SelectSingleNode(".//img")
                                    ?.GetAttributeValue("alt", "");

                            var nomeNacRaw = !string.IsNullOrWhiteSpace(imgAlt) ? imgAlt : valor;
                            info.Nacionalidade = NormalizarNacionalidade(nomeNacRaw);
                        }
                        else if (label.Contains("clube") || label.Contains("verein") || label.Contains("club"))
                        {
                            info.Clube = valor;
                        }
                    }
                }

                // Regex fallback (última tentativa)
                if (info.DataNascimento == null)
                {
                    var matchData = Regex.Match(profileHtml, @"(\d{2}/\d{2}/\d{4})");
                    if (matchData.Success)
                        info.DataNascimento = ParseDataNascimento(matchData.Groups[1].Value);
                }

                // Validação final
                if (info.DataNascimento.HasValue && info.DataNascimento.Value.Year < 1900)
                    info.DataNascimento = null;

                _logger.LogInformation(
                    "[Transfermarkt] Perfil escolhido: Nome={Nome}, Clube={Clube}, Nasc={Nasc}, Nac={Nac}",
                    info.NomeCompleto,
                    info.Clube ?? "não informado",
                    info.DataNascimento?.ToString("dd/MM/yyyy") ?? "null",
                    info.Nacionalidade ?? "não informada");

                return info;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Transfermarkt] Erro ao extrair dados do perfil");
                return null;
            }
        }





        private DateTime? ParseDataNascimento(string valor)
        {
            if (string.IsNullOrWhiteSpace(valor)) return null;

            // 1. Formato dd/MM/yyyy
            var match = Regex.Match(valor, @"(\d{2}/\d{2}/\d{4})");
            if (match.Success &&
                DateTime.TryParseExact(match.Groups[1].Value, "dd/MM/yyyy",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                return dt;
            }

            // 2. Formato "Nov 18, 1999"
            var matchEn = Regex.Match(valor, @"(\w+ \d{1,2}, \d{4})");
            if (matchEn.Success &&
                DateTime.TryParse(matchEn.Groups[1].Value, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var dtEn))
            {
                return dtEn;
            }

            // 3. Fallback genérico
            if (DateTime.TryParse(valor, out var dtAny))
                return dtAny;

            return null;
        }

        private string NormalizarNacionalidade(string raw)
        {
            var limpo = raw.Trim();
            return _mapaFlags.TryGetValue(limpo, out var traduzido)
                ? traduzido
                : limpo; // mantém original se não tiver mapeamento
        }

        /// <summary>
        /// Verifica se dois nomes de clube são parecidos (ignora maiúsculas, acentos, "FC", "CR" etc.)
        /// </summary>
        private static bool NomesParecidos(string a, string b)
        {
            static string Normalizar(string s) =>
                Regex.Replace(
                    s.ToLowerInvariant()
                     .Replace("á", "a").Replace("é", "e").Replace("í", "i")
                     .Replace("ó", "o").Replace("ú", "u").Replace("ã", "a")
                     .Replace("ê", "e").Replace("â", "a").Replace("ô", "o")
                     .Replace("ç", "c").Replace("ñ", "n"),
                    @"\b(cr|fc|sc|ec|ac|se|esporte|clube|club|futebol|de|do|da|dos|las|los)\b|\s+",
                    "");

            var na = Normalizar(a);
            var nb = Normalizar(b);

            return na == nb
                || na.Contains(nb)
                || nb.Contains(na);
        }
    }
}
