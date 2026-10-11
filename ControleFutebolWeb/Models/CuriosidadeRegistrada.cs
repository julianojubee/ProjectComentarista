namespace ControleFutebolWeb.Models
{
    // Registro de que uma curiosidade do pós-jogo (CuriosidadesJogoHelper) já foi
    // gravada como anotação no perfil do jogador/clube desse usuário.
    //
    // Fica numa tabela à parte, e não como coluna na anotação, porque precisa
    // sobreviver à exclusão dela: se o usuário apaga a anotação, reabrir o
    // pós-jogo não pode recriá-la.
    public class CuriosidadeRegistrada
    {
        public int Id { get; set; }

        public int JogoId { get; set; }
        public Jogo Jogo { get; set; } = null!;

        public string UsuarioId { get; set; } = "";
        public ApplicationUser Usuario { get; set; } = null!;

        // CuriosidadesJogoHelper.Curiosidade.Chave (ex.: "desarme:123", "goleada").
        public string Chave { get; set; } = "";

        public DateTime DtInc { get; set; } = DateTime.UtcNow;
    }
}
