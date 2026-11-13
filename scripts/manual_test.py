#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""只打开文件，不导出，看看能否手动操作"""
import sys, io, win32com.client, pythoncom
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

drw = r"D:\Winnsen_Structure_Agent_Studio\data\production_orders_20261029\continuation_sol_20261029_r5\drawing_batch_prepare_r51\runs\run_r1\02_sheet_箱体竖隔板R\箱体竖隔板R_R51.SLDDRW"

pythoncom.CoInitialize()
sw = win32com.client.Dispatch("SldWorks.Application")
sw.Visible = True

print(f"Opening: {drw}")
model = sw.OpenDoc(drw, 3)

if model:
    print("Opened successfully!")
    print("SolidWorks window is visible - please manually try File > Save As > PDF")
    print("Then press Enter here...")
    input()
    sw.CloseDoc(drw)
else:
    print("Failed to open")
