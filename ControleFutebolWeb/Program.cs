using System.Text;
using ControleFutebolWeb.Converters; // ← importa o converter
using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Services;
using Microsoft.EntityFrameworkCore;

internal class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Register code pages provider so encodings like "windows-1252" are supported
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        // Conexão com PostgreSQL
        builder.Services.AddDbContext<FutebolContext>(options =>
            options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

        // Registra o TransfermarktService com HttpClient dedicado
        builder.Services.AddHttpClient<TransfermarktService>()
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5,
            });

        // 🔹 Aqui você adiciona o converter globalmente
        builder.Services.AddControllersWithViews()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new NullableLongConverter());
            });

        builder.Services.AddHttpClient<ApiFootballDataService>();
        builder.Services.AddHttpClient<TransfermarktSulAmericanaService>();
        builder.Services.AddHostedService<AtualizacaoJogosService>();
        builder.Services.AddHostedService<AtualizarJogadoresSemDataService>();
        builder.Services.AddHostedService<AtualizarCopaSulAmericanaService>();
        builder.Services.AddHttpClient<TransfermarktTreinadorService>();

        builder.Services.AddHttpClient();

        // habilita logging no console e debug
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.AddDebug();
        builder.Services.Configure<CompeticoesApiOptions>(
        builder.Configuration.GetSection("CompeticoesApi"));
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
