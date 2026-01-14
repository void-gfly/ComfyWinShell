# 内存监控修复文档

## 问题描述

在多台机器上，`HwInfo.cs` 的内存监控功能显示不正确的总内存和已用内存值。

### 原始问题

1. **传感器名称匹配过于严格**：只搜索少量固定名称（如 "memory used", "memory total"），导致在不同硬件配置下无法找到正确的传感器。
2. **GB/MB 检测阈值错误**：使用 `<= 128` 作为 GB 单位判断条件，对于 128GB+ 内存系统会误判。
3. **缺少回退机制**：当 LibreHardwareMonitor 传感器检测失败时，没有其他获取内存信息的方法。

---

## 解决方案

### 1. 扩展传感器名称匹配范围

**修改位置**：`SelectMemoryUsage()` 方法

**改进内容**：
- **Used Memory** 搜索关键词扩展：
  ```csharp
  "memory used", "used", "memory usage", 
  "ram used", "physical memory used",
  "gpu memory used", "vram used", "used memory"
  ```
- **Total Memory** 搜索关键词扩展：
  ```csharp
  "memory total", "total", "memory capacity",
  "ram total", "physical memory total",
  "gpu memory total", "vram total", "total memory"
  ```
- **Available Memory** 搜索关键词扩展：
  ```csharp
  "memory available", "available", "free", 
  "free memory", "available memory"
  ```

### 2. 修复 GB/MB 检测阈值

**修改位置**：`NormalizeMemoryValues()` 方法

**原始逻辑**（有问题）：
```csharp
if (totalValue <= 128 && usedValue <= 128)  // ❌ 128GB+ 系统会误判
{
    return (usedValue * 1024.0, totalValue * 1024.0);
}
```

**修复后逻辑**：
```csharp
if (totalValue < 1024 && usedValue < 1024)  // ✅ 改用 1024 作为阈值
{
    System.Diagnostics.Debug.WriteLine(
        $"[HwInfo] Detected GB units (Total={totalValue} GB), converting to MB");
    return (usedValue * 1024.0, totalValue * 1024.0);
}
```

**原理**：
- 现代系统内存很少低于 1GB（1024 MB）
- 如果 `total < 1024`，几乎可以确定单位是 GB
- 对于 128GB+ 系统，原来的阈值 128 会导致误判（128GB = 131072 MB，不会被转换）

### 3. 添加 Load% 传感器支持

**新增方法**：`FindLoadValue()`

```csharp
private static double? FindLoadValue(IEnumerable<ISensor> sensors, params string[] nameTokens)
{
    var loadSensors = sensors.Where(s => s.SensorType == SensorType.Load).ToList();
    var sensor = FindByName(loadSensors, nameTokens);
    return sensor?.Value;
}
```

**应用场景**：
某些硬件不报告 "memory used"，但报告 "memory load %"。此时可通过以下公式计算：
```csharp
if (!used.HasValue && total.HasValue)
{
    var loadPercent = FindLoadValue(sensors, "memory");
    if (loadPercent.HasValue)
    {
        used = total.Value * (loadPercent.Value / 100.0);
    }
}
```

### 4. 添加 Windows API 回退机制

**新增 P/Invoke 声明**：
```csharp
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
    // ... 其他字段
}
```

**新增方法**：`GetMemoryFromWindowsAPI()`

```csharp
private static (double? used, double? total) GetMemoryFromWindowsAPI()
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
        System.Diagnostics.Debug.WriteLine($"[HwInfo] Windows API memory query failed: {ex.Message}");
    }
    return (null, null);
}
```

**回退逻辑**：
```csharp
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
```

---

## 完整检测流程

修复后的 `SelectMemoryUsage()` 方法采用以下多层回退策略：

```
1. 尝试从传感器直接读取 "memory used" 和 "memory total"
   ├── 扩展搜索关键词（"used", "memory usage", "ram used" 等）
   └── 如果成功 → 跳到步骤 5

2. 如果没有 total，尝试通过 used + available 计算
   └── 搜索 "memory available", "available", "free" 等

3. 如果没有 used，尝试通过 total × Load% 计算
   └── 搜索 SensorType.Load 类型的 "memory" 传感器

4. 如果以上全部失败，回退到 Windows API
   └── 调用 GlobalMemoryStatusEx() 直接从操作系统获取

5. 单位标准化（GB → MB）
   └── 如果 total < 1024，判定为 GB 单位，乘以 1024 转换为 MB
```

---

## 诊断日志

修复后添加了以下调试日志，便于排查问题：

```csharp
// Windows API 回退日志
System.Diagnostics.Debug.WriteLine(
    $"[HwInfo] Memory sensor fallback to Windows API: " +
    $"Used={fallbackResult.used:F1} MB, Total={fallbackResult.total:F1} MB");

// GB 单位转换日志
System.Diagnostics.Debug.WriteLine(
    $"[HwInfo] Detected GB units (Total={totalValue} GB), converting to MB");

// Windows API 失败日志
System.Diagnostics.Debug.WriteLine($"[HwInfo] Windows API memory query failed: {ex.Message}");
```

**查看方式**：
- 在 Visual Studio 中运行时，这些日志会输出到 **输出窗口** (Output Window)
- 或使用 [DebugView](https://learn.microsoft.com/en-us/sysinternals/downloads/debugview) 等工具捕获

---

## 测试建议

### 1. 不同内存容量系统
- ✅ 8GB 系统
- ✅ 16GB 系统
- ✅ 32GB 系统
- ✅ 64GB 系统
- ⚠️ **128GB+ 系统**（原始阈值会误判）

### 2. 不同硬件品牌
- Intel 主板 + Intel CPU
- AMD 主板 + AMD CPU
- 不同品牌主板（ASUS, MSI, Gigabyte, ASRock）

### 3. 不同传感器驱动场景
- LibreHardwareMonitor 正常工作
- LibreHardwareMonitor 部分失败（需要管理员权限）
- LibreHardwareMonitor 完全失败（Windows API 回退）

---

## 修改文件

- ✅ `HwInfo.cs` - 核心修复逻辑

---

## 相关问题

- 原问题报告：用户反馈内存监控显示错误
- 相关文档：
  - [LibreHardwareMonitor GitHub](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor)
  - [GlobalMemoryStatusEx API](https://learn.microsoft.com/en-us/windows/win32/api/sysinfoapi/nf-sysinfoapi-globalmemorystatusex)

---

## 版本历史

- **v1.0** (2026-01-14)：初始修复
  - 扩展传感器名称匹配
  - 修复 GB/MB 阈值检测（128 → 1024）
  - 添加 Load% 传感器支持
  - 添加 Windows API 回退机制
  - 添加诊断日志
