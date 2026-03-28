using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using LibreHardwareMonitor.Hardware;
using WpfDesktop.Services.Interfaces;

namespace WpfDesktop.Services
{
    public sealed class HwInfo : IDisposable
    {
        // Windows API for memory status fallback
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        private readonly Computer _computer;
        private readonly UpdateVisitor _visitor = new UpdateVisitor();
        private readonly bool _available;
        private readonly ILogService? _logService;
        private bool _disposed;

        private System.Diagnostics.PerformanceCounter? _diskReadCounter;
        private System.Diagnostics.PerformanceCounter? _diskWriteCounter;
        private bool _perfCountersInitialized;

        private long _lastNetReceivedBytes = -1;
        private long _lastNetSentBytes = -1;
        private DateTime _lastNetTime = DateTime.MinValue;

        public HwInfo(ILogService? logService = null)
        {
            _logService = logService;
            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = true,
                IsMotherboardEnabled = true,
                IsControllerEnabled = true,
                IsStorageEnabled = true,
                IsNetworkEnabled = false
            };
            try
            {
                _computer.Open();
                _available = true;
            }
            catch (Exception ex)
            {
                _logService?.LogError("初始化硬件监控库失败", ex);
                _available = false;
            }
        }

        public HwInfoSnapshot GetSnapshot()
        {
            if (_disposed || !_available)
            {
                return new HwInfoSnapshot();
            }

            try
            {
                _computer.Accept(_visitor);
            }
            catch
            {
                return new HwInfoSnapshot();
            }

            var cpuHardware = EnumerateHardware(_computer).Where(h => h.HardwareType == HardwareType.Cpu).ToList();
            var gpuHardwareList = EnumerateHardware(_computer).Where(IsGpuHardware).ToList();
            var memoryHardware = EnumerateHardware(_computer).Where(h => h.HardwareType == HardwareType.Memory).ToList();
            var motherboardHardware = EnumerateHardware(_computer).Where(h => h.HardwareType == HardwareType.Motherboard).ToList();
            var storageHardware = EnumerateHardware(_computer).Where(h => h.HardwareType == HardwareType.Storage).ToList();

            var cpuSensors = cpuHardware.SelectMany(GetAllSensors).ToList();
            var memorySensors = memoryHardware.SelectMany(GetAllSensors).ToList();
            var motherboardSensors = motherboardHardware.SelectMany(GetAllSensors).ToList();
            var storageSensors = storageHardware.SelectMany(GetAllSensors).ToList();

            var cpuName = cpuHardware.FirstOrDefault()?.Name ?? "CPU";
            var cpuLoad = SelectLoad(cpuSensors, "cpu total", "total");
            var cpuTemp = SelectTemperature(cpuSensors);
            var cpuFan = SelectFanRpm(cpuSensors, "cpu") ?? SelectFanRpm(motherboardSensors, "cpu");

            var (memUsed, memTotal) = SelectMemoryUsage(memorySensors, preferPhysicalMemory: true);

            var (diskReadMb, diskWriteMb) = SelectDiskRates(storageSensors);
            var (netDownloadMb, netUploadMb) = GetNetworkRates();

            // 收集每个 GPU 的信息
            var gpus = new List<GpuInfoSnapshot>();
            foreach (var gpuHardware in gpuHardwareList)
            {
                var gpuSensors = GetAllSensors(gpuHardware).ToList();

                var gpuLoad = SelectLoad(gpuSensors, "core", "gpu");
                var gpuTemp = SelectTemperature(gpuSensors);
                var gpuFan = SelectFanRpm(gpuSensors, "fan") ?? SelectFanRpm(motherboardSensors, "gpu");
                var (gpuUsed, gpuTotal) = SelectMemoryUsage(gpuSensors);

                gpus.Add(new GpuInfoSnapshot
                {
                    Name = gpuHardware.Name ?? "Unknown GPU",
                    LoadPercent = gpuLoad,
                    TemperatureC = gpuTemp,
                    FanRpm = gpuFan,
                    MemoryUsedMb = gpuUsed,
                    MemoryTotalMb = gpuTotal
                });
            }

            return new HwInfoSnapshot
            {
                CpuName = cpuName,
                CpuLoadPercent = cpuLoad,
                CpuTemperatureC = cpuTemp,
                CpuFanRpm = cpuFan,
                Gpus = gpus,
                MemoryUsedMb = memUsed,
                MemoryTotalMb = memTotal,
                DiskReadRateMb = diskReadMb,
                DiskWriteRateMb = diskWriteMb,
                NetworkDownloadRateMb = netDownloadMb,
                NetworkUploadRateMb = netUploadMb
            };
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            if (_available)
            {
                _computer.Close();
            }

            _diskReadCounter?.Dispose();
            _diskWriteCounter?.Dispose();

            _disposed = true;
        }

        private static IEnumerable<IHardware> EnumerateHardware(IComputer computer)
        {
            foreach (var hardware in computer.Hardware)
            {
                foreach (var item in EnumerateHardware(hardware))
                {
                    yield return item;
                }
            }
        }

        private static IEnumerable<IHardware> EnumerateHardware(IHardware hardware)
        {
            yield return hardware;
            foreach (var sub in hardware.SubHardware)
            {
                foreach (var item in EnumerateHardware(sub))
                {
                    yield return item;
                }
            }
        }

        private static IEnumerable<ISensor> GetAllSensors(IHardware hardware)
        {
            foreach (var sensor in hardware.Sensors)
            {
                yield return sensor;
            }

            foreach (var sub in hardware.SubHardware)
            {
                foreach (var sensor in GetAllSensors(sub))
                {
                    yield return sensor;
                }
            }
        }

        private static bool IsGpuHardware(IHardware hardware)
        {
            return hardware.HardwareType == HardwareType.GpuNvidia
                   || hardware.HardwareType == HardwareType.GpuAmd
                   || hardware.HardwareType == HardwareType.GpuIntel;
        }

        private double? SelectLoad(IEnumerable<ISensor> sensors, params string[] preferredTokens)
        {
            var loadSensors = sensors.Where(s => s.SensorType == SensorType.Load).ToList();
            var preferred = FindByName(loadSensors, preferredTokens);
            if (preferred != null)
            {
                return preferred.Value;
            }

            var values = loadSensors.Select(s => s.Value).Where(v => v.HasValue).Select(v => (double)v.GetValueOrDefault()).ToList();
            if (values.Count == 0)
            {
                return null;
            }

            return values.Average();
        }

        private double? SelectTemperature(IEnumerable<ISensor> sensors)
        {
            var tempSensors = sensors.Where(s => s.SensorType == SensorType.Temperature).ToList();
            var preferred = FindByName(tempSensors, "package", "tctl", "tdie", "core", "gpu");
            if (preferred != null)
            {
                return preferred.Value;
            }

            var values = tempSensors.Select(s => s.Value).Where(v => v.HasValue).Select(v => (double)v.GetValueOrDefault()).ToList();
            if (values.Count == 0)
            {
                return null;
            }

            return values.Max();
        }

        private double? SelectFanRpm(IEnumerable<ISensor> sensors, params string[] preferredTokens)
        {
            var fanSensors = sensors.Where(s => s.SensorType == SensorType.Fan).ToList();
            var preferred = FindByName(fanSensors, preferredTokens);
            if (preferred != null)
            {
                return preferred.Value;
            }

            var values = fanSensors.Select(s => s.Value).Where(v => v.HasValue).Select(v => (double)v.GetValueOrDefault()).ToList();
            if (values.Count == 0)
            {
                return null;
            }

            return values.Max();
        }

        private (double? readMb, double? writeMb) SelectDiskRates(IEnumerable<ISensor> sensors)
        {
            var rateSensors = sensors.Where(s => s.SensorType == SensorType.Throughput || s.SensorType == SensorType.Data || s.SensorType == SensorType.SmallData).ToList();
            var readRates = rateSensors.Where(s => s.Name?.IndexOf("Read Rate", StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            var writeRates = rateSensors.Where(s => s.Name?.IndexOf("Write Rate", StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            var readBytesPerSec = readRates.Sum(s => s.Value ?? 0);
            var writeBytesPerSec = writeRates.Sum(s => s.Value ?? 0);

            if (readRates.Count == 0 && writeRates.Count == 0)
            {
                return GetDiskRateFromPerformanceCounters();
            }

            var readMbPerSec = readBytesPerSec / (1024.0 * 1024.0);
            var writeMbPerSec = writeBytesPerSec / (1024.0 * 1024.0);

            return (readRates.Count > 0 ? readMbPerSec : null, writeRates.Count > 0 ? writeMbPerSec : null);
        }

        private (double? readMb, double? writeMb) GetDiskRateFromPerformanceCounters()
        {
            if (!_perfCountersInitialized)
            {
                _perfCountersInitialized = true;
                try
                {
#pragma warning disable CA1416
                    _diskReadCounter = new System.Diagnostics.PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", "_Total", true);
                    _diskWriteCounter = new System.Diagnostics.PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", "_Total", true);
                    _diskReadCounter.NextValue();
                    _diskWriteCounter.NextValue();
#pragma warning restore CA1416
                }
                catch (Exception ex)
                {
                    _logService?.LogError("磁盘性能计数器初始化失败(非管理员可能引发)", ex);
                    _diskReadCounter?.Dispose();
                    _diskWriteCounter?.Dispose();
                    _diskReadCounter = null;
                    _diskWriteCounter = null;
                }
            }

            if (_diskReadCounter != null && _diskWriteCounter != null)
            {
                try
                {
#pragma warning disable CA1416
                    double readMb = _diskReadCounter.NextValue() / (1024.0 * 1024.0);
                    double writeMb = _diskWriteCounter.NextValue() / (1024.0 * 1024.0);
                    return (readMb, writeMb);
#pragma warning restore CA1416
                }
                catch
                {
                    return (null, null);
                }
            }

            return (null, null);
        }

        private (double? downloadMb, double? uploadMb) GetNetworkRates()
        {
            try
            {
                long receivedBytes = 0;
                long sentBytes = 0;
                var interfaces = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
                foreach (var ni in interfaces)
                {
                    if (ni.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up && 
                        ni.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
                    {
                        var stats = ni.GetIPStatistics();
                        receivedBytes += stats.BytesReceived;
                        sentBytes += stats.BytesSent;
                    }
                }

                var now = DateTime.UtcNow;

                if (_lastNetReceivedBytes == -1 || _lastNetSentBytes == -1)
                {
                    _lastNetReceivedBytes = receivedBytes;
                    _lastNetSentBytes = sentBytes;
                    _lastNetTime = now;
                    return (0.0, 0.0);
                }

                var elapsedSeconds = (now - _lastNetTime).TotalSeconds;
                if (elapsedSeconds <= 0) return (0.0, 0.0);

                var downloadRate = (receivedBytes - _lastNetReceivedBytes) / elapsedSeconds / (1024.0 * 1024.0);
                var uploadRate = (sentBytes - _lastNetSentBytes) / elapsedSeconds / (1024.0 * 1024.0);

                if (downloadRate < 0) downloadRate = 0;
                if (uploadRate < 0) uploadRate = 0;

                _lastNetReceivedBytes = receivedBytes;
                _lastNetSentBytes = sentBytes;
                _lastNetTime = now;

                return (downloadRate, uploadRate);
            }
            catch
            {
                return (null, null);
            }
        }

        private (double? used, double? total) SelectMemoryUsage(IEnumerable<ISensor> sensors, bool preferPhysicalMemory = false)
        {
            if (preferPhysicalMemory)
            {
                var physical = GetMemoryFromWindowsAPI();
                if (physical.used.HasValue && physical.total.HasValue)
                {
                    return NormalizeMemoryValues(physical.used, physical.total);
                }
            }

            // 扩展传感器名称匹配范围，提高兼容性
            var used = FindDataValue(sensors, 
                "memory used", "used", "memory usage", 
                "ram used", "physical memory used",
                "gpu memory used", "vram used", "used memory");
            
            var total = FindDataValue(sensors, 
                "memory total", "total", "memory capacity",
                "ram total", "physical memory total",
                "gpu memory total", "vram total", "total memory");

            // 如果没有 total，尝试通过 used + available 计算
            if (!total.HasValue)
            {
                var available = FindDataValue(sensors, 
                    "memory available", "available", "free", 
                    "free memory", "available memory");
                if (used.HasValue && available.HasValue)
                {
                    total = used.Value + available.Value;
                }
            }

            // 如果没有 used，尝试通过 total 和 Load% 计算
            if (!used.HasValue && total.HasValue)
            {
                var loadPercent = FindLoadValue(sensors, "memory");
                if (loadPercent.HasValue)
                {
                    used = total.Value * (loadPercent.Value / 100.0);
                }
            }

            // 如果传感器检测失败，使用 Windows API 回退
            if (!used.HasValue || !total.HasValue)
            {
                var fallbackResult = GetMemoryFromWindowsAPI();
                if (fallbackResult.used.HasValue && fallbackResult.total.HasValue)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[HwInfo] Memory sensor fallback to Windows API: " +
                        $"Used={fallbackResult.used:F1} MB, Total={fallbackResult.total:F1} MB");
                    return fallbackResult;
                }
            }

            return NormalizeMemoryValues(used, total);
        }

        /// <summary>
        /// 查找 Load 类型传感器的值
        /// </summary>
        private double? FindLoadValue(IEnumerable<ISensor> sensors, params string[] nameTokens)
        {
            var loadSensors = sensors.Where(s => s.SensorType == SensorType.Load).ToList();
            var sensor = FindByName(loadSensors, nameTokens);
            return sensor?.Value;
        }

    /// <summary>
    /// 使用 Windows API 获取系统内存信息（回退方案）
    /// </summary>
    private (double? used, double? total) GetMemoryFromWindowsAPI()
        {
            try
            {
                var memStatus = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
                if (GlobalMemoryStatusEx(ref memStatus))
                {
                    var totalMB = memStatus.ullTotalPhys / (1024.0 * 1024.0);
                    var availMB = memStatus.ullAvailPhys / (1024.0 * 1024.0);
                    var usedMB = totalMB - availMB;
                    return (usedMB, totalMB);
                }
            }
            catch (Exception ex)
            {
                _logService?.LogError("内存状态 API 调用失败", ex);
            }
            return (null, null);
        }

        private (double? used, double? total) NormalizeMemoryValues(double? used, double? total)
        {
            if (!used.HasValue || !total.HasValue || total.Value <= 0)
            {
                return (used, total);
            }

            var usedValue = used.Value;
            var totalValue = total.Value;

            // 修复 GB 检测逻辑：
            // - 如果 total < 1024，很可能是 GB 单位（因为系统内存/显存很少低于 1GB）
            // - 原来的 128 阈值对于大内存系统（如 128GB+）会误判
            if (totalValue < 1024 && usedValue < 1024)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[HwInfo] Detected GB units (Total={totalValue} GB), converting to MB");
                return (usedValue * 1024.0, totalValue * 1024.0);
            }

            return (usedValue, totalValue);
        }

        private double? FindDataValue(IEnumerable<ISensor> sensors, params string[] nameTokens)
        {
            var dataSensors = sensors.Where(s => s.SensorType == SensorType.Data || s.SensorType == SensorType.SmallData).ToList();
            var sensor = FindByName(dataSensors, nameTokens);
            return sensor?.Value;
        }

        private ISensor? FindByName(IEnumerable<ISensor> sensors, params string[] nameTokens)
        {
            foreach (var sensor in sensors)
            {
                if (sensor.Value == null)
                {
                    continue;
                }

                var name = sensor.Name ?? "";
                foreach (var token in nameTokens)
                {
                    if (name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return sensor;
                    }
                }
            }

            return null;
        }

        private sealed class UpdateVisitor : IVisitor
        {
            public void VisitComputer(IComputer computer)
            {
                computer.Traverse(this);
            }

            public void VisitHardware(IHardware hardware)
            {
                hardware.Update();
                foreach (var subHardware in hardware.SubHardware)
                {
                    subHardware.Accept(this);
                }
            }

            public void VisitSensor(ISensor sensor) { }

            public void VisitParameter(IParameter parameter) { }
        }
    }

    /// <summary>
    /// 单个 GPU 的信息快照
    /// </summary>
    public sealed class GpuInfoSnapshot
    {
        public string Name { get; set; } = "Unknown GPU";
        public double? LoadPercent { get; set; }
        public double? TemperatureC { get; set; }
        public double? FanRpm { get; set; }
        public double? MemoryUsedMb { get; set; }
        public double? MemoryTotalMb { get; set; }
    }

    public sealed class HwInfoSnapshot
    {
        public string CpuName { get; set; } = "CPU";
        public double? CpuLoadPercent { get; set; }
        public double? CpuTemperatureC { get; set; }
        public double? CpuFanRpm { get; set; }
        public double? MemoryUsedMb { get; set; }
        public double? MemoryTotalMb { get; set; }
        public double? DiskReadRateMb { get; set; }
        public double? DiskWriteRateMb { get; set; }
        public double? NetworkUploadRateMb { get; set; }
        public double? NetworkDownloadRateMb { get; set; }

        /// <summary>
        /// 所有 GPU 的信息列表
        /// </summary>
        public List<GpuInfoSnapshot> Gpus { get; set; } = new();

        // 保留单 GPU 属性以兼容现有代码（返回第一个 GPU 的数据）
        public double? GpuLoadPercent => Gpus.FirstOrDefault()?.LoadPercent;
        public double? GpuTemperatureC => Gpus.FirstOrDefault()?.TemperatureC;
        public double? GpuFanRpm => Gpus.FirstOrDefault()?.FanRpm;
        public double? GpuMemoryUsedMb => Gpus.FirstOrDefault()?.MemoryUsedMb;
        public double? GpuMemoryTotalMb => Gpus.FirstOrDefault()?.MemoryTotalMb;
    }
}
