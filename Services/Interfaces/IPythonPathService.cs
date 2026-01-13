namespace WpfDesktop.Services.Interfaces;

public interface IPythonPathService
{
    /// <summary>
    /// 获取 Python 可执行文件路径
    /// </summary>
    string? PythonPath { get; }

    /// <summary>
    /// 是否已找到有效的 Python
    /// </summary>
    bool IsValid { get; }

    /// <summary>
    /// 根据 ComfyUI 根目录解析 Python 路径
    /// 如果配置中已有有效路径则直接使用，否则搜索并保存
    /// </summary>
    /// <param name="comfyRootPath">ComfyUI 根目录</param>
    void Resolve(string comfyRootPath);
}
