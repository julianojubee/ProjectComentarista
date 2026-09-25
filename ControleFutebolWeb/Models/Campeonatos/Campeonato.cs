namespace ControleFutebolWeb.Models.Campeonatos
{
    /// <summary>
    /// Campeonato criado pelo usuário. É o equivalente de Competicao + temporada
    /// para o módulo: cada edição é um Campeonato próprio ("Copa da Firma 2026",
    /// "Copa da Firma 2027"), o que dispensa o conceito de temporada aqui.
    ///
    /// A tabela, os grupos e o chaveamento NÃO têm código próprio: as partidas são
    /// convertidas em objetos Jogo/Time em memória (nunca gravados) e entregues a
    /// CompeticaoPainelBuilder / ClassificacaoCalculator. Por isso Tipo e
    /// CriteriosDesempate usam exatamente os mesmos valores de Competicao.
    /// </summary>
    public class Campeonato
    {
        public int Id { get; set; }

        // Dono/organizador — o único que edita. Os amigos acompanham pelo link público.
        public string UsuarioId { get; set; } = "";
        public ApplicationUser Usuario { get; set; } = null!;

        public string Nome { get; set; } = "";
        public ModalidadeCampeonato Modalidade { get; set; }
        // Videogame: qual jogo ("EA FC 26", "eFootball"). Amador: livre (ex.: "Society").
        public string? Plataforma { get; set; }
        public string? LogoUrl { get; set; }

        // Mesmos valores de Competicao.Tipo: PONTOS_CORRIDOS | GRUPOS | MATA_MATA.
        // Com fases cadastradas, vale o Tipo de cada fase (ver CampeonatoFase).
        public string Tipo { get; set; } = "PONTOS_CORRIDOS";
        // Mesmo formato de Competicao.CriteriosDesempate (ver CriteriosDesempateHelper).
        public string? CriteriosDesempate { get; set; }

        public StatusCampeonato Status { get; set; } = StatusCampeonato.Rascunho;
        public DateTime? DataInicio { get; set; }
        public DateTime CriadoEm { get; set; }
        public DateTime? EncerradoEm { get; set; }

        // Link público somente leitura (/c/{token}) — mesma ideia de
        // AnaliseCompartilhada, mas um link por campeonato basta. Null = privado;
        // "revogar" gera token novo ou limpa o campo.
        public string? TokenPublico { get; set; }

        public ICollection<CampeonatoFase> Fases { get; set; } = new List<CampeonatoFase>();
        public ICollection<CampeonatoParticipante> Participantes { get; set; } = new List<CampeonatoParticipante>();
        public ICollection<PartidaCampeonato> Partidas { get; set; } = new List<PartidaCampeonato>();
    }
}
