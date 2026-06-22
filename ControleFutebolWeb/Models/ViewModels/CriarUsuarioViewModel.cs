using System.ComponentModel.DataAnnotations;

namespace ControleFutebolWeb.Models.ViewModels
{
    public class CriarUsuarioViewModel
    {
        [Required(ErrorMessage = "Informe o nome")]
        public string Nome { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe o login")]
        public string UserName { get; set; } = string.Empty;

        [EmailAddress]
        public string? Email { get; set; }

        [Required(ErrorMessage = "Informe a senha")]
        [MinLength(6, ErrorMessage = "Mínimo 6 caracteres")]
        public string Password { get; set; } = string.Empty;

        public bool IsAdmin { get; set; }
    }
}
