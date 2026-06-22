namespace ControleFutebolWeb.Models
{
    public class JogoAnalisadoUsuario
    {
        public int Id { get; set; }

        public int JogoId { get; set; }
        public Jogo Jogo { get; set; } = null!;

        public string UsuarioId { get; set; } = "";
        public ApplicationUser Usuario { get; set; } = null!;
    }
}
