using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using UnderMine.Fonts.Models;
using UnderMine.Models;
using UnderMine.Services.Logger;
using static UnderMine.IU.MenuWindow;

namespace UnderMine.Network
{
    public class UserApiService
    {
        private readonly ContextData _contextData;
        private readonly LoginResult _loginResult;
        private readonly HttpClient _httpClient;
        private readonly ProgressReporter _reporter;

        public class UserDataResponse
        {
            public string Username { get; set; }
            public List<NewsPart> News { get; set; }
        }

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

            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<List<Server>> GetServersAsync(CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(_loginResult.Token))
            {
                _reporter.Log("[API] Ошибка: отсутствует токен для получения списка серверов.", "WARN");
                return new List<Server>();
            }

            try
            {
                _reporter.Log("[API] Загрузка списка серверов...", "INFO");

                var request = new HttpRequestMessage(HttpMethod.Get, "api/v0.1/servers");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _loginResult.Token);

                using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    _reporter.Log($"[API] Не удалось загрузить сервера. Статус: {response.StatusCode}", "WARN");
                    return new List<Server>();
                }

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var servers = await response.Content.ReadFromJsonAsync<List<Server>>(options, ct);

                if (servers != null)
                {
                    foreach (var s in servers)
                    {
                        _reporter.Log($"[DEBUG] API Server: {s.Name}, User: {s.username ?? "NULL"}", "INFO");
                    }
                }

                _reporter.Log($"[API] Список серверов успешно обновлен ({servers?.Count ?? 0} шт.)", "INFO");
                return servers ?? new List<Server>();
            }
            catch (Exception ex)
            {
                _reporter.Log($"[API] Ошибка при получении списка серверов: {ex.Message}", "ERROR");
                return new List<Server>();
            }
        }

        public async Task<ServerInfo> GetServerInfo(string serverName, CancellationToken cancellationToken = default)
        {
            try
            {
                var servers = await GetServersAsync(cancellationToken);

                if (servers == null || servers.Count == 0)
                    return new ServerInfo { Success = false, ErrorMessage = "Сервера не найдены" };

                var targetServer = servers.FirstOrDefault(s => s.Name == serverName) ?? servers.First();

                var info = targetServer.Config;
                info.Success = true;
                info.username = targetServer.username; 

                return info;
            }
            catch (Exception ex)
            {
                return new ServerInfo { Success = false, ErrorMessage = ex.Message };
            }
        }
        public async Task<UserDataResponse?> GetUserDataAsync(CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(_loginResult.Token)) return null;

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "api/v0.1/get_userdata");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _loginResult.Token);

                using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    return await response.Content.ReadFromJsonAsync<UserDataResponse>(options, ct);
                }
                return null;
            }
            catch (Exception ex)
            {
                _reporter.Log($"[API] Ошибка получения данных пользователя: {ex.Message}", "ERROR");
                return null;
            }
        }
    }
}