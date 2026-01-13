# 代理服务器功能实现说明

## 功能概述

已成功在设置页面添加代理服务器配置功能。启用后，ComfyUI、Git、pip 和版本管理等所有进程都将使用指定的代理服务器。

## 实现的文件

### 1. 数据模型 (`Models/AppSettings.cs`)
- 新增 `ProxySettings` 类，包含：
  - `Enabled`: 是否启用代理
  - `Server`: 代理服务器地址（如 `http://127.0.0.1:7890`）
  - `Username`: 代理用户名（可选）
  - `Password`: 代理密码（可选）

### 2. 服务接口 (`Services/Interfaces/IProxyService.cs`)
- 定义代理服务接口
- `IsEnabled`: 检查代理是否启用
- `GetProxyServer()`: 获取代理服务器地址
- `ConfigureProcessProxy()`: 配置进程环境变量以使用代理

### 3. 服务实现 (`Services/ProxyService.cs`)
- 实现代理配置逻辑
- 自动配置以下环境变量：
  - `HTTP_PROXY` / `http_proxy`
  - `HTTPS_PROXY` / `https_proxy`
  - `PIP_PROXY` (pip 专用)
  - `GIT_HTTP_PROXY_AUTHMETHOD` (Git 专用)
  - `NO_PROXY` / `no_proxy` (本地地址排除)

### 4. 集成到现有服务
- **ProcessService** (`Services/ProcessService.cs`)
  - 注入 `IProxyService`
  - 在启动 ComfyUI 进程前配置代理环境变量
  
- **GitService** (`Services/GitService.cs`)
  - 注入 `IProxyService`
  - 在执行所有 Git 命令前配置代理环境变量

### 5. UI 界面 (`Views/SettingsView.xaml` 和 `.xaml.cs`)
- 新增"网络代理"配置区域，包含：
  - 启用/禁用复选框
  - 代理服务器地址输入框
  - 用户名输入框（可选）
  - 密码输入框（可选，使用 PasswordBox 安全控件）
- 实现密码框的双向绑定逻辑

### 6. 依赖注入 (`App.xaml.cs`)
- 在 DI 容器中注册 `IProxyService` → `ProxyService`

## 使用方法

1. 打开应用，导航到"设置"页面
2. 找到"网络代理"区域
3. 勾选"启用代理服务器"
4. 输入代理服务器地址，例如：
   - `http://127.0.0.1:7890` (Clash)
   - `http://127.0.0.1:1080` (V2Ray)
   - `http://proxy.company.com:8080` (企业代理)
5. 如果代理需要认证，填写用户名和密码（可选）
6. 点击"保存配置"

## 代理生效范围

配置代理后，以下所有操作都会自动使用代理：

### ComfyUI 进程
- Python 包管理器 (pip) 下载依赖
- ComfyUI 运行时的网络请求
- 模型下载

### Git 操作
- 克隆仓库 (`git clone`)
- 拉取更新 (`git fetch`, `git pull`)
- 推送提交 (`git push`)
- 自定义节点更新

### 版本管理
- 下载新版本 ComfyUI
- 检查更新

## 技术细节

### 环境变量配置
系统通过设置标准的代理环境变量来实现代理功能：

```csharp
startInfo.Environment["HTTP_PROXY"] = "http://127.0.0.1:7890";
startInfo.Environment["HTTPS_PROXY"] = "http://127.0.0.1:7890";
startInfo.Environment["NO_PROXY"] = "localhost,127.0.0.1,::1";
```

### 认证支持
如果提供了用户名和密码，会自动构建认证 URL：
```
http://username:password@proxy.server.com:8080
```

### 本地地址排除
自动排除本地地址，避免本地通信经过代理：
- `localhost`
- `127.0.0.1`
- `::1`

## 配置存储

代理配置保存在 `appsettings.json` 中：

```json
{
  "AppSettings": {
    "Proxy": {
      "Enabled": true,
      "Server": "http://127.0.0.1:7890",
      "Username": "",
      "Password": ""
    }
  }
}
```

## 注意事项

1. **密码安全**：密码以明文存储在配置文件中，请注意文件权限
2. **代理格式**：必须包含协议前缀（`http://` 或 `https://`）
3. **网络测试**：保存配置后，建议先测试 Git 操作以验证代理是否正常工作
4. **性能影响**：使用代理可能会略微降低网络速度，取决于代理服务器性能

## 故障排查

如果代理不生效：
1. 确认代理服务器地址格式正确
2. 确认代理服务器正在运行
3. 检查代理服务器是否允许 HTTP/HTTPS 流量
4. 如果使用认证，确认用户名和密码正确
5. 查看控制台日志输出，确认环境变量已设置

## 未来改进

可选的后续优化：
- [ ] 密码加密存储
- [ ] 测试代理连接功能
- [ ] 支持 SOCKS5 代理
- [ ] 支持 PAC 自动配置
- [ ] 支持排除特定域名/IP
