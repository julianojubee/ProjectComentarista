namespace ControleFutebolWeb.Models.Api
{
    public class CompeticaoDto
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Regiao { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty;
        public bool EhSelecaoNacional { get; set; }
        public bool TopTier { get; set; }
        public string? LogoUrl { get; set; }
    }
}
