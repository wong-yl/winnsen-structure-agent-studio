"""
16029订单 - 钣金展开图和BOM自动生成脚本
需要环境: Windows + SolidWorks 2020+ + Python 3.8+
依赖: pip install pywin32
"""

import os
import sys
import json
import csv
from pathlib import Path
import win32com.client
from datetime import datetime

# ===============================
# 配置区
# ===============================
PROJECT_ROOT = Path(r"D:/参数化试验出图1/16029 寄存柜(1200×1917×485) 左12右13门")
DRAWING_DIR = PROJECT_ROOT / "1.工程图"
DXF_OUTPUT_DIR = PROJECT_ROOT / "2.钣金展开图/DXF"
PDF_FLAT_OUTPUT_DIR = PROJECT_ROOT / "2.钣金展开图/PDF"
BOM_OUTPUT_DIR = PROJECT_ROOT / "3.BOM"

# 确保输出目录存在
DXF_OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
PDF_FLAT_OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
BOM_OUTPUT_DIR.mkdir(parents=True, exist_ok=True)


class SolidWorksAutomation:
    def __init__(self):
        self.sw_app = None
        self.log = []

    def connect(self):
        """连接SolidWorks应用"""
        try:
            self.sw_app = win32com.client.Dispatch("SldWorks.Application")
            self.sw_app.Visible = True
            self.log.append(f"[{datetime.now()}] ✓ 已连接SolidWorks")
            return True
        except Exception as e:
            self.log.append(f"[{datetime.now()}] ✗ 连接失败: {e}")
            return False

    def open_drawing(self, slddrw_path):
        """打开工程图文件"""
        try:
            errors = 0
            warnings = 0
            doc = self.sw_app.OpenDoc6(
                str(slddrw_path),
                3,  # swDocDRAWING
                0,  # swOpenDocOptions_Silent
                "",
                errors,
                warnings
            )
            if doc:
                self.log.append(f"  ✓ 打开: {slddrw_path.name}")
                return doc
            else:
                self.log.append(f"  ✗ 打开失败: {slddrw_path.name} (错误码:{errors})")
                return None
        except Exception as e:
            self.log.append(f"  ✗ 异常: {e}")
            return None

    def export_flat_pattern_dxf(self, drawing_doc, part_name):
        """导出展开图DXF"""
        try:
            # 切换到展开图视图（需要预先在图纸中创建）
            # 这里简化处理：直接导出当前视图
            dxf_path = DXF_OUTPUT_DIR / f"{part_name}.DXF"

            # SolidWorks导出DXF
            result = drawing_doc.SaveAs3(
                str(dxf_path),
                0,  # swSaveAsOptions_Silent
                0   # swFileSaveAs_SaveCopy
            )

            if result:
                self.log.append(f"    ✓ DXF导出: {dxf_path.name}")
                return True
            else:
                self.log.append(f"    ✗ DXF导出失败")
                return False
        except Exception as e:
            self.log.append(f"    ✗ DXF异常: {e}")
            return False

    def export_flat_pattern_pdf(self, drawing_doc, part_name):
        """导出展开图PDF"""
        try:
            pdf_path = PDF_FLAT_OUTPUT_DIR / f"{part_name}_展开图.PDF"

            result = drawing_doc.SaveAs3(
                str(pdf_path),
                0,
                0
            )

            if result:
                self.log.append(f"    ✓ PDF导出: {pdf_path.name}")
                return True
            else:
                self.log.append(f"    ✗ PDF导出失败")
                return False
        except Exception as e:
            self.log.append(f"    ✗ PDF异常: {e}")
            return False

    def close_document(self):
        """关闭当前文档"""
        try:
            self.sw_app.CloseDoc("")
            return True
        except:
            return False

    def save_log(self, log_path):
        """保存操作日志"""
        with open(log_path, 'w', encoding='utf-8') as f:
            f.write('\n'.join(self.log))


def collect_slddrw_files():
    """收集所有需要处理的SLDDRW文件"""
    slddrw_files = []

    # 遍历所有部件子目录
    for subdir in DRAWING_DIR.iterdir():
        if subdir.is_dir():
            for slddrw in subdir.glob("*.SLDDRW"):
                slddrw_files.append({
                    'path': slddrw,
                    'name': slddrw.stem,
                    'category': subdir.name
                })

    return slddrw_files


def generate_bom_from_csv():
    """从零件清单CSV生成BOM Excel"""
    try:
        import pandas as pd

        csv_path = PROJECT_ROOT / "零件清单_数据库种子.csv"
        bom_path = BOM_OUTPUT_DIR / "16029_寄存柜_BOM清单.xlsx"

        # 读取CSV
        df = pd.read_csv(csv_path, encoding='utf-8')

        # 添加数量列（默认为1，实际需工程师确认）
        df['数量'] = 1
        df['单重(kg)'] = df['重量(g)'] / 1000
        df['总重(kg)'] = df['单重(kg)'] * df['数量']

        # 重新排列列顺序
        columns_order = [
            '零件图号', '零件名称', '部件类别', '材料', '材料规格',
            '数量', '单重(kg)', '总重(kg)', '批次号', '状态'
        ]
        df_bom = df[columns_order]

        # 导出Excel
        with pd.ExcelWriter(bom_path, engine='openpyxl') as writer:
            df_bom.to_excel(writer, sheet_name='BOM清单', index=False)

            # 添加汇总表
            summary = df_bom.groupby('部件类别').agg({
                '零件名称': 'count',
                '数量': 'sum',
                '总重(kg)': 'sum'
            }).rename(columns={'零件名称': '零件种类数'})
            summary.to_excel(writer, sheet_name='部件汇总')

        print(f"✓ BOM清单已生成: {bom_path}")
        return True

    except ImportError:
        print("⚠️ 需要安装pandas: pip install pandas openpyxl")
        return False
    except Exception as e:
        print(f"✗ BOM生成失败: {e}")
        return False


def main():
    print("=" * 60)
    print("16029订单 - 钣金展开图和BOM自动生成")
    print("=" * 60)

    # 第一步：生成BOM（不需要SolidWorks）
    print("\n[步骤1/2] 生成BOM清单...")
    generate_bom_from_csv()

    # 第二步：处理展开图（需要SolidWorks）
    print("\n[步骤2/2] 处理钣金展开图...")
    print("⚠️ 此步骤需要SolidWorks运行")

    response = input("是否继续？(y/n): ")
    if response.lower() != 'y':
        print("已取消展开图处理")
        return

    # 收集文件
    slddrw_files = collect_slddrw_files()
    print(f"找到 {len(slddrw_files)} 个SLDDRW文件")

    # 连接SolidWorks
    sw = SolidWorksAutomation()
    if not sw.connect():
        print("✗ 无法连接SolidWorks，请确保软件已安装")
        return

    # 批量处理
    success_count = 0
    for i, file_info in enumerate(slddrw_files, 1):
        print(f"\n[{i}/{len(slddrw_files)}] 处理: {file_info['name']}")

        doc = sw.open_drawing(file_info['path'])
        if doc:
            # 导出DXF和PDF
            dxf_ok = sw.export_flat_pattern_dxf(doc, file_info['name'])
            pdf_ok = sw.export_flat_pattern_pdf(doc, file_info['name'])

            if dxf_ok and pdf_ok:
                success_count += 1

            sw.close_document()

    # 保存日志
    log_path = PROJECT_ROOT / f"展开图生成日志_{datetime.now().strftime('%Y%m%d_%H%M%S')}.txt"
    sw.save_log(log_path)

    print(f"\n{'=' * 60}")
    print(f"处理完成: {success_count}/{len(slddrw_files)} 成功")
    print(f"日志已保存: {log_path}")
    print(f"{'=' * 60}")


if __name__ == "__main__":
    main()
