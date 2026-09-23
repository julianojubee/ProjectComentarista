namespace ControleFutebolWeb.Models.Campeonatos
{
    /// <summary>
    /// Súmula simplificada: gol, gol contra, assistência e cartões. Fonte da
    /// artilharia, garçons e disciplina do campeonato (e do histórico entre amigos).
    ///
    /// O autor aponta para NO MÁXIMO um cadastro (JogadorId real ou
    /// JogadorProprioId); nenhum = só o nome digitado, para quem lança a súmula
    /// da pelada sem cadastrar todo mundo. NomeSnapshot é sempre preenchido e é o
    /// que a artilharia mostra se o cadastro sumir.
    /// </summary>
    public class EventoPartidaCampeonato
    {
        public int Id { get; set; }

        public int PartidaId { get; set; }
        public PartidaCampeonato Partida { get; set; } = null!;

        // Lado do atleta do lance. No gol contra é o lado de quem fez o gol contra
        // (o jogador está no elenco dele), e o gol conta para o adversário.
        public int ParticipanteId { get; set; }
        public CampeonatoParticipante Participante { get; set; } = null!;

        public TipoEventoPartida Tipo { get; set; }
        public int? Minuto { get; set; }

        public int? JogadorId { get; set; }
        public Jogador? Jogador { get; set; }
        public int? JogadorProprioId { get; set; }
        public JogadorProprio? JogadorProprio { get; set; }
        public string NomeSnapshot { get; set; } = "";

        // Assistência aponta para o gol que ela originou (apagar o gol leva junto).
        public int? GolId { get; set; }
        public EventoPartidaCampeonato? Gol { get; set; }
    }
}
