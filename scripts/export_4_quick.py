#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""快速导出剩余4个PDF"""
import sys, io, os, win32com.client, pythoncom
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

OUTPUT_DIR = r"C:\Users\Administrator\Desktop\delivery_remaining_4_R5"
os.makedirs(OUTPUT_DIR, exist_ok=True)

FILES = [
    (r"D:\Winnsen_Structure_Agent_Studio\data\production_orders_20261029\continuation_sol_20261029_r5\drawing_remaining_prepare_r77\runs\run_r2\01_sheet_箱体左侧板\箱体左侧板_R77.SLDDRW", "箱体左侧板_R5.pdf"),
    (r"D:\Winnsen_Structure_Agent_Studio\data\production_orders_20261029\continuation_sol_20261029_r5\drawing_remaining_prepare_r83\runs\run_entry2_r1\02_sheet_箱体竖隔板R\箱体竖隔板R_R83_UNRELEASED.SLDDRW", "箱体竖隔板R_R5.pdf"),
    (r"D:\Winnsen_Structure_Agent_Studio\data\production_orders_20261029\continuation_sol_20261029_r5\drawing_remaining_prepare_r83\runs\run_entry3_r1\03_reinforcement_箱体侧板加强筋1\箱体侧板加强筋1_R83_UNRELEASED.SLDDRW", "箱体侧板加强筋1_R5.pdf"),
    (r"D:\Winnsen_Structure_Agent_Studio\data\production_orders_20261029\continuation_sol_20261029_r5\drawing_remaining_prepare_r83\runs\run_entry4_r1\04_cover_上盖壳体底板\上盖壳体底板_R83_UNRELEASED.SLDDRW", "上盖壳体底板_R5.pdf"),
]

pythoncom.CoInitialize()
sw = win32com.client.Dispatch("SldWorks.Application")
sw.Visible = True
print("SolidWorks connected")

ok = 0
for idx, (drw, pdf_name) in enumerate(FILES, 1):
    pdf_path = os.path.join(OUTPUT_DIR, pdf_name)
    print(f"[{idx}/{len(FILES)}] {pdf_name}")
    try:
        model = sw.OpenDoc(drw, 3)
        if model:
            model.SaveAs(pdf_path)
            sw.CloseDoc(drw)
            if os.path.exists(pdf_path):
                size = os.path.getsize(pdf_path) / 1024
                print(f"  OK {size:.0f} KB")
                ok += 1
            else:
                print("  FAIL no output")
        else:
            print("  FAIL open")
    except Exception as e:
        print(f"  ERROR {e}")
        try: sw.CloseDoc(drw)
        except: pass

print(f"\nDONE ok={ok} fail={len(FILES)-ok}")
print(OUTPUT_DIR)
