#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
探测 SolidWorks API 可用方法
"""

import sys
import io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

import win32com.client
import pythoncom

print("=== 探测 SolidWorks API ===")
print()

try:
    pythoncom.CoInitialize()
    sw = win32com.client.Dispatch("SldWorks.Application")
    sw.Visible = True
    print("✓ SolidWorks已连接")
    print()

    # 列出所有可用方法
    print("[可用方法]")
    methods = [m for m in dir(sw) if not m.startswith('_')]

    # 查找打开文档相关的方法
    print("\n[打开文档相关]")
    open_methods = [m for m in methods if 'open' in m.lower() or 'doc' in m.lower()]
    for m in open_methods[:20]:
        print(f"  - {m}")

    # 查找关闭相关的方法
    print("\n[关闭/退出相关]")
    close_methods = [m for m in methods if 'close' in m.lower() or 'exit' in m.lower() or 'quit' in m.lower()]
    for m in close_methods[:20]:
        print(f"  - {m}")

    print()
    print(f"总共 {len(methods)} 个方法")

    pythoncom.CoUninitialize()

except Exception as e:
    print(f"❌ 错误: {e}")
    import traceback
    traceback.print_exc()
