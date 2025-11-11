using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Path = System.IO.Path;

namespace UnderMine.Core.server_1;

public class SettingsLoader
{
    public Task LoadSettings()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "versions", "TechnoMagic", "options.txt");
        
        if (File.Exists(path)) return Task.CompletedTask;
        
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var options = new Dictionary<string, string>
        {
            ["version"] = "3465",
            ["autoJump"] = "false",
            ["enableVsync"] = "true",
            ["entityShadows"] = "true",
            ["fullscreen"] = "true",
            ["bobView"] = "true",
            ["fov"] = "0.0",
            ["gamma"] = "0.5",
            ["renderDistance"] = "12",
            ["simulationDistance"] = "12",
            ["maxFps"] = "120",
            ["graphicsMode"] = "0",
            ["ao"] = "true",
            ["biomeBlendRadius"] = "2",
            ["lang"] = "ru_ru",
            ["guiScale"] = "2",
            ["particles"] = "1",
            ["mouseSensitivity"] = "0.5",
            ["mainHand"] = "\"right\"",
            ["attackIndicator"] = "1",
            ["narrator"] = "0",
            ["soundCategory_master"] = "1.0",
            ["soundCategory_music"] = "1.0",
            ["soundCategory_block"] = "1.0",
            ["soundCategory_hostile"] = "1.0",
            ["soundCategory_neutral"] = "1.0",
            ["soundCategory_player"] = "1.0",
            ["soundCategory_ambient"] = "1.0",
            ["soundCategory_voice"] = "1.0",
            ["modelPart_cape"] = "true",
            ["modelPart_jacket"] = "true",
            ["modelPart_left_sleeve"] = "true",
            ["modelPart_right_sleeve"] = "true",
            ["modelPart_left_pants_leg"] = "true",
            ["modelPart_right_pants_leg"] = "true",
            ["tutorialStep"] = "none",
            ["joinedFirstServer"] = "true",
            ["hideBundleTutorial"] = "true",
            ["onboardAccessibility"] = "false",
            ["skipMultiplayerWarning"] = "true",
            ["modelPart_hat"] = "true",
            ["key_supplementaries.keybind.quiver"] = "key.keyboard.0"
        };
        using (var writer = new StreamWriter(path)) 
        { foreach (var kv in options) 
            writer.WriteLine($"{kv.Key}:{kv.Value}"); 
        }

        return Task.CompletedTask;
    }
}