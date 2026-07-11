namespace ControleFutebolWeb.Models.ViewModels
{
    // Página /Competicoes/CompeticoesApi: catálogo das ligas disponíveis na
    // api-football para a temporada 2026 (dump estático em wwwroot/data),
    // agrupadas por país — referência para registrar o IdApi de uma competição.
    public class CompeticoesApiViewModel
    {
        public List<CompeticoesApiPais> Paises { get; set; } = new();
        public int Total { get; set; }
        public int TotalRegistradas { get; set; }
    }

    public class CompeticoesApiPais
    {
        public string Nome { get; set; } = "";
        public string Bandeira { get; set; } = "";
        public List<CompeticaoApiLiga> Competicoes { get; set; } = new();
    }

    // "Liga" para não colidir com Models.CompeticaoApiItem (busca de ligas de um time)
    public class CompeticaoApiLiga
    {
        public int Id { get; set; }
        public string Nome { get; set; } = "";
        public string Tipo { get; set; } = "";
        public string Logo { get; set; } = "";
        public string Pais { get; set; } = "";
        public string Bandeira { get; set; } = "";

        // Já existe uma Competicao cadastrada com IdApi == Id.
        public bool Registrada { get; set; }
    }
}
