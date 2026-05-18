using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using WpfDesktop.Models;

namespace WpfDesktop.Services;

/// <summary>
/// 应用级文件日志写入器。
/// </summary>
internal static class FileLogWriter
{
    private const string LogFileExtension = ".log";
    private static readonly object SyncRoot = new();
    private static string? _logDirectory;
    private static int _retentionDays;
    private static DateTime _lastCleanupDate = DateTime.MinValue;

    /// <summary>
    /// 初始化文件日志目录。
    /// </summary>
    public static string? Initialize(string applicationDirectory, int retentionDays)
    {
        lock (SyncRoot)
        {
            _retentionDays = Math.Max(0, retentionDays);
            _lastCleanupDate = DateTime.MinValue;

            var candidateDirectories = new[]
            {
                Path.Combine(applicationDirectory, "logs"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ComfyShell", "logs")
            };

            foreach (var candidate in candidateDirectories)
            {
                if (TrySetLogDirectory(candidate))
                {
                    CleanupOldFiles(DateTime.Now);
                    return _logDirectory;
                }
            }

            _logDirectory = null;
            return null;
        }
    }

    /// <summary>
    /// 清除测试或重置时的状态。
    /// </summary>
    internal static void ResetForTests()
    {
        lock (SyncRoot)
        {
            _logDirectory = null;
            _retentionDays = 0;
            _lastCleanupDate = DateTime.MinValue;
        }
    }

    /// <summary>
    /// 记录普通日志到文件。
    /// </summary>
    public static void Log(string source, string message, GUILogLevel level = GUILogLevel.Info)
    {
        WriteRecord(level, source, message, exception: null);
    }

    /// <summary>
    /// 记录错误日志到文件，并附加异常详情。
    /// </summary>
    public static void LogError(string source, string message, Exception? exception = null)
    {
        WriteRecord(GUILogLevel.Error, source, message, exception);
    }

    /// <summary>
    /// 直接写入一段原始日志文本。
    /// </summary>
    public static void WriteRaw(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        WriteText(content.TrimEnd() + Environment.NewLine);
    }

    private static void WriteRecord(GUILogLevel level, string source, string message, Exception? exception)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
        var builder = new StringBuilder();
        builder.Append(timestamp)
            .Append(' ')
            .Append('[')
            .Append(level.ToString().ToUpperInvariant())
            .Append(']');

        if (!string.IsNullOrWhiteSpace(source))
        {
            builder.Append(' ').Append(source.Trim());
        }

        var normalizedMessage = Normalize(message);
        if (normalizedMessage.Count > 0)
        {
            builder.Append(' ').Append(normalizedMessage[0]);
            for (var i = 1; i < normalizedMessage.Count; i++)
            {
                builder.AppendLine();
                builder.Append("    ").Append(normalizedMessage[i]);
            }
        }

        if (exception != null)
        {
            builder.AppendLine();
            builder.Append("    Exception: ").Append(exception.GetType().FullName).Append(": ").Append(exception.Message);

            var stackTrace = exception.StackTrace;
            if (!string.IsNullOrWhiteSpace(stackTrace))
            {
                builder.AppendLine();
                builder.Append("    StackTrace:");
                foreach (var line in stackTrace.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
                {
                    builder.AppendLine();
                    builder.Append("        ").Append(line);
                }
            }

            var inner = exception.InnerException;
            var depth = 0;
            while (inner != null)
            {
                builder.AppendLine();
                builder.Append("    Inner[").Append(depth).Append("]: ")
                    .Append(inner.GetType().FullName).Append(": ").Append(inner.Message);

                if (!string.IsNullOrWhiteSpace(inner.StackTrace))
                {
                    builder.AppendLine();
                    builder.Append("    Inner[").Append(depth).Append("] StackTrace:");
                    foreach (var line in inner.StackTrace.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
                    {
                        builder.AppendLine();
                        builder.Append("        ").Append(line);
                    }
                }

                inner = inner.InnerException;
                depth++;
            }
        }

        builder.AppendLine();
        WriteText(builder.ToString());
    }

    private static List<string> Normalize(string message)
    {
        return message.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(line => line.TrimEnd())
            .ToList();
    }

    private static void WriteText(string content)
    {
        lock (SyncRoot)
        {
            if (string.IsNullOrWhiteSpace(_logDirectory))
            {
                return;
            }

            var now = DateTime.Now;
            CleanupOldFiles(now);

            var filePath = Path.Combine(_logDirectory, now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + LogFileExtension);
            Directory.CreateDirectory(_logDirectory);

            var bytes = Encoding.UTF8.GetBytes(content);
            using var stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush();
        }
    }

    private static bool TrySetLogDirectory(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            _logDirectory = directory;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void CleanupOldFiles(DateTime now)
    {
        if (_retentionDays <= 0 || string.IsNullOrWhiteSpace(_logDirectory))
        {
            return;
        }

        if (_lastCleanupDate.Date == now.Date)
        {
            return;
        }

        _lastCleanupDate = now.Date;
        var cutoffDate = now.Date.AddDays(-_retentionDays + 1);

        try
        {
            foreach (var filePath in Directory.EnumerateFiles(_logDirectory, "*" + LogFileExtension))
            {
                var fileName = Path.GetFileNameWithoutExtension(filePath);
                if (!DateTime.TryParseExact(fileName, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fileDate))
                {
                    continue;
                }

                if (fileDate.Date < cutoffDate)
                {
                    File.Delete(filePath);
                }
            }
        }
        catch
        {
            // 日志清理失败不影响主流程。
        }
    }
}