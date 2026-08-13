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

    // Configuração extra de critérios que não são "quantidade × peso".
    // Hoje só o "sem_sofrer_gol" usa: as posições que recebem o bônus,
    // separadas por ';' (ex.: "GOLEIRO;ZAGUEIRO;LATERAL;ALA").
    public string? Config { get; set; }

    public string? UsuarioId { get; set; }
    public ApplicationUser? Usuario { get; set; }
}
