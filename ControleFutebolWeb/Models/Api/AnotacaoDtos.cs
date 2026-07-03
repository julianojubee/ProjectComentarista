namespace ControleFutebolWeb.Models.Api
{
    public class AnotacaoTimeDto
    {
        public int Id { get; set; }
        public int TimeId { get; set; }
        public string? TimeNome { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string Conteudo { get; set; } = string.Empty;
        public string? Categoria { get; set; }
        public DateTime DtInc { get; set; }
        public DateTime? DtAlt { get; set; }
    }

    public class AnotacaoTimeInput
    {
        public int TimeId { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string Conteudo { get; set; } = string.Empty;
        public string? Categoria { get; set; }
    }
}
