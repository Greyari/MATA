using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using P1F_TPM360_HUB.Function;
using P1F_TPM360_HUB.Service;

var builder = WebApplication.CreateBuilder(args);
var env     = builder.Environment;

// ===================================================================
// KONFIGURASI: appsettings.json + environment variables
// ===================================================================
builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

var configuration = builder.Configuration;

// ===================================================================
// DATABASE: Entity Framework Core + SQL Server
// ===================================================================
string connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        connectionString,
        b => b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

// ===================================================================
// AUTENTIKASI: Cookie + OpenID Connect (SSO PingFederate)
// ===================================================================

// Data Protection: menyimpan encryption key ke filesystem (untuk multi-instance)
builder.Services.AddDataProtection()
    .SetApplicationName("sso")
    .PersistKeysToFileSystem(new DirectoryInfo(configuration["Auth:KeyStorage"]));

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme          = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.Cookie.Name           = "ping";
    options.Cookie.Path           = "/";                         // Accessible dari semua path
    options.Cookie.SecurePolicy   = CookieSecurePolicy.Always;   // Hanya HTTPS
    options.Cookie.HttpOnly       = true;                        // Tidak bisa diakses JavaScript
    options.SlidingExpiration     = true;                        // Perpanjang session jika aktif
})
.AddOpenIdConnect(options =>
{
    options.ClientId     = configuration["Auth:ClientId"];
    options.ClientSecret = configuration["Auth:ClientSecret"];
    options.Authority    = configuration["Auth:Authority"];
    options.ResponseType = "code";
    options.Scope.Add("openid");
    options.Scope.Add("profile");
    options.CallbackPath = new PathString(configuration["Auth:CallbackPath"]);
    options.SaveTokens   = true;

    // PKCE: code verifier & challenge untuk keamanan tambahan
    var auth          = new Authentication();
    var codeVerifier  = auth.GenerateCodeVerifier();
    var codeChallenge = auth.GenerateCodeChallenge(codeVerifier);

    options.Events = new OpenIdConnectEvents
    {
        // Dipanggil setelah token berhasil divalidasi
        OnTokenValidated = context => Task.CompletedTask,

        // Dipanggil setelah seluruh proses autentikasi selesai
        OnTicketReceived = context => Task.CompletedTask,

        // Dipanggil sebelum redirect ke halaman login SSO
        OnRedirectToIdentityProvider = context =>
        {
            var request = context.HttpContext.Request;
            string scheme    = request.Scheme;
            string host      = request.Host.Value;
            string pathBase  = request.PathBase;
            string path      = request.Path;

            // URL untuk kembali setelah login SSO berhasil
            string redirectUrl = $"{scheme}://{host}{pathBase}/api/auth/Index?originalPath={pathBase}{path}";

            if (env.IsProduction())
            {
                // Di production: tambahkan PKCE dan simpan state di cookie
                context.ProtocolMessage.SetParameter("code_challenge",        codeChallenge);
                context.ProtocolMessage.SetParameter("code_challenge_method", "S256");

                var secureCookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure   = true,
                    SameSite = SameSiteMode.None
                };

                context.HttpContext.Response.Cookies.Append("code_verifier", codeVerifier,  secureCookieOptions);
                context.HttpContext.Response.Cookies.Append("redirect_url",  redirectUrl,   secureCookieOptions);
            }
            else
            {
                // Di development: simpan redirect URL di state parameter
                context.ProtocolMessage.State = $"returnUrl=={redirectUrl}";
            }

            context.ProtocolMessage.RedirectUri = configuration["Auth:RedirectURI"];
            return Task.CompletedTask;
        },

        // Dipanggil jika autentikasi gagal (misal: token expired)
        OnAuthenticationFailed = async context =>
        {
            if (context.Response.StatusCode != 401) return;

            string refreshToken = context.HttpContext.Request.Cookies["refresh_token"];

            if (!string.IsNullOrEmpty(refreshToken))
            {
                var tokenService = context.HttpContext.RequestServices.GetService<ITokenService>();
                bool isRefreshed = await tokenService.RefreshAccessToken(refreshToken, context.HttpContext);

                if (isRefreshed)
                {
                    // Refresh berhasil → coba akses halaman aslinya lagi
                    context.HandleResponse();
                    context.Response.Redirect(context.Request.Path);
                }
                else
                {
                    // Refresh gagal → arahkan ke halaman login
                    context.Response.Redirect("/Account/Login/ABC");
                }
            }
            else
            {
                // Tidak ada refresh token → arahkan ke login
                context.Response.Redirect("/Account/Login/CDA");
            }
        }
    };
});

// ===================================================================
// OTORISASI: Policy berbasis level user
// ===================================================================
builder.Services.AddAuthorization(options =>
{
    // Policy "UserLevel": izinkan akses jika user memiliki setidaknya satu level yang diperbolehkan
    options.AddPolicy("UserLevel", policy => policy.RequireAssertion(context =>
    {
        string levelClaim = context.User.FindFirst("P1F_TPM360_HUB_level")?.Value;
        if (string.IsNullOrEmpty(levelClaim)) return false;

        string[] userLevels    = levelClaim.Split(';');
        string[] allowedLevels = { "mat", "mat_admin", "cm_admin", "cm_user", "superadmin" };

        return userLevels.Any(l => allowedLevels.Contains(l));
    }));
});

// ===================================================================
// REGISTRASI SERVICE
// ===================================================================
builder.Services.AddControllersWithViews().AddRazorRuntimeCompilation();
builder.Services.AddControllersWithViews();
builder.Services.AddMvc();
builder.Services.AddSession();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();

builder.Services.AddTransient<ITokenService, TokenService>();
builder.Services.AddSingleton<FileManagementService>();
builder.Services.AddSingleton<ExcelServiceProvider>();
builder.Services.AddScoped<ImportExportFactory>();
builder.Services.AddScoped<DatabaseAccessLayer>();

// ===================================================================
// PIPELINE: Middleware
// ===================================================================
var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts(); // HTTP Strict Transport Security (default 30 hari)
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

// Middleware: redirect 403 Forbidden ke halaman utama
app.Use(async (context, next) =>
{
    await next();
    if (context.Response.StatusCode == StatusCodes.Status403Forbidden)
        context.Response.Redirect("/");
});

// Route default: Home/Index
app.MapControllerRoute(
    name:    "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();