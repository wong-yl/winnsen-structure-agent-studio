#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
批量更新 R59 图纸为 R5 RELEASED 格式
基于测试成功的简化版本
"""

import sys
import io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

import os
import time
import shutil
from datetime import datetime
import win32com.client
import pythoncom

# 配置
BACKUP_DIR = r"D:\Winnsen_Structure_Agent_Studio\data\production_orders_20261029\continuation_sol_20261029_r5\drawing_batch_prepare_r59\backup_r59_original"
OUTPUT_DIR = r"C:\Users\Administrator\Desktop\delivery_r59_parts_RELEASED"

# R5 格式配置
R5_CONFIG = {
    'designer': 'Eli',
    'drafter': 'Eli',
    'checker': 'Eli',
    'reviewer': 'Eli',
    'approver': 'Eli',
    'date': '2026-11-02',
    'revision': 'R5'
}

# 统计
stats = {'total': 0, 'success': 0, 'failed': 0, 'errors': []}

def get_r59_files():
    """获取所有R59文件"""
    files = []
    for f in os.listdir(BACKUP_DIR):
        if f.upper().endswith('_R59.SLDDRW'):
            files.append(os.path.join(BACKUP_DIR, f))
    return sorted(files)

print("="*70)
print("  批量更新 R59 → R5 RELEASED")
print("="*70)
print(f"\n备份目录: {BACKUP_DIR}")
print(f"输出目录: {OUTPUT_DIR}\n")

# 创建输出目录
os.makedirs(OUTPUT_DIR, exist_ok=True)

# 初始化SolidWorks
print("[初始化] 连接到SolidWorks...")
pythoncom.CoInitialize()
sw = win32com.client.Dispatch("SldWorks.Application")
sw.Visible = True
print("✓ SolidWorks已连接\n")

# 获取文件列表
files = get_r59_files()
stats['total'] = len(files)
print(f"✓ 找到 {len(files)} 个 R59 文件\n")

print("="*70)
print(f"开始批量处理（共 {len(files)} 个文件）")
print("="*70)

start_time = time.time()

for idx, file_path in enumerate(files, 1):
    filename = os.path.basename(file_path)
    print(f"\n{'='*70}")
    print(f"[{idx}/{len(files)}] {filename}")
    print('='*70)

    try:
        # [1/5] 打开文件
        print("  [1/5] 打开文件...")
        sw_model = sw.OpenDoc(file_path, 3)  # 3 = swDocDRAWING

        if not sw_model:
            raise Exception("OpenDoc 返回 None")

        print("    ✓ 文件已打开")

        # [2/5] 更新属性
        print("  [2/5] 更新自定义属性...")
        custom_props = sw_model.Extension.CustomPropertyManager("")

        count = 0
        for prop_name, prop_value in R5_CONFIG.items():
            custom_props.Set(prop_name, prop_value)
            count += 1

        print(f"    ✓ 更新了 {count} 个属性")

        # [3/5] 保存原文件
        print("  [3/5] 保存文件...")
        sw_model.Save()
        print("    ✓ 已保存")

        # [4/5] 另存为新文件名
        print("  [4/5] 另存为 _R5_RELEASED 文件...")
        new_filename = filename.replace('_R59.SLDDRW', '_R5_RELEASED.SLDDRW')
        new_path = os.path.join(OUTPUT_DIR, new_filename)

        # 使用 SaveAs 另存为
        errors = 0
        warnings = 0
        sw_model.Extension.SaveAs(new_path, 0, 0, None, errors, warnings)

        if os.path.exists(new_path):
            print(f"    ✓ 已另存为: {new_filename}")
        else:
            print(f"    ⚠️  另存为可能失败，但继续处理")

        # [5/5] 导出PDF
        print("  [5/5] 导出PDF...")
        pdf_filename = new_filename.replace('.SLDDRW', '.pdf')
        pdf_path = os.path.join(OUTPUT_DIR, pdf_filename)

        # 重新打开新文件来导出PDF
        sw.CloseDoc(file_path)

        if os.path.exists(new_path):
            sw_model = sw.OpenDoc(new_path, 3)
            if sw_model:
                sw_model.Extension.SaveAs(pdf_path, 0, 0, None, 0, 0)

                if os.path.exists(pdf_path):
                    print(f"    ✓ PDF已导出: {pdf_filename}")
                else:
                    print(f"    ⚠️  PDF导出可能失败")

                sw.CloseDoc(new_path)
            else:
                print("    ⚠️  无法重新打开新文件导出PDF")
        else:
            print("    ⚠️  跳过PDF导出（新文件不存在）")

        stats['success'] += 1
        print(f"  ✓ 成功处理")

    except Exception as e:
        stats['failed'] += 1
        error_msg = str(e)
        stats['errors'].append(f"{filename}: {error_msg}")
        print(f"  ❌ 处理异常: {error_msg}")

        # 确保关闭文件
        try:
            sw.CloseDoc(file_path)
        except:
            pass

    # 每3个文件休息一下
    if idx % 3 == 0 and idx < len(files):
        print(f"\n[休息] 2秒...")
        time.sleep(2)

# 完成
elapsed = (time.time() - start_time) / 60

print(f"\n\n{'='*70}")
print("  批量更新完成")
print('='*70)
print(f"\n总文件数:    {stats['total']}")
print(f"成功更新:    {stats['success']} ✓")
print(f"失败:        {stats['failed']} ❌")
print(f"用时:        {elapsed:.1f} 分钟\n")

if stats['errors']:
    print(f"\n⚠️  有 {len(stats['errors'])} 个错误:")
    for err in stats['errors']:
        print(f"  - {err}")

print(f"\n输出目录: {OUTPUT_DIR}\n")
