using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using WpfDesktop.Models;
using WpfDesktop.Services.Interfaces;

namespace WpfDesktop.Services;

/// <summary>
/// 默认外部命令执行器。
/// </summary>
public sealed class ExternalProcessRunner : IExternalProcessRunner
{
    private readonly IProxyService _proxyService;

    public ExternalProcessRunner(IProxyService proxyService)
    {
        _proxyService = proxyService;
    }

    public async Task<ExternalProcessResult> RunAsync(ExternalProcessCommand command, CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = command.FileName,
            Arguments = command.Arguments,
            WorkingDirectory = command.WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        _proxyService.ConfigureProcessProxy(startInfo);

        foreach (var (key, value) in command.Environment)
        {
            startInfo.Environment[key] = value;
        }

        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        using var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                outputBuilder.AppendLine(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                errorBuilder.AppendLine(e.Data);
            }
        };

        try
        {
            process.Start();
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 2)
        {
            return new ExternalProcessResult(-1, string.Empty, $"未找到命令: {command.FileName}");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            throw;
        }

        return new ExternalProcessResult(process.ExitCode, outputBuilder.ToString(), errorBuilder.ToString());
    }
}
