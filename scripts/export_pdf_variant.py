#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
使用win32com.client.VARIANT包装参数
"""

import sys
import io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

import os
import win32com.client
from win32com.client import VARIANT
import pythoncom
import pywintypes

# 配置
BACKUP_DIR = r"D:\Winnsen_Structure_Agent_Studio\data\production_orders_20261029\continuation_sol_20261029_r5\drawing_batch_prepare_r59\backup_r59_original"
OUTPUT_DIR = r"C:\Users\Administrator\Desktop\delivery_r59_parts_RELEASED"

# 统计
stats = {'total': 0, 'success': 0, 'failed': 0}

print("="*70)
print("  使用VARIANT包装参数导出PDF")
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
    pdf_filename = filename.replace('_R59.SLDDRW', '_R5_RELEASED.pdf')
    pdf_path = os.path.join(OUTPUT_DIR, pdf_filename)

    try:
        # 打开文件
        print("  打开中...")
        sw_model = sw.OpenDoc(drw_path, 3)  # 简单方法

        if not sw_model:
            print("  ❌ 打开失败")
            stats['failed'] += 1
            continue

        print("  ✓ 已打开")

        # 导出PDF - 使用简单的SaveAs方法
        print("  导出PDF...")
        pdf_created = False

        try:
            sw_model.SaveAs(pdf_path)

            if os.path.exists(pdf_path):
                pdf_created = True
        except Exception as e:
            print(f"  ❌ 导出失败: {e}")

        # 关闭文件
        sw.CloseDoc(drw_path)

        # 检查结果
        if pdf_created:
            file_size = os.path.getsize(pdf_path) / 1024
            print(f"\n  ✓✓ {pdf_filename} ({file_size:.0f} KB)")
            stats['success'] += 1
        else:
            print(f"\n  ❌ 所有方法都失败")
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
