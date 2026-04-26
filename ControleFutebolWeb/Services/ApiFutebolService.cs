using ControleFutebolWeb.Models;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace ControleFutebolWeb.Services
{
    public class ApiFootballDataService
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _options;

        public ApiFootballDataService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri("https://api.football-data.org/v4/");
            _httpClient.DefaultRequestHeaders.Add("X-Auth-Token", config["FootballData:Token"]);
            _options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        }

        // Times da competição
        public async Task<List<ClubeInfo>> GetTeamsAsync(string competitionCode)
        {
            var response = await _httpClient.GetAsync($"competitions/{competitionCode}/teams");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();

            var wrapper = JsonSerializer.Deserialize<TeamsResponse>(json, _options)!;
            return wrapper.Teams.Select(t => new ClubeInfo
            {
                Id = t.Id,
                Nome = t.Name,
                Escudo = t.Crest
            }).ToList();
        }

        // Partidas da competição
        public async Task<List<MatchResponse>> GetMatchesAsync(string competitionCode)
        {
            var response = await _httpClient.GetAsync($"competitions/{competitionCode}/matches");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();

            var wrapper = JsonSerializer.Deserialize<MatchesResponse>(json, _options)!;
            return wrapper.Matches;
        }


        // Detalhes do time + elenco
        public async Task<TeamDetail> GetTeamDetailAsync(int teamId)
        {
            var response = await _httpClient.GetAsync($"teams/{teamId}");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();

            var team = JsonSerializer.Deserialize<TeamResponse>(json, _options)!;

            return new TeamDetail
            {
                Id = team.Id,
                Nome = team.Name,
                Escudo = team.Crest,
                Elenco = team.Squad.Select(p => new Player
                {
                    Id = p.Id,
                    Nome = p.Name,
                    Posicao = p.Position,
                    Nacionalidade = p.Nationality,
                    Nascimento = p.DateOfBirth
                }).ToList()
            };
        }

        // Jogadores de um time
        public async Task<List<Jogador>> GetJogadoresAsync(int teamId)
        {
            var response = await _httpClient.GetAsync($"teams/{teamId}");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var team = JsonSerializer.Deserialize<TeamResponse>(json, _options)!;

            return team.Squad.Select(s => new Jogador
            {
                Id = s.Id,
                Nome = s.Name,
                Posicao = s.Position,
                DataNascimento = s.DateOfBirth ?? DateTime.MinValue,
                Nacionalidade = new Nacionalidade { Nome = s.Nationality },
                TimeId = teamId
            }).ToList();
        }
    }
}