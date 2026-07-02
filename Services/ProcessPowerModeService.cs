using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace WpfDesktop.Services;

internal static class ProcessPowerModeService
{
    internal const uint ProcessPowerThrottlingCurrentVersion = 1;
    internal const uint ProcessPowerThrottlingExecutionSpeed = 0x1;
    internal const uint ProcessModeBackgroundEnd = 0x00200000;

    private const int ProcessPowerThrottlingInformationClass = 4;

    internal static ProcessPowerThrottlingState CreateDisableExecutionSpeedThrottlingState()
    {
        return new ProcessPowerThrottlingState
        {
            Version = ProcessPowerThrottlingCurrentVersion,
            ControlMask = ProcessPowerThrottlingExecutionSpeed,
            StateMask = 0
        };
    }

    internal static bool ShouldRestoreNormalPriority(ProcessPriorityClass priorityClass)
    {
        return priorityClass is ProcessPriorityClass.Idle or ProcessPriorityClass.BelowNormal;
    }

    internal static bool TryDisableEfficiencyMode(Process process, Action<string>? logWarning = null)
    {
        var powerThrottlingDisabled = false;
        var backgroundModeEnded = false;
        var priorityRestored = false;

        try
        {
            if (process.HasExited)
            {
                return false;
            }

            var state = CreateDisableExecutionSpeedThrottlingState();
            var success = SetProcessInformation(
                process.SafeHandle,
                ProcessPowerThrottlingInformationClass,
                ref state,
                Marshal.SizeOf<ProcessPowerThrottlingState>());

            if (!success)
            {
                var errorCode = Marshal.GetLastWin32Error();
                logWarning?.Invoke($"关闭进程效能模式失败 PID={process.Id}，Win32Error={errorCode}");
            }

            powerThrottlingDisabled = success;
            backgroundModeEnded = SetPriorityClass(process.SafeHandle, ProcessModeBackgroundEnd);
            if (!backgroundModeEnded)
            {
                var errorCode = Marshal.GetLastWin32Error();
                logWarning?.Invoke($"关闭进程后台模式失败 PID={process.Id}，Win32Error={errorCode}");
            }

            if (ShouldRestoreNormalPriority(process.PriorityClass))
            {
                process.PriorityClass = ProcessPriorityClass.Normal;
                priorityRestored = true;
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or PlatformNotSupportedException or NotSupportedException or Win32Exception)
        {
            logWarning?.Invoke($"关闭进程效能模式失败: {ex.Message}");
            return false;
        }

        return powerThrottlingDisabled || backgroundModeEnded || priorityRestored;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetProcessInformation(
        SafeProcessHandle hProcess,
        int processInformationClass,
        ref ProcessPowerThrottlingState processInformation,
        int processInformationSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetPriorityClass(SafeProcessHandle hProcess, uint dwPriorityClass);

    [StructLayout(LayoutKind.Sequential)]
    internal struct ProcessPowerThrottlingState
    {
        public uint Version;
        public uint ControlMask;
        public uint StateMask;
    }
}
