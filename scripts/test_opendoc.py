#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
测试 SolidWorks OpenDoc6 API
找出正确的参数类型
"""

import sys
import io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

import win32com.client
import pythoncom

# 测试文件
test_file = r"D:/参数化试验出图1/16029 寄存柜(1200×1917×485) 左12右13门/1.工程图/上盖壳体前侧板_R55.SLDDRW"

print("=== 测试 SolidWorks OpenDoc6 ===" )
print(f"测试文件: {test_file}")
print()

try:
    pythoncom.CoInitialize()
    sw = win32com.client.Dispatch("SldWorks.Application")
    sw.Visible = True
    print("✓ SolidWorks已连接")
    print()

    # 方法1：使用简化的 OpenDoc
    print("[方法1] 使用 OpenDoc (简化版)...")
    try:
        sw_model = sw.OpenDoc(test_file, 3)  # 3 = swDocDRAWING
        if sw_model:
            print("✓ 成功打开文件（方法1）")
            print(f"  文件名: {sw_model.GetTitle()}")
            sw.CloseDoc(test_file)
        else:
            print("❌ 方法1失败")
    except Exception as e:
        print(f"❌ 方法1异常: {e}")

    print()

    # 方法2：使用 OpenDoc6 with Dispatch
    print("[方法2] 使用 OpenDoc6...")
    try:
        from win32com.client import VARIANT
        import pythoncom

        # 创建 VARIANT 引用参数
        errors = VARIANT(pythoncom.VT_BYREF | pythoncom.VT_I4, 0)
        warnings = VARIANT(pythoncom.VT_BYREF | pythoncom.VT_I4, 0)

        sw_model = sw.OpenDoc6(
            test_file,
            3,  # swDocDRAWING
            1,  # swOpenDocOptions_Silent
            "",
            errors,
            warnings
        )

        if sw_model:
            print("✓ 成功打开文件（方法2）")
            print(f"  文件名: {sw_model.GetTitle()}")
            print(f"  错误代码: {errors.value}")
            print(f"  警告代码: {warnings.value}")
            sw.CloseDoc(test_file)
        else:
            print(f"❌ 方法2失败")
            print(f"  错误代码: {errors.value}")
            print(f"  警告代码: {warnings.value}")

    except Exception as e:
        print(f"❌ 方法2异常: {e}")
        import traceback
        traceback.print_exc()

    print()
    print("=== 测试完成 ===")

    sw.ExitApp()
    pythoncom.CoUninitialize()

except Exception as e:
    print(f"❌ 严重错误: {e}")
    import traceback
    traceback.print_exc()
