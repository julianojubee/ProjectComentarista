using ControleFutebolWeb.Models;
using ControleFutebolWeb.Models.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ControleFutebolWeb.Controllers.Api
{
    [Route("api/v1/auth")]
    [AllowAnonymous]
    public class AuthController : ApiControllerBase
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _configuration;

        public AuthController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IConfiguration configuration)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _configuration = configuration;
        }

        // POST api/v1/auth/login
        [HttpPost("login")]
        public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest("Usuário e senha são obrigatórios.");

            var user = await _userManager.FindByNameAsync(request.Username)
                ?? await _userManager.FindByEmailAsync(request.Username);
            if (user == null)
                return Unauthorized();

            var resultado = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
            if (!resultado.Succeeded)
                return Unauthorized();

            return Ok(GerarToken(user));
        }

        private LoginResponse GerarToken(ApplicationUser user)
        {
            var jwtKey = _configuration["Jwt:Key"];
            var key = string.IsNullOrWhiteSpace(jwtKey) ? "chave-de-desenvolvimento-apenas-para-testes-locais" : jwtKey;
            var issuer = _configuration["Jwt:Issuer"] ?? "ControleFutebolWeb";
            var audience = _configuration["Jwt:Audience"] ?? "ControleFutebolWebApi";
            var expireMinutes = _configuration.GetValue<int?>("Jwt:ExpireMinutes") ?? 60;

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id),
                new(ClaimTypes.Name, user.UserName ?? user.Id),
                new("nome", user.Nome),
                new("isAdmin", user.IsAdmin ? "true" : "false")
            };

            var expiresAtUtc = DateTime.UtcNow.AddMinutes(expireMinutes);
            var credenciais = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);
            var jwt = new JwtSecurityToken(issuer, audience, claims, expires: expiresAtUtc, signingCredentials: credenciais);

            return new LoginResponse
            {
                Token = new JwtSecurityTokenHandler().WriteToken(jwt),
                ExpiresAtUtc = expiresAtUtc,
                UserName = user.UserName ?? string.Empty,
                Nome = user.Nome,
                IsAdmin = user.IsAdmin
            };
        }
    }
}
