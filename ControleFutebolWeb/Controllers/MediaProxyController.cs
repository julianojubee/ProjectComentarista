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

        private static readonly HashSet<string> _apiSportsHosts = new(StringComparer.OrdinalIgnoreCase)
        {
            "media.api-sports.io",
            "media-1.api-sports.io",
            "media-2.api-sports.io",
            "media-3.api-sports.io",
        };

        private static readonly HashSet<string> _allowedHosts = new(
            _apiSportsHosts.Append("flagcdn.com")   // bandeiras de países (FlagHelper.GetFlagImageUrl)
                           .Append(_hostEscudos)    // escudos das telas de estatística avançada
                           .Append(_hostFifa)       // bandeiras e artes das competições da FIFA
                           .Append(_hostFotosFifa), // fotos oficiais de jogadoras e técnicos da FIFA
            StringComparer.OrdinalIgnoreCase);

        // Host dos escudos usados pela tela de estatísticas avançadas do jogador.
        // Fica aqui, e não na view, para que a página servida ao usuário não carregue
        // endereço de fonte externa nenhum: o HTML pede /MediaProxy/Escudo/{id} e é
        // este controller que sabe de onde a imagem vem.
        private const string _hostEscudos = "images.fotmob.com";

        // Bandeira que serve de escudo às seleções importadas da FIFA
        // ("…/picture/flags-sq-4/BRA") e a arte da edição, que vira o logo da
        // competição — ver FifaService. Sem este host na allowlist a resposta é 403 e
        // a tela mostra o alt do <img> no lugar de todos os escudos.
        private const string _hostFifa = "api.fifa.com";

        // Fotos do elenco importado da FIFA (FifaService.ImportarElencoAsync): a API
        // manda o endereço no digitalhub, não no próprio api.fifa.com.
        private const string _hostFotosFifa = "digitalhub.fifa.com";

        private static readonly HashSet<string> _tiposPermitidos = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/png", "image/jpeg", "image/webp", "image/gif", "image/svg+xml"
        };

        private sealed record CachedImage(byte[] Bytes, string ContentType);

        // Quanto tempo uma URL que acabou de falhar fica marcada como ruim. Curto de
        // propósito: é só para atravessar uma instabilidade do upstream, não para
        // esconder a imagem até o próximo restart.
        private static readonly TimeSpan _ttlFalha = TimeSpan.FromMinutes(5);

        public MediaProxyController(IHttpClientFactory httpClientFactory,
            ILogger<MediaProxyController> logger, IConfiguration config, IMemoryCache cache)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _config = config;
            _cache = cache;
        }

        private static string ChaveImagem(string url) => $"midia:{url}";
        private static string ChaveFalha(string url) => $"midia-falha:{url}";

        // O [ResponseCache] da action grava Cache-Control: public,max-age=86400 ANTES
        // de a action rodar — ou seja, também nas respostas de erro. Sem desfazer isso,
        // um timeout momentâneo do api-sports fazia o navegador guardar o 404 e deixar
        // a foto/escudo sumido por 24 h, mesmo com o upstream já normalizado.
        private IActionResult SemCache(IActionResult resultado)
        {
            Response.Headers.CacheControl = "no-store";
            Response.Headers.Remove("Expires");
            Response.Headers.Remove("Pragma");
            return resultado;
        }

        // Marca a URL como falha recente para que as próximas requisições (a mesma
        // tela costuma pedir dezenas de imagens, e o usuário recarrega) respondam na
        // hora, em vez de cada uma segurar uma conexão até estourar o timeout.
        private void RegistrarFalha(string url) =>
            _cache.Set(ChaveFalha(url), true, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _ttlFalha,
                Size = 1 // obrigatório: o MemoryCache tem SizeLimit configurado.
            });

        /// <summary>
        /// GET /MediaProxy/Escudo/203826 — escudo de um clube ou seleção pelo id.
        ///
        /// Existe para a URL de origem não aparecer no HTML: passar a imagem por
        /// ?url=... deixaria o endereço da fonte visível em toda página que mostra um
        /// escudo. Aqui a página só cita um número, e a montagem acontece no servidor.
        /// </summary>
        [HttpGet]
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
        public Task<IActionResult> Escudo(long id, CancellationToken ct) =>
            id <= 0
                ? Task.FromResult(SemCache(BadRequest()))
                : Imagem($"https://{_hostEscudos}/image_resources/logo/teamlogo/{id}.png", ct);

        /// <summary>
        /// GET /MediaProxy/Liga/87 — símbolo de uma competição pelo id. Mesma razão do
        /// escudo: a página só cita o número.
        /// </summary>
        [HttpGet]
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
        public Task<IActionResult> Liga(long id, CancellationToken ct) =>
            id <= 0
                ? Task.FromResult(SemCache(BadRequest()))
                : Imagem($"https://{_hostEscudos}/image_resources/logo/leaguelogo/{id}.png", ct);

        /// <summary>
        /// GET /MediaProxy/FotoJogador/1815149 — retrato do jogador pelo id do FotMob.
        ///
        /// Mesma razao do escudo acima: e este caminho, e nao a URL da fonte, que fica
        /// gravado em Jogador.FotoUrl e aparece no HTML. Como o valor guardado e
        /// relativo, ele atravessa FotoSrc/FotoSrcAbsoluto sem tratamento especial e
        /// serve tambem o app Android.
        /// </summary>
        [HttpGet]
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
        public Task<IActionResult> FotoJogador(long id, CancellationToken ct) =>
            id <= 0
                ? Task.FromResult(SemCache(BadRequest()))
                : Imagem(Services.FotMobService.FotoJogadorUrl(id), ct);

        // GET /MediaProxy/Imagem?url=https://media.api-sports.io/football/players/50077.png
        // AllowAnonymous: o app Android carrega imagens pelo Coil, que não envia
        // cookie nem JWT — com o AuthorizeFilter global a resposta virava redirect
        // de login e nenhuma foto/escudo aparecia. Baixo risco: allowlist estrita
        // de hosts (api-sports/flagcdn), só imagens, com cache.
        [HttpGet]
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
        public async Task<IActionResult> Imagem([FromQuery] string url, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(url))
                return SemCache(BadRequest());

            // Caminho local (ex.: /MediaProxy/FotoJogador/123, gravado pela sincronização
            // com o FotMob): não há o que buscar fora — só redireciona para ele. Muitas
            // telas passam toda FotoUrl por aqui sem distinguir. "//" e "/\" ficam de
            // fora porque o navegador os trata como outro host (open redirect).
            if (url.StartsWith('/') && !url.StartsWith("//") && !url.StartsWith("/\\") &&
                Url.IsLocalUrl(url))
                return SemCache(Redirect(url));

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                !_allowedHosts.Contains(uri.Host))
                // StatusCode e não Forbid(): com o esquema de cookie, Forbid() vira
                // redirect 302 para /Account/Login, e a <img> acabava recebendo a
                // página de login em vez de uma negativa.
                return SemCache(StatusCode(StatusCodes.Status403Forbidden));

            // Serve do cache em memória quando disponível (evita rebaixar a mesma imagem,
            // especialmente escudos que se repetem em várias linhas).
            if (_cache.TryGetValue(ChaveImagem(url), out CachedImage? cached) && cached != null)
                return File(cached.Bytes, cached.ContentType);

            // Falhou há pouco: devolve o erro de imediato, sem bater no upstream.
            if (_cache.TryGetValue(ChaveFalha(url), out _))
                return SemCache(NotFound());

            try
            {
                var client = _httpClientFactory.CreateClient("MediaProxy");

                using var requisicao = new HttpRequestMessage(HttpMethod.Get, uri);

                // Fotos do api-sports exigem autenticação mesmo para imagens. O header
                // vai na requisição (e não em DefaultRequestHeaders) para que a chave
                // nunca acompanhe um cliente reaproveitado para outro host.
                if (_apiSportsHosts.Contains(uri.Host))
                {
                    var apiKey = _config["ApiFootball:Key"];
                    if (!string.IsNullOrEmpty(apiKey))
                        requisicao.Headers.TryAddWithoutValidation("x-apisports-key", apiKey);
                }

                var response = await client.SendAsync(requisicao, ct);

                if (!response.IsSuccessStatusCode)
                {
                    RegistrarFalha(url);
                    return SemCache(StatusCode((int)response.StatusCode));
                }

                var remoteType = response.Content.Headers.ContentType?.MediaType ?? "";
                var contentType = _tiposPermitidos.Contains(remoteType) ? remoteType : "image/png";
                var bytes = await response.Content.ReadAsByteArrayAsync(ct);

                _cache.Set(ChaveImagem(url), new CachedImage(bytes, contentType), new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24),
                    Size = bytes.Length
                });

                return File(bytes, contentType);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // O próprio usuário abandonou a página. Não é falha do upstream: não
                // loga e não marca a URL como ruim (a resposta nem chega a ninguém).
                return SemCache(new EmptyResult());
            }
            catch (TaskCanceledException)
            {
                // Timeout do HttpClient ("MediaProxy", 10s). Uma tela cheia de fotos
                // gera dezenas destes de uma vez — loga uma linha, sem stack trace,
                // que aqui é sempre o mesmo e só afogava o log.
                RegistrarFalha(url);
                _logger.LogWarning("[MediaProxy] Timeout ao buscar {Url}", url);
                return SemCache(NotFound());
            }
            catch (HttpRequestException ex)
            {
                // DNS que não resolve, conexão recusada, TLS — a causa está na
                // mensagem ("Este host não é conhecido", etc.); o stack trace é
                // sempre o mesmo caminho do HttpClient e não ajuda a diagnosticar.
                RegistrarFalha(url);
                _logger.LogWarning("[MediaProxy] Falha de rede ao buscar {Url}: {Motivo}",
                    url, ex.Message);
                return SemCache(NotFound());
            }
            catch (Exception ex)
            {
                RegistrarFalha(url);
                _logger.LogWarning(ex, "[MediaProxy] Falha ao buscar {Url}", url);
                return SemCache(NotFound());
            }
        }
    }
}
