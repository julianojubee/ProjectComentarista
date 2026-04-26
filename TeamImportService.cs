using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace ControleFutebolWeb.Services
{
    public class TeamImportService : ITeamImportService
    {
        private readonly HttpClient _http;
        private readonly FutebolContext _db;
        private readonly ILogger<TeamImportService> _logger;
        private readonly string _baseUrl;
        private readonly string _apiKey;

        public TeamImportService(HttpClient http, FutebolContext db, IConfiguration config, ILogger<TeamImportService> logger)
        {
            _http = http;
            _db = db;
            _logger = logger;
            _baseUrl = config["TeamApi:BaseUrl"] ?? throw new ArgumentNullException("TeamApi:BaseUrl not configured");
            _apiKey = config["TeamApi:ApiKey"] ?? string.Empty;
            if (!string.IsNullOrEmpty(_apiKey))
            {
                _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            }
        }

        public async Task<int> ImportSerieATeamsAsync(CancellationToken cancellationToken = default)
        {
            // Example: GET {baseUrl}/teams
            var url = $"{_baseUrl}/teams";
            using var resp = await _http.GetAsync(url, cancellationToken);
            resp.EnsureSuccessStatusCode();

            using var stream = await resp.Content.ReadAsStreamAsync(cancellationToken);

            // DTO adapted to simple array; change according to provider
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            var apiTeams = await JsonSerializer.DeserializeAsync<List<ApiTeamDto>>(stream, options, cancellationToken)
                           ?? new List<ApiTeamDto>();

            var added = 0;
            foreach (var at in apiTeams)
            {
                // Normalize name for comparison
                var name = at.Name?.Trim();
                if (string.IsNullOrEmpty(name)) continue;

                var exists = await _db.Times.AnyAsync(t => t.Nome == name, cancellationToken);
                if (exists) continue;

                var time = new Time
                {
                    Nome = name,
                    Cidade = at.City?.Trim() ?? string.Empty,
                    Jogadores = new List<Jogador>()
                };

                _db.Times.Add(time);
                added++;
            }

            if (added > 0) await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Imported {Count} teams from API", added);
            return added;
        }

        private class ApiTeamDto
        {
            public string? Name { get; set; }
            public string? City { get; set; }
            // add other fields returned by the chosen API (e.g., id, venue, crest)
        }
    }
}