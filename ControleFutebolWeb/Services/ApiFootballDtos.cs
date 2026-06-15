using System.Text.Json.Serialization;

namespace ControleFutebolWeb.Services
{
    // ── Wrapper genérico da api-football ──────────────────────────────────────
    public class ApiFootballResponse<T>
    {
        [JsonPropertyName("response")]
        public List<T> Response { get; set; } = new();
    }

    // ── Fixture básico (lista por liga/temporada) ─────────────────────────────
    public class AfFixture
    {
        [JsonPropertyName("fixture")]
        public AfFixtureInfo Fixture { get; set; } = new();

        [JsonPropertyName("league")]
        public AfLeague League { get; set; } = new();

        [JsonPropertyName("teams")]
        public AfTeams Teams { get; set; } = new();

        [JsonPropertyName("goals")]
        public AfGoals Goals { get; set; } = new();

        [JsonPropertyName("events")]
        public List<AfEvent> Events { get; set; } = new();

        [JsonPropertyName("lineups")]
        public List<AfLineup> Lineups { get; set; } = new();

        [JsonPropertyName("players")]
        public List<AfPlayerTeam> Players { get; set; } = new();
    }

    public class AfFixtureInfo
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("referee")]
        public string? Referee { get; set; }

        [JsonPropertyName("date")]
        public DateTime? Date { get; set; }

        [JsonPropertyName("venue")]
        public AfVenue? Venue { get; set; }

        [JsonPropertyName("status")]
        public AfStatus Status { get; set; } = new();
    }

    public class AfVenue
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    public class AfStatus
    {
        [JsonPropertyName("short")]
        public string Short { get; set; } = "";
    }

    public class AfLeague
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("season")]
        public int Season { get; set; }

        [JsonPropertyName("round")]
        public string Round { get; set; } = "";

        [JsonPropertyName("group")]
        public string? Group { get; set; }
    }

    public class AfTeams
    {
        [JsonPropertyName("home")]
        public AfTeam Home { get; set; } = new();

        [JsonPropertyName("away")]
        public AfTeam Away { get; set; } = new();
    }

    public class AfTeam
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("logo")]
        public string? Logo { get; set; }

        [JsonPropertyName("winner")]
        public bool? Winner { get; set; }
    }

    public class AfGoals
    {
        [JsonPropertyName("home")]
        public int? Home { get; set; }

        [JsonPropertyName("away")]
        public int? Away { get; set; }
    }

    // ── Evento (gol, cartão, substituição) ───────────────────────────────────
    public class AfEvent
    {
        [JsonPropertyName("time")]
        public AfEventTime Time { get; set; } = new();

        [JsonPropertyName("team")]
        public AfTeamRef Team { get; set; } = new();

        [JsonPropertyName("player")]
        public AfPlayerRef Player { get; set; } = new();

        [JsonPropertyName("assist")]
        public AfPlayerRef? Assist { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("detail")]
        public string Detail { get; set; } = "";
    }

    public class AfEventTime
    {
        [JsonPropertyName("elapsed")]
        public int Elapsed { get; set; }
    }

    public class AfTeamRef
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
    }

    public class AfPlayerRef
    {
        [JsonPropertyName("id")]
        public int? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    // ── Lineup ────────────────────────────────────────────────────────────────
    public class AfLineup
    {
        [JsonPropertyName("team")]
        public AfTeamRef Team { get; set; } = new();

        [JsonPropertyName("formation")]
        public string? Formation { get; set; }

        [JsonPropertyName("coach")]
        public AfCoach? Coach { get; set; }

        [JsonPropertyName("startXI")]
        public List<AfLineupPlayer> StartXI { get; set; } = new();

        [JsonPropertyName("substitutes")]
        public List<AfLineupPlayer> Substitutes { get; set; } = new();
    }

    public class AfCoach
    {
        [JsonPropertyName("id")]
        public int? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    public class AfLineupPlayer
    {
        [JsonPropertyName("player")]
        public AfLineupPlayerInfo Player { get; set; } = new();
    }

    public class AfLineupPlayerInfo
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("number")]
        public int? Number { get; set; }

        [JsonPropertyName("pos")]
        public string? Pos { get; set; }
    }

    // ── Players (estatísticas individuais) ───────────────────────────────────
    public class AfPlayerTeam
    {
        [JsonPropertyName("team")]
        public AfTeamRef Team { get; set; } = new();

        [JsonPropertyName("players")]
        public List<AfPlayerStats> Players { get; set; } = new();
    }

    public class AfPlayerStats
    {
        [JsonPropertyName("player")]
        public AfPlayerInfo Player { get; set; } = new();

        [JsonPropertyName("statistics")]
        public List<AfPlayerStatEntry> Statistics { get; set; } = new();
    }

    public class AfPlayerInfo
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("photo")]
        public string? Photo { get; set; }
    }

    public class AfPlayerStatEntry
    {
        [JsonPropertyName("games")]
        public AfGames? Games { get; set; }
    }

    public class AfGames
    {
        [JsonPropertyName("minutes")]
        public int? Minutes { get; set; }

        [JsonPropertyName("position")]
        public string? Position { get; set; }

        [JsonPropertyName("substitute")]
        public bool Substitute { get; set; }
    }

    // ── /players?id=X&season=Y ────────────────────────────────────────────────
    public class AfPlayerProfileEntry
    {
        [JsonPropertyName("player")]
        public AfPlayerProfileInfo Player { get; set; } = new();
    }

    public class AfPlayerProfileInfo
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("photo")]
        public string? Photo { get; set; }

        [JsonPropertyName("nationality")]
        public string? Nationality { get; set; }

        [JsonPropertyName("birth")]
        public AfPlayerBirth? Birth { get; set; }
    }

    public class AfPlayerBirth
    {
        [JsonPropertyName("date")]
        public string? Date { get; set; }
    }

    // Result returned by BuscarInfoJogadorAsync
    public class AfPlayerInfoResult
    {
        public DateTime? DataNascimento { get; set; }
        public string? Nacionalidade { get; set; }
        public string? FotoUrl { get; set; }
    }
}
