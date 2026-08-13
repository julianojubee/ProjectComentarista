namespace ControleFutebolWeb.Models
{
    // Registro de transferência de clube detectada automaticamente: quando um
    // jogador já cadastrado para um time aparece na escalação de outro clube
    // (import da api-football), o TimeId dele é trocado e a mudança fica
    // registrada aqui — histórico consultável na "Janela de Transferências".
    // Movimentações de/para seleções nacionais NÃO contam (SelecaoId separado).
    public class Transferencia
    {
        public int Id { get; set; }

        public int JogadorId { get; set; }
        public Jogador Jogador { get; set; } = null!;

        public int? TimeOrigemId { get; set; }
        public Time? TimeOrigem { get; set; }

        // null = aposentadoria: o jogador saiu do futebol em vez de trocar de clube.
        // O TimeId dele continua no último clube (o histórico de jogos precisa dele);
        // quem marca que parou de jogar é Jogador.Aposentado.
        public int? TimeDestinoId { get; set; }
        public Time? TimeDestino { get; set; }

        // Jogo em que a transferência foi detectada (fonte da competição no filtro).
        public int? JogoId { get; set; }
        public Jogo? Jogo { get; set; }

        public DateTime Data { get; set; }

        // null = detecção automática (import), compartilhada com todo mundo.
        // Preenchido = transferência manual, só o próprio usuário pode excluir —
        // um engano de um usuário não deve afetar o que os outros veem/gerenciam.
        public string? UsuarioId { get; set; }
        public ApplicationUser? Usuario { get; set; }
    }
}
