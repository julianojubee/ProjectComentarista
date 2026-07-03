using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ControleFutebolWeb.Data
{
    public class FutebolContextFactory : IDesignTimeDbContextFactory<FutebolContext>
    {
        public FutebolContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<FutebolContext>();

            // String de conexão igual à usada em runtime (appsettings/user-secrets): Postgres 17, porta 5433
            optionsBuilder.UseNpgsql("Host=localhost;Port=5433;Database=ProjectComentarista;Username=postgres;Password=180695");

            return new FutebolContext(optionsBuilder.Options);
        }
    }
}
