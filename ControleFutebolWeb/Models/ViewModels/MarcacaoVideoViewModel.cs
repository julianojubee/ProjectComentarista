namespace ControleFutebolWeb.Models.ViewModels
{
    /// <summary>Uma jogadora na lista lateral da marcação por vídeo.</summary>
    /// <param name="Numero">Número da camisa — é por ele que o marcador digita.
    /// Null quando o cadastro não tem: aí a jogadora só é selecionável no clique.</param>
    public sealed record JogadoraMarcacao(
        int Id, string Nome, int? Numero, string? Posicao, bool Titular, bool Reserva);

    /// <summary>Uma tecla do catálogo, no formato que o front consome.</summary>
    public sealed record AcaoMarcacaoVm(
        string Id, string Rotulo, string Grupo, string Tecla, bool Shift, bool Estatistica);

    /// <summary>Uma marcação já feita, para a linha do tempo da tela.</summary>
    public sealed record MarcacaoVm(
        int Id, int JogadorId, string AcaoId, int SegundoVideo, int? MinutoJogo);

    /// <summary>
    /// Tudo que a tela de marcação por vídeo precisa de uma vez: o jogo, os dois
    /// elencos com número de camisa, o catálogo de teclas e o que já foi marcado.
    /// Carrega tudo no GET porque durante a marcação a tela não pode parar para
    /// buscar nada — quem está marcando acompanha o vídeo em tempo real.
    /// </summary>
    public class MarcacaoVideoViewModel
    {
        public Jogo Jogo { get; set; } = null!;

        public IReadOnlyList<JogadoraMarcacao> ElencoCasa { get; set; } = Array.Empty<JogadoraMarcacao>();
        public IReadOnlyList<JogadoraMarcacao> ElencoVisitante { get; set; } = Array.Empty<JogadoraMarcacao>();

        public IReadOnlyList<AcaoMarcacaoVm> Acoes { get; set; } = Array.Empty<AcaoMarcacaoVm>();
        public IReadOnlyList<MarcacaoVm> Marcacoes { get; set; } = Array.Empty<MarcacaoVm>();

        /// <summary>O jogo já tem estatística importada de uma API — marcar a mão é
        /// redundante e a consolidação vai preservar o que veio da fonte.</summary>
        public bool TemEstatisticaDeOutraFonte { get; set; }
    }
}
