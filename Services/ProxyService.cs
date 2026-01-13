using System.Diagnostics;
using Microsoft.Extensions.Options;
using WpfDesktop.Models;
using WpfDesktop.Services.Interfaces;

namespace WpfDesktop.Services;

/// <summary>
/// 代理和镜像服务实现
/// </summary>
public class ProxyService : IProxyService
{
    private readonly IOptionsMonitor<AppSettings> _settingsMonitor;

    public ProxyService(IOptionsMonitor<AppSettings> settingsMonitor)
    {
        _settingsMonitor = settingsMonitor;
    }

    public bool IsEnabled => _settingsMonitor.CurrentValue.Proxy.Enabled;

    public bool IsGitHubMirrorEnabled => _settingsMonitor.CurrentValue.GitHubMirror.Enabled;

    public bool IsPipMirrorEnabled => _settingsMonitor.CurrentValue.PipMirror.Enabled;

    public string GetProxyServer()
    {
        var proxy = _settingsMonitor.CurrentValue.Proxy;
        if (!proxy.Enabled || string.IsNullOrWhiteSpace(proxy.Server))
        {
            return string.Empty;
        }

        return proxy.Server;
    }

    public string ConvertGitHubUrl(string originalUrl)
    {
        var mirror = _settingsMonitor.CurrentValue.GitHubMirror;
        if (!mirror.Enabled || string.IsNullOrWhiteSpace(mirror.MirrorUrl) || string.IsNullOrWhiteSpace(originalUrl))
        {
            return originalUrl;
        }

        // 检查是否是 GitHub URL
        if (!originalUrl.Contains("github.com", StringComparison.OrdinalIgnoreCase))
        {
            return originalUrl;
        }

        // 如果镜像 URL 已经包含 github.com，直接替换
        // 例如: https://ghproxy.com/https://github.com/xxx -> 替换 github.com 部分
        if (mirror.MirrorUrl.Contains("github.com", StringComparison.OrdinalIgnoreCase))
        {
            // 提取镜像前缀（去除 github.com 及之后的部分）
            var mirrorPrefix = mirror.MirrorUrl.Split(new[] { "github.com" }, StringSplitOptions.None)[0] + "github.com";
            
            // 确保原始 URL 以 https://github.com 开头
            if (originalUrl.StartsWith("https://github.com", StringComparison.OrdinalIgnoreCase))
            {
                return originalUrl.Replace("https://github.com", mirrorPrefix, StringComparison.OrdinalIgnoreCase);
            }
            else if (originalUrl.StartsWith("http://github.com", StringComparison.OrdinalIgnoreCase))
            {
                return originalUrl.Replace("http://github.com", mirrorPrefix, StringComparison.OrdinalIgnoreCase);
            }
        }

        // 否则，尝试简单拼接
        // 移除镜像 URL 末尾的斜杠
        var trimmedMirror = mirror.MirrorUrl.TrimEnd('/');
        
        // 如果原始 URL 不以 http 开头，添加 https://
        if (!originalUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !originalUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            originalUrl = "https://" + originalUrl;
        }

        // 直接拼接（适用于某些镜像格式）
        return $"{trimmedMirror}/{originalUrl}";
    }

    public string GetPipMirrorArgs()
    {
        var pip = _settingsMonitor.CurrentValue.PipMirror;
        if (!pip.Enabled || string.IsNullOrWhiteSpace(pip.IndexUrl))
        {
            return string.Empty;
        }

        var args = $"-i {pip.IndexUrl}";
        
        // 添加信任主机参数（避免 SSL 证书验证问题）
        if (pip.TrustedHost)
        {
            try
            {
                var uri = new Uri(pip.IndexUrl);
                args += $" --trusted-host {uri.Host}";
            }
            catch
            {
                // 如果 URL 解析失败，忽略 trusted-host 参数
            }
        }

        return args;
    }

    public void ConfigureProcessProxy(ProcessStartInfo startInfo)
    {
        var proxy = _settingsMonitor.CurrentValue.Proxy;
        if (!proxy.Enabled || string.IsNullOrWhiteSpace(proxy.Server))
        {
            return;
        }

        // 配置 HTTP 和 HTTPS 代理环境变量
        // 这些环境变量会被 Python (pip, urllib), Git, Node.js 等工具识别
        startInfo.Environment["HTTP_PROXY"] = proxy.Server;
        startInfo.Environment["HTTPS_PROXY"] = proxy.Server;
        startInfo.Environment["http_proxy"] = proxy.Server;
        startInfo.Environment["https_proxy"] = proxy.Server;

        // 配置认证信息（如果提供）
        if (!string.IsNullOrWhiteSpace(proxy.Username) && !string.IsNullOrWhiteSpace(proxy.Password))
        {
            var credentials = $"{proxy.Username}:{proxy.Password}@";
            var serverWithAuth = proxy.Server.Replace("://", $"://{credentials}");

            startInfo.Environment["HTTP_PROXY"] = serverWithAuth;
            startInfo.Environment["HTTPS_PROXY"] = serverWithAuth;
            startInfo.Environment["http_proxy"] = serverWithAuth;
            startInfo.Environment["https_proxy"] = serverWithAuth;
        }

        // 配置 Git 专用代理（优先级高于环境变量）
        startInfo.Environment["GIT_HTTP_PROXY_AUTHMETHOD"] = "basic";
        
        // 配置 pip 代理（某些版本的 pip 需要这个）
        startInfo.Environment["PIP_PROXY"] = proxy.Server;

        // 配置不使用代理的地址（本地地址）
        startInfo.Environment["NO_PROXY"] = "localhost,127.0.0.1,::1";
        startInfo.Environment["no_proxy"] = "localhost,127.0.0.1,::1";

        // 配置 pip 镜像源（通过环境变量）
        var pip = _settingsMonitor.CurrentValue.PipMirror;
        if (pip.Enabled && !string.IsNullOrWhiteSpace(pip.IndexUrl))
        {
            startInfo.Environment["PIP_INDEX_URL"] = pip.IndexUrl;
            
            if (pip.TrustedHost)
            {
                try
                {
                    var uri = new Uri(pip.IndexUrl);
                    startInfo.Environment["PIP_TRUSTED_HOST"] = uri.Host;
                }
                catch
                {
                    // 忽略解析错误
                }
            }
        }
    }
}
