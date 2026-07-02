# ComfyUI 多进程模型 MMAP 共享结论

## 问题结论

在同一台机器上开多个 ComfyUI 进程，如果它们加载的是**同一个本地模型文件**，并且该模型走的是 **mmap** 路径，那么：

- **CPU/RAM 层面**：Windows 可以把同一文件的映射页复用到多个进程，模型文件页不一定会被每个进程完整复制一份。
- **GPU/显存层面**：不会共享。每个 ComfyUI 进程把模型放到各自 GPU 后，显存仍然是各占一份。

也就是说，**mmap 共享的是文件页缓存，不是 ComfyUI 自己的“跨进程模型缓存”，更不是显存共享**。

## ComfyUI 的实际行为

- 对于 **`.safetensors`**，ComfyUI 的加载路径默认就是 mmap 方向。
- 对于 **`.ckpt` / `.pt`**，需要显式启用 `--mmap-torch-files` 才会走 mmap。
- `--disable-mmap` 是**关闭** mmap，不是开启共享。
- ComfyUI 没有额外的“跨进程共享缓存”专用开关。

## GGUF 的情况

GGUF 在 ComfyUI 里通常不是走主程序内置 loader，而是通过**第三方 custom node** 来加载，最常见的是 `ComfyUI-GGUF` 这类节点包。

这意味着 GGUF 的行为要按 **loader 实现** 来看，而不是只看 ComfyUI 本体：

- GGUF 格式本身支持 mmap。
- 具体到 ComfyUI 里的 GGUF，是否能复用文件页缓存，取决于第三方 node 的 loader 是否走 mmap。
- 从 `ComfyUI-GGUF` 的实现看，它在加载后会尝试释放 mmap 相关引用，因此更像是“加载阶段利用文件映射”，而不是“长期常驻一个跨进程共享的模型缓存对象”。

所以对 GGUF 更准确的说法是：

- **RAM / 文件页缓存**：有机会复用，前提是同一个本地 GGUF 文件、loader 走 mmap。
- **ComfyUI 进程内模型对象**：不共享。
- **GPU 显存**：不共享，仍然各进程各一份。

## 需要的启动选项

### 1. `.safetensors`
通常不需要额外参数，默认即可走 mmap。

如果你明确传了 `--disable-mmap`，就会关闭这条路径。

### 2. `.ckpt` / `.pt`
需要启动参数：

```bash
python main.py --mmap-torch-files
```

## 什么时候“看起来像重复占内存”

以下情况会让你误以为每个进程都重复缓存了一份：

- 看的是进程 `Working Set`，而不是按文件统计的物理页。
- 模型不是同一个文件，或者文件内容不同。
- 进程启动后又把权重搬到各自 GPU，导致显存分别占用。
- 模型来自网络盘或特殊挂载，mmap 可能退化得很慢，加载行为也会更明显。

## 如何验证

建议按这个顺序看：

1. 用 **RAMMap** 看 `File Summary`，确认同一个模型文件是否被系统作为文件页缓存复用。
2. 用进程视图看 `Working Set`，但不要把它当成“总重复占用”的唯一证据，因为它包含 shared 和 private 页。
3. 再看 GPU 显存占用，确认每个 ComfyUI 进程是否各自持有一份 GPU 权重。

## 相关依据

- Windows 文件映射可跨进程共享：
  - [Memory-Mapped File Information](https://learn.microsoft.com/en-us/windows/win32/psapi/memory-mapped-file-information)
  - [Sharing Files and Memory](https://learn.microsoft.com/en-us/windows/win32/memory/sharing-files-and-memory)
- Windows `Working Set` 包含 shared 和 private：
  - [Process Working Set](https://learn.microsoft.com/en-us/windows/win32/procthread/process-working-set)
- RAMMap 可按文件和进程查看物理内存：
  - [RAMMap](https://learn.microsoft.com/it-it/sysinternals/downloads/rammap)
- ComfyUI 社区关于 mmap 与参数的讨论：
  - [Option for disabling mmap for safetensors loading for network storage users #2288](https://github.com/comfyanonymous/ComfyUI/issues/2288)
  - [Using server arguments crashed Comfy Desktop start up #8690](https://github.com/Comfy-Org/ComfyUI/issues/8690)

## 简短结论

如果你的目标是“多个 ComfyUI 进程是否能复用同一模型的 RAM 文件页缓存”，答案是：**可以，前提是走 mmap 且是同一个本地文件**。  
如果你的目标是“多个进程是否共享一份 GPU 显存里的模型”，答案是：**不可以**。
