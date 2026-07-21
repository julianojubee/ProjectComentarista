namespace ControleFutebolWeb.Models
{
    // Lançamento manual de pagamento (PIX etc.) feito pelo admin na tela
    // /Pagamentos. Cada pagamento estende ApplicationUser.AcessoPagoAte;
    // PagoAte guarda o vencimento resultante deste lançamento (snapshot),
    // usado para reverter o vencimento se o lançamento for excluído.
    public class PagamentoUsuario
    {
        public int Id { get; set; }

        public string UsuarioId { get; set; } = "";
        public ApplicationUser Usuario { get; set; } = null!;

        // Datas "puras" ancoradas ao meio-dia UTC (ver convenção do projeto).
        public DateTime DataPagamento { get; set; }
        public DateTime PagoAte { get; set; }

        public decimal Valor { get; set; }
        public string? Observacao { get; set; }

        // Quando o admin registrou o lançamento (auditoria).
        public DateTime DataRegistro { get; set; }
    }
}
