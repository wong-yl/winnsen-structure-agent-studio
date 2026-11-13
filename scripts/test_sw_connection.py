#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""测试 SolidWorks 连接"""

import sys
import io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

import win32com.client
import pythoncom

print("测试 SolidWorks 连接...")

try:
    pythoncom.CoInitialize()
    print("1. COM 初始化成功")

    sw = win32com.client.Dispatch("SldWorks.Application")
    print("2. 连接到 SolidWorks 成功")

    sw.Visible = True
    print("3. SolidWorks 可见")

    # 测试获取文档数
    try:
        doc_count = sw.GetDocumentCount
        if callable(doc_count):
            doc_count = doc_count()
        print(f"4. 当前打开文档数: {doc_count}")
    except Exception as e:
        print(f"4. 获取文档数失败: {e}")

    print("\n✅ SolidWorks 连接正常")

    pythoncom.CoUninitialize()

except Exception as e:
    print(f"\n❌ 错误: {e}")
    import traceback
    traceback.print_exc()
