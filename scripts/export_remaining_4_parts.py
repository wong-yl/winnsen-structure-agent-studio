#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
导出剩余4个零件的PDF
箱体左侧板、箱体竖隔板R、箱体侧板加强筋1、上盖壳体底板
"""

import sys
import io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

import os
import win32com.client
import pythoncom

# 输出目录
OUTPUT_DIR = r"C:\Users\Administrator\Desktop\delivery_remaining_4_R5"

# 需要处理的文件列表
FILES = [
    (r"D:\Winnsen_Structure_Agent_Studio\data\production_orders_20261029\continuation_sol_20261029_r5\drawing_remaining_prepare_r77\runs\run_r2\01_sheet_箱体左侧板\箱体左侧板_R77.SLDDRW", "箱体左侧板_R5.pdf"),
    (r"D:\Winnsen_Structure_Agent_Studio\data\production_orders_20261029\continuation_sol_20261029_r5\drawing_remaining_prepare_r83\runs\run_entry2_r1\02_sheet_箱体竖隔板R\箱体竖隔板R_R83_UNRELEASED.SLDDRW", "箱体竖隔板R_R5.pdf"),
    (r"D:\Winnsen_Structure_Agent_Studio\data\production_orders_20261029\continuation_sol_20261029_r5\drawing_remaining_prepare_r83\runs\run_entry3_r1\03_reinforcement_箱体侧板加强筋1\箱体侧板加强筋1_R83_UNRELEASED.SLDDRW", "箱体侧板加强筋1_R5.pdf"),
    (r"D:\Winnsen_Structure_Agent_Studio\data\production_orders_20261029\continuation_sol_20261029_r5\drawing_remaining_prepare_r83\runs\run_entry4_r1\04_cover_上盖壳体底板\上盖壳体底板_R83_UNRELEASED.SLDDRW", "上盖壳体底板_R5.pdf"),
]

# 统计
stats = {'total': len(FILES), 'success': 0, 'failed': 0}

print("="*70)
print("  导出剩余4个零件的PDF")
print("="*70)
print(f"\n输出目录: {OUTPUT_DIR}\n")

# 创建输出目录
os.makedirs(OUTPUT_DIR, exist_ok=True)

# 初始化SolidWorks
print("[初始化] 连接到SolidWorks...")
pythoncom.CoInitialize()
sw = win32com.client.Dispatch("SldWorks.Application")
sw.Visible = True
print("✓ SolidWorks已连接\n")

print("="*70)

for idx, (drw_path, pdf_filename) in enumerate(FILES, 1):
    filename = os.path.basename(drw_path)
    print(f"\n[{idx}/{len(FILES)}] {filename}")

    pdf_path = os.path.join(OUTPUT_DIR, pdf_filename)

    # 检查文件是否存在
    if not os.path.exists(drw_path):
        print(f"  ❌ 文件不存在")
        stats['failed'] += 1
        continue

    try:
        # 打开文件
        print("  打开中...")
        sw_model = sw.OpenDoc(drw_path, 3)

        if not sw_model:
            print("  ❌ 打开失败")
            stats['failed'] += 1
            continue

        print("  ✓ 已打开")

        # 导出PDF
        print("  导出PDF...")
        sw_model.SaveAs(pdf_path)

        # 关闭文件
        sw.CloseDoc(drw_path)

        # 检查结果
        if os.path.exists(pdf_path):
            file_size = os.path.getsize(pdf_path) / 1024
            print(f"  ✓✓ {pdf_filename} ({file_size:.0f} KB)")
            stats['success'] += 1
        else:
            print(f"  ❌ PDF未生成")
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
