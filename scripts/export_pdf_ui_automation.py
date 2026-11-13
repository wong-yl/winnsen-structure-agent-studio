#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
使用UI自动化导出PDF
"""

import sys
import io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

import os
import time
import win32com.client
import pythoncom
import pyautogui
import keyboard

# 配置
BACKUP_DIR = r"D:\Winnsen_Structure_Agent_Studio\data\production_orders_20261029\continuation_sol_20261029_r5\drawing_batch_prepare_r59\backup_r59_original"
OUTPUT_DIR = r"C:\Users\Administrator\Desktop\delivery_r59_parts_RELEASED"

# 统计
stats = {'total': 0, 'success': 0, 'failed': 0}

print("="*70)
print("  使用UI自动化导出 PDF")
print("="*70)
print("\n⚠️  请不要移动鼠标或按键盘！\n")

# 初始化SolidWorks
print("[初始化] 连接到SolidWorks...")
pythoncom.CoInitialize()
sw = win32com.client.Dispatch("SldWorks.Application")
sw.Visible = True
time.sleep(2)
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
        print("  [1/4] 打开文件...")
        sw_model = sw.OpenDoc(drw_path, 3)

        if not sw_model:
            print("  ❌ 打开失败")
            stats['failed'] += 1
            continue

        print("  ✓ 已打开")
        time.sleep(1)

        # 使用键盘快捷键：File -> Save As
        print("  [2/4] 打开另存为对话框...")
        keyboard.press_and_release('ctrl+shift+s')  # Save As快捷键
        time.sleep(2)

        # 输入文件名
        print("  [3/4] 输入文件名...")
        keyboard.write(pdf_path, delay=0.05)
        time.sleep(0.5)

        # Tab到文件类型下拉框，选择PDF
        print("  [4/4] 选择PDF格式...")
        keyboard.press_and_release('tab')  # 移动到"保存类型"
        time.sleep(0.3)
        keyboard.write('pdf', delay=0.1)  # 输入pdf会筛选到PDF选项
        time.sleep(0.5)

        # 按Enter保存
        keyboard.press_and_release('enter')
        time.sleep(2)

        # 如果有选项对话框，再按Enter确认
        keyboard.press_and_release('enter')
        time.sleep(1)

        # 关闭文件
        sw.CloseDoc(drw_path)

        # 检查是否成功
        if os.path.exists(pdf_path):
            file_size = os.path.getsize(pdf_path) / 1024
            print(f"  ✓ {pdf_filename} ({file_size:.0f} KB)")
            stats['success'] += 1
        else:
            print(f"  ⚠️  PDF文件未找到")
            stats['failed'] += 1

    except Exception as e:
        print(f"  ❌ 错误: {e}")
        stats['failed'] += 1
        try:
            sw.CloseDoc(drw_path)
        except:
            pass

    # 每个文件后休息一下
    time.sleep(1)

print(f"\n\n{'='*70}")
print("  完成")
print('='*70)
print(f"\n总文件数:  {stats['total']}")
print(f"成功导出:  {stats['success']} ✓")
print(f"失败:      {stats['failed']} ❌\n")
