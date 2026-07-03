namespace ControleFutebolWeb.Models.Api
{
    public class JogadorResumoDto
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string NomeExibicao { get; set; } = string.Empty;
        public string Posicao { get; set; } = string.Empty;
        public int Idade { get; set; }
        public int? NumeroCamisa { get; set; }
        public string? NacionalidadeNome { get; set; }
        public int TimeId { get; set; }
        public string? TimeNome { get; set; }
        public string? FotoUrl { get; set; }
    }

    public class JogadorDetalheDto : JogadorResumoDto
    {
        public DateTime? DataNascimento { get; set; }
        public int? SelecaoId { get; set; }
        public string? SelecaoNome { get; set; }
        public string? LinkTransfermarket { get; set; }
        public string? Observacoes { get; set; }
    }
}
