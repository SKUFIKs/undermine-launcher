using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UnderMine.Models;
using UnderMine.Services;

namespace UnderMine.Core.Game
{
    /// <summary>
    /// Service for synchronizing game files from a remote manifest.
    /// Implements fast-check, parallel download, hash validation, and safe pruning.
    /// </summary>
    public class ClientSyncService
    {
        // A large sync (tens of thousands of files / hundreds of MB) must not be aborted by the
        // API client's 30s timeout, so downloads use a dedicated client (cancellation instead).
        private static readonly HttpClient DownloadClient = new() { Timeout = Timeout.InfiniteTimeSpan };

        private readonly ProgressService _reporter;
        private readonly ILogger<ClientSyncService> _logger;
        private const int MaxRetries = 3;
        private const int MaxParallelDownloads = 4;

        /// <summary>Directories/files the launcher must never delete, even when absent from the manifest.</summary>
        private static readonly HashSet<string> ProtectedRoots = new(StringComparer.OrdinalIgnoreCase)
        {
            "saves", "screenshots", "logs", "crash-reports", "backups", "local",
            "emblems", "playerdata", "stats", "advancements", "realms",
            // Provisioned by the launcher itself, not part of the build manifest.
            "natives", "runtime"
        };

        private static readonly HashSet<string> ProtectedFiles = new(StringComparer.OrdinalIgnoreCase)
        {
            "servers.dat", "usercache.json", "usernamecache.json",
            "options.txt", "launcher_profiles.json", "manifest.json",
            // Launcher-owned state: tracks that an instance has already been started once.
            FirstLaunchSettings.LaunchedMarkerFileName
        };

        private long _lastProgressTicks;

        public ClientSyncService(ProgressService reporter, ILogger<ClientSyncService> logger)
        {
            _reporter = reporter;
            _logger = logger;
        }

        /// <summary>
        /// Synchronizes the local game directory with the remote manifest.
        /// </summary>
        public async Task<SyncResult> SyncAsync(
            BuildManifest manifest,
            string gameDir,
            CancellationToken ct = default)
        {
            var result = new SyncResult();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            _logger.LogInformation("Starting sync for build {BuildId} to {GameDir}", manifest.BuildId, gameDir);
            _reporter.Log($"Synchronizing {manifest.DisplayName} ({manifest.Files.Count} files)...");
            ReportProgress(0, "Checking local files...");

            Directory.CreateDirectory(gameDir);

            var filesToDownload = new List<ManifestFile>();
            var filesToPrune = new List<string>();

            // Phase 1: Compare local files with manifest
            foreach (var file in manifest.Files)
            {
                ct.ThrowIfCancellationRequested();

                var localPath = Path.Combine(gameDir, file.Path.Replace('/', Path.DirectorySeparatorChar));

                if (file.Policy == SyncPolicy.Preserve)
                {
                    // PRESERVE: never delete, never overwrite; only restore when missing.
                    if (File.Exists(localPath))
                        continue;
                }
                else if (file.Policy == SyncPolicy.DownloadOnce)
                {
                    // DOWNLOAD_ONCE: download only if missing.
                    if (File.Exists(localPath))
                        continue;
                }
                else if (File.Exists(localPath) && !await NeedsDownloadAsync(localPath, file, ct))
                {
                    continue;
                }

                filesToDownload.Add(file);
            }

            // Phase 2: Determine which local files to prune. Files that are not in the manifest
            // and not protected (user data / provisioned runtime) are removed.
            var manifestPaths = manifest.Files.Select(f => f.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var localFile in Directory.EnumerateFiles(gameDir, "*", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();

                var relative = Path.GetRelativePath(gameDir, localFile).Replace('\\', '/');
                if (manifestPaths.Contains(relative)) continue;
                if (IsProtected(relative)) continue;

                filesToPrune.Add(relative);
            }

            // Phase 3: Download missing/changed files
            if (filesToDownload.Count > 0)
            {
                _logger.LogInformation("Downloading {Count} file(s)", filesToDownload.Count);
                await DownloadFilesAsync(filesToDownload, gameDir, ct);
            }
            else
            {
                ReportProgress(0.9, "All files up to date.");
            }

            // Phase 4: Prune obsolete files
            if (filesToPrune.Count > 0)
            {
                _logger.LogInformation("Pruning {Count} obsolete file(s)", filesToPrune.Count);
                PruneFiles(filesToPrune, gameDir, ct);
            }

            stopwatch.Stop();

            result.Success = true;
            result.DownloadedFiles = filesToDownload.Count;
            result.PrunedFiles = filesToPrune.Count;
            result.ElapsedMs = stopwatch.ElapsedMilliseconds;

            _logger.LogInformation("Sync completed in {ElapsedMs}ms. Downloaded: {Downloaded}, Pruned: {Pruned}",
                result.ElapsedMs, result.DownloadedFiles, result.PrunedFiles);
            _reporter.Log($"Sync completed in {result.ElapsedMs}ms. Downloaded: {result.DownloadedFiles}, Pruned: {result.PrunedFiles}");
            ReportProgress(1, "Files synchronized.");

            return result;
        }

        /// <summary>True when a relative path belongs to user data / provisioned content.</summary>
        public static bool IsProtected(string relativePath)
        {
            var segments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0) return false;

            if (ProtectedRoots.Contains(segments[0])) return true;
            return ProtectedFiles.Contains(segments[^1]);
        }

        /// <summary>
        /// Checks if a local file needs to be downloaded by comparing size and hash.
        /// Uses fast-check (size first, then hash only if size matches).
        /// </summary>
        private static async Task<bool> NeedsDownloadAsync(string localPath, ManifestFile file, CancellationToken ct)
        {
            var fileInfo = new FileInfo(localPath);

            // Fast check: size mismatch.
            if (fileInfo.Length != file.Size) return true;

            // Empty hash (older manifests): trust the size match.
            if (string.IsNullOrEmpty(file.Sha256)) return false;

            var localHash = await ComputeSha256Async(localPath, ct);
            return !string.Equals(localHash, file.Sha256, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Downloads multiple files in parallel with retry logic.
        /// </summary>
        private async Task DownloadFilesAsync(List<ManifestFile> files, string gameDir, CancellationToken ct)
        {
            using var semaphore = new SemaphoreSlim(MaxParallelDownloads);
            var completed = 0;
            var total = files.Count;
            var totalBytes = files.Sum(f => f.Size);

            _reporter.Log($"Downloading {total} files ({FormatBytes(totalBytes)})...");

            var tasks = files.Select(async file =>
            {
                await semaphore.WaitAsync(ct);
                try
                {
                    await DownloadFileWithRetryAsync(file, gameDir, ct);
                    var current = Interlocked.Increment(ref completed);

                    if (current % 25 == 0 || current == total)
                        _logger.LogInformation("Synced {Current}/{Total} files", current, total);

                    ReportProgress((double)current / total, $"Downloading files... {current}/{total}");
                }
                finally
                {
                    semaphore.Release();
                }
            }).ToList();

            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Downloads a single file with retry logic and hash validation.
        /// </summary>
        private async Task DownloadFileWithRetryAsync(ManifestFile file, string gameDir, CancellationToken ct)
        {
            var localPath = Path.Combine(gameDir, file.Path.Replace('/', Path.DirectorySeparatorChar));
            var tempPath = localPath + ".tmp";

            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);

                    using var response = await DownloadClient.GetAsync(file.Url, HttpCompletionOption.ResponseHeadersRead, ct);
                    response.EnsureSuccessStatusCode();

                    await using (var contentStream = await response.Content.ReadAsStreamAsync(ct))
                    await using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                    {
                        await contentStream.CopyToAsync(fileStream, 81920, ct);
                    }

                    if (!string.IsNullOrEmpty(file.Sha256))
                    {
                        var downloadedHash = await ComputeSha256Async(tempPath, ct);
                        if (!string.Equals(downloadedHash, file.Sha256, StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogWarning("Hash mismatch for {Path} (attempt {Attempt}/{MaxRetries})",
                                file.Path, attempt, MaxRetries);
                            File.Delete(tempPath);

                            if (attempt < MaxRetries)
                            {
                                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
                                continue;
                            }

                            throw new InvalidOperationException($"Hash validation failed for {file.Path} after {MaxRetries} attempts");
                        }
                    }

                    File.Move(tempPath, localPath, overwrite: true);
                    return;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex) when (attempt < MaxRetries)
                {
                    _logger.LogWarning(ex, "Download failed for {Path} (attempt {Attempt}/{MaxRetries})",
                        file.Path, attempt, MaxRetries);

                    try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
                }
            }

            _logger.LogError("Failed to download {Path} after {MaxRetries} attempts", file.Path, MaxRetries);
            throw new InvalidOperationException($"Failed to download {file.Path} after {MaxRetries} attempts");
        }

        /// <summary>
        /// Removes files that are no longer in the manifest, then prunes empty directories.
        /// </summary>
        private void PruneFiles(IEnumerable<string> files, string gameDir, CancellationToken ct)
        {
            foreach (var file in files)
            {
                ct.ThrowIfCancellationRequested();

                var localPath = Path.Combine(gameDir, file.Replace('/', Path.DirectorySeparatorChar));
                try
                {
                    if (File.Exists(localPath))
                    {
                        File.Delete(localPath);
                        _logger.LogDebug("Pruned: {File}", file);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to prune file: {File}", file);
                }

                // Clean up directories that became empty (never above the game root).
                var dir = Path.GetDirectoryName(localPath);
                while (!string.IsNullOrEmpty(dir) && dir.StartsWith(gameDir, StringComparison.OrdinalIgnoreCase) &&
                       !string.Equals(dir, gameDir, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                        {
                            Directory.Delete(dir);
                            _logger.LogDebug("Removed empty directory: {Dir}", dir);
                        }
                        else
                        {
                            break;
                        }
                    }
                    catch
                    {
                        break;
                    }

                    dir = Path.GetDirectoryName(dir);
                }
            }

            _reporter.Log($"Pruned {files.Count()} obsolete file(s)");
        }

        /// <summary>
        /// Reports the sync stage as a 0..1 fraction, throttled so a burst of parallel
        /// downloads does not flood the progress animator.
        /// </summary>
        private void ReportProgress(double fraction, string message)
        {
            var now = Environment.TickCount64;
            var last = Interlocked.Read(ref _lastProgressTicks);
            if (fraction < 1 && now - last < 150) return;

            Interlocked.Exchange(ref _lastProgressTicks, now);
            _ = _reporter.ReportAsync("sync", fraction, message);
        }

        /// <summary>
        /// Computes SHA-256 hash of a file.
        /// </summary>
        private static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct)
        {
            using var sha = SHA256.Create();
            await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var hash = await sha.ComputeHashAsync(stream, ct);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        /// <summary>
        /// Formats bytes to human-readable string.
        /// </summary>
        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            return $"{size:F1} {sizes[order]}";
        }
    }

    /// <summary>
    /// Result of a synchronization operation.
    /// </summary>
    public class SyncResult
    {
        public bool Success { get; set; }
        public int DownloadedFiles { get; set; }
        public int PrunedFiles { get; set; }
        public long ElapsedMs { get; set; }
    }
}
