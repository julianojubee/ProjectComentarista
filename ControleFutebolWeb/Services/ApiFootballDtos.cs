using System.Text.Json;
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

        [JsonPropertyName("score")]
        public AfScore Score { get; set; } = new();

        [JsonPropertyName("events")]
        public List<AfEvent> Events { get; set; } = new();

        [JsonPropertyName("lineups")]
        public List<AfLineup> Lineups { get; set; } = new();

        [JsonPropertyName("players")]
        public List<AfPlayerTeam> Players { get; set; } = new();

        [JsonPropertyName("statistics")]
        public List<AfTeamStatistics> Statistics { get; set; } = new();
    }

    // ── Estatísticas da partida (posse, finalizações, etc.) ──────────────────
    public class AfTeamStatistics
    {
        [JsonPropertyName("team")]
        public AfTeamRef Team { get; set; } = new();

        [JsonPropertyName("statistics")]
        public List<AfStatItem> Statistics { get; set; } = new();
    }

    public class AfStatItem
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("value")]
        [JsonConverter(typeof(AfStatValueConverter))]
        public string? Value { get; set; }
    }

    // O campo "value" vem como número, string ("48%") ou null — normaliza tudo para string.
    public class AfStatValueConverter : JsonConverter<string?>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return reader.TokenType switch
            {
                JsonTokenType.Null => null,
                JsonTokenType.String => reader.GetString(),
                JsonTokenType.Number => reader.TryGetInt64(out var l) ? l.ToString() : reader.GetDouble().ToString(),
                JsonTokenType.True => "true",
                JsonTokenType.False => "false",
                _ => null
            };
        }

        public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
            => writer.WriteStringValue(value);
    }

    public class AfFixtureInfo
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("referee")]
        public string? Referee { get; set; }

        [JsonPropertyName("date")]
        public DateTimeOffset? Date { get; set; }

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

    // ── Placar detalhado (intervalo, tempo normal, prorrogação, pênaltis) ─────
    public class AfScore
    {
        [JsonPropertyName("halftime")]
        public AfGoals Halftime { get; set; } = new();

        [JsonPropertyName("fulltime")]
        public AfGoals Fulltime { get; set; } = new();

        [JsonPropertyName("extratime")]
        public AfGoals Extratime { get; set; } = new();

        // Disputa de pênaltis — preenchido apenas em mata-mata decidido nas penalidades.
        [JsonPropertyName("penalty")]
        public AfGoals Penalty { get; set; } = new();
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

        // Comentário do lance. Usado para identificar disputa de pênaltis:
        // a api-football marca cada cobrança com comments "Penalty Shootout".
        [JsonPropertyName("comments")]
        public string? Comments { get; set; }
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

        [JsonPropertyName("logo")]
        public string? Logo { get; set; }

        [JsonPropertyName("colors")]
        public AfLineupColors? Colors { get; set; }
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

    public class AfLineupColors
    {
        [JsonPropertyName("player")]
        public AfLineupColorSet? Player { get; set; }

        [JsonPropertyName("goalkeeper")]
        public AfLineupColorSet? Goalkeeper { get; set; }
    }

    public class AfLineupColorSet
    {
        [JsonPropertyName("primary")]
        public string? Primary { get; set; }

        [JsonPropertyName("number")]
        public string? Number { get; set; }

        [JsonPropertyName("border")]
        public string? Border { get; set; }
    }

    public class AfCoach
    {
        [JsonPropertyName("id")]
        public int? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("photo")]
        public string? Photo { get; set; }
    }

    // ── DTO completo para GET /coachs?search=... ─────────────────────────────
    public class AfCoachFull
    {
        [JsonPropertyName("id")]          public int?    Id          { get; set; }
        [JsonPropertyName("name")]        public string? Name        { get; set; }
        [JsonPropertyName("firstname")]   public string? Firstname   { get; set; }
        [JsonPropertyName("lastname")]    public string? Lastname    { get; set; }
        [JsonPropertyName("age")]         public int?    Age         { get; set; }
        [JsonPropertyName("nationality")] public string? Nationality { get; set; }
        [JsonPropertyName("height")]      public string? Height      { get; set; }
        [JsonPropertyName("weight")]      public string? Weight      { get; set; }
        [JsonPropertyName("photo")]       public string? Photo       { get; set; }
        [JsonPropertyName("birth")]       public AfCoachBirth? Birth { get; set; }
        [JsonPropertyName("team")]        public AfTeamRef?    Team  { get; set; }

        // Histórico de passagens do treinador. O registro "stub" (ver ResolverTreinadorApiAsync
        // no ApiFootballService) costuma trazer só a passagem atual; o registro completo traz
        // todas as passagens, mas não necessariamente a mais recente.
        [JsonPropertyName("career")]      public List<AfCoachCareerItem>? Career { get; set; }
    }

    public class AfCoachBirth
    {
        [JsonPropertyName("date")]    public string? Date    { get; set; }
        [JsonPropertyName("place")]   public string? Place   { get; set; }
        [JsonPropertyName("country")] public string? Country { get; set; }
    }

    // Item de carreira do treinador ("career" em /coachs?search=...).
    // start/end vêm como string "yyyy-MM-dd"; end nulo = passagem atual.
    public class AfCoachCareerItem
    {
        [JsonPropertyName("team")]  public AfTeamRef? Team  { get; set; }
        [JsonPropertyName("start")] public string?     Start { get; set; }
        [JsonPropertyName("end")]   public string?     End   { get; set; }
    }

    public class AfLineupPlayer
    {
        [JsonPropertyName("player")]
        public AfLineupPlayerInfo Player { get; set; } = new();
    }

    public class AfLineupPlayerInfo
    {
        [JsonPropertyName("id")]
        public int? Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("number")]
        public int? Number { get; set; }

        [JsonPropertyName("pos")]
        public string? Pos { get; set; }

        [JsonPropertyName("grid")]
        public string? Grid { get; set; }
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

        [JsonPropertyName("offsides")]
        public int? Offsides { get; set; }

        [JsonPropertyName("shots")]
        public AfShots? Shots { get; set; }

        [JsonPropertyName("goals")]
        public AfPlayerGoals? Goals { get; set; }

        [JsonPropertyName("passes")]
        public AfPasses? Passes { get; set; }

        [JsonPropertyName("tackles")]
        public AfTackles? Tackles { get; set; }

        [JsonPropertyName("duels")]
        public AfDuels? Duels { get; set; }

        [JsonPropertyName("dribbles")]
        public AfDribbles? Dribbles { get; set; }

        [JsonPropertyName("fouls")]
        public AfFouls? Fouls { get; set; }

        [JsonPropertyName("cards")]
        public AfCards? Cards { get; set; }

        [JsonPropertyName("penalty")]
        public AfPenalty? Penalty { get; set; }
    }

    public class AfGames
    {
        [JsonPropertyName("minutes")]
        public int? Minutes { get; set; }

        [JsonPropertyName("position")]
        public string? Position { get; set; }

        [JsonPropertyName("rating")]
        public string? Rating { get; set; }

        [JsonPropertyName("substitute")]
        public bool Substitute { get; set; }
    }

    public class AfShots
    {
        [JsonPropertyName("total")]
        public int? Total { get; set; }

        [JsonPropertyName("on")]
        public int? On { get; set; }
    }

    public class AfPlayerGoals
    {
        [JsonPropertyName("total")]
        public int? Total { get; set; }

        [JsonPropertyName("conceded")]
        public int? Conceded { get; set; }

        [JsonPropertyName("assists")]
        public int? Assists { get; set; }

        [JsonPropertyName("saves")]
        public int? Saves { get; set; }
    }

    public class AfPasses
    {
        [JsonPropertyName("total")]
        public int? Total { get; set; }

        [JsonPropertyName("key")]
        public int? Key { get; set; }
    }

    public class AfTackles
    {
        [JsonPropertyName("total")]
        public int? Total { get; set; }

        [JsonPropertyName("blocks")]
        public int? Blocks { get; set; }

        [JsonPropertyName("interceptions")]
        public int? Interceptions { get; set; }
    }

    public class AfDuels
    {
        [JsonPropertyName("total")]
        public int? Total { get; set; }

        [JsonPropertyName("won")]
        public int? Won { get; set; }
    }

    public class AfDribbles
    {
        [JsonPropertyName("attempts")]
        public int? Attempts { get; set; }

        [JsonPropertyName("success")]
        public int? Success { get; set; }

        [JsonPropertyName("past")]
        public int? Past { get; set; }
    }

    public class AfFouls
    {
        [JsonPropertyName("drawn")]
        public int? Drawn { get; set; }

        [JsonPropertyName("committed")]
        public int? Committed { get; set; }
    }

    public class AfCards
    {
        [JsonPropertyName("yellow")]
        public int Yellow { get; set; }

        [JsonPropertyName("red")]
        public int Red { get; set; }
    }

    public class AfPenalty
    {
        [JsonPropertyName("won")]
        public int? Won { get; set; }

        [JsonPropertyName("commited")]
        public int? Commited { get; set; }

        [JsonPropertyName("scored")]
        public int? Scored { get; set; }

        [JsonPropertyName("missed")]
        public int? Missed { get; set; }

        [JsonPropertyName("saved")]
        public int? Saved { get; set; }
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

        [JsonPropertyName("firstname")]
        public string? Firstname { get; set; }

        [JsonPropertyName("lastname")]
        public string? Lastname { get; set; }

        [JsonPropertyName("age")]
        public int? Age { get; set; }

        [JsonPropertyName("photo")]
        public string? Photo { get; set; }

        [JsonPropertyName("nationality")]
        public string? Nationality { get; set; }

        [JsonPropertyName("height")]
        public string? Height { get; set; }

        [JsonPropertyName("weight")]
        public string? Weight { get; set; }

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
        public string? PrimeiroNome { get; set; }
        public string? UltimoNome { get; set; }
        public int? Altura { get; set; }
        public int? Peso { get; set; }
    }

    // ── /standings?league=X&season=Y ─────────────────────────────────────────
    public class AfStandingsEntry
    {
        [JsonPropertyName("league")]
        public AfStandingsLeague League { get; set; } = new();
    }

    public class AfStandingsLeague
    {
        [JsonPropertyName("standings")]
        public List<List<AfStandingsTeamEntry>> Standings { get; set; } = new();
    }

    public class AfStandingsTeamEntry
    {
        [JsonPropertyName("team")]
        public AfTeamRef Team { get; set; } = new();

        [JsonPropertyName("group")]
        public string? Group { get; set; }
    }

    // ── /players?id=X&season=Y — estatísticas por temporada ─────────────────
    public class AfPlayerSeasonEntry
    {
        [JsonPropertyName("player")]
        public AfPlayerProfileInfo Player { get; set; } = new();

        [JsonPropertyName("statistics")]
        public List<AfPlayerSeasonStats> Statistics { get; set; } = new();
    }

    public class AfPlayerSeasonStats
    {
        [JsonPropertyName("team")]
        public AfTeamRef Team { get; set; } = new();

        [JsonPropertyName("league")]
        public AfPlayerSeasonLeague League { get; set; } = new();

        [JsonPropertyName("games")]
        public AfSeasonGames Games { get; set; } = new();

        [JsonPropertyName("goals")]
        public AfSeasonGoals Goals { get; set; } = new();

        [JsonPropertyName("passes")]
        public AfSeasonPasses Passes { get; set; } = new();

        [JsonPropertyName("shots")]
        public AfSeasonShots Shots { get; set; } = new();

        [JsonPropertyName("tackles")]
        public AfSeasonTackles Tackles { get; set; } = new();

        [JsonPropertyName("dribbles")]
        public AfSeasonDribbles Dribbles { get; set; } = new();

        [JsonPropertyName("cards")]
        public AfSeasonCards Cards { get; set; } = new();

        [JsonPropertyName("substitutes")]
        public AfSeasonSubstitutes Substitutes { get; set; } = new();

        [JsonPropertyName("duels")]
        public AfSeasonDuels Duels { get; set; } = new();

        [JsonPropertyName("fouls")]
        public AfSeasonFouls Fouls { get; set; } = new();
    }

    public class AfPlayerSeasonLeague
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("country")]
        public string? Country { get; set; }

        [JsonPropertyName("logo")]
        public string? Logo { get; set; }

        [JsonPropertyName("flag")]
        public string? Flag { get; set; }

        [JsonPropertyName("season")]
        public int Season { get; set; }
    }

    public class AfSeasonGames
    {
        [JsonPropertyName("appearences")]
        public int? Appearences { get; set; }

        [JsonPropertyName("lineups")]
        public int? Lineups { get; set; }

        [JsonPropertyName("minutes")]
        public int? Minutes { get; set; }

        [JsonPropertyName("position")]
        public string? Position { get; set; }

        [JsonPropertyName("rating")]
        public string? Rating { get; set; }
    }

    public class AfSeasonGoals
    {
        [JsonPropertyName("total")]
        public int? Total { get; set; }

        [JsonPropertyName("assists")]
        public int? Assists { get; set; }
    }

    public class AfSeasonPasses
    {
        [JsonPropertyName("total")]
        public int? Total { get; set; }

        [JsonPropertyName("key")]
        public int? Key { get; set; }

        [JsonPropertyName("accuracy")]
        public int? Accuracy { get; set; }
    }

    public class AfSeasonShots
    {
        [JsonPropertyName("total")]
        public int? Total { get; set; }

        [JsonPropertyName("on")]
        public int? On { get; set; }
    }

    public class AfSeasonTackles
    {
        [JsonPropertyName("total")]
        public int? Total { get; set; }

        [JsonPropertyName("blocks")]
        public int? Blocks { get; set; }

        [JsonPropertyName("interceptions")]
        public int? Interceptions { get; set; }
    }

    public class AfSeasonDribbles
    {
        [JsonPropertyName("attempts")]
        public int? Attempts { get; set; }

        [JsonPropertyName("success")]
        public int? Success { get; set; }
    }

    public class AfSeasonCards
    {
        [JsonPropertyName("yellow")]
        public int? Yellow { get; set; }

        [JsonPropertyName("yellowred")]
        public int? Yellowred { get; set; }

        [JsonPropertyName("red")]
        public int? Red { get; set; }
    }

    public class AfSeasonSubstitutes
    {
        [JsonPropertyName("in")]
        public int? In { get; set; }

        [JsonPropertyName("out")]
        public int? Out { get; set; }

        [JsonPropertyName("bench")]
        public int? Bench { get; set; }
    }

    public class AfSeasonDuels
    {
        [JsonPropertyName("total")]
        public int? Total { get; set; }

        [JsonPropertyName("won")]
        public int? Won { get; set; }
    }

    public class AfSeasonFouls
    {
        [JsonPropertyName("drawn")]
        public int? Drawn { get; set; }

        [JsonPropertyName("committed")]
        public int? Committed { get; set; }
    }

    // ── /players/squads?team=X ───────────────────────────────────────────────
    public class AfSquadEntry
    {
        [JsonPropertyName("team")]
        public AfTeamRef Team { get; set; } = new();

        [JsonPropertyName("players")]
        public List<AfSquadPlayer> Players { get; set; } = new();
    }

    public class AfSquadPlayer
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("firstname")]
        public string? Firstname { get; set; }

        [JsonPropertyName("lastname")]
        public string? Lastname { get; set; }

        [JsonPropertyName("number")]
        public int? Number { get; set; }

        [JsonPropertyName("position")]
        public string? Position { get; set; }

        [JsonPropertyName("photo")]
        public string? Photo { get; set; }
    }

    // ── Wrapper para resposta única (teams/statistics, etc.) ─────────────────
    public class ApiFootballSingleResponse<T>
    {
        [JsonPropertyName("response")]
        public T? Response { get; set; }
    }

    // ── DTOs para teams/statistics ────────────────────────────────────────────
    public class AfTeamSeasonStats
    {
        [JsonPropertyName("league")]
        public AfTeamStatsLeague League { get; set; } = new();

        [JsonPropertyName("team")]
        public AfTeamRef Team { get; set; } = new();

        [JsonPropertyName("form")]
        public string? Form { get; set; }

        [JsonPropertyName("fixtures")]
        public AfFixturesStat Fixtures { get; set; } = new();

        [JsonPropertyName("goals")]
        public AfGoalsStat Goals { get; set; } = new();

        [JsonPropertyName("biggest")]
        public AfBiggest Biggest { get; set; } = new();

        [JsonPropertyName("clean_sheet")]
        public AfHomeAwayTotal CleanSheet { get; set; } = new();

        [JsonPropertyName("failed_to_score")]
        public AfHomeAwayTotal FailedToScore { get; set; } = new();

        [JsonPropertyName("penalty")]
        public AfPenaltyStat Penalty { get; set; } = new();

        [JsonPropertyName("lineups")]
        public List<AfLineupStat> Lineups { get; set; } = new();

        [JsonPropertyName("cards")]
        public AfCardsStat Cards { get; set; } = new();
    }

    public class AfTeamStatsLeague
    {
        [JsonPropertyName("id")]    public int Id { get; set; }
        [JsonPropertyName("name")]  public string Name { get; set; } = "";
        [JsonPropertyName("logo")]  public string? Logo { get; set; }
        [JsonPropertyName("season")] public int Season { get; set; }
    }

    public class AfFixturesStat
    {
        [JsonPropertyName("played")] public AfHomeAwayTotal Played { get; set; } = new();
        [JsonPropertyName("wins")]   public AfHomeAwayTotal Wins   { get; set; } = new();
        [JsonPropertyName("draws")]  public AfHomeAwayTotal Draws  { get; set; } = new();
        [JsonPropertyName("loses")]  public AfHomeAwayTotal Loses  { get; set; } = new();
    }

    public class AfHomeAwayTotal
    {
        [JsonPropertyName("home")]  public int Home  { get; set; }
        [JsonPropertyName("away")]  public int Away  { get; set; }
        [JsonPropertyName("total")] public int Total { get; set; }
    }

    public class AfGoalsStat
    {
        [JsonPropertyName("for")]     public AfGoalSide For     { get; set; } = new();
        [JsonPropertyName("against")] public AfGoalSide Against { get; set; } = new();
    }

    public class AfGoalSide
    {
        [JsonPropertyName("total")]   public AfHomeAwayTotal Total   { get; set; } = new();
        [JsonPropertyName("average")] public AfGoalAverage   Average { get; set; } = new();
        [JsonPropertyName("minute")]  public Dictionary<string, AfMinuteStat?> Minute { get; set; } = new();
    }

    public class AfGoalAverage
    {
        [JsonPropertyName("home")]  public string Home  { get; set; } = "0";
        [JsonPropertyName("away")]  public string Away  { get; set; } = "0";
        [JsonPropertyName("total")] public string Total { get; set; } = "0";
    }

    public class AfMinuteStat
    {
        [JsonPropertyName("total")]      public int?    Total      { get; set; }
        [JsonPropertyName("percentage")] public string? Percentage { get; set; }
    }

    public class AfBiggest
    {
        [JsonPropertyName("streak")] public AfStreak    Streak { get; set; } = new();
        [JsonPropertyName("wins")]   public AfResultStr Wins   { get; set; } = new();
        [JsonPropertyName("loses")]  public AfResultStr Loses  { get; set; } = new();
        [JsonPropertyName("goals")]  public AfBiggestGoals Goals { get; set; } = new();
    }

    public class AfStreak
    {
        [JsonPropertyName("wins")]  public int Wins  { get; set; }
        [JsonPropertyName("draws")] public int Draws { get; set; }
        [JsonPropertyName("loses")] public int Loses { get; set; }
    }

    public class AfResultStr
    {
        [JsonPropertyName("home")] public string? Home { get; set; }
        [JsonPropertyName("away")] public string? Away { get; set; }
    }

    public class AfBiggestGoals
    {
        [JsonPropertyName("for")]     public AfHomeAwayGoals For     { get; set; } = new();
        [JsonPropertyName("against")] public AfHomeAwayGoals Against { get; set; } = new();
    }

    public class AfHomeAwayGoals
    {
        [JsonPropertyName("home")] public int? Home { get; set; }
        [JsonPropertyName("away")] public int? Away { get; set; }
    }

    public class AfPenaltyStat
    {
        [JsonPropertyName("scored")] public AfPenaltyCount Scored { get; set; } = new();
        [JsonPropertyName("missed")] public AfPenaltyCount Missed { get; set; } = new();
        [JsonPropertyName("total")]  public int Total { get; set; }
    }

    public class AfPenaltyCount
    {
        [JsonPropertyName("total")]      public int    Total      { get; set; }
        [JsonPropertyName("percentage")] public string Percentage { get; set; } = "0%";
    }

    public class AfLineupStat
    {
        [JsonPropertyName("formation")] public string Formation { get; set; } = "";
        [JsonPropertyName("played")]    public int    Played    { get; set; }
    }

    public class AfCardsStat
    {
        [JsonPropertyName("yellow")] public Dictionary<string, AfMinuteStat?> Yellow { get; set; } = new();
        [JsonPropertyName("red")]    public Dictionary<string, AfMinuteStat?> Red    { get; set; } = new();
    }
}
