using ControleFutebolWeb.Models;

namespace ControleFutebolWeb.Models.ViewModels
{
    // "Equipe da rodada": os melhores por posição numa rodada, na linha do que o
    // Sofascore chama de Equipe da semana. Diferente de lá, a nota aqui é a do
    // próprio usuário (manual quando existe, calculada pelos critérios dele
    // quando não) — é o dado que o sistema tem de mais próprio.
    //
    // A escalação não é mais um 4-3-3 fixo: a formação padrão é a mais usada
    // pelos times naquela rodada, e cada slot recebe quem realmente jogou
    // naquela posição na rodada (ver Competicoes/EquipeDaRodada).
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

        // Formação em que a equipe está montada.
        public int? FormacaoId { get; set; }
        public string FormacaoNome { get; set; } = "";
        // true = ninguém escolheu, veio da formação mais usada na rodada.
        public bool FormacaoAutomatica { get; set; }
        public List<FormacaoDaRodada> FormacoesDisponiveis { get; set; } = new();

        // Slots da formação, na ordem cadastrada. Um slot pode ficar sem jogador
        // quando ninguém avaliado na rodada joga ali.
        public List<SlotDaRodada> Slots { get; set; } = new();

        public IEnumerable<JogadorDaRodada> Todos =>
            Slots.Where(s => s.Jogador != null).Select(s => s.Jogador!);

        public bool Vazia => !Todos.Any();

        // Destaque da rodada: a maior nota entre os escolhidos.
        public JogadorDaRodada? Craque =>
            Todos.OrderByDescending(j => j.Nota).FirstOrDefault();
    }

    public class FormacaoDaRodada
    {
        public int Id { get; set; }
        public string Nome { get; set; } = "";
        // Quantas vezes os times usaram esta formação na rodada.
        public int Usos { get; set; }
    }

    public class SlotDaRodada
    {
        public string NomePosicao { get; set; } = "";
        // Coordenadas em % do campo, iguais às do cadastro da formação.
        public double PosicaoX { get; set; }
        public double PosicaoY { get; set; }
        public JogadorDaRodada? Jogador { get; set; }
        // true = ninguém da rodada jogou exatamente nessa posição, então o slot
        // foi preenchido por alguém do mesmo setor (a UI avisa).
        public bool Aproximado { get; set; }
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
        // Posição em que ele jogou na rodada (escalação da partida); null quando
        // não há escalação com coordenada e só restou o cadastro do jogador.
        public string? PosicaoNaRodada { get; set; }

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
