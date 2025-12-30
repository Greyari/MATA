using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using OfficeOpenXml;

namespace P1F_TPM360_HUB.Service
{
    public interface ITokenService
    {
        Task<bool> RefreshAccessToken(string refreshToken, HttpContext httpContext);
    }

    public class TokenService : ITokenService
    {
        private readonly IConfiguration _configuration;

        public TokenService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<bool> RefreshAccessToken(string refreshToken, HttpContext httpContext)
        {
            try
            {
                using (var httpClient = new HttpClient())
                {
                    var tokenRequest = new Dictionary<string, string>
                    {
                        { "grant_type", "refresh_token" },
                        { "refresh_token", refreshToken },
                        { "client_id", _configuration["Auth:ClientId"] },
                        { "client_secret", _configuration["Auth:ClientSecret"] },
                        { "scope", "openid profile" } // Adjust scopes as needed
                    };

                    var requestContent = new FormUrlEncodedContent(tokenRequest);
                    var tokenEndpoint = $"{_configuration["Auth:TokenEndpoint"]}"; // Adjust as per your Auth settings
                    var response = await httpClient.PostAsync(tokenEndpoint, requestContent);

                    if (response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        var tokenResponse = JsonSerializer.Deserialize<RefreshTokenResponse>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                        // Check if the token response contains valid tokens
                        if (tokenResponse != null)
                        {
                            // Save the new tokens in cookies (or wherever they are maintained)
                            httpContext.Response.Cookies.Append("access_token", tokenResponse.access_token, new CookieOptions
                            {
                                HttpOnly = true,
                                Secure = true, // Ensure this is true in production
                                SameSite = SameSiteMode.Strict // Adjust as needed
                            });

                            // Update or set refresh token if necessary
                            if (!string.IsNullOrEmpty(tokenResponse.refresh_token))
                            {
                                httpContext.Response.Cookies.Append("refresh_token", tokenResponse.refresh_token, new CookieOptions
                                {
                                    HttpOnly = true,
                                    Secure = true,
                                    SameSite = SameSiteMode.Strict
                                });
                            }

                            return true; // Indicate that the refresh was successful
                        }
                    }
                }
            }
            catch (HttpRequestException httpRequestException)
            {
                // Handle specific HTTP request errors
                // Log the exception and return false
                // For example, you might log to a file or monitoring service
                Console.WriteLine($"HTTP Request error: {httpRequestException.Message}");
                // Optionally perform specific actions based on status codes here
            }
            catch (JsonException jsonException)
            {
                // Handle JSON deserialization errors
                Console.WriteLine($"JSON Deserialization error: {jsonException.Message}");
            }
            catch (Exception ex)
            {
                // Handle all other exceptions
                Console.WriteLine($"An error occurred: {ex.Message}");
                // You might want to log it or take some recovery steps
            }

            return false; // Indicate the refresh failed
        }
    }
    public class RefreshTokenResponse
    {
        public string access_token { get; set; }
        public string refresh_token { get; set; }
        public string token_type { get; set; }
        public int expires_in { get; set; }
    }
}
