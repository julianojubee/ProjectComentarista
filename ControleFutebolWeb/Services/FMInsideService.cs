
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using System.Web;

namespace ControleFutebolWeb.Services
{
    public class FMInsideService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<FMInsideService> _logger;

        private const string BaseUrl = "https://fminside.net";

        public FMInsideService(HttpClient httpClient, ILogger<FMInsideService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                "(KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept",
                "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "pt-BR,pt;q=0.9,en;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Referer", "https://fminside.net/");
        }

        /// <summary>
        /// Busca a URL da foto do jogador no FMInside.
        /// Tenta com nome + clube, depois só nome se não achar.
        /// </summary>
        public async Task<string?> BuscarFotoJogador(string nomeJogador, string? nomeClube = null)
        {
            try
            {
                _logger.LogInformation("[FMInside] Buscando: {Nome} | Clube: {Clube}",
                    nomeJogador, nomeClube);

                // Estratégia 1: nome + clube
                if (!string.IsNullOrWhiteSpace(nomeClube))
                {
                    var clubeNorm = NormalizarParaBusca(nomeClube);
                    var foto = await BuscarNaListagem(nomeJogador, clubeNorm);
                    if (foto != null) return foto;

                    // Estratégia 2: nome + palavra-chave do clube
                    var palavraChave = ExtrairPalavraChaveClube(nomeClube);
                    if (!string.IsNullOrWhiteSpace(palavraChave) &&
                        palavraChave != clubeNorm)
                    {
                        _logger.LogInformation("[FMInside] Tentando com palavra-chave: '{P}'",
                            palavraChave);
                        foto = await BuscarNaListagem(nomeJogador, palavraChave);
                        if (foto != null) return foto;
                    }
                }

                // Estratégia 3: só pelo nome
                _logger.LogInformation("[FMInside] Tentando só pelo nome: '{N}'", nomeJogador);
                return await BuscarNaListagem(nomeJogador, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[FMInside] Erro ao buscar foto de {Nome}", nomeJogador);
                return null;
            }
        }

        /// <summary>
        /// Faz a busca na listagem do FMInside e retorna a URL da foto do melhor resultado.
        /// </summary>
        private async Task<string?> BuscarNaListagem(string nome, string? clube)
        {
            var urlBusca = $"{BaseUrl}/players?name={HttpUtility.UrlEncode(nome)}";
            if (!string.IsNullOrWhiteSpace(clube))
                urlBusca += $"&club={HttpUtility.UrlEncode(clube)}";

            _logger.LogInformation("[FMInside] GET: {Url}", urlBusca);

            string html;
            try
            {
                html = await _httpClient.GetStringAsync(urlBusca);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[FMInside] Falha HTTP: {Url}", urlBusca);
                return null;
            }

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Links de perfil ficam em /players/ID/nome-do-jogador
            var linksJogador = doc.DocumentNode.SelectNodes(
                "//a[contains(@href,'/players/') and string-length(@href) > 10]");

            if (linksJogador == null || !linksJogador.Any())
            {
                _logger.LogInformation("[FMInside] Nenhum resultado para '{N}' / '{C}'",
                    nome, clube ?? "-");
                return null;
            }

            // Pega o primeiro link de perfil único (deduplica)
            var hrefs = linksJogador
                .Select(l => l.GetAttributeValue("href", ""))
                .Where(h => System.Text.RegularExpressions.Regex.IsMatch(h, @"/players/\d+"))
                .Distinct()
                .ToList();

            if (!hrefs.Any()) return null;

            var perfilUrl = hrefs.First().StartsWith("http")
                ? hrefs.First()
                : BaseUrl + hrefs.First();

            _logger.LogInformation("[FMInside] Perfil encontrado: {Url}", perfilUrl);

            return await ExtrairFotoDoPerfilAsync(perfilUrl);
        }

        /// <summary>
        /// Acessa a página de perfil e extrai a URL da foto via og:image.
        /// </summary>
        private async Task<string?> ExtrairFotoDoPerfilAsync(string perfilUrl)
        {
            try
            {
                await Task.Delay(500); // pausa leve — FMInside é menos restritivo

                var html = await _httpClient.GetStringAsync(perfilUrl);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // <meta property="og:image" content="//img.fminside.net/facesfm26/ID.png" />
                var ogImage = doc.DocumentNode
                    .SelectSingleNode("//meta[@property='og:image']");

                var content = ogImage?.GetAttributeValue("content", "")?.Trim();

                if (string.IsNullOrWhiteSpace(content))
                {
                    _logger.LogWarning("[FMInside] og:image não encontrado em {Url}", perfilUrl);
                    return null;
                }

                // Garante protocolo https (o FMInside às vezes omite o schema: //img.fminside...)
                if (content.StartsWith("//"))
                    content = "https:" + content;

                // Ignora placeholder (face padrão do FM)
                if (content.Contains("default") || content.Contains("silhouette") ||
                    content.Contains("placeholder") || content.Contains("noface"))
                {
                    _logger.LogInformation("[FMInside] Foto padrão ignorada: {Url}", content);
                    return null;
                }

                _logger.LogInformation("[FMInside] Foto encontrada: {Url}", content);
                return content;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[FMInside] Erro ao acessar perfil {Url}", perfilUrl);
                return null;
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// Normaliza o nome do clube para a busca no FMInside.
        /// Remove prefixos como SC, CR, EC, FC e cidades conhecidas.
        /// </summary>
        private static string NormalizarParaBusca(string nomeClube)
        {
            var stopwords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "sc", "cr", "ec", "fc", "cd", "ca", "ac", "se", "rb", "gd", "fb",
                "sport", "clube", "club", "futebol", "football",
                "de", "do", "da", "dos", "las", "los",
                "porto", "alegre"
            };

            var tokens = nomeClube
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(t => t.Length > 1 && !stopwords.Contains(t))
                .ToArray();

            return string.Join(" ", tokens).Trim();
        }

        /// <summary>
        /// Extrai a palavra mais característica do clube para fallback de busca.
        /// Ex: "SC Internacional Porto Alegre" → "Internacional"
        /// </summary>
        private static string ExtrairPalavraChaveClube(string nomeClube)
        {
            var stopwords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "sc", "cr", "ec", "fc", "cd", "ca", "ac", "se", "rb", "gd", "fb",
                "sport", "clube", "club", "futebol", "football",
                "de", "do", "da", "dos", "las", "los",
                "porto", "alegre", "rio", "janeiro", "sao", "paulo", "são",
                "atletico", "athletico"
            };

            var tokens = nomeClube
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim('.', ','))
                .Where(t => t.Length > 2 && !stopwords.Contains(t))
                .ToList();

            // Retorna a palavra mais longa (tende a ser a mais característica)
            return tokens.OrderByDescending(t => t.Length).FirstOrDefault() ?? "";
        }
    }
}