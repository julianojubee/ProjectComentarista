using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ControleFutebolWeb.Models
{
    public class AnotacaoTime
    {
        public int Id { get; set; }

        public int TimeId { get; set; }
        [ValidateNever]
        public Time Time { get; set; } = null!;

        public string Titulo { get; set; } = string.Empty;
        public string Conteudo { get; set; } = string.Empty;

        public string? Categoria { get; set; } // ex.: "Sondagem", "Demissão", "Curiosidade"

        public DateTime DtInc { get; set; } = DateTime.UtcNow;
        public DateTime? DtAlt { get; set; }

        public string? UsuarioId { get; set; }
        public ApplicationUser? Usuario { get; set; }
    }
}
