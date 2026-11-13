#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
简单测试：只处理第一个R59文件
"""

import sys
import io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

import os
import time
import win32com.client
import pythoncom

# 测试文件（从备份目录读取）
TEST_FILE = r"D:\Winnsen_Structure_Agent_Studio\data\production_orders_20261029\continuation_sol_20261029_r5\drawing_batch_prepare_r59\backup_r59_original\门框 横隔板_R59.SLDDRW"

print("="*70)
print("  R59 → R5 RELEASED 测试脚本")
print("="*70)
print(f"\n测试文件: {os.path.basename(TEST_FILE)}")

# 初始化
pythoncom.CoInitialize()
sw = win32com.client.Dispatch("SldWorks.Application")
sw.Visible = True
print("✓ SolidWorks已连接")

# 打开文件
print("\n打开文件...")
sw_model = sw.OpenDoc(TEST_FILE, 3)  # 3 = swDocDRAWING

if not sw_model:
    print("❌ 无法打开文件")
    sys.exit(1)

print("✓ 文件已打开")

# 获取自定义属性
print("\n读取当前属性...")
custom_props = sw_model.Extension.CustomPropertyManager("")

prop_names = ['designer', 'drafter', 'checker', 'date', 'revision']
for name in prop_names:
    try:
        ret_val, value, resolved = '', '', ''
        ret = custom_props.Get3(name, False, ret_val, resolved)
        print(f"  {name}: {resolved if ret == 0 else 'N/A'}")
    except:
        print(f"  {name}: 读取失败")

# 更新属性
print("\n更新属性...")
updates = {
    'designer': 'Eli',
    'drafter': 'Eli',
    'checker': 'Eli',
    'reviewer': 'Eli',
    'approver': 'Eli',
    'date': '2026-11-02',
    'revision': 'R5'
}

for name, value in updates.items():
    custom_props.Set(name, value)
    print(f"  ✓ {name} = {value}")

# 保存（不带参数，最简单）
print("\n保存文件...")
try:
    result = sw_model.Save()
    print(f"Save() 返回: {result}")
except Exception as e:
    print(f"❌ Save() 失败: {e}")

# 关闭
print("\n关闭文件...")
sw.CloseDoc(TEST_FILE)

print("\n✓ 测试完成")
