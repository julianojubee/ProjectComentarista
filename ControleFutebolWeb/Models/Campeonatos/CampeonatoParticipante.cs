namespace ControleFutebolWeb.Models.Campeonatos
{
    /// <summary>
    /// Inscrição num campeonato. Separa QUEM compete de COM QUE TIME:
    ///   - videogame: Nome = "Juliano", jogando com o Flamengo (TimeId, time real)
    ///     ou com o time dos sonhos dele (TimeProprioId);
    ///   - amador: o participante É o time — Nome vazio e TimeProprioId preenchido.
    /// No máximo um dos dois times é preenchido; nenhum = só o nome (a turma que
    /// troca de time a cada partida — ver PartidaCampeonato.TimeCasaUsadoId).
    ///
    /// É o Id desta linha que vira Time.Id no Jogo em memória entregue ao
    /// ClassificacaoCalculator: a tabela é de participantes, não de times.
    /// </summary>
    public class CampeonatoParticipante
    {
        public int Id { get; set; }

        public int CampeonatoId { get; set; }
        public Campeonato Campeonato { get; set; } = null!;

        // Pessoa/rótulo. Null = usa o nome do time.
        public string? Nome { get; set; }

        public int? TimeProprioId { get; set; }
        public TimeProprio? TimeProprio { get; set; }

        public int? TimeId { get; set; }
        public Time? Time { get; set; }

        // Cópia do nome e escudo do time na inscrição: renomear o time próprio ou
        // o escudo do clube real mudar não reescreve campeonatos já disputados.
        public string? NomeTimeSnapshot { get; set; }
        public string? EscudoUrlSnapshot { get; set; }

        // Grupo na fase de grupos ("A", "B"...) e cabeça de chave para sorteio/chaveamento.
        public string? Grupo { get; set; }
        public int? Semente { get; set; }
        public int Ordem { get; set; }

        public string NomeExibicao =>
            !string.IsNullOrWhiteSpace(Nome) && !string.IsNullOrWhiteSpace(NomeTimeSnapshot)
                ? $"{Nome} ({NomeTimeSnapshot})"
                : Nome ?? NomeTimeSnapshot ?? "";
    }
}
