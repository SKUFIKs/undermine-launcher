using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UnderMine.Core.Game;
using Xunit;

namespace UnderMine.Tests
{
    public class NativesExtractorTests
    {
        [Theory]
        [InlineData("lwjgl-3.3.3-natives-windows.jar", "windows", true)]
        [InlineData("lwjgl-3.3.3-natives-windows-arm64.jar", "windows", false)]
        [InlineData("lwjgl-3.3.3-natives-windows-x86.jar", "windows", false)]
        [InlineData("lwjgl-3.3.3-natives-linux.jar", "windows", false)]
        [InlineData("lwjgl-3.3.3-natives-macos.jar", "macos", true)]
        [InlineData("lwjgl-3.3.3.jar", "windows", false)]
        public void IsNativeForPlatform_MatchesExactClassifier(string fileName, string classifier, bool expected)
        {
            Assert.Equal(expected, NativesExtractor.IsNativeForPlatform(fileName, classifier));
        }

        [Fact]
        public void Extract_FlattensNativesSkipsMetaInfAndOtherPlatforms()
        {
            var gameDir = Path.Combine(Path.GetTempPath(), "undermine-native-" + Guid.NewGuid().ToString("N"));
            try
            {
                var classifier = NativesExtractor.GetPlatformClassifier();
                var otherClassifier = classifier == "linux" ? "windows" : "linux";

                var libsDir = Path.Combine(gameDir, "libraries", "org", "lwjgl");
                Directory.CreateDirectory(libsDir);

                var matchingJar = Path.Combine(libsDir, $"lwjgl-3.3.3-natives-{classifier}.jar");
                var otherJar = Path.Combine(libsDir, $"lwjgl-3.3.3-natives-{otherClassifier}.jar");

                CreateJar(matchingJar, "libexample.dll", "META-INF/MANIFEST.MF");
                CreateJar(otherJar, "libexample-other.dll");

                var relativePaths = new List<string>
                {
                    $"libraries/org/lwjgl/lwjgl-3.3.3-natives-{classifier}.jar",
                    $"libraries/org/lwjgl/lwjgl-3.3.3-natives-{otherClassifier}.jar"
                };

                var extracted = NativesExtractor.Extract(gameDir, relativePaths, NullLogger.Instance);

                var nativesDir = Path.Combine(gameDir, "natives");
                Assert.Equal(1, extracted); // only libexample.dll, MANIFEST.MF skipped
                Assert.True(File.Exists(Path.Combine(nativesDir, "libexample.dll")));
                Assert.False(File.Exists(Path.Combine(nativesDir, "libexample-other.dll")));
                Assert.False(File.Exists(Path.Combine(nativesDir, "MANIFEST.MF")));
            }
            finally
            {
                try { Directory.Delete(gameDir, true); } catch { /* best effort */ }
            }
        }

        private static void CreateJar(string path, params string[] fileEntries)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var zip = ZipFile.Open(path, ZipArchiveMode.Create);

            foreach (var entry in fileEntries)
            {
                var zipEntry = zip.CreateEntry(entry);
                using var stream = zipEntry.Open();
                stream.Write(new byte[] { 1, 2, 3 }, 0, 3);
            }
        }
    }
}
