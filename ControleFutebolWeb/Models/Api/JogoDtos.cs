namespace ControleFutebolWeb.Models.Api
{
    public class JogoResumoDto
    {
        public int Id { get; set; }
        public DateTime? Data { get; set; }
        public int CompeticaoId { get; set; }
        public string? CompeticaoNome { get; set; }
        public int TimeCasaId { get; set; }
        public string TimeCasaNome { get; set; } = string.Empty;
        public string? TimeCasaEscudoUrl { get; set; }
        public int TimeVisitanteId { get; set; }
        public string TimeVisitanteNome { get; set; } = string.Empty;
        public string? TimeVisitanteEscudoUrl { get; set; }
        public int? PlacarCasa { get; set; }
        public int? PlacarVisitante { get; set; }
        public string? Status { get; set; }
    }

    public class JogoDetalheDto : JogoResumoDto
    {
        public string? Estadio { get; set; }
        public string? Arbitro { get; set; }
        public int? PenaltisCasa { get; set; }
        public int? PenaltisVisitante { get; set; }
    }
}
