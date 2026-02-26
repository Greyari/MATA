using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using P1F_TPM360_HUB.Function;
using P1F_TPM360_HUB.Models;
using P1F_TPM360_HUB.Service;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;

namespace P1F_TPM360_HUB.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : Controller
    {
        // ===================================================================
        // DEPENDENCY INJECTION
        // ===================================================================
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ITokenService _tokenService;
        private readonly IConfiguration _configuration;
        private readonly DatabaseAccessLayer _db;

        public AuthController(
            IHttpClientFactory httpClientFactory,
            ITokenService tokenService,
            IConfiguration configuration,
            DatabaseAccessLayer db)
        {
            _httpClientFactory = httpClientFactory;
            _tokenService = tokenService;
            _configuration = configuration;
            _db = db;
        }

        // ===================================================================
        // LOGIN & REDIRECT
        // ===================================================================

        /// <summary>
        /// Endpoint utama setelah SSO berhasil.
        /// Mengambil data user dari DB, lalu menyimpan claim ke cookie session.
        /// </summary>
        [HttpGet("Index")]
        public async Task<IActionResult> Index(string originalPath = "/")
        {
            // Tentukan path tujuan setelah login
            string pathBase = HttpContext.Request.PathBase;
            if (originalPath == "/" && !string.IsNullOrWhiteSpace(pathBase))
                originalPath = pathBase;

            // Ambil informasi user dari claims SSO
            string sesaId    = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            string firstName = User.FindFirst("firstName")?.Value;
            string lastName  = User.FindFirst("lastName")?.Value;
            string fullName  = $"{firstName} {lastName}";

            var claimsIdentity = (ClaimsIdentity)User.Identity;

            // Hapus claim lama agar tidak duplikat
            foreach (var claimType in new[] { "P1F_TPM360_HUB_name", "P1F_TPM360_HUB_level", "P1F_TPM360_HUB_role", "P1F_TPM360_HUB_lines" })
            {
                var existingClaim = claimsIdentity?.FindFirst(claimType);
                if (existingClaim != null)
                    claimsIdentity.RemoveClaim(existingClaim);
            }

            // Tambahkan claim nama
            claimsIdentity.AddClaim(new Claim("P1F_TPM360_HUB_name", fullName));

            // Ambil detail user dari database (level, role, lines)
            List<UserDetailModel> userDetail = _db.GetUserDetail(sesaId);

            if (userDetail.Any())
            {
                var user = userDetail.First();

                if (!string.IsNullOrEmpty(user.level))
                {
                    // User ditemukan dan punya akses
                    claimsIdentity.AddClaim(new Claim("P1F_TPM360_HUB_level", user.level));
                    claimsIdentity.AddClaim(new Claim("P1F_TPM360_HUB_role",  user.role));
                    claimsIdentity.AddClaim(new Claim("P1F_TPM360_HUB_lines", user.lines));
                }
                else
                {
                    // User ditemukan tapi level kosong → tidak punya akses
                    claimsIdentity.AddClaim(new Claim("P1F_TPM360_HUB_level", "no_access"));
                }
            }
            else
            {
                // User tidak ditemukan di database → tidak punya akses
                claimsIdentity.AddClaim(new Claim("P1F_TPM360_HUB_level", "no_access"));
            }

            // Simpan claims yang sudah diperbarui ke cookie
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity));

            return Redirect(originalPath);
        }

        /// <summary>
        /// Memulai proses login via SSO (OAuth/OIDC).
        /// Setelah login, user akan diarahkan ke GetUserProfile.
        /// </summary>
        [HttpGet("Login")]
        public IActionResult Login()
        {
            return Challenge(new AuthenticationProperties
            {
                RedirectUri = Url.Action("GetUserProfile")
            });
        }

        // ===================================================================
        // TOKEN MANAGEMENT
        // ===================================================================

        /// <summary>
        /// [DEBUG] Endpoint untuk menguji proses refresh token secara manual.
        /// </summary>
        [HttpGet("test-refresh")]
        public async Task<IActionResult> TestRefresh(string refreshToken)
        {
            var result = await _tokenService.RefreshAccessToken(refreshToken, HttpContext);
            return Ok(result);
        }

        /// <summary>
        /// Melakukan refresh access token menggunakan refresh token yang tersimpan di cookie.
        /// </summary>
        [HttpGet("RefreshToken")]
        public async Task<IActionResult> RefreshToken()
        {
            // Validasi: pastikan user sudah terautentikasi
            var authResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (!authResult.Succeeded)
                return BadRequest("Authentication failed.");

            string refreshToken = authResult.Properties.GetTokenValue("refresh_token");
            if (string.IsNullOrEmpty(refreshToken))
                return BadRequest("No refresh token found.");

            // Kirim request refresh token ke server OAuth
            var client = _httpClientFactory.CreateClient();
            var tokenRequest = new Dictionary<string, string>
            {
                { "grant_type",    "refresh_token" },
                { "refresh_token",  refreshToken },
                { "client_id",     _configuration["Auth:ClientId"] },
                { "client_secret", _configuration["Auth:ClientSecret"] }
            };

            var tokenResponse = await client.PostAsync(
                _configuration["Auth:TokenEndpoint"],
                new FormUrlEncodedContent(tokenRequest));

            if (!tokenResponse.IsSuccessStatusCode)
                return BadRequest("Token refresh failed.");

            // Parse token baru
            var responseBody = await tokenResponse.Content.ReadAsStringAsync();
            var newTokens    = JsonSerializer.Deserialize<RefreshTokenResponse>(responseBody);

            // Update token di cookie
            authResult.Properties.UpdateTokenValue("access_token", newTokens.access_token);
            if (!string.IsNullOrEmpty(newTokens.refresh_token))
                authResult.Properties.UpdateTokenValue("refresh_token", newTokens.refresh_token);

            // Sign in ulang dengan token yang sudah diperbarui
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                authResult.Principal,
                authResult.Properties);

            return RedirectToAction("TokenInfo");
        }

        // ===================================================================
        // PROFIL & INFORMASI USER
        // ===================================================================

        /// <summary>
        /// [DEBUG] Menampilkan informasi claim user yang sedang aktif.
        /// </summary>
        [HttpGet("CheckClaim")]
        public async Task<IActionResult> CheckClaim()
        {
            string sesaId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            string name   = User.FindFirst("P1F_TPM360_HUB_name")?.Value;
            return Ok($"{sesaId} - {name}");
        }

        /// <summary>
        /// Mengambil profil user dari UserInfo Endpoint SSO menggunakan access token.
        /// </summary>
        [Authorize]
        [HttpGet("GetUserProfile")]
        public async Task<IActionResult> GetUserProfile()
        {
            var authResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (!authResult.Succeeded)
                return BadRequest("Authentication failed.");

            string accessToken       = authResult.Properties.GetTokenValue("access_token");
            string userInfoEndpoint  = _configuration["Auth:UserInfoEndpoint"];

            // Siapkan HTTP client dengan token
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // Ambil data profil dari SSO
            HttpResponseMessage response = await client.GetAsync(userInfoEndpoint);
            if (!response.IsSuccessStatusCode)
                return BadRequest("Failed to retrieve user profile.");

            var jsonResponse = await response.Content.ReadAsStringAsync();
            var userProfile  = JsonSerializer.Deserialize<dynamic>(jsonResponse);

            return Ok(new { Profile = userProfile });
        }

        // ===================================================================
        // LOGOUT
        // ===================================================================

        /// <summary>
        /// Logout user dan redirect ke halaman home.
        /// </summary>
        [HttpGet("Logout")]
        public IActionResult Logout()
        {
            return SignOut(
                new AuthenticationProperties { RedirectUri = Url.Action("Index", "Home") },
                CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }

    // ===================================================================
    // MODEL PENDUKUNG
    // ===================================================================

    /// <summary>Model untuk data profil user dari SSO.</summary>
    public class UserProfile
    {
        public string sub         { get; set; }
        public string firstName   { get; set; }
        public string lastName    { get; set; }
        public string email       { get; set; }
        public string manager     { get; set; }
        public string managerName { get; set; }
    }

    /// <summary>Model untuk response saat refresh token berhasil.</summary>
    public class RefreshTokenResponse
    {
        public string access_token  { get; set; }
        public string refresh_token { get; set; }
        public string token_type    { get; set; }
        public int    expires_in    { get; set; }
    }
}