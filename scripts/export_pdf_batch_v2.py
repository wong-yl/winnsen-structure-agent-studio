#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
批量导出PDF - 使用更可靠的打开方式
"""

import sys
import io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

import os
import time
import win32com.client
import pythoncom
import pywintypes

# 配置
INPUT_DIR = r"C:\Users\Administrator\Desktop\delivery_r59_parts_RELEASED"

# SolidWorks常量
swDocDRAWING = 3
swOpenDocOptions_Silent = 1

# 统计
stats = {'total': 0, 'success': 0, 'failed': 0}

print("="*70)
print("  批量导出 PDF")
print("="*70)

# 初始化SolidWorks
print("\n[初始化] 连接到SolidWorks...")
pythoncom.CoInitialize()

try:
    sw = win32com.client.Dispatch("SldWorks.Application")
except:
    print("❌ 无法连接到SolidWorks")
    sys.exit(1)

sw.Visible = True
print("✓ SolidWorks已连接\n")

# 获取所有SLDDRW文件
files = [f for f in os.listdir(INPUT_DIR) if f.upper().endswith('.SLDDRW')]
stats['total'] = len(files)

print(f"✓ 找到 {len(files)} 个图纸文件\n")
print("="*70)

for idx, filename in enumerate(sorted(files), 1):
    print(f"\n[{idx}/{len(files)}] {filename}")

    drw_path = os.path.join(INPUT_DIR, filename)
    pdf_path = os.path.join(INPUT_DIR, filename.replace('.SLDDRW', '.pdf').replace('.slddrw', '.pdf'))

    try:
        # 打开文件（使用更详细的参数）
        print("  打开中...")

        # 先尝试关闭可能打开的同名文件
        try:
            sw.CloseDoc(drw_path)
        except:
            pass

        # 打开文档
        errors = win32com.client.VARIANT(pythoncom.VT_BYREF | pythoncom.VT_I4, 0)
        warnings = win32com.client.VARIANT(pythoncom.VT_BYREF | pythoncom.VT_I4, 0)

        sw_model = sw.OpenDoc6(
            drw_path,
            swDocDRAWING,
            swOpenDocOptions_Silent,
            "",
            errors,
            warnings
        )

        if not sw_model:
            print(f"  ❌ 打开失败 (errors: {errors.value}, warnings: {warnings.value})")
            stats['failed'] += 1
            continue

        print("  ✓ 已打开")

        # 激活文档
        sw.ActivateDoc3(drw_path, False, swOpenDocOptions_Silent, errors)

        # 导出PDF
        print("  导出PDF...")

        # 尝试使用ExportPdfData方法
        pdf_data = sw_model.Extension.GetPdfExportData()
        if pdf_data:
            ret = sw_model.Extension.ExportPdf(pdf_data, pdf_path)
            if ret:
                print(f"  ✓ PDF已导出")
                stats['success'] += 1
            else:
                print(f"  ⚠️  ExportPdf返回False，尝试SaveAs...")
                # 回退到SaveAs
                sw_model.Extension.SaveAs(pdf_path, 0, 0, None, 0, 0)
                if os.path.exists(pdf_path):
                    print(f"  ✓ PDF已导出（SaveAs）")
                    stats['success'] += 1
                else:
                    print(f"  ❌ PDF导出失败")
                    stats['failed'] += 1
        else:
            print(f"  ⚠️  无法获取PDF导出数据，尝试SaveAs...")
            sw_model.Extension.SaveAs(pdf_path, 0, 0, None, 0, 0)
            if os.path.exists(pdf_path):
                print(f"  ✓ PDF已导出（SaveAs）")
                stats['success'] += 1
            else:
                print(f"  ❌ PDF导出失败")
                stats['failed'] += 1

        # 关闭文件
        sw.CloseDoc(drw_path)
        time.sleep(0.5)

    except Exception as e:
        print(f"  ❌ 错误: {e}")
        stats['failed'] += 1
        try:
            sw.CloseDoc(drw_path)
        except:
            pass

print(f"\n\n{'='*70}")
print("  完成")
print('='*70)
print(f"\n总文件数:  {stats['total']}")
print(f"成功导出:  {stats['success']} ✓")
print(f"失败:      {stats['failed']} ❌\n")
