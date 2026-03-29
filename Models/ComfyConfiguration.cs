using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using WpfDesktop.Models.Enums;

namespace WpfDesktop.Models;

/// <summary>
/// 表示 ComfyUI 的完整启动配置集合。
/// </summary>
public partial class ComfyConfiguration : ObservableObject
{
    /// <summary>
    /// 网络监听、端口和上传限制等配置。
    /// </summary>
    [ObservableProperty]
    private NetworkConfiguration _network = new();

    /// <summary>
    /// 基础目录、输入输出目录和扩展模型路径等配置。
    /// </summary>
    [ObservableProperty]
    private PathConfiguration _paths = new();

    /// <summary>
    /// CUDA、DirectML、OneAPI 等设备选择配置。
    /// </summary>
    [ObservableProperty]
    private DeviceConfiguration _device = new();

    /// <summary>
    /// 显存占用模式、保留显存与异步卸载等配置。
    /// </summary>
    [ObservableProperty]
    private MemoryConfiguration _memory = new();

    /// <summary>
    /// 全局精度、UNet、VAE 与文本编码器精度配置。
    /// </summary>
    [ObservableProperty]
    private PrecisionConfiguration _precision = new();

    /// <summary>
    /// 注意力算法和上转换行为等配置。
    /// </summary>
    [ObservableProperty]
    private AttentionConfiguration _attention = new();

    /// <summary>
    /// 预览图生成方式与预览尺寸配置。
    /// </summary>
    [ObservableProperty]
    private PreviewConfiguration _preview = new();

    /// <summary>
    /// 缓存模式、LRU 数量和内存阈值配置。
    /// </summary>
    [ObservableProperty]
    private CacheConfiguration _cache = new();

    /// <summary>
    /// ComfyUI Manager 及其界面行为配置。
    /// </summary>
    [ObservableProperty]
    private ManagerConfiguration _manager = new();

    /// <summary>
    /// 自动启动和前端资源版本配置。
    /// </summary>
    [ObservableProperty]
    private LaunchConfiguration _launch = new();

    /// <summary>
    /// 其它杂项功能、日志与 API 相关配置。
    /// </summary>
    [ObservableProperty]
    private MiscellaneousConfiguration _miscellaneous = new();
}

/// <summary>
/// 表示网络监听与上传限制相关配置。
/// </summary>
public partial class NetworkConfiguration : ObservableObject
{
    /// <summary>
    /// Web 服务监听地址，例如 127.0.0.1 或 0.0.0.0。
    /// </summary>
    [ObservableProperty]
    private string _listen = "127.0.0.1";

    /// <summary>
    /// Web 服务监听端口。
    /// </summary>
    [ObservableProperty]
    private int _port = 8188;

    /// <summary>
    /// 允许跨域访问的源地址列表或单个源。
    /// </summary>
    [ObservableProperty]
    private string? _corsOrigin;

    /// <summary>
    /// TLS 私钥文件路径，用于启用 HTTPS。
    /// </summary>
    [ObservableProperty]
    private string? _tlsKeyFile;

    /// <summary>
    /// TLS 证书文件路径，用于启用 HTTPS。
    /// </summary>
    [ObservableProperty]
    private string? _tlsCertFile;

    /// <summary>
    /// 允许上传的最大文件大小，单位为 MB。
    /// </summary>
    [ObservableProperty]
    private double _maxUploadSizeMb = 100;
}

/// <summary>
/// 表示 ComfyUI 路径与目录相关配置。
/// </summary>
public partial class PathConfiguration : ObservableObject
{
    /// <summary>
    /// ComfyUI 启动时使用的基础工作目录。
    /// </summary>
    [ObservableProperty]
    private string? _baseDirectory;

    /// <summary>
    /// 额外模型路径配置文件列表，用于传递给 ComfyUI。
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<string> _extraModelPathsConfig = new();

    /// <summary>
    /// 扩展模型基础目录（用于自动生成 extra_model_paths.yaml）
    /// </summary>
    [ObservableProperty]
    private string? _extraModelBaseDirectory;

    /// <summary>
    /// 生成结果输出目录。
    /// </summary>
    [ObservableProperty]
    private string? _outputDirectory;

    /// <summary>
    /// 临时文件目录。
    /// </summary>
    [ObservableProperty]
    private string? _tempDirectory;

    /// <summary>
    /// 输入资源目录，例如待处理图片或工作流输入文件目录。
    /// </summary>
    [ObservableProperty]
    private string? _inputDirectory;

    /// <summary>
    /// 用户数据目录，用于存放用户相关配置与缓存。
    /// </summary>
    [ObservableProperty]
    private string? _userDirectory;
}

/// <summary>
/// 表示设备选择与硬件加速相关配置。
/// </summary>
public partial class DeviceConfiguration : ObservableObject
{
    /// <summary>
    /// 指定要使用的 CUDA 设备编号。
    /// </summary>
    [ObservableProperty]
    private int? _cudaDevice;

    /// <summary>
    /// 默认计算设备编号或索引。
    /// </summary>
    [ObservableProperty]
    private int? _defaultDevice;

    /// <summary>
    /// DirectML 后端使用的设备编号。
    /// </summary>
    [ObservableProperty]
    private int? _directMlDevice;

    /// <summary>
    /// OneAPI 设备选择表达式。
    /// </summary>
    [ObservableProperty]
    private string? _oneApiDeviceSelector;

    /// <summary>
    /// 是否禁用 IPEX 优化。
    /// </summary>
    [ObservableProperty]
    private bool _disableIpexOptimize;

    /// <summary>
    /// 是否强制 VAE 在 CPU 上运行。
    /// </summary>
    [ObservableProperty]
    private bool _cpuVae;
}

/// <summary>
/// 表示显存占用与卸载策略相关配置。
/// </summary>
public partial class MemoryConfiguration : ObservableObject
{
    /// <summary>
    /// 显存使用模式，例如自动、低显存或纯 CPU。
    /// </summary>
    [ObservableProperty]
    private VramMode _vramMode = VramMode.Auto;

    /// <summary>
    /// 预留不供 ComfyUI 使用的显存大小，单位为 GB。
    /// </summary>
    [ObservableProperty]
    private double? _reserveVramGb;

    /// <summary>
    /// 是否启用异步卸载以降低显存压力。
    /// </summary>
    [ObservableProperty]
    private bool _asyncOffload;

    /// <summary>
    /// 异步卸载使用的流数量。
    /// </summary>
    [ObservableProperty]
    private int? _asyncOffloadStreams;

    /// <summary>
    /// 是否启用智能内存管理策略。
    /// </summary>
    [ObservableProperty]
    private bool _smartMemory = true;
}

/// <summary>
/// 表示 UNet、VAE 和文本编码器的精度相关配置。
/// </summary>
public partial class PrecisionConfiguration : ObservableObject
{
    /// <summary>
    /// 全局强制精度模式。
    /// </summary>
    [ObservableProperty]
    private ForcePrecisionMode _forcePrecision = ForcePrecisionMode.Default;

    /// <summary>
    /// UNet 推理精度模式。
    /// </summary>
    [ObservableProperty]
    private UnetPrecisionMode _unetPrecision = UnetPrecisionMode.Default;

    /// <summary>
    /// VAE 推理精度模式。
    /// </summary>
    [ObservableProperty]
    private VaePrecisionMode _vaePrecision = VaePrecisionMode.Default;

    /// <summary>
    /// 文本编码器推理精度模式。
    /// </summary>
    [ObservableProperty]
    private TextEncoderPrecisionMode _textEncoderPrecision = TextEncoderPrecisionMode.Default;
}

/// <summary>
/// 表示注意力实现方式与上采样策略配置。
/// </summary>
public partial class AttentionConfiguration : ObservableObject
{
    /// <summary>
    /// 注意力实现模式。
    /// </summary>
    [ObservableProperty]
    private AttentionMode _mode = AttentionMode.Default;

    /// <summary>
    /// 注意力计算中的上转换策略。
    /// </summary>
    [ObservableProperty]
    private UpcastMode _upcastMode = UpcastMode.Default;

    /// <summary>
    /// 是否启用 xFormers 加速。
    /// </summary>
    [ObservableProperty]
    private bool _useXFormers = true;
}

/// <summary>
/// 表示预览图生成方式与尺寸配置。
/// </summary>
public partial class PreviewConfiguration : ObservableObject
{
    /// <summary>
    /// 预览图生成方法。
    /// </summary>
    [ObservableProperty]
    private PreviewMethod _method = PreviewMethod.None;

    /// <summary>
    /// 预览图的目标尺寸。
    /// </summary>
    [ObservableProperty]
    private int _previewSize = 512;
}

/// <summary>
/// 表示缓存模式与阈值相关配置。
/// </summary>
public partial class CacheConfiguration : ObservableObject
{
    /// <summary>
    /// 缓存工作模式。
    /// </summary>
    [ObservableProperty]
    private CacheMode _mode = CacheMode.Default;

    /// <summary>
    /// LRU 缓存模式下允许保留的对象数量。
    /// </summary>
    [ObservableProperty]
    private int _lruCount;

    /// <summary>
    /// RAM 缓存模式下的内存阈值，单位为 GB。
    /// </summary>
    [ObservableProperty]
    private double? _ramThresholdGb;
}

/// <summary>
/// 表示 ComfyUI Manager 相关功能配置。
/// </summary>
public partial class ManagerConfiguration : ObservableObject
{
    /// <summary>
    /// 是否启用 ComfyUI Manager 功能。
    /// </summary>
    [ObservableProperty]
    private bool _enableManager;

    /// <summary>
    /// 是否禁用 Manager 的界面入口。
    /// </summary>
    [ObservableProperty]
    private bool _disableManagerUi;

    /// <summary>
    /// 是否启用旧版界面。
    /// </summary>
    [ObservableProperty]
    private bool _enableLegacyUi;
}

/// <summary>
/// 表示自动启动和前端资源相关配置。
/// </summary>
public partial class LaunchConfiguration : ObservableObject
{
    /// <summary>
    /// 是否在应用准备完成后自动启动 ComfyUI。
    /// </summary>
    [ObservableProperty]
    private bool _autoLaunch;

    /// <summary>
    /// 是否禁止在控制台输出服务地址信息。
    /// </summary>
    [ObservableProperty]
    private bool _dontPrintServer;

    /// <summary>
    /// 指定要使用的前端版本标识。
    /// </summary>
    [ObservableProperty]
    private string? _frontEndVersion;

    /// <summary>
    /// 自定义前端资源根目录。
    /// </summary>
    [ObservableProperty]
    private string? _frontEndRoot;
}

/// <summary>
/// 表示日志、API、多用户与其它杂项配置。
/// </summary>
public partial class MiscellaneousConfiguration : ObservableObject
{
    /// <summary>
    /// 是否强制使用 channels_last 内存布局。
    /// </summary>
    [ObservableProperty]
    private bool _forceChannelsLast;

    /// <summary>
    /// 是否声明支持 FP8 计算。
    /// </summary>
    [ObservableProperty]
    private bool _supportsFp8Compute;

    /// <summary>
    /// 是否强制使用非阻塞数据传输。
    /// </summary>
    [ObservableProperty]
    private bool _forceNonBlocking;

    /// <summary>
    /// 默认使用的哈希算法名称。
    /// </summary>
    [ObservableProperty]
    private string _defaultHashingFunction = "sha256";

    /// <summary>
    /// 是否启用确定性计算模式，以便结果更可复现。
    /// </summary>
    [ObservableProperty]
    private bool _deterministic;

    /// <summary>
    /// 传递给 ComfyUI 的快速选项列表。
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<string> _fastOptions = new();

    /// <summary>
    /// 是否禁用内存映射文件机制。
    /// </summary>
    [ObservableProperty]
    private bool _disableMmap;

    /// <summary>
    /// 是否仅对 Torch 文件启用内存映射。
    /// </summary>
    [ObservableProperty]
    private bool _mmapTorchFiles;

    /// <summary>
    /// 是否禁用元数据写入或读取。
    /// </summary>
    [ObservableProperty]
    private bool _disableMetadata;

    /// <summary>
    /// 是否禁用全部自定义节点加载。
    /// </summary>
    [ObservableProperty]
    private bool _disableAllCustomNodes;

    /// <summary>
    /// 允许加载的自定义节点白名单。
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<string> _whitelistCustomNodes = new();

    /// <summary>
    /// 是否禁用 API 节点。
    /// </summary>
    [ObservableProperty]
    private bool _disableApiNodes;

    /// <summary>
    /// 是否启用多用户模式。
    /// </summary>
    [ObservableProperty]
    private bool _multiUser;

    /// <summary>
    /// 日志详细级别。
    /// </summary>
    [ObservableProperty]
    private LogLevel _verbose = LogLevel.Information;

    /// <summary>
    /// 是否将标准输出内容记录到日志。
    /// </summary>
    [ObservableProperty]
    private bool _logStdout;

    /// <summary>
    /// 是否启用响应体压缩。
    /// </summary>
    [ObservableProperty]
    private bool _enableCompressResponseBody;

    /// <summary>
    /// 自定义 Comfy API 的基础地址。
    /// </summary>
    [ObservableProperty]
    private string? _comfyApiBase;

    /// <summary>
    /// 外部数据库连接地址。
    /// </summary>
    [ObservableProperty]
    private string? _databaseUrl;

    /// <summary>
    /// 是否禁用资源文件自动扫描。
    /// </summary>
    [ObservableProperty]
    private bool _disableAssetsAutoscan;
}
