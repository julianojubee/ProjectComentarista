using ControleFutebolWeb.Models;

namespace ControleFutebolWeb.Models.ViewModels
{
    // "Equipe da rodada": os melhores por setor numa rodada, na linha do que o
    // Sofascore chama de Equipe da semana. Diferente de lá, a nota aqui é a do
    // próprio usuário (manual quando existe, calculada pelos critérios dele
    // quando não) — é o dado que o sistema tem de mais próprio.
    public class EquipeDaRodadaViewModel
    {
        public Competicao Competicao { get; set; } = null!;
        public int? Temporada { get; set; }
        public List<int> TemporadasDisponiveis { get; set; } = new();

        public int Rodada { get; set; }
        public List<int> RodadasDisponiveis { get; set; } = new();

        public int JogosNaRodada { get; set; }
        // Quantos jogadores da rodada tinham nota — a base de onde saiu a seleção.
        public int JogadoresAvaliados { get; set; }

        public List<JogadorDaRodada> Goleiros { get; set; } = new();
        public List<JogadorDaRodada> Defensores { get; set; } = new();
        public List<JogadorDaRodada> MeioCampo { get; set; } = new();
        public List<JogadorDaRodada> Atacantes { get; set; } = new();

        public IEnumerable<JogadorDaRodada> Todos =>
            Goleiros.Concat(Defensores).Concat(MeioCampo).Concat(Atacantes);

        public bool Vazia => !Todos.Any();

        // Destaque da rodada: a maior nota entre os escolhidos.
        public JogadorDaRodada? Craque =>
            Todos.OrderByDescending(j => j.Nota).FirstOrDefault();
    }

    public class JogadorDaRodada
    {
        public Jogador Jogador { get; set; } = null!;
        public Time? Time { get; set; }
        public int JogoId { get; set; }
        public double Nota { get; set; }
        // true = nota calculada pelos critérios, não digitada pelo usuário.
        public bool NotaAutomatica { get; set; }
        public string Adversario { get; set; } = "";
        public string Placar { get; set; } = "";
        public int Gols { get; set; }
        public int Assistencias { get; set; }

        public string NotaColor => Nota switch
        {
            >= 8.5 => "#f59e0b",
            >= 7.5 => "#22c55e",
            >= 6.5 => "#3b82f6",
            >= 5.5 => "#6b7280",
            _ => "#ef4444"
        };
    }
}
