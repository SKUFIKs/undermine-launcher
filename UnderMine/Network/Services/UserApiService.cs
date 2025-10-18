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
        private readonly Config _config;
        private readonly LoginResult _loginResult;
        private readonly HttpClient _httpClient;

        public UserApiService(Config config, LoginResult loginResult)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _loginResult = loginResult ?? throw new ArgumentNullException(nameof(loginResult));

            var handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

            _httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri(_config.BaseUrl),
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
                    ErrorMessage = "Error: empty token."
                };
            }

            try
            {
                var requestUrl = _config.BaseUrl + _config.EntranceApiKey;
                
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _loginResult.Token);

                var response = await _httpClient.GetAsync(requestUrl);
                var content = await response.Content.ReadAsStringAsync();
                
                if (!response.IsSuccessStatusCode)
                {
                    string errorMsg = response.StatusCode switch
                    {
                        System.Net.HttpStatusCode.BadRequest => "Bad request (400)",
                        System.Net.HttpStatusCode.Unauthorized => "Authorization required (401)",
                        System.Net.HttpStatusCode.Forbidden => "Access denied (403)",
                        System.Net.HttpStatusCode.NotFound => "Resource not found (404)",
                        System.Net.HttpStatusCode.InternalServerError => "Server error (500)",
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
                        ErrorMessage = "Server returned empty response"
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
                    ErrorMessage = $"Unknown error: {ex.Message}"
                };
            }
        }
    }
}
