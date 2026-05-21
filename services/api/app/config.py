from __future__ import annotations

import os
from pathlib import Path


ROOT_DIR = Path(__file__).resolve().parents[3]
DEFAULT_DB_PATH = ROOT_DIR / "data" / "studio.sqlite"
DB_PATH = Path(os.getenv("STUDIO_DB_PATH", DEFAULT_DB_PATH))
DEFAULT_CAD_WORKSPACE = Path(r"D:\winnsen_cad_workspace")
if not DEFAULT_CAD_WORKSPACE.exists():
    DEFAULT_CAD_WORKSPACE = Path(r"D:\机械结构工程师智能体")
CAD_WORKSPACE = Path(os.getenv("STUDIO_CAD_WORKSPACE", str(DEFAULT_CAD_WORKSPACE)))
WORKER_LOG_DIR = Path(os.getenv("STUDIO_WORKER_LOG_DIR", ROOT_DIR / "workers" / "generation_logs"))
GENERATED_MODEL_DIR = Path(os.getenv("STUDIO_GENERATED_MODEL_DIR", ROOT_DIR / "workers" / "generated_models"))
MANUAL_RUN_DIR = Path(os.getenv("STUDIO_MANUAL_RUN_DIR", ROOT_DIR / "workers" / "manual_runs"))
HANDOFF_DIR = Path(os.getenv("STUDIO_HANDOFF_DIR", ROOT_DIR / "workers" / "handoffs"))
SOLIDWORKS_RUN_LOCK_PATH = Path(os.getenv("STUDIO_SOLIDWORKS_RUN_LOCK_PATH", WORKER_LOG_DIR / "solidworks-run.lock"))
FREECAD_RUN_LOCK_PATH = Path(os.getenv("STUDIO_FREECAD_RUN_LOCK_PATH", WORKER_LOG_DIR / "freecad-run.lock"))
RULE_EXTRACTION_DIR = Path(os.getenv("STUDIO_RULE_EXTRACTION_DIR", ROOT_DIR / "workers" / "rule_extractions"))
TEMPLATE_ASSET_ROOT = Path(os.getenv("STUDIO_PARAMETRIC_TEMPLATE_ROOT", r"C:\Users\Administrator\Desktop\参数化模板素材"))
TEMPLATE_CATALOG_PATH = Path(os.getenv("STUDIO_TEMPLATE_CATALOG_PATH", ROOT_DIR / "data" / "parametric_template_catalog.json"))
TEMPLATE_CATALOG_MARKDOWN_PATH = Path(
    os.getenv("STUDIO_TEMPLATE_CATALOG_MARKDOWN_PATH", ROOT_DIR / "data" / "parametric_template_catalog.md")
)
RULE_SEED_CANDIDATES_PATH = Path(
    os.getenv("STUDIO_RULE_SEED_CANDIDATES_PATH", ROOT_DIR / "data" / "rule_seed_candidates.json")
)
RULE_SEED_REVIEW_LEDGER_PATH = Path(
    os.getenv("STUDIO_RULE_SEED_REVIEW_LEDGER_PATH", ROOT_DIR / "data" / "rule_seed_review_ledger.json")
)
RULE_SEED_EVIDENCE_CHECKLIST_PATH = Path(
    os.getenv("STUDIO_RULE_SEED_EVIDENCE_CHECKLIST_PATH", ROOT_DIR / "data" / "rule_seed_evidence_checklist.json")
)
RULE_SEED_QUANTITY_FORMULAS_PATH = Path(
    os.getenv("STUDIO_RULE_SEED_QUANTITY_FORMULAS_PATH", ROOT_DIR / "data" / "rule_seed_quantity_formulas.json")
)
DRAWING_SHEETMETAL_INTAKE_DIR = Path(
    os.getenv("STUDIO_DRAWING_SHEETMETAL_INTAKE_DIR", ROOT_DIR / "workers" / "drawing_sheetmetal" / "intake")
)
DRAWING_SHEETMETAL_INTAKE_MANIFEST_PATH = Path(
    os.getenv(
        "STUDIO_DRAWING_SHEETMETAL_INTAKE_MANIFEST_PATH",
        DRAWING_SHEETMETAL_INTAKE_DIR / "intake_manifest.json",
    )
)
DRAWING_SHEETMETAL_EVIDENCE_PATH = Path(
    os.getenv("STUDIO_DRAWING_SHEETMETAL_EVIDENCE_PATH", ROOT_DIR / "data" / "drawing_sheetmetal_intake_evidence.json")
)
DRAWING_SHEETMETAL_EVIDENCE_MARKDOWN_PATH = Path(
    os.getenv("STUDIO_DRAWING_SHEETMETAL_EVIDENCE_MARKDOWN_PATH", ROOT_DIR / "data" / "drawing_sheetmetal_intake_evidence.md")
)
DRAWING_SHEETMETAL_DXF_EXTRACTOR = Path(
    os.getenv(
        "STUDIO_DRAWING_SHEETMETAL_DXF_EXTRACTOR",
        ROOT_DIR / "workers" / "drawing_sheetmetal" / "extract_dxf_sheetmetal_reference.py",
    )
)
DRAWING_SHEETMETAL_FREECAD_CHECKER = Path(
    os.getenv(
        "STUDIO_DRAWING_SHEETMETAL_FREECAD_CHECKER",
        ROOT_DIR / "workers" / "drawing_sheetmetal" / "freecad_check_intake_model.py",
    )
)
FREECAD_FCSTD_GEOMETRY_AUDITOR = Path(
    os.getenv(
        "STUDIO_FREECAD_FCSTD_GEOMETRY_AUDITOR",
        ROOT_DIR / "workers" / "drawing_sheetmetal" / "freecad_audit_fcstd_geometry_integrity.py",
    )
)
SHEETMETAL_RULE_EVIDENCE_16029_PATH = Path(
    os.getenv("STUDIO_16029_SHEETMETAL_RULE_EVIDENCE_JSON", ROOT_DIR / "data" / "sheetmetal_rule_evidence_16029.json")
)
SHEETMETAL_RULE_EVIDENCE_16029_MARKDOWN_PATH = Path(
    os.getenv("STUDIO_16029_SHEETMETAL_RULE_EVIDENCE_MD", ROOT_DIR / "data" / "sheetmetal_rule_evidence_16029.md")
)
LOCKER_16029_VARIANT_RULE_PACKET_PATH = Path(
    os.getenv("STUDIO_16029_VARIANT_RULE_PACKET_JSON", ROOT_DIR / "data" / "locker_16029_variant_rule_packet.json")
)
LOCKER_16029_VARIANT_RULE_PACKET_MARKDOWN_PATH = Path(
    os.getenv("STUDIO_16029_VARIANT_RULE_PACKET_MD", ROOT_DIR / "data" / "locker_16029_variant_rule_packet.md")
)
LOCKER_16029_VARIANT_QUALITY_MATRIX_PATH = Path(
    os.getenv("STUDIO_16029_VARIANT_QUALITY_MATRIX_JSON", ROOT_DIR / "data" / "locker_16029_variant_quality_matrix.json")
)
LOCKER_16029_VARIANT_QUALITY_MATRIX_MARKDOWN_PATH = Path(
    os.getenv("STUDIO_16029_VARIANT_QUALITY_MATRIX_MD", ROOT_DIR / "data" / "locker_16029_variant_quality_matrix.md")
)
LOCKER_16029_ENGINEERING_HANDOFF_BUNDLE_PATH = Path(
    os.getenv(
        "STUDIO_16029_ENGINEERING_HANDOFF_BUNDLE_JSON",
        ROOT_DIR / "data" / "locker_16029_engineering_handoff_bundle.json",
    )
)
LOCKER_16029_ENGINEERING_HANDOFF_BUNDLE_MARKDOWN_PATH = Path(
    os.getenv(
        "STUDIO_16029_ENGINEERING_HANDOFF_BUNDLE_MD",
        ROOT_DIR / "data" / "locker_16029_engineering_handoff_bundle.md",
    )
)
LOCKER_16029_STRUCTURAL_RULE_AUDIT_SCRIPT = Path(
    os.getenv(
        "STUDIO_16029_STRUCTURAL_RULE_AUDIT_SCRIPT",
        ROOT_DIR / "workers" / "maintenance" / "audit_16029_variant_structural_rules.py",
    )
)
LOCKER_16029_VARIANT_QUALITY_MATRIX_SCRIPT = Path(
    os.getenv(
        "STUDIO_16029_VARIANT_QUALITY_MATRIX_SCRIPT",
        ROOT_DIR / "workers" / "maintenance" / "build_16029_variant_quality_matrix.py",
    )
)
LOCKER_16029_ENGINEERING_HANDOFF_BUNDLE_SCRIPT = Path(
    os.getenv(
        "STUDIO_16029_ENGINEERING_HANDOFF_BUNDLE_SCRIPT",
        ROOT_DIR / "workers" / "maintenance" / "build_16029_engineering_handoff_bundle.py",
    )
)
MAX_DRAWING_SHEETMETAL_UPLOAD_BYTES = int(os.getenv("STUDIO_MAX_DRAWING_SHEETMETAL_UPLOAD_BYTES", str(80 * 1024 * 1024)))
FREECAD_SHORTCUT = Path(os.getenv("STUDIO_FREECAD_SHORTCUT", r"C:\Users\Administrator\Desktop\FreeCAD 1.1.1.lnk"))
FREECAD_EXE = Path(
    os.getenv(
        "STUDIO_FREECAD_EXE",
        r"D:\软件安装录\freecad\FreeCAD_1.1.1\FreeCAD_1.1.1-Windows-x86_64-py311\FreeCAD.exe",
    )
)
SOLIDWORKS_SHORTCUT = Path(os.getenv("STUDIO_SOLIDWORKS_SHORTCUT", r"C:\Users\Public\Desktop\SOLIDWORKS 2025.lnk"))
SOLIDWORKS_EXE = Path(
    os.getenv(
        "STUDIO_SOLIDWORKS_EXE",
        r"D:\软件安装录\soildworks\SOLIDWORKS\SLDWORKS.exe",
    )
)

SUPPORTED_LOCKER_DOOR_COUNTS = {4, 6, 8, 10, 12, 14, 16, 18, 20, 22, 24}
SUPPORTED_16029_SOLIDWORKS_TEMPLATE_DOOR_COUNTS = {10, 12, 14}
SUPPORTED_16029_FREECAD_RULE_DOOR_COUNTS = {10, 12, 14}
VERIFIED_LOCKER_16029_SOLIDWORKS_DOOR_COUNTS = {10, 12, 14}
SUPPORTED_16038_SOLIDWORKS_DOOR_COUNTS = {4, 7, 8, 12}
SUPPORTED_16038_FREECAD_DOOR_COUNTS = {4, 7, 8}
SUPPORTED_DRAWING_SHEETMETAL_SUFFIXES = {
    ".dxf",
    ".dwg",
    ".pdf",
    ".png",
    ".jpg",
    ".jpeg",
    ".step",
    ".stp",
    ".sldprt",
    ".sldasm",
    ".slddrw",
    ".fcstd",
}
