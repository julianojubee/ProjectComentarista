namespace ControleFutebolWeb.Models
{
    // Observações livres sobre um jogo (público, curiosidades, contexto etc.), por usuário.
    // Desacoplada de JogoAnalisadoUsuario de propósito: escrever uma observação não deve
    // marcar o jogo como analisado, nem desmarcar "analisado" deve apagar a observação.
    public class ObservacaoJogoUsuario
    {
        public int Id { get; set; }

        public int JogoId { get; set; }
        public Jogo Jogo { get; set; } = null!;

        public string UsuarioId { get; set; } = "";
        public ApplicationUser Usuario { get; set; } = null!;

        public string? Texto { get; set; }

        public DateTime DtAlt { get; set; }
    }
}
