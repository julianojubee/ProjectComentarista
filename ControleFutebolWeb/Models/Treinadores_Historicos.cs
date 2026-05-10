namespace ControleFutebolWeb.Models
{
    public class TreinadorHistorico
    {
        public int Id { get; set; }

        // FK para Treinador
        public int TreinadorId { get; set; }
        public Treinador Treinador { get; set; } = null!;

        // FK para Time
        public int TimeId { get; set; }
        public Time Time { get; set; } = null!;

        public DateTime DtInicio { get; set; }
        public DateTime? DtFim { get; set; }

        // Dias calculados dinamicamente
        public int Dias
        {
            get
            {
                var fim = DtFim ?? DateTime.Today;
                return (fim - DtInicio).Days;
            }
        }
    }


}
