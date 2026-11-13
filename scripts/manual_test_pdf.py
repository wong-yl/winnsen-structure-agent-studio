#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
手动测试：打开第一个文件，然后手动在SW里导出PDF
"""

import sys
import io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

import os
import win32com.client
import pythoncom

# 配置
BACKUP_DIR = r"D:\Winnsen_Structure_Agent_Studio\data\production_orders_20261029\continuation_sol_20261029_r5\drawing_batch_prepare_r59\backup_r59_original"

print("手动测试")
print("="*70)

# 初始化SolidWorks
print("\n连接SolidWorks...")
pythoncom.CoInitialize()
sw = win32com.client.Dispatch("SldWorks.Application")
sw.Visible = True

# 打开第一个文件
files = sorted([f for f in os.listdir(BACKUP_DIR) if f.upper().endswith('_R59.SLDDRW')])
first_file = files[0]
drw_path = os.path.join(BACKUP_DIR, first_file)

print(f"\n打开: {first_file}")
sw_model = sw.OpenDoc(drw_path, 3)

if sw_model:
    print("✓ 文件已打开")
    print("\n现在请在SolidWorks里手动尝试：")
    print("  File -> Save As -> PDF")
    print(f"\n保存为: C:\\Users\\Administrator\\Desktop\\delivery_r59_parts_RELEASED\\{first_file.replace('.SLDDRW', '.pdf')}")
    print("\n按任意键关闭文件...")
    input()

    sw.CloseDoc(drw_path)
    print("文件已关闭")
else:
    print("❌ 无法打开文件")
