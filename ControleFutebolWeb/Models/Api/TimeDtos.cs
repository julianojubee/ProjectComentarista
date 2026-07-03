namespace ControleFutebolWeb.Models.Api
{
    public class TimeResumoDto
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string? Cidade { get; set; }
        public string? EscudoUrl { get; set; }
        public bool EhSelecao { get; set; }
    }

    public class TimeDetalheDto : TimeResumoDto
    {
        public string? BackgroundUrl { get; set; }
        public string? CorPrincipal { get; set; }
        public string? CorSecundaria { get; set; }
        public string? CamisaUrl { get; set; }
        public string? CamisaVisitanteUrl { get; set; }
        public string? LinkTransfermarket { get; set; }
    }
}
