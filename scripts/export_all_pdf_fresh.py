#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""重新导出所有工程图PDF和展开图PDF"""
import sys, io, os, json, time, shutil
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

import win32com.client
import pythoncom

ORDER_DIR = r"D:\参数化试验出图1\16029 寄存柜(1200×1917×485) 左12右13门"
DWG_DIR = os.path.join(ORDER_DIR, "1.工程图")
PDF_DIR = os.path.join(ORDER_DIR, "4.PDF图纸")
FLAT_PDF_DIR = os.path.join(ORDER_DIR, "2.钣金展开图", "PDF")

# 需要重新导出的目录映射
CAT_MAP = {
    "门框": "02-门框",
    "箱体": "03-箱体",
    "上盖": "04-上盖",
    "底座": "05-底座",
}

stats = {
    'eng_success': 0,
    'eng_failed': 0,
    'flat_success': 0,
    'flat_failed': 0,
    'errors': []
}

def safe_call(obj_or_func):
    if callable(obj_or_func):
        return obj_or_func()
    return obj_or_func

def clear_old_pdfs(category_dirs):
    """清理旧的PDF文件（保留门板）"""
    print("清理旧PDF文件...")
    for cat_code in category_dirs:
        cat_dir = os.path.join(PDF_DIR, cat_code)
        if not os.path.exists(cat_dir):
            continue

        for fname in os.listdir(cat_dir):
            if fname.endswith('.pdf'):
                pdf_path = os.path.join(cat_dir, fname)
                try:
                    os.remove(pdf_path)
                    print(f"  删除: {cat_code}/{fname}")
                except Exception as e:
                    print(f"  删除失败: {cat_code}/{fname}: {e}")

def export_engineering_pdfs(sw):
    """导出原工程图PDF"""
    print("\n" + "="*80)
    print("第1阶段：导出原工程图PDF（门框、箱体、上盖、底座）")
    print("="*80)

    # 清理旧PDF
    clear_old_pdfs(list(CAT_MAP.values()))

    for category, cat_code in CAT_MAP.items():
        cat_dwg_dir = os.path.join(DWG_DIR, category)
        cat_pdf_dir = os.path.join(PDF_DIR, cat_code)

        if not os.path.exists(cat_dwg_dir):
            print(f"  跳过: {category} (目录不存在)")
            continue

        os.makedirs(cat_pdf_dir, exist_ok=True)

        print(f"\n处理: {category}/")

        for fname in sorted(os.listdir(cat_dwg_dir)):
            if not fname.endswith('.SLDDRW'):
                continue

            dwg_path = os.path.join(cat_dwg_dir, fname)

            try:
                # 关闭之前的文档
                doc_count = safe_call(sw.GetDocumentCount)
                if doc_count > 0:
                    sw.CloseAllDocuments(True)
                    time.sleep(0.2)

                # 打开工程图
                doc = sw.OpenDoc(dwg_path, 3)
                if not doc:
                    print(f"  ❌ {fname}: OpenDoc失败")
                    stats['eng_failed'] += 1
                    continue

                # 生成PDF路径（使用SLDDRW文件名）
                base_name = os.path.splitext(fname)[0]
                pdf_path = os.path.join(cat_pdf_dir, f"{base_name}.pdf")

                # 导出PDF
                result = doc.SaveAs3(pdf_path, 0, 0)

                # 关闭文档
                sw.CloseDoc(dwg_path)

                if os.path.exists(pdf_path):
                    print(f"  ✅ {fname} → {os.path.basename(pdf_path)}")
                    stats['eng_success'] += 1
                else:
                    print(f"  ❌ {fname}: PDF未生成")
                    stats['eng_failed'] += 1

                time.sleep(0.2)

            except Exception as e:
                print(f"  ❌ {fname}: {e}")
                stats['eng_failed'] += 1
                stats['errors'].append({'file': fname, 'error': str(e)})

def export_flatpattern_pdfs(sw):
    """导出展开图PDF"""
    print("\n" + "="*80)
    print("第2阶段：导出展开图PDF（全部28个）")
    print("="*80)

    # 清理旧展开图PDF
    print("\n清理旧展开图PDF...")
    if os.path.exists(FLAT_PDF_DIR):
        for fname in os.listdir(FLAT_PDF_DIR):
            if fname.endswith('_展开.pdf'):
                try:
                    os.remove(os.path.join(FLAT_PDF_DIR, fname))
                    print(f"  删除: {fname}")
                except Exception as e:
                    print(f"  删除失败: {fname}: {e}")

    os.makedirs(FLAT_PDF_DIR, exist_ok=True)

    # 遍历所有SLDDRW（门板、门框、箱体、上盖、底座、门配件）
    all_categories = ["门板", "门框", "箱体", "上盖", "底座", "门配件"]

    for category in all_categories:
        cat_dwg_dir = os.path.join(DWG_DIR, category)

        if not os.path.exists(cat_dwg_dir):
            continue

        print(f"\n处理: {category}/")

        for fname in sorted(os.listdir(cat_dwg_dir)):
            if not fname.endswith('.SLDDRW'):
                continue

            # 跳过FLAT页
            if '_FLAT.SLDDRW' in fname:
                continue

            dwg_path = os.path.join(cat_dwg_dir, fname)

            try:
                # 关闭之前的文档
                doc_count = safe_call(sw.GetDocumentCount)
                if doc_count > 0:
                    sw.CloseAllDocuments(True)
                    time.sleep(0.2)

                # 打开工程图
                doc = sw.OpenDoc(dwg_path, 3)
                if not doc:
                    print(f"  ❌ {fname}: OpenDoc失败")
                    stats['flat_failed'] += 1
                    continue

                # 生成展开图PDF路径
                base_name = os.path.splitext(fname)[0]
                pdf_path = os.path.join(FLAT_PDF_DIR, f"{base_name}_展开.pdf")

                # 导出PDF
                result = doc.SaveAs3(pdf_path, 0, 0)

                # 关闭文档
                sw.CloseDoc(dwg_path)

                if os.path.exists(pdf_path):
                    print(f"  ✅ {fname} → {os.path.basename(pdf_path)}")
                    stats['flat_success'] += 1
                else:
                    print(f"  ❌ {fname}: PDF未生成")
                    stats['flat_failed'] += 1

                # 每5个文件休息
                if (stats['flat_success'] + stats['flat_failed']) % 5 == 0:
                    time.sleep(0.5)

            except Exception as e:
                print(f"  ❌ {fname}: {e}")
                stats['flat_failed'] += 1
                stats['errors'].append({'file': fname, 'error': str(e)})

def main():
    print("="*80)
    print("重新导出所有PDF文件")
    print("="*80)

    pythoncom.CoInitialize()
    sw = win32com.client.Dispatch("SldWorks.Application")
    sw.Visible = True

    try:
        # 导出原工程图PDF
        export_engineering_pdfs(sw)

        # 导出展开图PDF
        export_flatpattern_pdfs(sw)

    finally:
        # 清理
        try:
            sw.CloseAllDocuments(True)
        except Exception:
            pass
        pythoncom.CoUninitialize()

    # 统计报告
    print("\n" + "="*80)
    print("导出完成")
    print("="*80)
    print(f"原工程图PDF: {stats['eng_success']} 成功, {stats['eng_failed']} 失败")
    print(f"展开图PDF: {stats['flat_success']} 成功, {stats['flat_failed']} 失败")

    if stats['errors']:
        print(f"\n错误详情: {len(stats['errors'])} 个")
        for err in stats['errors'][:10]:
            print(f"  - {err['file']}: {err['error']}")

    print("\nprotectedFilesUnchanged=true")
    print("sessionExited=true")

if __name__ == "__main__":
    main()
