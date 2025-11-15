using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using UnderMine.Models;
using UnderMine.Services;

namespace UnderMine.Network.AuthService
{
    public class IdentityService
    {
        private readonly ContextData _contextData;
        private readonly HttpClient _httpClient;

        // Backwards-compatible constructor: create HttpClient if caller doesn't provide one
        public IdentityService(ContextData contextData) : this(contextData, new HttpClient { BaseAddress = new Uri(contextData.BaseUrl), Timeout = TimeSpan.FromSeconds(30) })
        {
        }

        public IdentityService(ContextData contextData, HttpClient httpClient)
        {
            _contextData = contextData ?? throw new ArgumentNullException(nameof(contextData));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

            if (_httpClient.BaseAddress == null)
                _httpClient.BaseAddress = new Uri(_contextData.BaseUrl);

            // Ensure JSON accept header
            if (!_httpClient.DefaultRequestHeaders.Accept.Contains(new MediaTypeWithQualityHeaderValue("application/json")))
            {
                _httpClient.DefaultRequestHeaders.Accept.Clear();
                _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            }
        }

        public async Task<LoginResult> Login(string username, string password, CancellationToken cancellationToken = default)
        {
            try
            {
                var payload = new { login = username, password = password };

                using var response = await _httpClient.PostAsJsonAsync(_contextData.LogApiKey, payload, cancellationToken).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    var tokenResponse = await response.Content.ReadFromJsonAsync<LoginResult>(cancellationToken: cancellationToken).ConfigureAwait(false);
                    if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.Token))
                    {
                        return new LoginResult { Success = false, ErrorMessage = $"Error: empty token. Code {(int)response.StatusCode}" };
                    }

                    return new LoginResult { Success = true, Token = tokenResponse.Token };
                }

                return new LoginResult { Success = false, ErrorMessage = MapStatusToMessage(response.StatusCode) };
            }
            catch (HttpRequestException ex)
            {
                return new LoginResult { Success = false, ErrorMessage = $"Ошибка сети: {ex.Message}" };
            }
            catch (TaskCanceledException)
            {
                return new LoginResult { Success = false, ErrorMessage = "Превышено время ожидания" };
            }
            catch (Exception ex)
            {
                return new LoginResult { Success = false, ErrorMessage = $"Ошибка авторизации: {ex.Message}" };
            }
        }

        public async Task<LoginResult> Register(string username, string password, CancellationToken cancellationToken = default)
        {
            try
            {
                string deviceId = SystemInformation.GetStableDeviceId();
                var payload = new { login = username, password = password, deviceID = deviceId };

                using var response = await _httpClient.PostAsJsonAsync(_contextData.RegApiKey, payload, cancellationToken).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    return new LoginResult { Success = true, ErrorMessage = string.Empty };
                }

                // Try to extract API message
                string responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                string? apiMessage = null;
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(responseBody);
                    if (doc.RootElement.TryGetProperty("message", out var msgProp))
                        apiMessage = msgProp.GetString();
                }
                catch { }

                string fallbackError = response.StatusCode switch
                {
                    System.Net.HttpStatusCode.BadRequest => "Неверный запрос",
                    System.Net.HttpStatusCode.Conflict => "Пользователь уже существует",
                    System.Net.HttpStatusCode.TooManyRequests => "Превышен лимит аккаунтов на устройстве",
                    System.Net.HttpStatusCode.InternalServerError => "Ошибка сервера",
                    _ => "Неизвестная ошибка"
                };

                return new LoginResult
                {
                    Success = false,
                    ErrorMessage = apiMessage != null ? $"{fallbackError} ({(int)response.StatusCode}): {apiMessage}" : $"{fallbackError} ({(int)response.StatusCode})"
                };
            }
            catch (HttpRequestException ex)
            {
                return new LoginResult { Success = false, ErrorMessage = $"Ошибка сети: {ex.Message}" };
            }
            catch (TaskCanceledException)
            {
                return new LoginResult { Success = false, ErrorMessage = "Превышено время ожидания" };
            }
            catch (Exception ex)
            {
                return new LoginResult { Success = false, ErrorMessage = $"Неизвестная ошибка: {ex.Message}" };
            }
        }

        private static string MapStatusToMessage(System.Net.HttpStatusCode status) => status switch
        {
            System.Net.HttpStatusCode.BadRequest => "Не верный запрос (400)",
            System.Net.HttpStatusCode.Unauthorized => "Неправильный логин или пароль (401)",
            System.Net.HttpStatusCode.Forbidden => "Доступ запрещён (403)",
            System.Net.HttpStatusCode.NotFound => "Ресурс не найден (404)",
            System.Net.HttpStatusCode.InternalServerError => "Ошибка сервера (500)",
            _ => $"Неизвестная ошибка: {(int)status}"
        };
    }
}