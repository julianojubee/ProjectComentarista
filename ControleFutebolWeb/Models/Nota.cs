namespace ControleFutebolWeb.Models
{
    public class Nota
    {
        public int Id { get; set; }
        public double Valor { get; set; }   // pontuação total calculada (base + ações)
        public string Comentario { get; set; }  // observação

        // Nota final informada manualmente (override). Quando preenchida, o relatório usa
        // este valor absoluto e ignora o cálculo de base + ações. As ações seguem salvas
        // em Detalhes e aparecem no histórico do jogador.
        public double? NotaManual { get; set; }

        public int JogadorId { get; set; }
        public Jogador Jogador { get; set; }

        public int JogoId { get; set; }
        public Jogo Jogo { get; set; }
        public ICollection<Notadetalhe> Detalhes { get; set; } = new List<Notadetalhe>();

        public string? UsuarioId { get; set; }
        public ApplicationUser? Usuario { get; set; }

        public bool IsAutomatica { get; set; } = false;
    }
}