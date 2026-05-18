from __future__ import annotations

import argparse
import json
import re
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


DEFAULT_ROOT = Path(r"C:\Users\Administrator\Desktop\参数化模板素材")
DEFAULT_JSON = Path(r"D:\Winnsen_Structure_Agent_Studio\data\parametric_template_catalog.json")
DEFAULT_MARKDOWN = Path(r"D:\Winnsen_Structure_Agent_Studio\data\parametric_template_catalog.md")

CAD_EXTENSIONS = {
    ".sldasm",
    ".sldprt",
    ".slddrw",
    ".dxf",
    ".step",
    ".stp",
    ".xlsx",
    ".pdf",
    ".jpg",
}

ASSEMBLY_HINTS = re.compile(r"总装配|总装配体|标准寄存柜|洗衣寄存柜|控制主柜|寄存控制主柜|寄存柜主柜|寄存柜\(")
LOW_PRIORITY_HINTS = ("\\img\\", "\\包装\\", "\\█包装\\", "\\作废", "\\bak\\")
BACKUP_HINTS = ("\\备份\\", "\\订单配置\\", "po#")
DIMENSION_RE = re.compile(r"(?<!\d)(\d{3,4})\s*[×xX]\s*(\d{3,4})\s*[×xX]\s*(\d{3,4})(?!\d)")
DOOR_RE = re.compile(r"(\d{1,2})\s*门")


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def file_record(path: Path, root: Path) -> dict[str, Any]:
    stat = path.stat()
    return {
        "path": str(path),
        "relativePath": str(path.relative_to(root)),
        "bytes": stat.st_size,
        "updatedAt": datetime.fromtimestamp(stat.st_mtime, timezone.utc).isoformat().replace("+00:00", "Z"),
    }


def priority_for_assembly(path: Path) -> int:
    text = str(path).lower()
    score = 100
    if ASSEMBLY_HINTS.search(path.name):
        score -= 45
    if "总装配" in path.name or "总装配体" in path.name:
        score -= 25
    if any(hint in text for hint in LOW_PRIORITY_HINTS):
        score += 60
    if any(hint in text for hint in BACKUP_HINTS):
        score += 18
    if "harness" in text:
        score += 20
    return score


def unique_sorted(values: set[str]) -> list[str]:
    return sorted(values, key=lambda item: (len(item), item))


def infer_dimensions(paths: list[Path]) -> list[str]:
    values: set[str] = set()
    for path in paths:
        for match in DIMENSION_RE.finditer(str(path)):
            values.add("x".join(match.groups()))
    return unique_sorted(values)


def infer_door_variants(paths: list[Path]) -> list[str]:
    values: set[str] = set()
    for path in paths:
        for match in DOOR_RE.finditer(str(path)):
            values.add(f"{match.group(1)}门")
    return unique_sorted(values)


def evidence_paths(project_dir: Path, files: list[Path], ext: str, name_hint: re.Pattern[str] | None = None, limit: int = 16) -> list[dict[str, Any]]:
    matched = [
        path
        for path in files
        if path.suffix.lower() == ext and (name_hint is None or name_hint.search(str(path)))
    ]
    return [file_record(path, project_dir) for path in sorted(matched, key=str)[:limit]]


def evidence_dirs(project_dir: Path, files: list[Path], ext: str, dir_hint: re.Pattern[str], limit: int = 12) -> list[str]:
    dirs = {
        str(path.parent.relative_to(project_dir))
        for path in files
        if path.suffix.lower() == ext and dir_hint.search(str(path.parent))
    }
    return sorted(dirs)[:limit]


def scan_project(project_dir: Path) -> dict[str, Any]:
    files = [path for path in project_dir.rglob("*") if path.is_file()]
    counts = Counter(path.suffix.lower() for path in files if path.suffix.lower() in CAD_EXTENSIONS)

    assembly_files = [path for path in files if path.suffix.lower() == ".sldasm" and ASSEMBLY_HINTS.search(str(path))]
    assembly_files = sorted(assembly_files, key=lambda path: (priority_for_assembly(path), str(path)))
    primary_candidates = [file_record(path, project_dir) | {"priority": priority_for_assembly(path)} for path in assembly_files[:12]]

    evidence = {
        "bomFiles": evidence_paths(project_dir, files, ".xlsx", re.compile(r"BOM", re.IGNORECASE), limit=12),
        "dxfDirectories": evidence_dirs(project_dir, files, ".dxf", re.compile(r"展开|钣金", re.IGNORECASE), limit=12),
        "stepFiles": evidence_paths(project_dir, files, ".step", None, limit=12)
        + evidence_paths(project_dir, files, ".stp", None, limit=12),
        "pdfDirectories": evidence_dirs(project_dir, files, ".pdf", re.compile(r"pdf|PDF|图纸|产品尺寸", re.IGNORECASE), limit=12),
    }

    paths_for_inference = files + [project_dir]
    return {
        "name": project_dir.name,
        "path": str(project_dir),
        "counts": {
            "SLDASM": counts[".sldasm"],
            "SLDPRT": counts[".sldprt"],
            "SLDDRW": counts[".slddrw"],
            "DXF": counts[".dxf"],
            "STEP": counts[".step"],
            "STP": counts[".stp"],
            "XLSX": counts[".xlsx"],
            "PDF": counts[".pdf"],
            "JPG": counts[".jpg"],
        },
        "inferredDimensions": infer_dimensions(paths_for_inference),
        "inferredDoorVariants": infer_door_variants(paths_for_inference),
        "primaryAssemblyCandidates": primary_candidates,
        "evidence": evidence,
    }


def build_catalog(root: Path) -> dict[str, Any]:
    projects = [scan_project(path) for path in sorted(root.iterdir(), key=lambda item: item.name) if path.is_dir()]
    total_counts = Counter()
    for project in projects:
        total_counts.update(project["counts"])

    return {
        "generatedAt": utc_now(),
        "sourceRoot": str(root),
        "projectCount": len(projects),
        "totals": dict(total_counts),
        "projects": projects,
    }


def write_markdown(catalog: dict[str, Any], path: Path) -> None:
    lines = [
        "# 参数化模板素材目录",
        "",
        f"- Generated at: `{catalog['generatedAt']}`",
        f"- Source root: `{catalog['sourceRoot']}`",
        f"- Project roots: `{catalog['projectCount']}`",
        "",
        "| 项目 | SLDASM | SLDPRT | SLDDRW | DXF | STEP/STP | XLSX | PDF | JPG | 候选总装配 |",
        "|---|---:|---:|---:|---:|---:|---:|---:|---:|---|",
    ]
    for project in catalog["projects"]:
        counts = project["counts"]
        candidates = project["primaryAssemblyCandidates"]
        candidate = candidates[0]["relativePath"] if candidates else "未识别"
        lines.append(
            "| {name} | {sldasm} | {sldprt} | {slddrw} | {dxf} | {step} | {xlsx} | {pdf} | {jpg} | `{candidate}` |".format(
                name=project["name"],
                sldasm=counts["SLDASM"],
                sldprt=counts["SLDPRT"],
                slddrw=counts["SLDDRW"],
                dxf=counts["DXF"],
                step=counts["STEP"] + counts["STP"],
                xlsx=counts["XLSX"],
                pdf=counts["PDF"],
                jpg=counts["JPG"],
                candidate=candidate,
            )
        )
    lines.append("")
    lines.append("## 规则学习说明")
    lines.append("")
    lines.append("- 该目录只负责索引候选素材，不把候选总装配直接视为可生成模型。")
    lines.append("- 后续规则抽取应优先比对同尺寸不同门数、同系列不同柜深、主控柜操作区、材料/工艺变体。")
    lines.append("- 包装、作废、备份和订单配置文件可能提供线索，但需要在规则表中标明来源等级。")
    lines.append("")
    path.write_text("\n".join(lines), encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description="Index Winnsen parametric template CAD assets.")
    parser.add_argument("--root", default=str(DEFAULT_ROOT), help="Parametric template asset root.")
    parser.add_argument("--json-out", default=str(DEFAULT_JSON), help="Catalog JSON output path.")
    parser.add_argument("--md-out", default=str(DEFAULT_MARKDOWN), help="Catalog Markdown output path.")
    args = parser.parse_args()

    root = Path(args.root)
    if not root.exists():
        raise SystemExit(f"Template root does not exist: {root}")

    catalog = build_catalog(root)
    json_out = Path(args.json_out)
    md_out = Path(args.md_out)
    json_out.parent.mkdir(parents=True, exist_ok=True)
    md_out.parent.mkdir(parents=True, exist_ok=True)
    json_out.write_text(json.dumps(catalog, ensure_ascii=False, indent=2), encoding="utf-8")
    write_markdown(catalog, md_out)
    print(f"Wrote {json_out}")
    print(f"Wrote {md_out}")
    print(f"Projects: {catalog['projectCount']}; SLDASM: {catalog['totals'].get('SLDASM', 0)}; DXF: {catalog['totals'].get('DXF', 0)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
