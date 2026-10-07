namespace WpfDesktop.Models.Enums;

// 保留旧 Microsoft.Extensions.Logging.LogLevel 的数值，避免旧 JSON 改变含义。
public enum ComfyLogLevel
{
    LegacyTrace = 0,
    Debug = 1,
    Information = 2,
    Warning = 3,
    Error = 4,
    Critical = 5,
    LegacyNone = 6,
    Detail = 7
}
