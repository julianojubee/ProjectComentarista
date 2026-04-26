namespace ControleFutebolWeb.ser.vices.responses
{
    public class TeamResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Crest { get; set; } = string.Empty;

        public List<SquadPlayer> Squad { get; set; } = new();
    }

    public class SquadPlayer
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public string Nationality { get; set; } = string.Empty;
        public DateTime? DateOfBirth { get; set; }
        public int? ShirtNumber { get; set; }
    }
}