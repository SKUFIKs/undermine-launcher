using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using UnderMine.Models;

namespace UnderMine.Network.Services
{
    public class UserApiService
    {
        private readonly ContextData _contextData;
        private readonly LoginResult _loginResult;
        private readonly HttpClient _httpClient;

        public UserApiService(ContextData contextData, LoginResult loginResult)
        {
            _contextData = contextData ?? throw new ArgumentNullException(nameof(contextData));
            _loginResult = loginResult ?? throw new ArgumentNullException(nameof(loginResult));
            _httpClient = new HttpClient()
            {
                BaseAddress = new Uri(_contextData.BaseUrl),
                Timeout = TimeSpan.FromSeconds(30)
            };
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json")
            );
        }

        public async Task<ServerInfo> GetServerInfo()
        {
            if (string.IsNullOrEmpty(_loginResult.Token))
            {
                return new ServerInfo
                {
                    Success = false,
                    ErrorMessage = $"Неизвестная ошибка."
                };
            }

            try
            {
                var requestUrl = _contextData.BaseUrl + _contextData.EntranceApiKey;
                
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _loginResult.Token);

                var response = await _httpClient.GetAsync(requestUrl);
                var content = await response.Content.ReadAsStringAsync();
                
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

                var serverInfo = JsonSerializer.Deserialize<ServerInfo>(content, options);
                
                if (serverInfo == null)
                {
                    return new ServerInfo
                    {
                        Success = false,
                        ErrorMessage = "Сервер вернул пустой ответ"
                    };
                }

                serverInfo.Success = true; 
                return serverInfo;
            }
            catch (Exception ex)
            {
                return new ServerInfo
                {
                    Success = false,
                    ErrorMessage = $"Неизвестная ошибка: {ex.Message}"
                };
            }
        }
    }
}
