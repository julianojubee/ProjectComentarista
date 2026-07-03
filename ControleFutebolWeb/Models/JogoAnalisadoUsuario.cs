namespace ControleFutebolWeb.Models
{
    public class JogoAnalisadoUsuario
    {
        public int Id { get; set; }

        public int JogoId { get; set; }
        public Jogo Jogo { get; set; } = null!;

        public string UsuarioId { get; set; } = "";
        public ApplicationUser Usuario { get; set; } = null!;

        // Observações por time da escalação final (serializadas com tags CASA/VISITANTE).
        public string? Observacoes { get; set; }

        // Marcador explícito de "analisado" (checkbox da tela Analisar). Separado da
        // existência da linha: a linha também é criada implicitamente ao salvar a
        // escalação final (para guardar Observacoes), então "linha existe" deixou de
        // significar "está marcado como analisado" — ver comentário em
        // JogosController.MarcarAnalisado.
        public bool Analisado { get; set; } = true;
    }
}
