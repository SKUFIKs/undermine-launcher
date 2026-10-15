namespace UnderMine.Services
{
    public class SystemMetadata
    {
        public string operatingSystem;
        public string cpuInfo;
        public long totalMemoryBytes;
        public string deviceId;
        public string architecture;

        public SystemMetadata(string OperatingSystem, string CpuInfo, long TotalMemoryBytes, string DeviceId, string Architecture)
        {
            operatingSystem = OperatingSystem;
            cpuInfo = CpuInfo;
            totalMemoryBytes = TotalMemoryBytes;
            deviceId = DeviceId;
            architecture = Architecture;
        }
    }
}