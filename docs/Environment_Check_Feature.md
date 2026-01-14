# ComfyUI 环境检测功能

## 功能概述

为 ComfyShell 添加了 ComfyUI 运行环境检测单元，可一键检测系统环境是否满足 ComfyUI 运行要求。

## 检测项目

### 必需项（Must Pass）

| 检测项 | 要求 | 说明 |
|--------|------|------|
| **Python 版本** | >= 3.10 | 检测 Python 是否已安装，版本是否符合要求 |
| **PyTorch** | 已安装 | 检测 PyTorch 库是否可正常导入 |
| **Git** | 已安装 | 检测 Git 是否可用（用于自定义节点管理） |

### 可选项（Optional）

| 检测项 | 说明 |
|--------|------|
| **CUDA 支持** | 检测 NVIDIA GPU 及 CUDA 可用性（通过 nvidia-smi 和 PyTorch） |
| **AMD ROCm 支持** | 检测 AMD GPU 及 ROCm 可用性（通过 rocm-smi 和 PyTorch） |
| **FFmpeg** | 检测 FFmpeg 是否在环境变量中（视频处理功能需要） |

## 使用方法

### 入口位置

**控制台（Process Monitor）页面** → 顶部工具栏 → **"检测环境"** 按钮

- 位置：在"清空日志"按钮的左侧
- 样式：金色高亮按钮（AccentButtonStyle）
- 检测中时按钮禁用，避免重复执行

### 检测结果输出

所有检测结果实时输出到控制台日志列表中，格式如下：

```
========================================
开始 ComfyUI 环境检测
========================================
[检测] Python 版本...
  ✓ Python 3.10.11
[检测] Git 可用性...
  ✓ git version 2.39.1.windows.1
[检测] PyTorch 可用性...
  ✓ PyTorch 2.0.1+cu118
[检测] CUDA 支持...
  ✓ NVIDIA GPU 可用
    Driver Version: 536.23, GPU: NVIDIA GeForce RTX 3060
    PyTorch CUDA: 可用
[检测] AMD ROCm 支持...
  ⚠ ROCm 不可用（将使用 CUDA 或 CPU 模式）
[检测] FFmpeg 可用性...
  ✓ ffmpeg version 5.1.2-full_build
========================================
环境检测完成 - ✓ 所有必需项通过
========================================
```

### 状态图标说明

- `✓` **成功** - 该项检测通过
- `⚠` **警告** - 非必需项未通过（不影响基本功能）
- `✗` **错误** - 必需项未通过（可能影响 ComfyUI 运行）

## 检测实现细节

### 1. Python 版本检测

```bash
python --version
```

- 解析版本号（格式：Python 3.10.11）
- 检查是否 >= 3.10
- 如果配置了自定义 Python 路径，优先使用配置的路径

### 2. Git 可用性检测

```bash
git --version
```

- 检测 Git 是否在 PATH 环境变量中
- 输出 Git 版本信息

### 3. PyTorch 可用性检测

```python
import torch
print(f'PyTorch {torch.__version__}')
```

- 通过 Python 脚本检测 PyTorch 是否可导入
- 输出 PyTorch 版本号

### 4. CUDA 支持检测

**方法 1：nvidia-smi**
```bash
nvidia-smi --query-gpu=driver_version,name --format=csv,noheader
```

**方法 2：PyTorch CUDA**
```python
import torch
print('CUDA available' if torch.cuda.is_available() else 'CUDA not available')
print(f'Device count: {torch.cuda.device_count()}')
print(f'Device name: {torch.cuda.get_device_name(0)}')
```

- 两种方法并行检测，任一成功即认为 CUDA 可用
- 如果 nvidia-smi 成功，进一步检测 PyTorch CUDA 可用性

### 5. AMD ROCm 支持检测

**方法 1：rocm-smi**
```bash
rocm-smi --showproductname
```

**方法 2：PyTorch ROCm**
```python
import torch
hip_available = hasattr(torch.version, 'hip') and torch.version.hip is not None
print('ROCm available' if hip_available else 'ROCm not available')
print(f'HIP version: {torch.version.hip}')
```

### 6. FFmpeg 可用性检测

```bash
ffmpeg -version
```

- 检测 FFmpeg 是否在 PATH 环境变量中
- 提取并显示版本信息

## 代码架构

### 新增文件

1. **`Services/Interfaces/IEnvironmentCheckService.cs`**
   - 环境检测服务接口
   - 定义 `EnvironmentCheckResult`, `CheckItemResult`, `CheckStatus` 等数据结构

2. **`Services/EnvironmentCheckService.cs`**
   - 环境检测服务实现
   - 包含所有检测逻辑

3. **`Converters/InverseBooleanConverter.cs`**
   - 布尔值取反转换器（用于按钮禁用逻辑）

### 修改文件

1. **`ViewModels/ProcessMonitorViewModel.cs`**
   - 添加 `IEnvironmentCheckService` 和 `IPythonPathService` 依赖注入
   - 添加 `CheckEnvironmentCommand` 命令
   - 添加 `IsCheckingEnvironment` 属性

2. **`Views/ProcessMonitorView.xaml`**
   - 在工具栏添加"检测环境"按钮
   - 配置按钮的 ToolTip（显示检测项列表）
   - 绑定 `CheckEnvironmentCommand`

3. **`App.xaml.cs`**
   - 注册 `IEnvironmentCheckService` 服务到 DI 容器

4. **`App.xaml`**
   - 注册 `InverseBooleanConverter` 到全局资源

## 使用场景

### 1. 新用户初次安装

在首次使用 ComfyShell 之前，点击"检测环境"确保系统满足运行要求：

- 确认 Python 版本正确
- 确认 PyTorch 已安装
- 确认 Git 可用
- 了解 GPU 支持情况（CUDA/ROCm）

### 2. 启动失败排查

当 ComfyUI 启动失败时，使用环境检测快速定位问题：

- Python 版本过低？
- PyTorch 未安装？
- CUDA 不可用？

### 3. 环境变化后验证

在以下情况后验证环境：

- 更新/重装 Python
- 更新/重装 PyTorch
- 更新显卡驱动
- 更新 CUDA/ROCm

## 技术亮点

### 1. 多层回退检测策略

以 CUDA 检测为例：
1. 尝试 `nvidia-smi` 命令
2. 如果失败，尝试 `torch.cuda.is_available()`
3. 如果两者都失败，报告 CUDA 不可用（但不视为错误）

### 2. 超时保护

每个命令执行都有超时限制（默认 30 秒，可调整）：
- Python/Git/PyTorch 检测：30 秒
- CUDA/ROCm/FFmpeg 检测：5-10 秒

避免因命令卡住导致整个检测流程阻塞。

### 3. 异步执行

所有检测逻辑完全异步，不阻塞 UI 主线程。

### 4. 实时日志输出

检测过程中实时输出日志到控制台，用户可立即看到检测进度。

## 已知限制

### 1. Python 路径获取

当前从 `IPythonPathService.PythonPath` 获取 Python 路径：
- 如果未配置，默认使用系统 PATH 中的 `python`
- 如果 ComfyUI 使用 `python_embeded`，但未在配置中指定，可能检测不准确

### 2. GPU 检测准确性

- 某些旧版驱动可能导致 `nvidia-smi` 输出格式不同
- PyTorch CUDA 版本与系统 CUDA 版本可能不匹配
- ROCm 检测依赖于 `rocm-smi` 命令，某些 AMD GPU 驱动不提供此命令

### 3. FFmpeg 检测

仅检测 `ffmpeg` 命令是否在 PATH 中，不检测功能完整性（如 codec 支持）。

## 未来改进方向

1. **详细报告导出**
   - 将检测结果导出为 JSON/Markdown 文件
   - 包含更详细的硬件信息

2. **自动修复建议**
   - 当检测失败时，提供修复建议和下载链接
   - 例如：Python 版本过低 → 提供 Python 官网下载链接

3. **定期后台检测**
   - 在应用启动时自动进行快速检测
   - 当环境发生变化时通知用户

4. **自定义节点依赖检测**
   - 检测已安装的自定义节点所需的 Python 库是否满足
   - 提示缺失的依赖

## 测试建议

### 正常环境

- ✅ Python 3.10+，PyTorch 已安装，Git 已安装
- ✅ NVIDIA GPU + CUDA
- ✅ FFmpeg 已安装

### 部分缺失环境

- ⚠ Python 3.10+，PyTorch 已安装，Git 已安装，无 GPU
- ⚠ Python 3.10+，PyTorch 已安装，Git 已安装，无 FFmpeg

### 错误环境

- ❌ Python < 3.10
- ❌ PyTorch 未安装
- ❌ Git 未安装

## 相关文档

- [AGENTS.md](../AGENTS.md) - 项目代码规范
- [README.md](../README.md) - ComfyShell 项目说明

---

**版本**: v1.0  
**日期**: 2026-01-14  
**作者**: AI Assistant (Sisyphus)
