using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using UnderMine.Models;
using UnderMine.Services;

namespace UnderMine.Network
{
    public class IdentityService
    {
        private readonly ContextData _contextData;
        private readonly HttpClient _httpClient;
        private readonly ProgressReporter _reporter;

        public IdentityService(ContextData contextData, ProgressReporter reporter, HttpClient? httpClient = null)
        {
            _contextData = contextData ?? throw new ArgumentNullException(nameof(contextData));
            _reporter = reporter ?? throw new ArgumentNullException(nameof(reporter));

            _httpClient = httpClient ?? new HttpClient
            {
                BaseAddress = new Uri(contextData.BaseUrl),
                Timeout = TimeSpan.FromSeconds(30)
            };

            if (_httpClient.BaseAddress == null)
                _httpClient.BaseAddress = new Uri(_contextData.BaseUrl);

            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<LoginResult> Login(string username, string password, CancellationToken ct = default)
        {
            return await ExecuteAuthRequest(_contextData.LogApiKey, username, password, "Login", ct);
        }

        public async Task<LoginResult> Register(string username, string password, CancellationToken ct = default)
        {
            return await ExecuteAuthRequest(_contextData.RegApiKey, username, password, "Register", ct);
        }

        /// <summary>
        /// Общий метод для Login и Register, чтобы не дублировать код
        /// </summary>
        private async Task<LoginResult> ExecuteAuthRequest(string endpoint, string username, string password, string context, CancellationToken ct)
        {
            try
            {
                _reporter.Log($"[Auth] {context}: попытка для пользователя {username}...");

                var payload = new
                {
                    Login = username,
                    Password = password,
                    DeviceId = UnderMine.Services.SystemInformation.GetStableDeviceId()
                };

                using var response = await _httpClient.PostAsJsonAsync(endpoint, payload, ct).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    // Для регистрации сервер может не возвращать токен сразу, проверяем это
                    var result = await response.Content.ReadFromJsonAsync<LoginResult>(cancellationToken: ct).ConfigureAwait(false);

                    if (context == "Login" && string.IsNullOrEmpty(result?.Token))
                    {
                        _reporter.Log($"[Auth] {context}: Сервер вернул успех, но токен пуст", "WARN");
                        return new LoginResult { Success = false, ErrorMessage = "Ошибка: пустой токен от сервера." };
                    }

                    _reporter.Log($"[Auth] {context}: Успешно.");
                    return result ?? new LoginResult { Success = true };
                }

                string errorMsg = await GetErrorMessageAsync(response, ct);
                _reporter.Log($"[Auth] {context} отклонен: {errorMsg}", "WARN");
                return new LoginResult { Success = false, ErrorMessage = errorMsg };
            }
            catch (Exception ex)
            {
                _reporter.LogException(ex, $"Auth-{context}");
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
            catch { /* Игнорируем ошибки парсинга, используем стандартное сообщение */ }
  
            return ProgressReporter.GetHttpStatusMessage(response.StatusCode);
        }
    }
}