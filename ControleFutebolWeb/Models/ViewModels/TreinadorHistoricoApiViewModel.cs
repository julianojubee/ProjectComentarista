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

        // Homônimos devolvidos pela API quando a resolução fica ambígua (ex.: os dois
        // "Luís Castro" portugueses). A tela lista para o usuário escolher qual importar.
        public List<TreinadorCandidatoViewModel> Candidatos { get; set; } = new();

        // Id (api-football) do candidato escolhido — mantido nos links/form para a escolha
        // sobreviver do preview até o salvamento.
        public int? EscolhidoId { get; set; }

        // Dados do registro escolhido que serão gravados no cadastro do treinador ao salvar
        // (o time atual nunca é alterado — só idade, nacionalidade e foto).
        public TreinadorCandidatoViewModel? Escolhido { get; set; }
    }

    /// <summary>
    /// Um técnico devolvido pela api-football como candidato — usado tanto para listar os
    /// homônimos quanto para mostrar de quem são os dados que serão gravados.
    /// </summary>
    public class TreinadorCandidatoViewModel
    {
        public int Id { get; set; }
        public string Nome { get; set; } = "";
        public string? NomeCompleto { get; set; }
        public int? Idade { get; set; }
        public string? Nacionalidade { get; set; }
        public string? FotoUrl { get; set; }
        public string? TimeAtual { get; set; }
        public int TotalPassagens { get; set; }

        // Resumo textual da carreira ("2013 – 2026"), para dar contexto na escolha.
        public string? PeriodoCarreira { get; set; }

        // Primeiros clubes da carreira, para o usuário reconhecer o técnico de relance.
        public List<string> ClubesResumo { get; set; } = new();
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

        // true quando essa passagem já está salva no banco (mesmo time + mesmo mês/ano de
        // início) — calculado com a mesma regra de dedupe do SalvarHistoricoApi, para que a
        // pré-visualização preveja exatamente o que vai acontecer ao confirmar o salvamento.
        public bool JaSalva { get; set; }

        // Resume o que vai acontecer com esta passagem ao clicar em "Salvar Histórico".
        public string StatusPrevisto => ClubeNaoCadastrado ? "ignorada" : JaSalva ? "ja-salva" : "nova";
    }
}
