#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
16029订单 批量生成钣金展开图
从SLDDRW工程图中提取展开视图，导出DXF和PDF
"""

import sys
import io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

import os
import time
import json
from pathlib import Path
from datetime import datetime
import win32com.client
import pythoncom

# 配置
ORDER_DIR = r"D:/参数化试验出图1/16029 寄存柜(1200×1917×485) 左12右13门"
INPUT_DIR = os.path.join(ORDER_DIR, "1.工程图")
OUTPUT_DXF_DIR = os.path.join(ORDER_DIR, "2.钣金展开图/DXF")
OUTPUT_PDF_DIR = os.path.join(ORDER_DIR, "2.钣金展开图/PDF")
LOG_FILE = os.path.join(ORDER_DIR, f"展开图导出日志_{datetime.now().strftime('%Y%m%d_%H%M%S')}.json")

# 进度文件（支持中断恢复）
PROGRESS_FILE = os.path.join(ORDER_DIR, ".flatpattern_progress.json")

# 统计
stats = {
    'total': 0,
    'success_dxf': 0,
    'success_pdf': 0,
    'skipped': 0,
    'failed': 0,
    'errors': []
}

def load_progress():
    """加载已处理的文件列表"""
    if os.path.exists(PROGRESS_FILE):
        try:
            with open(PROGRESS_FILE, 'r', encoding='utf-8') as f:
                return json.load(f)
        except:
            return {'processed': [], 'timestamp': ''}
    return {'processed': [], 'timestamp': ''}

def save_progress(processed_files):
    """保存进度"""
    with open(PROGRESS_FILE, 'w', encoding='utf-8') as f:
        json.dump({
            'processed': processed_files,
            'timestamp': datetime.now().isoformat()
        }, f, ensure_ascii=False, indent=2)

def find_slddrw_files(input_dir):
    """递归查找所有SLDDRW文件"""
    slddrw_files = []
    for root, dirs, files in os.walk(input_dir):
        for file in files:
            if file.upper().endswith('.SLDDRW'):
                full_path = os.path.join(root, file)
                slddrw_files.append(full_path)
    return slddrw_files

def init_solidworks():
    """初始化SolidWorks连接"""
    print("[初始化] 连接到SolidWorks...")

    try:
        pythoncom.CoInitialize()
        sw = win32com.client.Dispatch("SldWorks.Application")
        sw.Visible = True
        print("✓ SolidWorks已连接")
        return sw
    except Exception as e:
        print(f"❌ 错误：无法连接到SolidWorks")
        print(f"   {str(e)}")
        print("\n请确保：")
        print("  1. SolidWorks已安装")
        print("  2. SolidWorks正在运行")
        print("  3. 关闭了所有已打开的文档")
        sys.exit(1)

def is_sheet_metal_drawing(sw_model):
    """检查图纸是否是钣金件"""
    try:
        # 获取第一个视图
        view = sw_model.GetFirstView()
        if not view:
            return False

        # 跳过图纸视图（Sheet）
        view = view.GetNextView()
        if not view:
            return False

        # 获取引用的模型
        ref_doc = view.ReferencedDocument
        if not ref_doc:
            return False

        # 检查是否是零件文档
        doc_type = ref_doc.GetType
        if callable(doc_type):
            doc_type = doc_type()
        if doc_type != 1:  # 1 = Part
            return False

        # 检查是否有钣金特征
        feat = ref_doc.FirstFeature()
        while feat:
            feat_type = feat.GetTypeName2()
            if 'SheetMetal' in feat_type or 'FlatPattern' in feat_type:
                return True
            feat = feat.GetNextFeature()

        return False
    except:
        return False

def has_flat_pattern_view(sw_model):
    """检查图纸是否已有展开视图"""
    try:
        view = sw_model.GetFirstView()
        while view:
            view_name = view.Name
            if view_name and ('展开' in view_name or 'FLAT' in view_name.upper() or 'PATTERN' in view_name.upper()):
                return True
            view = view.GetNextView()
        return False
    except:
        return False

def export_dxf_from_drawing(sw, sw_model, output_path):
    """从图纸导出DXF（展开视图）"""
    try:
        # 确保输出目录存在
        os.makedirs(os.path.dirname(output_path), exist_ok=True)

        # 导出DXF
        # 参数：路径，版本（默认），导出类型（2=当前图纸）
        success = sw_model.ExportToDWG2(
            output_path,
            output_path,  # 映射文件路径（使用相同路径）
            1,  # 版本：1=AutoCAD 2013
            True,  # 导出所有图纸
            None,  # 导出数据（None=使用默认）
            False,  # 不导出草图
            False,  # 不导出注释
            False,  # 不包含隐藏实体
            True   # 导出为DXF格式
        )

        if success and os.path.exists(output_path):
            return True, None
        else:
            return False, "导出失败（未生成文件）"
    except Exception as e:
        return False, str(e)

def export_pdf_from_drawing(sw_model, output_path):
    """从图纸导出PDF"""
    try:
        # 确保输出目录存在
        os.makedirs(os.path.dirname(output_path), exist_ok=True)

        # 导出PDF
        success = sw_model.Extension.SaveAs(
            output_path,
            0,  # 保存为新名称
            1,  # swSaveAsOptions_Silent (不显示对话框)
            None,  # 导出数据
            0,  # 错误
            0   # 警告
        )

        if success and os.path.exists(output_path):
            return True, None
        else:
            return False, "导出失败（未生成文件）"
    except Exception as e:
        return False, str(e)

def process_drawing_file(sw, file_path, processed_files):
    """处理单个图纸文件"""
    file_name = os.path.basename(file_path)

    # 检查是否已处理
    if file_path in processed_files:
        print(f"  [跳过] {file_name}（已处理）")
        stats['skipped'] += 1
        return True

    print(f"\n[处理] {file_name}")

    # 生成输出文件名
    base_name = os.path.splitext(file_name)[0]

    # 如果已经是展开图文件（*_FLAT.SLDDRW），去掉_FLAT后缀
    if base_name.upper().endswith('_FLAT'):
        base_name = base_name[:-5]

    output_dxf = os.path.join(OUTPUT_DXF_DIR, f"{base_name}_展开.DXF")
    output_pdf = os.path.join(OUTPUT_PDF_DIR, f"{base_name}_展开.pdf")

    try:
        # 关闭所有已打开的文档（防止文档残留）
        print(f"  [1/5] 清理环境...")
        try:
            doc_count = sw.GetDocumentCount
            if callable(doc_count):
                doc_count = doc_count()
            if doc_count > 0:
                print(f"    发现 {doc_count} 个已打开文档，正在关闭...")
                sw.CloseAllDocuments(True)  # True = 不保存
                time.sleep(1)  # 等待关闭完成
        except Exception as e:
            print(f"    清理环境时出错（继续）: {e}")

        # 打开图纸
        print(f"  [2/5] 打开图纸...")

        # 打开文档：路径, 类型(3=Drawing), 选项, 配置, 错误, 警告
        errors = 0
        warnings = 0
        sw_model = sw.OpenDoc6(file_path, 3, 1, "", errors, warnings)  # 1=只读打开

        if not sw_model:
            error_msg = "无法打开文件"
            print(f"  ❌ {error_msg}")
            stats['errors'].append({
                'file': file_name,
                'error': error_msg,
                'timestamp': datetime.now().isoformat()
            })
            stats['failed'] += 1
            return False

        # 检查是否是钣金件
        print(f"  [2/4] 检查零件类型...")
        is_sheet_metal = is_sheet_metal_drawing(sw_model)
        has_flat = has_flat_pattern_view(sw_model)

        if not is_sheet_metal and not has_flat and '_FLAT' not in file_name.upper():
            print(f"  ⚠️  非钣金件，跳过")
            sw.CloseDoc(file_path)
            stats['skipped'] += 1
            processed_files.append(file_path)
            save_progress(processed_files)
            return True

        # 导出DXF
        print(f"  [3/4] 导出DXF...")
        success_dxf, error_dxf = export_dxf_from_drawing(sw, sw_model, output_dxf)

        if success_dxf:
            print(f"  ✓ DXF: {os.path.basename(output_dxf)}")
            stats['success_dxf'] += 1
        else:
            print(f"  ❌ DXF导出失败: {error_dxf}")
            stats['errors'].append({
                'file': file_name,
                'type': 'DXF',
                'error': error_dxf,
                'timestamp': datetime.now().isoformat()
            })

        # 导出PDF
        print(f"  [4/4] 导出PDF...")
        success_pdf, error_pdf = export_pdf_from_drawing(sw_model, output_pdf)

        if success_pdf:
            print(f"  ✓ PDF: {os.path.basename(output_pdf)}")
            stats['success_pdf'] += 1
        else:
            print(f"  ❌ PDF导出失败: {error_pdf}")
            stats['errors'].append({
                'file': file_name,
                'type': 'PDF',
                'error': error_pdf,
                'timestamp': datetime.now().isoformat()
            })

        # 关闭文档
        sw.CloseDoc(file_path)

        # 标记为已处理
        processed_files.append(file_path)
        save_progress(processed_files)

        # 至少一个成功就算成功
        if success_dxf or success_pdf:
            return True
        else:
            stats['failed'] += 1
            return False

    except Exception as e:
        error_msg = f"处理异常: {str(e)}"
        print(f"  ❌ {error_msg}")
        stats['errors'].append({
            'file': file_name,
            'error': error_msg,
            'timestamp': datetime.now().isoformat()
        })
        stats['failed'] += 1

        # 尝试关闭文档
        try:
            sw.CloseDoc(file_path)
        except:
            pass

        return False

def save_log():
    """保存日志文件"""
    log_data = {
        'order': '16029 寄存柜(1200×1917×485) 左12右13门',
        'timestamp': datetime.now().isoformat(),
        'statistics': stats,
        'success_rate': f"{(stats['success_dxf']/stats['total']*100) if stats['total']>0 else 0:.1f}%",
    }

    with open(LOG_FILE, 'w', encoding='utf-8') as f:
        json.dump(log_data, f, ensure_ascii=False, indent=2)

    print(f"\n日志已保存：{LOG_FILE}")

def main():
    print("=" * 70)
    print("  16029订单 批量生成钣金展开图")
    print("=" * 70)
    print()
    print(f"订单目录: {ORDER_DIR}")
    print(f"输入目录: {INPUT_DIR}")
    print(f"输出DXF: {OUTPUT_DXF_DIR}")
    print(f"输出PDF: {OUTPUT_PDF_DIR}")
    print()

    # 查找所有SLDDRW文件
    print("[扫描] 查找SLDDRW文件...")
    slddrw_files = find_slddrw_files(INPUT_DIR)
    stats['total'] = len(slddrw_files)

    if stats['total'] == 0:
        print("❌ 错误：未找到SLDDRW文件")
        sys.exit(1)

    print(f"✓ 找到 {stats['total']} 个SLDDRW文件")
    print()

    # 加载进度
    progress = load_progress()
    processed_files = progress['processed']

    if processed_files:
        print(f"[恢复] 已处理 {len(processed_files)} 个文件，继续处理剩余文件")
        print()

    # 创建输出目录
    os.makedirs(OUTPUT_DXF_DIR, exist_ok=True)
    os.makedirs(OUTPUT_PDF_DIR, exist_ok=True)

    # 初始化SolidWorks
    sw = init_solidworks()

    # 处理每个文件
    print()
    print("=" * 70)
    print(f"开始批量处理（共{stats['total']}个文件）")
    print("提示：可以随时按 Ctrl+C 中断，再次运行会继续未完成的文件")
    print("=" * 70)

    start_time = time.time()

    try:
        for i, file_path in enumerate(slddrw_files, 1):
            print(f"\n{'='*70}")
            print(f"进度: {i}/{stats['total']} ({i/stats['total']*100:.1f}%)")
            print(f"文件: {os.path.basename(file_path)}")
            print(f"{'='*70}")

            process_drawing_file(sw, file_path, processed_files)

            # 每处理5个文件休息一下，避免SolidWorks卡顿
            if i % 5 == 0:
                print("\n[休息] 5秒...")
                time.sleep(5)

    except KeyboardInterrupt:
        print("\n\n⚠️  用户中断！")
        print("进度已保存，下次运行会继续未完成的文件。")

    finally:
        # 清理：关闭所有文档但不退出 SolidWorks
        try:
            sw.CloseAllDocuments(True)
        except:
            pass

        pythoncom.CoUninitialize()

    # 统计
    elapsed_time = time.time() - start_time
    print("\n")
    print("=" * 70)
    print("  批量处理完成")
    print("=" * 70)
    print()
    print(f"总文件数:    {stats['total']}")
    print(f"DXF成功:     {stats['success_dxf']} ✓")
    print(f"PDF成功:     {stats['success_pdf']} ✓")
    print(f"跳过:        {stats['skipped']}")
    print(f"失败:        {stats['failed']} ❌")
    print(f"用时:        {elapsed_time/60:.1f} 分钟")
    print()

    if stats['success_dxf'] > 0:
        print(f"✅ 成功率: {stats['success_dxf']/stats['total']*100:.1f}%")

    if stats['errors']:
        print(f"\n⚠️  有 {len(stats['errors'])} 个错误，详见日志文件")

    # 保存日志
    save_log()

    # 清理进度文件（如果全部完成）
    if len(processed_files) == stats['total']:
        try:
            os.remove(PROGRESS_FILE)
            print("\n✓ 进度文件已清理")
        except:
            pass

    print()
    print("输出目录:")
    print(f"  DXF: {OUTPUT_DXF_DIR}")
    print(f"  PDF: {OUTPUT_PDF_DIR}")
    print()

if __name__ == "__main__":
    try:
        main()
    except Exception as e:
        print(f"\n❌ 严重错误: {str(e)}")
        import traceback
        traceback.print_exc()
        sys.exit(1)
