using System;
using System.IO;
using System.Runtime.InteropServices;

namespace UnderMine.Services
{
   
    public record AppPath(string Value);

    public class PathService
    {
        private const string AppName = "UnderMine";

        public AppPath GetStorageDirectory()
        {
            string path;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    AppName);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                    "Library", "Application Support", AppName);
            }
            else 
            {
                path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".config", AppName);
            }
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            return new AppPath(path);
        }
    }
}