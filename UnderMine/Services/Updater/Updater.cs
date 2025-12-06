using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Velopack;

namespace UnderMine.Services.Updater
{
    public class Updater
    {
        public async Task UpdateLauncher()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var mgr = new UpdateManager("https://the.place/you-host/updates");

                var newVersion = await mgr.CheckForUpdatesAsync();
                if (newVersion == null)
                    return;

                await mgr.DownloadUpdatesAsync(newVersion);

                mgr.WaitExitThenApplyUpdates(newVersion);
            }
        }
    }
}
