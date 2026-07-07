using System.ComponentModel.DataAnnotations;

namespace ControleFutebolWeb.Models.ViewModels
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Informe o usuário")]
        public string UserName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Informe a senha")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }

        // Confirma que o usuário quer encerrar o acesso já em andamento em outro local.
        public bool ForcarLogin { get; set; }
    }
}
