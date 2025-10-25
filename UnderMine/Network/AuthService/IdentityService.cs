using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using UnderMine.Models;

namespace UnderMine.Network.AuthService
{
    public class IdentityService
    {
        private readonly Config _config;
        private readonly HttpClient _httpClient;
        
        public IdentityService(Config config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            
            _httpClient = new HttpClient()
            {
                BaseAddress = new Uri(_config.BaseUrl),
                Timeout = TimeSpan.FromSeconds(30)
            };

            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json")
            );
        }
        
        public async Task<LoginResult> Login(string username, string password)
        {
            string logUrl = _config.BaseUrl + _config.LogApiKey;

            try
            {
                var loginData = new { Login = username, Password = password };
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var json = JsonSerializer.Serialize(loginData);


                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(logUrl, content);
                

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();

                    var tokenResponse = JsonSerializer.Deserialize<LoginResult>(responseContent, options);

                    if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.Token))
                    {
                        return new LoginResult
                        {
                            Success = false,
                            ErrorMessage = $"Error: empty token. Code {response.StatusCode}",
                        };
                    }
                    
                    return new LoginResult { Success = true, Token = tokenResponse.Token };
                }
                else
                {
                    return new LoginResult
                    {
                        Success = false,
                        ErrorMessage = response.StatusCode switch
                        {
                            System.Net.HttpStatusCode.BadRequest => "Не верный запрос (400)",
                            System.Net.HttpStatusCode.Unauthorized => "Неправильный логин или пароль (401)",
                            System.Net.HttpStatusCode.Forbidden => "Доступ запрещён (403)",
                            System.Net.HttpStatusCode.NotFound => "Ресурс не найден (404)",
                            System.Net.HttpStatusCode.InternalServerError => "Ошибка сервера (500)",
                            _ => $"Неизвестная ошибка: {(int)response.StatusCode}"
                        },
                    };
                }
            }
            catch (Exception ex)
            {
                return new LoginResult { Success = false, ErrorMessage = $"Ошибка авторизации: {ex.Message}" };
            }
        }

        public async Task<LoginResult> Register(string username, string password)
        {
            string regUrl =  _config.BaseUrl + _config.RegApiKey;

            try
            {
                var registerData = new { Login = username, Password = password };
                var json = JsonSerializer.Serialize(registerData);

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(regUrl, content);
                await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    return new LoginResult { Success = true, ErrorMessage = string.Empty };
                }
                
                return new LoginResult
                {
                    Success = false,
                    ErrorMessage = response.StatusCode switch
                    {
                        System.Net.HttpStatusCode.BadRequest => $"Неверный запрос (400)",
                        System.Net.HttpStatusCode.Conflict => $"Пользователь уже существует (409)",
                        System.Net.HttpStatusCode.InternalServerError => $"Ошибка сервера (500)",
                        _ => $"Неизвестная ошибка {(int)response.StatusCode}:"
                    }
                };
            }
            catch (HttpRequestException ex)
            {
                return new LoginResult { Success = false, ErrorMessage = $"Ошибка подключения. Проверьте подключение к сети. {ex.Message}" };
            }
            catch (TaskCanceledException)
            {
                return new LoginResult { Success = false, ErrorMessage = "Превышено время ожидания." };
            }
            catch (Exception ex)
            {
                return new LoginResult { Success = false, ErrorMessage = $"Неизвестная ошибка: {ex.Message}" };
            }
        }
    }
}
