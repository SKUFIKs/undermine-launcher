using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json; 
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

        public IdentityService(ContextData contextData) : this(contextData, new HttpClient { BaseAddress = new Uri(contextData.BaseUrl), Timeout = TimeSpan.FromSeconds(30) })
        {
        }

        public IdentityService(ContextData contextData, HttpClient httpClient)
        {
            _contextData = contextData ?? throw new ArgumentNullException(nameof(contextData));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

            if (_httpClient.BaseAddress == null)
                _httpClient.BaseAddress = new Uri(_contextData.BaseUrl);

            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<LoginResult> Login(string username, string password, CancellationToken cancellationToken = default)
        {
            try
            {
                string deviceId = SystemInformation.GetStableDeviceId();
                var payload = new { Login = username, Password = password, DeviceId = deviceId };

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

                string errorMsg = await GetErrorMessageAsync(response, cancellationToken);
                return new LoginResult { Success = false, ErrorMessage = errorMsg };
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }

        public async Task<LoginResult> Register(string username, string password, CancellationToken cancellationToken = default)
        {
            try
            {
                string deviceId = SystemInformation.GetStableDeviceId();
                var payload = new { Login = username, Password = password, DeviceId = deviceId };

                using var response = await _httpClient.PostAsJsonAsync(_contextData.RegApiKey, payload, cancellationToken).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    return new LoginResult { Success = true, ErrorMessage = string.Empty };
                }

                string errorMsg = await GetErrorMessageAsync(response, cancellationToken);
                return new LoginResult { Success = false, ErrorMessage = errorMsg };
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        }

        private async Task<string> GetErrorMessageAsync(HttpResponseMessage response, CancellationToken ct)
        {
            string defaultMessage = MapStatusToMessage(response.StatusCode);
            string? serverMessage = null;

            try
            {
                string responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(responseBody))
                {
                    using var doc = JsonDocument.Parse(responseBody);  
                    if (doc.RootElement.TryGetProperty("message", out var msgProp))
                    {
                        serverMessage = msgProp.GetString();
                    }
                }
            }
            catch
            {
                // Если JSON не парсится, просто игнорируем тело и возвращаем статус
            }

            if (!string.IsNullOrEmpty(serverMessage))
            {
                return $"{serverMessage} (Код: {(int)response.StatusCode})";
            }

            return defaultMessage;
        }

        private LoginResult HandleException(Exception ex)
        {
            if (ex is HttpRequestException)
                return new LoginResult { Success = false, ErrorMessage = $"Ошибка сети: {ex.Message}" };
            if (ex is TaskCanceledException)
                return new LoginResult { Success = false, ErrorMessage = "Превышено время ожидания" };

            return new LoginResult { Success = false, ErrorMessage = $"Ошибка: {ex.Message}" };
        }

        private static string MapStatusToMessage(System.Net.HttpStatusCode status) => status switch
        {
            System.Net.HttpStatusCode.BadRequest => "Неверный запрос (400)",
            System.Net.HttpStatusCode.Unauthorized => "Ошибка авторизации (401)",
            System.Net.HttpStatusCode.Forbidden => "Доступ запрещён (403)",
            System.Net.HttpStatusCode.NotFound => "Сервис не найден (404)",
            System.Net.HttpStatusCode.Conflict => "Такой пользователь уже существует (409)",
            System.Net.HttpStatusCode.Gone => "Аккаунт забанен или удален (410)", 
            System.Net.HttpStatusCode.TooManyRequests => "Слишком много попыток (429)",
            System.Net.HttpStatusCode.InternalServerError => "Ошибка сервера (500)",
            _ => $"Ошибка {(int)status}"
        };
    }
}