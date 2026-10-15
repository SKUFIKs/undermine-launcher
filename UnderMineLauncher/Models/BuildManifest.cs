using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UnderMine.Models;

/// <summary>
/// Represents a build manifest for client synchronization.
/// </summary>
public class BuildManifest
{
    [JsonPropertyName("buildId")]
    public string BuildId { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("gameVersion")]
    public string GameVersion { get; set; } = string.Empty;

    [JsonPropertyName("loaderType")]
    public string LoaderType { get; set; } = string.Empty;

    [JsonPropertyName("loaderVersion")]
    public string LoaderVersion { get; set; } = string.Empty;

    [JsonPropertyName("javaVersion")]
    public int JavaVersion { get; set; } = 17;

    [JsonPropertyName("mainClass")]
    public string MainClass { get; set; } = string.Empty;

    [JsonPropertyName("jvmArgs")]
    public List<string> JvmArgs { get; set; } = new();

    [JsonPropertyName("gameArgs")]
    public List<string> GameArgs { get; set; } = new();

    [JsonPropertyName("files")]
    public List<ManifestFile> Files { get; set; } = new();
}

/// <summary>
/// Represents a single file entry in the build manifest.
/// </summary>
public class ManifestFile
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("policy")]
    public string Policy { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}

/// <summary>
/// Synchronization policy for manifest files.
/// </summary>
public static class SyncPolicy
{
    public const string ForceSync = "FORCE_SYNC";
    public const string DownloadOnce = "DOWNLOAD_ONCE";
    public const string Preserve = "PRESERVE";
}
