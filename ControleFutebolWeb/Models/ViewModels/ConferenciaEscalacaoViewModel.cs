namespace ControleFutebolWeb.Models.ViewModels
{
    // Tela /Servicos/ConferirEscalacao: compara a escalação inicial salva com o XI que
    // a ESPN publicou. Só mostra — aplicar é um segundo passo, explícito, porque a
    // escalação costuma ter ajuste manual que não pode ser sobrescrito sem aviso.
    public class ConferenciaEscalacaoViewModel
    {
        public int JogoId { get; set; }
        public DateTime? Data { get; set; }
        public string Partida { get; set; } = "";

        public bool Ok { get; set; }
        public string? Erro { get; set; }

        public List<LadoEscalacaoEspn> Lados { get; set; } = new();

        // A escalação é por usuário: cada analista tem a sua cópia do mesmo jogo, e a
        // correção só faz sentido aplicada a uma delas por vez. A lista mostra quantas
        // divergências cada um tem, para saber quem precisa de ajuste.
        public List<UsuarioEscalacaoItem> Usuarios { get; set; } = new();
        public string? UsuarioSelecionadoId { get; set; }

        public string UsuarioSelecionadoNome =>
            Usuarios.FirstOrDefault(u => u.Id == UsuarioSelecionadoId)?.Nome ?? "—";

        // Setas táticas presas à escalação inicial deste usuário. É o ÚNICO dado que
        // aplicar destrói: a FK de setasescalacao é CASCADE, então trocar a escalação
        // leva as setas junto. Nota, observação, jogada e fase tática se penduram no
        // jogo (e no jogador), não na escalação, e sobrevivem.
        public int SetasAfetadas { get; set; }

        // A fase FINAL não é tocada, mas ela foi montada a partir da INICIAL antiga —
        // depois de aplicar, vale reconstruí-la.
        public bool TemFaseFinal { get; set; }

        // Notas do usuário selecionado dadas a jogadores que, segundo a ESPN, não
        // entraram em campo. Sobram quando a escalação errada levou o analista a
        // avaliar quem nem jogou. Nunca são apagadas junto com a escalação: ficam
        // listadas para ele decidir uma a uma.
        public List<NotaSuspeitaItem> NotasSuspeitas { get; set; } = new();

        // Nada a fazer: os dois lados batem com a ESPN.
        public bool TudoIgual => Ok && Lados.All(l => !l.TemDivergencia);
    }

    public class UsuarioEscalacaoItem
    {
        public string Id { get; set; } = "";
        public string Nome { get; set; } = "";

        // Quantos jogadores dos dois XIs não batem com a ESPN. Zero = escalação correta.
        public int Divergencias { get; set; }

        // Ainda não tem escalação própria neste jogo — vê a compartilhada.
        public bool SemEscalacaoPropria { get; set; }

        // Notas que este usuário deu neste jogo. Aparecem aqui porque corrigir a
        // escalação pode deixar nota de quem, segundo a ESPN, não entrou em campo.
        public int Notas { get; set; }
    }

    public class NotaSuspeitaItem
    {
        public int NotaId { get; set; }
        public string Jogador { get; set; } = "";
        public double Valor { get; set; }
        public double? NotaManual { get; set; }
        public string? Comentario { get; set; }
        public bool EhAutomatica { get; set; }

        // O jogador não casou com ninguém do roster da ESPN — pode ser que ele tenha
        // jogado e o nome não bateu. A tela avisa para não apagar por engano.
        public bool JogadorNaoLocalizadoNaEspn { get; set; }
    }

    public class LadoEscalacaoEspn
    {
        public bool IsTimeCasa { get; set; }
        public string Time { get; set; } = "";

        public string? FormacaoSalva { get; set; }
        public string? FormacaoEspn { get; set; }

        // A ESPN usa o mesmo formato do nosso cadastro ("4-2-3-1"), então a comparação
        // é direta. Formação que não existe no cadastro não conta como divergência —
        // não há para onde mudar.
        public bool FormacaoDiverge { get; set; }

        public List<ItemEscalacaoEspn> Titulares { get; set; } = new();

        // Quem está no nosso XI e a ESPN não tem como titular.
        public List<string> Saem { get; set; } = new();

        public bool TemDivergencia =>
            FormacaoDiverge || Saem.Count > 0 || Titulares.Any(t => t.Situacao != SituacaoEscalacao.Confere);
    }

    public enum SituacaoEscalacao
    {
        Confere,     // já está no XI salvo
        Entra,       // existe no elenco, mas não está escalado como titular
        Criar,       // não existe no elenco — precisa ser cadastrado
    }

    public class ItemEscalacaoEspn
    {
        public int? Numero { get; set; }
        public string Nome { get; set; } = "";
        public string Posicao { get; set; } = "";
        public SituacaoEscalacao Situacao { get; set; }

        // Como o jogador está gravado no nosso cadastro, quando o nome diverge do da
        // ESPN ("Joao Victor" lá, "Victor Sá" aqui). Null quando bate ou quando não existe.
        public string? NomeNoCadastro { get; set; }

        // Como o casamento foi feito, para a tela poder mostrar em que confiar.
        public string? CasadoPor { get; set; }
    }
}
