#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
测试：先手动在SolidWorks中打开文件，然后用Python激活
"""

import sys
import io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

import win32com.client
import pythoncom

test_file = r"D:/参数化试验出图1/16029 寄存柜(1200×1917×485) 左12右13门/1.工程图/上盖壳体前侧板_R55.SLDDRW"

print("=== 测试方案 ===")
print()
print("请手动操作：")
print("1. 在SolidWorks中打开文件：")
print(f"   {test_file}")
print("2. 打开后，输入'y'继续")
print()

input("准备好了吗？(输入y继续): ")

try:
    pythoncom.CoInitialize()
    sw = win32com.client.Dispatch("SldWorks.Application")
    sw.Visible = True
    print("✓ SolidWorks已连接")
    print()

    # 尝试激活已打开的文档
    print("尝试激活当前文档...")
    sw_model = sw.ActiveDoc

    if sw_model:
        print(f"✓ 找到活动文档！")
        print(f"  文件名: {sw_model.GetTitle()}")
        print(f"  类型: {sw_model.GetType()}")
        print(f"  路径: {sw_model.GetPathName()}")
        print()

        # 测试导出DXF
        output_path = r"D:/参数化试验出图1/16029 寄存柜(1200×1917×485) 左12右13门/2.钣金展开图/DXF/TEST_展开.DXF"
        print(f"尝试导出DXF到: {output_path}")

        success = sw_model.ExportToDWG2(
            output_path,
            output_path,
            1,  # AutoCAD 2013
            True,  # 导出所有图纸
            None,
            False,
            False,
            False,
            True   # DXF格式
        )

        if success:
            print("✓ DXF导出成功！")
        else:
            print("❌ DXF导出失败")

        # 测试导出PDF
        output_pdf = r"D:/参数化试验出图1/16029 寄存柜(1200×1917×485) 左12右13门/2.钣金展开图/PDF/TEST_展开.pdf"
        print(f"\n尝试导出PDF到: {output_pdf}")

        success = sw_model.Extension.SaveAs(
            output_pdf,
            0,
            1,  # Silent
            None,
            0,
            0
        )

        if success:
            print("✓ PDF导出成功！")
        else:
            print("❌ PDF导出失败")

    else:
        print("❌ 没有找到活动文档")
        print("请确保在SolidWorks中已打开文件")

    pythoncom.CoUninitialize()

except Exception as e:
    print(f"❌ 错误: {e}")
    import traceback
    traceback.print_exc()
