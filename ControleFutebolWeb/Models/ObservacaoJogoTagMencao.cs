namespace ControleFutebolWeb.Models
{
    // Menção a um jogador dentro do texto livre de uma ObservacaoJogoTag (via "@Nome"),
    // independente do Tipo da observação. Usada para também exibir a observação no
    // perfil (Estatísticas) de cada jogador mencionado.
    public class ObservacaoJogoTagMencao
    {
        public int Id { get; set; }

        public int ObservacaoJogoTagId { get; set; }
        public ObservacaoJogoTag ObservacaoJogoTag { get; set; } = null!;

        public int JogadorId { get; set; }
        public Jogador Jogador { get; set; } = null!;
    }
}
