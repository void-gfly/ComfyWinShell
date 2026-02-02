using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using WpfDesktop.Models;
using WpfDesktop.Models.Enums;
using WpfDesktop.Services.Interfaces;

namespace WpfDesktop.Services;

/// <summary>
/// ComfyUI 工作流分析服务实现
/// </summary>
public partial class WorkflowAnalyzerService : IWorkflowAnalyzerService
{
    private readonly IComfyPathService _comfyPathService;
    private readonly IPythonPathService _pythonPathService;
    private readonly ILogService _logService;

    // 已安装节点缓存
    private HashSet<string>? _installedNodesCache;
    private Dictionary<string, string>? _nodeToPackageCache;
    private Dictionary<string, string>? _displayNameToTypeCache;

    /// <summary>
    /// 模型加载器配置映射
    /// key: class_type, value: (参数名, 主目录, 模型类型, 备用目录列表)
    /// </summary>
    private static readonly Dictionary<string, (string ParamName, string ModelDir, ModelType Type, string[] AlternateDirs)> ModelLoaderMapping = new()
    {
        ["CheckpointLoaderSimple"] = ("ckpt_name", "checkpoints", ModelType.Checkpoint, Array.Empty<string>()),
        ["CheckpointLoader"] = ("ckpt_name", "checkpoints", ModelType.Checkpoint, Array.Empty<string>()),
        ["LoraLoader"] = ("lora_name", "loras", ModelType.Lora, Array.Empty<string>()),
        ["LoraLoaderModelOnly"] = ("lora_name", "loras", ModelType.Lora, Array.Empty<string>()),
        ["VAELoader"] = ("vae_name", "vae", ModelType.Vae, Array.Empty<string>()),
        ["ControlNetLoader"] = ("control_net_name", "controlnet", ModelType.ControlNet, Array.Empty<string>()),
        ["DiffControlNetLoader"] = ("control_net_name", "controlnet", ModelType.ControlNet, Array.Empty<string>()),
        // CLIP 模型可能在 clip 或 text_encoders 目录
        ["CLIPLoader"] = ("clip_name", "clip", ModelType.Clip, new[] { "text_encoders" }),
        ["DualCLIPLoader"] = ("clip_name1", "clip", ModelType.Clip, new[] { "text_encoders" }),
        ["CLIPVisionLoader"] = ("clip_name", "clip_vision", ModelType.ClipVision, Array.Empty<string>()),
        ["UNETLoader"] = ("unet_name", "unet", ModelType.Unet, new[] { "diffusion_models" }),
        ["StyleModelLoader"] = ("style_model_name", "style_models", ModelType.StyleModel, Array.Empty<string>()),
        ["UpscaleModelLoader"] = ("model_name", "upscale_models", ModelType.UpscaleModel, Array.Empty<string>()),
        ["HypernetworkLoader"] = ("hypernetwork_name", "hypernetworks", ModelType.Hypernetwork, Array.Empty<string>()),
        ["GLIGENLoader"] = ("gligen_name", "gligen", ModelType.Gligen, Array.Empty<string>()),
    };

    /// <summary>
    /// ComfyUI 内置节点列表（不需要额外安装）
    /// </summary>
    private static readonly HashSet<string> BuiltInNodes = new(StringComparer.OrdinalIgnoreCase)
    {
        // Loaders
        "CheckpointLoaderSimple", "CheckpointLoader", "LoraLoader", "LoraLoaderModelOnly",
        "VAELoader", "ControlNetLoader", "DiffControlNetLoader", "CLIPLoader", "DualCLIPLoader",
        "CLIPVisionLoader", "UNETLoader", "StyleModelLoader", "UpscaleModelLoader",
        "HypernetworkLoader", "GLIGENLoader", "unCLIPCheckpointLoader",
        
        // Sampling
        "KSampler", "KSamplerAdvanced", "SamplerCustom", "SamplerCustomAdvanced",
        "KSamplerSelect", "SamplerDPMPP_2M_SDE", "SamplerDPMPP_SDE", "SamplerDPMPP_3M_SDE",
        "SamplerEulerAncestral", "SamplerLMS", "SamplerDPMAdaptive", "SamplerDPMPP_2S_Ancestral",
        "BasicScheduler", "KarrasScheduler", "ExponentialScheduler", "PolyexponentialScheduler",
        "SDTurboScheduler", "BetaSamplingScheduler", "LCMScheduler",
        "SplitSigmas", "SplitSigmasDenoise", "FlipSigmas",
        
        // Guiders
        "CFGGuider", "DualCFGGuider", "BasicGuider", "DisableNoise", "RandomNoise",
        
        // Conditioning
        "CLIPTextEncode", "CLIPTextEncodeSDXL", "CLIPSetLastLayer", "ConditioningCombine",
        "ConditioningAverage", "ConditioningConcat", "ConditioningSetArea",
        "ConditioningSetAreaPercentage", "ConditioningSetMask", "ConditioningZeroOut",
        "ControlNetApply", "ControlNetApplyAdvanced", "GLIGENTextBoxApply",
        "unCLIPConditioning", "CLIPVisionEncode", "StyleModelApply",
        "ConditioningSetTimestepRange",
        
        // Latent
        "EmptyLatentImage", "LatentUpscale", "LatentUpscaleBy", "LatentFromBatch",
        "RepeatLatentBatch", "LatentBlend", "LatentComposite", "LatentCompositeMasked",
        "LatentCrop", "LatentFlip", "LatentRotate", "LatentBatch", "LatentBatchSeedBehavior",
        "LatentInterpolate", "LatentAdd", "LatentSubtract", "LatentMultiply",
        
        // Image
        "LoadImage", "LoadImageMask", "SaveImage", "PreviewImage", "ImageScale",
        "ImageScaleBy", "ImageUpscaleWithModel", "ImageInvert", "ImageBatch",
        "ImagePadForOutpaint", "EmptyImage", "ImageCrop", "ImageCompositeMasked",
        "ImageBlend", "ImageBlur", "ImageQuantize", "ImageSharpen",
        "ImageScaleToTotalPixels", "ImageFromBatch", "RebatchImages",
        
        // Mask
        "ImageToMask", "MaskToImage", "SolidMask", "InvertMask", "CropMask",
        "MaskComposite", "FeatherMask", "GrowMask", "ThresholdMask",
        
        // VAE
        "VAEDecode", "VAEEncode", "VAEEncodeForInpaint", "VAEDecodeTiled", "VAEEncodeTiled",
        
        // Model
        "ModelSamplingDiscrete", "ModelSamplingContinuousEDM", "ModelSamplingContinuousV",
        "RescaleCFG", "PatchModelAddDownscale", "FreeU", "FreeU_V2",
        "ModelMergeSimple", "ModelMergeBlocks", "ModelMergeSubtract", "ModelMergeAdd",
        "CLIPMergeSimple", "CLIPMergeSubtract", "CLIPMergeAdd",
        
        // Utilities
        "Note", "PrimitiveNode", "Reroute",
        
        // GetSet Nodes (内置)
        "GetNode", "SetNode",
        
        // Primitives / Constants
        "INTConstant", "FLOATConstant", "STRINGConstant", "BOOLConstant",
        
        // 其他常见内置节点
        "MarkdownNote",
    };

    public WorkflowAnalyzerService(
        IComfyPathService comfyPathService, 
        IPythonPathService pythonPathService,
        ILogService logService)
    {
        _comfyPathService = comfyPathService;
        _pythonPathService = pythonPathService;
        _logService = logService;
    }

    public bool IsWorkflowFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return false;
        return filePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
    }

    public async Task RefreshNodeCacheAsync()
    {
        _logService.Log("开始刷新节点缓存...");
        _installedNodesCache = null;
        _nodeToPackageCache = null;
        _displayNameToTypeCache = null;
        await ScanInstalledNodesAsync();
        _logService.Log($"节点缓存刷新完成，共扫描到 {_installedNodesCache?.Count ?? 0} 个节点");
    }

    public async Task<WorkflowAnalysisResult> AnalyzeWorkflowAsync(string workflowPath)
    {
        var result = new WorkflowAnalysisResult
        {
            WorkflowPath = workflowPath,
            WorkflowName = Path.GetFileName(workflowPath),
            AnalyzedAt = DateTime.Now
        };

        try
        {
            if (!File.Exists(workflowPath))
            {
                result.Success = false;
                result.ErrorMessage = "工作流文件不存在";
                return result;
            }

            var jsonContent = await File.ReadAllTextAsync(workflowPath);
            using var document = JsonDocument.Parse(jsonContent);
            var root = document.RootElement;

            // 检测工作流格式
            var isApiFormat = DetectApiFormat(root);
            result.Format = isApiFormat ? "API" : "UI";

            // 解析节点
            var nodes = ParseNodes(root, isApiFormat);
            result.TotalNodeCount = nodes.Count;

            // 提取自定义节点
            result.RequiredNodes = await ExtractCustomNodesAsync(nodes);

            // 提取模型引用
            result.RequiredModels = await ExtractModelsAsync(nodes, isApiFormat);

            result.Success = true;
        }
        catch (JsonException ex)
        {
            result.Success = false;
            result.ErrorMessage = $"JSON 解析失败: {ex.Message}";
            _logService.LogError("工作流 JSON 解析失败", ex);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"分析失败: {ex.Message}";
            _logService.LogError("工作流分析失败", ex);
        }

        return result;
    }

    /// <summary>
    /// 检测是否为 API 格式（节点 ID 作为键）
    /// </summary>
    private static bool DetectApiFormat(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) return false;

        // API 格式的键是数字字符串，且直接包含 class_type
        foreach (var prop in root.EnumerateObject())
        {
            if (int.TryParse(prop.Name, out _))
            {
                if (prop.Value.ValueKind == JsonValueKind.Object &&
                    prop.Value.TryGetProperty("class_type", out _))
                {
                    return true;
                }
            }
            // UI 格式有 nodes 数组
            if (prop.Name == "nodes" && prop.Value.ValueKind == JsonValueKind.Array)
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// 解析工作流节点
    /// </summary>
    private static List<WorkflowNode> ParseNodes(JsonElement root, bool isApiFormat)
    {
        var nodes = new List<WorkflowNode>();

        if (isApiFormat)
        {
            // API 格式：{ "1": { "class_type": "...", "inputs": {...} } }
            foreach (var prop in root.EnumerateObject())
            {
                if (!int.TryParse(prop.Name, out _)) continue;
                if (prop.Value.ValueKind != JsonValueKind.Object) continue;

                var node = new WorkflowNode
                {
                    Id = prop.Name
                };

                if (prop.Value.TryGetProperty("class_type", out var classType))
                {
                    node.ClassType = classType.GetString() ?? "";
                }

                if (prop.Value.TryGetProperty("inputs", out var inputs))
                {
                    node.Inputs = inputs;
                }

                if (prop.Value.TryGetProperty("_meta", out var meta) &&
                    meta.TryGetProperty("title", out var title))
                {
                    node.Title = title.GetString();
                }

                nodes.Add(node);
            }
        }
        else
        {
            // UI 格式：{ "nodes": [ { "id": 1, "type": "...", "widgets_values": [...] } ] }
            if (!root.TryGetProperty("nodes", out var nodesArray)) return nodes;
            if (nodesArray.ValueKind != JsonValueKind.Array) return nodes;

            foreach (var nodeElement in nodesArray.EnumerateArray())
            {
                var node = new WorkflowNode();

                if (nodeElement.TryGetProperty("id", out var id))
                {
                    node.Id = id.ValueKind == JsonValueKind.Number
                        ? id.GetInt32().ToString()
                        : id.GetString() ?? "";
                }

                if (nodeElement.TryGetProperty("type", out var type))
                {
                    node.ClassType = type.GetString() ?? "";
                }

                if (nodeElement.TryGetProperty("widgets_values", out var widgetsValues))
                {
                    node.WidgetsValues = widgetsValues;
                }

                if (nodeElement.TryGetProperty("title", out var title))
                {
                    node.Title = title.GetString();
                }

                nodes.Add(node);
            }
        }

        return nodes;
    }

    /// <summary>
    /// 提取自定义节点（异步）
    /// </summary>
    private async Task<List<RequiredCustomNode>> ExtractCustomNodesAsync(List<WorkflowNode> nodes)
    {
        // 懒加载：首次使用时扫描已安装节点
        if (_installedNodesCache == null || _nodeToPackageCache == null)
        {
            await ScanInstalledNodesAsync();
        }

        var nodeTypeCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in nodes)
        {
            if (string.IsNullOrEmpty(node.ClassType)) continue;

            if (nodeTypeCount.ContainsKey(node.ClassType))
                nodeTypeCount[node.ClassType]++;
            else
                nodeTypeCount[node.ClassType] = 1;
        }

        var result = nodeTypeCount
            .Select(kvp =>
            {
                var normalizedType = GetNormalizedNodeType(kvp.Key);

                return new RequiredCustomNode
                {
                    NodeType = normalizedType,
                    DisplayName = kvp.Key,
                    UsageCount = kvp.Value,
                    IsBuiltIn = BuiltInNodes.Contains(normalizedType),
                    Exists = DetermineNodeExists(normalizedType),
                    PackageName = GetPackageName(normalizedType)
                };
            })
            .OrderByDescending(n => n.UsageCount)
            .ThenBy(n => n.NodeType)
            .ToList();
        
        // 调试日志：显示缺失节点列表
        var missingNodes = result.Where(n => !n.Exists && !n.IsBuiltIn).ToList();
        if (missingNodes.Count > 0)
        {
            _logService.Log($"⚠ 工作流中有 {missingNodes.Count} 个节点未在已安装列表中找到:");
            foreach (var node in missingNodes.Take(10))  // 只显示前10个
            {
                _logService.Log($"  - {node.NodeType}");
            }
            if (missingNodes.Count > 10)
            {
                _logService.Log($"  ... 还有 {missingNodes.Count - 10} 个");
            }
        }
        
        return result;
    }

    /// <summary>
    /// 判断节点是否存在
    /// </summary>
    private bool DetermineNodeExists(string nodeType)
    {
        // 内置节点始终存在
        if (BuiltInNodes.Contains(nodeType)) return true;
        
        // 检查缓存中是否有该节点
        var exists = _installedNodesCache?.Contains(nodeType) ?? false;
        
        // 调试日志：显示匹配失败的节点
        if (!exists && _installedNodesCache != null)
        {
            // 尝试查找相似节点（可能是大小写问题）
            var similar = _installedNodesCache.FirstOrDefault(n => 
                n.Equals(nodeType, StringComparison.OrdinalIgnoreCase));
            if (similar != null)
            {
                _logService.Log($"⚠ 节点大小写不匹配: 工作流={nodeType}, 已安装={similar}");
            }
        }
        
        return exists;
    }

    private string GetNormalizedNodeType(string nodeType)
    {
        if (string.IsNullOrWhiteSpace(nodeType)) return nodeType;

        // 内置的 NODE_CLASS_MAPPINGS key：原样返回
        if (_installedNodesCache?.Contains(nodeType) == true) return nodeType;

        // UI 保存的展示名：尝试映射
        if (_displayNameToTypeCache?.TryGetValue(nodeType, out var classType) == true &&
            !string.IsNullOrWhiteSpace(classType))
        {
            return classType;
        }

        return nodeType;
    }

    /// <summary>
    /// 获取节点所属包名
    /// </summary>
    private string? GetPackageName(string nodeType)
    {
        if (BuiltInNodes.Contains(nodeType)) return "ComfyUI (内置)";
        
        return _nodeToPackageCache?.TryGetValue(nodeType, out var packageName) == true 
            ? packageName 
            : null;
    }

    /// <summary>
    /// 扫描已安装的自定义节点（异步）
    /// </summary>
    private async Task ScanInstalledNodesAsync()
    {
        _installedNodesCache = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _nodeToPackageCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _displayNameToTypeCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!_comfyPathService.IsValid || string.IsNullOrEmpty(_comfyPathService.ComfyUiPath))
        {
            return;
        }

        var customNodesPath = Path.Combine(_comfyPathService.ComfyUiPath, "custom_nodes");
        if (!Directory.Exists(customNodesPath))
        {
            return;
        }

        try
        {
            // 使用 Python 脚本动态扫描节点（唯一准确的方法）
            _logService.Log("开始使用 Python 脚本扫描自定义节点...");
            var pythonScanSuccess = await ScanNodesWithPythonScriptAsync();
            
            if (pythonScanSuccess)
            {
                _logService.Log($"节点扫描完成，共发现 {_installedNodesCache.Count} 个自定义节点");
            }
            else
            {
                _logService.Log("节点扫描失败：Python 脚本执行失败或未找到 Python 解释器");
            }
        }
        catch (Exception ex)
        {
            _logService.LogError($"扫描自定义节点时发生异常: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 使用 Python 脚本动态扫描节点（异步方法）
    /// </summary>
    private async Task<bool> ScanNodesWithPythonScriptAsync()
    {
        try
        {
            // 查找 Python 解释器
            var pythonPath = FindPythonExecutable();
            if (string.IsNullOrEmpty(pythonPath))
            {
                _logService.Log("❌ 节点扫描失败：未找到 Python 解释器");
                _logService.Log("提示：请确保安装了 Python，或 ComfyUI 目录中存在 python_embeded 或 venv");
                return false;
            }

            _logService.Log($"✓ 找到 Python 解释器: {pythonPath}");

            // 查找 scan_nodes.py 脚本
            var scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "scan_nodes.py");
            if (!File.Exists(scriptPath))
            {
                _logService.Log($"❌ 节点扫描失败：未找到扫描脚本");
                _logService.Log($"预期路径: {scriptPath}");
                return false;
            }

            _logService.Log($"✓ 找到扫描脚本: {scriptPath}");
            _logService.Log($"正在扫描: {_comfyPathService.ComfyUiPath}");

            // 运行 Python 脚本（异步）
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = pythonPath,
                    Arguments = $"\"{scriptPath}\" \"{_comfyPathService.ComfyUiPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8
                }
            };

            // 设置环境变量：禁用 Python 警告（避免污染 JSON 输出）
            process.StartInfo.EnvironmentVariables["PYTHONWARNINGS"] = "ignore";
            
            process.Start();
            
            // 异步读取输出（避免死锁）
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            
            await process.WaitForExitAsync();
            
            var output = await outputTask;
            var error = await errorTask;

            if (process.ExitCode != 0)
            {
                _logService.Log($"❌ Python 脚本执行失败 (退出码: {process.ExitCode})");
                if (!string.IsNullOrEmpty(error))
                {
                    _logService.Log($"错误详情: {error}");
                }
                return false;
            }

            // 解析 JSON 输出
            if (string.IsNullOrWhiteSpace(output))
            {
                _logService.Log("❌ Python 脚本返回空输出");
                if (!string.IsNullOrWhiteSpace(error))
                {
                    _logService.Log($"错误信息: {error}");
                }
                return false;
            }

            // 记录原始输出（调试用）
            _logService.Log($"Python 脚本输出长度: {output.Length} 字节");
            
            // 清理输出：查找 JSON 开始和结束标记（容错处理）
            var jsonStart = output.IndexOf('{');
            var jsonEnd = output.LastIndexOf('}');
            
            if (jsonStart == -1 || jsonEnd == -1 || jsonEnd < jsonStart)
            {
                _logService.Log("❌ 输出中未找到有效的 JSON");
                _logService.Log($"输出内容: {output.Substring(0, Math.Min(500, output.Length))}");
                if (!string.IsNullOrWhiteSpace(error))
                {
                    _logService.Log($"Python 错误输出: {error}");
                }
                return false;
            }
            
            var jsonOutput = output.Substring(jsonStart, jsonEnd - jsonStart + 1);
            
            PythonScanResult? scanResult;
            try
            {
                scanResult = JsonSerializer.Deserialize<PythonScanResult>(jsonOutput);
            }
            catch (JsonException jsonEx)
            {
                _logService.Log("❌ JSON 解析失败");
                _logService.Log($"JSON 错误: {jsonEx.Message}");
                _logService.Log($"JSON 内容前 500 字符: {jsonOutput.Substring(0, Math.Min(500, jsonOutput.Length))}");
                if (!string.IsNullOrWhiteSpace(error))
                {
                    _logService.Log($"Python 错误输出: {error}");
                }
                return false;
            }
            
            if (scanResult == null)
            {
                _logService.Log("❌ 无法解析 Python 脚本输出（结果为 null）");
                _logService.Log($"原始输出: {output.Substring(0, Math.Min(200, output.Length))}...");
                return false;
            }

            // 检查是否有错误
            if (scanResult.Error != null)
            {
                _logService.Log($"❌ Python 脚本返回错误: {scanResult.Error}");
                return false;
            }

            // 填充缓存 - 优先使用 all_node_types（完整节点列表）
            if (scanResult.AllNodeTypes != null && scanResult.AllNodeTypes.Count > 0)
            {
                _logService.Log($"✓ 使用 ComfyUI 原生节点加载机制");
                
                foreach (var nodeType in scanResult.AllNodeTypes)
                {
                    _installedNodesCache!.Add(nodeType);
                }

                // 展示名 -> 真正 class_type 映射（用于 UI 格式工作流）
                if (scanResult.DisplayNameToType != null)
                {
                    foreach (var (displayName, classType) in scanResult.DisplayNameToType)
                    {
                        if (!string.IsNullOrWhiteSpace(displayName) && !string.IsNullOrWhiteSpace(classType))
                        {
                            _displayNameToTypeCache![displayName] = classType;
                        }
                    }
                }
                
                // 如果有按包分组的信息，也填充包映射
                if (scanResult.Nodes != null)
                {
                    foreach (var (packageName, nodeTypes) in scanResult.Nodes)
                    {
                        if (nodeTypes != null && packageName != "_all_nodes")
                        {
                            foreach (var nodeType in nodeTypes)
                            {
                                if (!_nodeToPackageCache!.ContainsKey(nodeType))
                                {
                                    _nodeToPackageCache[nodeType] = packageName;
                                }
                            }
                        }
                    }
                }
            }
            else if (scanResult.Nodes != null)
            {
                // 兼容旧格式：按包分组的节点
                _logService.Log($"已扫描的包: {string.Join(", ", scanResult.Nodes.Keys.Take(10))}" + 
                    (scanResult.Nodes.Count > 10 ? $" ... 共 {scanResult.Nodes.Count} 个" : ""));
                
                foreach (var (packageName, nodeTypes) in scanResult.Nodes)
                {
                    if (nodeTypes != null)
                    {
                        foreach (var nodeType in nodeTypes)
                        {
                            _installedNodesCache!.Add(nodeType);
                            
                            if (!_nodeToPackageCache!.ContainsKey(nodeType))
                            {
                                _nodeToPackageCache[nodeType] = packageName;
                            }
                        }
                    }
                }
            }

            // 记录统计信息
            _logService.Log($"✓ 扫描完成：发现 {scanResult.TotalPackages} 个包，共 {scanResult.TotalNodes} 个节点");
            
            // 记录错误（如果有）- 显示具体哪些包失败
            if (scanResult.Errors != null && scanResult.Errors.Count > 0)
            {
                _logService.Log($"⚠ 扫描过程中有 {scanResult.Errors.Count} 个包加载失败:");
                foreach (var err in scanResult.Errors.Take(5))
                {
                    _logService.Log($"  - {err.Package}: {err.Error}");
                }
                if (scanResult.Errors.Count > 5)
                {
                    _logService.Log($"  ... 还有 {scanResult.Errors.Count - 5} 个失败");
                }
            }

            return true;
        }
        catch (JsonException jsonEx)
        {
            _logService.Log($"❌ Python 脚本扫描 JSON 解析异常: {jsonEx.Message}");
            return false;
        }
        catch (Exception ex)
        {
            _logService.Log($"❌ Python 脚本扫描异常: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// 获取 ComfyUI 的 Python 解释器路径
    /// </summary>
    private string? FindPythonExecutable()
    {
        // 使用已解析的 Python 路径（从 PythonPathService）
        if (_pythonPathService.IsValid && !string.IsNullOrEmpty(_pythonPathService.PythonPath))
        {
            _logService.Log($"✓ 使用已配置的 Python: {_pythonPathService.PythonPath}");
            return _pythonPathService.PythonPath;
        }

        // 未找到有效的 Python 环境
        _logService.Log("❌ 未找到有效的 Python 环境");
        _logService.Log("提示：请确保 ComfyUI 已正确配置，Python 路径应在应用启动时自动检测");
        
        return null;
    }

    /// <summary>
    /// 提取模型引用
    /// </summary>
    private async Task<List<RequiredModel>> ExtractModelsAsync(List<WorkflowNode> nodes, bool isApiFormat)
    {
        var models = new List<RequiredModel>();
        var seenModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in nodes)
        {
            if (string.IsNullOrEmpty(node.ClassType)) continue;
            if (!ModelLoaderMapping.TryGetValue(node.ClassType, out var loaderInfo)) continue;

            string? modelName = null;

            if (isApiFormat && node.Inputs.HasValue)
            {
                // API 格式：从 inputs 中获取
                if (node.Inputs.Value.TryGetProperty(loaderInfo.ParamName, out var paramValue))
                {
                    modelName = paramValue.ValueKind == JsonValueKind.String
                        ? paramValue.GetString()
                        : null;
                }
            }
            else if (!isApiFormat && node.WidgetsValues.HasValue)
            {
                // UI 格式：从 widgets_values 中获取（通常是第一个值）
                var widgetsArray = node.WidgetsValues.Value;
                if (widgetsArray.ValueKind == JsonValueKind.Array && widgetsArray.GetArrayLength() > 0)
                {
                    var firstValue = widgetsArray[0];
                    if (firstValue.ValueKind == JsonValueKind.String)
                    {
                        modelName = firstValue.GetString();
                    }
                }
            }

            if (string.IsNullOrEmpty(modelName)) continue;
            if (seenModels.Contains(modelName)) continue;
            seenModels.Add(modelName);

            var model = new RequiredModel
            {
                ModelName = modelName,
                ModelPath = $"{loaderInfo.ModelDir}/{modelName}",
                ModelType = loaderInfo.Type,
                LoaderNodeType = node.ClassType
            };

            // 查找模型文件
            await Task.Run(() => FindModelFile(model, loaderInfo.ModelDir));

            models.Add(model);
        }

        return models.OrderBy(m => m.ModelType).ThenBy(m => m.ModelName).ToList();
    }

    /// <summary>
    /// 查找模型文件（支持多个可能的目录 + 全局递归搜索）
    /// </summary>
    private void FindModelFile(RequiredModel model, string modelDir)
    {
        if (!_comfyPathService.IsValid || string.IsNullOrEmpty(_comfyPathService.ComfyUiPath))
        {
            model.Exists = false;
            return;
        }

        var comfyModelsPath = Path.Combine(_comfyPathService.ComfyUiPath, "models");
        
        // 第一步：在预期的目录列表中搜索（主目录 + 备用目录）
        var searchDirs = new List<string> { modelDir };
        
        // 添加备用目录（从映射表中获取）
        foreach (var mapping in ModelLoaderMapping.Values)
        {
            if (mapping.ModelDir == modelDir && mapping.AlternateDirs.Length > 0)
            {
                searchDirs.AddRange(mapping.AlternateDirs);
                break;
            }
        }

        // 在预期目录中搜索
        foreach (var dir in searchDirs)
        {
            var fullPath = Path.Combine(comfyModelsPath, dir, model.ModelName);
            
            if (File.Exists(fullPath))
            {
                // 找到文件，更新模型信息
                SetModelFound(model, fullPath, dir);
                return;
            }
        }

        // 第二步：如果在预期目录中找不到，则在所有 models 子目录中递归搜索
        try
        {
            if (Directory.Exists(comfyModelsPath))
            {
                var foundPath = SearchModelRecursively(comfyModelsPath, model.ModelName);
                if (foundPath != null)
                {
                    // 计算相对于 models 目录的相对路径
                    var relativePath = Path.GetRelativePath(comfyModelsPath, foundPath);
                    var relativeDir = Path.GetDirectoryName(relativePath)?.Replace(Path.DirectorySeparatorChar, '/') ?? "";
                    
                    SetModelFound(model, foundPath, relativeDir);
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            _logService.Log($"递归搜索模型失败: {model.ModelName}, {ex.Message}");
        }

        // 文件未找到，设置默认完整路径（使用主目录）
        model.Exists = false;
        model.FullPath = Path.Combine(comfyModelsPath, modelDir, model.ModelName);
    }

    /// <summary>
    /// 递归搜索模型文件
    /// </summary>
    private string? SearchModelRecursively(string directory, string modelFileName)
    {
        try
        {
            // 先搜索当前目录
            var filePath = Path.Combine(directory, modelFileName);
            if (File.Exists(filePath))
            {
                return filePath;
            }

            // 递归搜索子目录
            foreach (var subDir in Directory.GetDirectories(directory))
            {
                var result = SearchModelRecursively(subDir, modelFileName);
                if (result != null)
                {
                    return result;
                }
            }
        }
        catch
        {
            // 忽略访问被拒绝等错误
        }

        return null;
    }

    /// <summary>
    /// 设置模型找到时的信息
    /// </summary>
    private void SetModelFound(RequiredModel model, string fullPath, string relativeDir)
    {
        model.Exists = true;
        model.FullPath = fullPath;
        model.ModelPath = string.IsNullOrEmpty(relativeDir) 
            ? model.ModelName 
            : $"{relativeDir}/{model.ModelName}";
        
        try
        {
            var fileInfo = new FileInfo(fullPath);
            model.SizeBytes = fileInfo.Length;
        }
        catch
        {
            // 忽略文件访问错误
        }
    }

    /// <summary>
    /// 内部工作流节点表示
    /// </summary>
    private class WorkflowNode
    {
        public string Id { get; set; } = "";
        public string ClassType { get; set; } = "";
        public string? Title { get; set; }
        public JsonElement? Inputs { get; set; }
        public JsonElement? WidgetsValues { get; set; }
    }

    /// <summary>
    /// Python 脚本扫描结果
    /// </summary>
    private class PythonScanResult
    {
        [JsonPropertyName("nodes")]
        public Dictionary<string, List<string>>? Nodes { get; set; }
        
        [JsonPropertyName("total_packages")]
        public int TotalPackages { get; set; }
        
        [JsonPropertyName("total_nodes")]
        public int TotalNodes { get; set; }
        
        [JsonPropertyName("all_node_types")]
        public List<string>? AllNodeTypes { get; set; }

        [JsonPropertyName("display_name_to_type")]
        public Dictionary<string, string>? DisplayNameToType { get; set; }
        
        [JsonPropertyName("errors")]
        public List<PythonScanError>? Errors { get; set; }
        
        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }

    /// <summary>
    /// Python 扫描错误信息
    /// </summary>
    private class PythonScanError
    {
        [JsonPropertyName("package")]
        public string Package { get; set; } = "";
        
        [JsonPropertyName("error")]
        public string Error { get; set; } = "";
        
        [JsonPropertyName("traceback")]
        public string? Traceback { get; set; }
    }
}
