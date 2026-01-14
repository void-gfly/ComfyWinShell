using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using WpfDesktop.Models;

namespace WpfDesktop.Converters;

/// <summary>
/// 日志级别到颜色转换器
/// </summary>
[ValueConversion(typeof(GUILogLevel), typeof(Brush))]
public sealed class LogLevelToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is GUILogLevel level)
        {
            return level switch
            {
                GUILogLevel.Success => new SolidColorBrush(Color.FromRgb(0, 255, 0)),      // 绿色 #00FF00
                GUILogLevel.Warning => new SolidColorBrush(Color.FromRgb(255, 165, 0)),    // 橙色 #FFA500
                GUILogLevel.Error => new SolidColorBrush(Color.FromRgb(255, 0, 0)),        // 红色 #FF0000
                _ => new SolidColorBrush(Color.FromRgb(255, 255, 255))                  // 白色 #FFFFFF
            };
        }
        return new SolidColorBrush(Color.FromRgb(255, 255, 255)); // 默认白色
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
