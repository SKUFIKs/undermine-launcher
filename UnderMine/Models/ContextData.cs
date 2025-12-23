using System.Text.Json.Serialization;

namespace UnderMine.Models;

public class ContextData
{
    public string BaseUrl { get; set; } = "https://api-undermine.ru";
    //public string BaseUrl { get; set; } = "http://localhost:8080";
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
    
    [JsonPropertyName("ShadersLink")]
    public string ShadersLink { get; init;} = null!;
    
    [JsonPropertyName("InstallerLink")]
    public string InstallerLink  {get; init;} = null!;
    [JsonPropertyName("KubejsLink")]
    public string KubejsLink  {get; init;} = null!;
    
    [JsonPropertyName("ConfLink")]
    public string ConfLink  {get; init;} = null!;

    [JsonPropertyName("LibraryExpectedChecksum")]
    public string LibraryExpectedChecksum { get; init; } = "750358cbb093fcdbd129127b5df493800b9cb9d6";
    [JsonPropertyName("VersionExpectedChecksum")]
    public string VersionExpectedChecksum { get; init; } = "5be32f73845fc8ef6927c2d1decc72eccc161be2";
    [JsonPropertyName("AssetsExpectedChecksum")]
    public string AssetsExpectedChecksum { get; init; } = "empty";


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
