namespace ControleFutebolWeb.Models
{
    // Seta de movimentação de um jogador no campinho tático (tela Analisar):
    // aponta do jogador (Escalacao.PosicaoX/Y) para o destino X/Y. Um jogador
    // pode ter várias setas (movimentos para lados diferentes), e como pertence
    // à Escalacao ela é por jogo, fase (INICIAL/FINAL/fase tática) e usuário.
    public class EscalacaoSeta
    {
        public int Id { get; set; }

        public int EscalacaoId { get; set; }
        public Escalacao Escalacao { get; set; } = null!;

        // Destino da seta, em % do campo (mesmo sistema de PosicaoX/PosicaoY).
        public double X { get; set; }
        public double Y { get; set; }
    }
}
