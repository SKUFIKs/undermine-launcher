using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using UnderMine.Models;

namespace UnderMine.Core.Game
{
    /// <summary>
    /// Builds and launches the Minecraft process with the correct JVM arguments,
    /// classpath, native library path and game arguments based on the build manifest.
    /// </summary>
    public class ProcessLauncher
    {
        private readonly ILogger<ProcessLauncher> _logger;

        public ProcessLauncher(ILogger<ProcessLauncher> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Builds the process start information for launching Minecraft.
        /// </summary>
        public ProcessStartInfo BuildProcess(
            BuildManifest manifest,
            string gameDir,
            string javaPath,
            string username,
            string uuid,
            string accessToken,
            int maxMemoryMb = 4096,
            string? nativesDir = null,
            string launcherName = "UnderMineLauncher",
            string launcherVersion = "1.0",
            bool fullscreen = false)
        {
            nativesDir ??= NativesExtractor.GetNativesDirectory(gameDir);

            if (string.IsNullOrWhiteSpace(launcherName)) launcherName = "UnderMineLauncher";
            if (string.IsNullOrWhiteSpace(launcherVersion)) launcherVersion = "1.0";

            var substitutions = BuildSubstitutions(
                manifest, gameDir, nativesDir, username, uuid, accessToken, launcherName, launcherVersion);

            // Forge/NeoForge profiles ship their own "-p" module path. It has to be resolved before
            // the classpath is built: those jars must not also appear on the classpath, otherwise
            // BootstrapLauncher aborts with "Module ... was already on the JVMs module path ...
            // but class-path contains it at location ...".
            var modulePath = ExtractModulePath(manifest.JvmArgs.Select(a => Substitute(a, substitutions)));
            var classpath = BuildClasspath(manifest, gameDir, modulePath);
            substitutions["classpath"] = classpath;

            var args = new List<string>
            {
                $"-Xmx{Math.Max(1024, maxMemoryMb)}M",
                $"-Xms{Math.Max(512, Math.Min(maxMemoryMb / 2, 2048))}M",
                $"-Djava.library.path={nativesDir}",
                $"-Dorg.lwjgl.librarypath={nativesDir}",
                $"-Dminecraft.launcher.brand={launcherName}",
                $"-Dminecraft.launcher.version={launcherVersion}"

            };

            foreach (var raw in manifest.JvmArgs)
                args.Add(Substitute(raw, substitutions));

            if (fullscreen && !args.Contains("--fullscreen", StringComparer.OrdinalIgnoreCase))
            {
                args.Add("--fullscreen");
            }

            var hasClasspathArg = args.Any(a =>
                a.Equals("-cp", StringComparison.OrdinalIgnoreCase) ||
                a.Equals("-classpath", StringComparison.OrdinalIgnoreCase));

            if (!hasClasspathArg)
            {
                args.Add("-cp");
                args.Add(classpath);
            }

            // Legacy profiles (and the API fallback) require modules to be resolved from the
            // classpath; modern Forge/NeoForge profiles already ship their own "-p" module path
            // (resolved from ${library_directory}) so this is only added when missing.
            var loader = (manifest.LoaderType ?? string.Empty).ToLowerInvariant();
            if ((loader == "forge" || loader == "neoforge") &&
                !args.Any(a => a.Contains("ALL-MODULE-PATH", StringComparison.OrdinalIgnoreCase)))
            {
                args.Add("--add-modules");
                args.Add("ALL-MODULE-PATH");
            }

            args.Add(manifest.MainClass);

            foreach (var raw in manifest.GameArgs)
                args.Add(Substitute(raw, substitutions));

            var startInfo = new ProcessStartInfo
            {
                FileName = javaPath,
                WorkingDirectory = gameDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            // ArgumentList keeps paths/arguments quoted correctly on every platform.
            foreach (var arg in args)
                startInfo.ArgumentList.Add(arg);

            startInfo.Environment["GAME_DIR"] = gameDir;
            var javaHome = Path.GetDirectoryName(Path.GetDirectoryName(javaPath));
            if (!string.IsNullOrEmpty(javaHome))
                startInfo.Environment["JAVA_HOME"] = javaHome;

            _logger.LogInformation("Launch command: {Java} {Args}", javaPath, string.Join(' ', args));
            _logger.LogInformation("Classpath entries: {Count}", classpath.Count(c => c == Path.PathSeparator) + 1);

            return startInfo;
        }

        /// <summary>
        /// Launches the process and returns it. Output is forwarded to the logger.
        /// </summary>
        public Process Launch(ProcessStartInfo startInfo)
        {
            _logger.LogInformation("Launching process: {FileName}", startInfo.FileName);

            var process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };

            process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    _logger.LogInformation("[GAME] {Output}", e.Data);
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    _logger.LogWarning("[GAME] {Error}", e.Data);
            };

            if (!process.Start())
                throw new InvalidOperationException("Failed to start the Minecraft process.");

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            _logger.LogInformation("Process started with PID {ProcessId}", process.Id);
            return process;
        }

        /// <summary>
        /// Collects the jars that are handed to the JVM through the module path ("-p",
        /// "--module-path", "-module-path" or an inline "<c>--module-path=...</c>" form).
        /// </summary>
        private static IReadOnlyCollection<string> ExtractModulePath(IEnumerable<string> jvmArgs)
        {
            var args = jvmArgs.ToList();
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < args.Count; i++)
            {
                var arg = args[i];
                if (string.IsNullOrEmpty(arg))
                    continue;

                string? value = null;
                if (arg.Equals("-p", StringComparison.Ordinal) ||
                    arg.Equals("-module-path", StringComparison.Ordinal) ||
                    arg.Equals("--module-path", StringComparison.Ordinal))
                {
                    if (i + 1 < args.Count)
                        value = args[i + 1];
                }
                else if (arg.StartsWith("-p=", StringComparison.Ordinal))
                {
                    value = arg[3..];
                }
                else if (arg.StartsWith("--module-path=", StringComparison.Ordinal))
                {
                    value = arg["--module-path=".Length..];
                }

                if (string.IsNullOrWhiteSpace(value))
                    continue;

                foreach (var part in value.Split(Path.PathSeparator, StringSplitOptions.TrimEntries))
                {
                    if (string.IsNullOrEmpty(part))
                        continue;

                    paths.Add(SafeFullPath(part.Trim('"').Trim('\'')));
                }
            }

            return paths;
        }

        /// <summary>
        /// The game classpath: the client jar (versions/&lt;ver&gt;/&lt;ver&gt;.jar) followed by the
        /// libraries the version profile actually declares, minus the ones that already reach the JVM
        /// through the module path and minus the platform natives archives (those are extracted instead).
        /// </summary>
        private static string BuildClasspath(BuildManifest manifest, string gameDir, IReadOnlyCollection<string> modulePath)
        {
            var modulePaths = new HashSet<string>(modulePath, StringComparer.OrdinalIgnoreCase);
            var entries = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var versionJar = ResolveVersionJar(manifest, gameDir);
            if (versionJar != null)
                entries.Add(versionJar);

            var libraries = ResolveDeclaredLibraries(manifest, gameDir);
            if (libraries.Count == 0)
                libraries = GlobLibraries(gameDir);

            foreach (var library in libraries)
            {
                if (modulePaths.Contains(library))
                    continue;

                if (NativesExtractor.IsNativeJar(Path.GetFileName(library)))
                    continue;

                if (seen.Add(library))
                    entries.Add(library);
            }

            return string.Join(Path.PathSeparator, entries);
        }

        /// <summary>
        /// Reads the library list from the version JSON chain (the loader profile and every version it
        /// inherits from). Falling back to "whatever is in libraries/" would smuggle in jars the profile
        /// does not pin - an outdated ASM next to a newer one breaks NeoForge at startup.
        /// </summary>
        private static List<string> ResolveDeclaredLibraries(BuildManifest manifest, string gameDir)
        {
            var versionsDir = Path.Combine(gameDir, "versions");
            if (!Directory.Exists(versionsDir))
                return new List<string>();

            var profileId = ResolveProfileId(versionsDir, manifest);
            if (string.IsNullOrEmpty(profileId))
                return new List<string>();

            if (!File.Exists(Path.Combine(versionsDir, profileId, profileId + ".json")))
                return new List<string>();

            var chain = LoadVersionChain(versionsDir, profileId);
            if (chain.Count == 0)
                return new List<string>();

            // Base first, leaf last: a loader profile overrides the version the base declares.
            var order = new List<string>();
            var libraries = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

            for (var i = chain.Count - 1; i >= 0; i--)
            {
                if (!chain[i].TryGetProperty("libraries", out var declared) || declared.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var library in declared.EnumerateArray())
                {
                    if (library.ValueKind != JsonValueKind.Object)
                        continue;

                    var key = LibraryKey(library);
                    if (key.Length == 0)
                        continue;

                    if (!libraries.ContainsKey(key))
                        order.Add(key);

                    libraries[key] = library.Clone();
                }
            }

            var results = new List<string>();
            foreach (var key in order)
            {
                if (!IsAllowedByRules(libraries[key]))
                    continue;

                var path = ResolveLibraryPath(libraries[key], gameDir);
                if (path != null)
                    results.Add(path);
            }

            return results;
        }

        /// <summary>
        /// Picks the version profile to launch: the loader profile for modded builds, the vanilla
        /// version id otherwise.
        /// </summary>
        private static string? ResolveProfileId(string versionsDir, BuildManifest manifest)
        {
            var loader = (manifest.LoaderType ?? string.Empty).Trim();

            if (loader.Length > 0 &&
                !loader.Equals("vanilla", StringComparison.OrdinalIgnoreCase) &&
                !loader.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                var candidates = Directory
                    .EnumerateDirectories(versionsDir)
                    .Select(Path.GetFileName)
                    .Where(n => !string.IsNullOrEmpty(n) &&
                                !string.Equals(n, manifest.GameVersion, StringComparison.OrdinalIgnoreCase))
                    .Cast<string>()
                    .ToList();

                if (candidates.Count > 0)
                {
                    if (!string.IsNullOrEmpty(manifest.LoaderVersion))
                    {
                        var byVersion = candidates.FirstOrDefault(
                            n => n.Contains(manifest.LoaderVersion, StringComparison.OrdinalIgnoreCase));
                        if (byVersion != null)
                            return byVersion;
                    }

                    var byLoader = candidates.FirstOrDefault(
                        n => n.Contains(loader, StringComparison.OrdinalIgnoreCase));
                    return byLoader ?? candidates[0];
                }
            }

            return string.IsNullOrEmpty(manifest.GameVersion) ? null : manifest.GameVersion;
        }

        /// <summary>Loads the profile and every version it inherits from, leaf first.</summary>
        private static List<JsonElement> LoadVersionChain(string versionsDir, string profileId)
        {
            var chain = new List<JsonElement>();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var current = profileId;
            while (!string.IsNullOrEmpty(current) && visited.Add(current!))
            {
                var file = Path.Combine(versionsDir, current!, current! + ".json");
                if (!File.Exists(file))
                    break;

                JsonDocument? document = null;
                try
                {
                    document = JsonDocument.Parse(File.ReadAllText(file));
                }
                catch (Exception e) when (e is JsonException or IOException)
                {
                    break;
                }

                if (document == null)
                    break;

                using (document)
                {
                    chain.Add(document.RootElement.Clone());

                    current = document.RootElement.TryGetProperty("inheritsFrom", out var inherits) &&
                              inherits.ValueKind == JsonValueKind.String
                        ? inherits.GetString()
                        : null;
                }
            }

            return chain;
        }

        /// <summary>
        /// Identity of a library for merge purposes: group + artifact + classifier. The version is
        /// deliberately left out - that is exactly what a loader profile overrides (NeoForge pins a
        /// newer ASM than the vanilla version declares, and both must not reach the classpath).
        /// </summary>
        private static string LibraryKey(JsonElement library)
        {
            if (!library.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String)
                return string.Empty;

            var parts = name.GetString()!.Split(':');
            if (parts.Length < 3)
                return string.Empty;

            return parts.Length > 3 && parts[3].Length > 0
                ? $"{parts[0]}:{parts[1]}:{parts[3]}"
                : $"{parts[0]}:{parts[1]}";
        }

        /// <summary>Applies the Mojang rule format: the last matching rule wins, no match means denied.</summary>
        private static bool IsAllowedByRules(JsonElement library)
        {
            if (!library.TryGetProperty("rules", out var rules) ||
                rules.ValueKind != JsonValueKind.Array ||
                rules.GetArrayLength() == 0)
                return true;

            var allowed = false;
            var osName = CurrentOsName();

            foreach (var rule in rules.EnumerateArray())
            {
                if (rule.ValueKind != JsonValueKind.Object)
                    continue;

                var matches = true;

                if (rule.TryGetProperty("os", out var os) &&
                    os.ValueKind == JsonValueKind.Object &&
                    os.TryGetProperty("name", out var name) &&
                    name.ValueKind == JsonValueKind.String &&
                    !string.Equals(name.GetString(), osName, StringComparison.OrdinalIgnoreCase))
                {
                    matches = false;
                }

                if (matches)
                {
                    // Feature rules only exist for optional libraries we never enable here.
                    var action = rule.TryGetProperty("action", out var a) && a.ValueKind == JsonValueKind.String
                        ? a.GetString()
                        : null;

                    allowed = string.Equals(action, "allow", StringComparison.OrdinalIgnoreCase);
                }
            }

            return allowed;
        }

        private static string CurrentOsName()
        {
            if (OperatingSystem.IsWindows()) return "windows";
            if (OperatingSystem.IsMacOS()) return "osx";
            if (OperatingSystem.IsLinux()) return "linux";
            return "unknown";
        }

        /// <summary>Resolves the on-disk jar for a library entry (artifact path, else name convention).</summary>
        private static string? ResolveLibraryPath(JsonElement library, string gameDir)
        {
            var librariesDir = Path.Combine(gameDir, "libraries");

            if (library.TryGetProperty("downloads", out var downloads) &&
                downloads.ValueKind == JsonValueKind.Object &&
                downloads.TryGetProperty("artifact", out var artifact) &&
                artifact.ValueKind == JsonValueKind.Object &&
                artifact.TryGetProperty("path", out var path) &&
                path.ValueKind == JsonValueKind.String)
            {
                var relative = path.GetString()!.Replace('/', Path.DirectorySeparatorChar);
                return SafeFullPath(Path.Combine(librariesDir, relative));
            }

            if (!library.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String)
                return null;

            var parts = name.GetString()!.Split(':');
            if (parts.Length < 3)
                return null;

            var artifactId = parts[1];
            var version = parts[2];
            var fileName = parts.Length > 3 && parts[3].Length > 0
                ? $"{artifactId}-{version}-{parts[3]}.jar"
                : $"{artifactId}-{version}.jar";

            var relativePath = Path.Combine(
                parts[0].Replace('.', Path.DirectorySeparatorChar),
                artifactId,
                version,
                fileName);

            return SafeFullPath(Path.Combine(librariesDir, relativePath));
        }

        private static string SafeFullPath(string path)
        {
            try
            {
                return Path.GetFullPath(path);
            }
            catch
            {
                return path;
            }
        }

        /// <summary>Fallback for builds that ship no readable version profile.</summary>
        private static List<string> GlobLibraries(string gameDir)
        {
            var librariesDir = Path.Combine(gameDir, "libraries");
            if (!Directory.Exists(librariesDir))
                return new List<string>();

            return Directory
                .EnumerateFiles(librariesDir, "*.jar", SearchOption.AllDirectories)
                .Where(f => !NativesExtractor.IsNativeJar(Path.GetFileName(f)))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .Select(SafeFullPath)
                .ToList();
        }

        private static string? ResolveVersionJar(BuildManifest manifest, string gameDir)
        {
            if (!string.IsNullOrEmpty(manifest.GameVersion))
            {
                var expected = Path.Combine(gameDir, "versions", manifest.GameVersion, manifest.GameVersion + ".jar");
                if (File.Exists(expected))
                    return expected;
            }

            var versionsDir = Path.Combine(gameDir, "versions");
            if (!Directory.Exists(versionsDir))
                return null;

            return Directory
                .EnumerateFiles(versionsDir, "*.jar", SearchOption.AllDirectories)
                .Where(f => !NativesExtractor.IsNativeJar(Path.GetFileName(f)))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }

        /// <summary>
        /// Builds the placeholder table used by both the version-json argument templates and the
        /// manifest fallback args. Placeholder names differ between Mojang, Forge and the API, so
        /// several aliases map to the same value.
        /// </summary>
        private static Dictionary<string, string> BuildSubstitutions(
            BuildManifest manifest,
            string gameDir,
            string nativesDir,
            string username,
            string uuid,
            string accessToken,
            string launcherName,
            string launcherVersion)
        {
            var assetsDir = Path.Combine(gameDir, "assets");
            var librariesDir = Path.Combine(gameDir, "libraries");
            var assetIndex = ResolveAssetIndex(manifest, gameDir);

            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["username"] = username,
                ["auth_player_name"] = username,
                ["version"] = manifest.GameVersion,
                ["version_name"] = manifest.GameVersion,
                ["gameDir"] = gameDir,
                ["game_directory"] = gameDir,
                ["assetsDir"] = assetsDir,
                ["assets_root"] = assetsDir,
                ["assetIndex"] = assetIndex,
                ["assets_index_name"] = assetIndex,
                ["uuid"] = uuid,
                ["auth_uuid"] = uuid,
                ["accessToken"] = accessToken,
                ["auth_access_token"] = accessToken,
                ["auth_session"] = accessToken,
                ["userType"] = "msa",
                ["user_type"] = "msa",
                ["versionType"] = manifest.DisplayName,
                ["version_type"] = manifest.DisplayName,
                ["library_directory"] = librariesDir,
                ["classpath_separator"] = Path.PathSeparator.ToString(),
                ["natives_directory"] = nativesDir,
                ["launcher_name"] = launcherName,
                ["launcher_version"] = launcherVersion,
                ["resolution_width"] = "1920",
                ["resolution_height"] = "1080"
            };
        }

        private static string Substitute(string value, IReadOnlyDictionary<string, string> map)
        {
            if (string.IsNullOrEmpty(value) || !value.Contains("${", StringComparison.Ordinal))
                return value;

            foreach (var pair in map)
                value = value.Replace("${" + pair.Key + "}", pair.Value, StringComparison.OrdinalIgnoreCase);

            return value;
        }

        /// <summary>
        /// The asset index name is derived from the index file that actually ships in the build
        /// (the API names it after the game version). Reading it from the manifest avoids guessing:
        /// the real Mojang id ("17") would not match the shipped file name.
        /// </summary>
        private static string ResolveAssetIndex(BuildManifest manifest, string gameDir)
        {
            var shippedIndex = manifest.Files
                .Select(f => f.Path)
                .FirstOrDefault(p =>
                    p.StartsWith("assets/indexes/", StringComparison.OrdinalIgnoreCase) &&
                    p.EndsWith(".json", StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(shippedIndex))
                return Path.GetFileNameWithoutExtension(shippedIndex);

            // Older manifests may not list the index; fall back to the version JSON, then version.
            try
            {
                var versionJson = Path.Combine(gameDir, "versions", manifest.GameVersion, manifest.GameVersion + ".json");
                if (File.Exists(versionJson))
                {
                    using var document = JsonDocument.Parse(File.ReadAllText(versionJson));
                    if (document.RootElement.TryGetProperty("assetIndex", out var assetIndex) &&
                        assetIndex.TryGetProperty("id", out var id) &&
                        id.ValueKind == JsonValueKind.String)
                    {
                        return id.GetString()!;
                    }
                }
            }
            catch
            {
                // Fall through to the game version.
            }

            return manifest.GameVersion;
        }
    }
}
