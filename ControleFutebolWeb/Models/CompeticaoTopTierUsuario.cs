namespace ControleFutebolWeb.Models
{
    public class CompeticaoTopTierUsuario
    {
        public int Id { get; set; }

        public int CompeticaoId { get; set; }
        public Competicao Competicao { get; set; } = null!;

        public string UsuarioId { get; set; } = "";
        public ApplicationUser Usuario { get; set; } = null!;
    }
}
