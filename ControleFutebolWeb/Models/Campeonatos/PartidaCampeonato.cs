namespace ControleFutebolWeb.Models.Campeonatos
{
    /// <summary>
    /// Partida de um Campeonato. Segue as convenções de Jogo para a conversão em
    /// memória ser direta: placar null = não realizada; pênaltis null = sem disputa.
    /// ("Partida" sozinho já existe em ApiFutebolModelsPartidas.)
    /// </summary>
    public class PartidaCampeonato
    {
        public int Id { get; set; }

        public int CampeonatoId { get; set; }
        public Campeonato Campeonato { get; set; } = null!;

        public int? FaseId { get; set; }
        public CampeonatoFase? Fase { get; set; }

        public int Rodada { get; set; }
        // Rótulo que vai direto para Jogo.Grupo na conversão: "Grupo A" na fase de
        // grupos, o nome da etapa no mata-mata ("Quartas", "Semifinal", "Final") e
        // null em pontos corridos. Gravado pelo GeradorPartidasCampeonato.
        public string? Grupo { get; set; }
        // Posição no chaveamento do mata-mata (1..N dentro da rodada), para o
        // vencedor saber em qual partida da rodada seguinte entra.
        public int? ChaveOrdem { get; set; }

        // Null no mata-mata enquanto o confronto anterior não foi decidido.
        public int? ParticipanteCasaId { get; set; }
        public CampeonatoParticipante? ParticipanteCasa { get; set; }
        public int? ParticipanteVisitanteId { get; set; }
        public CampeonatoParticipante? ParticipanteVisitante { get; set; }

        // Videogame com troca de time a cada partida: o time real usado NESTE jogo.
        // Null = o time da inscrição.
        public int? TimeCasaUsadoId { get; set; }
        public Time? TimeCasaUsado { get; set; }
        public int? TimeVisitanteUsadoId { get; set; }
        public Time? TimeVisitanteUsado { get; set; }

        public int? PlacarCasa { get; set; }
        public int? PlacarVisitante { get; set; }
        public int? PenaltisCasa { get; set; }
        public int? PenaltisVisitante { get; set; }
        // W.O.: placar fica o padrão combinado (ex.: 3x0) e a tela sinaliza.
        public bool WO { get; set; }

        public DateTime? Data { get; set; }
        public string? Local { get; set; }
        public string? Observacoes { get; set; }

        public ICollection<EventoPartidaCampeonato> Eventos { get; set; } = new List<EventoPartidaCampeonato>();
    }
}
