namespace ControleFutebolWeb.Models.Api
{
    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class LoginResponse
    {
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresAtUtc { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Nome { get; set; } = string.Empty;
        public bool IsAdmin { get; set; }
    }
}
