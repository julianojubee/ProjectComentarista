using System.ComponentModel.DataAnnotations;
using ControleFutebolWeb.Helpers;
using ControleFutebolWeb.Helpers.Campeonatos;
using ControleFutebolWeb.Models.Campeonatos;

namespace ControleFutebolWeb.Models.ViewModels
{
    // Formulário de /Campeonatos/Criar. O formato vira as fases do campeonato
    // (ver CampeonatosController.FasesDoFormato) — toda geração de partidas é por fase.
    public class CampeonatoCriarInput
    {
        [Required(ErrorMessage = "Dê um nome ao campeonato.")]
        [StringLength(100)]
        public string Nome { get; set; } = "";

        public ModalidadeCampeonato Modalidade { get; set; } = ModalidadeCampeonato.VideoGame;

        [StringLength(60)]
        public string? Plataforma { get; set; }

        // PONTOS_CORRIDOS | GRUPOS (grupos + mata-mata) | MATA_MATA
        public string Formato { get; set; } = "PONTOS_CORRIDOS";

        public bool IdaEVolta { get; set; }

        // Só no formato GRUPOS: o mata-mata que vem depois dos grupos.
        public bool MataMataIdaEVolta { get; set; }

        // Formatos com mata-mata: jogo entre os perdedores das semifinais.
        public bool DisputaTerceiro { get; set; }

        [Range(1, 8, ErrorMessage = "Entre 1 e 8 por grupo.")]
        public int ClassificadosPorGrupo { get; set; } = 2;
    }

    public class CampeonatoDetalhesViewModel
    {
        public Campeonato Campeonato { get; set; } = null!;
        public string Aba { get; set; } = "tabela";
        public CompeticaoPainelBuilder.Painel Painel { get; set; } = new();
        public Dictionary<int, CampeonatoParticipante> Participantes { get; set; } = new();
        public List<EstatisticaAtleta> Artilharia { get; set; } = new();
        public SituacaoCampeonato Situacao { get; set; }
        public List<TimeProprio> MeusTimes { get; set; } = new();
        public List<Time> TimesReais { get; set; } = new();
        public string? LinkPublico { get; set; }

        public string NomeDe(int? participanteId) =>
            participanteId is int id && Participantes.TryGetValue(id, out var p)
                ? p.NomeExibicao
                : CampeonatoPainelAdapter.NomeVagaEmAberto;

        public string? EscudoDe(int? participanteId) =>
            participanteId is int id && Participantes.TryGetValue(id, out var p)
                ? p.EscudoUrlSnapshot ?? p.TimeProprio?.EscudoUrl ?? p.Time?.EscudoUrl
                : null;

        // Fase que pode ser gerada agora: a primeira sem partidas, desde que a anterior
        // tenha partidas. A validação de verdade (placares, classificados) é do serviço.
        public CampeonatoFase? ProximaFaseAGerar
        {
            get
            {
                CampeonatoFase? anterior = null;
                foreach (var f in Campeonato.Fases.OrderBy(f => f.Ordem))
                {
                    var temPartidas = Campeonato.Partidas.Any(p => p.FaseId == f.Id);
                    if (!temPartidas)
                        return anterior == null || Campeonato.Partidas.Any(p => p.FaseId == anterior.Id) ? f : null;
                    anterior = f;
                }
                return null;
            }
        }

        public bool PrimeiraFaseGerada => Campeonato.Partidas.Any();
    }

    // Uma opção de atleta na súmula: valor "r:{id}" (jogador real), "p:{id}" (próprio).
    public record OpcaoAtleta(string Valor, string Nome);

    public class SumulaViewModel
    {
        public Campeonato Campeonato { get; set; } = null!;
        public PartidaCampeonato Partida { get; set; } = null!;
        public CampeonatoParticipante? Casa { get; set; }
        public CampeonatoParticipante? Visitante { get; set; }
        public List<OpcaoAtleta> ElencoCasa { get; set; } = new();
        public List<OpcaoAtleta> ElencoVisitante { get; set; } = new();
    }

    public class TimeProprioInput
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Dê um nome ao time.")]
        [StringLength(80)]
        public string Nome { get; set; } = "";

        [StringLength(5)]
        public string? Sigla { get; set; }

        [StringLength(80)]
        public string? Cidade { get; set; }

        // Só exibição: o escudo chega por upload (TimesPropriosController.Salvar).
        public string? EscudoUrl { get; set; }

        // Hex puro: a cor vai para um atributo style nas telas.
        [RegularExpression("^#[0-9a-fA-F]{6}$", ErrorMessage = "Cor inválida.")]
        public string? CorPrincipal { get; set; } = "#d4a520";
        [RegularExpression("^#[0-9a-fA-F]{6}$", ErrorMessage = "Cor inválida.")]
        public string? CorSecundaria { get; set; } = "#141414";
    }

    public class JogadorProprioInput
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Informe o nome.")]
        [StringLength(80)]
        public string Nome { get; set; } = "";

        [StringLength(40)]
        public string? Apelido { get; set; }

        [StringLength(40)]
        public string? Posicao { get; set; }

        [Range(0, 99)]
        public int? NumeroCamisa { get; set; }

        [DataType(DataType.Date)]
        public DateTime? DataNascimento { get; set; }

        // Só exibição: a foto chega por upload (JogadoresPropriosController.Salvar).
        public string? FotoUrl { get; set; }

        public int? NacionalidadeId { get; set; }
    }
}
