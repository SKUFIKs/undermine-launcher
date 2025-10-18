using System.Text.Json.Serialization;

namespace UnderMine.Models;

public class Config
{
    public string BaseUrl { get; set; } = "https://93.90.233.165:49152";
    public string LogApiKey { get; set; } = "/api/auth/login";
    public string RegApiKey { get; set; } = "/api/auth/register";
    public string EntranceApiKey { get; set; } = "/api/v0.1/server_info";
    
}

public class ServerInfo 
{
    
    [JsonPropertyName("username")]
    public string Username { get; init; } = null!;

    [JsonPropertyName("versionMC1")]
    public string VersionMc1 { get; init; } = null!;

    [JsonPropertyName("forgeV1")]
    public string ForgeV1 {get; init; } = null!;

    [JsonPropertyName("manifestLink1")]
    public string ManifestLink1   { get; init; } = null!;

    [JsonPropertyName("versionMC2")]
    public string VersionMc2 {get; init;} = null!;

    [JsonPropertyName("forgeV2")]
    public string ForgeV2 {get; init;} = null!;

    [JsonPropertyName("manifestLink2")]
    public string ManifestLink2 { get; init;} = null!;
    
    [JsonPropertyName("ConfLink1")]
    public string ConfLink1 { get; init;} = null!;
    
    [JsonPropertyName("GuiHach1")]
    public string GuiHach1  {get; init;} = null!;
    
    public bool Success { get; set; }
    
    public string ErrorMessage { get; set; } = string.Empty;
}

public class LoginResult
{
    [JsonPropertyName("token")] 
    public string Token { get; init; } = string.Empty;
    
    [JsonPropertyName("success")]
    public bool Success { get; init; }
    
    [JsonPropertyName("message")]
    public string ErrorMessage { get; init; } = string.Empty;
}

public class SystemInfo
{
    public double TotalMemory { get; set; }
}
