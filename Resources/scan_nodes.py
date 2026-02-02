#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
ComfyUI 自定义节点扫描器
直接使用 ComfyUI 的节点加载机制，确保环境完全一致
"""

import sys
import os
import json
import asyncio


def main():
    """主函数：在 ComfyUI 环境中扫描所有已注册节点"""
    if len(sys.argv) != 2:
        error_output = {
            "nodes": {},
            "total_packages": 0,
            "total_nodes": 0,
            "errors": None,
            "error": "用法: python scan_nodes.py <comfy_path>\n示例: python scan_nodes.py E:\\ComfyUI",
        }
        print(json.dumps(error_output, ensure_ascii=False, indent=2))
        sys.exit(1)

    comfy_path = sys.argv[1]

    # 验证路径
    if not os.path.exists(comfy_path):
        error_output = {
            "nodes": {},
            "total_packages": 0,
            "total_nodes": 0,
            "errors": None,
            "error": f"ComfyUI 路径不存在: {comfy_path}",
        }
        print(json.dumps(error_output, ensure_ascii=False, indent=2))
        sys.exit(1)

    # 切换工作目录到 ComfyUI 根目录（模拟 ComfyUI 启动环境）
    original_cwd = os.getcwd()
    os.chdir(comfy_path)

    # 将 ComfyUI 添加到 sys.path 最前面
    sys.path.insert(0, comfy_path)

    # 禁用命令行参数解析（避免冲突）
    # ComfyUI 使用 comfy.options.enable_args_parsing() 来启用参数解析
    # 我们需要在导入前设置一些环境变量来避免问题
    os.environ["COMFY_DISABLE_ARGS_PARSING"] = "1"

    try:
        # 导入 ComfyUI 的节点模块
        import nodes

        # 使用 asyncio 运行 init_extra_nodes 来加载自定义节点
        # 这是 ComfyUI main.py 中使用的方法
        if hasattr(nodes, "init_extra_nodes"):
            loop = asyncio.new_event_loop()
            asyncio.set_event_loop(loop)
            try:
                loop.run_until_complete(
                    nodes.init_extra_nodes(init_custom_nodes=True, init_api_nodes=False)
                )
            finally:
                loop.close()

        # 获取所有已注册的节点
        all_nodes = list(nodes.NODE_CLASS_MAPPINGS.keys())

        # 运行时展示名映射（UI 保存的 type 经常是展示名，而不是 NODE_CLASS_MAPPINGS 的 key）
        # 优先使用 ComfyUI 若提供的 NODE_DISPLAY_NAME_MAPPINGS；否则尝试从节点类的 DISPLAY_NAME 推导
        display_name_to_type = {}

        if hasattr(nodes, "NODE_DISPLAY_NAME_MAPPINGS"):
            # 期望结构：{ class_type: display_name }
            try:
                for (
                    class_type,
                    display_name,
                ) in nodes.NODE_DISPLAY_NAME_MAPPINGS.items():
                    if isinstance(display_name, str) and display_name:
                        display_name_to_type[display_name] = class_type
            except Exception:
                # 不吞异常：让外层 except 报错，便于定位兼容性问题
                raise

        if not display_name_to_type:
            # 尝试从节点类的 DISPLAY_NAME 推导
            try:
                for class_type, node_cls in nodes.NODE_CLASS_MAPPINGS.items():
                    display_name = getattr(node_cls, "DISPLAY_NAME", None)
                    if isinstance(display_name, str) and display_name:
                        display_name_to_type[display_name] = class_type
            except Exception:
                raise

        # 尝试按包分组（如果可能）
        nodes_by_package = {}

        # 检查是否有包信息
        if hasattr(nodes, "NODE_PACKAGE_MAPPINGS"):
            # 某些版本的 ComfyUI 有包映射
            for node_name, package_name in nodes.NODE_PACKAGE_MAPPINGS.items():
                if package_name not in nodes_by_package:
                    nodes_by_package[package_name] = []
                nodes_by_package[package_name].append(node_name)
        else:
            # 没有包信息，把所有节点放到 "all" 组
            nodes_by_package["_all_nodes"] = all_nodes

        output = {
            "nodes": nodes_by_package,
            "total_packages": len(nodes_by_package),
            "total_nodes": len(all_nodes),
            "all_node_types": all_nodes,  # 额外提供完整列表
            "display_name_to_type": display_name_to_type,
            "errors": None,
        }

        print(json.dumps(output, ensure_ascii=False, indent=2))

    except ImportError as e:
        # ComfyUI 核心模块导入失败
        error_output = {
            "nodes": {},
            "total_packages": 0,
            "total_nodes": 0,
            "errors": None,
            "error": f"无法导入 ComfyUI 模块: {str(e)}",
        }
        print(json.dumps(error_output, ensure_ascii=False, indent=2))
        sys.exit(1)

    except Exception as e:
        import traceback

        error_output = {
            "nodes": {},
            "total_packages": 0,
            "total_nodes": 0,
            "errors": None,
            "error": f"扫描失败: {str(e)}\n{traceback.format_exc()}",
        }
        print(json.dumps(error_output, ensure_ascii=False, indent=2))
        sys.exit(1)

    finally:
        os.chdir(original_cwd)


if __name__ == "__main__":
    main()
