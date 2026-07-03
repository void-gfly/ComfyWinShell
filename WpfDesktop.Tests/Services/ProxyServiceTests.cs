using System.Diagnostics;
using Microsoft.Extensions.Options;
using WpfDesktop.Models;
using WpfDesktop.Services;
using WpfDesktop.Services.Interfaces;
using Xunit;

namespace WpfDesktop.Tests.Services;

public sealed class ProxyServiceTests
{
    [Fact]
    public void ConfigureProcessProxy_AppliesPipMirrorWhenHttpProxyIsDisabled()
    {
        var settings = new AppSettings
        {
            Proxy = new ProxySettings
            {
                Enabled = false,
                Server = string.Empty
            },
            PipMirror = new PipMirrorSettings
            {
                Enabled = true,
                IndexUrl = "https://pypi.example/simple",
                TrustedHost = true
            }
        };
        var service = new ProxyService(new FakeOptionsMonitor(settings), new FakeLogService());
        var startInfo = new ProcessStartInfo();

        service.ConfigureProcessProxy(startInfo);

        Assert.Equal("https://pypi.example/simple", startInfo.Environment["PIP_INDEX_URL"]);
        Assert.Equal("pypi.example", startInfo.Environment["PIP_TRUSTED_HOST"]);
        Assert.False(startInfo.Environment.ContainsKey("HTTP_PROXY"));
    }

    private sealed class FakeOptionsMonitor : IOptionsMonitor<AppSettings>
    {
        public FakeOptionsMonitor(AppSettings settings)
        {
            CurrentValue = settings;
        }

        public AppSettings CurrentValue { get; }
        public AppSettings Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<AppSettings, string?> listener) => null;
    }

    private sealed class FakeLogService : ILogService
    {
        public event EventHandler<string>? LogReceived;
        public event EventHandler<LogEntry>? LogEntryReceived;
        public void Log(string message) => Log(message, GUILogLevel.Info);
        public void Log(string message, GUILogLevel level) => LogEntryReceived?.Invoke(this, new LogEntry { Message = message, Level = level });
        public void LogError(string message, Exception? exception = null) => Log(message, GUILogLevel.Error);
    }
}
