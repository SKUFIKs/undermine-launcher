using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using UnderMine.Core.Game;
using UnderMine.Models;
using Xunit;

namespace UnderMine.Tests
{
    /// <summary>
    /// A NeoForge profile puts a handful of libraries on the module path ("-p"). The classpath has to
    /// be derived from the version JSON chain so that nothing else claims those modules - BootstrapLauncher
    /// aborts the launch when a module is reachable from both the module path and the classpath.
    /// </summary>
    public class ProcessLauncherClasspathTests : IDisposable
    {
        private readonly string _gameDir;
        private readonly ProcessLauncher _launcher = new(NullLogger<ProcessLauncher>.Instance);

        private const string BaseVersionJson = @"{
            ""id"": ""1.21.1"",
            ""mainClass"": ""net.minecraft.client.main.Main"",
            ""assetIndex"": { ""id"": ""1.21.1"" },
            ""libraries"": [
                { ""name"": ""org.ow2.asm:asm-analysis:9.3"",
                  ""downloads"": { ""artifact"": { ""path"": ""org/ow2/asm/asm-analysis/9.3/asm-analysis-9.3.jar"" } } },
                { ""name"": ""com.example:lib:1.0"",
                  ""downloads"": { ""artifact"": { ""path"": ""com/example/lib/1.0/lib-1.0.jar"" } } },
                { ""name"": ""org.lwjgl:lwjgl:3.3.3"",
                  ""downloads"": { ""artifact"": { ""path"": ""org/lwjgl/lwjgl/3.3.3/lwjgl-3.3.3.jar"" },
                                  ""classifiers"": { ""natives-windows"": { ""path"": ""org/lwjgl/lwjgl/3.3.3/lwjgl-3.3.3-natives-windows.jar"" } } } },
                { ""name"": ""org.example:mac-only:1.0"",
                  ""downloads"": { ""artifact"": { ""path"": ""org/example/mac-only/1.0/mac-only-1.0.jar"" } },
                  ""rules"": [ { ""action"": ""allow"", ""os"": { ""name"": ""osx"" } } ] }
            ]
        }";

        private const string LoaderVersionJson = @"{
            ""id"": ""neoforge-21.1.248"",
            ""inheritsFrom"": ""1.21.1"",
            ""mainClass"": ""cpw.mods.bootstraplauncher.BootstrapLauncher"",
            ""libraries"": [
                { ""name"": ""org.ow2.asm:asm-analysis:9.10.1"",
                  ""downloads"": { ""artifact"": { ""path"": ""org/ow2/asm/asm-analysis/9.10.1/asm-analysis-9.10.1.jar"" } } }
            ],
            ""arguments"": { ""jvm"": [
                ""-p"",
                ""${library_directory}/org/ow2/asm/asm-analysis/9.10.1/asm-analysis-9.10.1.jar"",
                ""--add-modules"",
                ""ALL-MODULE-PATH""
            ] }
        }";

        public ProcessLauncherClasspathTests()
        {
            _gameDir = Path.Combine(Path.GetTempPath(), "undermine-cp-" + Guid.NewGuid().ToString("N"));

            Touch("versions/1.21.1/1.21.1.jar");
            WriteText("versions/1.21.1/1.21.1.json", BaseVersionJson);
            WriteText("versions/neoforge-21.1.248/neoforge-21.1.248.json", LoaderVersionJson);

            Touch("libraries/org/ow2/asm/asm-analysis/9.3/asm-analysis-9.3.jar");
            Touch("libraries/org/ow2/asm/asm-analysis/9.10.1/asm-analysis-9.10.1.jar");
            Touch("libraries/com/example/lib/1.0/lib-1.0.jar");
            Touch("libraries/org/lwjgl/lwjgl/3.3.3/lwjgl-3.3.3.jar");
            Touch("libraries/org/lwjgl/lwjgl/3.3.3/lwjgl-3.3.3-natives-windows.jar");
            Touch("libraries/org/example/mac-only/1.0/mac-only-1.0.jar");
            Touch("assets/indexes/1.21.1.json");
        }

        public void Dispose()
        {
            try { Directory.Delete(_gameDir, true); } catch { /* best effort */ }
        }

        private void Touch(string relativePath) => WriteText(relativePath, string.Empty);

        private void WriteText(string relativePath, string content)
        {
            var full = Path.Combine(_gameDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
        }

        private BuildManifest NeoForgeManifest() => new()
        {
            BuildId = "test",
            DisplayName = "Pixelmon",
            GameVersion = "1.21.1",
            LoaderType = "neoforge",
            LoaderVersion = "21.1.248",
            JavaVersion = 21,
            MainClass = "cpw.mods.bootstraplauncher.BootstrapLauncher",
            JvmArgs = new List<string>
            {
                "-XX:+UseG1GC",
                "-p",
                "${library_directory}/org/ow2/asm/asm-analysis/9.10.1/asm-analysis-9.10.1.jar",
                "--add-modules",
                "ALL-MODULE-PATH"
            },
            GameArgs = new List<string> { "--username", "${username}", "--version", "${version_name}" },
            Files = new List<ManifestFile>
            {
                new() { Path = "assets/indexes/1.21.1.json", Size = 2, Sha256 = "", Policy = "FORCE_SYNC", Url = "http://example" }
            }
        };

        private string BuildClasspath()
        {
            var info = _launcher.BuildProcess(NeoForgeManifest(), _gameDir, "java", "Steve", "u", "t");
            var args = info.ArgumentList.ToList();

            Assert.DoesNotContain(args, a => a.Contains("${", StringComparison.Ordinal));

            var cpIndex = args.IndexOf("-cp");
            Assert.True(cpIndex >= 0, "A -cp argument is expected");
            return args[cpIndex + 1];
        }

        [Fact]
        public void Classpath_UsesClientJarAndDeclaredLibraries()
        {
            var classpath = BuildClasspath();

            Assert.Contains(Path.Combine(_gameDir, "versions", "1.21.1", "1.21.1.jar"), classpath);
            Assert.Contains(Path.Combine(_gameDir, "libraries", "com", "example", "lib", "1.0", "lib-1.0.jar"), classpath);
            Assert.Contains(Path.Combine(_gameDir, "libraries", "org", "lwjgl", "lwjgl", "3.3.3", "lwjgl-3.3.3.jar"), classpath);
        }

        [Fact]
        public void Classpath_ExcludesLibrariesAlreadyOnTheModulePath()
        {
            var classpath = BuildClasspath();

            Assert.DoesNotContain("asm-analysis-9.10.1.jar", classpath);
        }

        [Fact]
        public void Classpath_ExcludesLibraryVersionSupersededByTheLoaderProfile()
        {
            // The stale ASM that only lives on disk must not reach the classpath: it declares the same
            // module as the 9.10.1 jar on the module path, which is what breaks NeoForge at startup.
            var classpath = BuildClasspath();

            Assert.DoesNotContain("asm-analysis-9.3.jar", classpath);
        }

        [Fact]
        public void Classpath_ExcludesNativesAndRuleFilteredLibraries()
        {
            var classpath = BuildClasspath();

            Assert.DoesNotContain("-natives-", classpath);
            Assert.DoesNotContain("mac-only-1.0.jar", classpath);
        }

        [Fact]
        public void Classpath_FallsBackToGlobWhenNoVersionProfileShips()
        {
            File.Delete(Path.Combine(_gameDir, "versions", "1.21.1", "1.21.1.json"));
            File.Delete(Path.Combine(_gameDir, "versions", "neoforge-21.1.248", "neoforge-21.1.248.json"));

            var classpath = BuildClasspath();

            Assert.Contains(Path.Combine(_gameDir, "libraries", "com", "example", "lib", "1.0", "lib-1.0.jar"), classpath);
            Assert.DoesNotContain("-natives-", classpath);
        }
    }
}