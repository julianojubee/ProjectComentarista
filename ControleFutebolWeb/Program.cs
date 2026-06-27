using System.Security.Cryptography;
using System.Text;
using ControleFutebolWeb.Authorization;
using ControleFutebolWeb.Converters; // ← importa o converter
using ControleFutebolWeb.Data;
using ControleFutebolWeb.Models;
using ControleFutebolWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Não divulga "Kestrel" no header Server (reduz fingerprinting).
        builder.WebHost.ConfigureKestrel(o => o.AddServerHeader = false);

        // Register code pages provider so encodings like "windows-1252" are supported
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        // Conexão com PostgreSQL
        builder.Services.AddDbContext<FutebolContext>(options =>
            options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
                   .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));


        // 🔹 Aqui você adiciona o converter globalmente
        builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequiredLength = 8;
            options.Password.RequiredUniqueChars = 4;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
            options.Lockout.AllowedForNewUsers = true;
            // Evita enumeração de usuários e exige confirmação coerente.
            options.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<FutebolContext>()
        .AddDefaultTokenProviders();

        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("Admin", policy =>
                policy.AddRequirements(new AdminRequirement()));
        });
        builder.Services.AddScoped<IAuthorizationHandler, AdminHandler>();

        builder.Services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/Account/Login";
            options.AccessDeniedPath = "/Account/Login";
            options.ExpireTimeSpan = TimeSpan.FromDays(7);
            options.SlidingExpiration = true;

            // Endurecimento do cookie de sessão.
            options.Cookie.HttpOnly = true;                       // inacessível ao JS (mitiga XSS roubando sessão)
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // só trafega via HTTPS
            options.Cookie.SameSite = SameSiteMode.Lax;           // mitiga CSRF em navegação cross-site
            options.Cookie.Name = "__Host-Comentarista.Auth";    // prefixo __Host- amarra o cookie a host+HTTPS+path=/
        });

        // Antiforgery: além do token em formulário, aceita o token via header
        // (usado pelas chamadas fetch/AJAX) — ver wrapper em site.js.
        builder.Services.AddAntiforgery(o => o.HeaderName = "RequestVerificationToken");

        builder.Services.AddControllersWithViews(options =>
        {
            // Exige autenticação em todos os controllers por padrão
            options.Filters.Add(new Microsoft.AspNetCore.Mvc.Authorization.AuthorizeFilter());
            // Valida antiforgery em TODOS os POST/PUT/DELETE/PATCH por padrão (anti-CSRF).
            options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
        })
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new NullableLongConverter());
            });

        builder.Services.AddHttpClient<ApiFootballDataService>();
        builder.Services.AddHttpClient<ApiFootballService>();
        builder.Services.AddHttpClient<TransfermarktTreinadorService>();
        builder.Services.AddHttpClient("MediaProxy", c =>
        {
            c.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
            c.Timeout = TimeSpan.FromSeconds(10);
        });
        builder.Services.AddMemoryCache(o =>
        {
            // ~128 MB de imagens em cache (Size = bytes da imagem)
            o.SizeLimit = 128L * 1024 * 1024;
        });
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

                // Senha inicial vem de configuração/ambiente (SeedAdmin:Password ou
                // SEEDADMIN__PASSWORD). Sem ela, gera uma senha forte aleatória e a
                // registra no log uma única vez — nunca usa default fixo no código.
                var logger = services.GetRequiredService<ILogger<Program>>();
                var seedPassword = app.Configuration["SeedAdmin:Password"];
                if (string.IsNullOrWhiteSpace(seedPassword))
                {
                    seedPassword = GerarSenhaForte();
                    logger.LogWarning(
                        "Usuário admin criado com senha gerada automaticamente: {SenhaTemporaria} — " +
                        "TROQUE imediatamente após o primeiro login.", seedPassword);
                }

                var resultadoAdmin = await userManager.CreateAsync(admin, seedPassword);
                if (!resultadoAdmin.Succeeded)
                    logger.LogError("Falha ao criar admin inicial: {Erros}",
                        string.Join("; ", resultadoAdmin.Errors.Select(e => e.Description)));
            }
        }

        // Respeita os headers X-Forwarded-* do nginx (proxy reverso) para que o
        // Kestrel saiba que o acesso externo é HTTPS e gere links/redirects corretos.
        app.UseForwardedHeaders(new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
        });

        // Tratamento de erros e HSTS. Em produção, não vaza stack trace ao usuário
        // e instrui o navegador a só acessar via HTTPS.
        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler("/Home/Error");
            app.UseHsts(); // Strict-Transport-Security
        }

        // Headers de segurança aplicados a todas as respostas (inclui arquivos estáticos).
        app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;

            // Impede o navegador de "adivinhar" o content-type (mitiga MIME sniffing).
            headers["X-Content-Type-Options"] = "nosniff";
            // Bloqueia o site dentro de <iframe> (anti clickjacking) — legado.
            headers["X-Frame-Options"] = "DENY";
            // Limita o vazamento da URL de origem em requisições cross-site.
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            // Desliga APIs sensíveis do navegador que o app não usa.
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";
            // Não expõe a versão do framework.
            headers.Remove("X-Powered-By");

            // Content-Security-Policy: trava origens de recursos. Mantém 'unsafe-inline'
            // porque o layout usa <script>/<style>/onclick inline; o ideal futuro é migrar
            // para nonces e remover o 'unsafe-inline' de script-src.
            headers["Content-Security-Policy"] =
                "default-src 'self'; " +
                // Permite imagens externas via HTTPS (escudos/logos colados por URL) e data:.
                "img-src 'self' data: https:; " +
                "script-src 'self' 'unsafe-inline'; " +
                // 'unsafe-inline' + Google Fonts (usado via @import em algumas telas).
                "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
                "font-src 'self' data: https://fonts.gstatic.com; " +
                "connect-src 'self'; " +
                "frame-ancestors 'none'; " +
                "base-uri 'self'; " +
                "form-action 'self'; " +
                "object-src 'none'";

            await next();
        });

        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");

        app.Run();
    }

    // Gera uma senha aleatória forte que satisfaz a política de senhas configurada.
    private static string GerarSenhaForte()
    {
        const string maiusculas = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string minusculas = "abcdefghijkmnopqrstuvwxyz";
        const string digitos = "23456789";
        const string especiais = "!@#$%*-_?";
        const string todos = maiusculas + minusculas + digitos + especiais;

        var chars = new List<char>
        {
            maiusculas[RandomNumberGenerator.GetInt32(maiusculas.Length)],
            minusculas[RandomNumberGenerator.GetInt32(minusculas.Length)],
            digitos[RandomNumberGenerator.GetInt32(digitos.Length)],
            especiais[RandomNumberGenerator.GetInt32(especiais.Length)],
        };

        while (chars.Count < 20)
            chars.Add(todos[RandomNumberGenerator.GetInt32(todos.Length)]);

        // Embaralha (Fisher–Yates) para não deixar os obrigatórios sempre no início.
        for (int i = chars.Count - 1; i > 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars.ToArray());
    }
}
