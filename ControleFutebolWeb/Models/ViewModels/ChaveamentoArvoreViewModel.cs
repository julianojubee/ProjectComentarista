using ControleFutebolWeb.Models;

namespace ControleFutebolWeb.Models.ViewModels
{
    // Chaveamento (mata-mata) genérico em árvore de dois lados, montado a partir dos
    // jogos importados (oitavas → quartas → semis → final). Confrontos de ida e volta
    // são agregados num único duelo; a ligação entre as fases é inferida pelos vencedores.

    // Um duelo do mata-mata. Reaproveita SlotChaveamento para representar cada lado
    // (uma seleção/time concreto ou um rótulo "A definir").
    public class ConfrontoArvore
    {
        public SlotChaveamento Lado1 { get; set; } = new();
        public SlotChaveamento Lado2 { get; set; } = new();

        // Placar agregado (soma das pernas). Nulo enquanto o confronto não foi totalmente jogado.
        public int? PlacarLado1 { get; set; }
        public int? PlacarLado2 { get; set; }

        // Disputa de pênaltis no confronto (decidida na última perna). Nula se não houve.
        public int? PenaltisLado1 { get; set; }
        public int? PenaltisLado2 { get; set; }

        public DateTime? Data { get; set; }

        // Ids dos jogos reais que compõem o confronto (1 = jogo único, 2 = ida e volta).
        public List<int> JogoIds { get; set; } = new();

        // Rótulo da fase ("Oitavas", "Quartas", "Semifinal", "Final").
        public string Rotulo { get; set; } = "";

        public bool Realizado => PlacarLado1.HasValue && PlacarLado2.HasValue;
        public bool TevePenaltis => PenaltisLado1.HasValue && PenaltisLado2.HasValue;
        public bool Vazio => !Lado1.Definido && !Lado2.Definido;

        public bool Lado1Venceu
        {
            get
            {
                if (!Realizado) return false;
                if (PlacarLado1 > PlacarLado2) return true;
                if (PlacarLado1 == PlacarLado2 && TevePenaltis) return PenaltisLado1 > PenaltisLado2;
                return false;
            }
        }

        public bool Lado2Venceu
        {
            get
            {
                if (!Realizado) return false;
                if (PlacarLado2 > PlacarLado1) return true;
                if (PlacarLado1 == PlacarLado2 && TevePenaltis) return PenaltisLado2 > PenaltisLado1;
                return false;
            }
        }
    }

    public class ChaveamentoArvoreViewModel
    {
        // Cada lista já vem ordenada de cima para baixo, dividida pelos dois lados do bracket.
        public List<ConfrontoArvore> OitavasEsq { get; set; } = new();   // 4
        public List<ConfrontoArvore> OitavasDir { get; set; } = new();   // 4
        public List<ConfrontoArvore> QuartasEsq { get; set; } = new();   // 2
        public List<ConfrontoArvore> QuartasDir { get; set; } = new();   // 2
        public ConfrontoArvore SemiEsq { get; set; } = new();
        public ConfrontoArvore SemiDir { get; set; } = new();
        public ConfrontoArvore Final { get; set; } = new();

        public bool TemDados { get; set; }
    }
}
