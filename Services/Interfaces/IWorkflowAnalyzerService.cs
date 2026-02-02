using WpfDesktop.Models;

namespace WpfDesktop.Services.Interfaces;

/// <summary>
/// ComfyUI 工作流分析服务接口
/// </summary>
public interface IWorkflowAnalyzerService
{
    /// <summary>
    /// 分析工作流文件
    /// </summary>
    /// <param name="workflowPath">工作流文件路径</param>
    /// <returns>分析结果</returns>
    Task<WorkflowAnalysisResult> AnalyzeWorkflowAsync(string workflowPath);

    /// <summary>
    /// 检查文件是否为工作流文件
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>是否为工作流文件</returns>
    bool IsWorkflowFile(string filePath);

    /// <summary>
    /// 清除节点缓存并重新扫描（异步）
    /// </summary>
    Task RefreshNodeCacheAsync();
}
