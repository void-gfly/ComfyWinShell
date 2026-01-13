using System.IO;
using WpfDesktop.Services.Interfaces;

namespace WpfDesktop.Services;

/// <summary>
/// 自动检测 ComfyUI 安装路径
/// 预期目录结构：
/// ParentDir/
/// ├── WpfDesktop/           (本应用)
/// ├── ComfyUI_windows_portable/
/// │   └── ComfyUI/          (Git 仓库) ← 目标路径
/// 或者：
/// ParentDir/
/// ├── WpfDesktop/           (本应用)
/// └── ComfyUI/              (Git 仓库) ← 目标路径
/// </summary>
public class ComfyPathService : IComfyPathService
{
    public string? ComfyUiPath { get; private set; }
    public string? ComfyRootPath { get; private set; }
    public bool IsValid { get; private set; }
    public string? ErrorMessage { get; private set; }

    public ComfyPathService()
    {
        Refresh();
    }

    public void Refresh()
    {
        ComfyUiPath = null;
        ComfyRootPath = null;
        IsValid = false;
        ErrorMessage = null;

        var appDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);

        // 向上查找，最多 3 层
        var currentDir = appDir;
        for (int i = 0; i < 3; i++)
        {
            var parentDir = Path.GetDirectoryName(currentDir);
            if (string.IsNullOrEmpty(parentDir))
                break;

            // 检测方式 1: 同级的 ComfyUI_windows_portable/ComfyUI
            var portablePath = Path.Combine(parentDir, "ComfyUI_windows_portable", "ComfyUI");
            if (IsValidComfyUiDirectory(portablePath))
            {
                ComfyRootPath = Path.Combine(parentDir, "ComfyUI_windows_portable");
                ComfyUiPath = portablePath;
                IsValid = true;
                return;
            }

            // 检测方式 2: 同级的 ComfyUI 目录
            var directPath = Path.Combine(parentDir, "ComfyUI");
            if (IsValidComfyUiDirectory(directPath))
            {
                ComfyRootPath = parentDir;
                ComfyUiPath = directPath;
                IsValid = true;
                return;
            }

            // 检测方式 3: 当前目录本身就是 ComfyUI_windows_portable 的子目录
            if (Path.GetFileName(parentDir) == "ComfyUI_windows_portable")
            {
                var comfyPath = Path.Combine(parentDir, "ComfyUI");
                if (IsValidComfyUiDirectory(comfyPath))
                {
                    ComfyRootPath = parentDir;
                    ComfyUiPath = comfyPath;
                    IsValid = true;
                    return;
                }
            }

            currentDir = parentDir;
        }

        // 未找到
        ErrorMessage = GetNotFoundMessage(appDir);
    }

    private static bool IsValidComfyUiDirectory(string path)
    {
        if (!Directory.Exists(path))
            return false;

        // 检查是否有 main.py（ComfyUI 入口文件）
        var mainPy = Path.Combine(path, "main.py");
        if (File.Exists(mainPy))
            return true;

        // 检查是否有 .git 目录（Git 仓库）
        var gitDir = Path.Combine(path, ".git");
        if (Directory.Exists(gitDir))
            return true;

        return false;
    }

    private static string GetNotFoundMessage(string appDir)
    {
        var parentDir = Path.GetDirectoryName(appDir) ?? appDir;

        return $"""
            未找到 ComfyUI 安装目录。

            请确保目录结构如下：

            {parentDir}\
            ├── ComfyUI_windows_portable\
            │   └── ComfyUI\          ← ComfyUI 核心代码
            │   └── python_embeded\
            │   └── run_nvidia_gpu.bat
            └── WpfDesktop\           ← 本应用程序

            或者：

            {parentDir}\
            ├── ComfyUI\              ← ComfyUI 核心代码
            └── WpfDesktop\           ← 本应用程序

            请将本应用放置在 ComfyUI 的同级目录下。
            """;
    }
}
