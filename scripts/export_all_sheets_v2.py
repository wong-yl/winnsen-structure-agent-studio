# 新版本导出脚本：尝试导出所有sheets
import win32com.client
import os
import json

# 获取SolidWorks应用
sw = win32com.client.Dispatch("SolidWorks.Application")
sw.Visible = False

# 关键配置
ROOT_DIR = r"D:\参数化试验出图1\16029 寄存柜(1200×1917×485) 左12右13门"
DWG_DIR = os.path.join(ROOT_DIR, "1.工程图")
PDF_DIR = os.path.join(ROOT_DIR, "4.PDF图纸")

# 要导出的原工程图
export_configs = [
    ("门板", "door_panel_left_H316_R5.SLDDRW", "01-门板"),
    ("门框", "门框 上_R59.SLDDRW", "02-门框"),
    ("箱体", "箱体竖隔板R_R59.SLDDRW", "03-箱体"),
    ("底座", "底座底板_R59.SLDDRW", "04-底座"),
    ("上盖", "上盖壳体前侧板_R55.SLDDRW", "05-上盖"),
]

results = []

for category, filename, pdf_subdir in export_configs:
    dwg_path = os.path.join(DWG_DIR, category, filename)
    pdf_subpath = os.path.join(PDF_DIR, pdf_subdir)
    os.makedirs(pdf_subpath, exist_ok=True)
    pdf_path = os.path.join(pdf_subpath, filename.replace(".SLDDRW", ".pdf"))

    if not os.path.exists(dwg_path):
        results.append({
            "category": category,
            "file": filename,
            "status": "ERROR",
            "error": "SLDDRW文件不存在"
        })
        continue

    try:
        print(f"打开: {filename}")
        doc = sw.OpenDoc(dwg_path, 3)  # 3 = swDocDRAWING

        # 尝试方法1: 使用Extension.SaveAs与PDF选项
        # swSaveAsPDFOptions用位域表示各种PDF导出选项
        # 我们需要找到"导出所有sheets"的位

        # 首先获取document的sheets信息
        try:
            # 获取SheetNames
            sheet_names = doc.GetSheetNames()
            print(f"  发现 {len(sheet_names)} 个sheets: {sheet_names}")
        except Exception as e:
            print(f"  无法获取sheet names: {e}")

        # 尝试不同的保存方法
        # 方法1: SaveAs3 - 标准SolidWorks导出
        success = False
        try:
            print(f"  尝试方法1: SaveAs3...")
            ret = doc.SaveAs3(pdf_path, 0, 2)  # swSaveAsPDFOptions = 2 (?)
            file_size = os.path.getsize(pdf_path) if os.path.exists(pdf_path) else 0
            print(f"    返回值: {ret}, 文件大小: {file_size} 字节")

            # 检查是否是完整导出
            if file_size > 100000:  # 大于100KB表示可能是完整的
                success = True
            else:
                print(f"    文件太小({file_size}字节)，可能不是完整导出")
        except Exception as e:
            print(f"    SaveAs3失败: {e}")

        # 方法2: 如果SaveAs3没工作，尝试Extension.SaveAs
        if not success:
            try:
                print(f"  尝试方法2: Extension.SaveAs...")
                # PDF选项常数
                swSaveAsPDFOptions = 1  # 基础PDF导出
                ret = doc.Extension.SaveAs(pdf_path, 0, swSaveAsPDFOptions, None, None, None)
                file_size = os.path.getsize(pdf_path) if os.path.exists(pdf_path) else 0
                print(f"    返回值: {ret}, 文件大小: {file_size} 字节")
                success = True
            except Exception as e:
                print(f"    Extension.SaveAs失败: {e}")

        if success and os.path.exists(pdf_path):
            file_size = os.path.getsize(pdf_path)
            results.append({
                "category": category,
                "file": filename,
                "status": "OK",
                "pdf_size": file_size,
                "pdf_path": pdf_path
            })
            print(f"  ✓ 导出成功: {pdf_path} ({file_size} 字节)")
        else:
            results.append({
                "category": category,
                "file": filename,
                "status": "FAILED",
                "error": "导出方法均失败"
            })
            print(f"  ✗ 导出失败")

        sw.CloseDoc(dwg_path)

    except Exception as e:
        results.append({
            "category": category,
            "file": filename,
            "status": "ERROR",
            "error": str(e)
        })
        print(f"  ✗ 异常: {e}")
        sw.CloseAllDocuments()

print("\n" + "="*60)
print("导出总结:")
print(json.dumps(results, indent=2, ensure_ascii=False))

sw.CloseAllDocuments()
