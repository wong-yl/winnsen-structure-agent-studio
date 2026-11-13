#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
批量导出PDF
"""

import sys
import io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

import os
import time
import win32com.client
import pythoncom

# 配置
INPUT_DIR = r"C:\Users\Administrator\Desktop\delivery_r59_parts_RELEASED"

# 统计
stats = {'total': 0, 'success': 0, 'failed': 0}

print("="*70)
print("  批量导出 PDF")
print("="*70)

# 初始化SolidWorks
print("\n[初始化] 连接到SolidWorks...")
pythoncom.CoInitialize()
sw = win32com.client.Dispatch("SldWorks.Application")
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
    pdf_path = os.path.join(INPUT_DIR, filename.replace('.SLDDRW', '.pdf'))

    try:
        # 打开文件
        print("  打开中...")
        sw_model = sw.OpenDoc(drw_path, 3)  # 3 = swDocDRAWING

        if not sw_model:
            print("  ❌ 打开失败")
            stats['failed'] += 1
            continue

        # 导出PDF
        print("  导出PDF...")
        sw_model.Extension.SaveAs(pdf_path, 0, 0, None, 0, 0)

        # 关闭文件
        sw.CloseDoc(drw_path)

        # 检查是否成功
        if os.path.exists(pdf_path):
            print(f"  ✓ {filename.replace('.SLDDRW', '.pdf')}")
            stats['success'] += 1
        else:
            print("  ⚠️  PDF文件未生成")
            stats['failed'] += 1

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
