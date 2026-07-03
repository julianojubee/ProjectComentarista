namespace ControleFutebolWeb.Models
{
    // Pênalti perdido/defendido numa partida. A api-football devolve esse lance
    // como evento type "Goal" com detail "Missed Penalty" — não é gol, então é
    // guardado aqui (com minuto) para aparecer no feed de eventos sem afetar o
    // placar nem a contagem de gols.
    public class PenaltiPerdido
    {
        public int Id { get; set; }

        public int JogoId { get; set; }
        public Jogo Jogo { get; set; } = null!;

        public int JogadorId { get; set; }
        public Jogador Jogador { get; set; } = null!;

        public int Minuto { get; set; }
        public bool IsTimeCasa { get; set; }
    }
}
