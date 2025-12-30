using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using P1F_TPM360_HUB.Function;
using P1F_TPM360_HUB.Service;


var builder = WebApplication.CreateBuilder(args);

var env = builder.Environment;

builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

var configuration = builder.Configuration;

string connectionString = "Data Source=10.155.129.69;Initial Catalog=P1F_MAINT;Persist Security Info=True;User ID=dtuser;Password=DTCavite@2024;MultipleActiveResultSets=true;TrustServerCertificate=True;";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
options.UseSqlServer(
                    connectionString,
                    b => b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

builder.Services.AddControllersWithViews();
builder.Services.AddTransient<ITokenService, TokenService>();

// OPENID CONNECT OAUTH2
builder.Services.AddDataProtection()
    .SetApplicationName("sso")
    .PersistKeysToFileSystem(new DirectoryInfo(configuration["Auth:KeyStorage"]));

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddCookie(
    options =>
    {
        //options.LoginPath = "/Account/Login"; // Specify the login page path
        //options.AccessDeniedPath = "/Account/AccessDenied"; // Optional: Specify the access denied page
        options.Cookie.Name = "ping";
        options.Cookie.Path = "/"; // Make cookie accessible for all paths
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // Use Always in production
        options.Cookie.HttpOnly = true; // Prevent JavaScript access
        options.SlidingExpiration = true; // Optional: enable sliding expiration
    }
)
.AddOpenIdConnect(options =>
{
    options.ClientId = configuration["Auth:ClientId"];
    options.ClientSecret = configuration["Auth:ClientSecret"];
    options.Authority = configuration["Auth:Authority"];
    options.ResponseType = "code";
    options.Scope.Add("openid");
    options.Scope.Add("profile");

    // Callback URL after authentication
    options.CallbackPath = new PathString(configuration["Auth:CallbackPath"]);

    // Configure what to do upon receiving tokens
    options.SaveTokens = true;

    var auth = new Authentication();
    var codeVerifier = auth.GenerateCodeVerifier();
    var codeChallenge = auth.GenerateCodeChallenge(codeVerifier);
    // Event Handling
    options.Events = new OpenIdConnectEvents
    {
        OnTokenValidated = context =>
        {
            return Task.CompletedTask;
        },
        OnTicketReceived = context =>
        {
            var principal = context.Principal;
            return Task.CompletedTask;
        },
        OnRedirectToIdentityProvider = context =>
        {
            //if (context.Response.StatusCode == StatusCodes.Status401Unauthorized || !context.HttpContext.User.Identity.IsAuthenticated)
            //{
            //    // Redirect to the custom login page instead of Ping SSO
            //    context.Response.Redirect("/Account/Login");
            //    context.HandleResponse(); // Prevent the default redirection to Ping SSO
            //    return Task.CompletedTask;
            //}
            var request = context.HttpContext.Request;
            var scheme = request.Scheme; // HTTP or HTTPS
            var host = request.Host.Value; // domain and port
            var pathBase = request.PathBase; // domain and port
            var path = request.Path; // domain and port
            var redirect_url = $"{scheme}://{host}{pathBase}/api/auth/Index?originalPath={pathBase}{path}";
            if (env.IsProduction())
            {
                // Set PKCE parameters in the request
                context.ProtocolMessage.SetParameter("code_challenge", codeChallenge);
                context.ProtocolMessage.SetParameter("code_challenge_method", "S256");

                // Store the code_verifier in the properties for later use during the token request
                //context.Properties.SetParameter("code_verifier", codeVerifier);
                context.HttpContext.Response.Cookies.Append("code_verifier", codeVerifier, new CookieOptions
                {
                    HttpOnly = true, // Optional, improve security
                    Secure = true, // Ensure cookie is sent over HTTPS
                    SameSite = SameSiteMode.None // Adjust as necessary for your application
                });

                context.HttpContext.Response.Cookies.Append("redirect_url", redirect_url, new CookieOptions
                {
                    HttpOnly = true, // Optional, improve security
                    Secure = true, // Ensure cookie is sent over HTTPS
                    SameSite = SameSiteMode.None // Adjust as necessary for your application
                });
            }
            else
            {
                context.ProtocolMessage.State = $"returnUrl=={redirect_url}";
            }

            context.ProtocolMessage.RedirectUri = configuration["Auth:RedirectURI"];
            return Task.CompletedTask;
        },
        OnAuthenticationFailed = async context =>
        {
            // Check if the token is expired
            if (context.Response.StatusCode == 401)
            {
                var refreshToken = context.HttpContext.Request.Cookies["refresh_token"];

                if (!string.IsNullOrEmpty(refreshToken))
                {
                    var tokenService = context.HttpContext.RequestServices.GetService<ITokenService>();
                    // Attempt to refresh the access token using the refresh token
                    var isTokenRefreshed = await tokenService.RefreshAccessToken(refreshToken, context.HttpContext);

                    if (isTokenRefreshed)
                    {
                        // Optionally re-execute the request or redirect
                        context.HandleResponse(); // Prevent the default 401 handling
                        context.Response.Redirect(context.Request.Path); // Redirect to the original request path
                    }
                    else
                    {
                        // Handle refresh token failure (e.g., redirect to login or show an error)
                        context.Response.Redirect("/Account/Login/ABC"); // Redirect to login page
                    }
                }
                else
                {
                    // No refresh token available, redirect to login
                    context.Response.Redirect("/Account/Login/CDA");
                }
            }
            //return Task.CompletedTask;
        }
    };
});


builder.Services.AddAuthorization(options =>
{
    //options.AddPolicy("RequireAudit", policy => policy.RequireAssertion(context =>
    //                context.User.HasClaim("p1f_headcount_level", "quality") || context.User.HasClaim("p1f_headcount_level", "warehouse")));
    options.AddPolicy("UserLevel", policy => policy.RequireAssertion(context =>
    {
        // Ambil claim level
        var levelClaim = context.User.FindFirst("P1F_TPM360_HUB_level")?.Value;

        if (string.IsNullOrEmpty(levelClaim)) return false;

        // Pecah string berdasarkan titik koma menjadi list
        var userLevels = levelClaim.Split(';');

        // Daftar level yang diizinkan
        var allowedLevels = new[] { "mat", "mat_admin", "cm_admin", "cm_user", "superadmin" };

        // Cek apakah ada salah satu level user yang terdaftar di allowedLevels
        return userLevels.Any(l => allowedLevels.Contains(l));
    }));
});

//builder.Services.ConfigureApplicationCookie(options =>
//{
//    options.AccessDeniedPath = "/TEST";
//});
// END OPENID CONNECT

builder.Services.AddSingleton<FileManagementService>();
builder.Services.AddSingleton<ExcelServiceProvider>();
builder.Services.AddSingleton<ImportExportFactory>();

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddMvc();
builder.Services.AddSession();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication(); //new
app.UseAuthorization();
app.UseSession();

app.Use(async (context, next) =>
{
    // Call the next middleware in the pipeline
    await next();

    // If the response status is 403 Forbidden
    if (context.Response.StatusCode == StatusCodes.Status403Forbidden)
    {
        // Redirect to the Access Denied page
        context.Response.Redirect("/");
    }
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
