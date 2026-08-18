using ControleFutebolWeb.Models.ViewModels;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace ControleFutebolWeb.Services
{
    /// <summary>
    /// Catálogo estático das ligas da api-football (dump em
    /// wwwroot/data/competicoes-api-{temporada}.json), que alimenta a tela
    /// /Competicoes/CompeticoesApi e a escolha de liga em /Transferencias.
    ///
    /// Serve também para validar o código digitado no link "apifoot:LEAGUE_ID:SEASON"
    /// ao cadastrar uma competição e reaproveitar o escudo que a API já publica —
    /// assim não é preciso subir um logo por competição.
    /// </summary>
    public class CatalogoLigasApi
    {
        private readonly IWebHostEnvironment _env;
        private readonly IMemoryCache _cache;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<CatalogoLigasApi> _logger;

        public CatalogoLigasApi(
            IWebHostEnvironment env,
            IMemoryCache cache,
            IServiceScopeFactory scopeFactory,
            ILogger<CatalogoLigasApi> logger)
        {
            _env = env;
            _cache = cache;
            // Este serviço é singleton e o ApiFootballService vem de AddHttpClient:
            // resolver por escopo evita prender um HttpClient para sempre.
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        /// <summary>
        /// Ligas do dump da temporada pedida. Sem arquivo para ela, usa o dump mais
        /// recente disponível — o catálogo muda pouco de ano para ano e um link
        /// "apifoot:128:2027" não deve ser recusado só por faltar o arquivo de 2027.
        /// </summary>
        public async Task<List<CompeticaoApiLiga>> CarregarAsync(int? temporada = null)
        {
            var caminho = ResolverCaminho(temporada);
            if (caminho == null) return new();

            if (_cache.TryGetValue<List<CompeticaoApiLiga>>(CacheKey(caminho), out var cacheado) && cacheado != null)
                return cacheado;

            var json = await File.ReadAllTextAsync(caminho);
            var ligas = JsonSerializer.Deserialize<List<CompeticaoApiLiga>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

            _cache.Set(CacheKey(caminho), ligas, new MemoryCacheEntryOptions
            {
                Size = json.Length,
                SlidingExpiration = TimeSpan.FromHours(6),
            });

            return ligas;
        }

        /// <summary>
        /// Liga com esse id, ou null se o código não existe nem no dump nem na API.
        ///
        /// O dump é um recorte (hoje ~512 ligas, contra 733 que a API lista na temporada),
        /// então um código ausente dele não significa código inválido — ex.: 308 (Division 1
        /// da Arábia Saudita) existe na API e era recusado no cadastro. Só quando o dump não
        /// tem é que consulta /leagues?id=X, e o resultado fica em cache por 7 dias.
        /// </summary>
        public async Task<CompeticaoApiLiga?> BuscarLigaAsync(
            int leagueId, int? temporada = null, CancellationToken ct = default)
        {
            var doDump = (await CarregarAsync(temporada)).FirstOrDefault(l => l.Id == leagueId);
            if (doDump != null) return doDump;

            return await BuscarNaApiAsync(leagueId, ct);
        }

        // Falha de rede/quota não pode virar exceção na tela de cadastro: sem resposta,
        // o código continua sendo tratado como inexistente (mesmo comportamento de antes).
        private async Task<CompeticaoApiLiga?> BuscarNaApiAsync(int leagueId, CancellationToken ct)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var api = scope.ServiceProvider.GetRequiredService<ApiFootballService>();

                var entrada = await api.BuscarLigaApiAsync(leagueId, ct);
                if (entrada == null || entrada.League.Id == 0) return null;

                return new CompeticaoApiLiga
                {
                    Id = entrada.League.Id,
                    Nome = entrada.League.Name,
                    Tipo = entrada.League.Type,
                    Logo = entrada.League.Logo,
                    Pais = entrada.Country.Name,
                    Bandeira = entrada.Country.Flag ?? "",
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[CatalogoLigas] Falha ao consultar a liga {Id} na api-football.", leagueId);
                return null;
            }
        }

        private string? ResolverCaminho(int? temporada)
        {
            var pasta = Path.Combine(_env.WebRootPath, "data");
            if (!Directory.Exists(pasta)) return null;

            if (temporada.HasValue)
            {
                var doAno = Path.Combine(pasta, $"competicoes-api-{temporada}.json");
                if (File.Exists(doAno)) return doAno;
            }

            return Directory.GetFiles(pasta, "competicoes-api-*.json")
                .OrderByDescending(f => f)
                .FirstOrDefault();
        }

        private static string CacheKey(string caminho) => $"catalogo-ligas-api:{caminho}";
    }
}
