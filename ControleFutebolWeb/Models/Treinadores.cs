using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ControleFutebolWeb.Models
{
    public class Treinador
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;

        public DateTime DataNascimento { get; set; }

        public int Idade
        {
            get
            {
                var hoje = DateTime.Today;
                var idade = hoje.Year - DataNascimento.Year;
                if (DataNascimento.Date > hoje.AddYears(-idade)) idade--;
                return idade;
            }
        }
        public int? NacionalidadeId { get; set; }
        public Nacionalidade? Nacionalidade { get; set; }
        [ValidateNever]   // <- evita erro de ModelState
        // Time atual
        public int TimeId { get; set; }
        [ValidateNever]
        public Time Time { get; set; } = null!;

        // Histórico de times anteriores
        public ICollection<TreinadorHistorico> Historicos { get; set; } = new List<TreinadorHistorico>();

        public DateTime DtInc { get; set; }
        public DateTime? DtAlt { get; set; }
        public string? FotoUrl { get; set; }
    }

}