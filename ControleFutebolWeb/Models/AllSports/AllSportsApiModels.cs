using System.Text.Json;
using System.Text.Json.Serialization;

namespace ControleFutebolWeb.Models.AllSports
{
    // Raiz: o arquivo pode ser um array direto OU { "result": [...] }
    public class AllSportsResponse
    {
        [JsonPropertyName("result")]
        public List<AllSportsJogo> Result { get; set; } = new();
    }

    public class AllSportsJogo
    {
        [JsonPropertyName("event_key")]
        [JsonConverter(typeof(ControleFutebolWeb.Converters.NullableLongConverter))]
        public long? EventKey { get; set; }

        [JsonPropertyName("event_date")]
        public string EventDate { get; set; } = "";

        [JsonPropertyName("event_time")]
        public string EventTime { get; set; } = "";

        [JsonPropertyName("event_home_team")]
        public string HomeTeam { get; set; } = "";

        [JsonPropertyName("home_team_key")]
        [JsonConverter(typeof(ControleFutebolWeb.Converters.NullableLongConverter))]
        public long? HomeTeamKey { get; set; }

        [JsonPropertyName("event_away_team")]
        public string AwayTeam { get; set; } = "";

        [JsonPropertyName("away_team_key")]
        [JsonConverter(typeof(ControleFutebolWeb.Converters.NullableLongConverter))]
        public long? AwayTeamKey { get; set; }

        [JsonPropertyName("event_halftime_result")]
        public string? HalftimeResult { get; set; }

        [JsonPropertyName("event_final_result")]
        public string? FinalResult { get; set; }

        [JsonPropertyName("event_penalty_result")]
        public string? PenaltyResult { get; set; }

        [JsonPropertyName("event_status")]
        public string? EventStatus { get; set; }

        [JsonPropertyName("league_name")]
        public string? LeagueName { get; set; }

        [JsonPropertyName("league_round")]
        public string? LeagueRound { get; set; }

        [JsonPropertyName("home_team_logo")]
        public string? HomeTeamLogo { get; set; }

        [JsonPropertyName("away_team_logo")]
        public string? AwayTeamLogo { get; set; }

        [JsonPropertyName("event_home_formation")]
        public string? HomeFormation { get; set; }

        [JsonPropertyName("event_away_formation")]
        public string? AwayFormation { get; set; }

        [JsonPropertyName("goalscorers")]
        public List<AllSportsGol> Goalscorers { get; set; } = new();

        [JsonPropertyName("substitutes")]
        public List<AllSportsSubstitute> Substitutes { get; set; } = new();

        [JsonPropertyName("cards")]
        public List<AllSportsCard> Cards { get; set; } = new();

        [JsonPropertyName("lineups")]
        public AllSportsLineups? Lineups { get; set; }

        [JsonPropertyName("statistics")]
        public List<AllSportsStat> Statistics { get; set; } = new();
    }

    public class AllSportsGol
    {
        [JsonPropertyName("time")]
        public string Time { get; set; } = "";

        [JsonPropertyName("home_scorer")]
        public string? HomeScorer { get; set; }

        [JsonPropertyName("home_scorer_id")]
        [JsonConverter(typeof(ControleFutebolWeb.Converters.NullableLongConverter))]
        public long? HomeScorerKey { get; set; }

        [JsonPropertyName("away_scorer")]
        public string? AwayScorer { get; set; }

        [JsonPropertyName("away_scorer_id")]
        [JsonConverter(typeof(ControleFutebolWeb.Converters.NullableLongConverter))]
        public long? AwayScorerKey { get; set; }

        [JsonPropertyName("score")]
        public string? Score { get; set; }

        [JsonPropertyName("info")]
        public string? Info { get; set; }

        [JsonPropertyName("info_time")]
        public string? InfoTime { get; set; }

        // 🔹 Propriedade auxiliar para facilitar
        [JsonIgnore]
        public long? PlayerKey => HomeScorerKey ?? AwayScorerKey;
    }


    public class AllSportsCard
    {
        [JsonPropertyName("time")]
        public string Time { get; set; } = "";

        [JsonPropertyName("home_fault")]
        public string? HomeFault { get; set; }

        [JsonPropertyName("away_fault")]
        public string? AwayFault { get; set; }

        [JsonPropertyName("card")]
        public string CardType { get; set; } = "";


        [JsonPropertyName("home_player_id")]
        [JsonConverter(typeof(ControleFutebolWeb.Converters.NullableLongConverter))]
        public long? HomePlayerId { get; set; }

        [JsonPropertyName("away_player_id")]
        [JsonConverter(typeof(ControleFutebolWeb.Converters.NullableLongConverter))]
        public long? AwayPlayerId { get; set; }

        // 🔹 Propriedade auxiliar
        [JsonIgnore]
        public long? PlayerKey => HomePlayerId ?? AwayPlayerId;
    }


    public class AllSportsSubstitute
    {
        [JsonPropertyName("time")]
        public string Time { get; set; } = "";

        [JsonPropertyName("home_scorer")]
        [JsonConverter(typeof(SubPlayerConverter))]
        public AllSportsSubPlayer? HomeScorer { get; set; }

        [JsonPropertyName("away_scorer")]
        [JsonConverter(typeof(SubPlayerConverter))]
        public AllSportsSubPlayer? AwayScorer { get; set; }

        [JsonPropertyName("score")]
        public string? Score { get; set; }

        [JsonPropertyName("home_assist")]
        public string? HomeAssist { get; set; }

        [JsonPropertyName("away_assist")]
        public string? AwayAssist { get; set; }

        [JsonPropertyName("info")]
        public string? Info { get; set; }

        [JsonPropertyName("info_time")]
        public string? InfoTime { get; set; }
    }

    public class AllSportsSubPlayer
    {
        [JsonPropertyName("in")]
        public string? In { get; set; }

        [JsonPropertyName("out")]
        public string? Out { get; set; }

        [JsonPropertyName("in_id")]
        [JsonConverter(typeof(ControleFutebolWeb.Converters.NullableLongConverter))]
        public long? InId { get; set; }

        [JsonPropertyName("out_id")]
        [JsonConverter(typeof(ControleFutebolWeb.Converters.NullableLongConverter))]
        public long? OutId { get; set; }
    }

    // 🔹 Converter embutido no mesmo namespace
    public class SubPlayerConverter : JsonConverter<AllSportsSubPlayer?>
    {
        public override AllSportsSubPlayer? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var nome = reader.GetString();
                return new AllSportsSubPlayer { In = nome };
            }
            else if (reader.TokenType == JsonTokenType.StartObject)
            {
                return JsonSerializer.Deserialize<AllSportsSubPlayer>(ref reader, options);
            }
            else if (reader.TokenType == JsonTokenType.StartArray)
            {
                // Consome array vazio []
                reader.Read();
                return null;
            }
            return null;
        }

        public override void Write(Utf8JsonWriter writer, AllSportsSubPlayer? value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value, options);
        }
    }

    public class AllSportsLineups
    {
        [JsonPropertyName("home_team")]
        public AllSportsTeamLineup? HomeTeam { get; set; }

        [JsonPropertyName("away_team")]
        public AllSportsTeamLineup? AwayTeam { get; set; }
    }

    public class AllSportsTeamLineup
    {
        [JsonPropertyName("starting_lineups")]
        public List<AllSportsPlayer> StartingLineups { get; set; } = new();

        [JsonPropertyName("substitutes")]
        public List<AllSportsPlayer> Substitutes { get; set; } = new();
    }

    public class AllSportsPlayer
    {
        [JsonPropertyName("player")]
        public string? Name { get; set; } = "";

        [JsonPropertyName("player_number")]
        public int? Number { get; set; }

        [JsonPropertyName("player_position")]
        public int? Position { get; set; }

        [JsonPropertyName("player_key")]
        public long? PlayerKey { get; set; }
    }

    public class AllSportsStat
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("home")]
        public string? Home { get; set; }

        [JsonPropertyName("away")]
        public string? Away { get; set; }
    }
}