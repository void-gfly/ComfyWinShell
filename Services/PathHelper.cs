using System.IO;

namespace WpfDesktop.Services;

/// <summary>
/// 路径解析辅助类
/// </summary>
public static class PathHelper
{
    /// <summary>
    /// 解析数据根目录路径：相对路径基于 exe 所在目录，绝对路径或环境变量路径直接展开
    /// </summary>
    public static string ResolveDataRoot(string path)
    {
        if (Path.IsPathRooted(path) || path.Contains('%'))
        {
            return Environment.ExpandEnvironmentVariables(path);
        }
        var exeDir = AppContext.BaseDirectory;
        return Path.GetFullPath(Path.Combine(exeDir, path));
    }
}
