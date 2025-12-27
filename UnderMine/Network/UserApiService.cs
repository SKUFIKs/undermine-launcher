using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using UnderMine.Models;
using UnderMine.Core.Services; 

namespace UnderMine.Network
{
    public class UserApiService
    {
        private readonly ContextData _contextData;
        private readonly LoginResult _loginResult;
        private readonly HttpClient _httpClient;
        private readonly ProgressReporter _reporter; 

        public UserApiService(ContextData contextData, LoginResult loginResult, ProgressReporter reporter)
            : this(contextData, loginResult, reporter, new HttpClient { BaseAddress = new Uri(contextData.BaseUrl), Timeout = TimeSpan.FromSeconds(30) })
        {
        }

        public UserApiService(ContextData contextData, LoginResult loginResult, ProgressReporter reporter, HttpClient httpClient)
        {
            _contextData = contextData ?? throw new ArgumentNullException(nameof(contextData));
            _loginResult = loginResult ?? throw new ArgumentNullException(nameof(loginResult));
            _reporter = reporter ?? throw new ArgumentNullException(nameof(reporter)); 
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

            if (_httpClient.BaseAddress == null)
                _httpClient.BaseAddress = new Uri(_contextData.BaseUrl);

            if (!_httpClient.DefaultRequestHeaders.Accept.Contains(new MediaTypeWithQualityHeaderValue("application/json")))
            {
                _httpClient.DefaultRequestHeaders.Accept.Clear();
                _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            }
        }

        public async Task<ServerInfo> GetServerInfo(CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(_loginResult.Token))
            {
                _reporter.Log("[API] Токен авторизации отсутствует. Запрос информации о сервере невозможен.", "WARN");
                return new ServerInfo
                {
                    Success = false,
                    ErrorMessage = "Неизвестная ошибка."
                };
            }

            try
            {
                _reporter.Log("[API] Запрос информации о сервере...", "INFO"); 

                var request = new HttpRequestMessage(HttpMethod.Get, _contextData.EntranceApiKey);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _loginResult.Token);

                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    string errorMsg = response.StatusCode switch
                    {
                        System.Net.HttpStatusCode.BadRequest => "Не верный запрос (400)",
                        System.Net.HttpStatusCode.Unauthorized => "Неправильный логин или пароль (401)",
                        System.Net.HttpStatusCode.Forbidden => "Доступ запрещён (403)",
                        System.Net.HttpStatusCode.NotFound => "Ресурс не найден (404)",
                        System.Net.HttpStatusCode.InternalServerError => "Ошибка сервера (500)",
                        _ => $"Unknown error: {(int)response.StatusCode}"
                    };

                    _reporter.Log($"[API] Ошибка получения инфо сервера: {errorMsg}", "WARN");

                    return new ServerInfo
                    {
                        Success = false,
                        ErrorMessage = errorMsg
                    };
                }

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var serverInfo = await response.Content.ReadFromJsonAsync<ServerInfo>(options, cancellationToken).ConfigureAwait(false);

                if (serverInfo == null)
                {
                    _reporter.Log("[API] Сервер вернул пустой ответ (null).", "WARN");
                    return new ServerInfo
                    {
                        Success = false,
                        ErrorMessage = "Сервер вернул пустой ответ"
                    };
                }

                serverInfo.Success = true;
                _reporter.Log("[API] Информация о сервере успешно получена.", "INFO"); 
                return serverInfo;
            }
            catch (HttpRequestException ex)
            {
                _reporter.Log($"[API] Сетевая ошибка при запросе сервера: {ex.Message}", "ERROR");
                return new ServerInfo { Success = false, ErrorMessage = $"Ошибка сети: {ex.Message}" };
            }
            catch (TaskCanceledException)
            {
                _reporter.Log("[API] Тайм-аут запроса к серверу.", "WARN");
                return new ServerInfo { Success = false, ErrorMessage = "Превышено время ожидания" };
            }
            catch (Exception ex)
            {
                _reporter.Log($"[API] Неизвестная ошибка: {ex.Message}", "ERROR");
                return new ServerInfo { Success = false, ErrorMessage = $"Неизвестная ошибка: {ex.Message}" };
            }
        }
    }
}