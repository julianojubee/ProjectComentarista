using ControleFutebolWeb.Models;

namespace ControleFutebolWeb.Models.ViewModels
{
    // Chaveamento (mata-mata) da Copa do Mundo 2026.
    // Modelo híbrido: a estrutura sai do template fixo da FIFA, os slots são
    // preenchidos com as seleções previstas pelos grupos e sobrescritos pelos
    // jogos reais conforme são importados/realizados.

    // Um "slot" de um confronto: ou uma seleção concreta, ou um rótulo posicional
    // (ex.: "1º A", "Melhor 3º (A/B/C/D/F)", "Vencedor 73").
    public class SlotChaveamento
    {
        public Time? Time { get; set; }
        public string Rotulo { get; set; } = "";
        // Time ainda não confirmado: grupo em andamento, terceiros não definidos
        // ou vencedor de confronto anterior ainda não decidido.
        public bool Provisorio { get; set; }
        // Slot de grupo (1º/2º colocado): time confiável usado como âncora para
        // casar o jogo real do mata-mata mesmo quando o adversário (melhor 3º)
        // diverge da previsão do template.
        public bool Ancora { get; set; }
        public bool Definido => Time != null;
    }

    public class ConfrontoCopaViewModel
    {
        public int Numero { get; set; }              // nº oficial da partida (73..104)
        public SlotChaveamento Casa { get; set; } = new();
        public SlotChaveamento Visitante { get; set; } = new();
        public int? PlacarCasa { get; set; }
        public int? PlacarVisitante { get; set; }
        public int? PenaltisCasa { get; set; }       // disputa de pênaltis (nulo se não houve)
        public int? PenaltisVisitante { get; set; }
        public DateTime? Data { get; set; }
        public int? JogoId { get; set; }             // jogo real importado (link Analisar)
        public bool Realizado => PlacarCasa.HasValue && PlacarVisitante.HasValue;
        public bool TevePenaltis => PenaltisCasa.HasValue && PenaltisVisitante.HasValue;
    }

    public class FaseCopaViewModel
    {
        public string Chave { get; set; } = "";      // "R32","R16","QF","SF","TP","F"
        public string Nome { get; set; } = "";       // título completo
        public string NomeCurto { get; set; } = "";  // rótulo da aba
        public List<ConfrontoCopaViewModel> Confrontos { get; set; } = new();
    }

    public class ChaveamentoCopaViewModel
    {
        public List<FaseCopaViewModel> Fases { get; set; } = new();

        // Há algo a exibir? (algum confronto com seleção definida ou placar)
        public bool TemDados => Fases.Any(f => f.Confrontos.Any(
            c => c.Casa.Definido || c.Visitante.Definido || c.Realizado));

        // Índice da fase que deve abrir por padrão: a primeira com jogo pendente,
        // ou a última se tudo já foi decidido.
        public int FaseAtualIndex
        {
            get
            {
                for (int i = 0; i < Fases.Count; i++)
                    if (Fases[i].Confrontos.Any(c => !c.Realizado))
                        return i;
                return Fases.Count > 0 ? Fases.Count - 1 : 0;
            }
        }
    }
}
