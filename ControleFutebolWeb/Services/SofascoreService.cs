using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Web;

namespace ControleFutebolWeb.Services
{
    public class SofascoreService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<SofascoreService> _logger;

        // Endpoint correto confirmado pelo usuário
        private const string SearchUrl =
            "https://api.sofascore.com/api/v1/search/{0}";

        // URL da foto: basta ter o ID do jogador
        private const string ImageUrl =
            "https://img.sofascore.com/api/v1/player/{0}/image";

        public SofascoreService(HttpClient httpClient, ILogger<SofascoreService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        /// <summary>
        /// Busca a URL da foto do jogador usando a API interna do Sofascore.
        /// Ex: https://api.sofascore.com/api/v1/search/Alan%20Patrick%20Internacional
        /// </summary>
        public async Task<string?> BuscarFotoJogador(string nomeJogador, string? nomeClube = null)
        {
            try
            {
                _logger.LogInformation("[Sofascore] Buscando: '{Nome}' | Clube: '{Clube}'",
                    nomeJogador, nomeClube);

                // Tentativa 1: nome + clube
                var playerId = await BuscarIdAsync(nomeJogador, nomeClube);

                // Tentativa 2: só o nome
                if (playerId == null && !string.IsNullOrWhiteSpace(nomeClube))
                {
                    _logger.LogInformation("[Sofascore] Sem resultado com clube, tentando só o nome.");
                    await Task.Delay(800);
                    playerId = await BuscarIdAsync(nomeJogador, null);
                }

                if (playerId == null)
                {
                    _logger.LogInformation("[Sofascore] Jogador não encontrado: '{Nome}'", nomeJogador);
                    return null;
                }

                var foto = string.Format(ImageUrl, playerId);
                _logger.LogInformation("[Sofascore] Foto: {Url}", foto);
                return foto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Sofascore] Erro ao buscar foto de '{Nome}'", nomeJogador);
                return null;
            }
        }

        // ── Busca o ID do jogador na API ──────────────────────────────────────

        private async Task<string?> BuscarIdAsync(string nomeJogador, string? nomeClube)
        {
            var termo = string.IsNullOrWhiteSpace(nomeClube)
                ? nomeJogador.Trim()
                : $"{nomeJogador.Trim()} {nomeClube.Trim()}";

            var url = string.Format(SearchUrl, HttpUtility.UrlEncode(termo));

            _logger.LogInformation("[Sofascore] GET: {Url}", url);

            string json;
            try
            {
                await Task.Delay(Random.Shared.Next(600, 1200));

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                AdicionarHeaders(request);

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("[Sofascore] HTTP {Status} para '{Termo}'",
                        (int)response.StatusCode, termo);
                    return null;
                }

                json = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("[Sofascore] JSON recebido: {Json}", json[..Math.Min(500, json.Length)]);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Sofascore] Falha HTTP para '{Termo}'", termo);
                return null;
            }

            return ParsearIdDoJson(json, nomeJogador, nomeClube);
        }

        // ── Parseia o JSON e retorna o ID do melhor resultado ─────────────────

        private string? ParsearIdDoJson(string json, string nomeJogador, string? nomeClube)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // O endpoint /api/v1/search/{termo} pode retornar estruturas diferentes.
                // Tenta localizar o array de resultados em qualquer uma delas:
                //
                // Estrutura A — array direto:
                // [ { "id": 123, "name": "...", "team": {...} }, ... ]
                //
                // Estrutura B — objeto com "results":
                // { "results": [ { "type": "player", "entity": { "id": 123, ... } }, ... ] }
                //
                // Estrutura C — objeto com "players":
                // { "players": [ { "id": 123, "name": "...", "team": {...} }, ... ] }

                JsonElement resultsElement;

                if (root.ValueKind == JsonValueKind.Array)
                {
                    resultsElement = root;
                }
                else if (root.TryGetProperty("results", out var r))
                {
                    resultsElement = r;
                }
                else if (root.TryGetProperty("players", out var p))
                {
                    resultsElement = p;
                }
                else
                {
                    // Loga as chaves disponíveis para ajudar no diagnóstico
                    var chaves = root.ValueKind == JsonValueKind.Object
                        ? string.Join(", ", root.EnumerateObject().Select(x => x.Name))
                        : root.ValueKind.ToString();
                    _logger.LogWarning("[Sofascore] Estrutura JSON inesperada. Chaves/tipo: {Chaves}", chaves);
                    return null;
                }

                var nomeNorm = NormalizarTexto(nomeJogador);
                var clubeNorm = string.IsNullOrWhiteSpace(nomeClube)
                    ? null
                    : NormalizarTexto(nomeClube);

                string? melhorId = null;
                int melhorScore = -1;

                foreach (var item in resultsElement.EnumerateArray())
                {
                    // Suporte às duas estruturas de item:
                    // A) item tem "id" e "name" diretamente (array direto ou "players")
                    // B) item tem "type" e "entity" (estrutura com "results")

                    JsonElement jogadorNode;

                    if (item.TryGetProperty("entity", out var entity))
                    {
                        // Estrutura B — verifica se o tipo é "player"
                        if (item.TryGetProperty("type", out var typeProp) &&
                            typeProp.GetString() != "player")
                            continue;

                        jogadorNode = entity;
                    }
                    else
                    {
                        // Estrutura A — o item já é o jogador
                        jogadorNode = item;
                    }

                    if (!jogadorNode.TryGetProperty("id", out var idProp)) continue;

                    var id = idProp.GetInt64().ToString();

                    var nomeResultado = "";
                    if (jogadorNode.TryGetProperty("name", out var nameProp))
                        nomeResultado = NormalizarTexto(nameProp.GetString() ?? "");

                    var timeResultado = "";
                    if (jogadorNode.TryGetProperty("team", out var teamProp) &&
                        teamProp.TryGetProperty("name", out var teamNameProp))
                        timeResultado = NormalizarTexto(teamNameProp.GetString() ?? "");

                    _logger.LogInformation(
                        "[Sofascore] Candidato — id={Id} | nome='{Nome}' | time='{Time}'",
                        id, nomeResultado, timeResultado);

                    // Nome precisa bater minimamente
                    bool nomeOk = nomeResultado.Contains(nomeNorm)
                               || nomeNorm.Contains(nomeResultado)
                               || PrimeiroTokenIgual(nomeResultado, nomeNorm);

                    if (!nomeOk) continue;

                    int score = 1;

                    // Clube tem peso alto quando informado
                    if (clubeNorm != null &&
                        (timeResultado.Contains(clubeNorm) || clubeNorm.Contains(timeResultado)))
                        score += 10;

                    if (score > melhorScore)
                    {
                        melhorScore = score;
                        melhorId = id;
                    }
                }

                if (melhorId != null)
                {
                    _logger.LogInformation("[Sofascore] Selecionado — id={Id} (score={Score})",
                        melhorId, melhorScore);
                    return melhorId;
                }

                _logger.LogInformation("[Sofascore] Nenhum resultado bateu os critérios.");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Sofascore] Erro ao parsear JSON.");
                return null;
            }
        }

        // ── Headers que simulam o Chrome fazendo chamada CORS à API ──────────

        private static void AdicionarHeaders(HttpRequestMessage request)
        {
            request.Headers.TryAddWithoutValidation("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                "(KHTML, like Gecko) Chrome/124.0.6367.202 Safari/537.36");
            request.Headers.TryAddWithoutValidation("Accept", "application/json");
            request.Headers.TryAddWithoutValidation("Accept-Language",
                "pt-BR,pt;q=0.9,en-US;q=0.8,en;q=0.7");
            request.Headers.TryAddWithoutValidation("Origin", "https://www.sofascore.com");
            request.Headers.TryAddWithoutValidation("Referer", "https://www.sofascore.com/");
            request.Headers.TryAddWithoutValidation("Sec-Ch-Ua",
                "\"Chromium\";v=\"124\", \"Google Chrome\";v=\"124\", \"Not-A.Brand\";v=\"99\"");
            request.Headers.TryAddWithoutValidation("Sec-Ch-Ua-Mobile", "?0");
            request.Headers.TryAddWithoutValidation("Sec-Ch-Ua-Platform", "\"Windows\"");
            request.Headers.TryAddWithoutValidation("Sec-Fetch-Dest", "empty");
            request.Headers.TryAddWithoutValidation("Sec-Fetch-Mode", "cors");
            request.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "same-site");
            request.Headers.TryAddWithoutValidation("Connection", "keep-alive");
        }

        // ── Helpers de texto ─────────────────────────────────────────────────

        private static string NormalizarTexto(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";

            return s.ToLowerInvariant()
                .Replace("á", "a").Replace("à", "a").Replace("â", "a").Replace("ã", "a")
                .Replace("é", "e").Replace("ê", "e")
                .Replace("í", "i").Replace("î", "i")
                .Replace("ó", "o").Replace("ô", "o").Replace("õ", "o")
                .Replace("ú", "u").Replace("û", "u")
                .Replace("ç", "c").Replace("ñ", "n")
                .Replace("-", " ")
                .Trim();
        }

        private static bool PrimeiroTokenIgual(string a, string b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
            return a.Split(' ')[0] == b.Split(' ')[0];
        }
    }
}
