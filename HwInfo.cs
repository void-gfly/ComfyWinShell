using System;
using System.Collections.Generic;
using System.Linq;
using LibreHardwareMonitor.Hardware;

namespace WpfDesktop.Services
{
    public sealed class HwInfo : IDisposable
    {
        private readonly Computer _computer;
        private readonly UpdateVisitor _visitor = new UpdateVisitor();
        private readonly bool _available;
        private bool _disposed;

        public HwInfo()
        {
            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = true,
                IsMotherboardEnabled = true,
                IsControllerEnabled = true
            };
            try
            {
                _computer.Open();
                _available = true;
            }
            catch
            {
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

            var cpuSensors = cpuHardware.SelectMany(GetAllSensors).ToList();
            var memorySensors = memoryHardware.SelectMany(GetAllSensors).ToList();
            var motherboardSensors = motherboardHardware.SelectMany(GetAllSensors).ToList();

            var cpuName = cpuHardware.FirstOrDefault()?.Name ?? "CPU";
            var cpuLoad = SelectLoad(cpuSensors, "cpu total", "total");
            var cpuTemp = SelectTemperature(cpuSensors);
            var cpuFan = SelectFanRpm(cpuSensors, "cpu") ?? SelectFanRpm(motherboardSensors, "cpu");

            var (memUsed, memTotal) = SelectMemoryUsage(memorySensors);

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
                MemoryTotalMb = memTotal
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

        private static double? SelectLoad(IEnumerable<ISensor> sensors, params string[] preferredTokens)
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

        private static double? SelectTemperature(IEnumerable<ISensor> sensors)
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

        private static double? SelectFanRpm(IEnumerable<ISensor> sensors, params string[] preferredTokens)
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

        private static (double? used, double? total) SelectMemoryUsage(IEnumerable<ISensor> sensors)
        {
            var used = FindDataValue(sensors, "memory used", "gpu memory used", "vram used", "used memory");
            var total = FindDataValue(sensors, "memory total", "gpu memory total", "vram total", "total memory");

            if (!total.HasValue)
            {
                var available = FindDataValue(sensors, "memory available", "free memory");
                if (used.HasValue && available.HasValue)
                {
                    total = used.Value + available.Value;
                }
            }

            return NormalizeMemoryValues(used, total);
        }

        private static (double? used, double? total) NormalizeMemoryValues(double? used, double? total)
        {
            if (!used.HasValue || !total.HasValue || total.Value <= 0)
            {
                return (used, total);
            }

            var usedValue = used.Value;
            var totalValue = total.Value;

            // LHM 的内存/显存数据可能以 GB 报告，数值通常小于 128
            if (totalValue <= 128 && usedValue <= 128)
            {
                return (usedValue * 1024.0, totalValue * 1024.0);
            }

            return (usedValue, totalValue);
        }

        private static double? FindDataValue(IEnumerable<ISensor> sensors, params string[] nameTokens)
        {
            var dataSensors = sensors.Where(s => s.SensorType == SensorType.Data || s.SensorType == SensorType.SmallData).ToList();
            var sensor = FindByName(dataSensors, nameTokens);
            return sensor?.Value;
        }

        private static ISensor? FindByName(IEnumerable<ISensor> sensors, params string[] nameTokens)
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
