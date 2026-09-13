namespace ControleFutebolWeb.Models
{
    /// <summary>
    /// Jogador que a api-football lista como fora (ou em dúvida) para uma partida
    /// específica — a aba "Indisponíveis" do modal Pré-jogo.
    ///
    /// É gravado, e isso o separa da lesão que o FotMob informa no perfil do jogador
    /// (ver SituacaoFisicaViewModel, que de propósito não grava nada): lá o dado é o
    /// estado ATUAL de uma pessoa, que muda sozinho e não pode virar registro; aqui é
    /// um fato datado sobre UMA partida — "o Neymar não estava disponível no Santos ×
    /// Internacional de 06/09" continua verdade depois que ele voltar a jogar. É o
    /// mesmo motivo pelo qual gol e cartão são gravados.
    ///
    /// A lista é substituída inteira a cada busca de escalação: a fonte só publica a
    /// situação de agora, e ela muda até a hora do jogo (quem estava em dúvida na
    /// véspera aparece escalado no dia). Casar linha a linha para atualizar daria o
    /// mesmo resultado com mais chance de deixar sobra.
    /// </summary>
    public class JogoIndisponivel
    {
        public int Id { get; set; }

        public int JogoId { get; set; }
        public Jogo? Jogo { get; set; }

        /// <summary>Time do jogador NO jogo — a fonte diz de que lado ele ficou de fora.</summary>
        public int TimeId { get; set; }
        public Time? Time { get; set; }

        /// <summary>
        /// Jogador do nosso cadastro, quando o id da API bate com alguém. Fica null
        /// para quem nunca foi importado (garoto da base, reserva que nunca entrou);
        /// esse ainda assim aparece na lista, com o nome que a fonte deu — escondê-lo
        /// seria omitir um desfalque real por um detalhe de cadastro nosso.
        /// </summary>
        public int? JogadorId { get; set; }
        public Jogador? Jogador { get; set; }

        /// <summary>Id do jogador na api-football — é por ele que o vínculo acima é feito.</summary>
        public long? IdApiJogador { get; set; }

        /// <summary>Nome como a fonte publicou. Só é exibido quando não há jogador vinculado.</summary>
        public string Nome { get; set; } = "";

        public string? FotoUrl { get; set; }

        /// <summary>
        /// "Missing Fixture" (está fora) ou "Questionable" (dúvida), como vem da API.
        /// Guardado cru: é o texto da fonte, e traduzir na gravação transformaria um
        /// dado importado em interpretação nossa gravada.
        /// </summary>
        public string Tipo { get; set; } = "";

        /// <summary>
        /// Motivo cru da fonte ("Yellow Cards", "Ankle Injury", "Inactive"). A
        /// classificação em lesão/suspensão e a tradução são feitas na exibição, por
        /// IndisponibilidadeHelper — assim melhorar a tradução não exige reimportar.
        /// </summary>
        public string Motivo { get; set; } = "";

        /// <summary>Quando buscamos essa lista (não é a data em que o jogador se lesionou).</summary>
        public DateTime AtualizadoEm { get; set; }
    }
}
