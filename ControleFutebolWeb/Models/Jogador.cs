using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ControleFutebolWeb.Models
{
    public class Jogador
    {
        public int Id { get; set; }

        public string Nome { get; set; } = string.Empty;
        public string Posicao { get; set; } = string.Empty;

        // Novo campo: Data de Nascimento
        public DateTime DataNascimento { get; set; }

        // Idade calculada dinamicamente
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

        public int? NumeroCamisa { get; set; }

        public int? NacionalidadeId { get; set; }
        public Nacionalidade? Nacionalidade { get; set; }
        [ValidateNever]   // <- evita erro de ModelState

        public int TimeId { get; set; }
        [ValidateNever]   // <- evita erro de ModelState

        public Time Time { get; set; } = null!;
    }
}