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
    /// Verifies that a build manifest is turned into a correct, fully-substituted launch command.
    /// </summary>
    public class ProcessLauncherTests : IDisposable
    {
        private readonly string _gameDir;
        private readonly ProcessLauncher _launcher = new(NullLogger<ProcessLauncher>.Instance);

        public ProcessLauncherTests()
        {
            _gameDir = Path.Combine(Path.GetTempPath(), "undermine-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_gameDir, "versions", "1.21.1"));
            Directory.CreateDirectory(Path.Combine(_gameDir, "libraries", "com", "example"));
            Directory.CreateDirectory(Path.Combine(_gameDir, "assets", "indexes"));

            File.WriteAllText(Path.Combine(_gameDir, "versions", "1.21.1", "1.21.1.jar"), "jar");
            File.WriteAllText(Path.Combine(_gameDir, "libraries", "com", "example", "lib-1.0.jar"), "lib");
            File.WriteAllText(Path.Combine(_gameDir, "libraries", "com", "example", "natives-1.0-natives-windows.jar"), "nat");
            File.WriteAllText(Path.Combine(_gameDir, "libraries", "com", "example", "natives-1.0-natives-linux.jar"), "nat");
            File.WriteAllText(Path.Combine(_gameDir, "assets", "indexes", "1.21.1.json"), "{}");
        }

        public void Dispose()
        {
            try { Directory.Delete(_gameDir, true); } catch { /* best effort */ }
        }

        private static BuildManifest FabricManifest() => new()
        {
            BuildId = "test",
            DisplayName = "Fabulously Optimized",
            GameVersion = "1.21.1",
            LoaderType = "fabric",
            LoaderVersion = "0.19.3",
            JavaVersion = 21,
            MainClass = "net.fabricmc.loader.impl.launch.knot.KnotClient",
            JvmArgs = new List<string> { "-XX:+UseG1GC", "-DFabricMcEmu=net.minecraft.client.main.Main" },
            GameArgs = new List<string>
            {
                "--username", "${username}",
                "--version", "${version}",
                "--gameDir", "${gameDir}",
                "--assetIndex", "${assetIndex}",
                "--uuid", "${uuid}",
                "--accessToken", "${accessToken}"
            },
            Files = new List<ManifestFile>
            {
                new() { Path = "assets/indexes/1.21.1.json", Size = 2, Sha256 = "", Policy = "FORCE_SYNC", Url = "http://example" }
            }
        };

        [Fact]
        public void BuildProcess_UsesSettingsAndSubstitutesPlaceholders()
        {
            var info = _launcher.BuildProcess(
                FabricManifest(), _gameDir, @"C:\java\bin\java.exe", "Steve", "uuid-1", "token-1", maxMemoryMb: 8192);

            Assert.Equal(@"C:\java\bin\java.exe", info.FileName);
            Assert.Equal(_gameDir, info.WorkingDirectory);

            var args = info.ArgumentList.ToList();
            Assert.Contains("-Xmx8192M", args);
            Assert.Contains($"-Djava.library.path={Path.Combine(_gameDir, "natives")}", args);
            Assert.Contains("net.fabricmc.loader.impl.launch.knot.KnotClient", args);
            Assert.Contains("Steve", args);          // ${username}
            Assert.Contains("token-1", args);         // ${accessToken}
            Assert.Contains("uuid-1", args);          // ${uuid}
            Assert.Contains("1.21.1", args);          // ${assetIndex} resolved from the shipped index file

            // No unresolved placeholder may reach the JVM.
            Assert.DoesNotContain(args, a => a.Contains("${", StringComparison.Ordinal));
        }

        [Fact]
        public void BuildProcess_ClasspathHasVersionJarAndLibrariesButNoNatives()
        {
            var info = _launcher.BuildProcess(FabricManifest(), _gameDir, "java", "Steve", "u", "t");
            var args = info.ArgumentList.ToList();

            var cpIndex = args.IndexOf("-cp");
            Assert.True(cpIndex >= 0, "A -cp argument is expected");
            var classpath = args[cpIndex + 1];

            Assert.Contains(Path.Combine(_gameDir, "versions", "1.21.1", "1.21.1.jar"), classpath);
            Assert.Contains(Path.Combine(_gameDir, "libraries", "com", "example", "lib-1.0.jar"), classpath);
            Assert.DoesNotContain("-natives-", classpath);
        }

        [Fact]
        public void BuildProcess_DoesNotDuplicateClasspathWhenManifestProvidesIt()
        {
            var manifest = FabricManifest();
            manifest.JvmArgs = new List<string> { "-cp", "${classpath}" };

            var info = _launcher.BuildProcess(manifest, _gameDir, "java", "Steve", "u", "t");
            var args = info.ArgumentList.ToList();

            Assert.Single(args.Where(a => a == "-cp"));
            Assert.DoesNotContain(args, a => a.Contains("${", StringComparison.Ordinal));
        }
    }
}
