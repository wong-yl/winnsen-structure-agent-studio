#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
批量更新R59图纸为R5 RELEASED格式
- 更新签字栏为 Eli
- 更新日期为 2026-11-02
- 更新版本为 R5
- 重命名文件为 *_R5_RELEASED.SLDDRW
- 导出PDF到 delivery_r59_parts_RELEASED 目录
"""

import sys
import io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

import os
import time
import shutil
from datetime import datetime
import win32com.client
import pythoncom

# R59 原始文件列表
R59_FILES = [
    r"D:\Winnsen_Structure_Agent_Studio\data\production_orders_20261029\continuation_sol_20261029_r5\drawing_batch_prepare_r59\runs\run_r1\01_frame_门框 横隔板\门框 横隔板_R59.SLDDRW",
    r"D:\Winnsen_Structure_Agent_Studio\data\production_orders_20261029\continuation_sol_20261029_r5\drawing_batch_prepare_r59\runs\run_r1\02_frame_门框 横隔板R\门框 横隔板R_R59.SLDDRW",
    r"D:\Winnsen_Structure_Agent_Studio\data\production_orders_20261029\continuation_sol_20261029_r5\drawing_batch_prepare_r59\runs\run_r1\03_frame_门框 竖隔板L\门框 竖隔板L_R59.SLDDRW",
    r"D:\Winnsen_Structure_Agent_Studio\data\production_orders_20261029\continuation_sol_20261029_r5\drawing_batch_prepare_r59\runs\run_r1\04_frame_门框 竖隔板R\门框 竖隔板R_R59.SLDDRW",
    r"D:\Winnsen_Structure_Agent_Studio\data\production_orders_20261029\continuation_sol_20261029_r5\drawing_batch_prepare_r59\runs\run_r1\05_frame_门框 上\门框 上_R59.SLDDRW",
    r"D:\Winnsen_Structure_Agent_Studio\data\production_orders_20261029\continuation_sol_20261029_r5\drawing_batch_prepare_r59\runs\run_r1\06_frame_门框 下\门框 下_R59.SLDDRW",
    r"D:\Winnsen_Structure_Agent_Studio\data\production_orders_20261029\continuation_sol_20261029_r5\drawing_batch_prepare_r59\runs\run_r1\07_frame_门框 左\门框 左_R59.SLDDRW",
    r"D:\Winnsen_Structure_Agent_Studio\data\production_orders_20261029\continuation_sol_20261029_r5\drawing_batch_prepare_r59\runs\run_r1\08_frame_门框 右\门框 右_R59.SLDDRW",
    r"D:\Winnsen_Structure_Agent_Studio\data\production_orders_20261029\continuation_sol_20261029_r5\drawing_batch_prepare_r59\runs\run_r1\09_door_stiffener_H130\door_stiffener_W537_H130_R59.SLDDRW",
    r"D:\Winnsen_Structure_Agent_Studio\data\production_orders_20261029\continuation_sol_20261029_r5\drawing_batch_prepare_r59\runs\run_r1\10_door_stiffener_H179\door_stiffener_W537_H179_R59.SLDDRW",
    r"D:\Winnsen_Structure_Agent_Studio\data\production_orders_20261029\continuation_sol_20261029_r5\drawing_batch_prepare_r59\runs\run_r1\11_door_stiffener_H316\door_stiffener_W537_H316_R59.SLDDRW",
    r"D:\Winnsen_Structure_Agent_Studio\data\production_orders_20261029\continuation_sol_20261029_r5\drawing_batch_prepare_r59\runs\run_r1\12_reinforcement_箱体竖隔板加强件\箱体竖隔板加强件_R59.SLDDRW",
    r"D:\Winnsen_Structure_Agent_Studio\data\production_orders_20261029\continuation_sol_20261029_r5\drawing_batch_prepare_r59\runs\run_r1\13_base_底座底板\底座底板_R59.SLDDRW",
]

# 输出目录
OUTPUT_DIR = r"C:\Users\Administrator\Desktop\delivery_r59_parts_RELEASED"
BACKUP_DIR = r"D:\Winnsen_Structure_Agent_Studio\data\production_orders_20261029\continuation_sol_20261029_r5\drawing_batch_prepare_r59\backup_r59_original"

# R5 Released 配置
R5_CONFIG = {
    'designer': 'Eli',
    'drafter': 'Eli',
    'checker': 'Eli',
    'reviewer': 'Eli',
    'approver': 'Eli',
    'date': '2026-11-02',
    'revision': 'R5',
}

# 统计
stats = {
    'total': 0,
    'success': 0,
    'failed': 0,
    'errors': []
}

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
        sys.exit(1)

def update_drawing_properties(sw_model):
    """更新图纸的自定义属性"""
    try:
        # 获取自定义属性管理器
        custom_prop_mgr = sw_model.Extension.CustomPropertyManager("")

        if not custom_prop_mgr:
            return False, "无法获取自定义属性管理器"

        # 属性映射表
        property_updates = {
            # 签字栏
            'DrawnBy': R5_CONFIG['drafter'],
            'CheckedBy': R5_CONFIG['checker'],
            'EngineeringApproval': R5_CONFIG['reviewer'],
            'ManufacturingApproval': R5_CONFIG['approver'],
            'DesignedBy': R5_CONFIG['designer'],

            # 日期
            'DrawnDate': R5_CONFIG['date'],
            'CheckedDate': R5_CONFIG['date'],
            'EngineeringDate': R5_CONFIG['date'],
            'ManufacturingDate': R5_CONFIG['date'],

            # 版本
            'Revision': R5_CONFIG['revision'],
        }

        updated_count = 0

        # 更新每个属性
        for prop_name, new_value in property_updates.items():
            try:
                # 先尝试直接设置
                ret = custom_prop_mgr.Set2(prop_name, new_value)
                if ret == 0:  # 成功
                    updated_count += 1
                else:
                    # 属性不存在，添加新属性
                    custom_prop_mgr.Add3(prop_name, 30, new_value, 2)  # 30=swCustomInfoText
                    updated_count += 1

            except Exception as e:
                # 单个属性失败不影响其他属性
                pass

        if updated_count > 0:
            print(f"    ✓ 更新了 {updated_count} 个属性")
            return True, None
        else:
            return False, "没有属性被更新"

    except Exception as e:
        return False, str(e)

def process_drawing_file(sw, file_path, index, total):
    """处理单个图纸文件"""
    file_name = os.path.basename(file_path)

    print(f"\n{'='*70}")
    print(f"[{index}/{total}] {file_name}")
    print(f"{'='*70}")

    # 检查文件是否存在
    if not os.path.exists(file_path):
        print(f"  ❌ 文件不存在")
        stats['failed'] += 1
        stats['errors'].append({
            'file': file_name,
            'error': '文件不存在',
            'timestamp': datetime.now().isoformat()
        })
        return False

    # 备份原文件
    print(f"  [1/5] 备份原文件...")
    try:
        os.makedirs(BACKUP_DIR, exist_ok=True)
        backup_path = os.path.join(BACKUP_DIR, file_name)
        shutil.copy2(file_path, backup_path)
        print(f"    ✓ 已备份到: {backup_path}")
    except Exception as e:
        print(f"    ⚠️  备份失败: {str(e)}")

    try:
        # 打开图纸
        print(f"  [2/5] 打开图纸...")

        sw_model = sw.OpenDoc(file_path, 3)  # 3 = swDocDRAWING

        if not sw_model:
            error_msg = f"无法打开文件"
            print(f"    ❌ {error_msg}")
            stats['errors'].append({
                'file': file_name,
                'error': error_msg,
                'timestamp': datetime.now().isoformat()
            })
            stats['failed'] += 1
            return False

        print(f"    ✓ 图纸已打开")

        # 更新自定义属性
        print(f"  [3/5] 更新自定义属性...")
        success_prop, error_prop = update_drawing_properties(sw_model)

        if not success_prop:
            print(f"    ⚠️  属性更新失败: {error_prop}")

        # 先保存属性更新
        print(f"  [4/5] 保存属性...")
        sw_model.Save3(1, 0, 0)  # 1=Silent, 0=errors, 0=warnings
        print(f"    ✓ 已保存")

        # 生成新文件名
        new_file_name = file_name.replace("_R59.SLDDRW", "_R5_RELEASED.SLDDRW")
        new_file_path = file_path.replace("_R59.SLDDRW", "_R5_RELEASED.SLDDRW")

        # 另存为新文件
        print(f"  [5/5] 另存为 {new_file_name} 并导出PDF...")
        sw_model.SaveAs3(new_file_path, 0, 0)
        print(f"    ✓ SLDDRW已保存")

        # 关闭旧文件，打开新文件以导出PDF
        sw.CloseDoc(file_path)
        time.sleep(0.3)

        sw_model = sw.OpenDoc(new_file_path, 3)
        if not sw_model:
            raise Exception("无法重新打开文件")

        # 导出PDF
        os.makedirs(OUTPUT_DIR, exist_ok=True)
        pdf_name = new_file_name.replace(".SLDDRW", ".pdf")
        pdf_path = os.path.join(OUTPUT_DIR, pdf_name)

        pdf_success = sw_model.Extension.SaveAs(
            pdf_path,
            0,    # 保存为新名称
            1,    # swSaveAsOptions_Silent
            None, # 导出数据
            0,    # 错误
            0     # 警告
        )

        if pdf_success and os.path.exists(pdf_path):
            print(f"    ✓ PDF已导出: {pdf_name}")
        else:
            print(f"    ⚠️  PDF未生成")

        # 关闭文档
        sw.CloseDoc(new_file_path)

        if success_prop and save_success:
            stats['success'] += 1
            print(f"  ✅ 完成 ({stats['success']}/{total})")
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

def main():
    print("=" * 70)
    print("  批量更新 R59 图纸为 R5 RELEASED 格式")
    print("=" * 70)
    print()
    print(f"输入文件: 13 个 R59 SLDDRW")
    print(f"输出目录: {OUTPUT_DIR}")
    print(f"备份目录: {BACKUP_DIR}")
    print()
    print("R5 格式配置:")
    for key, value in R5_CONFIG.items():
        print(f"  {key}: {value}")
    print()

    stats['total'] = len(R59_FILES)

    print(f"✓ 找到 {stats['total']} 个文件")
    print()

    # 创建输出目录
    os.makedirs(OUTPUT_DIR, exist_ok=True)
    os.makedirs(BACKUP_DIR, exist_ok=True)

    # 初始化SolidWorks
    sw = init_solidworks()

    # 处理每个文件
    print()
    print("=" * 70)
    print(f"开始批量处理（共 {stats['total']} 个文件）")
    print("=" * 70)

    start_time = time.time()

    try:
        for i, file_path in enumerate(R59_FILES, 1):
            process_drawing_file(sw, file_path, i, stats['total'])

            # 每处理3个文件休息一下
            if i % 3 == 0 and i < stats['total']:
                print("\n[休息] 2秒...")
                time.sleep(2)

    except KeyboardInterrupt:
        print("\n\n⚠️  用户中断！")

    finally:
        # 清理
        try:
            # 不要退出SW，只是断开连接
            pass
        except:
            pass

        pythoncom.CoUninitialize()

    # 统计
    elapsed_time = time.time() - start_time
    print("\n")
    print("=" * 70)
    print("  批量更新完成")
    print("=" * 70)
    print()
    print(f"总文件数:    {stats['total']}")
    print(f"成功更新:    {stats['success']} ✓")
    print(f"失败:        {stats['failed']} ❌")
    print(f"用时:        {elapsed_time/60:.1f} 分钟")
    print()

    if stats['success'] > 0:
        print(f"✅ 成功率: {stats['success']/stats['total']*100:.1f}%")

    if stats['errors']:
        print(f"\n⚠️  有 {len(stats['errors'])} 个错误:")
        for err in stats['errors']:
            print(f"  - {err['file']}: {err['error']}")

    print()
    print(f"输出目录: {OUTPUT_DIR}")
    print(f"备份目录: {BACKUP_DIR}")
    print()

if __name__ == "__main__":
    try:
        main()
    except Exception as e:
        print(f"\n❌ 严重错误: {str(e)}")
        import traceback
        traceback.print_exc()
        sys.exit(1)
