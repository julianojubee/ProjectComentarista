using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ControleFutebolWeb.Data
{
    public class FutebolContextFactory : IDesignTimeDbContextFactory<FutebolContext>
    {
        public FutebolContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<FutebolContext>();

            // String de conexão igual ao appsettings.json
            optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=ProjectComentarista;Username=postgres;Password=180695");

            return new FutebolContext(optionsBuilder.Options);
        }
    }
}
