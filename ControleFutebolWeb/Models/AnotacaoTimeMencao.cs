namespace ControleFutebolWeb.Models
{
    // Menção a um jogador dentro do texto de uma AnotacaoTime (via "@Nome").
    // Mesma ideia da ObservacaoJogoTagMencao, mas para as anotações do clube —
    // permite listar no perfil do jogador todas as anotações que o referenciam.
    public class AnotacaoTimeMencao
    {
        public int Id { get; set; }

        public int AnotacaoTimeId { get; set; }
        public AnotacaoTime AnotacaoTime { get; set; } = null!;

        public int JogadorId { get; set; }
        public Jogador Jogador { get; set; } = null!;
    }
}
