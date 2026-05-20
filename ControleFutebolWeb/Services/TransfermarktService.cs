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
        private readonly FutebolContext _context;

        public TransfermarktService(HttpClient httpClient, ILogger<TransfermarktService> logger, FutebolContext context)
        {
            _httpClient = httpClient;
            _logger = logger;
            _context = context;

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
        /// Busca foto do jogador. Se tiver linkTransfermarkt salvo, usa direto.
        /// Caso contrário, faz busca genérica pelo nome/clube.
        /// </summary>
        public async Task<string?> BuscarFotoJogador(Jogador jogador)
        {
            try
            {
                if (!string.IsNullOrEmpty(jogador.linktransfermarket))
                {
                    return await BuscarFotoPorLink(jogador.linktransfermarket);
                }

                return await BuscarFotoPorNome(jogador.Nome, jogador.Time?.Nome);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Foto] Erro ao buscar foto de {Nome}", jogador.Nome);
                return null;
            }
        }

        private async Task<string?> BuscarFotoPorLink(string profileUrl)
        {
            if (string.IsNullOrWhiteSpace(profileUrl)) return null;

            if (!profileUrl.StartsWith("http"))
                profileUrl = "https://www.transfermarkt.com.br" + profileUrl;

            _logger.LogInformation("[Foto] Acessando perfil direto: {Url}", profileUrl);

            var profileHtml = await _httpClient.GetStringAsync(profileUrl);
            var profileDoc = new HtmlDocument();
            profileDoc.LoadHtml(profileHtml);

            var ogImage = profileDoc.DocumentNode.SelectSingleNode("//meta[@property='og:image']");
            var fotoUrl = ogImage?.GetAttributeValue("content", "")?.Trim();

            if (!string.IsNullOrEmpty(fotoUrl))
            {
                _logger.LogInformation("[Foto] Foto encontrada via link: {Url}", fotoUrl);
                return fotoUrl;
            }

            var imgPerfil = profileDoc.DocumentNode
                .SelectSingleNode("//img[contains(@class,'data-header__profile-image')]");
            return imgPerfil?.GetAttributeValue("src", "")?.Trim();
        }

        private async Task<string?> BuscarFotoPorNome(string nomeJogador, string? nomeClube = null)
        {
            var query = HttpUtility.UrlEncode(nomeJogador);
            var url = $"https://www.transfermarkt.com.br/schnellsuche/ergebnis/schnellsuche?query={query}";

            _logger.LogInformation("[Foto] Buscando: {Nome} | Clube: {Clube}", nomeJogador, nomeClube);

            var html = await _httpClient.GetStringAsync(url);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var tabela = doc.DocumentNode
                .SelectNodes("//table[contains(@class,'items')]")
                ?.FirstOrDefault();

            if (tabela == null)
            {
                _logger.LogWarning("[Foto] Nenhuma tabela de resultados: {Nome}", nomeJogador);
                return null;
            }

            var linhas = tabela.SelectNodes(".//tbody/tr[not(contains(@class,'thead'))]");
            if (linhas == null || !linhas.Any()) return null;

            foreach (var l in linhas)
            {
                var nomeCell = ExtrairNomeLinha(l);
                var clubeCell = ExtrairClubeLinha(l);
                _logger.LogInformation("[Foto] Candidato: Nome={Nome} | Clube={Clube}", nomeCell, clubeCell);
            }

            HtmlNode? linhaSelecionada = null;

            if (!string.IsNullOrWhiteSpace(nomeClube))
            {
                linhaSelecionada = linhas.FirstOrDefault(l =>
                    NomesClubeSimilares(ExtrairClubeLinha(l), nomeClube));

                if (linhaSelecionada != null)
                    _logger.LogInformation("[Foto] Clube encontrado (match): {Clube}",
                        ExtrairClubeLinha(linhaSelecionada));
            }

            linhaSelecionada ??= linhas.First();
            _logger.LogInformation("[Foto] Linha selecionada: {Nome} / {Clube}",
                ExtrairNomeLinha(linhaSelecionada), ExtrairClubeLinha(linhaSelecionada));

            return await ExtrairFotoDaLinha(linhaSelecionada);
        }

        public async Task<TransfermarktPlayerInfo?> BuscarJogadorPorLink(string profileUrl)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(profileUrl)) return null;

                // Garante que o link é completo
                if (!profileUrl.StartsWith("http"))
                    profileUrl = "https://www.transfermarkt.com.br" + profileUrl;

                _logger.LogInformation("[Transfermarkt] Acessando perfil direto: {Url}", profileUrl);

                var profileHtml = await _httpClient.GetStringAsync(profileUrl);
                var profileDoc = new HtmlDocument();
                profileDoc.LoadHtml(profileHtml);

                var info = new TransfermarktPlayerInfo();

                // Nome completo
                info.NomeCompleto = profileDoc.DocumentNode
                    .SelectSingleNode("//h1[contains(@class,'data-header__headline')]")?.InnerText?.Trim()
                    ?? profileDoc.DocumentNode.SelectSingleNode("//h1")?.InnerText?.Trim();

                // Data de nascimento
                var birthNode = profileDoc.DocumentNode.SelectSingleNode("//span[@itemprop='birthDate']");
                if (birthNode != null)
                {
                    var texto = birthNode.InnerText.Trim();
                    var partes = texto.Split('(')[0].Trim();
                    info.DataNascimento = ParseDataNascimento(partes);
                }

                // Nacionalidade
                var nacNode = profileDoc.DocumentNode.SelectSingleNode("//span[@class='info-table__content info-table__content--bold']//img");
                if (nacNode != null)
                {
                    var nacRaw = nacNode.GetAttributeValue("title", "");
                    info.Nacionalidade = NacionalidadesHelper.Normalizar(nacRaw);
                }

                // Clube atual
                var clubeNode = profileDoc.DocumentNode.SelectSingleNode("//span[contains(@class,'info-table__content')]");
                if (clubeNode != null)
                    info.Clube = HtmlEntity.DeEntitize(clubeNode.InnerText.Trim());

                return info;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Transfermarkt] Erro ao acessar perfil direto");
                return null;
            }
        }
        private static string ExtrairNomeLinha(HtmlNode linha)
        {
            // Estrutura: <td class="hauptlink"><a>Nome</a></td>
            var link = linha.SelectSingleNode(".//td[contains(@class,'hauptlink')]//a");
            return HtmlEntity.DeEntitize(link?.InnerText?.Trim() ?? "");
        }

        private static string ExtrairClubeLinha(HtmlNode linha)
        {
            // Estratégia 1: link para /verein/ (o mais confiável)
            var linkVerein = linha.SelectNodes(".//a[contains(@href,'/verein/')]")
                ?.FirstOrDefault();
            if (linkVerein != null)
            {
                // Prefere o atributo title (nome completo do clube)
                var title = linkVerein.GetAttributeValue("title", "").Trim();
                if (!string.IsNullOrWhiteSpace(title))
                    return HtmlEntity.DeEntitize(title);

                var texto = linkVerein.InnerText.Trim();
                if (!string.IsNullOrWhiteSpace(texto))
                    return HtmlEntity.DeEntitize(texto);
            }

            // Estratégia 2: segunda célula da inline-table (abaixo do nome)
            var subTds = linha.SelectNodes(".//td[@class='inline-table']//tr");
            if (subTds?.Count >= 2)
            {
                var clubeTexto = subTds[1].InnerText.Trim();
                if (!string.IsNullOrWhiteSpace(clubeTexto))
                    return HtmlEntity.DeEntitize(clubeTexto);
            }

            return "";
        }

        private static bool NomesClubeSimilares(string nomesite, string nomeBanco)
        {
            if (string.IsNullOrWhiteSpace(nomesite) || string.IsNullOrWhiteSpace(nomeBanco))
                return false;

            var a = NormalizarClube(nomesite);
            var b = NormalizarClube(nomeBanco);

            // Match exato após normalização
            if (a == b) return true;

            // Um contém o outro (cobre "Internacional" ↔ "SC Internacional Porto Alegre")
            if (a.Contains(b) || b.Contains(a)) return true;

            // Match parcial: cada token de b aparece em a
            var tokensB = b.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokensB.Length > 0 && tokensB.All(t => a.Contains(t))) return true;

            return false;
        }


        private static string NormalizarClube(string nome)
        {
            if (string.IsNullOrWhiteSpace(nome)) return "";

            var s = nome.ToLowerInvariant()
                .Replace("á", "a").Replace("é", "e").Replace("í", "i")
                .Replace("ó", "o").Replace("ú", "u").Replace("ã", "a")
                .Replace("ê", "e").Replace("â", "a").Replace("ô", "o")
                .Replace("ç", "c").Replace("ñ", "n");

            // Remove prefixos/sufixos comuns
            var stopwords = new[] { "sc", "cr", "ec", "fc", "cd", "ca", "ac", "se",
                             "sport", "clube", "club", "futebol", "football",
                             "de", "do", "da", "dos", "las", "los",
                             "porto", "alegre" }; // cidade removida para não interferir

            var tokens = s.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                          .Where(t => !stopwords.Contains(t) && t.Length > 1)
                          .ToArray();

            return string.Join(" ", tokens);
        }

        /// <summary>Acessa o perfil e extrai a URL da foto via og:image.</summary>
        private async Task<string?> ExtrairFotoDaLinha(HtmlNode linha)
        {
            try
            {
                var linkNode = linha.SelectSingleNode(".//td[contains(@class,'hauptlink')]//a")
                             ?? linha.SelectSingleNode(".//a[contains(@href,'/profil/spieler/')]");
                if (linkNode == null) return null;

                var href = linkNode.GetAttributeValue("href", "");
                if (string.IsNullOrWhiteSpace(href)) return null;

                var profileUrl = href.StartsWith("http")
                    ? href
                    : "https://www.transfermarkt.com.br" + href;

                _logger.LogInformation("[Foto] Acessando perfil: {Url}", profileUrl);
                await Task.Delay(TimeSpan.FromSeconds(1.5));

                var profileHtml = await _httpClient.GetStringAsync(profileUrl);
                var profileDoc = new HtmlDocument();
                profileDoc.LoadHtml(profileHtml);

                // Extrai og:image (mais confiável)
                var ogImage = profileDoc.DocumentNode
                    .SelectSingleNode("//meta[@property='og:image']");
                var fotoUrl = ogImage?.GetAttributeValue("content", "")?.Trim();

                if (!string.IsNullOrWhiteSpace(fotoUrl) && fotoUrl.Contains("transfermarkt"))
                {
                    _logger.LogInformation("[Foto] og:image encontrado: {Url}", fotoUrl);
                    return fotoUrl;
                }

                // Fallback: img de perfil
                var imgPerfil = profileDoc.DocumentNode
                    .SelectSingleNode("//img[contains(@class,'data-header__profile-image')]");
                fotoUrl = imgPerfil?.GetAttributeValue("src", "")?.Trim();

                if (!string.IsNullOrWhiteSpace(fotoUrl))
                {
                    _logger.LogInformation("[Foto] img perfil encontrado: {Url}", fotoUrl);
                    return fotoUrl;
                }

                _logger.LogWarning("[Foto] Nenhuma foto encontrada na página de perfil.");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Foto] Erro ao acessar perfil");
                return null;
            }
        }

        /// <summary>
        /// Busca dados do jogador no Transfermarkt pelo nome.
        /// Quando há múltiplos resultados, compara com o nome do clube para pegar o correto.
        /// </summary>
        public async Task<TransfermarktPlayerInfo?> BuscarJogador(
            string nomeJogador,
            string? nomeClube = null,
            string? nacionalidadeEsperada = null,
            int? idadeEsperada = null)
        {
            var query = HttpUtility.UrlEncode(nomeJogador);
            var url = $"https://www.transfermarkt.com.br/schnellsuche/ergebnis/schnellsuche?query={query}";

            var html = await _httpClient.GetStringAsync(url);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var linhas = doc.DocumentNode.SelectNodes("//table[contains(@class,'items')]/tbody/tr[not(contains(@class,'thead'))]");
            if (linhas == null || !linhas.Any()) return null;

            HtmlNode? linhaSelecionada = null;

            foreach (var linha in linhas)
            {
                var nomeCell = linha.SelectSingleNode(".//td[@class='hauptlink']/a")?.InnerText?.Trim();
                var clubeCell = ExtrairClubeLinha(linha);

                var nacCell = linha.SelectSingleNode(".//td[@class='zentriert']/img");
                var nacTexto = nacCell?.GetAttributeValue("title", "") ?? "";
                var nacNormalizada = NacionalidadesHelper.Normalizar(nacTexto);

                var idadeCell = linha.SelectSingleNode(".//td[@class='zentriert']");
                int idade = 0;
                if (idadeCell != null && int.TryParse(Regex.Match(idadeCell.InnerText, @"\d+").Value, out var idadeParsed))
                    idade = idadeParsed;

                _logger.LogInformation("[Transfermarkt] Candidato: Nome={Nome}, Clube={Clube}, Nac={Nac}, Idade={Idade}",
                    nomeCell, clubeCell, nacNormalizada, idade);

                // Critérios de seleção
                if (!string.IsNullOrWhiteSpace(nomeClube) && NomesClubeSimilares(clubeCell, nomeClube))
                {
                    linhaSelecionada = linha;
                    break;
                }
                if (!string.IsNullOrWhiteSpace(nacionalidadeEsperada) &&
                    nacNormalizada.Equals(nacionalidadeEsperada, StringComparison.OrdinalIgnoreCase))
                {
                    if (!idadeEsperada.HasValue || idade == idadeEsperada.Value)
                    {
                        linhaSelecionada = linha;
                        break;
                    }
                }
            }

            // Fallback final
            linhaSelecionada ??= linhas[0];

            return await ExtrairDadosDaLinha(linhaSelecionada);
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
                    var partes = texto.Split('(')[0].Trim(); // removes idade entre parênteses
                    var dt = ParseDataNascimento(partes);
                    if (dt.HasValue)
                    {
                        info.DataNascimento = dt;
                    }
                }

                // Extrai dados da tabela (fallback)
                var infoNodes = profileDoc.DocumentNode.SelectNodes("//span[@class='info-table__content info-table__content--bold']");
                var labelNodes = profileDoc.DocumentNode.SelectNodes("//span[@class='info-table__content info-table__content--regular']");

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
                            info.Nacionalidade = NacionalidadesHelper.Normalizar(nomeNacRaw);
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

        private static string MapearPosicaoTransfermarkt(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "MC";

            var s = raw.ToLowerInvariant();

            // Goleiro
            if (s.Contains("gk") || s.Contains("goal") || s.Contains("goleiro") || s.Contains("keeper") || s.Contains("torhüter"))
                return "GL";

            // Zagueiro / defensores / laterais
            if (s.Contains("cb") || s.Contains("center back") || s.Contains("central") || s.Contains("zagueiro") ||
                s.Contains("def") || s.Contains("df") || s.Contains("lb") || s.Contains("rb") ||
                s.Contains("lateral") || s.Contains("wing back") || s.Contains("back"))
                return "ZG";

            // Meia / volantes / meio-campo / ofensivo médio
            if (s.Contains("dm") || s.Contains("cdm") || s.Contains("volante") ||
                s.Contains("cm") || s.Contains("mid") || s.Contains("meia") || s.Contains("am") || s.Contains("cam") || s.Contains("att-mid"))
                return "MC";

            // Atacantes / pontas / centroavante / forwards / striker
            if (s.Contains("fw") || s.Contains("st") || s.Contains("striker") || s.Contains("atacante") ||
                s.Contains("forward") || s.Contains("cf") || s.Contains("lw") || s.Contains("rw") || s.Contains("wing"))
                return "AT";

            // Reservas ou não identificado
            if (s.Contains("sub") || s.Contains("res"))
                return "RES";

            // fallback
            return "MC";
        }

        // helper local dentro da classe TransfermarktService
        private static string ExpandirSiglaPosicaoParaNome(string sigla)
        {
            if (string.IsNullOrWhiteSpace(sigla)) return string.Empty;
            return sigla.ToUpperInvariant() switch
            {
                "GL" => "Goleiro",
                "ZG" => "Zagueiro",
                "MC" => "Meio",
                "AT" => "Atacante",
                "RES" => "Reserva",
                _ => sigla // deixa como veio (pode ser já um nome)
            };
        }

        // Adicione dentro da classe TransfermarktService (ex.: após ExpandirSiglaPosicaoParaNome)
        private static string CleanTeamName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

            // normaliza espaços e quebras
            var s = Regex.Replace(raw, @"\s+", " ").Replace("\r", "").Replace("\n", "").Trim();

            // remove tokens numéricos grandes que aparecem como ruído (ids, contadores)
            s = Regex.Replace(s, @"\d{2,}", "");

            // normaliza separadores e pontuação
            s = Regex.Replace(s, @"\s*[-–—/\\]\s*", " ");
            s = Regex.Replace(s, @"[,:;·•\u2022]+", " ");

            // remove textos entre parênteses que costumam trazer informação extra
            s = Regex.Replace(s, @"\s*\([^)]*\)\s*", " ");

            // remove múltiplos espaços remanescentes e trim final
            s = Regex.Replace(s, @"\s{2,}", " ").Trim();

            // retira pontuação inicial/final
            return s.Trim(new char[] { '-', '–', '—', '/', '\\', ',', '.', '(', ')' });
        }

        private static string RemoveNoiseTokens(string txt)
        {
            if (string.IsNullOrWhiteSpace(txt)) return string.Empty;

            var noise = new[]
            {
            "Agenda", "FT", "UCL", "Copa Libertadores", "UEFA Champions League",
            "Copa Sul-Americana", "Copa do Nordeste", "Sudamericano", "CONCACAF Champions Cup",
            "TV", "SC", "SF", "horário", "horario", "min", "′", "’"
             };

            foreach (var n in noise)
                txt = Regex.Replace(txt, Regex.Escape(n), "", RegexOptions.IgnoreCase);

            // remove timestamps e padrões óbvios de ruído
            txt = Regex.Replace(txt, @"\d{1,2}[:h]\d{2}", ""); // 15:00 ou 15h30
            txt = Regex.Replace(txt, @"\b[A-Z]{2,6}\b", "", RegexOptions.IgnoreCase); // tokens tipo TV, UCL
            txt = Regex.Replace(txt, @"\s{2,}", " ").Trim();

            return txt;
        }
      
        // Necessário comparer para HashSet de HtmlNode baseado em XPath
        private class HtmlNodeXPathComparer : IEqualityComparer<HtmlAgilityPack.HtmlNode>
        {
            public static HtmlNodeXPathComparer Instance { get; } = new HtmlNodeXPathComparer();

            public bool Equals(HtmlAgilityPack.HtmlNode? x, HtmlAgilityPack.HtmlNode? y)
            {
                if (x == null && y == null) return true;
                if (x == null || y == null) return false;
                return string.Equals(x.XPath, y.XPath, StringComparison.Ordinal);
            }

            public int GetHashCode(HtmlAgilityPack.HtmlNode obj)
            {
                return obj.XPath?.GetHashCode() ?? 0;
            }
        }
    }
}