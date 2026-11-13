#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
快速测试 OpenDoc 方法
"""

import sys
import io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

import win32com.client
import pythoncom

test_file = r"D:/参数化试验出图1/16029 寄存柜(1200×1917×485) 左12右13门/1.工程图/上盖壳体前侧板_R55.SLDDRW"

print("=== 测试 OpenDoc ===")
print(f"文件: {test_file}")
print()

try:
    pythoncom.CoInitialize()
    sw = win32com.client.Dispatch("SldWorks.Application")
    sw.Visible = True
    print("✓ SolidWorks已连接")
    print()

    print("尝试打开文件...")
    sw_model = sw.OpenDoc(test_file, 3)  # 3 = swDocDRAWING

    if sw_model:
        print(f"✓ 成功！")
        print(f"  文件名: {sw_model.GetTitle()}")
        print(f"  类型: {sw_model.GetType()}")

        # 关闭文档
        sw.CloseDoc(test_file)
        print("✓ 文档已关闭")
    else:
        print("❌ OpenDoc返回None")

    pythoncom.CoUninitialize()

except Exception as e:
    print(f"❌ 错误: {e}")
    import traceback
    traceback.print_exc()
