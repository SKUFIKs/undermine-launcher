using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UnderMine.Services;

namespace UnderMine.Core.Game
{
    /// <summary>
    /// Locates a Java runtime able to run a build, downloading a Temurin (Adoptium) JRE when the
    /// machine does not have a suitable one. Resolution order:
    /// user-configured path -> bundled instance runtime -> system Java -> downloaded JRE.
    /// </summary>
    public class JavaRuntimeService
    {
        // Downloads (JRE archives) can be tens of megabytes; a fixed 30s timeout would abort
        // them, so this service owns a client without a request timeout (cancellation instead).
        private static readonly HttpClient DownloadClient = new() { Timeout = Timeout.InfiniteTimeSpan };

        private readonly ProgressService _reporter;
        private readonly ILogger<JavaRuntimeService> _logger;

        public JavaRuntimeService(ProgressService reporter, ILogger<JavaRuntimeService> logger)
        {
            _reporter = reporter;
            _logger = logger;
        }

        /// <summary>
        /// Resolves a Java executable with at least <paramref name="requiredMajor"/>.
        /// Returns null when nothing suitable is available.
        /// </summary>
        public async Task<string?> ResolveJavaAsync(
            string? preferredPath,
            string launcherRoot,
            int requiredMajor,
            CancellationToken ct)
        {
            // 1. User-configured executable.
            if (!string.IsNullOrWhiteSpace(preferredPath) && File.Exists(preferredPath))
            {
                var major = GetMajorVersion(preferredPath);
                if (major <= 0 || major >= requiredMajor)
                    return preferredPath;

                _reporter.Log($"[Java] Configured Java {major} is older than required {requiredMajor}; ignoring {preferredPath}", "WARN");
            }

            // 2. Runtime already provisioned next to the launcher data.
            var runtimeRoot = Path.Combine(launcherRoot, "runtime");
            if (Directory.Exists(runtimeRoot))
            {
                var bundled = PickBestJava(EnumerateJavaExecutables(runtimeRoot), requiredMajor, out var bundledMajor);
                if (bundled != null)
                {
                    _reporter.Log($"[Java] Using bundled runtime (Java {bundledMajor}): {bundled}");
                    return bundled;
                }
            }

            // 3. System Java (JAVA_HOME, PATH, well-known install roots).
            var systemJava = PickBestJava(FindSystemJava(), requiredMajor, out var systemMajor);
            if (systemJava != null)
            {
                _reporter.Log($"[Java] Using system Java (Java {systemMajor}): {systemJava}");
                return systemJava;
            }

            // 4. Download Temurin.
            _reporter.Log($"[Java] No Java {requiredMajor}+ found, downloading Temurin JRE {requiredMajor}...");
            return await DownloadTemurinAsync(launcherRoot, requiredMajor, ct);
        }

        /// <summary>Reads the major version of a java executable ("21", "8", ...). 0 when unknown.</summary>
        public static int GetMajorVersion(string javaExe)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = javaExe,
                    Arguments = "-version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null) return 0;

                // `java -version` prints to stderr on all JDKs.
                var output = process.StandardError.ReadToEnd() + process.StandardOutput.ReadToEnd();
                process.WaitForExit(5000);

                return ParseMajorVersion(output);
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>Parses a major version out of `java -version` output.</summary>
        public static int ParseMajorVersion(string output)
        {
            if (string.IsNullOrEmpty(output)) return 0;

            var firstQuote = output.IndexOf('"');
            if (firstQuote < 0) return 0;
            var secondQuote = output.IndexOf('"', firstQuote + 1);
            if (secondQuote < 0) return 0;

            var version = output.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
            var parts = version.Split('.', '_', '-', '+');
            if (parts.Length == 0 || !int.TryParse(parts[0], out var major)) return 0;

            // Legacy "1.8.0_402" -> 8
            if (major == 1 && parts.Length > 1 && int.TryParse(parts[1], out var legacy))
                return legacy;

            return major;
        }

        /// <summary>
        /// Returns the executable whose major version is the smallest value &gt;= <paramref name="requiredMajor"/>
        /// (the closest match), or null when none qualifies. Preferring the closest match keeps a build
        /// on the Java major version it targets (e.g. 21) instead of an arbitrary newer one (e.g. 25).
        /// </summary>
        private static string? PickBestJava(IEnumerable<string> executables, int requiredMajor, out int selectedMajor)
        {
            string? best = null;
            var bestMajor = int.MaxValue;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var exe in executables)
            {
                if (!seen.Add(exe)) continue;

                var major = GetMajorVersion(exe);
                if (major < requiredMajor) continue;

                if (major < bestMajor)
                {
                    best = exe;
                    bestMajor = major;
                }
            }

            selectedMajor = bestMajor == int.MaxValue ? 0 : bestMajor;
            return best;
        }

        private static IEnumerable<string> EnumerateJavaExecutables(string root)
        {
            string[] names = OperatingSystem.IsWindows()
                ? new[] { "java.exe" }
                : new[] { "java" };

            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories); }
            catch { yield break; }

            foreach (var file in files)
            {
                if (names.Contains(Path.GetFileName(file), StringComparer.OrdinalIgnoreCase))
                    yield return file;
            }
        }

        private static IEnumerable<string> FindSystemJava()
        {
            var candidates = new List<string>();

            var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
            if (!string.IsNullOrEmpty(javaHome))
            {
                candidates.Add(Path.Combine(javaHome, "bin", "java.exe"));
                candidates.Add(Path.Combine(javaHome, "bin", "java"));
            }

            var path = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(path))
            {
                foreach (var rawDir in path.Split(Path.PathSeparator))
                {
                    var dir = rawDir.Trim().Trim('"');
                    if (string.IsNullOrEmpty(dir)) continue;

                    candidates.Add(Path.Combine(dir, "java.exe"));
                    candidates.Add(Path.Combine(dir, "java"));
                }
            }

            if (OperatingSystem.IsWindows())
            {
                foreach (var programFiles in new[]
                         {
                             Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                             Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
                         })
                {
                    if (string.IsNullOrEmpty(programFiles)) continue;

                    foreach (var vendor in new[] { "Java", "Eclipse Adoptium", "Microsoft", "Amazon Corretto", "Zulu" })
                    {
                        foreach (var dir in SafeDirectories(Path.Combine(programFiles, vendor)))
                            candidates.Add(Path.Combine(dir, "bin", "java.exe"));
                    }
                }
            }
            else
            {
                foreach (var jvm in SafeDirectories("/usr/lib/jvm"))
                    candidates.Add(Path.Combine(jvm, "bin", "java"));
            }

            return candidates.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private static IEnumerable<string> SafeDirectories(string root)
        {
            try
            {
                return Directory.Exists(root)
                    ? Directory.EnumerateDirectories(root)
                    : Enumerable.Empty<string>();
            }
            catch
            {
                return Enumerable.Empty<string>();
            }
        }

        private async Task<string?> DownloadTemurinAsync(string launcherRoot, int major, CancellationToken ct)
        {
            var runtimeRoot = Path.Combine(launcherRoot, "runtime");
            Directory.CreateDirectory(runtimeRoot);

            var targetDir = Path.Combine(runtimeRoot, $"jre-{major}");

            // Reuse an already downloaded runtime if it is complete.
            if (Directory.Exists(targetDir))
            {
                var existing = EnumerateJavaExecutables(targetDir).FirstOrDefault();
                if (existing != null && GetMajorVersion(existing) >= major)
                    return existing;
            }

            var (os, arch) = GetAdoptiumPlatform();
            var url = $"https://api.adoptium.net/v3/binary/latest/{major}/ga/{os}/{arch}/jre/hotspot/normal/eclipse";

            var isZip = OperatingSystem.IsWindows();
            var archivePath = Path.Combine(runtimeRoot, $"jre-{major}{(isZip ? ".zip" : ".tar.gz")}");

            try
            {
                await DownloadFileAsync(url, archivePath, ct);

                var tempDir = Path.Combine(runtimeRoot, $"jre-{major}-extract");
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
                Directory.CreateDirectory(tempDir);

                if (isZip)
                {
                    ZipFile.ExtractToDirectory(archivePath, tempDir, overwriteFiles: true);
                }
                else
                {
                    await using var file = File.OpenRead(archivePath);
                    await using var gzip = new GZipStream(file, CompressionMode.Decompress);
                    TarFile.ExtractToDirectory(gzip, tempDir, overwriteFiles: true);
                }

                // Temurin archives wrap everything in a single top-level directory.
                var top = Directory.EnumerateDirectories(tempDir).FirstOrDefault()
                          ?? throw new InvalidOperationException("Unexpected JRE archive layout");

                if (Directory.Exists(targetDir)) Directory.Delete(targetDir, true);
                Directory.Move(top, targetDir);
                Directory.Delete(tempDir, true);

                var javaExe = EnumerateJavaExecutables(targetDir).FirstOrDefault();
                if (javaExe == null) throw new InvalidOperationException("Java executable not found in downloaded JRE");

                _reporter.Log($"[Java] Temurin JRE {major} installed: {javaExe}");
                return javaExe;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download/install Temurin JRE {Major}", major);
                _reporter.Log($"[Java] Failed to install Java {major}: {ex.Message}", "ERROR");
                return null;
            }
            finally
            {
                try { if (File.Exists(archivePath)) File.Delete(archivePath); } catch { }
            }
        }

        private async Task DownloadFileAsync(string url, string path, CancellationToken ct)
        {
            using var response = await DownloadClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            var total = response.Content.Headers.ContentLength;
            await using var source = await response.Content.ReadAsStreamAsync(ct);
            await using var target = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

            var buffer = new byte[81920];
            long read0 = 0;
            int read;
            while ((read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), ct);
                read0 += read;

                if (total is > 0)
                    await _reporter.ReportAsync("java", Math.Clamp((double)read0 / total.Value, 0, 0.9),
                        $"Downloading Java {FormatMb(read0)}/{FormatMb(total.Value)}");
            }
        }

        private static string FormatMb(long bytes) => $"{bytes / 1048576.0:0.0} MB";

        private static (string Os, string Arch) GetAdoptiumPlatform()
        {
            var os = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows"
                : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "mac"
                : "linux";

            var arch = RuntimeInformation.OSArchitecture switch
            {
                Architecture.Arm64 => "aarch64",
                Architecture.X86 => "x32",
                _ => "x64"
            };

            return (os, arch);
        }
    }
}
