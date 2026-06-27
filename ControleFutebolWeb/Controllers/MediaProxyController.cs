using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace ControleFutebolWeb.Controllers
{
    public class MediaProxyController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<MediaProxyController> _logger;
        private readonly IConfiguration _config;
        private readonly IMemoryCache _cache;

        private static readonly HashSet<string> _allowedHosts = new(StringComparer.OrdinalIgnoreCase)
        {
            "media.api-sports.io",
            "media-1.api-sports.io",
            "media-2.api-sports.io",
            "media-3.api-sports.io",
        };

        private sealed record CachedImage(byte[] Bytes, string ContentType);

        public MediaProxyController(IHttpClientFactory httpClientFactory,
            ILogger<MediaProxyController> logger, IConfiguration config, IMemoryCache cache)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _config = config;
            _cache = cache;
        }

        // GET /MediaProxy/Imagem?url=https://media.api-sports.io/football/players/50077.png
        [HttpGet]
        [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
        public async Task<IActionResult> Imagem([FromQuery] string url, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(url))
                return BadRequest();

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                !_allowedHosts.Contains(uri.Host))
                return Forbid();

            // Serve do cache em memória quando disponível (evita rebaixar a mesma imagem,
            // especialmente escudos que se repetem em várias linhas).
            if (_cache.TryGetValue(url, out CachedImage? cached) && cached != null)
                return File(cached.Bytes, cached.ContentType);

            try
            {
                var client = _httpClientFactory.CreateClient("MediaProxy");

                // Fotos do api-sports exigem autenticação mesmo para imagens
                if (_allowedHosts.Contains(uri.Host))
                {
                    var apiKey = _config["ApiFootball:Key"];
                    if (!string.IsNullOrEmpty(apiKey) &&
                        !client.DefaultRequestHeaders.Contains("x-apisports-key"))
                        client.DefaultRequestHeaders.Add("x-apisports-key", apiKey);
                }

                var response = await client.GetAsync(url, ct);

                if (!response.IsSuccessStatusCode)
                    return StatusCode((int)response.StatusCode);

                var remoteType = response.Content.Headers.ContentType?.MediaType ?? "";
                var allowedTypes = new HashSet<string> { "image/png", "image/jpeg", "image/webp", "image/gif", "image/svg+xml" };
                var contentType = allowedTypes.Contains(remoteType) ? remoteType : "image/png";
                var bytes = await response.Content.ReadAsByteArrayAsync(ct);

                _cache.Set(url, new CachedImage(bytes, contentType), new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24),
                    Size = bytes.Length
                });

                return File(bytes, contentType);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[MediaProxy] Falha ao buscar {Url}", url);
                return NotFound();
            }
        }
    }
}
