# AGENTS.md

本文件为仓库内的智能编程代理提供操作规范与约定，适用于 `E:\dot_net\ComfyShell\WpfDesktop`。

## 1. 项目概述
- 项目类型：WPF 桌面应用
- 目标框架：`net10.0-windows`
- 架构模式：MVVM（`CommunityToolkit.Mvvm`）
- 依赖注入：`Microsoft.Extensions.Hosting` + `Microsoft.Extensions.DependencyInjection`

## 2. 目录结构约定
- `Models/`：数据模型与枚举
- `ViewModels/`：视图模型（`ViewModelBase` + `[ObservableProperty]`）
- `Views/`：XAML 视图
- `Services/`：业务逻辑服务与实现
- `Services/Interfaces/`：服务接口（以 `I` 前缀命名）
- `App.xaml.cs`：应用入口、DI 注册、全局异常处理

## 3. 构建 / 运行 / 发布
来自仓库说明（`CLAUDE.md`）：

```bash
# 构建
 dotnet build

# 运行
 dotnet run

# 发布
 dotnet publish -c Release
```

建议显式指定项目文件：

```bash
 dotnet build WpfDesktop.csproj
 dotnet run --project WpfDesktop.csproj
 dotnet publish WpfDesktop.csproj -c Release
```

## 4. 测试
- 简单单点测试代码优先用 .NET 10 file-based apps，单个 `.cs` 文件直接运行，不先建项目。
- 仅限临时验证、一次性实验、小工具；涉及多文件、长期维护、CI 回归或复杂依赖时，改用正式测试项目。
- 测试脚本优先用 `dotnet run file.cs` 或 `dotnet file.cs`，需要包引用时用 `#:package`，不要为此额外搭脚手架。

## 5. Lint / 格式化
- 未发现 `.editorconfig` 或专用格式化配置
- 依赖编译器约束：`<Nullable>enable</Nullable>` + `<ImplicitUsings>enable</ImplicitUsings>`
- 若需要统一风格，建议新增 `.editorconfig`（需在修改前沟通确认）

## 6. 代码风格与命名规范
### 6.1 命名与结构
- 命名空间：文件范围命名空间（file-scoped）
  - 示例：`namespace WpfDesktop.ViewModels;`
- 类名：PascalCase，并使用清晰后缀
  - `*ViewModel`, `*Service`, `*Configuration`, `*Message`
- 接口：`I` 前缀（如 `IProcessService`）
- 私有字段：`_camelCase`（带下划线）
- 公共属性：PascalCase
- 部分类型：使用 `partial class`（MVVM 源生成器要求）

### 6.2 MVVM Toolkit（CommunityToolkit.Mvvm）
- 视图模型继承：`ViewModelBase : ObservableObject`
- 属性生成：使用 `[ObservableProperty]` 修饰私有字段
  - 字段命名可使用 `_camelCase`，生成 PascalCase 属性
- 命令生成：使用 `[RelayCommand]` 修饰私有方法
  - `Task` 返回值自动生成 `IAsyncRelayCommand`
  - `void` 返回值自动生成 `IRelayCommand`
- 命令可执行状态：
  - 手动调用 `NotifyCanExecuteChanged()`
  - 或使用 `[NotifyCanExecuteChangedFor]` 关联属性

### 6.3 异步与线程
- 异步方法返回 `Task`，避免 `async void`（事件/命令除外）
- UI 更新需切回 UI 线程：
  - 参考 `RunOnUiThread` 模式（`MainViewModel`, `ProcessMonitorViewModel`）

### 6.4 异常处理

- 禁止空 `catch`。
- 禁止吞异常后静默失败。
- I/O、配置、网络调用需要在合适位置捕获并反馈到日志或 UI。
- 优先定位根因，不要用兜底分支掩盖问题。
- UI 层可采用“柔性失败”策略（如文件夹打开失败）
- 服务层、核心流程出现异常时应记录日志：使用 `ILogService`
- 全局异常处理已在 `App.xaml.cs` 注册（`UnhandledException` + `DispatcherUnhandledException`）

###  Polly 异常处理规范

- 仅用于外部依赖的瞬时故障（如网络、IO、超时），禁止用于业务或确定性错误（如参数错误、404）。
- 使用 `ResiliencePipelineBuilder` 构建，Retry 必须包含 `MaxRetryAttempts` + `ShouldHandle`，禁止无限或无条件重试。
- 所有外部调用必须设置 `Timeout`，高风险依赖建议加入 `CircuitBreaker`。
- Pipeline 必须复用（如 DI 单例），禁止在方法内部重复创建。
- 必须记录关键事件（如 `OnRetry` / `OnTimeout`）以保证可观测性。

### 6.5 空值与可空性
- 项目启用 `Nullable`，新增代码需显式处理可空场景
- 使用 `?` 标注可空引用类型，避免无意义的 `!`

### 6.6 集合与通知
- 需要 UI 响应的集合使用 `ObservableCollection<T>`
- 复杂绑定依赖关系可用 `On<Property>Changed` 部分方法

## 7. 依赖注入与服务规范
- 所有服务在 `App.xaml.cs` 中注册为单例
- ViewModel 构造函数注入服务依赖
- 新增服务时：
  - 先定义接口（`Services/Interfaces/`）
  - 再实现具体服务（`Services/`）
  - 在 `App.xaml.cs` 中注册

## 8. 文件与编码
- 所有文件必须使用 UTF-8 编码
- 避免引入不可读字符或本地编码（如 GBK/ANSI）

## 9. 工程与工具约束
- 仅在明确要求时新增第三方依赖
- 修复问题优先最小化修改，避免无关重构
- 修改前检查是否已有类似实现，优先复用现有模式

## 10. 说明
- 仓库内未发现 `.cursor/rules/`、`.cursorrules` 或 `.github/copilot-instructions.md`
- 若这些文件未来出现，应以其内容为更高优先级规则

### 文档创建限制
- **默认禁止**：不要主动创建 README、文档、说明文件
- **仅当用户明确要求时**才创建文档
- **完成任务后**仅输出简洁的完成总结（3-5 行），不写长文档