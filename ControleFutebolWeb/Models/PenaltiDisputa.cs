namespace ControleFutebolWeb.Models
{
    // Uma cobrança da disputa de pênaltis (mata-mata decidido nas penalidades).
    // A api-football devolve cada cobrança como evento type "Goal" com
    // comments "Penalty Shootout": detail "Penalty" = convertido,
    // "Missed Penalty" = perdido/defendido. Guardadas aqui (com a ordem da
    // cobrança) para montar o placar da disputa e a lista de quem bateu/errou,
    // sem afetar o placar do tempo normal nem a contagem de gols.
    public class PenaltiDisputa
    {
        public int Id { get; set; }

        public int JogoId { get; set; }
        public Jogo Jogo { get; set; } = null!;

        public int JogadorId { get; set; }
        public Jogador Jogador { get; set; } = null!;

        public bool IsTimeCasa { get; set; }

        // true = converteu (gol), false = perdeu/foi defendido.
        public bool Convertido { get; set; }

        // Ordem global da cobrança no jogo (1, 2, 3...), na sequência da disputa.
        public int Ordem { get; set; }
    }
}
