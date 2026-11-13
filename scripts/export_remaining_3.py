#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""用备用版本和跳过问题文件"""
import sys, io, os, win32com.client, pythoncom
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

OUTPUT_DIR = r"C:\Users\Administrator\Desktop\delivery_remaining_4_R5"
os.makedirs(OUTPUT_DIR, exist_ok=True)

# 用R51版本的箱体竖隔板R，跳过R83的
FILES = [
    (r"D:\Winnsen_Structure_Agent_Studio\data\production_orders_20261029\continuation_sol_20261029_r5\drawing_batch_prepare_r51\runs\run_r1\02_sheet_箱体竖隔板R\箱体竖隔板R_R51.SLDDRW", "箱体竖隔板R_R5.pdf"),
    (r"D:\Winnsen_Structure_Agent_Studio\data\production_orders_20261029\continuation_sol_20261029_r5\drawing_remaining_prepare_r83\runs\run_entry3_r1\03_reinforcement_箱体侧板加强筋1\箱体侧板加强筋1_R83_UNRELEASED.SLDDRW", "箱体侧板加强筋1_R5.pdf"),
    (r"D:\Winnsen_Structure_Agent_Studio\data\production_orders_20261029\continuation_sol_20261029_r5\drawing_remaining_prepare_r83\runs\run_entry4_r1\04_cover_上盖壳体底板\上盖壳体底板_R83_UNRELEASED.SLDDRW", "上盖壳体底板_R5.pdf"),
]

pythoncom.CoInitialize()
sw = win32com.client.Dispatch("SldWorks.Application")
sw.Visible = True
print("SolidWorks OK\n")

ok = 0
for idx, (drw, pdf_name) in enumerate(FILES, 1):
    pdf_path = os.path.join(OUTPUT_DIR, pdf_name)
    print(f"[{idx}/{len(FILES)}] {pdf_name}")

    # 如果已存在就跳过
    if os.path.exists(pdf_path):
        print(f"  SKIP (exists)")
        ok += 1
        continue

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
                print("  FAIL no PDF")
        else:
            print("  FAIL open")
    except Exception as e:
        print(f"  ERROR {e}")
        try: sw.CloseDoc(drw)
        except: pass

print(f"\nDONE ok={ok} fail={len(FILES)-ok}")
print(OUTPUT_DIR)
