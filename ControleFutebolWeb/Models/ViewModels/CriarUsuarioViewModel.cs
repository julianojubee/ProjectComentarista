using System.ComponentModel.DataAnnotations;

namespace ControleFutebolWeb.Models.ViewModels
{
    public class CriarUsuarioViewModel
    {
        [Required(ErrorMessage = "Informe o nome")]
        public string Nome { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe o login")]
        public string UserName { get; set; } = string.Empty;

        // Obrigatório: sem e-mail o usuário não consegue usar o "esqueci minha senha"
        // e a redefinição vira tarefa manual do admin (Account/RedefinirSenha).
        [Required(ErrorMessage = "Informe o e-mail")]
        [EmailAddress(ErrorMessage = "E-mail inválido")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe a senha")]
        [MinLength(6, ErrorMessage = "Mínimo 6 caracteres")]
        public string Password { get; set; } = string.Empty;

        public bool IsAdmin { get; set; }

        // Pode escrever no blog público (/blog/admin).
        public bool EhAutorBlog { get; set; }

        // Módulos do plano (ApplicationUser.Modulos).
        public bool ModuloAnalise { get; set; } = true;
        public bool ModuloCampeonatos { get; set; }
    }
}
