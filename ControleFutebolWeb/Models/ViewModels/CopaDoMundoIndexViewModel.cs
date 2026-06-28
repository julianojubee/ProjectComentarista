using ControleFutebolWeb.Models;

namespace ControleFutebolWeb.Models.ViewModels
{
    /// <summary>Dados da tela /CopaDoMundo (grupos + terceiros colocados + chaveamento).</summary>
    public class CopaDoMundoIndexViewModel
    {
        public int? Temporada { get; set; }
        public List<int> TemporadasDisponiveis { get; set; } = new();
        public List<GrupoViewModel> Grupos { get; set; } = new();
        public List<Jogo> ProximosJogos { get; set; } = new();
        public int RodadaAtual { get; set; }
        public List<Classificacao> TerceirosColocados { get; set; } = new();
        public ChaveamentoCopaViewModel? Chaveamento { get; set; }

        // ── Aba "Seleção" (o usuário monta o melhor XI por posição) ──────────
        public List<Formacao> Formacoes { get; set; } = new();
        public int? SelecaoFormacaoId { get; set; }

        // Várias seleções por usuário+temporada (ex.: 1ª fase, Final).
        public List<SelecaoCopaUsuario> Selecoes { get; set; } = new();
        public int? SelecaoAtualId { get; set; }
        public string? SelecaoAtualNome { get; set; }
        public bool ModoNovaSelecao { get; set; }

        public List<SelecaoSlotVM> SelecaoSlots { get; set; } = new();
        public List<Jogador> PoolJogadores { get; set; } = new();
        public Dictionary<int, Jogador> JogadoresPorId { get; set; } = new();
    }

    /// <summary>Um slot do campo da seleção: posição (X/Y %) + jogador escolhido (se houver).</summary>
    public class SelecaoSlotVM
    {
        public double X { get; set; }
        public double Y { get; set; }
        public int? JogadorId { get; set; }
    }

    /// <summary>Slot persistido no JSON da seleção.</summary>
    public class SelecaoSlotSalvo
    {
        public double X { get; set; }
        public double Y { get; set; }
        public int JogadorId { get; set; }
    }

    /// <summary>Slot recebido do formulário ao salvar a seleção.</summary>
    public class SelecaoSlotInput
    {
        public double X { get; set; }
        public double Y { get; set; }
        public int? JogadorId { get; set; }
    }
}
