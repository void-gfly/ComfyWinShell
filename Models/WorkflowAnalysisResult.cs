namespace WpfDesktop.Models;

/// <summary>
/// 工作流分析结果
/// </summary>
public class WorkflowAnalysisResult
{
    /// <summary>
    /// 工作流文件路径
    /// </summary>
    public string WorkflowPath { get; set; } = string.Empty;

    /// <summary>
    /// 工作流文件名
    /// </summary>
    public string WorkflowName { get; set; } = string.Empty;

    /// <summary>
    /// 分析时间
    /// </summary>
    public DateTime AnalyzedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// 工作流格式（api 或 full）
    /// </summary>
    public string Format { get; set; } = string.Empty;

    /// <summary>
    /// 工作流中的节点总数
    /// </summary>
    public int TotalNodeCount { get; set; }

    /// <summary>
    /// 所需的自定义节点列表
    /// </summary>
    public List<RequiredCustomNode> RequiredNodes { get; set; } = new();

    /// <summary>
    /// 所需的模型列表
    /// </summary>
    public List<RequiredModel> RequiredModels { get; set; } = new();

    /// <summary>
    /// 缺失的模型数量
    /// </summary>
    public int MissingModelCount => RequiredModels.Count(m => !m.Exists);

    /// <summary>
    /// 缺失的节点数量（非内置且未安装）
    /// </summary>
    public int MissingNodeCount => RequiredNodes.Count(n => !n.IsBuiltIn && !n.Exists);

    /// <summary>
    /// 是否分析成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 错误信息（如果分析失败）
    /// </summary>
    public string? ErrorMessage { get; set; }
}
