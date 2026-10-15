using System;
using System.IO;
using System.Text;

namespace UnderMine.Core.Game
{
    /// <summary>
    /// Applies the settings that only make sense for a freshly installed instance.
    /// Minecraft has no command line switch for fullscreen - it is read from options.txt - so the
    /// launcher flips that flag itself the very first time an instance starts and then leaves the
    /// player's own choice (F11) alone on every later launch.
    /// </summary>
    public static class FirstLaunchSettings
    {
        /// <summary>
        /// Launcher-owned marker proving the instance has been started at least once. It has to be
        /// listed in <see cref="ClientSyncService"/> protected files, otherwise the sync prunes it
        /// and fullscreen would be forced again on the next run.
        /// </summary>
        public const string LaunchedMarkerFileName = "launched.marker";

        private const string OptionsFileName = "options.txt";
        private const string FullscreenKey = "fullscreen";
        private const string FullscreenEntry = FullscreenKey + ":true";

        private static readonly UTF8Encoding Utf8NoBom = new(false);

        /// <summary>True when this instance has already been started at least once.</summary>
        public static bool HasLaunchedBefore(string instancePath)
            => !string.IsNullOrEmpty(instancePath) && File.Exists(Path.Combine(instancePath, LaunchedMarkerFileName));

        /// <summary>
        /// Forces fullscreen in options.txt, creating the file when the build ships none.
        /// Returns false when the file could not be touched - never fails the launch for a cosmetic setting.
        /// </summary>
        public static bool ForceFullscreen(string instancePath)
        {
            if (string.IsNullOrEmpty(instancePath))
                return false;

            try
            {
                var optionsPath = Path.Combine(instancePath, OptionsFileName);

                if (!File.Exists(optionsPath))
                {
                    // Minecraft fills every missing key with its default, a single line is enough.
                    File.WriteAllText(optionsPath, FullscreenEntry + Environment.NewLine, Utf8NoBom);
                    return true;
                }

                var lines = File.ReadAllLines(optionsPath);
                var replaced = false;

                for (var i = 0; i < lines.Length; i++)
                {
                    if (!lines[i].StartsWith(FullscreenKey + ":", StringComparison.OrdinalIgnoreCase))
                        continue;

                    lines[i] = FullscreenEntry;
                    replaced = true;
                }

                if (replaced)
                {
                    File.WriteAllText(optionsPath, string.Join("\n", lines), Utf8NoBom);
                    return true;
                }

                // No fullscreen key yet: append it instead of rewriting unrelated settings.
                var text = File.ReadAllText(optionsPath);
                if (text.Length > 0 && !text.EndsWith("\n") && !text.EndsWith("\r"))
                    text += "\n";

                File.WriteAllText(optionsPath, text + FullscreenEntry + "\n", Utf8NoBom);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }

        /// <summary>Records that the instance has been started.</summary>
        public static bool MarkLaunched(string instancePath)
        {
            if (string.IsNullOrEmpty(instancePath))
                return false;

            try
            {
                var stamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
                File.WriteAllText(Path.Combine(instancePath, LaunchedMarkerFileName), stamp, Utf8NoBom);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Best effort: worst case fullscreen is applied once more on the next start.
                return false;
            }
        }
    }
}
