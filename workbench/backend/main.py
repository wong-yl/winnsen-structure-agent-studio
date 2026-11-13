"""
Winnsen工作台后端 - FastAPI应用入口
运行: uvicorn main:app --reload --host 0.0.0.0 --port 8000
"""

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from pathlib import Path
import json

app = FastAPI(
    title="Winnsen订单工作台API",
    description="16029订单零件管理、批次追溯、质量检查系统",
    version="1.0.0"
)

# 跨域配置
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],  # 生产环境改为具体域名
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# 项目配置
PROJECT_ROOT = Path(r"D:/参数化试验出图1/16029 寄存柜(1200×1917×485) 左12右13门")
CONFIG_FILE = PROJECT_ROOT / "project_config.json"


@app.get("/")
async def root():
    return {
        "message": "Winnsen订单工作台API",
        "version": "1.0.0",
        "docs": "/docs"
    }


@app.get("/api/project/info")
async def get_project_info():
    """获取项目基本信息"""
    try:
        with open(CONFIG_FILE, 'r', encoding='utf-8') as f:
            config = json.load(f)
        return {
            "success": True,
            "data": config
        }
    except Exception as e:
        return {
            "success": False,
            "error": str(e)
        }


@app.get("/api/parts")
async def get_parts(category: str = None, batch: str = None):
    """获取零件列表"""
    # 从CSV读取（简化版，后续用数据库）
    import csv

    csv_path = PROJECT_ROOT / "零件清单_数据库种子.csv"
    parts = []

    with open(csv_path, 'r', encoding='utf-8') as f:
        reader = csv.DictReader(f)
        for row in reader:
            # 筛选
            if category and row['部件类别'] != category:
                continue
            if batch and row['批次号'] != batch:
                continue

            parts.append(row)

    return {
        "success": True,
        "total": len(parts),
        "data": parts
    }


@app.get("/api/parts/{part_number}")
async def get_part_detail(part_number: str):
    """获取零件详情"""
    import csv

    csv_path = PROJECT_ROOT / "零件清单_数据库种子.csv"

    with open(csv_path, 'r', encoding='utf-8') as f:
        reader = csv.DictReader(f)
        for row in reader:
            if row['零件图号'] == part_number:
                return {
                    "success": True,
                    "data": row
                }

    return {
        "success": False,
        "error": "零件未找到"
    }


@app.get("/api/statistics/summary")
async def get_statistics():
    """获取统计汇总"""
    import csv

    csv_path = PROJECT_ROOT / "零件清单_数据库种子.csv"
    categories = {}
    batches = {}

    with open(csv_path, 'r', encoding='utf-8') as f:
        reader = csv.DictReader(f)
        for row in reader:
            # 按类别统计
            cat = row['部件类别']
            categories[cat] = categories.get(cat, 0) + 1

            # 按批次统计
            batch = row['批次号']
            batches[batch] = batches.get(batch, 0) + 1

    # 文件统计
    drawing_count = len(list((PROJECT_ROOT / "1.工程图").rglob("*.SLDDRW")))
    pdf_count = len(list((PROJECT_ROOT / "4.PDF图纸").rglob("*.pdf")))
    dxf_count = len(list((PROJECT_ROOT / "2.钣金展开图/DXF").glob("*.DXF")))

    return {
        "success": True,
        "data": {
            "by_category": categories,
            "by_batch": batches,
            "files": {
                "slddrw": drawing_count,
                "pdf": pdf_count,
                "dxf": dxf_count
            },
            "completion": {
                "drawings": 100,
                "pdf": 94,  # 31/33
                "dxf": round(dxf_count / drawing_count * 100, 1) if drawing_count > 0 else 0
            }
        }
    }


@app.get("/api/quality/checks")
async def get_quality_checks():
    """获取质量检查结果"""
    with open(CONFIG_FILE, 'r', encoding='utf-8') as f:
        config = json.load(f)

    quality_data = config.get('quality_checks', {})

    return {
        "success": True,
        "data": {
            "missing_flat_patterns": quality_data.get('missing_flat_patterns', False),
            "duplicate_annotations": quality_data.get('duplicate_annotations', []),
            "todo_items": quality_data.get('todo_items', []),
            "checks_passed": not quality_data.get('missing_flat_patterns', False)
        }
    }


if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8000)
