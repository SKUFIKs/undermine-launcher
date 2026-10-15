using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UnderMine.Models;
using UnderMine.Services;

namespace UnderMine.Core.Network
{
    public class AuthService
    {
        private readonly ContextData _contextData;
        private readonly HttpClient _httpClient;
        private readonly ProgressService _reporter;
        private readonly ILogger<AuthService> _logger;

        public AuthService(HttpClient httpClient, ContextData contextData, ProgressService reporter, ILogger<AuthService> logger)
        {
            _httpClient = httpClient;
            _reporter = reporter;
            _contextData = contextData;
            _logger = logger;

            if (string.IsNullOrWhiteSpace(contextData.BaseUrl))
            {
                throw new ArgumentException("BaseUrl is empty! Check your .env configuration.");
            }

            if (_httpClient.BaseAddress == null)
            {
                _httpClient.BaseAddress = new Uri(contextData.BaseUrl);
                _httpClient.Timeout = TimeSpan.FromSeconds(30);
            }
        }

        public async Task<LoginResult> Login(string username, string password, CancellationToken ct = default)
        {
            return await ExecuteAuthRequest(_contextData.LogApiKey, username, password, "Login", ct);
        }

        public async Task<LoginResult> Register(string username, string password, CancellationToken ct = default)
        {
            return await ExecuteAuthRequest(_contextData.RegApiKey, username, password, "Register", ct);
        }

        private async Task<LoginResult> ExecuteAuthRequest(string endpoint, string username, string password, string context, CancellationToken ct)
        {
            try
            {
                _logger.LogInformation("[Auth] {Context}: attempting for user {Username}...", context, username);

                var scanner = new SystemService();
                var payload = new
                {
                    Login = username,
                    Password = password,
                    DeviceId = scanner.Scan().deviceId
                };

                using var response = await _httpClient.PostAsJsonAsync(endpoint, payload, ct).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<LoginResult>(cancellationToken: ct).ConfigureAwait(false);

                    if (context == "Login" && string.IsNullOrEmpty(result?.Token))
                    {
                        _logger.LogWarning("[Auth] {Context}: Server returned success but token is empty", context);
                        return new LoginResult { Success = false, ErrorMessage = "Error: empty token from server." };
                    }

                    _logger.LogInformation("[Auth] {Context}: Success.", context);
                    return result ?? new LoginResult { Success = true };
                }

                string errorMsg = await GetErrorMessageAsync(response, ct);
                _logger.LogWarning("[Auth] {Context} rejected: {Error}", context, errorMsg);
                return new LoginResult { Success = false, ErrorMessage = errorMsg };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Auth-{Context} failed", context);
                return new LoginResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        private async Task<string> GetErrorMessageAsync(HttpResponseMessage response, CancellationToken ct)
        {
            try
            {
                var responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(responseBody))
                {
                    using var doc = JsonDocument.Parse(responseBody);
                    if (doc.RootElement.TryGetProperty("message", out var msgProp))
                    {
                        return $"{msgProp.GetString()} ({(int)response.StatusCode})";
                    }
                }
            }
            catch { /* Ignore parsing errors, use default message */ }

            return ProgressService.GetHttpStatusMessage(response.StatusCode);
        }
    }
}
