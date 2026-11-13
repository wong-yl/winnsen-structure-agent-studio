#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""导出其他零件的PDF"""

import sys, io, os
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
import win32com.client, pythoncom

BASE = r"D:\Winnsen_Structure_Agent_Studio\data\production_orders_20261029\continuation_sol_20261029_r5"
OUT = r"C:\Users\Administrator\Desktop\delivery_other_parts_R5"

FILES = [
 (r"\drawing_layout_repair_r52\runs\run_r1\01_sheet_箱体横层板L\箱体横层板L_R52.SLDDRW", "箱体横层板L_R5.pdf"),
 (r"\drawing_batch_prepare_r51\runs\run_r1\01_sheet_箱体横层板R\箱体横层板R_R51.SLDDRW", "箱体横层板R_R5.pdf"),
 (r"\drawing_right_side_layout_r58\runs\run_r1\01_sheet_箱体右侧板\箱体右侧板_R58.SLDDRW", "箱体右侧板_R5.pdf"),
 (r"\drawing_flat_page_r50\runs\run_r7\partition_left_with_flat_R50_UNRELEASED.SLDDRW", "箱体竖隔板L_R5.pdf"),
 (r"\drawing_batch_prepare_r55\runs\run_r1\06_reinforcement_箱体侧板加强筋2\箱体侧板加强筋2_R55.SLDDRW", "箱体侧板加强筋2_R5.pdf"),
 (r"\drawing_layout_repair_r52\runs\run_r1\03_reinforcement_箱体横层板加强筋\箱体横层板加强筋_R52.SLDDRW", "箱体横层板加强筋_R5.pdf"),
 (r"\drawing_layout_repair_r52\runs\run_r1\02_cover_上盖壳体后侧板\上盖壳体后侧板_R52.SLDDRW", "上盖壳体后侧板_R5.pdf"),
 (r"\drawing_batch_prepare_r55\runs\run_r1\03_cover_上盖壳体前侧板\上盖壳体前侧板_R55.SLDDRW", "上盖壳体前侧板_R5.pdf"),
 (r"\drawing_batch_prepare_r55\runs\run_r1\05_cover_上盖壳体右侧板\上盖壳体右侧板_R55.SLDDRW", "上盖壳体右侧板_R5.pdf"),
 (r"\drawing_batch_prepare_r55\runs\run_r1\04_cover_上盖壳体左侧板\上盖壳体左侧板_R55.SLDDRW", "上盖壳体左侧板_R5.pdf"),
 (r"\drawing_two_layout_repairs_r64\runs\run_r4\01_base_底座加强筋\底座加强筋_R64.SLDDRW", "底座加强筋_R5.pdf"),
 (r"\drawing_batch_prepare_r55\runs\run_r1\02_base_底座外框\底座外框_R55.SLDDRW", "底座外框_R5.pdf"),
]

os.makedirs(OUT, exist_ok=True)
pythoncom.CoInitialize()
sw = win32com.client.Dispatch("SldWorks.Application")
sw.Visible = True
print("SolidWorks connected")

ok = 0
fail = 0
for idx, (rel, pdfname) in enumerate(FILES, 1):
    drw = BASE + rel
    pdf = os.path.join(OUT, pdfname)
    print(f"[{idx}/12] {pdfname}")
    if not os.path.exists(drw):
        print("  missing")
        fail += 1
        continue
    try:
        m = sw.OpenDoc(drw, 3)
        if not m:
            print("  open failed")
            fail += 1
            continue
        m.SaveAs(pdf)
        sw.CloseDoc(drw)
        if os.path.exists(pdf):
            print("  OK", round(os.path.getsize(pdf)/1024), "KB")
            ok += 1
        else:
            print("  no pdf")
            fail += 1
    except Exception as e:
        print("  error:", e)
        fail += 1

print(f"\nDONE ok={ok} fail={fail}")
print(OUT)
