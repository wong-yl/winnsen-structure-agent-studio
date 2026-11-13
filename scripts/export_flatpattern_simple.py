#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""简化版展开图导出脚本"""

import sys
import io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

import os
import time
import json
from datetime import datetime
import win32com.client
import pythoncom

# 配置
ORDER_DIR = r"D:\参数化试验出图1\16029 寄存柜(1200×1917×485) 左12右13门"
INPUT_DIR = os.path.join(ORDER_DIR, "1.工程图")
OUTPUT_DXF_DIR = os.path.join(ORDER_DIR, "2.钣金展开图", "DXF")
OUTPUT_PDF_DIR = os.path.join(ORDER_DIR, "2.钣金展开图", "PDF")

# 统计
success_count = 0
failed_count = 0
skipped_count = 0

def safe_call(obj_or_func):
    """安全调用：处理属性/方法的二义性"""
    if callable(obj_or_func):
        return obj_or_func()
    return obj_or_func

def find_drawings():
    """查找所有工程图文件"""
    drawings = []
    for root, dirs, files in os.walk(INPUT_DIR):
        for file in files:
            if file.upper().endswith('.SLDDRW'):
                drawings.append(os.path.join(root, file))
    return drawings

def export_one_drawing(sw, file_path):
    """处理单个图纸"""
    global success_count, failed_count, skipped_count

    file_name = os.path.basename(file_path)
    base_name = os.path.splitext(file_name)[0]

    # 去掉 _FLAT 后缀
    if base_name.upper().endswith('_FLAT'):
        base_name = base_name[:-5]

    output_dxf = os.path.join(OUTPUT_DXF_DIR, f"{base_name}_展开.DXF")
    output_pdf = os.path.join(OUTPUT_PDF_DIR, f"{base_name}_展开.pdf")

    print(f"\n处理: {file_name}")

    # 如果已经导出过，跳过
    if os.path.exists(output_dxf) and os.path.exists(output_pdf):
        print(f"  跳过（已存在）")
        skipped_count += 1
        return True

    try:
        # 关闭所有打开的文档
        try:
            doc_count = safe_call(sw.GetDocumentCount)
            if doc_count > 0:
                sw.CloseAllDocuments(True)
                time.sleep(0.5)
        except:
            pass

        # 打开图纸（使用简单的 2 参数版本）
        print(f"  打开图纸...")
        sw_model = sw.OpenDoc(file_path, 3)  # 3 = swDocDRAWING

        if not sw_model:
            print(f"  ❌ 无法打开")
            failed_count += 1
            return False

        # 确保输出目录存在
        os.makedirs(OUTPUT_DXF_DIR, exist_ok=True)
        os.makedirs(OUTPUT_PDF_DIR, exist_ok=True)

        # 导出 DXF
        print(f"  导出 DXF...")
        try:
            result = sw_model.SaveAs3(output_dxf, 0, 0)
            if result and os.path.exists(output_dxf):
                size_kb = os.path.getsize(output_dxf) / 1024
                print(f"  ✅ DXF: {os.path.basename(output_dxf)} ({size_kb:.0f} KB)")
            else:
                print(f"  ⚠️  DXF 导出返回 {result}")
        except Exception as e:
            print(f"  ❌ DXF 失败: {e}")

        # 导出 PDF
        print(f"  导出 PDF...")
        try:
            result = sw_model.SaveAs3(output_pdf, 0, 0)
            if result and os.path.exists(output_pdf):
                size_kb = os.path.getsize(output_pdf) / 1024
                print(f"  ✅ PDF: {os.path.basename(output_pdf)} ({size_kb:.0f} KB)")
            else:
                print(f"  ⚠️  PDF 导出返回 {result}")
        except Exception as e:
            print(f"  ❌ PDF 失败: {e}")

        # 关闭文档
        sw.CloseDoc(file_path)

        # 检查是否至少导出成功一个
        if os.path.exists(output_dxf) or os.path.exists(output_pdf):
            success_count += 1
            return True
        else:
            failed_count += 1
            return False

    except Exception as e:
        print(f"  ❌ 异常: {e}")
        failed_count += 1
        try:
            sw.CloseDoc(file_path)
        except:
            pass
        return False

def main():
    print("=" * 70)
    print("  16029 订单展开图导出（简化版）")
    print("=" * 70)
    print()
    print(f"订单目录: {ORDER_DIR}")
    print(f"输出 DXF: {OUTPUT_DXF_DIR}")
    print(f"输出 PDF: {OUTPUT_PDF_DIR}")
    print()

    # 查找图纸
    print("扫描工程图文件...")
    drawings = find_drawings()
    total = len(drawings)

    if total == 0:
        print("❌ 未找到 SLDDRW 文件")
        return

    print(f"✅ 找到 {total} 个文件\n")

    # 连接 SolidWorks
    print("连接 SolidWorks...")
    try:
        pythoncom.CoInitialize()
        sw = win32com.client.Dispatch("SldWorks.Application")
        sw.Visible = True
        print("✅ 已连接\n")
    except Exception as e:
        print(f"❌ 无法连接: {e}")
        return

    # 批量处理
    print("=" * 70)
    print("开始批量处理")
    print("=" * 70)

    start_time = time.time()

    try:
        for i, file_path in enumerate(drawings, 1):
            print(f"\n[{i}/{total}] ({i*100//total}%)")
            export_one_drawing(sw, file_path)

            # 每 5 个休息一下
            if i % 5 == 0:
                print("\n休息 3 秒...")
                time.sleep(3)

    except KeyboardInterrupt:
        print("\n\n用户中断")

    finally:
        # 关闭所有文档但不退出 SolidWorks
        try:
            sw.CloseAllDocuments(True)
        except:
            pass
        pythoncom.CoUninitialize()

    # 统计
    elapsed = (time.time() - start_time) / 60

    print("\n" + "=" * 70)
    print("完成")
    print("=" * 70)
    print(f"总文件:  {total}")
    print(f"成功:    {success_count}")
    print(f"跳过:    {skipped_count}")
    print(f"失败:    {failed_count}")
    print(f"用时:    {elapsed:.1f} 分钟")
    print()

    # 检查实际输出
    dxf_count = len([f for f in os.listdir(OUTPUT_DXF_DIR) if f.endswith('.DXF')]) if os.path.exists(OUTPUT_DXF_DIR) else 0
    pdf_count = len([f for f in os.listdir(OUTPUT_PDF_DIR) if f.endswith('.pdf')]) if os.path.exists(OUTPUT_PDF_DIR) else 0

    print(f"实际输出:")
    print(f"  DXF: {dxf_count} 个")
    print(f"  PDF: {pdf_count} 个")

if __name__ == '__main__':
    main()

