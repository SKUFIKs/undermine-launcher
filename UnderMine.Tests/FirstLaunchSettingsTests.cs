using System;
using System.IO;
using System.Linq;
using UnderMine.Core.Game;
using Xunit;

namespace UnderMine.Tests
{
    /// <summary>
    /// The very first start of an instance has to come up in fullscreen, later starts must leave the
    /// player's own choice alone - so the flag is written to options.txt exactly once.
    /// </summary>
    public class FirstLaunchSettingsTests : IDisposable
    {
        private readonly string _instancePath;

        public FirstLaunchSettingsTests()
        {
            _instancePath = Path.Combine(Path.GetTempPath(), "undermine-first-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_instancePath);
        }

        public void Dispose()
        {
            try { Directory.Delete(_instancePath, true); } catch { /* best effort */ }
        }

        private string OptionsPath => Path.Combine(_instancePath, "options.txt");

        private static readonly string ShippedOptions =
            "lang:ru_ru\n" +
            "fullscreen:false\n" +
            "guiScale:3\n" +
            "key_key.fullscreen:key.keyboard.f11\n" +
            "enableVsync:true\n";

        [Fact]
        public void ForceFullscreen_WhenBuildShipsNoOptions_CreatesTheFile()
        {
            Assert.True(FirstLaunchSettings.ForceFullscreen(_instancePath));

            Assert.Contains("fullscreen:true", File.ReadAllText(OptionsPath));
        }

        [Fact]
        public void ForceFullscreen_OverridesTheShippedFalseValue()
        {
            File.WriteAllText(OptionsPath, ShippedOptions);

            Assert.True(FirstLaunchSettings.ForceFullscreen(_instancePath));

            var lines = File.ReadAllLines(OptionsPath);
            Assert.Contains("fullscreen:true", lines);
            Assert.DoesNotContain("fullscreen:false", lines);
        }

        [Fact]
        public void ForceFullscreen_LeavesEveryOtherSettingAlone()
        {
            File.WriteAllText(OptionsPath, ShippedOptions);

            FirstLaunchSettings.ForceFullscreen(_instancePath);

            var lines = File.ReadAllLines(OptionsPath).Where(l => l.Length > 0).ToList();

            Assert.Contains("lang:ru_ru", lines);
            Assert.Contains("guiScale:3", lines);
            Assert.Contains("enableVsync:true", lines);
            Assert.Contains("key_key.fullscreen:key.keyboard.f11", lines);
        }

        [Fact]
        public void ForceFullscreen_WhenKeyIsAbsent_AppendsIt()
        {
            File.WriteAllText(OptionsPath, "lang:ru_ru\nguiScale:3\n");

            Assert.True(FirstLaunchSettings.ForceFullscreen(_instancePath));

            var lines = File.ReadAllLines(OptionsPath);
            Assert.Contains("fullscreen:true", lines);
            Assert.Equal(3, lines.Count(l => l.Length > 0));
        }

        [Fact]
        public void HasLaunchedBefore_IsFalseUntilTheInstanceWasActuallyStarted()
        {
            Assert.False(FirstLaunchSettings.HasLaunchedBefore(_instancePath));

            Assert.True(FirstLaunchSettings.MarkLaunched(_instancePath));

            Assert.True(FirstLaunchSettings.HasLaunchedBefore(_instancePath));
        }

        [Fact]
        public void MarkLaunched_OnMissingDirectory_DoesNotThrow()
        {
            Assert.False(FirstLaunchSettings.MarkLaunched(Path.Combine(_instancePath, "nope")));
        }

        [Fact]
        public void LaunchMarker_IsProtectedFromSyncPruning()
        {
            // The sync deletes every file the manifest does not know about; losing the marker would
            // force fullscreen onto the player again on the next install.
            Assert.True(ClientSyncService.IsProtected(FirstLaunchSettings.LaunchedMarkerFileName));
            Assert.True(ClientSyncService.IsProtected("sub/dir/" + FirstLaunchSettings.LaunchedMarkerFileName));
        }
    }
}
