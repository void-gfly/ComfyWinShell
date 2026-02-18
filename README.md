# ComfyShell 🚀

**ComfyShell** 是一个为 [ComfyUI](https://github.com/comfyanonymous/ComfyUI) 量身打造的现代化桌面启动管理工具。

基于 **.NET 10.0** 和 **WPF** 构建，采用 MVVM 架构，旨在简化 ComfyUI 的配置、版本管理、运行监控以及日常使用流程。

### 🌟 亮点功能
1.  **一键打包独立运行包**：根据 ComfyUI 工作流自动分析依赖，一键打包生成可独立运行的环境。
2.  **离线资源管理**：无需启动 ComfyUI 即可直接管理模型、插件等资源，高效便捷。

![应用截图](ui.png)


## ✨ 核心功能

*   **⚡ 多重配置管理 (Profile Management)**
    *   创建并保存多套启动配置（例如：低显存模式、高性能模式、CPU 模式）。
    *   一键切换不同的参数组合，无需手动修改批处理文件。

*   **🛠️ 深度参数配置**
    *   提供图形化界面配置 ComfyUI 的各种启动参数。
    *   支持 `VRAM Mode` (High/Low/Normal)、`Attention Mode`、`Precision` 等高级选项。
    *   自动构建并预览启动命令。

*   **📊 进程与硬件监控**
    *   **进程控制**：在应用内直接启动、停止和重启 ComfyUI。
    *   **实时日志**：内置控制台窗口查看 ComfyUI 的实时输出日志。
    *   **硬件仪表盘**：集成硬件监控功能，实时显示 CPU、GPU（显存/利用率）和内存状态，防止爆显存。

*   **📦 版本与环境管理**
    *   支持管理多个 ComfyUI 版本。
    *   集成 Git 功能，支持从 GitHub 克隆和更新 ComfyUI。
    *   自定义 Python 解释器路径。

*   **⏱️ 智能辅助**
    *   自动关机倒计时功能（适合长时间跑图场景）。

*   **🌐 网络加速 (Network Acceleration)**
    *   **代理服务器配置**：支持 HTTP/HTTPS 代理，可选用户名/密码认证。
    *   **GitHub 镜像加速**：内置多个国内 GitHub 镜像源（ghproxy.com、mirror.ghproxy.com 等），一键切换。
    *   **pip 镜像加速**：预置清华、阿里云、中科大、腾讯云等国内 pip 镜像，大幅提升依赖下载速度。
    *   **一键复制镜像 URL**：所有镜像地址都配备快捷复制按钮，方便手动配置。

*   **📦 资源管理 (Resources)**
    *   **模型文件夹统计**：自动扫描并计算各类模型文件夹的磁盘占用（checkpoints、loras、VAE 等），按大小排序。
    *   **自定义节点管理**：查看已安装的自定义节点列表及其状态。
    *   **工作流管理**：快速访问保存的工作流文件。

## 🏗️ 技术栈

本项目使用最新的微软技术栈构建，确保高性能与可维护性：

*   **框架**: .NET 10.0 (Windows Desktop)
*   **UI 框架**: WPF (Windows Presentation Foundation)
*   **架构模式**: MVVM (Model-View-ViewModel)
*   **核心库**:
    *   `CommunityToolkit.Mvvm`: 高效的 MVVM 工具包。
    *   `Microsoft.Extensions.DependencyInjection`: 依赖注入。
    *   `Microsoft.Extensions.Hosting`: 应用程序生命周期管理。
    *   `LibreHardwareMonitorLib`: 硬件传感器读取。

## 🚀 快速开始

### 环境要求

*   Windows 10 / 11 (x64)
*   [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) 或更高版本
*   [ComfyUI](https://github.com/comfyanonymous/ComfyUI) (可通过本软件直接下载)
*   Python 3.10+ (推荐)

### 构建与运行

1.  **克隆仓库**
    ```bash
    git clone https://github.com/YourUsername/ComfyShell.git
    cd ComfyShell/WpfDesktop
    ```

2.  **构建项目**
    ```powershell
    dotnet build
    ```

3.  **运行**
    ```powershell
    dotnet run
    ```
    或者直接打开 `WpfDesktop.sln` 使用 Visual Studio 2022+ 进行调试。

## 📂 目录结构

```text
E:\dot_net\ComfyShell\WpfDesktop\
├── Models\             # 数据模型与枚举 (ComfyConfiguration, Profile...)
├── ViewModels\         # 视图模型 (MVVM 逻辑核心)
├── Views\              # XAML UI 界面
├── Services\           # 业务逻辑服务 (Process, Git, Hardware...)
│   └── Interfaces\     # 服务接口定义
├── Resources\          # 图标与样式资源
├── appsettings.json    # 全局应用配置
└── App.xaml.cs         # 依赖注入容器配置与入口
```

## ⚙️ 配置说明

### 网络加速配置

应用的网络加速功能通过 `appsettings.json` 进行配置，支持以下选项：

#### 1. 代理服务器配置 (Proxy)
```json
{
  "AppSettings": {
    "Proxy": {
      "Enabled": false,
      "Server": "http://127.0.0.1:7890",
      "Username": "",
      "Password": ""
    }
  }
}
```
- **作用范围**：ComfyUI 启动、Git 操作、pip 安装
- **支持协议**：HTTP、HTTPS
- **环境变量**：自动配置 `HTTP_PROXY`、`HTTPS_PROXY`、`NO_PROXY` 等

#### 2. GitHub 镜像配置 (GitHubMirror)
```json
{
  "AppSettings": {
    "GitHubMirror": {
      "Enabled": true,
      "MirrorUrl": "https://ghproxy.com/https://github.com"
    }
  }
}
```
- **作用范围**：Git 克隆、自定义节点下载
- **内置镜像**：
  - `https://ghproxy.com/https://github.com`
  - `https://mirror.ghproxy.com/https://github.com`
  - `https://gh.api.99988866.xyz/https://github.com`

#### 3. pip 镜像配置 (PipMirror)
```json
{
  "AppSettings": {
    "PipMirror": {
      "Enabled": true,
      "IndexUrl": "https://pypi.tuna.tsinghua.edu.cn/simple",
      "TrustedHost": true
    }
  }
}
```
- **作用范围**：Python 依赖安装
- **内置镜像**：
  - 清华大学：`https://pypi.tuna.tsinghua.edu.cn/simple`
  - 阿里云：`https://mirrors.aliyun.com/pypi/simple`
  - 中科大：`https://pypi.mirrors.ustc.edu.cn/simple`
  - 腾讯云：`https://mirrors.cloud.tencent.com/pypi/simple`
- **环境变量**：自动配置 `PIP_INDEX_URL`、`PIP_TRUSTED_HOST`

更多详细说明请参阅 [NETWORK_ACCELERATION.md](NETWORK_ACCELERATION.md)。

## 🤝 贡献指南

欢迎提交 Issue 和 Pull Request！

1.  Fork 本仓库
2.  创建你的特性分支 (`git checkout -b feature/AmazingFeature`)
3.  提交你的修改 (`git commit -m 'Add some AmazingFeature'`)
4.  推送到分支 (`git push origin feature/AmazingFeature`)
5.  开启一个 Pull Request

## 📄 许可证

[MIT License](LICENSE) (建议添加 LICENSE 文件)

---
*由 [Gemini CLI Agent](https://ai.google.dev/) 辅助生成*
