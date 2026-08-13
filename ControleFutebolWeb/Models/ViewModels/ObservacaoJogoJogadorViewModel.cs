using ControleFutebolWeb.Models;

namespace ControleFutebolWeb.Models.ViewModels
{
    // Observações sobre um jogador feitas na tela de analisar de um jogo específico:
    // as marcadas com a tag Jogador para ele e as em que ele foi citado com "@Nome".
    public class ObservacaoJogoJogadorViewModel
    {
        public Jogo Jogo { get; set; } = null!;

        // Linhas de observação daquele jogo que se referem ao jogador.
        public List<string> Observacoes { get; set; } = new();
    }
}
