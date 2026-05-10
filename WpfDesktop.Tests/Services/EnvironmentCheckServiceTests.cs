using System.Diagnostics;
using WpfDesktop.Services;
using Xunit;

namespace WpfDesktop.Tests.Services;

public sealed class EnvironmentCheckServiceTests
{
    [Theory]
    [InlineData("python")]
    [InlineData("python.exe")]
    [InlineData(@"D:\ComfyAPP\comfyUI-中文目录\python_embeded\python.exe")]
    public void ApplyPythonUtf8Environment_SetsUtf8ForPythonCommands(string command)
    {
        var startInfo = new ProcessStartInfo();

        EnvironmentCheckService.ApplyPythonUtf8Environment(startInfo, command);

        Assert.Equal("1", startInfo.EnvironmentVariables["PYTHONUTF8"]);
        Assert.Equal("utf-8", startInfo.EnvironmentVariables["PYTHONIOENCODING"]);
    }

    [Fact]
    public void ApplyPythonUtf8Environment_DoesNotTouchNonPythonCommands()
    {
        var startInfo = new ProcessStartInfo();

        EnvironmentCheckService.ApplyPythonUtf8Environment(startInfo, "git");

        Assert.False(startInfo.EnvironmentVariables.ContainsKey("PYTHONUTF8"));
        Assert.False(startInfo.EnvironmentVariables.ContainsKey("PYTHONIOENCODING"));
    }
}
