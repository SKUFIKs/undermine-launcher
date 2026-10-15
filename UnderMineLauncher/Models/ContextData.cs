using System;
using System.Text.Json.Serialization;

namespace UnderMine.Models;

public class ContextData
{
    public string BaseUrl { get; set; } = "https://dev.mc-undermine.ru";
    public string LogApiKey { get; set; } = "/api/v1.0/login";
    public string RegApiKey { get; set; } = "/api/v1.0/register";
    public string UserInfoApiKey { get; set; } = "/api/v1.0/user_data";
    public string UserStatsApiKey { get; set; } = "/api/v1.0/user_data/statistics";
    public string ServerListApiKey { get; set; } = "/api/v1.0/servers";
}

public record UserDto(
    string Username,
    Guid Uuid
);

public record UserProfileDto(
    string Username,
    Guid Uuid,
    string Role,
    UserStatsDto Stats
);

public record UserStatsDto(
    int PlayTime,
    int Murders,
    int Deaths
);

public class LoginResult
{
    [JsonPropertyName("token")]
    public string Token { get; init; } = string.Empty;

    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("message")]
    public string ErrorMessage { get; init; } = string.Empty;
}
