// Helpers/CamisaHelper.cs
using ControleFutebolWeb.Models;

namespace ControleFutebolWeb.Helpers
{
    public static class CamisaHelper
    {
        public static string? ObterCamisa(Jogo jogo, Time time)
        {
            bool eTimeCasa = jogo.TimeCasaId == time.Id;

            if (eTimeCasa)
                return time.CamisaUrl;                          // manda → camisa principal
            else
                return time.CamisaVisitanteUrl ?? time.CamisaUrl; // visita → camisa visitante, fallback para principal
        }
    }
}