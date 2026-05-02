using ControleFutebolWeb.Data;
using ControleFutebolWeb.Services;
using Microsoft.EntityFrameworkCore;

internal class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);


        // Conexão com PostgreSQL
        builder.Services.AddDbContext<FutebolContext>(options =>
            options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

        builder.Services.AddControllersWithViews();
        builder.Services.AddHttpClient<ApiFootballDataService>();
        builder.Services.AddHostedService<AtualizacaoJogosService>();
        builder.Services.AddHttpClient();
        // habilita logging no console e debug
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.AddDebug();
        var app = builder.Build();

        // Inicializa o banco com dados (SeedData)
        using (var scope = app.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            var context = services.GetRequiredService<FutebolContext>();

            SeedData.Initialize(services);
            DbInitializer.Initialize(context);
        }

        app.UseStaticFiles();
        app.UseRouting();

      
        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");

        app.Run();
    }
}