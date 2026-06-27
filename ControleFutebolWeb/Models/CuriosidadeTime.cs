using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ControleFutebolWeb.Models
{
    public class CuriosidadeTime
    {
        public int Id { get; set; }

        [Required]
        public int TimeId { get; set; }

        [ForeignKey("TimeId")]
        public Time? Time { get; set; }

        [Required]
        public string Conteudo { get; set; } = string.Empty;

        public DateTime DataBusca { get; set; } = DateTime.UtcNow;
    }
}
