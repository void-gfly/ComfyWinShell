using WpfDesktop.Models;

namespace WpfDesktop.Services.Interfaces;

/// <summary>
/// 工作流打包服务接口
/// </summary>
public interface IWorkflowPackagerService
{
    /// <summary>
    /// 打包工作流
    /// </summary>
    /// <param name="analysisResult">工作流分析结果</param>
    /// <param name="targetPath">打包目标目录</param>
    /// <param name="progress">进度报告（文本消息）</param>
    /// <param name="progressPercentage">进度百分比</param>
    /// <returns>打包结果</returns>
    Task<WorkflowPackageResult> PackageWorkflowAsync(
        WorkflowAnalysisResult analysisResult,
        string targetPath,
        IProgress<string>? progress = null,
        IProgress<double>? progressPercentage = null);
}
