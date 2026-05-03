using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ControleFutebolWeb.Models
{
    public class Jogador
    {
        public int Id { get; set; }

        public string Nome { get; set; } = string.Empty;
        public string Posicao { get; set; } = string.Empty;

        // Data de Nascimento
        public DateTime DataNascimento { get; set; }

        // Idade calculada dinamicamente (baseada na DataNascimento)
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

        // 🔹 Novo campo: Idade extraída do Transfermarkt
        public int? IdadeTransfermarkt { get; set; }

        // 🔹 Novo campo: Flag para indicar se já foi atualizado
        public bool Atualizado { get; set; } = false;

        // 🔹 Novo campo: ID de origem da API/JSON

        public long? IdApi { get; set; }

        public int? NumeroCamisa { get; set; }

        public int? NacionalidadeId { get; set; }
        public Nacionalidade? Nacionalidade { get; set; }
        [ValidateNever]   // <- evita erro de ModelState

        public int TimeId { get; set; }
        [ValidateNever]   // <- evita erro de ModelState

        public Time Time { get; set; } = null!;

        // 🔹 Novos campos de controle
        public DateTime DtInc { get; set; }   // Data de inclusão
        public DateTime? DtAlt { get; set; }   // Data da última alteração
    }
}