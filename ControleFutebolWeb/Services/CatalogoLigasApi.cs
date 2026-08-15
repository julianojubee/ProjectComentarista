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

        public CatalogoLigasApi(IWebHostEnvironment env, IMemoryCache cache)
        {
            _env = env;
            _cache = cache;
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

        /// <summary>Liga do catálogo com esse id, ou null se o código não existe.</summary>
        public async Task<CompeticaoApiLiga?> BuscarLigaAsync(int leagueId, int? temporada = null)
            => (await CarregarAsync(temporada)).FirstOrDefault(l => l.Id == leagueId);

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
