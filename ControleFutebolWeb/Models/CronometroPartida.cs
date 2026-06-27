namespace ControleFutebolWeb.Models
{
    // Cronômetro persistente da partida, por jogo e por usuário. O tempo decorrido é
    // SegundosAcumulados + (agora - InicioUtc) quando RODANDO; só SegundosAcumulados quando
    // PARADO/FINALIZADO. Assim o tempo é restaurado ao reabrir a tela.
    public class CronometroPartida
    {
        public int Id { get; set; }

        public int JogoId { get; set; }
        public Jogo Jogo { get; set; } = null!;

        public string UsuarioId { get; set; } = "";
        public ApplicationUser? Usuario { get; set; }

        // PARADO | RODANDO | FINALIZADO
        public string Estado { get; set; } = "PARADO";

        public int SegundosAcumulados { get; set; }
        public DateTime? InicioUtc { get; set; }
    }
}
