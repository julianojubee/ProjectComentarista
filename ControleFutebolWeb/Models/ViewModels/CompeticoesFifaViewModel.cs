using ControleFutebolWeb.Services;

namespace ControleFutebolWeb.Models.ViewModels
{
    /// <summary>
    /// Tela Competições › FIFA: o catálogo da FIFA com o link pronto para o cadastro.
    /// </summary>
    public class CompeticoesFifaViewModel
    {
        public List<CompeticaoFifa> Competicoes { get; set; } = new();

        // Competição aberta (IdCompetition) e suas edições. Vazio na primeira carga:
        // listar as edições de todas custaria uma chamada por competição.
        public string? CompeticaoSelecionada { get; set; }
        public string? NomeSelecionada { get; set; }
        public List<SeasonFifa> Edicoes { get; set; } = new();

        // Links "fifa:comp:season" que já têm competição cadastrada.
        public HashSet<string> LinksRegistrados { get; set; } = new();
    }
}
