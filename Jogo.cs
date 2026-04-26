namespace ControleFutebolWeb.Models
{
    public class Jogo
    {
        public int Id { get; set; }
        public DateTime Data { get; set; }
        public int Rodada { get; set; }

        public int TimeCasaId { get; set; }
        public Time? TimeCasa { get; set; }
        public int PlacarCasa { get; set; }

        public int TimeForaId { get; set; }
        public Time? TimeFora { get; set; }
        public int PlacarFora { get; set; }
    }
}