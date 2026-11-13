#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""读取 SLDDRW 工程图的自定义属性（签字栏），只读不修改"""
import sys, io, os, json
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')

import win32com.client
import pythoncom

ORDER_DIR = r"D:\参数化试验出图1\16029 寄存柜(1200×1917×485) 左12右13门"
DWG_DIR = os.path.join(ORDER_DIR, "1.工程图")

TARGETS = [
    ("门板", "门板/door_panel_left_H316_R5.SLDDRW"),
    ("门框", "门框/门框 左_R59.SLDDRW"),
    ("箱体", "箱体/箱体竖隔板R_R51.SLDDRW"),
    ("底座", "底座/底座底板_R59.SLDDRW"),
    ("上盖", "上盖/上盖壳体前侧板_R55.SLDDRW"),
    ("门配件", "门配件/door_stiffener_W537_H130_R59.SLDDRW"),
]

def dump_props(doc):
    out = {}
    try:
        ci = doc.CustomInfo2
        if ci is None:
            return {"_error": "CustomInfo2 is None"}
        for i in range(0, ci.Count):
            try:
                key = ci.GetKey(i)
                val = ci.GetValue(i)
                out[key] = val
            except Exception as e:
                out["_idx%d" % i] = "ERR %s" % e
    except Exception as e:
        out["_error"] = str(e)
    return out

def main():
    pythoncom.CoInitialize()
    sw = win32com.client.Dispatch("SldWorks.Application")
    sw.Visible = True

    results = []
    for cat, rel in TARGETS:
        path = os.path.join(DWG_DIR, rel.replace("/", os.sep))
        entry = {"category": cat, "file": rel, "exists": os.path.exists(path)}
        if not entry["exists"]:
            results.append(entry)
            continue
        try:
            doc = sw.OpenDoc(path, 3)
            if not doc:
                entry["status"] = "OPEN_FAILED"
                results.append(entry)
                continue
            entry["status"] = "OK"
            entry["custom_info2"] = dump_props(doc)
            sw.CloseDoc(path)
        except Exception as e:
            entry["status"] = "ERROR"
            entry["error"] = str(e)
        results.append(entry)

    print(json.dumps(results, ensure_ascii=False, indent=2, default=str))

    try:
        sw.CloseAllDocuments(True)
    except Exception:
        pass
    pythoncom.CoUninitialize()
    print("\nsessionExited=true (SolidWorks kept running, no files modified)")

if __name__ == "__main__":
    main()
