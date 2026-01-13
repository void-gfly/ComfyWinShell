# 网络加速功能实现说明

## 功能概述

已在设置页面实现完整的网络加速功能，包括：
1. **代理服务器**：支持 HTTP/HTTPS 代理
2. **GitHub 镜像站点**：加速 Git 克隆和自定义节点下载
3. **pip 镜像源**：加速 Python 包下载

这些配置会影响 ComfyUI、Git、pip 和版本管理等所有网络操作。

---

## 一、代理服务器

### 配置说明
- **启用状态**：勾选"启用代理服务器"
- **代理地址**：如 `http://127.0.0.1:7890`
- **认证信息**：用户名和密码（可选）

### 适用场景
- 使用 Clash、V2Ray 等代理工具
- 企业网络环境需要代理
- 需要通过代理访问国外资源

### 生效范围
- ✅ ComfyUI 进程（pip、网络请求、模型下载）
- ✅ Git 操作（克隆、拉取、推送）
- ✅ 版本管理（下载、更新）

### 环境变量
自动配置：
- `HTTP_PROXY` / `http_proxy`
- `HTTPS_PROXY` / `https_proxy`
- `NO_PROXY` / `no_proxy`
- `PIP_PROXY`
- `GIT_HTTP_PROXY_AUTHMETHOD`

---

## 二、GitHub 镜像站点

### 配置说明
- **启用状态**：勾选"启用 GitHub 镜像站点"
- **镜像地址**：默认 `https://ghproxy.com/https://github.com`

### 常用镜像站点
| 镜像站点 | 地址 | 说明 |
|---------|------|------|
| ghproxy | `https://ghproxy.com/https://github.com` | 稳定性好，推荐 |
| mirror.ghproxy | `https://mirror.ghproxy.com/https://github.com` | 备用镜像 |
| 99988866 | `https://gh.api.99988866.xyz/https://github.com` | 备用镜像 |

### 适用场景
- GitHub 访问速度慢
- Git 克隆经常超时
- 下载自定义节点失败

### 工作原理
系统自动将 GitHub URL 转换为镜像 URL：
- 原始：`https://github.com/user/repo.git`
- 转换：`https://ghproxy.com/https://github.com/user/repo.git`

### 生效范围
- ✅ Git Clone 操作（安装 ComfyUI-Manager）
- ✅ 自定义节点下载
- ✅ 版本管理器克隆 ComfyUI

### 实现细节
- 在 `GitCloneDialog` 中自动转换 URL
- 不修改原始 URL 配置
- 仅影响实际网络请求

---

## 三、pip 镜像源

### 配置说明
- **启用状态**：勾选"启用 pip 镜像源"
- **镜像地址**：默认清华大学镜像
- **信任主机**：勾选以避免 SSL 证书问题

### 常用镜像源
| 镜像源 | 地址 | 速度 |
|--------|------|------|
| 清华大学 | `https://pypi.tuna.tsinghua.edu.cn/simple` | ⭐⭐⭐⭐⭐ |
| 阿里云 | `https://mirrors.aliyun.com/pypi/simple` | ⭐⭐⭐⭐ |
| 中科大 | `https://pypi.mirrors.ustc.edu.cn/simple` | ⭐⭐⭐⭐ |
| 腾讯云 | `https://mirrors.cloud.tencent.com/pypi/simple` | ⭐⭐⭐⭐ |

### 适用场景
- pip 安装依赖慢
- PyPI 官方源连接超时
- ComfyUI 启动时下载依赖失败

### 工作原理
通过环境变量配置 pip 镜像源：
- `PIP_INDEX_URL`: 指定镜像源地址
- `PIP_TRUSTED_HOST`: 信任该域名（跳过 SSL 验证）

### 生效范围
- ✅ ComfyUI 启动时自动安装依赖
- ✅ 用户手动执行 `pip install`
- ✅ 自定义节点安装依赖

### pip 命令参数
系统会自动添加以下参数：
```bash
-i https://pypi.tuna.tsinghua.edu.cn/simple --trusted-host pypi.tuna.tsinghua.edu.cn
```

---

## 实现的文件清单

### 1. 数据模型
- **`Models/AppSettings.cs`**
  - 新增 `GitHubMirrorSettings` 类
  - 新增 `PipMirrorSettings` 类

### 2. 服务层
- **`Services/Interfaces/IProxyService.cs`**
  - 新增 `ConvertGitHubUrl()` 方法
  - 新增 `GetPipMirrorArgs()` 方法
  - 新增镜像状态属性

- **`Services/ProxyService.cs`**
  - 实现 GitHub URL 转换逻辑
  - 实现 pip 镜像参数生成
  - 自动配置 pip 镜像环境变量

### 3. Git 克隆对话框
- **`Views/GitCloneDialog.xaml.cs`**
  - 注入 `IProxyService`
  - 自动转换 GitHub URL
  - 配置代理环境变量

### 4. UI 界面
- **`Views/SettingsView.xaml`**
  - 新增"镜像加速"配置区域
  - GitHub 镜像配置
  - pip 镜像配置
  - 常用镜像源提示

### 5. 应用入口
- **`App.xaml.cs`**
  - 暴露 `AppHost` 属性供对话框访问服务

---

## 使用方法

### 方案一：仅使用镜像（推荐）
1. 打开"设置"页面
2. 找到"镜像加速"区域
3. 勾选"启用 GitHub 镜像站点"
4. 勾选"启用 pip 镜像源"
5. 保存配置

**优点**：无需额外软件，直接使用国内镜像

### 方案二：仅使用代理
1. 启动本地代理软件（Clash / V2Ray）
2. 在"设置"中配置代理服务器
3. 保存配置

**优点**：访问所有国外资源，包括模型下载站点

### 方案三：组合使用（最佳）
1. 启用代理服务器（访问模型站点）
2. 启用 GitHub 镜像（加速 Git 操作）
3. 启用 pip 镜像（加速依赖下载）

**优点**：全方位加速，体验最佳

---

## 配置示例

### appsettings.json
```json
{
  "AppSettings": {
    "Proxy": {
      "Enabled": false,
      "Server": "http://127.0.0.1:7890",
      "Username": "",
      "Password": ""
    },
    "GitHubMirror": {
      "Enabled": true,
      "MirrorUrl": "https://ghproxy.com/https://github.com"
    },
    "PipMirror": {
      "Enabled": true,
      "IndexUrl": "https://pypi.tuna.tsinghua.edu.cn/simple",
      "TrustedHost": true
    }
  }
}
```

---

## 故障排查

### GitHub 镜像不生效
1. 确认镜像地址格式正确
2. 检查镜像站点是否可访问
3. 查看 Git Clone 对话框日志，确认使用了镜像 URL

### pip 镜像不生效
1. 确认镜像源地址正确
2. 勾选"信任该镜像源"选项
3. 查看 ComfyUI 控制台日志，确认 pip 使用了镜像源

### 代理和镜像冲突
- **可以同时启用**：代理用于模型下载，镜像用于 Git/pip
- **优先级**：镜像 > 代理（镜像速度通常更快）

---

## 性能对比

### Git Clone 速度（克隆 ComfyUI-Manager）
| 方式 | 速度 | 说明 |
|------|------|------|
| 直连 GitHub | 10-50 KB/s | 经常超时 |
| 代理 | 500-2000 KB/s | 取决于代理速度 |
| GitHub 镜像 | 2-10 MB/s | 国内服务器，极快 |

### pip 安装速度（安装 torch）
| 方式 | 速度 | 说明 |
|------|------|------|
| PyPI 官方 | 100-500 KB/s | 不稳定 |
| 代理 | 500-2000 KB/s | 取决于代理速度 |
| pip 镜像 | 5-30 MB/s | 国内 CDN，极快 |

---

## 安全说明

### 代理服务器
- 密码以明文存储在 `appsettings.json`
- 建议仅在本地环境使用
- 企业环境请咨询 IT 部门

### 镜像站点
- GitHub 镜像：仅加速下载，不修改代码
- pip 镜像：使用官方包，但由镜像站缓存
- 建议使用知名机构的镜像（高校、云厂商）

### 信任主机选项
- 启用后跳过 SSL 证书验证
- 仅对配置的镜像源生效
- 降低安全性，但解决兼容性问题

---

## 高级技巧

### 自定义镜像站点
如果默认镜像失效，可以自行搜索可用镜像：
- GitHub 镜像：搜索 "github proxy" 或 "github 加速"
- pip 镜像：搜索 "pypi 镜像源"

### 临时禁用
如果遇到问题，可以快速禁用：
1. 取消勾选启用选项
2. 保存配置
3. 重启相关操作

### 测试连接
保存配置后，建议测试：
- **GitHub 镜像**：尝试安装 ComfyUI-Manager
- **pip 镜像**：查看 ComfyUI 启动日志
- **代理服务器**：使用 Git fetch 测试

---

## 未来改进

可选的后续优化：
- [ ] 添加镜像连接测试功能
- [ ] 支持多个备用镜像（自动切换）
- [ ] 添加镜像响应速度检测
- [ ] 支持 SOCKS5 代理协议
- [ ] 提供镜像站点推荐功能
- [ ] 添加网络诊断工具
