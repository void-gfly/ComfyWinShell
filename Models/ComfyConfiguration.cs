using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using WpfDesktop.Models.Enums;

namespace WpfDesktop.Models;

public partial class ComfyConfiguration : ObservableObject
{
    [ObservableProperty]
    private NetworkConfiguration _network = new();

    [ObservableProperty]
    private PathConfiguration _paths = new();

    [ObservableProperty]
    private DeviceConfiguration _device = new();

    [ObservableProperty]
    private MemoryConfiguration _memory = new();

    [ObservableProperty]
    private PrecisionConfiguration _precision = new();

    [ObservableProperty]
    private AttentionConfiguration _attention = new();

    [ObservableProperty]
    private PreviewConfiguration _preview = new();

    [ObservableProperty]
    private CacheConfiguration _cache = new();

    [ObservableProperty]
    private ManagerConfiguration _manager = new();

    [ObservableProperty]
    private LaunchConfiguration _launch = new();

    [ObservableProperty]
    private MiscellaneousConfiguration _miscellaneous = new();
}

public partial class NetworkConfiguration : ObservableObject
{
    [ObservableProperty]
    private string _listen = "127.0.0.1";

    [ObservableProperty]
    private int _port = 8188;

    [ObservableProperty]
    private string? _corsOrigin;

    [ObservableProperty]
    private string? _tlsKeyFile;

    [ObservableProperty]
    private string? _tlsCertFile;

    [ObservableProperty]
    private double _maxUploadSizeMb = 100;
}

public partial class PathConfiguration : ObservableObject
{
    [ObservableProperty]
    private string? _baseDirectory;

    [ObservableProperty]
    private ObservableCollection<string> _extraModelPathsConfig = new();

    [ObservableProperty]
    private string? _outputDirectory;

    [ObservableProperty]
    private string? _tempDirectory;

    [ObservableProperty]
    private string? _inputDirectory;

    [ObservableProperty]
    private string? _userDirectory;
}

public partial class DeviceConfiguration : ObservableObject
{
    [ObservableProperty]
    private int? _cudaDevice;

    [ObservableProperty]
    private int? _defaultDevice;

    [ObservableProperty]
    private int? _directMlDevice;

    [ObservableProperty]
    private string? _oneApiDeviceSelector;

    [ObservableProperty]
    private bool _disableIpexOptimize;

    [ObservableProperty]
    private bool _cpuVae;
}

public partial class MemoryConfiguration : ObservableObject
{
    [ObservableProperty]
    private VramMode _vramMode = VramMode.Auto;

    [ObservableProperty]
    private double? _reserveVramGb;

    [ObservableProperty]
    private bool _asyncOffload;

    [ObservableProperty]
    private int? _asyncOffloadStreams;

    [ObservableProperty]
    private bool _smartMemory = true;
}

public partial class PrecisionConfiguration : ObservableObject
{
    [ObservableProperty]
    private ForcePrecisionMode _forcePrecision = ForcePrecisionMode.Default;

    [ObservableProperty]
    private UnetPrecisionMode _unetPrecision = UnetPrecisionMode.Default;

    [ObservableProperty]
    private VaePrecisionMode _vaePrecision = VaePrecisionMode.Default;

    [ObservableProperty]
    private TextEncoderPrecisionMode _textEncoderPrecision = TextEncoderPrecisionMode.Default;
}

public partial class AttentionConfiguration : ObservableObject
{
    [ObservableProperty]
    private AttentionMode _mode = AttentionMode.Default;

    [ObservableProperty]
    private UpcastMode _upcastMode = UpcastMode.Default;

    [ObservableProperty]
    private bool _useXFormers = true;
}

public partial class PreviewConfiguration : ObservableObject
{
    [ObservableProperty]
    private PreviewMethod _method = PreviewMethod.None;

    [ObservableProperty]
    private int _previewSize = 512;
}

public partial class CacheConfiguration : ObservableObject
{
    [ObservableProperty]
    private CacheMode _mode = CacheMode.Default;

    [ObservableProperty]
    private int _lruCount;

    [ObservableProperty]
    private double? _ramThresholdGb;
}

public partial class ManagerConfiguration : ObservableObject
{
    [ObservableProperty]
    private bool _enableManager;

    [ObservableProperty]
    private bool _disableManagerUi;

    [ObservableProperty]
    private bool _enableLegacyUi;
}

public partial class LaunchConfiguration : ObservableObject
{
    [ObservableProperty]
    private bool _autoLaunch;

    [ObservableProperty]
    private bool _dontPrintServer;

    [ObservableProperty]
    private string? _frontEndVersion;

    [ObservableProperty]
    private string? _frontEndRoot;
}

public partial class MiscellaneousConfiguration : ObservableObject
{
    [ObservableProperty]
    private bool _forceChannelsLast;

    [ObservableProperty]
    private bool _supportsFp8Compute;

    [ObservableProperty]
    private bool _forceNonBlocking;

    [ObservableProperty]
    private string _defaultHashingFunction = "sha256";

    [ObservableProperty]
    private bool _deterministic;

    [ObservableProperty]
    private ObservableCollection<string> _fastOptions = new();

    [ObservableProperty]
    private bool _disableMmap;

    [ObservableProperty]
    private bool _mmapTorchFiles;

    [ObservableProperty]
    private bool _disableMetadata;

    [ObservableProperty]
    private bool _disableAllCustomNodes;

    [ObservableProperty]
    private ObservableCollection<string> _whitelistCustomNodes = new();

    [ObservableProperty]
    private bool _disableApiNodes;

    [ObservableProperty]
    private bool _multiUser;

    [ObservableProperty]
    private LogLevel _verbose = LogLevel.Information;

    [ObservableProperty]
    private bool _logStdout;

    [ObservableProperty]
    private bool _enableCompressResponseBody;

    [ObservableProperty]
    private string? _comfyApiBase;

    [ObservableProperty]
    private string? _databaseUrl;

    [ObservableProperty]
    private bool _disableAssetsAutoscan;
}
