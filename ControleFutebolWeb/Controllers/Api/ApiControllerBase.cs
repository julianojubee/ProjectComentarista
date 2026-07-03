using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControleFutebolWeb.Controllers.Api
{
    // Base para os controllers da API (api/v1/...) consumida pelo app Android.
    // Exige JWT (não o cookie da Identity) e ignora o antiforgery global — CSRF
    // não se aplica aqui porque não há cookie de sessão envolvido.
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [IgnoreAntiforgeryToken]
    public abstract class ApiControllerBase : ControllerBase
    {
    }
}
