namespace ControleFutebolWeb.Models
{
    // Observação livre sobre um jogo, categorizada por tag (mandante/visitante/
    // competição/jogador/marco). Substitui o antigo esquema de texto serializado
    // com "[CASA]/[VISITANTE]" que ficava em JogoAnalisadoUsuario.Observacoes.
    public class ObservacaoJogoTag
    {
        public int Id { get; set; }

        public int JogoId { get; set; }
        public Jogo Jogo { get; set; } = null!;

        public string UsuarioId { get; set; } = "";
        public ApplicationUser Usuario { get; set; } = null!;

        // MANDANTE | VISITANTE | COMPETICAO | JOGADOR | MARCO
        public string Tipo { get; set; } = "";

        // Só preenchido quando Tipo == JOGADOR — usado para mostrar a observação
        // também na página de Estatísticas do jogador, junto ao jogo correspondente.
        public int? JogadorId { get; set; }
        public Jogador? Jogador { get; set; }

        public string Texto { get; set; } = "";

        public int Ordem { get; set; }
    }
}
