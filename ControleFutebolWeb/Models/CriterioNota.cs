using System.ComponentModel.DataAnnotations.Schema;

namespace ControleFutebolWeb.Models;

[Table("criterionotas")]
public class CriterioNota
{
    public int Id { get; set; }
    public string AcaoId { get; set; } = "";
    public string Label { get; set; } = "";
    public double Peso { get; set; }
    public bool Ativo { get; set; } = true;
    public int Ordem { get; set; } = 0;

    public string? UsuarioId { get; set; }
    public ApplicationUser? Usuario { get; set; }
}
