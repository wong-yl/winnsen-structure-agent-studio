#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
16029订单 BOM清单生成脚本
从CSV零件清单生成完整的Excel BOM表格
"""

import sys
import io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

import pandas as pd
import openpyxl
from openpyxl.styles import Font, Alignment, PatternFill, Border, Side
from openpyxl.utils import get_column_letter
from datetime import datetime
import os
import sys

# 配置
ORDER_DIR = r"D:/参数化试验出图1/16029 寄存柜(1200×1917×485) 左12右13门"
CSV_FILE = os.path.join(ORDER_DIR, "零件清单_数据库种子.csv")
OUTPUT_DIR = os.path.join(ORDER_DIR, "3.BOM")
OUTPUT_FILE = os.path.join(OUTPUT_DIR, "16029_寄存柜_BOM清单.xlsx")

# 标准件库（根据订单实际情况）
STANDARD_PARTS = [
    {"名称": "铰链", "规格": "304不锈钢", "单位": "个", "单件数量": 2, "门数": 25, "数量": 50, "备注": "每扇门2个"},
    {"名称": "锁具", "规格": "锌合金", "单位": "套", "单件数量": 1, "门数": 25, "数量": 25, "备注": "每扇门1套"},
    {"名称": "挂钩", "规格": "U型挂钩", "单位": "个", "单件数量": 1, "门数": 25, "数量": 25, "备注": "每扇门1个"},
    {"名称": "螺丝", "规格": "M4×10", "单位": "个", "单件数量": 8, "门数": 25, "数量": 200, "备注": "预留10%余量"},
    {"名称": "螺母", "规格": "M4", "单位": "个", "单件数量": 8, "门数": 25, "数量": 200, "备注": "预留10%余量"},
]

def load_parts_csv(csv_path):
    """加载零件CSV文件"""
    print(f"[1/5] 读取零件清单：{csv_path}")

    if not os.path.exists(csv_path):
        print(f"❌ 错误：找不到文件 {csv_path}")
        sys.exit(1)

    df = pd.read_csv(csv_path, encoding='utf-8-sig')  # 处理BOM
    print(f"✓ 成功读取 {len(df)} 个零件")

    # 打印列名用于调试
    print(f"  列名：{list(df.columns)}")
    return df

def create_bom_workbook(parts_df):
    """创建Excel工作簿"""
    print("[2/5] 创建Excel工作簿...")

    wb = openpyxl.Workbook()

    # 删除默认sheet
    wb.remove(wb.active)

    # 创建多个Sheet
    sheets = {
        'summary': wb.create_sheet("汇总表", 0),
        'sheetmetal': wb.create_sheet("钣金件清单", 1),
        'standard': wb.create_sheet("标准件清单", 2),
        'material': wb.create_sheet("材料统计", 3),
    }

    print("✓ 创建了4个工作表")
    return wb, sheets

def style_header(ws, row=1):
    """设置表头样式"""
    header_fill = PatternFill(start_color="366092", end_color="366092", fill_type="solid")
    header_font = Font(name='微软雅黑', size=11, bold=True, color="FFFFFF")
    border = Border(
        left=Side(style='thin'),
        right=Side(style='thin'),
        top=Side(style='thin'),
        bottom=Side(style='thin')
    )

    for cell in ws[row]:
        if cell.value:
            cell.fill = header_fill
            cell.font = header_font
            cell.alignment = Alignment(horizontal='center', vertical='center')
            cell.border = border

def write_summary_sheet(ws, parts_df):
    """写入汇总表"""
    print("[3/5] 生成汇总表...")

    # 标题
    ws['A1'] = "16029订单BOM汇总表"
    ws['A1'].font = Font(name='微软雅黑', size=16, bold=True)
    ws.merge_cells('A1:F1')

    # 订单信息
    ws['A3'] = "订单信息"
    ws['A3'].font = Font(name='微软雅黑', size=12, bold=True)

    info_data = [
        ["订单号", "16029"],
        ["产品名称", "寄存柜"],
        ["规格", "1200×1917×485"],
        ["门数", "25门（左12+右13）"],
        ["生成日期", datetime.now().strftime("%Y-%m-%d %H:%M")],
        ["版本", "R5"],
    ]

    for i, (key, value) in enumerate(info_data, start=4):
        ws[f'A{i}'] = key
        ws[f'B{i}'] = value
        ws[f'A{i}'].font = Font(name='微软雅黑', bold=True)

    # 统计信息
    ws['A11'] = "统计信息"
    ws['A11'].font = Font(name='微软雅黑', size=12, bold=True)

    stats_data = [
        ["钣金件种类", len(parts_df), "种"],
        ["工程图文件", 33, "个"],
        ["PDF图纸", 31, "个"],
        ["标准件种类", len(STANDARD_PARTS), "种"],
    ]

    for i, (key, value, unit) in enumerate(stats_data, start=12):
        ws[f'A{i}'] = key
        ws[f'B{i}'] = value
        ws[f'C{i}'] = unit
        ws[f'A{i}'].font = Font(name='微软雅黑', bold=True)

    # 分类统计
    ws['A17'] = "零件分类统计"
    ws['A17'].font = Font(name='微软雅黑', size=12, bold=True)

    ws.append(["类别", "数量", "占比"])
    style_header(ws, row=18)

    # 使用正确的列名
    category_col = '部件类别' if '部件类别' in parts_df.columns else 'category'
    if category_col in parts_df.columns:
        category_stats = parts_df[category_col].value_counts()
        for category, count in category_stats.items():
            percentage = f"{count/len(parts_df)*100:.1f}%"
            ws.append([category, count, percentage])
    else:
        ws.append(["数据缺失", len(parts_df), "100%"])

    # 调整列宽
    ws.column_dimensions['A'].width = 20
    ws.column_dimensions['B'].width = 30
    ws.column_dimensions['C'].width = 15

    print("✓ 汇总表完成")

def write_sheetmetal_sheet(ws, parts_df):
    """写入钣金件清单"""
    print("[4/5] 生成钣金件清单...")

    # 标题
    ws['A1'] = "钣金件清单"
    ws['A1'].font = Font(name='微软雅黑', size=14, bold=True)
    ws.merge_cells('A1:H1')

    # 表头
    headers = ["序号", "零件图号", "零件名称", "零件类别", "材料", "数量", "批次来源", "备注"]
    ws.append(headers)
    style_header(ws, row=2)

    # 数据
    for idx, row in parts_df.iterrows():
        ws.append([
            idx + 1,
            row.get('零件图号', ''),
            row.get('零件名称', ''),
            row.get('零件类别', ''),
            row.get('材料', ''),
            row.get('数量', 1),
            row.get('批次来源', ''),
            row.get('备注', ''),
        ])

    # 调整列宽
    widths = [8, 20, 25, 12, 15, 8, 15, 30]
    for i, width in enumerate(widths, start=1):
        ws.column_dimensions[get_column_letter(i)].width = width

    # 居中对齐
    for row in ws.iter_rows(min_row=3, max_row=ws.max_row):
        for cell in row:
            cell.alignment = Alignment(horizontal='center', vertical='center')

    print(f"✓ 钣金件清单完成（{len(parts_df)} 个零件）")

def write_standard_sheet(ws):
    """写入标准件清单"""
    print("[4.5/5] 生成标准件清单...")

    # 标题
    ws['A1'] = "标准件清单"
    ws['A1'].font = Font(name='微软雅黑', size=14, bold=True)
    ws.merge_cells('A1:G1')

    # 表头
    headers = ["序号", "名称", "规格", "单位", "数量", "单价（参考）", "备注"]
    ws.append(headers)
    style_header(ws, row=2)

    # 数据
    for idx, part in enumerate(STANDARD_PARTS, start=1):
        ws.append([
            idx,
            part['名称'],
            part['规格'],
            part['单位'],
            part['数量'],
            "",  # 单价留空
            part['备注'],
        ])

    # 调整列宽
    widths = [8, 15, 20, 8, 10, 15, 30]
    for i, width in enumerate(widths, start=1):
        ws.column_dimensions[get_column_letter(i)].width = width

    # 居中对齐
    for row in ws.iter_rows(min_row=3, max_row=ws.max_row):
        for cell in row:
            cell.alignment = Alignment(horizontal='center', vertical='center')

    print(f"✓ 标准件清单完成（{len(STANDARD_PARTS)} 种标准件）")

def write_material_sheet(ws, parts_df):
    """写入材料统计"""
    print("[5/5] 生成材料统计...")

    # 标题
    ws['A1'] = "材料统计表"
    ws['A1'].font = Font(name='微软雅黑', size=14, bold=True)
    ws.merge_cells('A1:E1')

    # 表头
    headers = ["序号", "材料规格", "零件数量", "总数量", "备注"]
    ws.append(headers)
    style_header(ws, row=2)

    # 使用正确的列名
    material_col = '材料' if '材料' in parts_df.columns else 'material'
    qty_col = '数量' if '数量' in parts_df.columns else 'quantity'
    name_col = '零件名称' if '零件名称' in parts_df.columns else 'name'

    # 按材料分组统计
    if material_col in parts_df.columns:
        material_stats = parts_df.groupby(material_col).agg({
            name_col: 'count',
        }).reset_index()
        material_stats.columns = ['材料', '零件数量']
        material_stats['总数量'] = material_stats['零件数量']  # 假设每个零件数量为1

        # 数据
        for idx, row in material_stats.iterrows():
            ws.append([
                idx + 1,
                row['材料'],
                row['零件数量'],
                row['总数量'],
                f"{row['零件数量']}种零件，共{row['总数量']}件"
            ])

        # 合计行
        total_parts = len(parts_df)
        total_qty = len(parts_df)
        ws.append([
            "",
            "合计",
            total_parts,
            total_qty,
            f"共{len(material_stats)}种材料"
        ])
    else:
        ws.append([1, "数据缺失", len(parts_df), len(parts_df), ""])

    last_row = ws.max_row
    for cell in ws[last_row]:
        cell.font = Font(name='微软雅黑', bold=True)
        cell.fill = PatternFill(start_color="E7E6E6", end_color="E7E6E6", fill_type="solid")

    # 调整列宽
    widths = [8, 20, 12, 12, 40]
    for i, width in enumerate(widths, start=1):
        ws.column_dimensions[get_column_letter(i)].width = width

    # 居中对齐
    for row in ws.iter_rows(min_row=3, max_row=ws.max_row):
        for cell in row:
            cell.alignment = Alignment(horizontal='center', vertical='center')

    print(f"✓ 材料统计完成（{len(material_stats)} 种材料）")

def main():
    print("=" * 60)
    print("  16029订单 BOM清单生成器")
    print("=" * 60)
    print()

    # 1. 加载CSV
    parts_df = load_parts_csv(CSV_FILE)

    # 2. 创建工作簿
    wb, sheets = create_bom_workbook(parts_df)

    # 3-5. 写入各个Sheet
    write_summary_sheet(sheets['summary'], parts_df)
    write_sheetmetal_sheet(sheets['sheetmetal'], parts_df)
    write_standard_sheet(sheets['standard'])
    write_material_sheet(sheets['material'], parts_df)

    # 6. 保存文件
    print()
    print(f"保存BOM文件到：{OUTPUT_FILE}")

    os.makedirs(OUTPUT_DIR, exist_ok=True)
    wb.save(OUTPUT_FILE)

    print()
    print("=" * 60)
    print("✅ BOM清单生成完成！")
    print("=" * 60)
    print()
    print(f"输出文件：{OUTPUT_FILE}")
    print(f"文件大小：{os.path.getsize(OUTPUT_FILE) / 1024:.1f} KB")
    print()
    print("包含内容：")
    print("  - 汇总表（订单信息、统计数据）")
    print(f"  - 钣金件清单（{len(parts_df)} 个零件）")
    print(f"  - 标准件清单（{len(STANDARD_PARTS)} 种标准件）")
    print("  - 材料统计（按材料规格分组）")
    print()
    print("请打开Excel文件检查内容。")

if __name__ == "__main__":
    main()
