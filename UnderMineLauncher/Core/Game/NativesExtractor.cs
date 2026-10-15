using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace UnderMine.Core.Game
{
    /// <summary>
    /// Extracts platform natives (LWJGL &amp; friends) from the "*&#8209;natives&#8209;*" library jars
    /// into a single flat directory that is handed to the game via
    /// <c>-Djava.library.path</c> / <c>-Dorg.lwjgl.librarypath</c>.
    /// </summary>
    public static class NativesExtractor
    {
        public static string GetNativesDirectory(string gameDir) => Path.Combine(gameDir, "natives");

        /// <summary>
        /// Extracts the natives that match the current OS/architecture from the supplied jar paths
        /// (relative to <paramref name="gameDir"/>). Returns the number of extracted files.
        /// </summary>
        public static int Extract(string gameDir, IEnumerable<string> libraryRelativePaths, ILogger logger)
        {
            var nativesDir = GetNativesDirectory(gameDir);
            Directory.CreateDirectory(nativesDir);

            var classifier = GetPlatformClassifier();
            var extracted = 0;

            foreach (var relative in libraryRelativePaths)
            {
                if (string.IsNullOrEmpty(relative)) continue;
                if (!relative.EndsWith(".jar", StringComparison.OrdinalIgnoreCase)) continue;
                if (!IsNativeForPlatform(Path.GetFileName(relative), classifier)) continue;

                var jarPath = Path.Combine(gameDir, relative.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(jarPath)) continue;

                try
                {
                    using var archive = ZipFile.OpenRead(jarPath);
                    foreach (var entry in archive.Entries)
                    {
                        // Directories have an empty Name; signatures/descriptors are useless.
                        if (string.IsNullOrEmpty(entry.Name)) continue;
                        if (entry.FullName.StartsWith("META-INF/", StringComparison.OrdinalIgnoreCase)) continue;

                        // Flatten: java.library.path / System.loadLibrary look the file up by name.
                        var target = Path.Combine(nativesDir, entry.Name);
                        entry.ExtractToFile(target, overwrite: true);
                        extracted++;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to extract natives from {Jar}", relative);
                }
            }

            logger.LogInformation("Extracted {Count} native file(s) into {Dir}", extracted, nativesDir);
            return extracted;
        }

        /// <summary>
        /// The natives classifier for the running platform ("windows", "windows-arm64", ...).
        /// </summary>
        public static string GetPlatformClassifier()
        {
            var arm64 = RuntimeInformation.OSArchitecture == Architecture.Arm64;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return arm64 ? "windows-arm64" : "windows";

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return arm64 ? "macos-arm64" : "macos";

            return arm64 ? "linux-arm64" : "linux";
        }

        /// <summary>
        /// True when a jar such as "lwjgl-3.3.3-natives-windows.jar" targets the given classifier.
        /// The comparison is exact on the classifier segment, so the "-natives-windows-arm64" /
        /// "-natives-windows-x86" variants are not mistakenly matched on x64.
        /// </summary>
        public static bool IsNativeForPlatform(string fileName, string classifier)
        {
            const string marker = "-natives-";
            var index = fileName.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0) return false;

            var rest = fileName.Substring(index + marker.Length);
            if (rest.EndsWith(".jar", StringComparison.OrdinalIgnoreCase))
                rest = rest[..^4];

            return string.Equals(rest, classifier, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>True for any natives jar, regardless of platform.</summary>
        public static bool IsNativeJar(string fileName)
            => fileName.Contains("-natives-", StringComparison.OrdinalIgnoreCase);
    }
}
