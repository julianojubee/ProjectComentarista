namespace ControleFutebolWeb.Models.ViewModels
{
    public class PagamentosIndexViewModel
    {
        public List<UsuarioPagamentoViewModel> Usuarios { get; set; } = new();
        public List<PagamentoUsuario> Lancamentos { get; set; } = new();
    }

    public class UsuarioPagamentoViewModel
    {
        public ApplicationUser Usuario { get; set; } = null!;
        public PagamentoUsuario? UltimoPagamento { get; set; }
        public decimal TotalPago { get; set; }
    }
}
