#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
批量更新SLDDRW图纸属性为R5格式
更新签字栏、日期、版本信息
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
BACKUP_DIR = os.path.join(ORDER_DIR, ".backup_before_r5_update")
LOG_FILE = os.path.join(ORDER_DIR, f"图纸属性更新日志_{datetime.now().strftime('%Y%m%d_%H%M%S')}.json")

# R5 格式配置
R5_CONFIG = {
    'designer': 'Eli',
    'drafter': 'Eli',
    'checker': 'Eli',
    'reviewer': 'Eli',
    'approver': 'Eli',
    'date': '2026-11-02',
    'revision': 'R5',
    'note': 'R5 本单变型 / Eli 签审',
    'header_text': 'R5 / Eli 签审',
    'remark': '按生产工艺出图 / 免样门验证（Eli 2026-11-02）'
}

# 统计
stats = {
    'total': 0,
    'success': 0,
    'skipped': 0,
    'failed': 0,
    'errors': []
}

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
        sys.exit(1)

def backup_file(file_path):
    """备份文件"""
    try:
        os.makedirs(BACKUP_DIR, exist_ok=True)
        rel_path = os.path.relpath(file_path, INPUT_DIR)
        backup_path = os.path.join(BACKUP_DIR, rel_path)
        os.makedirs(os.path.dirname(backup_path), exist_ok=True)

        import shutil
        shutil.copy2(file_path, backup_path)
        return True, backup_path
    except Exception as e:
        return False, str(e)

def update_drawing_properties(sw, sw_model, file_name):
    """更新图纸的自定义属性"""
    try:
        # 获取自定义属性管理器
        custom_prop_mgr = sw_model.Extension.CustomPropertyManager("")

        if not custom_prop_mgr:
            return False, "无法获取自定义属性管理器"

        # 属性映射表（旧属性名 -> 新值）
        # 根据实际图纸模板的属性名调整
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

            # 版本和备注
            'Revision': R5_CONFIG['revision'],
            'Description': R5_CONFIG['note'],
            'Comments': R5_CONFIG['remark'],

            # 可能的R3前缀属性（根据PROGRESS文件）
            'R3_DrawnBy': R5_CONFIG['drafter'],
            'R3_CheckedBy': R5_CONFIG['checker'],
            'R3_EngineeringApproval': R5_CONFIG['reviewer'],
            'R3_DesignedBy': R5_CONFIG['designer'],
            'R3_DrawnDate': R5_CONFIG['date'],
            'R3_Revision': R5_CONFIG['revision'],
            'R3_Description': R5_CONFIG['note'],
        }

        updated_count = 0

        # 更新每个属性
        for prop_name, new_value in property_updates.items():
            try:
                # 检查属性是否存在
                val_out = ""
                resolved = False
                was_resolved = False

                ret = custom_prop_mgr.Get5(prop_name, False, val_out, resolved, was_resolved)

                if ret == 0:  # 属性存在
                    # 设置新值
                    custom_prop_mgr.Set2(prop_name, new_value)
                    updated_count += 1
                else:
                    # 属性不存在，添加新属性
                    custom_prop_mgr.Add3(prop_name, 30, new_value, 2)  # 30=swCustomInfoText
                    updated_count += 1

            except Exception as e:
                # 单个属性失败不影响其他属性
                pass

        if updated_count > 0:
            print(f"  ✓ 更新了 {updated_count} 个属性")
            return True, None
        else:
            return False, "没有属性被更新"

    except Exception as e:
        return False, str(e)

def update_sheet_format_notes(sw_model):
    """更新图纸格式中的注释文本"""
    try:
        # 获取第一个图纸
        sheet = sw_model.GetFirstSheet()
        if not sheet:
            return False, "无法获取图纸"

        # 这里可以添加更新图纸格式中特定文本的代码
        # 例如更新标题栏中的文字
        # 注意：直接修改图纸格式比较复杂，通常通过自定义属性链接实现

        return True, None

    except Exception as e:
        return False, str(e)

def process_drawing_file(sw, file_path, backup_enabled=True):
    """处理单个图纸文件"""
    file_name = os.path.basename(file_path)

    print(f"\n[处理] {file_name}")

    # 备份（可选）
    if backup_enabled:
        print(f"  [1/4] 备份原文件...")
        success, backup_path = backup_file(file_path)
        if success:
            print(f"  ✓ 已备份")
        else:
            print(f"  ⚠️  备份失败: {backup_path}")

    try:
        # 打开图纸
        print(f"  [2/4] 打开图纸...")
        # OpenDoc6 需要传引用参数，使用数组包装
        errors = [0]
        warnings = [0]

        sw_model = sw.OpenDoc6(
            file_path,
            3,  # swDocDRAWING
            2,  # swOpenDocOptions_LoadModel (加载引用模型)
            "",
            errors[0],
            warnings[0]
        )

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

        # 更新自定义属性
        print(f"  [3/4] 更新自定义属性...")
        success_prop, error_prop = update_drawing_properties(sw, sw_model, file_name)

        if not success_prop:
            print(f"  ⚠️  属性更新失败: {error_prop}")

        # 更新图纸格式注释（可选）
        # success_fmt, error_fmt = update_sheet_format_notes(sw_model)

        # 保存文件
        print(f"  [4/4] 保存文件...")
        save_success = sw_model.Save3(
            1,  # swSaveAsOptions_Silent
            0,  # errors
            0   # warnings
        )

        if save_success:
            print(f"  ✓ 保存成功")
        else:
            print(f"  ⚠️  保存可能失败")

        # 关闭文档
        sw.CloseDoc(file_path)

        if success_prop:
            stats['success'] += 1
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
        'operation': '批量更新图纸属性为R5格式',
        'timestamp': datetime.now().isoformat(),
        'r5_config': R5_CONFIG,
        'statistics': stats,
        'success_rate': f"{(stats['success']/stats['total']*100) if stats['total']>0 else 0:.1f}%",
    }

    with open(LOG_FILE, 'w', encoding='utf-8') as f:
        json.dump(log_data, f, ensure_ascii=False, indent=2)

    print(f"\n日志已保存：{LOG_FILE}")

def main():
    print("=" * 70)
    print("  批量更新图纸属性为R5格式")
    print("=" * 70)
    print()
    print(f"订单目录: {ORDER_DIR}")
    print(f"工程图目录: {INPUT_DIR}")
    print(f"备份目录: {BACKUP_DIR}")
    print()
    print("R5 格式配置:")
    for key, value in R5_CONFIG.items():
        print(f"  {key}: {value}")
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

    # 创建备份目录
    os.makedirs(BACKUP_DIR, exist_ok=True)

    # 自动确认（用于后台运行）
    print("⚠️  警告：此操作将修改所有工程图文件")
    print("   原文件会先备份到 .backup_before_r5_update/ 目录")
    print()
    print("✓ 自动开始（后台模式）")
    print()

    # 初始化SolidWorks
    sw = init_solidworks()

    # 处理每个文件
    print()
    print("=" * 70)
    print(f"开始批量更新（共{stats['total']}个文件）")
    print("=" * 70)

    start_time = time.time()

    try:
        for i, file_path in enumerate(slddrw_files, 1):
            print(f"\n{'='*70}")
            print(f"进度: {i}/{stats['total']} ({i/stats['total']*100:.1f}%)")
            print(f"文件: {os.path.basename(file_path)}")
            print(f"{'='*70}")

            process_drawing_file(sw, file_path, backup_enabled=True)

            # 每处理5个文件休息一下
            if i % 5 == 0:
                print("\n[休息] 3秒...")
                time.sleep(3)

    except KeyboardInterrupt:
        print("\n\n⚠️  用户中断！")

    finally:
        # 清理
        try:
            sw.ExitApp()
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
    print(f"跳过:        {stats['skipped']}")
    print(f"失败:        {stats['failed']} ❌")
    print(f"用时:        {elapsed_time/60:.1f} 分钟")
    print()

    if stats['success'] > 0:
        print(f"✅ 成功率: {stats['success']/stats['total']*100:.1f}%")

    if stats['errors']:
        print(f"\n⚠️  有 {len(stats['errors'])} 个错误，详见日志文件")

    # 保存日志
    save_log()

    print()
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
