namespace ControleFutebolWeb.Models
{
    // Uma "foto" tática da escalação durante o jogo (timeline), por jogo e por usuário.
    // INICIAL e FINAL continuam sendo fases especiais (string em Escalacao.FaseEscalacao);
    // estas são as fases intermediárias, identificadas por Chave (também gravada em
    // Escalacao.FaseEscalacao). São apenas registro tático/visual — não entram no cálculo
    // de notas/relatórios.
    public class FaseTatica
    {
        public int Id { get; set; }

        public int JogoId { get; set; }
        public Jogo Jogo { get; set; } = null!;

        public string UsuarioId { get; set; } = "";
        public ApplicationUser? Usuario { get; set; }

        public string Chave { get; set; } = "";   // usado em Escalacao.FaseEscalacao
        public int Ordem { get; set; }
        public int MinutoInicio { get; set; }
        public string? Nome { get; set; }
    }
}
