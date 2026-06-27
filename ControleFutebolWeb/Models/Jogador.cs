using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ControleFutebolWeb.Models
{
    public class Jogador
    {
        public int Id { get; set; }

        public string Nome { get; set; } = string.Empty;
        public string? PrimeiroNome { get; set; }
        public string? UltimoNome { get; set; }

        public string NomeExibicao =>
            (!string.IsNullOrWhiteSpace(PrimeiroNome) && !string.IsNullOrWhiteSpace(UltimoNome))
                ? $"{PrimeiroNome} {UltimoNome}"
                : Nome;

        public string Posicao { get; set; } = string.Empty;

        // Data de Nascimento (null = não informada)
        public DateTime? DataNascimento { get; set; }

        // Idade calculada dinamicamente (baseada na DataNascimento)
        public int Idade
        {
            get
            {
                if (!DataNascimento.HasValue) return 0;
                var hoje = DateTime.Today;
                var idade = hoje.Year - DataNascimento.Value.Year;
                if (DataNascimento.Value.Date > hoje.AddYears(-idade)) idade--;
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

        // Seleção nacional (opcional) — permite que um jogador de clube
        // também seja utilizado em jogos de seleção sem criar cadastro duplicado.
        public int? SelecaoId { get; set; }
        [ValidateNever]
        public Time? Selecao { get; set; }

        public DateTime DtInc { get; set; }
        public DateTime? DtAlt { get; set; }
        public string? FotoUrl { get; set; }
        [Column("linktransfermarket")]
        public string? LinkTransfermarket { get; set; }
        public string? Observacoes { get; set; }

    }
}