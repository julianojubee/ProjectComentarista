using ControleFutebolWeb.Models;

namespace ControleFutebolWeb.Models.ViewModels
{
    // Observações sobre um time feitas na tela de analisar de um jogo específico.
    // Reúne o jogo e as linhas de observação que se referem a esse time (lado que ele jogou).
    public class ObservacaoJogoTimeViewModel
    {
        public Jogo Jogo { get; set; } = null!;

        // true = o time desta página era o mandante (lado [CASA]); false = visitante ([VISITANTE]).
        public bool TimeEhCasa { get; set; }

        // Linhas de observação (já sem a tag [CASA]/[VISITANTE]) referentes ao time.
        public List<string> Observacoes { get; set; } = new();
    }
}
