#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""用新方法：强制设置PDF导出选项"""
import sys, io, os, win32com.client, pythoncom
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

OUTPUT_DIR = r"C:\Users\Administrator\Desktop\delivery_remaining_4_R5"
os.makedirs(OUTPUT_DIR, exist_ok=True)

FILES = [
    (r"D:\Winnsen_Structure_Agent_Studio\data\production_orders_20261029\continuation_sol_20261029_r5\drawing_batch_prepare_r51\runs\run_r1\02_sheet_箱体竖隔板R\箱体竖隔板R_R51.SLDDRW", "箱体竖隔板R_R5.pdf"),
    (r"D:\Winnsen_Structure_Agent_Studio\data\production_orders_20261029\continuation_sol_20261029_r5\drawing_remaining_prepare_r83\runs\run_entry3_r1\03_reinforcement_箱体侧板加强筋1\箱体侧板加强筋1_R83_UNRELEASED.SLDDRW", "箱体侧板加强筋1_R5.pdf"),
    (r"D:\Winnsen_Structure_Agent_Studio\data\production_orders_20261029\continuation_sol_20261029_r5\drawing_remaining_prepare_r83\runs\run_entry4_r1\04_cover_上盖壳体底板\上盖壳体底板_R83_UNRELEASED.SLDDRW", "上盖壳体底板_R5.pdf"),
]

pythoncom.CoInitialize()
sw = win32com.client.Dispatch("SldWorks.Application")
sw.Visible = True

# 先尝试直接用之前成功的文件，看看是不是方法问题
test_file = r"D:\Winnsen_Structure_Agent_Studio\data\production_orders_20261029\continuation_sol_20261029_r5\drawing_remaining_prepare_r77\runs\run_r2\01_sheet_箱体左侧板\箱体左侧板_R77.SLDDRW"
test_pdf = os.path.join(OUTPUT_DIR, "test_method.pdf")

print("Testing with known-good file...")
model = sw.OpenDoc(test_file, 3)
if model:
    print("  Opened OK")
    # 尝试method 1
    result = model.SaveAs(test_pdf)
    print(f"  SaveAs returned: {result}")
    sw.CloseDoc(test_file)
    if os.path.exists(test_pdf):
        print(f"  Method works! {os.path.getsize(test_pdf)/1024:.0f} KB")
        os.remove(test_pdf)
    else:
        print("  Method failed!")
else:
    print("  Cannot open test file")

print("\nNow trying the 3 problem files...\n")

ok = 0
for idx, (drw, pdf_name) in enumerate(FILES, 1):
    pdf_path = os.path.join(OUTPUT_DIR, pdf_name)
    print(f"[{idx}/{len(FILES)}] {pdf_name}")

    if os.path.exists(pdf_path):
        print(f"  SKIP (exists)")
        ok += 1
        continue

    try:
        print(f"  Opening: {os.path.basename(drw)}")
        model = sw.OpenDoc(drw, 3)

        if not model:
            print("  FAIL: model is None")
            continue

        print("  Opened OK")

        # 尝试获取模型信息
        try:
            title = model.GetTitle()
            print(f"  Title: {title}")
        except:
            print("  (cannot get title)")

        print("  Calling SaveAs...")
        result = model.SaveAs(pdf_path)
        print(f"  SaveAs returned: {result}")

        sw.CloseDoc(drw)

        if os.path.exists(pdf_path):
            size = os.path.getsize(pdf_path) / 1024
            print(f"  SUCCESS {size:.0f} KB")
            ok += 1
        else:
            print("  FAIL: no output file")

    except Exception as e:
        print(f"  ERROR: {e}")
        import traceback
        traceback.print_exc()
        try: sw.CloseDoc(drw)
        except: pass

print(f"\nDONE ok={ok} fail={len(FILES)-ok}")
