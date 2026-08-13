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
    [System.ComponentModel.DataAnnotations.Schema.Column("linktransfermarket")]
    public string? LinkTransfermarket { get; set; }

    // Liga da api-football em que o clube foi encontrado (id e país da liga),
    // preenchido quando o time é criado pela transferência manual para um clube
    // de fora das competições cadastradas (/Jogadores/Estatisticas → Transferências).
    // Serve para reencontrar o clube já salvo numa próxima transferência da mesma
    // liga, sem gastar chamada da API. Null nos times vindos da importação de jogos.
    public int? LigaIdApi { get; set; }
    public string? PaisApi { get; set; }
    // Estádio do clube, vindo do nó "venue" de /teams da api-football
    // (não confundir com Jogo.Estadio, que é o local de uma partida específica).
    public string? EstadioNome { get; set; }
    public string? EstadioCidade { get; set; }
    public int? EstadioCapacidade { get; set; }
    public string? EstadioGramado { get; set; }
    public string? EstadioImagemUrl { get; set; }

    // FK para a formação padrão
    public int FormacaoPadraoId { get; set; }
    public Formacao FormacaoPadrao { get; set; }
    // Relação com escalação padrão
    public ICollection<TimeEscalacaoPadrao> TimeEscalacaoPadrao { get; set; }
}
