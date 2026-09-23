namespace ControleFutebolWeb.Models.Campeonatos
{
    /// <summary>
    /// Fase de um campeonato com mais de um formato (grupos + mata-mata).
    /// Diferente de CompeticaoFase, aqui a partida aponta direto para a fase: as
    /// partidas são geradas pelo sistema, então não há "round" de API para
    /// classificar por heurística. Sem fases = fase única guiada por Campeonato.Tipo.
    /// </summary>
    public class CampeonatoFase
    {
        public int Id { get; set; }

        public int CampeonatoId { get; set; }
        public Campeonato Campeonato { get; set; } = null!;

        public string Nome { get; set; } = "";
        // PONTOS_CORRIDOS | GRUPOS | MATA_MATA
        public string Tipo { get; set; } = "";
        public int Ordem { get; set; }
        public bool IdaEVolta { get; set; }
        // Quantos avançam por grupo/tabela para a fase seguinte. Null = última fase.
        public int? Classificados { get; set; }

        // Só no mata-mata: jogo entre os perdedores das semifinais.
        public bool DisputaTerceiro { get; set; }
    }
}
