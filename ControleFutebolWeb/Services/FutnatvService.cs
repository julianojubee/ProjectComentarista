using System.Net.Http.Json;

namespace ControleFutebolWeb.Services
{
    // Consome a API pública do futnatv.net (GET /api/futebol?data=yyyy-MM-dd),
    // que lista os jogos do dia com a plataforma de transmissão de cada um.
    public class FutnatvService
    {
        private readonly HttpClient _http;
        private readonly ILogger<FutnatvService> _logger;

        public FutnatvService(HttpClient http, ILogger<FutnatvService> logger)
        {
            _http = http;
            _http.BaseAddress = new Uri("https://futnatv.net/");
            _http.Timeout = TimeSpan.FromSeconds(15);
            _logger = logger;
        }

        // dataBrasilia: data (sem hora) no fuso de referência do site, formato yyyy-MM-dd.
        // Não engole erro: quem chama decide como logar/monitorar a falha (ver AtualizarTransmissoesService).
        public async Task<FutnatvResultado> BuscarJogosDoDiaAsync(DateOnly dataBrasilia, CancellationToken ct = default)
        {
            var chaveData = dataBrasilia.ToString("yyyy-MM-dd");
            var url = $"api/futebol?data={chaveData}";
            try
            {
                var resposta = await _http.GetFromJsonAsync<FutnatvResponse>(url, ct);
                var dia = resposta?.Schedule?.FirstOrDefault(d => d.Key == chaveData);
                var jogos = dia?.Games ?? new List<FutnatvJogo>();

                return new FutnatvResultado(true, url, jogos, null);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or NotSupportedException or System.Text.Json.JsonException)
            {
                _logger.LogWarning(ex, "[Futnatv] Falha ao buscar jogos de {Data}.", chaveData);
                return new FutnatvResultado(false, url, new List<FutnatvJogo>(), ex.Message);
            }
        }
    }
}
