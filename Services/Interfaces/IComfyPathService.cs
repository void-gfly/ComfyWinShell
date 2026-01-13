using System.IO;

namespace WpfDesktop.Services.Interfaces;

/// <summary>
/// 自动检测 ComfyUI 安装路径
/// </summary>
public interface IComfyPathService
{
    /// <summary>
    /// 获取检测到的 ComfyUI 路径（Git 仓库目录）
    /// </summary>
    string? ComfyUiPath { get; }

    /// <summary>
    /// 获取检测到的 ComfyUI 根目录（便携版的外层目录）
    /// </summary>
    string? ComfyRootPath { get; }

    /// <summary>
    /// 是否已检测到有效的 ComfyUI 安装
    /// </summary>
    bool IsValid { get; }

    /// <summary>
    /// 检测失败时的错误信息
    /// </summary>
    string? ErrorMessage { get; }

    /// <summary>
    /// 重新检测
    /// </summary>
    void Refresh();
}
