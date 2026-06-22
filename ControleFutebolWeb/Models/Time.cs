namespace ControleFutebolWeb.Models;


public class Time
{
    public int Id { get; set; }
    public string Nome { get; set; }
    public string Cidade { get; set; }

    public ICollection<Jogador> Jogadores { get; set; } = new List<Jogador>();

    public string? EscudoUrl { get; set; }
    public string? BackgroundUrl { get; set; }
    public int IdApi { get; set; }

    // Indica se este "Time" representa uma seleção nacional (ex.: Brasil, Argentina)
    // e não um clube. Usado para vincular corretamente Jogador.TimeId (clube) e
    // Jogador.SelecaoId (seleção) ao importar jogos de competições diferentes.
    public bool EhSelecao { get; set; } = false;
    public string? CorPrincipal { get; set; }
    public string? CorSecundaria { get; set; }
    public string? CamisaUrl { get; set; }
    public string? CamisaVisitanteUrl { get; set; }
    public string? linktransfermarket { get; set; }
    // FK para a formação padrão
    public int FormacaoPadraoId { get; set; }
    public Formacao FormacaoPadrao { get; set; }
    // Relação com escalação padrão
    public ICollection<TimeEscalacaoPadrao> TimeEscalacaoPadrao { get; set; }
}
