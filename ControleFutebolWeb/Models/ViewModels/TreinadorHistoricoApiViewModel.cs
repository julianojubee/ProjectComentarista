using ControleFutebolWeb.Models;

namespace ControleFutebolWeb.Models.ViewModels
{
    /// <summary>
    /// Modelo da tela de pré-visualização do histórico importado via api-football
    /// (PreVisualizarHistoricoApi). Espelha a pré-visualização do Transfermarkt, mas cada
    /// passagem já vem com o Time local resolvido (ou null, quando o clube da API não está
    /// cadastrado — nesse caso a passagem é só exibida, nunca criada automaticamente).
    /// </summary>
    public class TreinadorHistoricoApiViewModel
    {
        public Treinador Treinador { get; set; } = null!;
        public List<HistoricoApiItemViewModel> Itens { get; set; } = new();

        // true quando a resolução ficou travada num cadastro parcial (stub) por haver mais
        // de um técnico homônimo na API — os dados abaixo podem estar incompletos.
        public bool Ambiguo { get; set; }

        // true quando a resolução encontrou o registro completo do técnico a partir de um
        // cadastro parcial (stub) vinculado ao time atual.
        public bool RegistroCompletoEncontrado { get; set; }
    }

    public class HistoricoApiItemViewModel
    {
        public int? TeamApiId { get; set; }
        public string NomeTime { get; set; } = "";
        public string? LogoUrl { get; set; }
        public DateTime? DtInicio { get; set; }
        public DateTime? DtFim { get; set; } // null = passagem atual

        // Time local correspondente (por Time.IdApi). Null = clube não cadastrado no banco —
        // a passagem é exibida só como informativo e não pode ser salva.
        public int? TimeLocalId { get; set; }
        public string? TimeLocalNome { get; set; }

        public bool ClubeNaoCadastrado => TimeLocalId == null;
    }
}
