using System.Text;
using ControleFutebolWeb.Converters; // ← importa o converter
using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Register code pages provider so encodings like "windows-1252" are supported
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        // Conexão com PostgreSQL
        builder.Services.AddDbContext<FutebolContext>(options =>
            options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));


        // 🔹 Aqui você adiciona o converter globalmente
        builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
        {
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredLength = 6;
        })
        .AddEntityFrameworkStores<FutebolContext>()
        .AddDefaultTokenProviders();

        builder.Services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/Account/Login";
            options.AccessDeniedPath = "/Account/Login";
            options.ExpireTimeSpan = TimeSpan.FromDays(7);
            options.SlidingExpiration = true;
        });

        builder.Services.AddControllersWithViews(options =>
        {
            // Exige autenticação em todos os controllers por padrão
            options.Filters.Add(new Microsoft.AspNetCore.Mvc.Authorization.AuthorizeFilter());
        })
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new NullableLongConverter());
            });

        builder.Services.AddHttpClient<ApiFootballDataService>();
        builder.Services.AddHttpClient<ApiFootballService>();
        builder.Services.AddHttpClient<TransfermarktTreinadorService>();
        builder.Services.AddSingleton<ServicoMonitor>();
        builder.Services.AddSingleton<AtualizarJogadoresSemDataService>();
        builder.Services.AddHostedService(sp =>
            sp.GetRequiredService<AtualizarJogadoresSemDataService>());
        //builder.Services.AddHostedService<AtualizacaoJogosService>();
        //builder.Services.AddHostedService<AtualizarCopaSulAmericanaService>();
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

            context.Database.Migrate();
            SeedData.Initialize(services);
            DbInitializer.Initialize(context);

            // Seed do admin inicial
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            if (!userManager.Users.Any())
            {
                var admin = new ApplicationUser
                {
                    UserName = "admin",
                    Email = "admin@comentarista.com",
                    Nome = "Administrador",
                    IsAdmin = true,
                    EmailConfirmed = true
                };
                await userManager.CreateAsync(admin, "Admin@123");
            }
        }

        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");

        app.Run();
    }
}
