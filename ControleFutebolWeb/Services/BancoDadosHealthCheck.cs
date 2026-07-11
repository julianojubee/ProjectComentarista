using ControleFutebolWeb.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ControleFutebolWeb.Services
{
    /// <summary>
    /// Health check consultado em /health (nginx, script de deploy e monitoramento).
    /// Considera a aplicação saudável quando consegue abrir conexão com o PostgreSQL —
    /// cobre tanto o processo de pé quanto a dependência mais crítica.
    /// </summary>
    public class BancoDadosHealthCheck : IHealthCheck
    {
        private readonly FutebolContext _context;

        public BancoDadosHealthCheck(FutebolContext context) => _context = context;

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _context.Database.CanConnectAsync(cancellationToken)
                    ? HealthCheckResult.Healthy("Banco de dados acessível.")
                    : HealthCheckResult.Unhealthy("Banco de dados inacessível.");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Erro ao conectar no banco de dados.", ex);
            }
        }
    }
}
