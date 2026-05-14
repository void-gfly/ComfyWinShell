using System.Globalization;
using System.Windows.Media;
using WpfDesktop.Converters;
using WpfDesktop.Models;
using Xunit;

namespace WpfDesktop.Tests.Converters;

public sealed class LogLevelToColorConverterTests
{
    [Fact]
    public void Convert_ReturnsMutedWhite_ForComfyOutput()
    {
        var converter = new LogLevelToColorConverter();

        var brush = Assert.IsType<SolidColorBrush>(
            converter.Convert(GUILogLevel.ComfyRaw, typeof(Brush), null!, CultureInfo.InvariantCulture));

        Assert.Equal(Color.FromRgb(220, 220, 220), brush.Color);
    }
}
