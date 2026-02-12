using WpfDesktop.Models;

namespace WpfDesktop.Services.Interfaces;

/// <summary>
/// ComfyUI 资源管理服务接口
/// </summary>
public interface IResourceService
{
    /// <summary>
    /// 获取所有自定义节点
    /// </summary>
    /// <returns>自定义节点列表</returns>
    Task<IReadOnlyList<CustomNodeInfo>> GetCustomNodesAsync();

    /// <summary>
    /// 获取所有模型文件夹信息
    /// </summary>
    /// <returns>模型文件夹列表</returns>
    Task<IReadOnlyList<ModelFolderInfo>> GetModelFoldersAsync();

    /// <summary>
    /// 获取 extra_model_paths.yaml 中配置的扩展模型文件夹信息
    /// </summary>
    /// <returns>扩展模型文件夹列表</returns>
    Task<IReadOnlyList<ModelFolderInfo>> GetExtraModelFoldersAsync();

    /// <summary>
    /// 获取所有工作流文件
    /// </summary>
    /// <returns>工作流文件列表</returns>
    Task<IReadOnlyList<WorkflowInfo>> GetWorkflowsAsync();

    /// <summary>
    /// 计算指定模型文件夹的磁盘占用
    /// </summary>
    /// <param name="folderPath">文件夹路径</param>
    /// <returns>文件夹大小（字节）和文件数量</returns>
    Task<(long SizeBytes, int FileCount)> CalculateFolderSizeAsync(string folderPath);

    /// <summary>
    /// 获取模型目录描述信息
    /// </summary>
    /// <returns>目录名到描述的字典</returns>
    Task<IReadOnlyDictionary<string, string>> GetModelDescriptionsAsync();
}
