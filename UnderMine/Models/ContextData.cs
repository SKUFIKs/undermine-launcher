using Newtonsoft.Json;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UnderMine.Models;

public class ContextData
{
    public string BaseUrl { get; set; } = "https://api-undermine.ru";
    //public string BaseUrl { get; set; } = "http://localhost:8080";
    public string LogApiKey { get; set; } = "/api/auth/login";
    public string RegApiKey { get; set; } = "/api/auth/register";
    public string ServersApiKey { get; set; } = "/api/v0.1/servers";

}

public class JreData
{
    [JsonProperty("version")]
    public required string Version { get; set; }

    [JsonProperty("download_url")] 
    public Dictionary<string, Dictionary<string, JrePlatform>> DownloadUrl { get; set; }
}

public class JrePlatform
{
    [JsonProperty("url")]
    public string Url { get; set; }

    [JsonProperty("sha256")]
    public string Sha256 { get; set; }
}

public class ServerInfo 
{
    [JsonPropertyName("server")]
    public string server { get; init; } = null!;

    [JsonPropertyName("username")]
    public string username { get; set; } = null!;

    [JsonPropertyName("version")]
    public string version { get; init; } = null!;
    [JsonPropertyName("bootloaderType")]
    public string BootloaderType { get; init; } = null!;

    [JsonPropertyName("loader")]
    public string loader { get; init; } = null!;

    [JsonPropertyName("manifestLink")]
    public string ManifestLink   { get; init; } = null!;

    [JsonPropertyName("shadersLink")]
    public string ShadersLink { get; init;} = null!;
    
    [JsonPropertyName("installerLink")]
    public string InstallerLink  {get; init;} = null!;
    [JsonPropertyName("kubejsLink")]
    public string KubejsLink  {get; init;} = null!;
    
    [JsonPropertyName("confLink")]
    public string ConfLink  {get; init;} = null!;
    [JsonPropertyName("clientLink")]
    public string ClientLink { get; init; } = null!;

    [JsonPropertyName("libraryExpectedChecksum")]
    public string LibraryExpectedChecksum { get; init; } = null!;
    [JsonPropertyName("versionExpectedChecksum")]
    public string VersionExpectedChecksum { get; init; } = null!;

    [JsonPropertyName("assetsExpectedChecksum")]
    public string AssetsExpectedChecksum { get; init; } = null!;

    [JsonPropertyName("uiVersion")]
    public string uiVersion { get; init; } = null!;

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

