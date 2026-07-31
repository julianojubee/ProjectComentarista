using ControleFutebolWeb.Models;

namespace ControleFutebolWeb.Models.ViewModels
{
    /// <summary>
    /// Tela /Simulador: classificação recalculada com os palpites do usuário à
    /// esquerda e os jogos que faltam, rodada a rodada, à direita.
    /// </summary>
    public class SimuladorViewModel
    {
        // Só competições de pontos corridos (ou com fase declarada de pontos corridos).
        public List<Competicao> CompeticoesDisponiveis { get; set; } = new();

        public Competicao? Competicao { get; set; }
        public int? Temporada { get; set; }
        public List<int> TemporadasDisponiveis { get; set; } = new();

        public List<SimuladorLinhaViewModel> Classificacao { get; set; } = new();

        // Rodadas navegáveis pelas setas — as que ainda têm ao menos um jogo sem
        // placar real (ou seja, simulável).
        public List<int> Rodadas { get; set; } = new();
        public int RodadaAtual { get; set; }
        public SimuladorRodadaViewModel? Rodada { get; set; }

        public int TotalPendentes { get; set; }
        public int TotalSimulados { get; set; }
    }

    /// <summary>Linha da tabela com a variação de posição causada pela simulação.</summary>
    public class SimuladorLinhaViewModel
    {
        public Classificacao Linha { get; set; } = null!;

        // Posição na tabela real (só resultados oficiais). Nulo = time que ainda não
        // jogou nada de verdade e só aparece por causa dos palpites.
        public int? PosicaoReal { get; set; }

        // Positivo = subiu na simulação. Nulo/zero = sem mudança.
        public int Variacao => PosicaoReal.HasValue ? PosicaoReal.Value - Linha.Posicao : 0;

        // Pontos que vieram de jogos simulados (destaque na tabela).
        public int PontosSimulados { get; set; }
    }

    public class SimuladorRodadaViewModel
    {
        public int Numero { get; set; }
        public int? RodadaAnterior { get; set; }
        public int? RodadaProxima { get; set; }
        public List<SimuladorJogoViewModel> Jogos { get; set; } = new();
    }

    public class SimuladorJogoViewModel
    {
        public Jogo Jogo { get; set; } = null!;

        // Jogo com placar oficial: entra na tabela, mas não pode ser editado.
        public bool Realizado { get; set; }

        // Placar exibido: o oficial quando realizado, o palpite quando simulado.
        public int? PlacarCasa { get; set; }
        public int? PlacarVisitante { get; set; }

        public bool Simulado { get; set; }
    }
}
