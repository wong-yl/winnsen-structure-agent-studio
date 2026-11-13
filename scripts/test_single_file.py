#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""单独测试第2个文件：箱体竖隔板R"""
import sys, io, os, win32com.client, pythoncom
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

OUTPUT_DIR = r"C:\Users\Administrator\Desktop\delivery_remaining_4_R5"
os.makedirs(OUTPUT_DIR, exist_ok=True)

drw = r"D:\Winnsen_Structure_Agent_Studio\data\production_orders_20261029\continuation_sol_20261029_r5\drawing_remaining_prepare_r83\runs\run_entry2_r1\02_sheet_箱体竖隔板R\箱体竖隔板R_R83_UNRELEASED.SLDDRW"
pdf_path = os.path.join(OUTPUT_DIR, "箱体竖隔板R_R5.pdf")

pythoncom.CoInitialize()
sw = win32com.client.Dispatch("SldWorks.Application")
sw.Visible = True

print(f"Opening: {os.path.basename(drw)}")
model = sw.OpenDoc(drw, 3)
if model:
    print("Opened OK")
    print("Saving as PDF...")
    model.SaveAs(pdf_path)
    sw.CloseDoc(drw)
    if os.path.exists(pdf_path):
        print(f"OK: {os.path.getsize(pdf_path)/1024:.0f} KB")
    else:
        print("FAIL: no output")
else:
    print("FAIL: cannot open")
