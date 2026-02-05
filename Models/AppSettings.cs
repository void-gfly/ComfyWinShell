namespace WpfDesktop.Models;

public class AppSettings
{
    public string Language { get; set; } = "zh-CN";
    public string Theme { get; set; } = "Dark";
    public string PythonRoot { get; set; } = "";
    public int MaxLogLines { get; set; } = 5000;

    /// <summary>
    /// 数据存储根目录
    /// </summary>
    public string DataRoot { get; set; } = "data";

    /// <summary>
    /// 应用程序名称
    /// </summary>
    public string AppName { get; set; } = "ComfyShell";

    /// <summary>
    /// 代理配置
    /// </summary>
    public ProxySettings Proxy { get; set; } = new();

    /// <summary>
    /// GitHub 镜像配置
    /// </summary>
    public GitHubMirrorSettings GitHubMirror { get; set; } = new();

    /// <summary>
    /// pip 镜像配置
    /// </summary>
    public PipMirrorSettings PipMirror { get; set; } = new();
}

/// <summary>
/// 代理服务器配置
/// </summary>
public class ProxySettings
{
    /// <summary>
    /// 是否启用代理
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// 代理服务器地址 (例如: http://127.0.0.1:7890)
    /// </summary>
    public string Server { get; set; } = "";

    /// <summary>
    /// 代理用户名 (可选)
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// 代理密码 (可选)
    /// </summary>
    public string? Password { get; set; }
}

/// <summary>
/// GitHub 镜像站点配置
/// </summary>
public class GitHubMirrorSettings
{
    /// <summary>
    /// 是否启用 GitHub 镜像
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// 镜像站点地址 (例如: https://ghproxy.com/https://github.com)
    /// </summary>
    public string MirrorUrl { get; set; } = "https://ghproxy.com/https://github.com";
}

/// <summary>
/// pip 镜像源配置
/// </summary>
public class PipMirrorSettings
{
    /// <summary>
    /// 是否启用 pip 镜像
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// 镜像源地址 (例如: https://pypi.tuna.tsinghua.edu.cn/simple)
    /// </summary>
    public string IndexUrl { get; set; } = "https://pypi.tuna.tsinghua.edu.cn/simple";

    /// <summary>
    /// 是否信任该镜像源 (避免 SSL 验证问题)
    /// </summary>
    public bool TrustedHost { get; set; } = true;
}
