using WpfDesktop.Services;
using Xunit;

namespace WpfDesktop.Tests.Services;

public sealed class AppInstanceLockHelperTests
{
    [Fact]
    public void BuildMutexName_WhenAppNameContainsInvalidCharacters_ReturnsSanitizedName()
    {
        var name = AppInstanceLockHelper.BuildMutexName("My App:Test");

        Assert.Equal(@"Local\ComfyShell.AppInstance.My_App_Test", name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BuildMutexName_WhenAppNameIsMissing_UsesDefaultAppName(string? appName)
    {
        var name = AppInstanceLockHelper.BuildMutexName(appName);

        Assert.Equal(@"Local\ComfyShell.AppInstance.ComfyShell", name);
    }
}
