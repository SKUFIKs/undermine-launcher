using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using UnderMine.Models;
using UnderMine.Services;

namespace UnderMine.Core.Network
{
    public class UserClient
    {
        private readonly ContextData _contextData;
        private readonly HttpClient _httpClient;
        private readonly ProgressService _reporter;

        public UserClient(HttpClient httpClient, ContextData contextData, ProgressService reporter)
        {
            _httpClient = httpClient;
            _reporter = reporter;
            _contextData = contextData;
        }

        public async Task<UserDto> GetUserInfo(LoginResult loginResult, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(loginResult.Token)) throw new ArgumentException("Token is missing");

            var requestUrl = $"{_contextData.BaseUrl?.TrimEnd('/')}{_contextData.UserInfoApiKey}";
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);

            var token = loginResult.Token;
            _reporter.Log($"Using token (first 10 chars): {token?.Substring(0, Math.Min(10, token.Length))}...");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            try
            {
                _reporter.Log("Fetching user data...");
                _reporter.Log(requestUrl);

                using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);

                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<UserDto>(cancellationToken: ct)
                             ?? throw new InvalidOperationException("Server returned empty body");
                _reporter.Log($"User data received: {result}");
                return result;
            }
            catch (HttpRequestException ex)
            {
                _reporter.Log($"Network error: {ex.StatusCode}");
                throw;
            }
        }

        public async Task<UserProfileDto> GetUserStatistics(LoginResult loginResult, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(loginResult.Token)) throw new ArgumentException("Token is missing");

            var requestUrl = $"{_contextData.BaseUrl?.TrimEnd('/')}{_contextData.UserStatsApiKey}";
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", loginResult.Token);

            try
            {
                using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);

                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<UserProfileDto>(cancellationToken: ct)
                             ?? throw new InvalidOperationException("Server returned empty body");

                return result;
            }
            catch (HttpRequestException ex)
            {
                _reporter.Log($"Network error: {ex.StatusCode}");
                throw;
            }
        }

        public async Task<List<ServerList>> GetServerList(LoginResult loginResult, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(loginResult.Token)) throw new ArgumentException("Token is missing");

            var requestUrl = $"{_contextData.BaseUrl?.TrimEnd('/')}{_contextData.ServerListApiKey}";
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", loginResult.Token);

            try
            {
                using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);

                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<List<ServerList>>(cancellationToken: ct)
                             ?? throw new InvalidOperationException("Empty response");

                return result;
            }
            catch (HttpRequestException ex)
            {
                _reporter.Log($"Network error: {ex.StatusCode}");
                throw;
            }
        }

        public async Task<ServerList> GetServerConfig(LoginResult loginResult, CancellationToken ct, int serverId)
        {
            if (string.IsNullOrEmpty(loginResult.Token)) throw new ArgumentException("Token is missing");

            var requestUrl = $"{_contextData.BaseUrl?.TrimEnd('/')}{_contextData.ServerListApiKey}/{serverId}";
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", loginResult.Token);

            try
            {
                using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);

                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<ServerList>(cancellationToken: ct)
                             ?? throw new InvalidOperationException("Server returned empty body");

                return result;
            }
            catch (HttpRequestException ex)
            {
                _reporter.Log($"Network error: {ex.StatusCode}");
                throw;
            }
        }

        public async Task<BuildManifest> GetBuildManifest(LoginResult loginResult, CancellationToken ct, int serverId)
        {
            if (string.IsNullOrEmpty(loginResult.Token)) throw new ArgumentException("Token is missing");

            var requestUrl = $"{_contextData.BaseUrl?.TrimEnd('/')}{_contextData.ServerListApiKey}/{serverId}/manifest";
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", loginResult.Token);

            try
            {
                using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);

                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<BuildManifest>(cancellationToken: ct)
                             ?? throw new InvalidOperationException("Server returned empty body");

                return result;
            }
            catch (HttpRequestException ex)
            {
                _reporter.Log($"Network error: {ex.StatusCode}");
                throw;
            }
        }
    }
}
