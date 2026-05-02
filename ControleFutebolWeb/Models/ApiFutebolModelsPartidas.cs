namespace ControleFutebolWeb.Models
{ 
    public class TeamsResponse
    {
        public List<TeamResponse> Teams { get; set; } = new();
    }

    public class TeamResponse
    {
        public int? Id { get; set; }  // ← int?
        public string Name { get; set; } = string.Empty;
        public string Crest { get; set; } = string.Empty;
        public List<PlayerResponse> Squad { get; set; } = new();
    }

    public class PlayerResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public string Nationality { get; set; } = string.Empty;
        public DateTime? DateOfBirth { get; set; }
    }

    public class MatchesResponse
    {
        public List<MatchResponse> Matches { get; set; } = new();
    }

    public class MatchResponse
    {
        public int Id { get; set; }
        public DateTime UtcDate { get; set; }
        public int? Matchday { get; set; }  // ← era int, agora int?
        public string? Group { get; set; }
        public TeamResponse HomeTeam { get; set; } = new();
        public TeamResponse AwayTeam { get; set; } = new();
        public ScoreResponse Score { get; set; } = new();
    }

    public class ScoreResponse
    {
        public FullTimeScore FullTime { get; set; } = new();
    }

    public class FullTimeScore
    {
        public int? Home { get; set; }
        public int? Away { get; set; }
    }

    public class TeamDetail
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Escudo { get; set; } = string.Empty;
        public List<Player> Elenco { get; set; } = new();
    }

    public class Player
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Posicao { get; set; } = string.Empty;
        public string Nacionalidade { get; set; } = string.Empty;
        public DateTime? Nascimento { get; set; }
    }
    public class Partida
    {
        public int Partida_Id { get; set; }
        public int Rodada { get; set; }   // novo campo

        public ClubeResumo Time_Mandante { get; set; } = new();
        public ClubeResumo Time_Visitante { get; set; } = new();

        public int? Placar_Mandante { get; set; }
        public int? Placar_Visitante { get; set; }

        public string Data_Realizacao { get; set; } = string.Empty;
        public string Hora_Realizacao { get; set; } = string.Empty;
    }

    public class ClubeResumo
    {
        public int Time_Id { get; set; }
        public string Nome_Popular { get; set; } = string.Empty;
        public string Escudo { get; set; } = string.Empty;
    }
    public class ClubeInfo
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Escudo { get; set; } = string.Empty;
    }

}