# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 项目概述

ComfyShell 是一个 WPF 桌面应用程序，用于管理和启动 ComfyUI 实例。提供版本管理、配置文件管理、进程监控、硬件监控等功能。

## 构建和运行

```bash
# 构建（需指定项目文件，因为目录下有多个项目）
dotnet build WpfDesktop.csproj

# 运行
dotnet run --project WpfDesktop.csproj

# 发布
dotnet publish WpfDesktop.csproj -c Release
```

## 技术栈

- .NET 10.0 + WPF
- CommunityToolkit.Mvvm (MVVM 框架，使用 `[ObservableProperty]` 源生成器)
- Microsoft.Extensions.Hosting (依赖注入、配置、日志)
- LibreHardwareMonitorLib (硬件监控)

## 架构

### 依赖注入

所有服务和 ViewModel 在 `App.xaml.cs` 中注册为单例，通过构造函数注入。添加新服务或 ViewModel 时需要在此处注册。

### 导航系统

`MainViewModel` 持有所有子 ViewModel 的字典，通过 `NavigateCommand` 切换视图。实现 `INavigationAware` 接口的 ViewModel 在导航到时会调用 `OnNavigatedToAsync()`。

### 进程管理

`ProcessService` 管理 ComfyUI 进程生命周期：
- 通过 `StartAsync` 启动进程，重定向 stdout/stderr
- 使用 HTTP 心跳检测 (`/system_stats`) 感知服务状态
- 支持检测 ComfyUI 内部重启（通过 Manager 重启时进程对象会失效，但心跳检测仍能感知服务状态）
- 通过 Windows API (`GenerateConsoleCtrlEvent`) 发送 Ctrl+C 实现优雅停止

### ComfyUI 路径检测

`ComfyPathService` 自动检测 ComfyUI 安装位置，支持两种目录结构：
- 便携版: `../ComfyUI_windows_portable/ComfyUI/`
- 克隆版: `../ComfyUI/`

### 配置系统

- `ComfyConfiguration`: 包含所有 ComfyUI 启动参数的嵌套配置类
- `ArgumentBuilder`: 将配置对象转换为命令行参数字符串
- 配置存储在 `%LOCALAPPDATA%\ComfyShell\profiles\` 下的 JSON 文件

## 关键模式

### ViewModel 属性

使用 CommunityToolkit.Mvvm 的源生成器：

```csharp
[ObservableProperty]
private string _myProperty;  // 自动生成 MyProperty 属性和 OnMyPropertyChanged 分部方法
```

### UI 线程调度

跨线程更新 UI 时使用 `Application.Current.Dispatcher.Invoke()`，`MainViewModel` 中有 `RunOnUiThread` 辅助方法。

### 对话框服务

`IDialogService` 提供文件夹选择、文件选择、消息框等功能，避免在 ViewModel 中直接使用 WPF 对话框。
