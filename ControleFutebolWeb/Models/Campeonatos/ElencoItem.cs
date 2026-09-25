namespace ControleFutebolWeb.Models.Campeonatos
{
    /// <summary>
    /// Um jogador no elenco de um TimeProprio. Aponta para UM de dois cadastros:
    ///   - JogadorId: jogador real do sistema (o mesmo Neymar pode estar no time
    ///     fictício de mil usuários — por isso é uma ligação, e não Jogador.TimeId);
    ///   - JogadorProprioId: jogador criado pelo usuário.
    /// Exatamente um dos dois é preenchido (check constraint no banco).
    ///
    /// Nome/foto/posição são copiados na inclusão: o cadastro real muda sozinho
    /// (sincronização FotMob, transferências, aposentadoria) e o elenco do usuário
    /// não deve mudar junto. A tela pode oferecer "atualizar com o cadastro".
    /// </summary>
    public class ElencoItem
    {
        public int Id { get; set; }

        public int TimeProprioId { get; set; }
        public TimeProprio TimeProprio { get; set; } = null!;

        public int? JogadorId { get; set; }
        public Jogador? Jogador { get; set; }

        public int? JogadorProprioId { get; set; }
        public JogadorProprio? JogadorProprio { get; set; }

        public string NomeSnapshot { get; set; } = "";
        public string? FotoUrlSnapshot { get; set; }
        public string? Posicao { get; set; }
        // Camisa NESTE time — não é a do cadastro de origem.
        public int? NumeroCamisa { get; set; }
        public int Ordem { get; set; }

        public DateTime IncluidoEm { get; set; }
    }
}
