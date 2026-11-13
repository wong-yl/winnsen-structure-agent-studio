import win32com.client
import json

sw = win32com.client.Dispatch("SolidWorks.Application")
sw.Visible = True

# 检查几个关键文件的属性
files_to_check = [
    r"D:\参数化试验出图1\16029 寄存柜(1200×1917×485) 左12右13门\1.工程图\门板\door_panel_left_H316_R5.SLDDRW",
    r"D:\参数化试验出图1\16029 寄存柜(1200×1917×485) 左12右13门\1.工程图\门框\门框 上_R59.SLDDRW",
    r"D:\参数化试验出图1\16029 寄存柜(1200×1917×485) 左12右13门\1.工程图\箱体\箱体竖隔板R_R59.SLDDRW",
]

results = []
for dwg_path in files_to_check:
    try:
        doc = sw.OpenDoc(dwg_path, 3)

        # 尝试读取自定义属性
        cust_props = doc.CustomInfo2("", 0)  # 第一个参数是configuration name（空表示通用）

        # 提取常用属性
        props_dict = {}
        for i in range(len(cust_props)):
            prop_name = cust_props[i]
            prop_val = doc.GetCustomInfoValue("", prop_name)
            props_dict[prop_name] = prop_val

        results.append({
            "file": dwg_path.split("\\")[-1],
            "props": props_dict
        })

        sw.CloseDoc(dwg_path)
    except Exception as e:
        results.append({
            "file": dwg_path.split("\\")[-1],
            "error": str(e)
        })

print(json.dumps(results, indent=2, ensure_ascii=False))
sw.CloseAllDocuments()
