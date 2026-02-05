using System;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace UnderMine.Services;

public class CheckEquipment
{
    public Models.SystemInfo SystemInfo { get; }
    
    public CheckEquipment(Models.SystemInfo systemInfo)
    {
        SystemInfo = systemInfo;
    }
    public async Task<Models.SystemInfo> CheckOperatingSystemAsync()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return await CheckForWindows();
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return await CheckForLinux();
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return await CheckForMacOs();

        return new Models.SystemInfo { TotalMemory = 0 };
    }

    private Task<Models.SystemInfo> CheckForWindows()
    {
        double totalMemoryBytes = 0;

        ManagementObjectSearcher searcher = new("SELECT * FROM Win32_PhysicalMemory");

        foreach (ManagementBaseObject? o in searcher.Get())
        {
            var mo = (ManagementObject)o;
            totalMemoryBytes += Convert.ToDouble(mo["Capacity"]);
        }
        
        double totalMemoryMb =  Math.Round(totalMemoryBytes/(1024*1024),0);

        return Task.FromResult(new Models.SystemInfo
        {
            TotalMemory = totalMemoryMb,
        });
    }

    private Task<Models.SystemInfo> CheckForLinux()
    {
        try
        {
            string memInfo = System.IO.File.ReadAllText("/proc/meminfo");
            string line = memInfo.Split('\n')[0];
            string[] parts = line.Split(':');
            string memKbStr = parts[1].Trim().Split(' ')[0];
            long memKb = long.Parse(memKbStr);
            double totalMemoryMb = memKb / 1024.0;
            
            return Task.FromResult(new Models.SystemInfo
            {
                TotalMemory = totalMemoryMb,
            });
        }
        catch (Exception)
        {
            return Task.FromResult(new Models.SystemInfo
            {
                TotalMemory = 8192,
            });
        }
    }

    private Task<Models.SystemInfo> CheckForMacOs()
    {
        try
        {
            Process proc = new Process();
            proc.StartInfo.FileName = "sysctl";
            proc.StartInfo.Arguments = "hw.memsize";
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.UseShellExecute = false;
            proc.Start();
            string output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();

            string[] parts = output.Split(':');
            long memBytes = long.Parse(parts[1].Trim());
            double totalMemoryMb = memBytes / (1024.0 * 1024.0);
            
            return Task.FromResult(new Models.SystemInfo
            {
                TotalMemory = totalMemoryMb,
            });
        }
        catch (Exception)
        {
            return Task.FromResult(new Models.SystemInfo
            {
                TotalMemory = 8192,
            });
        }
    }
}