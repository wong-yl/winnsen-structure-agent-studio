"""Create a read-only 2-column, 3-row DXF geometry preview from a trial manifest."""
import argparse
import hashlib
import json
import math
import os
from pathlib import Path
import sys

sys.dont_write_bytecode = True
from PIL import Image, ImageDraw, ImageFont

import dxf_check as dxf


ROOT = dxf.TRIAL_ROOT.resolve()
SIZES = ((437, 471.25), (287, 200), (537, 1000))
SLOTS = [(hand, width, height) for width, height in SIZES for hand in ("L", "R")]
COLORS = {"CUT": "#203044", "BEND": "#D64242", "MARK": "#8190A3"}


def sha256(data):
    return hashlib.sha256(data).hexdigest().upper()


def checked_path(path):
    full = Path(os.path.abspath(path))
    for ancestor in (full, *full.parents):
        if ancestor.exists() or ancestor.is_symlink():
            stat = ancestor.lstat()
            dxf.require(not ancestor.is_symlink() and not getattr(stat, "st_file_attributes", 0) & 0x400,
                        "PREVIEW_REPARSE_PATH")
    return full.resolve()


def read_cases(run):
    output_root = checked_path(run / "cad/outputs")
    path = checked_path(output_root / "output-manifest.json")
    dxf.require(run in path.parents and path.is_file(), "PREVIEW_OUTPUT_MANIFEST_REQUIRED")
    source = path.read_bytes()
    manifest = json.loads(source.decode("utf-8-sig"))
    dxf.require(manifest.get("schema") in {"16029-variable-output-manifest/v1", "16029-variable-output-manifest/v2"},
                "PREVIEW_OUTPUT_MANIFEST_SCHEMA_REQUIRED")
    dxf.require(isinstance(manifest.get("complete"), bool) and isinstance(manifest.get("cases"), list)
                and 0 < len(manifest["cases"]) <= 6, "PREVIEW_ONE_TO_SIX_DECLARED_CASES_REQUIRED")
    cases, inputs = {}, {str(path): sha256(source)}
    for item in manifest["cases"]:
        key = (item.get("hand"), item.get("widthMm"), item.get("heightMm"))
        dxf.require(key in SLOTS and key not in cases, "PREVIEW_CASE_IDENTITY_MISMATCH")
        dxf.require(isinstance(item.get("folder"), str) and isinstance(item.get("part"), str), "PREVIEW_CASE_PATH_REQUIRED")
        part = checked_path(output_root / item["folder"] / item["part"])
        dxf.require(output_root in part.parents, "PREVIEW_CASE_PATH_ESCAPE")
        declared = item.get("dxf", {})
        path = checked_path(part.parent / "flat.dxf")
        dxf.require(checked_path(declared.get("path", "")) == path and path.is_file(), "PREVIEW_DXF_PATH_MISMATCH")
        data = path.read_bytes()
        dxf.require(sha256(data) == declared.get("sha256"), "PREVIEW_DXF_SHA256_MISMATCH")
        inputs[str(path)] = sha256(data)
        sections = dxf.section_records(dxf.pairs_from_bytes(data))
        dxf.require(dxf.units_of(sections.get("HEADER", [])) == 4, "PREVIEW_MILLIMETRE_DXF_REQUIRED")
        primitives, circles, counts, layers, unsupported = dxf.geometry(dxf.records(sections["ENTITIES"]), 1)
        dxf.require(not unsupported and all(layer in COLORS for layer in layers), "PREVIEW_UNSUPPORTED_GEOMETRY_OR_LAYER")
        cut = [row for row in primitives if row["layer"] == "CUT"]
        holes = [row for row in circles if row["layer"] == "CUT"]
        box = dxf.bounds(cut, holes)
        dxf.require(box and box["width"] > 0 and box["height"] > 0, "PREVIEW_CUT_EXTENTS_REQUIRED")
        cases[key] = {"item": item, "path": str(path), "sha256": sha256(data), "primitives": primitives,
                      "circles": circles, "cutExtents": box, "drawingExtents": dxf.bounds(primitives, circles),
                      "entityTypeCounts": dict(counts), "layerCounts": dict(layers)}
    dxf.require(not manifest["complete"] or len(cases) == 6, "PREVIEW_COMPLETE_MANIFEST_REQUIRES_SIX_CASES")
    return manifest, source, cases, inputs


def font(size, bold=False):
    path = Path("C:/Windows/Fonts/msyhbd.ttc" if bold else "C:/Windows/Fonts/msyh.ttc")
    dxf.require(path.is_file(), "PREVIEW_CHINESE_FONT_REQUIRED " + str(path))
    return ImageFont.truetype(str(path), size)


def number(value):
    return format(value, ".4f").rstrip("0").rstrip(".")


def render(cases, complete):
    canvas = Image.new("RGB", (2400, 3000), "#EFF3F7")
    draw = ImageDraw.Draw(canvas)
    title, subtitle, body, small = font(52, True), font(37, True), font(29), font(25)
    draw.text((52, 30), "16029 门板 DXF 几何预览", font=title, fill=COLORS["CUT"])
    state = "清单已完整" if complete else "清单尚未完成"
    draw.text((54, 104), "实际读取 %d / 6 例  |  %s  |  几何预览，非生产图" % (len(cases), state),
              font=body, fill="#515F72")
    draw.text((54, 151), "各格独立缩放，非 1:1；保持 DXF 原始 X/Y 方向，不据此判断左右手或折弯方向。",
              font=small, fill="#657287")
    margin, gap, top, card_width, card_height = 52, 36, 214, 1130, 856
    for index, key in enumerate(SLOTS):
        hand, width, height = key
        x, y = margin + (index % 2)*(card_width+gap), top + (index // 2)*(card_height+gap)
        draw.rounded_rectangle((x, y, x+card_width, y+card_height), radius=18, fill="white", outline="#D3DCE7", width=2)
        side = "左门 L" if hand == "L" else "右门 R"
        draw.text((x+32, y+22), side + "  |  输入 %s × %s mm" % (number(width), number(height)),
                  font=subtitle, fill=COLORS["CUT"])
        case = cases.get(key)
        if case is None:
            draw.text((x+32, y+91), "暂无清单中的实际 DXF", font=body, fill="#8793A3")
            draw.text((x+285, y+430), "尚未生成 / 未读取", font=font(37), fill="#A0AAB8")
            continue
        box = case["cutExtents"]
        draw.text((x+32, y+86), "展开 %s × %s mm  |  %s" % (number(box["width"]), number(box["height"]), case["item"]["folder"]),
                  font=body, fill="#4C5E73")
        bounds = case["drawingExtents"]
        left, right, upper, lower = x+86, x+card_width-74, y+155, y+card_height-82
        scale = min((right-left)/bounds["width"], (lower-upper)/bounds["height"])
        center = [(bounds["min"][i]+bounds["max"][i])/2 for i in (0, 1)]
        def point(value):
            return ((left+right)/2+(value[0]-center[0])*scale,
                    (upper+lower)/2-(value[1]-center[1])*scale)
        for layer in ("CUT", "MARK", "BEND"):
            for row in case["primitives"]:
                if row["layer"] != layer:
                    continue
                if row["kind"] == "LINE":
                    points = [point(value) for value in dxf.ends(row)]
                else:
                    segments = max(8, math.ceil(abs(row["sweep"])*180/math.pi))
                    points = [point([row["c"][0]+row["r"]*math.cos(row["start"]+row["sweep"]*i/segments),
                                     row["c"][1]+row["r"]*math.sin(row["start"]+row["sweep"]*i/segments)])
                              for i in range(segments+1)]
                draw.line(points, fill=COLORS[layer], width=3 if layer == "CUT" else 2)
            for row in case["circles"]:
                if row["layer"] == layer:
                    a, b = point([row["c"][0]-row["r"], row["c"][1]+row["r"]]), point([row["c"][0]+row["r"], row["c"][1]-row["r"]])
                    draw.ellipse((*a, *b), outline=COLORS[layer], width=3)
        draw.line((x+34,y+card_height-44,x+85,y+card_height-44), fill=COLORS["CUT"], width=4)
        draw.text((x+96,y+card_height-62), "CUT 轮廓", font=small, fill=COLORS["CUT"])
        draw.line((x+324,y+card_height-44,x+375,y+card_height-44), fill=COLORS["BEND"], width=3)
        draw.text((x+386,y+card_height-62), "BEND 折弯线", font=small, fill=COLORS["BEND"])
    draw.text((54, 2920), "仅供查看实际 DXF 几何；不包含工程图标注、制造方向验收或生产放行。", font=body, fill="#515F72")
    return canvas


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("run", type=Path)
    parser.add_argument("--out", type=Path, required=True)
    args = parser.parse_args()
    run, target = checked_path(args.run), checked_path(args.out)
    dxf.require(ROOT in run.parents and run.parent.name == "runs" and run.is_dir(), "PREVIEW_TRIAL_RUN_REQUIRED")
    dxf.require(target.parent == ROOT and target.name.startswith("analysis-dxf-preview-") and not target.exists(),
                "PREVIEW_NEW_OUTPUT_DIRECTORY_REQUIRED")
    manifest, source, cases, inputs = read_cases(run)
    image = render(cases, manifest["complete"])
    dxf.require(all(sha256(Path(path).read_bytes()) == digest for path,digest in inputs.items()), "PREVIEW_SOURCE_CHANGED_DURING_READ")
    target.mkdir(exist_ok=False)
    png = target / "dxf-overview.png"
    with png.open("xb") as stream:
        image.save(stream, format="PNG")
    with (target / "output-manifest.snapshot.json").open("xb") as stream:
        stream.write(source)
    evidence = {"schema":"16029-dxf-geometry-preview/v1", "run":str(run), "actualCaseCount":len(cases),
                "expectedCaseCount":6, "sourceManifestComplete":manifest["complete"], "geometryPreviewOnly":True,
                "production_release":False, "fullPlanPass":False, "sourceInputsUnchanged":True,
                "coordinateDisplay":"DXF X right and Y up; each cell has independent pixel scale; no rotation or mirroring",
                "sourceInputsSha256":inputs, "previewScriptSha256":sha256(Path(__file__).read_bytes()),
                "geometryParserSha256":sha256(Path(dxf.__file__).read_bytes()),
                "png":{"path":str(png), "sha256":sha256(png.read_bytes()), "widthPx":image.width, "heightPx":image.height},
                "cases":[{key:case[key] for key in ("path","sha256","cutExtents","entityTypeCounts","layerCounts")}
                         | {"hand":identity[0], "inputWidthMm":identity[1], "inputHeightMm":identity[2],
                            "partSha256Declared":case["item"].get("partSha256")} for identity,case in cases.items()]}
    with (target / "preview.json").open("x",encoding="utf-8") as stream:
        json.dump(evidence,stream,ensure_ascii=False,indent=2)
    print(json.dumps({"directory":str(target), "png":str(png), "actualCaseCount":len(cases),
                      "sourceManifestComplete":manifest["complete"], "production_release":False},ensure_ascii=False))


if __name__ == "__main__":
    main()
