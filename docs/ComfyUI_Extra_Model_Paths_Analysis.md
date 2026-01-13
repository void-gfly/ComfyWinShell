# ComfyUI Extra Model Paths 工作机制分析与集成方案

## 📋 目录
1. [ComfyUI Extra Model Paths 概述](#1-comfyui-extra-model-paths-概述)
2. [工作机制详解](#2-工作机制详解)
3. [配置文件结构](#3-配置文件结构)
4. [ComfyShell 现有实现](#4-comfyshell-现有实现)
5. [融合到模型管理页面的方案](#5-融合到模型管理页面的方案)
6. [实施计划](#6-实施计划)

---

## 1. ComfyUI Extra Model Paths 概述

### 1.1 功能用途
`extra_model_paths.yaml` 是 ComfyUI 提供的一种**外部模型路径管理机制**，允许用户在不移动模型文件的情况下，让 ComfyUI 识别多个模型存储位置。

**主要应用场景**：
- 🔄 **跨程序共享**：多个 AI 工具（WebUI、ComfyUI、Fooocus）共用同一套模型文件，避免重复占用磁盘空间
- 💾 **分布式存储**：模型文件分散在不同磁盘分区或外部存储设备
- 🏢 **团队协作**：多个 ComfyUI 实例共享网络存储上的模型库
- ⚙️ **环境隔离**：开发/测试/生产环境使用不同的模型路径配置

### 1.2 基本原理
ComfyUI 启动时会：
1. 加载内置的 `models/` 目录（默认模型路径）
2. 读取 `extra_model_paths.yaml` 配置文件
3. 解析配置中定义的外部路径
4. 将所有路径合并到模型搜索索引中

**关键特性**：
- ✅ 支持相对路径和绝对路径
- ✅ 支持多个配置文件（通过 `--extra-model-paths-config` 参数可传递多个文件）
- ✅ 支持路径变量（如 `base_path`）
- ✅ 兼容 Stable Diffusion WebUI 的目录结构

---

## 2. 工作机制详解

### 2.1 配置文件位置

**Portable/Manual 安装版本**：
```
ComfyUI/extra_model_paths.yaml
```

**ComfyUI Desktop 版本**：
- **Windows**: `C:\Users\YourUsername\AppData\Roaming\ComfyUI\extra_models_config.yaml`
- **macOS**: `~/Library/Application Support/ComfyUI/extra_models_config.yaml`

### 2.2 启动参数
通过命令行参数指定额外的配置文件：
```bash
python main.py --extra-model-paths-config /path/to/config1.yaml --extra-model-paths-config /path/to/config2.yaml
```

**注意**：
- 可以多次使用该参数加载多个配置文件
- 后加载的配置会覆盖前面的同名路径定义
- 配置中的路径会与内置 `models/` 目录路径合并（不是替换）

### 2.3 路径解析规则

ComfyUI 使用以下优先级解析模型路径：

1. **`base_path` 变量**：作为所有相对路径的根目录
2. **绝对路径**：直接使用
3. **相对路径**：相对于 `base_path` 解析
4. **多行路径**：使用 `|` 符号可定义多个搜索路径（YAML 多行字符串）
5. **`is_default` 标记**：标记某个配置为默认配置，优先显示，并用作下载的默认目录

示例：
```yaml
my_config:
    base_path: D:\AI_Models\     # 基础路径
    is_default: true              # 标记为默认配置（可选）
    checkpoints: models\checkpoints  # 相对路径 → D:\AI_Models\models\checkpoints
    loras: |                     # 多个路径
         models\Lora
         models\LyCORIS
```

### 2.4 核心实现：folder_paths.py

ComfyUI 的路径管理核心在 `folder_paths.py` 文件中，该文件定义了：

**支持的文件扩展**：
```python
supported_pt_extensions = {'.ckpt', '.pt', '.bin', '.pth', '.safetensors'}
```

**核心 API 函数**：
```python
def get_full_path(folder_name, filename):
    """获取文件的完整路径"""
    # 在所有注册的路径中搜索文件
    ...

def get_folder_paths(folder_name):
    """获取指定类型的所有文件夹路径"""
    return folder_names_and_paths.get(folder_name, ([], set()))

def add_model_folder_path(folder_name, full_folder_path, is_default=False):
    """动态添加模型文件夹路径"""
    if folder_name not in folder_names_and_paths:
        folder_names_and_paths[folder_name] = ([], set())
    folder_names_and_paths[folder_name][0].append(full_folder_path)
```

**路径扩展处理**：
- 支持 `~` 用户路径扩展
- 支持 AppData 目录扩展
- 支持跨平台路径规范化

---

## 3. 配置文件结构

### 3.1 基本结构

```yaml
# 配置名称（可自定义，仅用于识别）
my_custom_config:
    base_path: YOUR_PATH         # 必需：基础路径
    checkpoints: models/checkpoints/
    clip: models/clip/
    clip_vision: models/clip_vision/
    configs: models/configs/
    controlnet: models/controlnet/
    diffusion_models: models/diffusion_models/
    embeddings: models/embeddings/
    loras: models/loras/
    upscale_models: models/upscale_models/
    vae: models/vae/
    # 更多模型类型...
```

### 3.2 完整支持的模型类型

根据官方示例文件，ComfyUI 支持以下模型目录：

| 模型类型 | 目录键名 | 常见内容 |
|---------|---------|---------|
| Checkpoints | `checkpoints` | Stable Diffusion 主模型（.safetensors/.ckpt） |
| LoRA | `loras` | LoRA 微调模型 |
| VAE | `vae` | VAE 编码器/解码器 |
| ControlNet | `controlnet` | ControlNet 控制模型 |
| CLIP | `clip` | CLIP 文本编码器 |
| CLIP Vision | `clip_vision` | CLIP 视觉编码器 |
| Upscale | `upscale_models` | 超分辨率模型（ESRGAN/RealESRGAN/SwinIR） |
| Embeddings | `embeddings` | 文本嵌入（Textual Inversion） |
| Hypernetworks | `hypernetworks` | Hypernetwork 模型 |
| Diffusion Models | `diffusion_models` 或 `unet` | Diffusion/UNet 模型 |
| Configs | `configs` | 模型配置文件 |

### 3.3 示例配置：兼容 WebUI

```yaml
a111:
    base_path: D:\stable-diffusion-webui\
    checkpoints: models/Stable-diffusion
    configs: models/Stable-diffusion
    vae: models/VAE
    loras: |
         models/Lora
         models/LyCORIS
    upscale_models: |
                  models/ESRGAN
                  models/RealESRGAN
                  models/SwinIR
    embeddings: embeddings
    hypernetworks: models/hypernetworks
    controlnet: models/ControlNet
```

### 3.4 示例配置：共享模型库

```yaml
shared_models:
    base_path: E:\AI_Shared_Models\
    is_default: true  # 标记为默认，优先列出
    checkpoints: checkpoints
    loras: loras
    vae: vae
    controlnet: controlnet
```

### 3.5 示例配置：团队共享（来自 Magnopus 实践）

适用于多个团队成员共享同一套模型的场景：

```yaml
comfyui:
    base_path: X:\comfyui_models
    checkpoints: models\checkpoints\
    controlnet: models\controlnet\
    custom_nodes: custom_nodes\
    loras: models\loras\
```

**启动命令**：
```bash
python main.py --extra-model-paths-config X:\comfyui_models\extra_model_paths.yaml
```

**优势**：
- 💾 节省磁盘空间（避免每个开发者重复存储）
- 🚀 加速团队成员入职
- 🔄 统一资源配置
- 🔗 支持网络存储

---

## 4. ComfyUI API 端点（用于前端集成）

### 4.1 模型相关 API

ComfyUI 提供以下 RESTful API 端点，可用于前端查询模型状态：

| 端点 | 方法 | 用途 | 返回示例 |
|-----|------|------|---------|
| `/models` | GET | 获取所有可用的模型类型列表 | `["checkpoints", "loras", "vae", ...]` |
| `/models/{folder}` | GET | 获取特定文件夹中的模型文件 | `["model1.safetensors", "model2.ckpt"]` |
| `/object_info` | GET | 获取所有节点类型的详细信息 | `{...}` |
| `/embeddings` | GET | 获取可用的 embeddings 名称列表 | `["embedding1", ...]` |
| `/system_stats` | GET | 获取系统信息（Python、设备、VRAM） | `{...}` |

**使用示例**：
```bash
# 获取所有模型类型
curl http://localhost:8188/models

# 获取 checkpoints 文件夹中的模型
curl http://localhost:8188/models/checkpoints

# 获取节点信息
curl http://localhost:8188/object_info
```

### 4.2 WebSocket 实时通信

**端点**：`ws://localhost:8188/ws`

**用途**：
- 📊 执行进度更新
- 🔄 节点执行状态
- ⚠️ 错误消息和调试信息
- 📋 队列状态实时更新

**集成建议**：
```csharp
// 在 ProcessService.cs 中可以添加 WebSocket 客户端
private async Task ConnectWebSocketAsync()
{
    var ws = new ClientWebSocket();
    await ws.ConnectAsync(new Uri("ws://localhost:8188/ws"), CancellationToken.None);
    
    // 监听模型加载事件...
}
```

### 4.3 前端集成最佳实践

1. **启动时**：
   - 调用 `/models` 获取所有模型类型
   - 调用 `/models/{folder}` 获取每个类型的模型列表
   - 显示在 UI 中供用户选择

2. **配置变更后**：
   - 保存 `extra_model_paths.yaml`
   - 重启 ComfyUI 进程
   - 重新调用 API 刷新模型列表

3. **实时监控**：
   - 通过 WebSocket 监听模型加载/卸载事件
   - 动态更新 UI 模型列表

---

## 5. ComfyShell 现有实现

### 4.1 配置模型
**文件**：`Models/ComfyConfiguration.cs`

```csharp
public partial class PathConfiguration : ObservableObject
{
    [ObservableProperty]
    private string? _baseDirectory;

    [ObservableProperty]
    private ObservableCollection<string> _extraModelPathsConfig = new();
    
    // ... 其他路径配置
}
```

**特点**：
- ✅ 使用 `ObservableCollection<string>` 存储多个配置文件路径
- ✅ 支持动态添加/删除配置文件路径

### 4.2 UI 界面
**文件**：`Views/ConfigurationView.xaml` (Line 221-225)

```xml
<StackPanel ToolTip="加载额外的模型路径配置文件 (extra_model_paths.yaml)。每行一个路径。(--extra-model-paths-config)">
    <TextBlock Text="额外模型路径 (Extra Models)" Style="{StaticResource FieldLabel}"/>
    <TextBox Text="{Binding ExtraModelPathsText, UpdateSourceTrigger=PropertyChanged}"
             AcceptsReturn="True" Height="80" TextWrapping="Wrap" VerticalScrollBarVisibility="Auto"/>
</StackPanel>
```

**特点**：
- ✅ 提供多行文本输入框
- ✅ 每行一个配置文件路径
- ✅ 实时同步到 `ExtraModelPathsConfig` 集合

### 4.3 ViewModel 逻辑
**文件**：`ViewModels/ConfigurationViewModel.cs` (Line 71, 278-280, 300-303)

```csharp
[ObservableProperty]
private string _extraModelPathsText = string.Empty;

partial void OnExtraModelPathsTextChanged(string value)
{
    UpdateCollectionFromText(Configuration.Paths.ExtraModelPathsConfig, value);
}

private void SyncTextFromCollections()
{
    ExtraModelPathsText = string.Join(Environment.NewLine, Configuration.Paths.ExtraModelPathsConfig);
    // ...
}
```

**特点**：
- ✅ 双向绑定：文本 ↔ 集合
- ✅ 支持逗号和换行符分隔
- ✅ 自动过滤空白项

### 4.4 参数构建
**文件**：`Services/ArgumentBuilder.cs` (Line 76-85)

```csharp
private static void AddPathArguments(List<string> args, PathConfiguration paths)
{
    if (paths.ExtraModelPathsConfig.Count > 0)
    {
        foreach (var path in paths.ExtraModelPathsConfig)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                args.Add($"--extra-model-paths-config {Quote(path)}");
            }
        }
    }
    // ...
}
```

**特点**：
- ✅ 自动为每个配置文件生成 `--extra-model-paths-config` 参数
- ✅ 自动处理包含空格的路径（加引号）

### 5.5 现有问题分析

| 问题 | 影响 | 严重程度 |
|-----|------|---------|
| **仅支持配置文件路径** | 用户需要手动编辑 YAML 文件，无法在应用内直接配置模型目录 | ⚠️ 中 |
| **无可视化编辑器** | 用户对 YAML 语法不熟悉容易出错 | ⚠️ 中 |
| **无配置验证** | 错误的路径或 YAML 格式不会在启动前被发现 | ⚠️ 中 |
| **无路径浏览器** | 需要手动复制粘贴文件路径 | ⚠️ 低 |
| **与资源管理页面脱节** | 资源管理页面（Resources）扫描的是 `BaseDirectory/models`，无法感知 extra paths | 🔴 高 |
| **无 API 集成** | 未调用 ComfyUI 的 `/models` API 验证配置是否生效 | ⚠️ 中 |

### 5.6 常见陷阱与注意事项

根据社区反馈，需要特别注意：

1. **配置文件名称差异**：
   - Portable 版本：`extra_model_paths.yaml`
   - Desktop 版本：`extra_models_config.yaml`（位于 AppData）
   - 两者不要混淆！

2. **`is_default: true` 限制**：
   - 虽然可以标记默认路径，但 ComfyUI-Manager 下载时仍可能使用内置默认路径
   - 需要额外配置 Manager 的下载路径

3. **跨平台路径问题**：
   - Windows：使用 `\` 或 `/`
   - Linux/macOS：仅使用 `/`
   - 建议统一使用正斜杠 `/`

4. **必须重启才能生效**：
   - 修改 YAML 后不会热重载
   - 必须完全重启 ComfyUI 进程

---

## 6. 融合到模型管理页面的方案

### 6.1 目标功能

基于现有的 **Resources 视图**（`ResourcesViewModel.cs` + `ResourcesView.xaml`）进行增强：

#### 核心功能
1. **可视化 YAML 编辑器**
   - 在 Resources 页面新增 "Extra Model Paths" 标签页
   - 提供图形化界面编辑 `extra_model_paths.yaml`
   - 支持添加/编辑/删除配置组（如 `my_config`, `webui_compat`）

2. **路径浏览与验证**
   - 每个路径输入框配备 "浏览" 按钮
   - 实时验证路径有效性（显示 ✅ 或 ❌）
   - 显示路径下的模型文件统计

3. **统一模型视图**
   - 资源管理页面同时显示内置 `models/` 和 extra paths 中的模型
   - 标注模型来源（Base / Extra: my_config）
   - 提供筛选功能（仅显示基础路径 / 仅显示额外路径）

4. **一键配置向导**
   - 预设模板：WebUI 兼容、共享模型库、多盘存储
   - 快速添加常见外部路径

### 6.2 UI 设计方案

#### 方案 A：在 Resources 页面新增标签页（推荐）

```
Resources View
├── Tab: 自定义节点 (Custom Nodes)
├── Tab: 模型文件夹 (Models) ← 现有功能
├── Tab: 工作流 (Workflows)
└── Tab: 额外模型路径 (Extra Paths) ← 新增
```

**优势**：
- ✅ 符合现有 UI 结构
- ✅ 与模型管理功能逻辑关联性强
- ✅ 不影响配置页面的简洁性

**Extra Paths 标签页结构**：
```
┌─────────────────────────────────────────────┐
│ 额外模型路径配置                             │
├─────────────────────────────────────────────┤
│  [添加配置组] [从模板创建] [导入 YAML]        │
├─────────────────────────────────────────────┤
│  ▼ my_shared_models (配置组名)               │
│    基础路径: E:\AI_Models  [浏览] [✅]        │
│    ┌──────────────────────────────────┐     │
│    │ ☑ Checkpoints  models/checkpoints │     │
│    │ ☑ LoRA         models/loras       │     │
│    │ ☑ VAE          models/vae         │     │
│    │ ☑ ControlNet   models/controlnet  │     │
│    │ ☐ Embeddings   (未启用)           │     │
│    └──────────────────────────────────┘     │
│    [编辑] [删除] [预览路径]                  │
│                                              │
│  ▼ webui_compat                              │
│    基础路径: D:\stable-diffusion-webui [浏览]│
│    [展开内容...]                             │
└─────────────────────────────────────────────┘
│ [保存到 YAML] [应用并重启 ComfyUI]           │
└─────────────────────────────────────────────┘
```

#### 方案 B：在 Configuration 页面增强（备选）

在现有 "路径设置 (Paths)" Expander 中：
- 将当前的文本输入框替换为 "高级配置" 按钮
- 点击后弹出模态对话框，内含完整的 YAML 编辑器

**优势**：
- ✅ 配置功能集中
- ✅ 适合高级用户

**劣势**：
- ❌ 与资源查看功能分离
- ❌ 需要额外对话框窗口

### 6.3 数据模型扩展

#### 新增模型类：ExtraModelPathConfig

```csharp
namespace WpfDesktop.Models;

/// <summary>
/// 表示一个 extra_model_paths.yaml 中的配置组
/// </summary>
public partial class ExtraModelPathConfig : ObservableObject
{
    [ObservableProperty]
    private string _name = "my_config";  // 配置组名称

    [ObservableProperty]
    private string _basePath = string.Empty;  // base_path

    [ObservableProperty]
    private bool _isDefault;  // 是否标记为默认配置

    // 各类模型路径（可为空表示不配置该类型）
    [ObservableProperty]
    private string? _checkpoints;

    [ObservableProperty]
    private string? _loras;

    [ObservableProperty]
    private string? _vae;

    [ObservableProperty]
    private string? _controlnet;

    [ObservableProperty]
    private string? _clip;

    [ObservableProperty]
    private string? _clipVision;

    [ObservableProperty]
    private string? _configs;

    [ObservableProperty]
    private string? _diffusionModels;

    [ObservableProperty]
    private string? _embeddings;

    [ObservableProperty]
    private string? _hypernetworks;

    [ObservableProperty]
    private string? _upscaleModels;

    [ObservableProperty]
    private string? _customNodes;  // 支持外部自定义节点路径

    /// <summary>
    /// 获取完整路径（base_path + 相对路径）
    /// </summary>
    public string GetFullPath(string? relativePath)
    {
        if (string.IsNullOrEmpty(relativePath)) return string.Empty;
        if (Path.IsPathRooted(relativePath)) return relativePath;
        return Path.Combine(BasePath, relativePath);
    }

    /// <summary>
    /// 验证配置的有效性
    /// </summary>
    public bool Validate(out List<string> errors)
    {
        errors = new List<string>();
        
        if (string.IsNullOrWhiteSpace(Name))
            errors.Add("配置名称不能为空");
        
        if (string.IsNullOrWhiteSpace(BasePath))
            errors.Add("基础路径不能为空");
        else if (!Directory.Exists(BasePath))
            errors.Add($"基础路径不存在: {BasePath}");
        
        return errors.Count == 0;
    }
}
```

#### 扩展 PathConfiguration

```csharp
public partial class PathConfiguration : ObservableObject
{
    // 现有字段...
    
    /// <summary>
    /// Extra Model Paths 配置组集合
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<ExtraModelPathConfig> _extraModelPathConfigs = new();
}
```

### 6.4 服务层扩展

#### 新增：IExtraModelPathService

```csharp
namespace WpfDesktop.Services.Interfaces;

public interface IExtraModelPathService
{
    /// <summary>
    /// 从 YAML 文件加载配置
    /// </summary>
    Task<List<ExtraModelPathConfig>> LoadFromYamlAsync(string filePath);

    /// <summary>
    /// 保存配置到 YAML 文件
    /// </summary>
    Task SaveToYamlAsync(string filePath, List<ExtraModelPathConfig> configs);

    /// <summary>
    /// 验证配置的有效性
    /// </summary>
    ValidationResult ValidateConfig(ExtraModelPathConfig config);

    /// <summary>
    /// 获取指定配置下所有模型路径的文件统计
    /// </summary>
    Task<Dictionary<string, ModelFolderInfo>> GetModelStatsAsync(ExtraModelPathConfig config);

    /// <summary>
    /// 生成 WebUI 兼容配置模板
    /// </summary>
    ExtraModelPathConfig CreateWebUiTemplate(string webUiPath);

    /// <summary>
    /// 生成共享模型库配置模板
    /// </summary>
    ExtraModelPathConfig CreateSharedTemplate(string sharedPath);
}
```

#### 实现要点

**YAML 解析**：使用 `YamlDotNet` NuGet 包
```csharp
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

public async Task<List<ExtraModelPathConfig>> LoadFromYamlAsync(string filePath)
{
    var yaml = await File.ReadAllTextAsync(filePath);
    var deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .Build();
    
    var rawConfig = deserializer.Deserialize<Dictionary<string, Dictionary<string, object>>>(yaml);
    
    return rawConfig.Select(kvp => ParseConfigGroup(kvp.Key, kvp.Value)).ToList();
}
```

**路径合并逻辑**：扩展 `ResourceService.cs`
```csharp
public async Task<List<ModelFolderInfo>> GetAllModelFoldersAsync()
{
    var folders = new List<ModelFolderInfo>();
    
    // 1. 扫描基础路径
    folders.AddRange(await ScanBaseModelFoldersAsync());
    
    // 2. 扫描 extra paths
    var extraConfigs = await _configurationService.GetExtraModelPathConfigsAsync();
    foreach (var config in extraConfigs)
    {
        folders.AddRange(await ScanExtraModelFoldersAsync(config));
    }
    
    return folders;
}
```

### 6.5 ViewModel 扩展

#### ExtraModelPathsViewModel（新增）

```csharp
public partial class ExtraModelPathsViewModel : ViewModelBase
{
    private readonly IExtraModelPathService _extraPathService;
    private readonly IConfigurationService _configurationService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private ObservableCollection<ExtraModelPathConfig> _configs = new();

    [ObservableProperty]
    private ExtraModelPathConfig? _selectedConfig;

    [ObservableProperty]
    private bool _isLoading;

    public IAsyncRelayCommand LoadCommand { get; }
    public IAsyncRelayCommand SaveCommand { get; }
    public IRelayCommand AddConfigCommand { get; }
    public IRelayCommand<ExtraModelPathConfig> DeleteConfigCommand { get; }
    public IRelayCommand<string> BrowsePathCommand { get; }
    public IAsyncRelayCommand<string> CreateFromTemplateCommand { get; }
    public IAsyncRelayCommand ImportYamlCommand { get; }
    public IAsyncRelayCommand ExportYamlCommand { get; }

    // ... 实现逻辑
}
```

#### ResourcesViewModel 改造

```csharp
public partial class ResourcesViewModel : ViewModelBase
{
    // 现有字段...
    
    [ObservableProperty]
    private ExtraModelPathsViewModel _extraPathsViewModel;

    // 新增：显示所有来源的模型
    [ObservableProperty]
    private bool _showOnlyBasePath = false;

    partial void OnShowOnlyBasePathChanged(bool value)
    {
        RefreshModelFolders();
    }

    private async Task RefreshModelFolders()
    {
        IsCalculating = true;
        try
        {
            // 使用扩展后的 ResourceService
            var allFolders = await _resourceService.GetAllModelFoldersAsync();
            
            if (ShowOnlyBasePath)
            {
                allFolders = allFolders.Where(f => f.Source == "Base").ToList();
            }
            
            ModelFolders = new ObservableCollection<ModelFolderInfo>(allFolders);
        }
        finally
        {
            IsCalculating = false;
        }
    }
}
```

---

## 7. 实施计划

### 7.1 阶段 1：基础设施（1-2 天）

**目标**：搭建数据模型和服务层

#### 任务清单
- [ ] 创建 `Models/ExtraModelPathConfig.cs`
- [ ] 扩展 `Models/PathConfiguration.cs`
- [ ] 创建 `Services/Interfaces/IExtraModelPathService.cs`
- [ ] 实现 `Services/ExtraModelPathService.cs`
  - [ ] YAML 解析与生成
  - [ ] 路径验证逻辑
  - [ ] 模板生成功能
- [ ] 添加 NuGet 依赖：`YamlDotNet`
- [ ] 在 `App.xaml.cs` 中注册服务

#### 验收标准
```csharp
// 单元测试示例
[Test]
public async Task LoadFromYaml_ShouldParseCorrectly()
{
    var service = new ExtraModelPathService();
    var configs = await service.LoadFromYamlAsync("test_config.yaml");
    
    Assert.AreEqual(2, configs.Count);
    Assert.AreEqual("my_config", configs[0].Name);
    Assert.AreEqual(@"E:\Models", configs[0].BasePath);
}
```

### 7.2 阶段 2：ViewModel 层（1 天）

**目标**：实现业务逻辑

#### 任务清单
- [ ] 创建 `ViewModels/ExtraModelPathsViewModel.cs`
- [ ] 实现所有 Command 逻辑
  - [ ] LoadCommand：从配置文件加载
  - [ ] SaveCommand：保存到 YAML
  - [ ] AddConfigCommand：添加新配置组
  - [ ] DeleteConfigCommand：删除配置组
  - [ ] BrowsePathCommand：文件夹浏览器
  - [ ] CreateFromTemplateCommand：从模板创建
- [ ] 在 `ResourcesViewModel.cs` 中集成 `ExtraModelPathsViewModel`

### 7.3 阶段 3：UI 实现（2 天）

**目标**：完成界面开发

#### 任务清单
- [ ] 在 `Views/ResourcesView.xaml` 中新增 "额外模型路径" 标签页
- [ ] 设计配置组列表（使用 `ItemsControl` + `Expander`）
- [ ] 实现路径输入框 + 浏览按钮
- [ ] 添加模型类型复选框列表
- [ ] 实现实时路径验证（✅/❌ 图标）
- [ ] 添加工具栏按钮（添加/导入/导出）
- [ ] 设计模板选择对话框

#### UI 样式要求
- 沿用现有的金色主题 (`#D4AF37`)
- 卡片式布局（`CardBorder` Style）
- Expander 展开动画

### 7.4 阶段 4：资源管理整合（1 天）

**目标**：让模型列表显示所有来源的模型

#### 任务清单
- [ ] 扩展 `Models/ModelFolderInfo.cs`，添加 `Source` 属性
  ```csharp
  [ObservableProperty]
  private string _source = "Base";  // "Base" | "Extra: my_config"
  ```
- [ ] 修改 `ResourceService.GetAllModelFoldersAsync()` 合并扫描逻辑
- [ ] 在模型列表 UI 中显示来源标签
- [ ] 添加筛选器（显示全部/仅基础/仅额外）

### 7.5 阶段 5：API 集成（1 天）

**目标**：调用 ComfyUI API 验证配置和刷新模型

#### 任务清单
- [ ] 在 `ProcessService.cs` 中添加模型 API 调用方法
  ```csharp
  public async Task<List<string>> GetAvailableModelTypesAsync();
  public async Task<List<string>> GetModelsInFolderAsync(string folderName);
  ```
- [ ] 添加配置验证功能：保存 YAML 后调用 API 验证
- [ ] 实现"刷新模型列表"按钮（无需重启）
- [ ] 添加 API 调用错误处理

### 7.6 阶段 6：测试与优化（1 天）

#### 测试用例
1. **功能测试**
   - [ ] 添加配置组，保存并重启 ComfyUI，验证模型是否加载
   - [ ] 编辑配置组，修改路径后刷新，验证模型列表更新
   - [ ] 删除配置组，验证 YAML 文件正确更新
   - [ ] 导入已有 YAML 文件，验证解析正确性

2. **边界测试**
   - [ ] 路径不存在时的错误提示
   - [ ] YAML 格式错误时的处理
   - [ ] 中文路径支持
   - [ ] 超长路径处理

3. **性能测试**
   - [ ] 扫描 10+ 外部路径的加载速度
   - [ ] 大量模型文件（1000+ 个）的统计速度

#### 优化方向
- 异步加载，避免 UI 冻结
- 路径扫描结果缓存
- 增量更新模型列表（仅刷新变更部分）

### 7.7 时间估算
| 阶段 | 预计工时 | 依赖 |
|-----|---------|-----|
| 阶段 1：基础设施 | 16 小时 | - |
| 阶段 2：ViewModel | 8 小时 | 阶段 1 |
| 阶段 3：UI 实现 | 16 小时 | 阶段 2 |
| 阶段 4：资源整合 | 8 小时 | 阶段 1, 2 |
| 阶段 5：API 集成 | 8 小时 | 阶段 1, 2 |
| 阶段 6：测试优化 | 8 小时 | 全部 |
| **总计** | **64 小时** (约 8 个工作日) | |

---

## 8. 附录

### 8.1 参考资料

#### 官方文档
- [ComfyUI 官方文档 - Models](https://docs.comfy.org/development/core-concepts/models)
- [ComfyUI 服务器路由文档](https://docs.comfy.org/development/comfyui-server/comms_routes)
- [ComfyUI GitHub - extra_model_paths.yaml.example](https://github.com/comfyanonymous/ComfyUI/blob/master/extra_model_paths.yaml.example)

#### 技术库
- [YamlDotNet 文档](https://github.com/aaubry/YamlDotNet/wiki)

#### 社区资源
- [GitHub Discussion #2849 - 自定义节点模型路径最佳实践](https://github.com/Comfy-Org/ComfyUI/discussions/2849)
- [Medium - 共享模型和自定义节点 (Magnopus)](https://medium.com/xrlo-extended-reality-lowdown/sharing-models-and-custom-nodes-in-comfyui-0965ef7f1485)
- [GitHub Issue #6039 - folder_paths 问题](https://github.com/comfyanonymous/ComfyUI/issues/6039)
- [GitHub PR #6441 - 跨平台路径规范化](https://github.com/comfyanonymous/ComfyUI/pull/6441)

#### 生产环境示例
- [AWS SageMaker ComfyUI 配置](https://github.com/aws-samples/comfyui-on-amazon-sagemaker)
- [RunPod Serverless Worker 配置](https://github.com/Dekita/runpod-serverless-comfyui-worker)
- [硅流 OneDiff 集成配置](https://github.com/siliconflow/onediff)

### 8.2 相关文件清单

#### 当前实现
```
Models/ComfyConfiguration.cs                  # PathConfiguration 定义
ViewModels/ConfigurationViewModel.cs          # 配置编辑逻辑
Views/ConfigurationView.xaml                  # 配置 UI (Line 221-225)
Services/ArgumentBuilder.cs                   # 参数构建 (Line 76-85)
Services/ResourceService.cs                   # 模型文件夹扫描
ViewModels/ResourcesViewModel.cs              # 资源管理页面逻辑
Views/ResourcesView.xaml                      # 资源管理页面 UI
Resources/model_descriptions.json             # 模型类型描述
```

#### 需要新增
```
Models/ExtraModelPathConfig.cs                # 新增
Services/Interfaces/IExtraModelPathService.cs # 新增
Services/ExtraModelPathService.cs             # 新增
ViewModels/ExtraModelPathsViewModel.cs        # 新增
```

#### 需要修改
```
Models/PathConfiguration.cs                   # 添加 ExtraModelPathConfigs 属性
Models/ModelFolderInfo.cs                     # 添加 Source 属性
Services/ResourceService.cs                   # 扩展 GetAllModelFoldersAsync
ViewModels/ResourcesViewModel.cs              # 集成 ExtraModelPathsViewModel
Views/ResourcesView.xaml                      # 新增标签页
App.xaml.cs                                   # 注册新服务
```

### 8.3 技术依赖

**NuGet 包**：
```xml
<PackageReference Include="YamlDotNet" Version="16.2.0" />
```

**目标框架**：
- .NET 10.0

**兼容性**：
- ComfyUI 版本：所有支持 `--extra-model-paths-config` 的版本（v0.0.1+）

---

## 🎯 下一步行动

1. **评审本文档**：确认方案可行性和优先级
2. **创建开发分支**：`feature/extra-model-paths-ui`
3. **开始阶段 1 开发**：搭建基础设施
4. **持续集成**：每个阶段完成后进行集成测试

---

**文档版本**：v1.0  
**创建日期**：2026-01-14  
**作者**：Sisyphus (AI Agent)  
**审核状态**：待评审
