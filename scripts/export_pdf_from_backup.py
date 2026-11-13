#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
从备份目录导出PDF（这些文件可以打开）
"""

import sys
import io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

import os
import shutil
import win32com.client
import pythoncom

# 配置
BACKUP_DIR = r"D:\Winnsen_Structure_Agent_Studio\data\production_orders_20261029\continuation_sol_20261029_r5\drawing_batch_prepare_r59\backup_r59_original"
OUTPUT_DIR = r"C:\Users\Administrator\Desktop\delivery_r59_parts_RELEASED"

# 统计
stats = {'total': 0, 'success': 0, 'failed': 0}

print("="*70)
print("  从备份目录导出 PDF")
print("="*70)

# 初始化SolidWorks
print("\n[初始化] 连接到SolidWorks...")
pythoncom.CoInitialize()
sw = win32com.client.Dispatch("SldWorks.Application")
sw.Visible = True
print("✓ SolidWorks已连接\n")

# 获取所有SLDDRW文件
files = [f for f in os.listdir(BACKUP_DIR) if f.upper().endswith('_R59.SLDDRW')]
stats['total'] = len(files)

print(f"✓ 找到 {len(files)} 个图纸文件\n")
print("="*70)

for idx, filename in enumerate(sorted(files), 1):
    print(f"\n[{idx}/{len(files)}] {filename}")

    drw_path = os.path.join(BACKUP_DIR, filename)
    # PDF输出文件名改为R5_RELEASED
    pdf_filename = filename.replace('_R59.SLDDRW', '_R5_RELEASED.pdf')
    pdf_path = os.path.join(OUTPUT_DIR, pdf_filename)

    try:
        # 打开文件
        print("  打开中...")
        sw_model = sw.OpenDoc(drw_path, 3)  # 3 = swDocDRAWING

        if not sw_model:
            print("  ❌ 打开失败")
            stats['failed'] += 1
            continue

        print("  ✓ 已打开")

        # 导出PDF
        print("  导出PDF...")
        sw_model.Extension.SaveAs(pdf_path, 0, 0, None, 0, 0)

        # 关闭文件
        sw.CloseDoc(drw_path)

        # 检查是否成功
        if os.path.exists(pdf_path):
            file_size = os.path.getsize(pdf_path) / 1024  # KB
            print(f"  ✓ {pdf_filename} ({file_size:.0f} KB)")
            stats['success'] += 1
        else:
            print(f"  ❌ PDF文件未生成")
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

if stats['success'] > 0:
    print(f"PDF文件保存在: {OUTPUT_DIR}\n")
