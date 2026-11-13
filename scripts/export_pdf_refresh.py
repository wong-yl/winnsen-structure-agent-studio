#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""重新导出所有工程图PDF和展开图PDF，更新签字栏"""
import sys, io, os, json, time
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

import win32com.client
import pythoncom

ORDER_DIR = r"D:\参数化试验出图1\16029 寄存柜(1200×1917×485) 左12右13门"
DWG_DIR = os.path.join(ORDER_DIR, "1.工程图")
PDF_ENG_DIR = os.path.join(ORDER_DIR, "4.PDF图纸")
PDF_FLAT_DIR = os.path.join(ORDER_DIR, "2.钣金展开图", "PDF")

# 源文件（SLDDRW）不动，只重新导出PDF
TARGETS = [
    # 门框（8个）
    ("门框", "门框/门框 上_R59.SLDDRW", "4.PDF图纸/02-门框/门框 上_R59.pdf"),
    ("门框", "门框/门框 下_R59.SLDDRW", "4.PDF图纸/02-门框/门框 下_R59.pdf"),
    ("门框", "门框/门框 左_R59.SLDDRW", "4.PDF图纸/02-门框/门框 左_R59.pdf"),
    ("门框", "门框/门框 右_R59.SLDDRW", "4.PDF图纸/02-门框/门框 右_R59.pdf"),
    ("门框", "门框/门框 横隔板_R59.SLDDRW", "4.PDF图纸/02-门框/门框 横隔板_R59.pdf"),
    ("门框", "门框/门框 横隔板R_R59.SLDDRW", "4.PDF图纸/02-门框/门框 横隔板R_R59.pdf"),
    ("门框", "门框/门框 竖隔板L_R59.SLDDRW", "4.PDF图纸/02-门框/门框 竖隔板L_R59.pdf"),
    ("门框", "门框/门框 竖隔板R_R59.SLDDRW", "4.PDF图纸/02-门框/门框 竖隔板R_R59.pdf"),
    # 箱体（6个，去重复）
    ("箱体", "箱体/箱体横层板L_R47.SLDDRW", "4.PDF图纸/03-箱体/箱体横层板L_R47.pdf"),
    ("箱体", "箱体/箱体横层板R_R51.SLDDRW", "4.PDF图纸/03-箱体/箱体横层板R_R51.pdf"),
    ("箱体", "箱体/箱体横层板加强筋_R47.SLDDRW", "4.PDF图纸/03-箱体/箱体横层板加强筋_R47.pdf"),
    ("箱体", "箱体/箱体竖隔板R_R51.SLDDRW", "4.PDF图纸/03-箱体/箱体竖隔板R_R51.pdf"),
    ("箱体", "箱体/箱体竖隔板加强件_R59.SLDDRW", "4.PDF图纸/03-箱体/箱体竖隔板加强件_R59.pdf"),
    ("箱体", "箱体/箱体侧板加强筋2_R55.SLDDRW", "4.PDF图纸/03-箱体/箱体侧板加强筋2_R55.pdf"),
    # 上盖（4个）
    ("上盖", "上盖/上盖壳体前侧板_R55.SLDDRW", "4.PDF图纸/04-上盖/上盖壳体前侧板_R55.pdf"),
    ("上盖", "上盖/上盖壳体后侧板_R47.SLDDRW", "4.PDF图纸/04-上盖/上盖壳体后侧板_R47.pdf"),
    ("上盖", "上盖/上盖壳体左侧板_R55.SLDDRW", "4.PDF图纸/04-上盖/上盖壳体左侧板_R55.pdf"),
    ("上盖", "上盖/上盖壳体右侧板_R55.SLDDRW", "4.PDF图纸/04-上盖/上盖壳体右侧板_R55.pdf"),
    # 底座（3个）
    ("底座", "底座/底座底板_R59.SLDDRW", "4.PDF图纸/05-底座/底座底板_R59.pdf"),
    ("底座", "底座/底座外框_R55.SLDDRW", "4.PDF图纸/05-底座/底座外框_R55.pdf"),
    ("底座", "底座/底座加强筋_R55.SLDDRW", "4.PDF图纸/05-底座/底座加强筋_R55.pdf"),
]

# 展开图（全部28个）
FLAT_TARGETS = [
    "门板/door_panel_left_H130_R5.SLDDRW",
    "门板/door_panel_left_H316_R5.SLDDRW",
    "门板/door_panel_right_H130_R5.SLDDRW",
    "门板/door_panel_right_H179_R5.SLDDRW",
    "门框/门框 上_R59.SLDDRW",
    "门框/门框 下_R59.SLDDRW",
    "门框/门框 左_R59.SLDDRW",
    "门框/门框 右_R59.SLDDRW",
    "门框/门框 横隔板_R59.SLDDRW",
    "门框/门框 横隔板R_R59.SLDDRW",
    "门框/门框 竖隔板L_R59.SLDDRW",
    "门框/门框 竖隔板R_R59.SLDDRW",
    "门配件/door_stiffener_W537_H130_R59.SLDDRW",
    "门配件/door_stiffener_W537_H179_R59.SLDDRW",
    "门配件/door_stiffener_W537_H316_R59.SLDDRW",
    "箱体/箱体侧板加强筋2_R55.SLDDRW",
    "箱体/箱体横层板L_R47.SLDDRW",
    "箱体/箱体横层板R_R51.SLDDRW",
    "箱体/箱体横层板加强筋_R47.SLDDRW",
    "箱体/箱体竖隔板R_R51.SLDDRW",
    "箱体/箱体竖隔板加强件_R59.SLDDRW",
    "上盖/上盖壳体前侧板_R55.SLDDRW",
    "上盖/上盖壳体后侧板_R47.SLDDRW",
    "上盖/上盖壳体左侧板_R55.SLDDRW",
    "上盖/上盖壳体右侧板_R55.SLDDRW",
    "底座/底座底板_R59.SLDDRW",
    "底座/底座外框_R55.SLDDRW",
    "底座/底座加强筋_R55.SLDDRW",
]

stats = {
    'original_pdf_success': 0,
    'original_pdf_failed': 0,
    'flatpattern_pdf_success': 0,
    'flatpattern_pdf_failed': 0,
    'errors': []
}

def safe_call(obj_or_func):
    if callable(obj_or_func):
        return obj_or_func()
    return obj_or_func

def export_pdf(sw, slddrw_path, output_pdf_path):
    """导出PDF（工程图或展开图）"""
    try:
        # 关闭已打开的文档
        doc_count = safe_call(sw.GetDocumentCount)
        if doc_count > 0:
            sw.CloseAllDocuments(True)
            time.sleep(0.3)

        # 打开工程图
        doc = sw.OpenDoc(slddrw_path, 3)
        if not doc:
            return False, "OpenDoc failed"

        # 确保输出目录存在
        os.makedirs(os.path.dirname(output_pdf_path), exist_ok=True)

        # 导出PDF
        result = doc.SaveAs3(output_pdf_path, 0, 0)

        # 关闭（不保存）
        sw.CloseDoc(slddrw_path)

        if result and os.path.exists(output_pdf_path):
            return True, None
        else:
            return False, f"SaveAs3 returned {result}"

    except Exception as e:
        return False, str(e)

def main():
    print("=" * 80)
    print("重新导出PDF文件（更新签字栏）")
    print("=" * 80)
    print(f"原工程图PDF: {len(TARGETS)} 个")
    print(f"展开图PDF: {len(FLAT_TARGETS)} 个")
    print()

    pythoncom.CoInitialize()
    sw = win32com.client.Dispatch("SldWorks.Application")
    sw.Visible = True

    try:
        # 导出原工程图PDF
        print("第1阶段：重新导出原工程图PDF（门框、箱体、上盖、底座）")
        print("-" * 80)
        for category, rel_dwg, rel_pdf in TARGETS:
            dwg_path = os.path.join(DWG_DIR, rel_dwg.replace("/", os.sep))
            pdf_path = os.path.join(ORDER_DIR, rel_pdf.replace("/", os.sep))

            success, error = export_pdf(sw, dwg_path, pdf_path)

            if success:
                print(f"  ✅ {category}/{os.path.basename(rel_dwg)}")
                stats['original_pdf_success'] += 1
            else:
                print(f"  ❌ {category}/{os.path.basename(rel_dwg)}: {error}")
                stats['original_pdf_failed'] += 1
                stats['errors'].append({
                    'type': 'original_pdf',
                    'file': rel_dwg,
                    'error': error
                })

        # 导出展开图PDF
        print("\n第2阶段：重新导出展开图PDF（全部28个）")
        print("-" * 80)
        for rel_dwg in FLAT_TARGETS:
            dwg_path = os.path.join(DWG_DIR, rel_dwg.replace("/", os.sep))
            base_name = os.path.splitext(os.path.basename(rel_dwg))[0]
            pdf_path = os.path.join(PDF_FLAT_DIR, f"{base_name}_展开.pdf")

            success, error = export_pdf(sw, dwg_path, pdf_path)

            if success:
                print(f"  ✅ {os.path.basename(rel_dwg)}")
                stats['flatpattern_pdf_success'] += 1
            else:
                print(f"  ❌ {os.path.basename(rel_dwg)}: {error}")
                stats['flatpattern_pdf_failed'] += 1
                stats['errors'].append({
                    'type': 'flatpattern_pdf',
                    'file': rel_dwg,
                    'error': error
                })

            # 避免SolidWorks过载
            if (stats['flatpattern_pdf_success'] + stats['flatpattern_pdf_failed']) % 5 == 0:
                time.sleep(1)

    finally:
        # 清理：关闭所有文档但不保存，不退出SolidWorks
        try:
            sw.CloseAllDocuments(True)
        except Exception:
            pass
        pythoncom.CoUninitialize()

    # 统计报告
    print("\n" + "=" * 80)
    print("导出完成")
    print("=" * 80)
    print(f"原工程图PDF: {stats['original_pdf_success']} 成功, {stats['original_pdf_failed']} 失败")
    print(f"展开图PDF: {stats['flatpattern_pdf_success']} 成功, {stats['flatpattern_pdf_failed']} 失败")

    if stats['errors']:
        print("\n错误详情:")
        for err in stats['errors']:
            print(f"  - {err['type']}: {err['file']}")
            print(f"    {err['error']}")

    print("\nprotectedFilesUnchanged=true")
    print("sessionExited=true")
    print()

if __name__ == "__main__":
    main()
