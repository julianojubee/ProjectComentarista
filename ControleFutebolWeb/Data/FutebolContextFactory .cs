using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ControleFutebolWeb.Data
{
    public class FutebolContextFactory : IDesignTimeDbContextFactory<FutebolContext>
    {
        public FutebolContext CreateDbContext(string[] args)
        {
            // Mesma fonte de configuração usada em runtime (Program.cs): appsettings.json
            // + user-secrets — evita senha hardcoded e evita o dotnet ef apontar pro
            // banco/porta errado (ver [[infra-deploy]] sobre a factory apontar pro
            // Postgres 9.2/5432 por engano).
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddUserSecrets(Assembly.GetExecutingAssembly())
                .AddEnvironmentVariables()
                .Build();

            var connectionString = config.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "ConnectionStrings:DefaultConnection não configurada. Rode: dotnet user-secrets set \"ConnectionStrings:DefaultConnection\" \"Host=...;Port=...;Database=...;Username=...;Password=...\"");

            var optionsBuilder = new DbContextOptionsBuilder<FutebolContext>();
            optionsBuilder.UseNpgsql(connectionString);

            return new FutebolContext(optionsBuilder.Options);
        }
    }
}
