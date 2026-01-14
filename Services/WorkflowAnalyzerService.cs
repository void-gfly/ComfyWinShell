using System.IO;
using System.Text.Json;
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
    private readonly ILogService _logService;

    // 已安装节点缓存
    private HashSet<string>? _installedNodesCache;
    private Dictionary<string, string>? _nodeToPackageCache;

    /// <summary>
    /// 模型加载器配置映射
    /// key: class_type, value: (参数名, 模型目录, 模型类型)
    /// </summary>
    private static readonly Dictionary<string, (string ParamName, string ModelDir, ModelType Type)> ModelLoaderMapping = new()
    {
        ["CheckpointLoaderSimple"] = ("ckpt_name", "checkpoints", ModelType.Checkpoint),
        ["CheckpointLoader"] = ("ckpt_name", "checkpoints", ModelType.Checkpoint),
        ["LoraLoader"] = ("lora_name", "loras", ModelType.Lora),
        ["LoraLoaderModelOnly"] = ("lora_name", "loras", ModelType.Lora),
        ["VAELoader"] = ("vae_name", "vae", ModelType.Vae),
        ["ControlNetLoader"] = ("control_net_name", "controlnet", ModelType.ControlNet),
        ["DiffControlNetLoader"] = ("control_net_name", "controlnet", ModelType.ControlNet),
        ["CLIPLoader"] = ("clip_name", "clip", ModelType.Clip),
        ["DualCLIPLoader"] = ("clip_name1", "clip", ModelType.Clip),
        ["CLIPVisionLoader"] = ("clip_name", "clip_vision", ModelType.ClipVision),
        ["UNETLoader"] = ("unet_name", "unet", ModelType.Unet),
        ["StyleModelLoader"] = ("style_model_name", "style_models", ModelType.StyleModel),
        ["UpscaleModelLoader"] = ("model_name", "upscale_models", ModelType.UpscaleModel),
        ["HypernetworkLoader"] = ("hypernetwork_name", "hypernetworks", ModelType.Hypernetwork),
        ["GLIGENLoader"] = ("gligen_name", "gligen", ModelType.Gligen),
    };

    /// <summary>
    /// ComfyUI 内置节点列表（非自定义节点）
    /// </summary>
    private static readonly HashSet<string> BuiltInNodes = new()
    {
        // Loaders
        "CheckpointLoaderSimple", "CheckpointLoader", "LoraLoader", "LoraLoaderModelOnly",
        "VAELoader", "ControlNetLoader", "DiffControlNetLoader", "CLIPLoader", "DualCLIPLoader",
        "CLIPVisionLoader", "UNETLoader", "StyleModelLoader", "UpscaleModelLoader",
        "HypernetworkLoader", "GLIGENLoader", "unCLIPCheckpointLoader",
        
        // Sampling
        "KSampler", "KSamplerAdvanced", "SamplerCustom", "SamplerCustomAdvanced",
        
        // Conditioning
        "CLIPTextEncode", "CLIPTextEncodeSDXL", "CLIPSetLastLayer", "ConditioningCombine",
        "ConditioningAverage", "ConditioningConcat", "ConditioningSetArea",
        "ConditioningSetAreaPercentage", "ConditioningSetMask", "ConditioningZeroOut",
        "ControlNetApply", "ControlNetApplyAdvanced", "GLIGENTextBoxApply",
        "unCLIPConditioning", "CLIPVisionEncode", "StyleModelApply",
        
        // Latent
        "EmptyLatentImage", "LatentUpscale", "LatentUpscaleBy", "LatentFromBatch",
        "RepeatLatentBatch", "LatentBlend", "LatentComposite", "LatentCompositeMasked",
        "LatentCrop", "LatentFlip", "LatentRotate", "LatentBatch", "LatentBatchSeedBehavior",
        
        // Image
        "LoadImage", "LoadImageMask", "SaveImage", "PreviewImage", "ImageScale",
        "ImageScaleBy", "ImageUpscaleWithModel", "ImageInvert", "ImageBatch",
        "ImagePadForOutpaint", "EmptyImage", "ImageCrop", "ImageCompositeMasked",
        "ImageBlend", "ImageBlur", "ImageQuantize", "ImageSharpen",
        
        // Mask
        "ImageToMask", "MaskToImage", "SolidMask", "InvertMask", "CropMask",
        "MaskComposite", "FeatherMask", "GrowMask", "ThresholdMask",
        
        // VAE
        "VAEDecode", "VAEEncode", "VAEEncodeForInpaint", "VAEDecodeTiled", "VAEEncodeTiled",
        
        // Model
        "ModelSamplingDiscrete", "ModelSamplingContinuousEDM", "ModelSamplingContinuousV",
        "RescaleCFG", "PatchModelAddDownscale", "FreeU", "FreeU_V2",
        
        // Utilities
        "Note", "PrimitiveNode", "Reroute",
    };

    public WorkflowAnalyzerService(IComfyPathService comfyPathService, ILogService logService)
    {
        _comfyPathService = comfyPathService;
        _logService = logService;
    }

    public bool IsWorkflowFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return false;
        return filePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
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
            result.RequiredNodes = ExtractCustomNodes(nodes);

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
    /// 提取自定义节点
    /// </summary>
    private List<RequiredCustomNode> ExtractCustomNodes(List<WorkflowNode> nodes)
    {
        // 懒加载：首次使用时扫描已安装节点
        if (_installedNodesCache == null || _nodeToPackageCache == null)
        {
            ScanInstalledNodes();
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

        return nodeTypeCount
            .Select(kvp => new RequiredCustomNode
            {
                NodeType = kvp.Key,
                DisplayName = kvp.Key,
                UsageCount = kvp.Value,
                IsBuiltIn = BuiltInNodes.Contains(kvp.Key),
                Exists = DetermineNodeExists(kvp.Key),
                PackageName = GetPackageName(kvp.Key)
            })
            .OrderByDescending(n => n.UsageCount)
            .ThenBy(n => n.NodeType)
            .ToList();
    }

    /// <summary>
    /// 判断节点是否存在
    /// </summary>
    private bool DetermineNodeExists(string nodeType)
    {
        // 内置节点始终存在
        if (BuiltInNodes.Contains(nodeType)) return true;
        
        // 检查缓存中是否有该节点
        return _installedNodesCache?.Contains(nodeType) ?? false;
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
    /// 扫描已安装的自定义节点
    /// </summary>
    private void ScanInstalledNodes()
    {
        _installedNodesCache = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _nodeToPackageCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

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
            foreach (var packageDir in Directory.GetDirectories(customNodesPath))
            {
                var packageName = Path.GetFileName(packageDir);
                
                // 跳过示例目录和隐藏目录
                if (packageName.StartsWith("_") || packageName.StartsWith("."))
                    continue;

                // 搜索所有 Python 文件
                var pyFiles = Directory.GetFiles(packageDir, "*.py", SearchOption.AllDirectories);
                foreach (var pyFile in pyFiles)
                {
                    ExtractNodeTypesFromPythonFile(pyFile, packageName);
                }
            }
        }
        catch (Exception ex)
        {
            _logService.LogError($"扫描自定义节点失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 从 Python 文件中提取节点类型
    /// </summary>
    private void ExtractNodeTypesFromPythonFile(string filePath, string packageName)
    {
        try
        {
            var content = File.ReadAllText(filePath);
            
            // 匹配 NODE_CLASS_MAPPINGS = { "NodeType": NodeClass, ... }
            var mappingsMatch = NodeClassMappingsRegex().Match(content);
            if (!mappingsMatch.Success) return;

            var mappingsContent = mappingsMatch.Groups[1].Value;
            
            // 提取字典中的键（节点类型名）
            var nodeTypeMatches = NodeTypeKeyRegex().Matches(mappingsContent);
            foreach (Match match in nodeTypeMatches)
            {
                var nodeType = match.Groups[1].Value;
                _installedNodesCache!.Add(nodeType);
                
                // 记录节点到包的映射
                if (!_nodeToPackageCache!.ContainsKey(nodeType))
                {
                    _nodeToPackageCache[nodeType] = packageName;
                }
            }
        }
        catch (Exception ex)
        {
            _logService.LogError($"解析 Python 文件失败: {filePath}, {ex.Message}", ex);
        }
    }

    // 正则表达式：匹配 NODE_CLASS_MAPPINGS = { ... }
    [GeneratedRegex(@"NODE_CLASS_MAPPINGS\s*=\s*\{([^}]+)\}", RegexOptions.Singleline)]
    private static partial Regex NodeClassMappingsRegex();

    // 正则表达式:匹配字典中的字符串键
    [GeneratedRegex(@"[""']([^""']+)[""']\s*:")]
    private static partial Regex NodeTypeKeyRegex();

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
    /// 查找模型文件
    /// </summary>
    private void FindModelFile(RequiredModel model, string modelDir)
    {
        if (!_comfyPathService.IsValid || string.IsNullOrEmpty(_comfyPathService.ComfyUiPath))
        {
            model.Exists = false;
            return;
        }

        var modelsPath = Path.Combine(_comfyPathService.ComfyUiPath, "models", modelDir);
        var fullPath = Path.Combine(modelsPath, model.ModelName);

        // 无论文件是否存在，都设置完整路径
        model.FullPath = fullPath;

        if (File.Exists(fullPath))
        {
            model.Exists = true;
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
        else
        {
            model.Exists = false;
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
}
