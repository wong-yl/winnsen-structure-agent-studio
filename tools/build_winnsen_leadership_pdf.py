from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageDraw, ImageFont
from reportlab.lib import colors
from reportlab.lib.pagesizes import A4, landscape
from reportlab.lib.utils import ImageReader
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.pdfgen import canvas

from build_winnsen_progress_report import (
    BLUE,
    DOCS,
    GREEN,
    LIGHT_BLUE,
    LIGHT_GRAY,
    LIGHT_ORANGE,
    MODEL_DIR,
    MUTED,
    ORANGE,
    ROOT,
    TEXT,
    ProjectData,
    font_path,
    load_data,
    make_flow_diagram,
    make_model_schematic,
    make_roadmap,
    make_validation_panel,
)


OUT_PDF = DOCS / "Winnsen结构智能体项目领导汇报_20260519.pdf"
ASSET_DIR = DOCS / "report_assets" / "20260519_leadership"
UI_SCREENSHOT = ROOT / "apps" / "web" / "docs" / "design" / "qa" / "desktop-dual-cad-entries-panel.png"


def register_pdf_font() -> str:
    pdfmetrics.registerFont(TTFont("WinnsenSC", str(font_path())))
    return "WinnsenSC"


def hex_color(value: str) -> colors.Color:
    if value.lower() == "white":
        return colors.white
    if value.lower() == "black":
        return colors.black
    return colors.HexColor(value)


def wrap_text(c: canvas.Canvas, text: str, font_name: str, font_size: float, max_width: float) -> list[str]:
    lines: list[str] = []
    for paragraph in str(text).splitlines() or [""]:
        current = ""
        for char in paragraph:
            candidate = current + char
            if c.stringWidth(candidate, font_name, font_size) <= max_width:
                current = candidate
            else:
                if current:
                    lines.append(current)
                current = char
        if current:
            lines.append(current)
    return lines


def draw_text(
    c: canvas.Canvas,
    x: float,
    y: float,
    text: str,
    font_name: str,
    font_size: float,
    color: str = TEXT,
    max_width: float | None = None,
    leading: float | None = None,
) -> float:
    c.setFont(font_name, font_size)
    c.setFillColor(hex_color(color))
    leading = leading or font_size * 1.45
    lines = wrap_text(c, text, font_name, font_size, max_width) if max_width else str(text).splitlines()
    for line in lines:
        c.drawString(x, y, line)
        y -= leading
    return y


def draw_header(c: canvas.Canvas, page: int, title: str, font_name: str) -> None:
    width, height = landscape(A4)
    c.setFillColor(hex_color(BLUE))
    c.rect(0, height - 34, width, 34, fill=True, stroke=False)
    c.setFont(font_name, 9.5)
    c.setFillColor(colors.white)
    c.drawString(34, height - 23, "Winnsen Structure Agent Studio | 项目汇报")
    c.drawRightString(width - 34, height - 23, f"{page:02d} / {title}")


def add_image(c: canvas.Canvas, path: Path, x: float, y: float, w: float, h: float) -> None:
    if not path.exists():
        return
    img = Image.open(path)
    iw, ih = img.size
    scale = min(w / iw, h / ih)
    rw, rh = iw * scale, ih * scale
    c.drawImage(ImageReader(str(path)), x + (w - rw) / 2, y + (h - rh) / 2, width=rw, height=rh, mask="auto")


def make_clean_platform_image(dst: Path) -> Path:
    img = Image.open(UI_SCREENSHOT).convert("RGB")
    # Remove the right task-log panel from the screenshot so the leadership PDF
    # focuses on capability rather than noisy generation records.
    crop = img.crop((0, 0, min(980, img.width), img.height))
    canvas_img = Image.new("RGB", (1200, 720), "white")
    crop.thumbnail((1120, 660), Image.Resampling.LANCZOS)
    canvas_img.paste(crop, ((1200 - crop.width) // 2, (720 - crop.height) // 2))
    canvas_img.save(dst, quality=92)
    return dst


def make_rule_table_image(dst: Path, data: ProjectData) -> Path:
    w, h = 1600, 820
    img = Image.new("RGB", (w, h), "white")
    d = ImageDraw.Draw(img)
    title_font = ImageFont.truetype(str(font_path()), 46)
    body_font = ImageFont.truetype(str(font_path()), 25)
    small_font = ImageFont.truetype(str(font_path()), 22)
    d.text((70, 55), "当前已沉淀的 16029 10门规则种子", font=title_font, fill=BLUE)
    rows = [
        ("外形目标", "1000W x 1917H x 550D", "同外形门数变化的第一组样本"),
        ("门数布局", f"{data.door_count}门 / 2列 x {data.rows_per_column}行", "用于推导 12/14 门对照规则"),
        ("单门尺寸", f"{data.door_width:.0f}W x {data.door_height:.0f}H mm", "门高随门数变化重算"),
        ("门距", f"{data.door_pitch:.0f} mm", "驱动门板、层板、横隔、锁位"),
        ("数量校验", "10门模块 / 10锁孔 / 8层板 / 8横隔", "基础数量关系已通过"),
        ("几何完整性", "invalid shape = 0", "FCStd 基础完整性检查通过"),
    ]
    x0, y0 = 80, 150
    col_w = [260, 420, 700]
    row_h = 84
    headers = ["规则项", "当前值", "工程意义"]
    x = x0
    d.rectangle((x0, y0, x0 + sum(col_w), y0 + row_h), fill=BLUE)
    for i, header in enumerate(headers):
        d.text((x + 24, y0 + 26), header, font=body_font, fill="white")
        x += col_w[i]
    y = y0 + row_h
    for idx, row in enumerate(rows):
        fill = "#F7F9FC" if idx % 2 == 0 else "white"
        d.rectangle((x0, y, x0 + sum(col_w), y + row_h), fill=fill, outline="#D6DEE9")
        x = x0
        for i, value in enumerate(row):
            d.text((x + 24, y + 25), value, font=small_font, fill=TEXT if i != 1 else BLUE)
            x += col_w[i]
        y += row_h
    d.rounded_rectangle((80, 700, 1520, 770), radius=18, fill=LIGHT_ORANGE)
    d.text((110, 722), "结论：当前不是在提前做所有变体，而是在用代表样本提炼可复用生成规则。", font=small_font, fill=TEXT)
    img.save(dst, quality=94)
    return dst


def prepare_assets(data: ProjectData) -> dict[str, Path]:
    ASSET_DIR.mkdir(parents=True, exist_ok=True)
    return {
        "platform": make_clean_platform_image(ASSET_DIR / "platform_project_only.jpg"),
        "flow": make_flow_diagram(ASSET_DIR / "flow_project_only.jpg"),
        "model": make_model_schematic(ASSET_DIR / "model_project_only.jpg", data),
        "validation": make_validation_panel(ASSET_DIR / "validation_project_only.jpg", data),
        "roadmap": make_roadmap(ASSET_DIR / "roadmap_project_only.jpg"),
        "rules": make_rule_table_image(ASSET_DIR / "rules_project_only.jpg", data),
    }


def card(c: canvas.Canvas, x: float, y: float, w: float, h: float, title: str, body: str, font: str, fill: str = LIGHT_GRAY) -> None:
    c.setFillColor(hex_color(fill))
    c.setStrokeColor(hex_color("#D6DEE9"))
    c.roundRect(x, y, w, h, 10, fill=True, stroke=True)
    draw_text(c, x + 16, y + h - 30, title, font, 14, BLUE)
    draw_text(c, x + 16, y + h - 58, body, font, 10.5, MUTED, max_width=w - 32, leading=16)


def table(c: canvas.Canvas, x: float, y: float, rows: list[list[str]], widths: list[float], font: str) -> None:
    row_h = 36
    for r, row in enumerate(rows):
        fill = BLUE if r == 0 else ("#F7F9FC" if r % 2 else "white")
        text_color = "white" if r == 0 else TEXT
        c.setFillColor(hex_color(fill))
        c.rect(x, y - row_h, sum(widths), row_h, fill=True, stroke=False)
        cx = x
        for value, width in zip(row, widths):
            c.setStrokeColor(hex_color("#D6DEE9"))
            c.rect(cx, y - row_h, width, row_h, fill=False, stroke=True)
            draw_text(c, cx + 8, y - 23, value, font, 9.5, text_color, max_width=width - 16, leading=13)
            cx += width
        y -= row_h


def build_pdf() -> None:
    DOCS.mkdir(parents=True, exist_ok=True)
    data = load_data()
    assets = prepare_assets(data)
    font = register_pdf_font()
    c = canvas.Canvas(str(OUT_PDF), pagesize=landscape(A4))
    width, height = landscape(A4)
    margin = 42

    # 1 cover
    c.setFillColor(colors.white)
    c.rect(0, 0, width, height, fill=True, stroke=False)
    c.setFillColor(hex_color(BLUE))
    c.rect(0, height - 72, width, 72, fill=True, stroke=False)
    c.setFillColor(hex_color(ORANGE))
    c.rect(0, height - 82, width, 10, fill=True, stroke=False)
    draw_text(c, margin, height - 46, "Winnsen Structure Agent Studio", font, 17, "white")
    draw_text(c, margin, height - 155, "硬件结构知识与钣金模型生成平台 MVP", font, 30, BLUE)
    draw_text(c, margin, height - 215, "项目内容及进度汇报", font, 46, TEXT)
    draw_text(c, margin, height - 260, "面向领导汇报 | 只呈现项目目标、当前成果、真实数据、风险边界与下一步计划", font, 15, MUTED)
    card(c, margin, 78, 225, 120, "当前阶段", "已完成 16029 10门 FreeCAD 规则参考模型，并通过基础校验。", font, LIGHT_BLUE)
    card(c, margin + 250, 78, 225, 120, "工程价值", "沉淀门数变化规则，减少结构工程师重复建模和前期判断时间。", font)
    card(c, margin + 500, 78, 225, 120, "下一步", "生成 12/14 门对照样本，形成同外形门数变化规则表。", font, LIGHT_ORANGE)
    draw_text(c, margin, 36, "2026-05-19", font, 11, MUTED)
    c.showPage()

    # 2 summary
    draw_header(c, 2, "阶段总览", font)
    draw_text(c, margin, height - 88, "3 分钟汇报结论", font, 27, BLUE)
    draw_text(c, margin, height - 128, "项目已经从页面原型推进到“有一个可校验规则生成模型”的阶段；后续不再堆模型库存，而是用代表样本提炼可复用参数化规则。", font, 16, TEXT, max_width=width - 2 * margin, leading=24)
    rows = [
        ["维度", "当前结论"],
        ["业务问题", "标准模型多、尺寸变体多、重复建模耗时，结构规则难复用。"],
        ["当前成果", f"16029 / 1000W x 1917H x 550D / {data.door_count}门工程参考模型已生成并校验通过。"],
        ["技术路线", "FreeCAD 先验证规则；SolidWorks 作为工程交接和复核通道。"],
        ["项目边界", "当前不是生产图纸；正式 SolidWorks 图纸、DXF、BOM 仍需工程流程放行。"],
    ]
    table(c, margin, height - 185, rows, [150, 625], font)
    c.showPage()

    # 3 early work value
    draw_header(c, 3, "前期工作转化价值", font)
    draw_text(c, margin, height - 88, "前期不是白干，而是把“能不能做”变成“怎么稳定做”", font, 24, BLUE)
    draw_text(
        c,
        margin,
        height - 126,
        "前十几天的大部分工作不是最终模型本身，而是把数据、入口、生成链路、校验和风险边界摸清。领导汇报里需要把这些工作翻译成可理解的阶段成果。",
        font,
        14,
        TEXT,
        max_width=width - 2 * margin,
        leading=22,
    )
    rows = [
        ["前期工作", "转化成的项目资产", "对后续的价值"],
        ["五个 MVP 页面", "项目总览、数据录入、规则成熟度、可生成模型、待确认项", "让项目从散乱脚本变成可管理平台"],
        ["CAD 数据资产梳理", "明确 16029 柜体族、标准模型、补充素材、输出目录", "后续生成不再凭空猜测，有来源和边界"],
        ["SolidWorks / FreeCAD 双入口", "建立两条 CAD 路线和任务记录", "SolidWorks 不顺时，FreeCAD 仍能推进规则验证"],
        ["SolidWorks 试错", "发现自动装配错位、窗口过多、资源压力等风险", "避免继续把时间耗在不稳定路径上"],
        ["校验机制", "数量校验、bbox、几何完整性、工程交付说明", "模型不只看起来像，还要有数据证明可复核"],
        ["路线收敛", "从“克隆模型”调整为“规则生成 + 工程交接”", "把项目目标拉回工程师提效，而不是堆展示页面"],
    ]
    table(c, margin, height - 185, rows, [180, 310, 285], font)
    card(
        c,
        margin,
        62,
        width - 2 * margin,
        74,
        "汇报口径",
        "前期工作应表述为“基础能力建设和风险收敛”，不是最终模型数量。真正的阶段成果是平台链路 + 首个可校验模型 + 下一步规则验证路径。",
        font,
        LIGHT_ORANGE,
    )
    c.showPage()

    # 4 problem and goal
    draw_header(c, 4, "项目背景与目标", font)
    draw_text(c, margin, height - 88, "项目解决的问题", font, 25, BLUE)
    problems = [
        ("重复建模", "不同门数、不同门高、不同五金组合需要重复调整。"),
        ("规则分散", "经验藏在历史模型、文件夹和工程师个人习惯中。"),
        ("交接成本", "没有统一的参数、校验和交付格式，工程复核效率低。"),
    ]
    for i, (t, b) in enumerate(problems):
        card(c, margin + i * 260, height - 255, 235, 120, t, b, font, LIGHT_GRAY)
    draw_text(c, margin, height - 315, "项目目标", font, 23, BLUE)
    rows = [
        ["目标", "说明", "当前状态"],
        ["规则沉淀", "把门数、门距、层板、锁位、铰链等变化关系转成可复用规则。", "进行中"],
        ["模型生成", "按参数生成工程参考模型，输出 STEP/FCStd 和校验报告。", "10门已通过"],
        ["工程提效", "让结构工程师先拿到可复核模型和参数表，再进入正式图纸。", "已形成首个样本"],
    ]
    table(c, margin, height - 365, rows, [130, 485, 160], font)
    c.showPage()

    # 5 platform
    draw_header(c, 5, "平台能力", font)
    draw_text(c, margin, height - 88, "MVP 已具备可操作入口和双 CAD 路线", font, 25, BLUE)
    add_image(c, assets["platform"], margin, 60, width - 2 * margin, height - 145)
    c.showPage()

    # 6 solution flow
    draw_header(c, 6, "总体方案", font)
    add_image(c, assets["flow"], margin, 62, width - 2 * margin, height - 120)
    c.showPage()

    # 7 data assets
    draw_header(c, 7, "数据资产与范围", font)
    draw_text(c, margin, height - 88, "当前先聚焦 16029 柜体族，不泛化到所有产品", font, 24, BLUE)
    rows = [
        ["项目", "说明"],
        ["主数据工作区", r"D:\机械结构工程师智能体：历史 CAD、STEP、SolidWorks 证据、组件/transform/bbox 信息。"],
        ["补充素材", r"C:\Users\Administrator\Desktop\参数化模板素材：后续补齐标准样本和特殊门型。"],
        ["当前交付包", str(MODEL_DIR)],
        ["当前优先级", "先跑通 16029 同外形 10/12/14 门变化规则，再扩展宽度和特殊门型。"],
    ]
    table(c, margin, height - 150, rows, [160, 615], font)
    c.showPage()

    # 8 model result
    draw_header(c, 8, "已完成模型", font)
    add_image(c, assets["model"], margin, 60, width - 2 * margin, height - 120)
    c.showPage()

    # 9 rule seeds
    draw_header(c, 9, "规则种子", font)
    add_image(c, assets["rules"], margin, 60, width - 2 * margin, height - 120)
    c.showPage()

    # 10 validation
    draw_header(c, 10, "校验结果", font)
    add_image(c, assets["validation"], margin, 60, width - 2 * margin, height - 120)
    c.showPage()

    # 11 risk and adjustment
    draw_header(c, 11, "风险与调整", font)
    draw_text(c, margin, height - 88, "不回避风险：SolidWorks 自动装配和本机资源是当前工程化风险", font, 22, BLUE)
    rows = [
        ["风险", "表现", "处理策略"],
        ["SolidWorks API 工程化风险", "早期自动装配出现错位，不能作为当前主线成果。", "先用 FreeCAD 验证规则，再回到 SolidWorks 做单模型验证。"],
        ["电脑资源压力", "同时打开或生成多个模型会拖慢机器。", "后续每次只跑一个 CAD 任务，不并行跑。"],
        ["生产交付边界", "STEP/FCStd 可参考，但不是完整 SolidWorks 特征树。", "明确标注工程参考模型，正式图纸/DXF/BOM 后续放行。"],
    ]
    table(c, margin, height - 155, rows, [180, 300, 295], font)
    card(c, margin, 88, width - 2 * margin, 88, "路线调整后的判断", "当前最有价值的不是继续克隆已有模型，而是把 16029 门数变化规则跑通，让工程师拿到能复核、能继续建图的参数和参考模型。", font, LIGHT_ORANGE)
    c.showPage()

    # 12 roadmap
    draw_header(c, 12, "下一阶段计划", font)
    add_image(c, assets["roadmap"], margin, 120, width - 2 * margin, 360)
    draw_text(c, margin, 85, "执行原则：每次只跑一个 CAD 任务；普通插话不重置阶段；电脑资源异常、模型明显错乱、用户明确叫停时才打断。", font, 13, TEXT, max_width=width - 2 * margin, leading=20)
    c.showPage()

    # 13 conclusion
    draw_header(c, 13, "阶段结论", font)
    draw_text(c, margin, height - 95, "当前可向领导汇报的结论", font, 27, BLUE)
    conclusions = [
        ("1", "已完成一个有真实数据支撑的工程参考模型", f"16029 / {data.door_count}门 / 1000W x 1917H x 550D，基础校验 PASS。"),
        ("2", "项目方向已经从“页面展示”转向“规则生成”", "模型、参数、校验和交付包开始形成闭环。"),
        ("3", "下一阶段目标清晰", "先完成 10/12/14 门同外形规则表，再决定 SolidWorks 工程交接方式。"),
    ]
    for i, (n, t, b) in enumerate(conclusions):
        x = margin
        y = height - 210 - i * 120
        c.setFillColor(hex_color(LIGHT_BLUE if i == 0 else LIGHT_GRAY))
        c.roundRect(x, y, width - 2 * margin, 88, 12, fill=True, stroke=False)
        c.setFillColor(hex_color(ORANGE))
        c.circle(x + 35, y + 44, 22, fill=True, stroke=False)
        c.setFillColor(colors.white)
        c.setFont(font, 20)
        c.drawCentredString(x + 35, y + 36, n)
        draw_text(c, x + 78, y + 54, t, font, 15, BLUE)
        draw_text(c, x + 78, y + 28, b, font, 11.5, MUTED, max_width=width - 2 * margin - 110)
    c.save()
    print(OUT_PDF)


if __name__ == "__main__":
    build_pdf()
