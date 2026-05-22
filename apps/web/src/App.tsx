import {
  Bot,
  AlertTriangle,
  Archive,
  Boxes,
  CheckCircle2,
  ChevronRight,
  ClipboardList,
  Database,
  FileWarning,
  FolderOpen,
  Gauge,
  Layers3,
  ListChecks,
  Menu,
  MessageSquareMore,
  Play,
  RefreshCw,
  Route,
  Search,
  Settings2,
  ShieldCheck,
  Upload,
  Wrench,
  X,
} from 'lucide-react'
import { useCallback, useEffect, useMemo, useState } from 'react'
import './App.css'
import {
  capabilities,
  drawingSheetMetalBatchRun,
  drawingSheetMetalOutputs,
  drawingSheetMetalRisks,
  drawingSheetMetalRoadmap,
  drawingSheetMetalSources,
  maturityDistribution,
  metricCards,
  pipelineRows,
  projects,
  reviewItems,
  ruleLearningAxes,
  ruleFamilies,
  sourcePaths,
  templateAssets,
  type Capability,
  type Evidence,
  type Maturity,
  type Project,
  type ReviewItem,
  type RuleLearningAxis,
  type RuleFamily,
  type TemplateAsset,
} from './data/studioData'

type PageId = 'overview' | 'models' | 'drawings' | 'review' | 'intake' | 'rules' | 'console'
type PageSectionId = 'delivery' | 'evidence' | 'control'
type CadRunner = 'freecad' | 'solidworks'
type LocalActionMode = 'open' | 'reveal'
type ParameterValues = Record<string, string>
type StatusTone = 'good' | 'warn' | 'risk' | 'idle'
type GenerationFeedback = {
  taskId: string
  tone: 'good' | 'warn' | 'risk'
  title: string
  detail: string
  cadRunner?: CadRunner
  outputDir?: string
  recommendedFile?: string
  validationReport?: string
  validationData?: string
}
type DryRunCheck = {
  name: string
  ok: boolean
  detail: string
}

type DryRunResult = {
  task_id: string
  status: GenerationTask['status']
  checks: DryRunCheck[]
  log_path: string
  checked_at: string
}

type SolidWorksRunSummary = {
  generation_mode: string
  interpretation: string
  source_assembly: string | null
  source_folder: string | null
  door_count: string | null
  layout_units: string | null
  layout_rule_status: string | null
  variant_label: string | null
  component_manifest_path: string | null
  component_manifest_rows: number | null
  requested_component_count: number | null
  added_component_count: number | null
  validation_pass_count: number | null
  validation_warn_count: number | null
  validation_fail_count: number | null
  transform_coverage: string | null
  bbox_coverage: string | null
  quality_status: string | null
  quality_summary: string | null
  flat_component_count: number | null
  all_component_count: number | null
  root_child_count: number | null
  reference_like_feature_count: number | null
  feature_total: number | null
  reference_sample_count: number | null
  reference_sample: string[]
  next_action: string
}

type WorkerExecutionResult = {
  task_id: string
  status: GenerationTask['status']
  cad_runner: CadRunner
  started_at: string
  finished_at: string
  command: string[]
  cwd: string
  output_dir: string
  outputs: string[]
  stdout_path: string | null
  stderr_path: string | null
  log_path: string
  exit_code: number | null
  solidworks_quality_status?: string | null
  solidworks_quality_summary?: string | null
  solidworks_run_summary?: SolidWorksRunSummary | null
  freecad_quality_status?: string | null
  freecad_quality_summary?: string | null
  freecad_quality_report?: string | null
  message: string
}

type Locker16029VariantQuality = {
  door_count: number
  status: string
  task_id: string | null
  output_dir: string | null
  verify_bbox_x_len: number | null
  verify_rows: number
  fcstd_mb: number | null
  step_mb: number | null
  step_geometry_check?: {
    status: string
    invalid_shape_count?: number | null
    bbox_x_len?: number | null
  }
  geometry_integrity: {
    status: string
    invalid_shape_objects?: number | null
    center_vertical_signature_failures?: number | null
  }
  structural_rule_audit?: {
    status: string
    failed_checks?: number | null
    door_height_mm?: number | null
    door_pitch_mm?: number | null
    door_width_mm?: number | null
    lock_center_x_abs_mm?: {
      min?: number | null
      max?: number | null
      spread?: number | null
    } | null
    hinge_axis_x_abs_mm?: {
      min?: number | null
      max?: number | null
      spread?: number | null
    } | null
  }
}

type Locker16029VariantQualityMatrix = {
  generated_at: string | null
  summary: {
    pass_ready_count: number
    pass_step_geometry_needs_fcstd_audit_count?: number
    pass_rule_counts_needs_fcstd_audit_count: number
    fail_count: number
    missing_output_count: number
  }
  variants: Locker16029VariantQuality[]
}

type Locker16029EngineeringHandoffBundle = {
  generated_at: string | null
  handoff_dir: string
  manifest_csv?: string
  engineer_open_index?: string
  handoff_dir_exists?: boolean
  manifest_csv_exists?: boolean
  engineer_open_index_exists?: boolean
  solidworks_open_verification_exists?: boolean
  readiness_summary?: {
    target_door_counts: number[]
    variant_count: number
    ready_count: number
    model_file_ready_count?: number
    blocked_count: number
    missing_file_count: number
    ready_door_counts: number[]
    blocked_door_counts: number[]
    missing_files: {
      door_count?: number | null
      key: string
      path?: string | null
    }[]
    solidworks_open_verified?: boolean
    solidworks_open_status?: string | null
    solidworks_open_message?: string | null
    all_ready: boolean
  }
  root_launchers?: {
    door_count: number
    solidworks_launcher: string
    target_launcher?: string
    stp?: string
    file_status?: {
      solidworks_launcher?: boolean
      target_launcher?: boolean
      stp?: boolean
    }
  }[]
  variants: {
    door_count: number
    status_label: string
    handoff_dir?: string
    status?: string
    step?: string | null
    stp: string
    fcstd?: string | null
    solidworks_launcher: string
    freecad_launcher?: string | null
    report_md?: string | null
    verify_csv?: string | null
    step_geometry_check_json?: string | null
    fcstd_integrity_md?: string | null
    structural_rule_audit_md?: string | null
    handoff_files_ready?: boolean
    file_status?: {
      handoff_dir?: boolean
      step?: boolean
      stp?: boolean
      fcstd?: boolean
      root_solidworks_launcher?: boolean | null
      solidworks_launcher?: boolean
      freecad_launcher?: boolean
      report_md?: boolean
      verify_csv?: boolean
      step_geometry_check_json?: boolean
      fcstd_integrity_md?: boolean
      structural_rule_audit_md?: boolean
    }
    metrics?: {
      bbox_x_mm?: number | null
      step_invalid_shape_count?: number | null
      door_height_mm?: number | null
      door_pitch_mm?: number | null
      door_width_mm?: number | null
      structural_rule_status?: string | null
      fcstd_integrity_status?: string | null
      step_geometry_status?: string | null
      step_mb?: number | null
      fcstd_mb?: number | null
      lock_center_x_abs_mm?: {
        min?: number | null
        max?: number | null
        spread?: number | null
      } | null
      hinge_axis_x_abs_mm?: {
        min?: number | null
        max?: number | null
        spread?: number | null
      } | null
    }
  }[]
}

type Locker16029VerifiedRulePacket = {
  generated_at: string | null
  status: string
  scope: string
  verified_door_counts: number[]
  formula_candidate_door_counts?: number[]
  verified_variants: {
    door_count: number
    layout_rule: {
      rows_per_column: number
      door_height_mm: number
      door_pitch_mm: number
      door_width_mm: number
    }
    component_count_rule: {
      shelves: number
      front_frame_crossbars: number
      electric_lock_hooks: number
    }
    measured_gate: {
      right_column_rotation_ok: string
    }
    pack_and_go_handoff: {
      ok: string
      file_count: number
      external_top_reference_count: number
    }
  }[]
}

type LocalActionResult = {
  status: string
  path: string
  message: string
}

type DrawingSheetMetalIntakeFileRecord = {
  file_name: string
  saved_path: string
  size_bytes: number
  suffix: string
  source_category: string
}

type DrawingSheetMetalExtractionFileResult = {
  file_name: string
  status: 'completed' | 'failed'
  output_dir: string
  summary_path: string | null
  card_path: string | null
  quality_status: string | null
  role_guess: string | null
  manufacturing_bbox_mm: Record<string, number | null>
  circle_count: number | null
  closed_loop_count: number | null
  message: string
}

type DrawingSheetMetalExtractionSummary = {
  status: 'not_started' | 'not_applicable' | 'completed' | 'partial_failed' | 'failed'
  updated_at: string | null
  output_dir: string | null
  processed_files: number
  failed_files: number
  rule_seed_candidates: number
  quality_status_counts: Record<string, number>
  results: DrawingSheetMetalExtractionFileResult[]
  message: string
}

type DrawingSheetMetalCadCheckFileResult = {
  file_name: string
  status: 'completed' | 'failed'
  output_dir: string
  summary_path: string | null
  report_path: string | null
  quality_status: string | null
  model_type: string | null
  assembly_bbox_mm: Record<string, number | null>
  shape_object_count: number | null
  solid_count: number | null
  invalid_shape_count: number | null
  message: string
}

type DrawingSheetMetalCadCheckSummary = {
  status: 'not_started' | 'not_applicable' | 'completed' | 'partial_failed' | 'failed'
  updated_at: string | null
  output_dir: string | null
  processed_files: number
  failed_files: number
  pass_files: number
  quality_status_counts: Record<string, number>
  results: DrawingSheetMetalCadCheckFileResult[]
  message: string
}

type DrawingSheetMetalIntakeRecord = {
  id: string
  status: 'intake_received' | 'blocked_unsupported_file'
  created_at: string
  source_type: string
  notes: string
  output_dir: string
  files: DrawingSheetMetalIntakeFileRecord[]
  next_action: string
  dxf_extraction: DrawingSheetMetalExtractionSummary
  cad_check: DrawingSheetMetalCadCheckSummary
}

type SheetMetalRuleEvidenceSample = {
  file_name: string
  quality_status?: string
  source_path?: string
  bbox_mm?: Record<string, number | null>
  flat_width_mm?: number
  flat_height_mm?: number
  door_index?: number
}

type SheetMetalRuleEvidenceFormula = {
  id: string
  title: string
  role: string
  evidence_level: string
  confidence: string
  formula_text: string
  dimensions_mm: Record<string, number | null>
  sample_count: number
  samples: SheetMetalRuleEvidenceSample[]
  usage_note: string
}

type SheetMetalRuleCandidate = {
  id: string
  title: string
  role: string
  evidence_level: string
  confidence: string
  dimensions_mm: {
    long?: number | null
    short?: number | null
  }
  source_files_count: number
  accepted_source_files_count: number
  blocked_source_files_count: number
  rule_seed: string
  notes: string
  source_examples: SheetMetalRuleEvidenceSample[]
}

type SheetMetalRuleEvidence16029 = {
  generated_at: string | null
  product_family: string
  source_batch: {
    run_id?: string
    source_root?: string
    batch_dir?: string
    manifest_path?: string
    summary_csv_path?: string
  }
  summary: {
    file_count: number
    main_source_file_count: number
    accepted_rule_seed_file_count: number
    formula_count: number
    candidate_count: number
    quality_status_counts: Record<string, number>
    role_counts: Record<string, number>
  }
  formulas: SheetMetalRuleEvidenceFormula[]
  rule_candidates: SheetMetalRuleCandidate[]
  blocked_evidence_summary: {
    blocked_count: number
    quality_status_counts: Record<string, number>
    large_bbox_noise_count: number
  }
  generator_guidance: string[]
  output_paths: Record<string, string>
}

type DrawingUploadMessage = {
  tone: 'good' | 'warn' | 'risk'
  title: string
  detail: string
  outputDir?: string
}

type TemplateCatalog = {
  generatedAt: string
  sourceRoot: string
  projectCount: number
  totals: Record<string, number>
}

type RuleExtractionResult = {
  id: string
  template_id: string
  template_title: string
  assembly_path: string
  status: 'requires_solidworks_run' | 'running' | 'completed' | 'failed'
  created_at: string
  updated_at: string
  output_dir: string
  run_script_path: string
  stdout_path: string | null
  stderr_path: string | null
  exit_code: number | null
  outputs: string[]
  learning_summary: RuleExtractionLearningSummary | null
  message: string
}

type RuleExtractionLearningSummary = {
  counts?: {
    components?: number
    componentsWithTransform?: number
    componentsWithBBox?: number
    mateFeatureCount?: number
    mateEntityRows?: number
    features?: number
    dimensions?: number
    patternRuleCount?: number
    sheetMetalFeatureCount?: number
    stepObjectBBoxCount?: number
    stepSourceObjectCount?: number
    stepSkippedObjectCount?: number
    stepRoleBindingCount?: number
    stepRoleMatchedComponentCount?: number
    stepRoleRepeatedGroupCount?: number
  }
  stepAssemblyBBoxEvidence?: {
    objectCount?: number
    assemblyBBoxMm?: {
      sizeX?: number
      sizeY?: number
      sizeZ?: number
    } | null
  }
  stepComponentRoleEvidence?: {
    objectCount?: number
    matchedComponentCount?: number
    repeatedGroupCount?: number
    roleCounts?: Record<string, number>
  }
  patternRules?: Array<{
    featureName?: string
    instanceCount?: number | null
    spacingMm?: number | null
  }>
  qualityGate?: string
  nextAction?: string
}

type RuleSeedCandidate = {
  ruleType: string
  label: string
  source: string
  value: string
  templateTitle: string
  runId: string
  confidence: string
  blocker: string
  matchedTemplates?: string[]
}

type RuleSeedCatalog = {
  generatedAt: string | null
  candidateCount: number
  candidates: RuleSeedCandidate[]
}

type RuleSeedReviewItem = {
  id: string
  ruleType: string
  label: string
  value: string
  priority: 'P0' | 'P1' | 'P2'
  status: string
  generationGate: string
  confidence: string
  sourceCount: number
  matchedTemplates: string[]
  runIds: string[]
  sources: string[]
  requiredEvidence: string[]
  recommendedAction: string
}

type RuleSeedReviewLedger = {
  generatedAt: string | null
  itemCount: number
  items: RuleSeedReviewItem[]
}

type RuleSeedEvidenceItem = {
  id: string
  priority: 'P0' | 'P1' | 'P2'
  label: string
  value: string
  generationGate: string
  requiredEvidence: string[]
  recommendedAction: string
  evidenceCounts: Record<string, number>
  evidenceState: string
  automationNextAction: string
  matchedTemplates?: string[]
  bomQuantityLinks?: BomQuantityLink[]
}

type BomQuantityRow = {
  sheet: string
  rowNumber: number
  code: string
  name: string
  material: string
  spec: string
  quantity: number | string | null
  matchedToken: string
}

type BomQuantityLink = {
  runId?: string
  templateRoot?: string
  type: string
  path: string
  name: string
  matchedRows: number
  numericQuantitySum: number
  numericQuantityCount: number
  rows: BomQuantityRow[]
}

type RuleSeedEvidenceChecklist = {
  generatedAt: string | null
  itemCount: number
  items: RuleSeedEvidenceItem[]
}

type QuantityFormulaCheck = {
  name: string
  ok: boolean
  actual: number | string | null
  expected: number | string | null
}

type QuantityFormulaVariantRow = {
  variant: string
  expectedDoorCount: number
  smallDoorAssemblies: number
  tallDoorAssemblies: number
  doorTotal: number
  lockHookCount: number
  latchPlateCount: number
}

type QuantityFormulaTemplate = {
  templateRoot: string
  runId: string
  bomFile: string
  bomName: string
  status: string
  derived: Record<string, number | string | null>
  variantRows?: QuantityFormulaVariantRow[]
  checks: QuantityFormulaCheck[]
  supportRows: BomQuantityRow[]
  summary: string
}

type RuleSeedQuantityFormulaItem = {
  id: string
  priority: 'P0' | 'P1' | 'P2'
  label: string
  value: string
  evidenceState: string
  formulaStatus: string
  formulaSummary: string
  generationGate: string
  templateFormulas: QuantityFormulaTemplate[]
}

type RuleSeedQuantityFormulaCatalog = {
  generatedAt: string | null
  source: string
  itemCount: number
  items: RuleSeedQuantityFormulaItem[]
}

type GenerationTask = {
  id: string
  cad_runner: CadRunner
  capability_id: string
  capability_title: string
  product_type: string
  module: string
  maturity: string
  output_level: string
  command: string
  parameters: Record<string, string>
  evidence: string[]
  limitation: string
  status:
    | 'draft_pending_worker'
    | 'ready_to_run'
    | 'blocked_pending_evidence'
    | 'blocked_preflight_failed'
    | 'running'
    | 'completed_reference'
    | 'failed_worker'
    | 'requires_manual_run'
  dry_run_result: DryRunResult | null
  execution_result: WorkerExecutionResult | null
  worker_log_path: string | null
  created_at: string
  updated_at: string
}

const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL ?? 'http://127.0.0.1:8000').replace(/\/$/, '')
const BRAND_MARK_SRC = '/brand/winnsen-mark.png'
const FREECAD_CMD = 'D:\\软件安装录\\freecad\\FreeCAD_1.1.1\\FreeCAD_1.1.1-Windows-x86_64-py311\\FreeCADCmd.exe'
const FREECAD_SHORTCUT = 'C:\\Users\\Administrator\\Desktop\\FreeCAD 1.1.1.lnk'
const SOLIDWORKS_SHORTCUT = 'C:\\Users\\Public\\Desktop\\SOLIDWORKS 2025.lnk'
const DEFAULT_MODEL_CAPABILITY_ID = 'locker_16029_regression'
const LOCKER_16038_RULE_BINDING_CAPABILITY_ID = 'locker_16038_variant_template'
const LOCKER_16038_RULE_BINDING_ID = 'STEP-VARIANT-16038-4-7-8-12'
const LOCKER_16029_OUTER_SIZE = '1000 W × 1917 H × 550 D'
const LOCKER_16029_UNIT_HEIGHT_MM = 152.5
const LOCKER_16029_DOOR_GAP_MM = 7
const LOCKER_16029_DOOR_AREA_HEIGHT_MM = 1827
const LOCKER_16029_GRID_EDGE_GAP_MM = 2
const LOCKER_16029_SUPPORTED_SOLIDWORKS_COUNTS = [10, 12, 14]
const LOCKER_16029_SUPPORTED_FREECAD_COUNTS = [10, 12, 14]
const LOCKER_16029_SUPPORTED_RULE_COUNTS = Array.from(
  new Set([...LOCKER_16029_SUPPORTED_SOLIDWORKS_COUNTS, ...LOCKER_16029_SUPPORTED_FREECAD_COUNTS]),
)
const LOCKER_16029_ENRICHED_REFERENCES = [10, 12, 14].map((doorCount) => ({
  doorCount,
  title: `${doorCount} 门增强矩阵样机 v2`,
  outputDir: `D:\\Winnsen_Structure_Agent_Studio\\workers\\handoffs\\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\\${doorCount}door\\solidworks_native`,
  assembly: `D:\\Winnsen_Structure_Agent_Studio\\workers\\handoffs\\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\\open_${doorCount}door_in_solidworks.cmd`,
  step: `D:\\Winnsen_Structure_Agent_Studio\\workers\\handoffs\\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\\${doorCount}door\\solidworks_native\\16029_1000W_1917H_550D_${doorCount}door_enriched_v2.step`,
  validationReport: `D:\\Winnsen_Structure_Agent_Studio\\workers\\handoffs\\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\\${doorCount}door\\solidworks_native\\solidworks_16029_enriched_${doorCount}door_matrix_validation.md`,
  validationData: `D:\\Winnsen_Structure_Agent_Studio\\workers\\handoffs\\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\\${doorCount}door\\solidworks_native\\solidworks_16029_enriched_${doorCount}door_matrix_validation.csv`,
  candidateMap: `D:\\Winnsen_Structure_Agent_Studio\\workers\\handoffs\\16029_10_12_14_STEP_ENGINEERING_HANDOFF_20260519\\${doorCount}door\\solidworks_native\\native_dependency_manifest.csv`,
}))
const DRAWING_UPLOAD_ACCEPT = [
  '.dxf',
  '.dwg',
  '.pdf',
  '.png',
  '.jpg',
  '.jpeg',
  '.step',
  '.stp',
  '.sldprt',
  '.sldasm',
  '.slddrw',
  '.fcstd',
].join(',')
const DRAWING_UPLOAD_MAX_BYTES = 80 * 1024 * 1024

const cadRunners: Array<{
  id: CadRunner
  label: string
  detail: string
  shortcut: string
}> = [
  {
    id: 'solidworks',
    label: 'SOLIDWORKS 2025',
    detail: '当前工程复核工具 / 原生文件优先',
    shortcut: SOLIDWORKS_SHORTCUT,
  },
  {
    id: 'freecad',
    label: 'FreeCAD 1.1.1',
    detail: '开源替代路线 / FCStd 与 STEP 输出',
    shortcut: FREECAD_SHORTCUT,
  },
]

const pageSections: Array<{ id: PageSectionId; label: string; helper: string }> = [
  { id: 'delivery', label: '工程交付主线', helper: '工程师实际拿模型和确认问题的入口' },
  { id: 'evidence', label: '证据与规则后台', helper: '数据、规则、图纸证据的管理区' },
  { id: 'control', label: 'Agent 控制台', helper: '边界、风险和下一步推进' },
]

const pages: Array<{
  id: PageId
  label: string
  navLabel: string
  description: string
  section: PageSectionId
  purpose: string
  nextAction: string
  icon: typeof Gauge
}> = [
  {
    id: 'overview',
    label: '项目总览',
    navLabel: '总览',
    description: '项目价值、当前成果、交付入口和风险入口',
    section: 'delivery',
    purpose: '给老板或项目负责人快速看当前进度、价值和阻塞。',
    nextAction: '先看 16029 交付状态，再进入模型生成与交接。',
    icon: Gauge,
  },
  {
    id: 'models',
    label: '模型生成与交接',
    navLabel: '模型生成',
    description: 'SolidWorks 当前工程主线 / FreeCAD 开源替代路线',
    section: 'delivery',
    purpose: '结构工程师需要模型时从这里选择门数、准备任务、运行或打开交接包。',
    nextAction: '优先使用 16029 10/12/14 门 verified rule packet 和 Pack-and-Go 交接包。',
    icon: Boxes,
  },
  {
    id: 'drawings',
    label: '图纸生成与钣金出图',
    navLabel: '图纸/钣金',
    description: '图纸/图片/钣金模型到参考模型、展开、标注与规则证据',
    section: 'delivery',
    purpose: '把上传的图纸、DXF、STEP、SLDPRT 等转成可复核的钣金规则证据。',
    nextAction: '先做单件解析和参考展开，正式图纸仍需工程复核。',
    icon: ClipboardList,
  },
  {
    id: 'review',
    label: '待确认项',
    navLabel: '待确认',
    description: 'P0/P1/P2 队列、规则定标和工程交付风险',
    section: 'delivery',
    purpose: '集中看哪些问题会阻塞模型质量和工程交接。',
    nextAction: '优先关闭 16029 层板、锁具、右门镜像和后侧/电气模块相关问题。',
    icon: FileWarning,
  },
  {
    id: 'intake',
    label: '数据录入状态',
    navLabel: '数据录入',
    description: 'BOM / DXF / SolidWorks / STEP / FreeCAD 迁移流水线',
    section: 'evidence',
    purpose: '看 CAD 资产、模板素材和规则学习样本是否已经进入系统。',
    nextAction: '新增素材先进入规则学习，不直接当成可生成模型。',
    icon: Database,
  },
  {
    id: 'rules',
    label: '规则库成熟度',
    navLabel: '规则库',
    description: '模块规则、来源证据、成熟度分布和规则提取入口',
    section: 'evidence',
    purpose: '查看哪些钣金/装配规则已经闭环，哪些仍只是候选。',
    nextAction: '把 verified rule packet 作为 16029 生成器的单一规则源。',
    icon: ShieldCheck,
  },
  {
    id: 'console',
    label: '结构 Agent',
    navLabel: 'Agent',
    description: '生成边界、规则闭环、风险判断和下一步推进',
    section: 'control',
    purpose: '给非工程用户看当前能力边界，避免把参考模型误认为生产图纸。',
    nextAction: '按验证门槛推进下一轮生成器质量修复。',
    icon: Bot,
  },
]

const maturityLabels: Record<Maturity, string> = {
  raw_imported: 'raw',
  mapped: 'mapped',
  dxf_parsed: 'dxf_parsed',
  solidworks_extracted: 'solidworks_extracted',
  step_bbox_measured: 'step_bbox_measured',
  engineering_reference: 'engineering_reference',
  production_candidate: 'production_candidate',
  manual_review_required: 'manual_review_required',
  blocked: 'blocked',
}

const evidenceTone: Record<string, string> = {
  DXF: 'blue',
  STEP: 'cyan',
  SolidWorks: 'green',
  FreeCAD: 'indigo',
  BOM: 'slate',
  工程图: 'amber',
  'DXF 展开尺寸': 'blue',
  'BOM/装配数量': 'slate',
  证据闭环记录: 'red',
  规则定标: 'red',
}

function App() {
  const [activePage, setActivePage] = useState<PageId>('overview')
  const [selectedProjectId, setSelectedProjectId] = useState(projects[0].id)
  const [mobileNavOpen, setMobileNavOpen] = useState(false)
  const [query, setQuery] = useState('')

  const selectedProject = projects.find((project) => project.id === selectedProjectId) ?? projects[0]
  const currentPage = pages.find((page) => page.id === activePage) ?? pages[0]

  const reviewCounts = useMemo(
    () =>
      reviewItems.reduce<Record<string, number>>((acc, item) => {
        acc[item.priority] = (acc[item.priority] ?? 0) + 1
        return acc
      }, {}),
    [],
  )

  const filteredReviewItems = useMemo(() => {
    const term = query.trim().toLowerCase()
    if (!term) return reviewItems
    return reviewItems.filter((item) =>
      [item.id, item.project, item.module, item.issue, item.nextAction].some((value) =>
        value.toLowerCase().includes(term),
      ),
    )
  }, [query])

  return (
    <div className="app-shell">
      <aside className={`sidebar ${mobileNavOpen ? 'sidebar-open' : ''}`}>
        <div className="brand-row">
          <div className="brand-mark" aria-hidden="true">
            <img src={BRAND_MARK_SRC} alt="" />
          </div>
          <div className="brand-copy">
            <strong>WINNSEN INDUSTRY</strong>
            <span>Structure Agent Studio</span>
          </div>
          <button className="icon-button sidebar-close" type="button" onClick={() => setMobileNavOpen(false)}>
            <X size={18} />
          </button>
        </div>

        <nav className="nav-stack" aria-label="主导航">
          {pageSections.map((section) => (
            <div key={section.id} className="nav-section">
              <div className="nav-section-copy">
                <span className="nav-section-label">{section.label}</span>
                <small>{section.helper}</small>
              </div>
              <div className="nav-section-items">
                {pages
                  .filter((page) => page.section === section.id)
                  .map((page) => {
                    const Icon = page.icon
                    return (
                      <button
                        key={page.id}
                        type="button"
                        data-page-id={page.id}
                        className={`nav-item ${activePage === page.id ? 'active' : ''}`}
                        onClick={() => {
                          setActivePage(page.id)
                          setMobileNavOpen(false)
                        }}
                      >
                        <Icon size={19} />
                        <span>{page.navLabel}</span>
                      </button>
                    )
                  })}
              </div>
            </div>
          ))}
        </nav>

        <div className="sidebar-panel">
          <span className="panel-label">当前数据资产</span>
          <strong>{sourcePaths.cadWorkspace}</strong>
          <small>新增模板素材：{sourcePaths.parametricTemplateRoot}</small>
        </div>
      </aside>

      {mobileNavOpen && <button className="scrim" type="button" aria-label="关闭导航" onClick={() => setMobileNavOpen(false)} />}

      <main className="workspace">
        <header className="topbar">
          <button className="icon-button mobile-menu" type="button" onClick={() => setMobileNavOpen(true)}>
            <Menu size={20} />
          </button>
          <div>
            <div className="eyeline">本地优先 / 证据驱动 / 非生产图纸承诺</div>
            <h1>{currentPage.label}</h1>
            <p>{currentPage.description}</p>
          </div>
          <div className="topbar-actions">
            <label className="project-switcher">
              <span>当前项目</span>
              <select value={selectedProjectId} onChange={(event) => setSelectedProjectId(event.target.value)}>
                {projects.map((project) => (
                  <option key={project.id} value={project.id}>
                    {project.name}
                  </option>
                ))}
              </select>
            </label>
          </div>
        </header>

        {activePage === 'overview' && (
          <OverviewPage selectedProject={selectedProject} reviewCounts={reviewCounts} onNavigate={setActivePage} />
        )}
        {activePage === 'intake' && <IntakePage selectedProject={selectedProject} />}
        {activePage === 'rules' && <RulesPage />}
        {activePage === 'models' && <ModelsPage />}
        {activePage === 'drawings' && <DrawingSheetMetalPage />}
        {activePage === 'console' && (
          <AgentConsolePage selectedProject={selectedProject} reviewCounts={reviewCounts} onNavigate={setActivePage} />
        )}
        {activePage === 'review' && (
          <ReviewPage query={query} setQuery={setQuery} filteredReviewItems={filteredReviewItems} />
        )}
      </main>
    </div>
  )
}

function OverviewPage({
  selectedProject,
  reviewCounts,
  onNavigate,
}: {
  selectedProject: Project
  reviewCounts: Record<string, number>
  onNavigate: (page: PageId) => void
}) {
  return (
    <div className="page-grid">
      <section className="metric-strip" aria-label="项目摘要">
        {metricCards.map((metric) => (
          <article key={metric.label} className={`metric-card tone-${metric.tone}`}>
            <span>{metric.label}</span>
            <strong>{metric.value}</strong>
            <small>{metric.detail}</small>
          </article>
        ))}
      </section>

      <PageMapSection onNavigate={onNavigate} />

      <section className="section-block">
        <div className="section-heading">
          <div>
            <h2>项目健康度总览</h2>
            <p>按照工程参考、生产候选、阻塞项分开呈现，避免生成结果和生产图纸混淆。</p>
          </div>
          <button className="ghost-button" type="button" onClick={() => onNavigate('review')}>
            查看待确认项
            <ChevronRight size={16} />
          </button>
        </div>

        <div className="project-grid">
          {projects.map((project) => (
            <ProjectCard key={project.id} project={project} />
          ))}
        </div>
      </section>

      <section className="split-grid">
        <article className="section-block compact-block">
          <div className="section-heading">
            <div>
              <h2>当前项目焦点</h2>
              <p>{selectedProject.sourceRoot}</p>
            </div>
            <StatusPill tone={selectedProject.statusTone}>{selectedProject.status}</StatusPill>
          </div>
          <div className="focus-layout">
            <ProgressDial value={selectedProject.progress} />
            <div className="focus-copy">
              <strong>{selectedProject.name}</strong>
              <span>{selectedProject.modelStatus}</span>
              <p>{selectedProject.risk}</p>
            </div>
          </div>
          <ul className="note-list">
            {selectedProject.notes.map((note) => (
              <li key={note}>{note}</li>
            ))}
          </ul>
        </article>

        <article className="section-block compact-block">
          <div className="section-heading">
            <div>
              <h2>阻塞与确认分布</h2>
              <p>优先处理 P0/P1，P2 保持为工程参考限制。</p>
            </div>
          </div>
          <div className="priority-grid">
            {(['P0', 'P1', 'P2', 'P3'] as const).map((priority) => (
              <div key={priority} className={`priority-cell priority-${priority.toLowerCase()}`}>
                <span>{priority}</span>
                <strong>{reviewCounts[priority] ?? 0}</strong>
              </div>
            ))}
          </div>
          <div className="source-paths">
            <span>关键状态文档</span>
            <code>{sourcePaths.intakeStatus}</code>
            <code>{sourcePaths.outdoorGate}</code>
          </div>
        </article>
      </section>
    </div>
  )
}

function PageMapSection({ onNavigate }: { onNavigate: (page: PageId) => void }) {
  return (
    <section className="section-block">
      <div className="section-heading">
        <div>
          <h2>软件页面地图</h2>
          <p>把工程师拿模型的入口放前面，数据、规则和 Agent 控制放到后台页面。</p>
        </div>
      </div>
      <div className="page-map-grid">
        {pages.map((page) => {
          const Icon = page.icon
          const section = pageSections.find((item) => item.id === page.section)
          return (
            <button key={page.id} type="button" className="page-map-card" onClick={() => onNavigate(page.id)}>
              <div className="page-map-card-header">
                <span className="page-map-icon" aria-hidden="true">
                  <Icon size={18} />
                </span>
                <span>{section?.label}</span>
              </div>
              <strong>{page.label}</strong>
              <p>{page.purpose}</p>
              <small>{page.nextAction}</small>
            </button>
          )
        })}
      </div>
    </section>
  )
}

function IntakePage({ selectedProject }: { selectedProject: Project }) {
  const completed = pipelineRows.filter((row) => row.status === 'pass').length
  const partial = pipelineRows.filter((row) => row.status === 'partial').length
  const blocked = pipelineRows.filter((row) => row.status === 'blocked').length

  return (
    <div className="page-grid">
      <section className="section-block">
        <div className="section-heading">
          <div>
            <h2>录入流水线状态</h2>
            <p>源文件保留在 CAD 资产工作区，App 只读取归一化状态摘要。</p>
          </div>
          <div className="inline-stats">
            <StatusPill tone="good">{completed} pass</StatusPill>
            <StatusPill tone="warn">{partial} partial</StatusPill>
            <StatusPill tone="risk">{blocked} blocked</StatusPill>
          </div>
        </div>
        <div className="pipeline-flow">
          {pipelineRows.map((row, index) => (
            <div key={row.stage} className={`pipeline-step status-${row.status}`}>
              <span>{index + 1}</span>
              <strong>{row.stage}</strong>
              <small>{row.owner}</small>
            </div>
          ))}
        </div>
      </section>

      <section className="section-block">
        <div className="section-heading">
          <div>
            <h2>阶段明细</h2>
            <p>16029 与户外柜复用同一条 intake pipeline；未来项目进入前先补 source root。</p>
          </div>
        </div>
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>阶段</th>
                <th>16029</th>
                <th>户外柜</th>
                <th>下一步</th>
                <th>证据</th>
                <th>风险</th>
              </tr>
            </thead>
            <tbody>
              {pipelineRows.map((row) => (
                <tr key={row.stage}>
                  <td>
                    <strong>{row.stage}</strong>
                    <StatusDot status={row.status} />
                  </td>
                  <td>{row.locker16029}</td>
                  <td>{row.outdoor}</td>
                  <td>{row.next}</td>
                  <td>
                    <EvidenceRow evidence={row.evidence} />
                  </td>
                  <td>{row.risk}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>

      <section className="section-block">
        <div className="section-heading">
          <div>
            <h2>{selectedProject.name} 数据录入摘要</h2>
            <p>{selectedProject.updatedAt}</p>
          </div>
        </div>
        <div className="stat-row">
          {selectedProject.stats.map((stat) => (
            <div key={stat.label} className={`record-stat tone-${stat.tone ?? 'idle'}`}>
              <span>{stat.label}</span>
              <strong>{stat.value}</strong>
            </div>
          ))}
        </div>
      </section>
    </div>
  )
}

function RulesPage() {
  const total = maturityDistribution.reduce((sum, item) => sum + item.count, 0)
  const [templateCatalog, setTemplateCatalog] = useState<TemplateCatalog | null>(null)
  const [catalogState, setCatalogState] = useState<'checking' | 'online' | 'refreshing' | 'offline'>('checking')
  const [catalogMessage, setCatalogMessage] = useState('正在读取参数化模板素材目录...')
  const [extractionRuns, setExtractionRuns] = useState<RuleExtractionResult[]>([])
  const [extractionState, setExtractionState] = useState<'checking' | 'online' | 'offline' | 'running'>('checking')
  const [extractionMessage, setExtractionMessage] = useState('正在读取 SolidWorks 规则提取记录...')
  const [ruleExtractionBusy, setRuleExtractionBusy] = useState<string | null>(null)
  const [ruleSeedCatalog, setRuleSeedCatalog] = useState<RuleSeedCatalog | null>(null)
  const [ruleSeedMessage, setRuleSeedMessage] = useState('正在读取规则种子候选表...')
  const [ruleSeedReviewLedger, setRuleSeedReviewLedger] = useState<RuleSeedReviewLedger | null>(null)
  const [ruleSeedReviewMessage, setRuleSeedReviewMessage] = useState('正在读取规则定标台账...')
  const [ruleSeedEvidenceChecklist, setRuleSeedEvidenceChecklist] = useState<RuleSeedEvidenceChecklist | null>(null)
  const [ruleSeedEvidenceMessage, setRuleSeedEvidenceMessage] = useState('正在读取自动证据闭环...')
  const [quantityFormulaCatalog, setQuantityFormulaCatalog] = useState<RuleSeedQuantityFormulaCatalog | null>(null)
  const [quantityFormulaMessage, setQuantityFormulaMessage] = useState('正在读取阵列数公式候选...')
  const priorityExtractionAssets = useMemo(
    () => templateAssets.filter((asset) => ['高', '中高'].includes(asset.ruleLearningValue)).slice(0, 5),
    [],
  )
  const topRuleSeeds = useMemo(
    () =>
      (ruleSeedCatalog?.candidates ?? [])
        .filter((candidate) => candidate.confidence === 'cross_template_seed')
        .slice(0, 8),
    [ruleSeedCatalog],
  )
  const topReviewItems = useMemo(() => (ruleSeedReviewLedger?.items ?? []).slice(0, 8), [ruleSeedReviewLedger])
  const topEvidenceItems = useMemo(() => (ruleSeedEvidenceChecklist?.items ?? []).slice(0, 8), [ruleSeedEvidenceChecklist])
  const topQuantityFormulaItems = useMemo(() => (quantityFormulaCatalog?.items ?? []).slice(0, 4), [quantityFormulaCatalog])
  const latestRunByTemplate = useMemo(() => {
    const index: Record<string, RuleExtractionResult> = {}
    extractionRuns.forEach((run) => {
      if (!index[run.template_id]) index[run.template_id] = run
    })
    return index
  }, [extractionRuns])
  const hasRunningExtraction = useMemo(
    () => extractionRuns.some((run) => run.status === 'running') || (ruleExtractionBusy?.startsWith('run:') ?? false),
    [extractionRuns, ruleExtractionBusy],
  )

  const refreshRuleExtractions = useCallback(async (nextMessage?: string) => {
    try {
      const response = await fetch(`${API_BASE_URL}/api/template-rule-extractions`)
      if (!response.ok) throw new Error(await response.text())
      const runs = (await response.json()) as RuleExtractionResult[]
      setExtractionRuns(runs)
      const anyRunning = runs.some((run) => run.status === 'running')
      setExtractionState(anyRunning ? 'running' : 'online')
      setExtractionMessage(nextMessage ?? ruleExtractionListMessage(runs))
    } catch (error) {
      setExtractionState('offline')
      setExtractionMessage(`规则提取记录刷新失败: ${error instanceof Error ? error.message : 'unknown error'}`)
    }
  }, [])

  useEffect(() => {
    let active = true

    async function loadTemplateCatalog() {
      setCatalogState('checking')
      try {
        const response = await fetch(`${API_BASE_URL}/api/template-assets`)
        if (!response.ok) throw new Error(await response.text())
        const catalog = (await response.json()) as TemplateCatalog
        if (!active) return
        setTemplateCatalog(catalog)
        setCatalogState('online')
        setCatalogMessage(`已读取 ${catalog.projectCount} 个项目目录，${catalog.totals.SLDASM ?? 0} 个装配，${catalog.totals.DXF ?? 0} 个 DXF。`)
      } catch (error) {
        if (!active) return
        setCatalogState('offline')
        setCatalogMessage(`模板目录 API 不可用: ${error instanceof Error ? error.message : 'unknown error'}`)
      }
    }

    loadTemplateCatalog()
    return () => {
      active = false
    }
  }, [])

  useEffect(() => {
    let active = true

    async function loadRuleSeeds() {
      try {
        const response = await fetch(`${API_BASE_URL}/api/rule-seed-candidates`)
        if (!response.ok) throw new Error(await response.text())
        const catalog = (await response.json()) as RuleSeedCatalog
        if (!active) return
        setRuleSeedCatalog(catalog)
        setRuleSeedMessage(
          catalog.candidateCount
            ? `已读取 ${catalog.candidateCount} 条候选规则种子，优先看跨模板重复项。`
            : '暂无规则种子候选表。',
        )
      } catch (error) {
        if (!active) return
        setRuleSeedMessage(`规则种子候选表不可用: ${error instanceof Error ? error.message : 'unknown error'}`)
      }
    }

    loadRuleSeeds()
    return () => {
      active = false
    }
  }, [])

  useEffect(() => {
    let active = true

    async function loadRuleSeedReviewLedger() {
      try {
        const response = await fetch(`${API_BASE_URL}/api/rule-seed-review-ledger`)
        if (!response.ok) throw new Error(await response.text())
        const ledger = (await response.json()) as RuleSeedReviewLedger
        if (!active) return
        setRuleSeedReviewLedger(ledger)
        setRuleSeedReviewMessage(
          ledger.itemCount ? `已归并 ${ledger.itemCount} 个规则定标项，P0 项优先自动闭环。` : '暂无规则定标台账。',
        )
      } catch (error) {
        if (!active) return
        setRuleSeedReviewMessage(`规则定标台账不可用: ${error instanceof Error ? error.message : 'unknown error'}`)
      }
    }

    loadRuleSeedReviewLedger()
    return () => {
      active = false
    }
  }, [])

  useEffect(() => {
    let active = true

    async function loadRuleSeedEvidenceChecklist() {
      try {
        const response = await fetch(`${API_BASE_URL}/api/rule-seed-evidence-checklist`)
        if (!response.ok) throw new Error(await response.text())
        const checklist = (await response.json()) as RuleSeedEvidenceChecklist
        if (!active) return
        setRuleSeedEvidenceChecklist(checklist)
        setRuleSeedEvidenceMessage(
          checklist.itemCount ? `已读取 ${checklist.itemCount} 个 P0/P1 自动证据闭环项。` : '暂无自动证据闭环清单。',
        )
      } catch (error) {
        if (!active) return
        setRuleSeedEvidenceMessage(`自动证据闭环不可用: ${error instanceof Error ? error.message : 'unknown error'}`)
      }
    }

    loadRuleSeedEvidenceChecklist()
    return () => {
      active = false
    }
  }, [])

  useEffect(() => {
    let active = true

    async function loadQuantityFormulas() {
      try {
        const response = await fetch(`${API_BASE_URL}/api/rule-seed-quantity-formulas`)
        if (!response.ok) throw new Error(await response.text())
        const catalog = (await response.json()) as RuleSeedQuantityFormulaCatalog
        if (!active) return
        setQuantityFormulaCatalog(catalog)
        setQuantityFormulaMessage(
          catalog.itemCount ? `已生成 ${catalog.itemCount} 个 P0 数量公式候选。` : '暂无阵列数公式候选。',
        )
      } catch (error) {
        if (!active) return
        setQuantityFormulaMessage(`阵列数公式候选不可用: ${error instanceof Error ? error.message : 'unknown error'}`)
      }
    }

    loadQuantityFormulas()
    return () => {
      active = false
    }
  }, [])

  useEffect(() => {
    let active = true

    async function loadRuleExtractions() {
      setExtractionState('checking')
      try {
        const response = await fetch(`${API_BASE_URL}/api/template-rule-extractions`)
        if (!response.ok) throw new Error(await response.text())
        const runs = (await response.json()) as RuleExtractionResult[]
        if (!active) return
        setExtractionRuns(runs)
        setExtractionState(runs.some((run) => run.status === 'running') ? 'running' : 'online')
        setExtractionMessage(ruleExtractionListMessage(runs))
      } catch (error) {
        if (!active) return
        setExtractionState('offline')
        setExtractionMessage(`规则提取 API 不可用: ${error instanceof Error ? error.message : 'unknown error'}`)
      }
    }

    loadRuleExtractions()
    return () => {
      active = false
    }
  }, [])

  useEffect(() => {
    if (!hasRunningExtraction) return undefined
    const intervalId = window.setInterval(() => {
      void refreshRuleExtractions()
    }, 3000)
    return () => window.clearInterval(intervalId)
  }, [hasRunningExtraction, refreshRuleExtractions])

  async function rescanTemplateCatalog() {
    setCatalogState('refreshing')
    setCatalogMessage('正在重扫 C:\\Users\\Administrator\\Desktop\\参数化模板素材...')
    try {
      const response = await fetch(`${API_BASE_URL}/api/template-assets/rescan`, { method: 'POST' })
      if (!response.ok) throw new Error(await response.text())
      const catalog = (await response.json()) as TemplateCatalog
      setTemplateCatalog(catalog)
      setCatalogState('online')
      setCatalogMessage(`重扫完成：${catalog.projectCount} 个项目目录，${catalog.totals.SLDASM ?? 0} 个装配，${catalog.totals.DXF ?? 0} 个 DXF。`)
    } catch (error) {
      setCatalogState('offline')
      setCatalogMessage(`重扫失败: ${error instanceof Error ? error.message : 'unknown error'}`)
    }
  }

  async function prepareRuleExtraction(asset: TemplateAsset) {
    setRuleExtractionBusy(`prepare:${asset.id}`)
    setExtractionMessage(`正在为 ${asset.title} 准备 SolidWorks 规则提取包...`)
    try {
      const response = await fetch(`${API_BASE_URL}/api/template-rule-extractions`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          template_id: asset.id,
          template_title: asset.title,
          assembly_path: asset.sourceAssemblyPath,
        }),
      })
      if (!response.ok) throw new Error(await response.text())
      const run = (await response.json()) as RuleExtractionResult
      setExtractionRuns((runs) => [run, ...runs.filter((item) => item.id !== run.id)])
      setExtractionState('online')
      setExtractionMessage(`已准备 ${asset.title} 的规则提取包：${run.id}。`)
    } catch (error) {
      setExtractionState('offline')
      setExtractionMessage(`准备规则提取包失败: ${error instanceof Error ? error.message : 'unknown error'}`)
    } finally {
      setRuleExtractionBusy(null)
    }
  }

  async function runRuleExtraction(run: RuleExtractionResult) {
    const runningRun: RuleExtractionResult = {
      ...run,
      status: 'running',
      updated_at: new Date().toISOString(),
      message: 'SolidWorks 规则提取已启动，页面会自动刷新完成状态。',
    }
    setRuleExtractionBusy(`run:${run.id}`)
    setExtractionState('running')
    setExtractionRuns((runs) => [runningRun, ...runs.filter((item) => item.id !== runningRun.id)])
    setExtractionMessage(ruleExtractionFeedbackMessage(runningRun))
    try {
      const response = await fetch(`${API_BASE_URL}/api/template-rule-extractions/${run.id}/run`, { method: 'POST' })
      if (!response.ok) throw new Error(await response.text())
      const updated = (await response.json()) as RuleExtractionResult
      setExtractionRuns((runs) => [updated, ...runs.filter((item) => item.id !== updated.id)])
      setExtractionState(updated.status === 'running' ? 'running' : 'online')
      setExtractionMessage(ruleExtractionFeedbackMessage(updated))
    } catch (error) {
      setExtractionState('offline')
      setExtractionMessage(`运行规则提取失败: ${error instanceof Error ? error.message : 'unknown error'}`)
    } finally {
      setRuleExtractionBusy(null)
      void refreshRuleExtractions()
    }
  }

  async function openRuleExtractionFolder(run: RuleExtractionResult) {
    setRuleExtractionBusy(`open:${run.id}`)
    try {
      const response = await fetch(`${API_BASE_URL}/api/local-actions/open-path`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ path: run.output_dir, mode: 'open' }),
      })
      if (!response.ok) throw new Error(await response.text())
      const result = (await response.json()) as LocalActionResult
      setExtractionMessage(result.message)
    } catch (error) {
      setExtractionMessage(`打开规则提取目录失败: ${error instanceof Error ? error.message : 'unknown error'}`)
    } finally {
      setRuleExtractionBusy(null)
    }
  }

  return (
    <div className="page-grid">
      <section className="split-grid">
        <article className="section-block compact-block">
          <div className="section-heading">
            <div>
              <h2>规则库成熟度分布</h2>
              <p>production_candidate 当前为 0，避免过度承诺。</p>
            </div>
          </div>
          <div className="maturity-stack">
            {maturityDistribution.map((item) => (
              <div key={item.maturity} className="maturity-row">
                <div>
                  <span className={`maturity-dot tone-${item.tone}`} />
                  <strong>{maturityLabels[item.maturity]}</strong>
                </div>
                <span>{item.count}</span>
                <div className="bar-track">
                  <div className={`bar-fill tone-${item.tone}`} style={{ width: `${(item.count / total) * 100}%` }} />
                </div>
              </div>
            ))}
          </div>
        </article>

        <article className="section-block compact-block">
          <div className="section-heading">
            <div>
              <h2>成熟度规则</h2>
              <p>所有规则必须能回溯到 DXF、STEP、SolidWorks、FreeCAD、BOM 或证据闭环记录。</p>
            </div>
          </div>
          <div className="principle-list">
            <div>
              <CheckCircle2 size={18} />
              <span>工程图、DXF、STEP、SolidWorks、FreeCAD 是几何证据。</span>
            </div>
            <div>
              <AlertTriangle size={18} />
              <span>LLM 只负责解释、审查、规划和风险判断，不直接成为几何来源。</span>
            </div>
            <div>
              <Wrench size={18} />
              <span>production_candidate 必须具备来源证据和验证报告。</span>
            </div>
          </div>
        </article>
      </section>

      <section className="section-block">
        <div className="section-heading">
          <div>
            <h2>参数化模板素材索引</h2>
            <p>新增素材先作为规则学习样本，只有证据链闭环后才升级为可生成模型。</p>
          </div>
          <StatusPill tone={catalogState === 'offline' ? 'risk' : catalogState === 'online' ? 'good' : 'warn'}>
            {catalogState === 'refreshing' ? '重扫中' : catalogState}
          </StatusPill>
        </div>
        <div className="catalog-summary">
          <div>
            <span>本地素材目录</span>
            <strong>{templateCatalog?.sourceRoot ?? sourcePaths.parametricTemplateRoot}</strong>
            <small>{catalogMessage}</small>
          </div>
          <div className="catalog-stats">
            <span><strong>{templateCatalog?.projectCount ?? 9}</strong> 项目</span>
            <span><strong>{templateCatalog?.totals.SLDASM ?? 559}</strong> SLDASM</span>
            <span><strong>{templateCatalog?.totals.SLDPRT ?? 1797}</strong> SLDPRT</span>
            <span><strong>{templateCatalog?.totals.DXF ?? 793}</strong> DXF</span>
          </div>
          <button
            className="secondary-action"
            type="button"
            disabled={catalogState === 'refreshing'}
            onClick={rescanTemplateCatalog}
          >
            <Database size={16} />
            {catalogState === 'refreshing' ? '正在重扫' : '重扫素材'}
          </button>
        </div>
        <div className="template-grid">
          {templateAssets.map((asset) => (
            <TemplateAssetCard key={asset.id} asset={asset} />
          ))}
        </div>
      </section>

      <section className="section-block">
        <div className="section-heading">
          <div>
            <h2>SolidWorks 规则提取入口</h2>
            <p>优先读取标准 SolidWorks 母版的组件树、配合、阵列尺寸和 STEP bbox，用来学习同尺寸补门数规则。</p>
          </div>
          <StatusPill tone={extractionState === 'offline' ? 'risk' : extractionState === 'running' ? 'warn' : 'good'}>
            {extractionState}
          </StatusPill>
        </div>
        <div className="catalog-summary">
          <div>
            <span>提取策略</span>
            <strong>先抽 16038 / 16029 / 16028 / 主控柜样本，再决定哪些规则可进入生成器。</strong>
          <small aria-live="polite">{extractionMessage}</small>
          </div>
          <button className="secondary-action" type="button" onClick={() => refreshRuleExtractions()}>
            <Database size={16} />
            刷新记录
          </button>
        </div>
        <div className="extraction-grid">
          {priorityExtractionAssets.map((asset) => (
            <RuleExtractionCard
              key={asset.id}
              asset={asset}
              latestRun={latestRunByTemplate[asset.id]}
              busyKey={ruleExtractionBusy}
              onPrepare={prepareRuleExtraction}
              onRun={runRuleExtraction}
              onOpen={openRuleExtractionFolder}
            />
          ))}
        </div>
      </section>

      <section className="section-block">
        <div className="section-heading">
          <div>
            <h2>规则种子候选表</h2>
            <p>从 SolidWorks 阵列尺寸和距离配合抽出的候选规则，先进入自动证据闭环。</p>
          </div>
          <StatusPill tone={topRuleSeeds.length ? 'warn' : 'idle'}>{ruleSeedCatalog?.candidateCount ?? 0} seeds</StatusPill>
        </div>
        <div className="catalog-summary">
          <div>
            <span>当前结论</span>
            <strong>跨模板重复项优先，DXF / BOM / 图纸证据闭环后才能进入生成器。</strong>
            <small>{ruleSeedMessage}</small>
          </div>
        </div>
        <div className="rule-seed-grid">
          {topRuleSeeds.map((candidate) => (
            <RuleSeedCard key={`${candidate.runId}-${candidate.ruleType}-${candidate.value}-${candidate.label}`} candidate={candidate} />
          ))}
        </div>
      </section>

      <section className="section-block">
        <div className="section-heading">
          <div>
            <h2>规则定标台账</h2>
            <p>按规则和值归并候选项，P0 优先自动做证据闭环。</p>
          </div>
          <StatusPill tone={topReviewItems.some((item) => item.priority === 'P0') ? 'risk' : 'warn'}>
            {ruleSeedReviewLedger?.itemCount ?? 0} items
          </StatusPill>
        </div>
        <div className="catalog-summary">
          <div>
            <span>生成器门槛</span>
            <strong>所有台账项默认 blocked_pending_evidence_closure，证据闭环前不进入自动生成。</strong>
            <small>{ruleSeedReviewMessage}</small>
          </div>
        </div>
        <div className="review-ledger-grid">
          {topReviewItems.map((item) => (
            <RuleSeedReviewCard key={item.id} item={item} />
          ))}
        </div>
      </section>

      <section className="section-block">
        <div className="section-heading">
          <div>
            <h2>自动证据闭环</h2>
            <p>系统先自动匹配 DXF、工程图和数量关系，只有冲突或缺口才进入复核。</p>
          </div>
          <StatusPill tone={topEvidenceItems.some((item) => item.evidenceState !== 'auto_calibration_candidate') ? 'warn' : 'good'}>
            {ruleSeedEvidenceChecklist?.itemCount ?? 0} checks
          </StatusPill>
        </div>
        <div className="catalog-summary">
          <div>
            <span>下一步自动动作</span>
            <strong>当前重点是把 P0 规则的 BOM/数量/几何证据自动连起来，再进入阵列数公式核对。</strong>
            <small>{ruleSeedEvidenceMessage}</small>
          </div>
        </div>
        <div className="evidence-closure-grid">
          {topEvidenceItems.map((item) => (
            <RuleSeedEvidenceCard key={item.id} item={item} />
          ))}
        </div>
      </section>

      <section className="section-block">
        <div className="section-heading">
          <div>
            <h2>阵列数公式候选</h2>
            <p>BOM 行级数量进一步推导门数、列数、每列层板数量，供生成器后续使用。</p>
          </div>
          <StatusPill tone={topQuantityFormulaItems.some((item) => item.formulaStatus === 'formula_consistent_candidate') ? 'good' : 'warn'}>
            {quantityFormulaCatalog?.itemCount ?? 0} formulas
          </StatusPill>
        </div>
        <div className="catalog-summary">
          <div>
            <span>公式状态</span>
            <strong>柜门列距已形成 2 列 x 6 行候选；16038 已新增门/锁角色公式候选，下一步闭环 4/7/8/12 门变体。</strong>
            <small>{quantityFormulaMessage}</small>
          </div>
        </div>
        <div className="quantity-formula-grid">
          {topQuantityFormulaItems.map((item) => (
            <RuleSeedQuantityFormulaCard key={item.id} item={item} />
          ))}
        </div>
      </section>

      <section className="section-block">
        <div className="section-heading">
          <div>
            <h2>同尺寸规则学习轴</h2>
            <p>目标是把门数、柜深、操作区和工艺差异拆成可验证规则，再给生成器使用。</p>
          </div>
          <StatusPill tone="good">SolidWorks 优先</StatusPill>
        </div>
        <div className="axis-grid">
          {ruleLearningAxes.map((axis) => (
            <RuleLearningAxisCard key={axis.axis} axis={axis} />
          ))}
        </div>
      </section>

      <section className="section-block">
        <div className="section-heading">
          <div>
            <h2>模块规则族</h2>
            <p>按模块、证据来源、成熟度和下一动作跟踪。</p>
          </div>
        </div>
        <div className="rule-grid">
          {ruleFamilies.map((rule) => (
            <RuleCard key={rule.family} rule={rule} />
          ))}
        </div>
      </section>
    </div>
  )
}

function ModelsPage() {
  const [activeCapabilityId, setActiveCapabilityId] = useState(
    () => capabilities.find((capability) => capability.id === DEFAULT_MODEL_CAPABILITY_ID)?.id ?? capabilities[0].id,
  )
  const [parameterValuesByCapability, setParameterValuesByCapability] = useState<Record<string, ParameterValues>>({})
  const [generationTasks, setGenerationTasks] = useState<GenerationTask[]>([])
  const [selectedTaskId, setSelectedTaskId] = useState<string | null>(null)
  const [queueState, setQueueState] = useState<'checking' | 'online' | 'offline' | 'saving'>('checking')
  const [queueMessage, setQueueMessage] = useState('正在连接本地生成队列...')
  const [dryRunTaskId, setDryRunTaskId] = useState<string | null>(null)
  const [executeTaskId, setExecuteTaskId] = useState<string | null>(null)
  const [solidWorksRunTaskId, setSolidWorksRunTaskId] = useState<string | null>(null)
  const [solidWorksAutoRunBusy, setSolidWorksAutoRunBusy] = useState(false)
  const [drawerMessage, setDrawerMessage] = useState('')
  const [generationFeedback, setGenerationFeedback] = useState<GenerationFeedback | null>(null)
  const [localActionBusy, setLocalActionBusy] = useState<string | null>(null)
  const [quantityFormulaCatalog, setQuantityFormulaCatalog] = useState<RuleSeedQuantityFormulaCatalog | null>(null)
  const [quantityFormulaMessage, setQuantityFormulaMessage] = useState('正在读取 16038 规则绑定证据...')
  const [variantQualityMatrix, setVariantQualityMatrix] = useState<Locker16029VariantQualityMatrix | null>(null)
  const [variantQualityMessage, setVariantQualityMessage] = useState('正在读取 16029 质量矩阵...')
  const [verifiedRulePacket, setVerifiedRulePacket] = useState<Locker16029VerifiedRulePacket | null>(null)
  const [verifiedRulePacketMessage, setVerifiedRulePacketMessage] = useState('正在读取 16029 已验证规则包...')
  const [engineeringHandoffBundle, setEngineeringHandoffBundle] =
    useState<Locker16029EngineeringHandoffBundle | null>(null)
  const [engineeringHandoffMessage, setEngineeringHandoffMessage] = useState('正在读取 16029 工程交接包...')
  const activeCapability = capabilities.find((capability) => capability.id === activeCapabilityId) ?? capabilities[0]
  const parameterValues = useMemo(
    () => ({
      ...defaultParameterValues(activeCapability.id, activeCapability.parameters),
      ...(parameterValuesByCapability[activeCapability.id] ?? {}),
    }),
    [activeCapability.id, activeCapability.parameters, parameterValuesByCapability],
  )
  const selectedTask = generationTasks.find((task) => task.id === selectedTaskId) ?? null
  const canCreateTask = activeCapability.status === 'generatable' || activeCapability.status === 'reference_only'
  const queueOnline = queueState === 'online' || queueState === 'saving'
  const parameterIssue = parameterIssueFor(activeCapability.id, parameterValues)
  const normalizedParameterValues = parameterPayload()
  const runnerIssueById = Object.fromEntries(
    cadRunners.map((runner) => [runner.id, runnerIssueFor(activeCapability.id, runner.id, normalizedParameterValues)]),
  ) as Record<CadRunner, string | null>
  const firstRunnerIssue =
    cadRunners.map((runner) => runnerIssueById[runner.id]).find((issue): issue is string => Boolean(issue)) ?? null
  const activeDoorCountPresets = doorCountPresetsFor(activeCapability.id)
  const canSubmitTask = canCreateTask && queueOnline && queueState !== 'saving' && !parameterIssue
  const prioritizedGenerationTasks = prioritizeGenerationTasks(generationTasks, activeCapability.id, normalizedParameterValues)
  const nonLegacyActiveTasks = prioritizedGenerationTasks.filter(
    (task) => task.capability_id === activeCapability.id && !isLegacySolidWorksDirectAssemblyTask(task),
  )
  const primaryVisibleGenerationTasks = nonLegacyActiveTasks.filter((task) =>
    taskMatchesPrimaryParameters(task, activeCapability.id, normalizedParameterValues),
  )
  const visibleGenerationTaskPool = primaryVisibleGenerationTasks.length
    ? primaryVisibleGenerationTasks
    : activeCapability.id === 'locker_16029_regression'
      ? []
      : nonLegacyActiveTasks
  const visibleGenerationTasks = visibleGenerationTaskPool.slice(0, 6)
  const hiddenTaskCount = Math.max(
    0,
    generationTasks.filter(
      (task) =>
        task.capability_id !== activeCapability.id ||
        isLegacySolidWorksDirectAssemblyTask(task) ||
        !taskMatchesPrimaryParameters(task, activeCapability.id, normalizedParameterValues),
    ).length,
  )
  const activeTaskCount = nonLegacyActiveTasks.length
  const exactTaskCount = prioritizedGenerationTasks.filter(
    (task) =>
      task.capability_id === activeCapability.id &&
      taskMatchesPrimaryParameters(task, activeCapability.id, normalizedParameterValues) &&
      !isLegacySolidWorksDirectAssemblyTask(task),
  ).length
  const activeRuleBinding = useMemo(() => {
    if (activeCapability.id !== LOCKER_16038_RULE_BINDING_CAPABILITY_ID) return null
    return quantityFormulaCatalog?.items.find((item) => item.id === LOCKER_16038_RULE_BINDING_ID) ?? null
  }, [activeCapability.id, quantityFormulaCatalog])

  useEffect(() => {
    let active = true

    async function loadGenerationTasks() {
      setQueueState('checking')
      try {
        const response = await fetch(`${API_BASE_URL}/api/generation-tasks`)
        if (!response.ok) throw new Error(`HTTP ${response.status}`)
        const tasks = (await response.json()) as GenerationTask[]
        if (!active) return
        setGenerationTasks(tasks)
        setQueueState('online')
        setQueueMessage(`已连接本地 API: ${API_BASE_URL}`)
      } catch (error) {
        if (!active) return
        setQueueState('offline')
        setQueueMessage(`未连接本地 API: ${API_BASE_URL}`)
        console.warn('Generation queue API unavailable', error)
      }
    }

    loadGenerationTasks()
    return () => {
      active = false
    }
  }, [])

  useEffect(() => {
    let active = true

    async function loadQuantityFormulas() {
      try {
        const response = await fetch(`${API_BASE_URL}/api/rule-seed-quantity-formulas`)
        if (!response.ok) throw new Error(await response.text())
        const catalog = (await response.json()) as RuleSeedQuantityFormulaCatalog
        if (!active) return
        setQuantityFormulaCatalog(catalog)
        const has16038Binding = catalog.items.some((item) => item.id === LOCKER_16038_RULE_BINDING_ID)
        setQuantityFormulaMessage(
          has16038Binding
            ? '已绑定 16038 4/7/8 门 STEP 证据和 12/12 SolidWorks 模块证据。'
            : '尚未找到 16038 变体公式绑定，请先刷新规则种子公式。',
        )
      } catch (error) {
        if (!active) return
        setQuantityFormulaMessage(`规则绑定证据不可用: ${error instanceof Error ? error.message : 'unknown error'}`)
      }
    }

    loadQuantityFormulas()
    return () => {
      active = false
    }
  }, [])

  useEffect(() => {
    let active = true

    async function loadVariantQualityMatrix() {
      try {
        const response = await fetch(`${API_BASE_URL}/api/locker-16029-variant-quality-matrix`)
        if (!response.ok) throw new Error(await response.text())
        const matrix = (await response.json()) as Locker16029VariantQualityMatrix
        if (!active) return
        setVariantQualityMatrix(matrix)
        setVariantQualityMessage(
          `10/12/14 质量矩阵已读取：可复核 ${matrix.summary.pass_ready_count}，待完整性审计 ${matrix.summary.pass_rule_counts_needs_fcstd_audit_count}。`,
        )
      } catch (error) {
        if (!active) return
        setVariantQualityMatrix(null)
        setVariantQualityMessage(`16029 质量矩阵读取失败: ${error instanceof Error ? error.message : 'unknown error'}`)
        console.warn('16029 quality matrix unavailable', error)
      }
    }

    loadVariantQualityMatrix()
    return () => {
      active = false
    }
  }, [])

  useEffect(() => {
    let active = true

    async function loadVerifiedRulePacket() {
      try {
        const response = await fetch(`${API_BASE_URL}/api/locker-16029-verified-rule-packet`)
        if (!response.ok) throw new Error(await response.text())
        const packet = (await response.json()) as Locker16029VerifiedRulePacket
        if (!active) return
        setVerifiedRulePacket(packet)
        setVerifiedRulePacketMessage(
          packet.status === 'PASS'
            ? `已验证规则包 PASS：${packet.verified_door_counts.join('/')} 门可进入工程交接。`
            : `已验证规则包状态：${packet.status}。`,
        )
      } catch (error) {
        if (!active) return
        setVerifiedRulePacket(null)
        setVerifiedRulePacketMessage(`16029 已验证规则包读取失败: ${error instanceof Error ? error.message : 'unknown error'}`)
        console.warn('16029 verified rule packet unavailable', error)
      }
    }

    loadVerifiedRulePacket()
    return () => {
      active = false
    }
  }, [])

  useEffect(() => {
    let active = true

    async function loadEngineeringHandoffBundle() {
      try {
        const response = await fetch(`${API_BASE_URL}/api/locker-16029-engineering-handoff-bundle`)
        if (!response.ok) throw new Error(await response.text())
        const bundle = (await response.json()) as Locker16029EngineeringHandoffBundle
        if (!active) return
        setEngineeringHandoffBundle(bundle)
        setEngineeringHandoffMessage(
          bundle.variants.length
            ? `工程交接包已整理：${bundle.variants.map((variant) => `${variant.door_count}门`).join(' / ')}。`
            : '尚未生成 16029 工程交接包。',
        )
      } catch (error) {
        if (!active) return
        setEngineeringHandoffBundle(null)
        setEngineeringHandoffMessage(`16029 工程交接包读取失败: ${error instanceof Error ? error.message : 'unknown error'}`)
        console.warn('16029 engineering handoff bundle unavailable', error)
      }
    }

    loadEngineeringHandoffBundle()
    return () => {
      active = false
    }
  }, [])

  function parameterPayload() {
    return Object.fromEntries(
      activeCapability.parameters.map((parameter) => [
        parameter,
        lockedParameterValue(activeCapability.id, parameter) ??
        parameterValues[parameter] ?? sampleParameterValue(parameter, activeCapability.id),
      ]),
    ) as ParameterValues
  }

  function updateParameterValue(parameter: string, value: string) {
    if (lockedParameterValue(activeCapability.id, parameter)) return

    setParameterValuesByCapability((current) => ({
      ...current,
      [activeCapability.id]: {
        ...(current[activeCapability.id] ?? {}),
        [parameter]: value,
      },
    }))
  }

  function commandPreview(cadRunner: CadRunner) {
    const args = commandArgumentsFor(activeCapability.id, cadRunner, parameterPayload())
    if (cadRunner === 'solidworks') {
      return `cscript.exe //Nologo ${solidworksScriptFor(activeCapability.id)} ${args}`.trim()
    }
    return activeCapability.generator === 'not_enabled'
      ? 'not_enabled'
      : `"${FREECAD_CMD}" ${freecadScriptFor(activeCapability.id, activeCapability.generator)} ${args}`.trim()
  }

  async function createGenerationTask(cadRunner: CadRunner) {
    const runnerIssue = runnerIssueById[cadRunner]
    if (!canSubmitTask || runnerIssue) {
      if (runnerIssue) setQueueMessage(`${cadRunnerLabel(cadRunner)} 当前参数不可用: ${runnerIssue}`)
      return null
    }

    setQueueState('saving')
    try {
      const response = await fetch(`${API_BASE_URL}/api/generation-tasks`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          cad_runner: cadRunner,
          capability_id: activeCapability.id,
          capability_title: activeCapability.title,
          product_type: activeCapability.productType,
          module: activeCapability.module,
          maturity: activeCapability.maturity,
          output_level:
            activeCapability.status === 'reference_only'
              ? `engineering_reference_${cadRunner}_bbox_only`
              : `engineering_reference_${cadRunner}`,
          command: commandPreview(cadRunner),
          parameters: parameterPayload(),
          evidence: activeCapability.evidence,
          limitation: activeCapability.limitation,
          status: 'draft_pending_worker',
        }),
      })
      if (!response.ok) throw new Error(await response.text())
      const task = (await response.json()) as GenerationTask
      setGenerationTasks((current) => [task, ...current].slice(0, 20))
      setSelectedTaskId(task.id)
      setQueueState('online')
      setQueueMessage(`${cadRunnerLabel(cadRunner)} 任务已写入 SQLite: ${task.id}`)
      return task
    } catch (error) {
      setQueueState('offline')
      setQueueMessage(`任务写入失败: ${error instanceof Error ? error.message : 'unknown error'}`)
      console.error('Generation task create failed', error)
      return null
    }
  }

  async function dryRunTaskRequest(taskId: string) {
    setDryRunTaskId(taskId)
    setDrawerMessage('正在执行 dry-run 检查...')
    try {
      const response = await fetch(`${API_BASE_URL}/api/generation-tasks/${taskId}/dry-run`, { method: 'POST' })
      if (!response.ok) throw new Error(await response.text())
      const updatedTask = (await response.json()) as GenerationTask
      setGenerationTasks((current) => current.map((task) => (task.id === updatedTask.id ? updatedTask : task)))
      setSelectedTaskId(updatedTask.id)
      setDrawerMessage(
        updatedTask.status === 'ready_to_run'
          ? 'dry-run 通过，任务已进入 ready_to_run。'
          : 'dry-run 未通过，任务仍需补齐执行前条件。',
      )
      return updatedTask
    } catch (error) {
      setDrawerMessage(`dry-run 失败: ${error instanceof Error ? error.message : 'unknown error'}`)
      console.error('Generation task dry-run failed', error)
      return null
    } finally {
      setDryRunTaskId(null)
    }
  }

  async function runTaskDryRun(taskId: string) {
    await dryRunTaskRequest(taskId)
  }

  async function executeTask(taskId: string) {
    setExecuteTaskId(taskId)
    setDrawerMessage('正在执行受控 worker...')
    setGenerationFeedback({
      taskId,
      tone: 'warn',
      title: '正在生成',
      detail: 'worker 正在执行，完成后会在这里显示输出目录和推荐打开文件。',
    })
    try {
      const response = await fetch(`${API_BASE_URL}/api/generation-tasks/${taskId}/execute`, { method: 'POST' })
      if (!response.ok) throw new Error(await response.text())
      const updatedTask = (await response.json()) as GenerationTask
      setGenerationTasks((current) => current.map((task) => (task.id === updatedTask.id ? updatedTask : task)))
      setSelectedTaskId(updatedTask.id)
      const feedback = feedbackForExecution(updatedTask)
      setGenerationFeedback(feedback)
      setDrawerMessage(feedback.detail)
    } catch (error) {
      const detail = `worker 执行失败: ${error instanceof Error ? error.message : 'unknown error'}`
      setGenerationFeedback({
        taskId,
        tone: 'risk',
        title: '生成失败',
        detail,
      })
      setDrawerMessage(detail)
      console.error('Generation task execute failed', error)
    } finally {
      setExecuteTaskId(null)
    }
  }

  async function runSolidWorksPackageRequest(taskId: string) {
    setSolidWorksRunTaskId(taskId)
    setDrawerMessage('正在运行 SolidWorks 生成脚本，完成后会刷新原生装配和验证报告。')
    setGenerationFeedback({
      taskId,
      tone: 'warn',
      title: '正在运行 SolidWorks',
      detail: '正在通过本地 PowerShell 调用 SolidWorks 自动化脚本，请等待生成反馈刷新。',
      cadRunner: 'solidworks',
    })
    try {
      const response = await fetch(`${API_BASE_URL}/api/generation-tasks/${taskId}/run-solidworks-package`, { method: 'POST' })
      if (!response.ok) throw new Error(await response.text())
      const updatedTask = (await response.json()) as GenerationTask
      setGenerationTasks((current) => current.map((task) => (task.id === updatedTask.id ? updatedTask : task)))
      setSelectedTaskId(updatedTask.id)
      const feedback = feedbackForExecution(updatedTask)
      setGenerationFeedback(feedback)
      setDrawerMessage(feedback.detail)
      return updatedTask
    } catch (error) {
      const detail = `SolidWorks 运行失败: ${error instanceof Error ? error.message : 'unknown error'}`
      setGenerationFeedback({
        taskId,
        tone: 'risk',
        title: 'SolidWorks 运行失败',
        detail,
        cadRunner: 'solidworks',
      })
      setDrawerMessage(detail)
      console.error('SolidWorks package run failed', error)
      return null
    } finally {
      setSolidWorksRunTaskId(null)
    }
  }

  async function runSolidWorksPackage(taskId: string) {
    await runSolidWorksPackageRequest(taskId)
  }

  async function createAndRunSolidWorksTask() {
    if (solidWorksAutoRunBusy) return
    setSolidWorksAutoRunBusy(true)
    setGenerationFeedback({
      taskId: 'pending',
      tone: 'warn',
      title: '正在启动 SolidWorks 生成',
      detail: '正在创建任务并执行 dry-run；通过后会直接启动 SolidWorks 自动化脚本。',
      cadRunner: 'solidworks',
    })
    try {
      const createdTask = await createGenerationTask('solidworks')
      if (!createdTask) return
      const dryRunTask = await dryRunTaskRequest(createdTask.id)
      if (!dryRunTask) return
      if (dryRunTask.status !== 'ready_to_run') {
        setGenerationFeedback({
          taskId: dryRunTask.id,
          tone: 'risk',
          title: 'SolidWorks 生成未启动',
          detail: 'dry-run 未通过。请打开任务详情查看阻塞项，修复后再运行 SolidWorks。',
          cadRunner: 'solidworks',
        })
        return
      }
      await runSolidWorksPackageRequest(dryRunTask.id)
    } finally {
      setSolidWorksAutoRunBusy(false)
    }
  }

  async function openLocalPath(path: string, mode: LocalActionMode = 'open') {
    const busyKey = `${mode}:${path}`
    setLocalActionBusy(busyKey)
    try {
      const response = await fetch(`${API_BASE_URL}/api/local-actions/open-path`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ path, mode }),
      })
      if (!response.ok) throw new Error(await response.text())
      const result = (await response.json()) as LocalActionResult
      setDrawerMessage(result.message)
      setQueueMessage(result.message)
    } catch (error) {
      const detail = `本地打开失败: ${error instanceof Error ? error.message : 'unknown error'}`
      setDrawerMessage(detail)
      setQueueMessage(detail)
      console.error('Local open action failed', error)
    } finally {
      setLocalActionBusy(null)
    }
  }

  async function openTaskInFreeCad(taskId: string) {
    const busyKey = `freecad:${taskId}`
    setLocalActionBusy(busyKey)
    try {
      const response = await fetch(`${API_BASE_URL}/api/local-actions/open-freecad-model`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ task_id: taskId }),
      })
      if (!response.ok) throw new Error(await response.text())
      const result = (await response.json()) as LocalActionResult
      setDrawerMessage(result.message)
      setQueueMessage(result.message)
    } catch (error) {
      const detail = `FreeCAD 打开失败: ${error instanceof Error ? error.message : 'unknown error'}`
      setDrawerMessage(detail)
      setQueueMessage(detail)
      console.error('FreeCAD open action failed', error)
    } finally {
      setLocalActionBusy(null)
    }
  }

  return (
    <div className="page-grid">
      <section className="section-block">
        <div className="section-heading">
            <div>
              <h2>可生成模型与规则学习队列</h2>
              <p>先区分已验证工程参考模型和规则学习中的模板；SolidWorks 是当前工程主线，FreeCAD 是未来开源替代路线。</p>
            </div>
          <StatusPill tone="warn">production_candidate = 0</StatusPill>
        </div>
        <CurrentSolidWorksGenerationPanel
          activeCapabilityId={activeCapability.id}
          currentDoorCount={normalizedParameterValues.door_count ?? '-'}
          solidWorksIssue={runnerIssueById.solidworks}
          freeCadIssue={runnerIssueById.freecad}
        />
        <div className="capability-grid">
          {capabilities.map((capability) => (
            <button
              key={capability.id}
              type="button"
              data-capability-id={capability.id}
              className={`capability-card status-${capability.status} ${
                activeCapability.id === capability.id ? 'selected' : ''
              }`}
              onClick={() => setActiveCapabilityId(capability.id)}
            >
              <span>{capability.productType}</span>
              <strong>{capability.title}</strong>
              <small>{capability.variants}</small>
              <StatusPill tone={capability.status === 'blocked' ? 'risk' : capability.status === 'generatable' ? 'good' : 'warn'}>
                {capability.status}
              </StatusPill>
            </button>
          ))}
        </div>
      </section>

      <section className="split-grid model-detail-grid">
        <article className="section-block compact-block">
          <div className="section-heading">
            <div>
              <h2>生成能力详情</h2>
              <p>{activeCapability.module}</p>
            </div>
          </div>
          <div className="detail-list">
            <DetailLine label="成熟度" value={maturityLabels[activeCapability.maturity]} />
            <DetailLine label="SolidWorks 脚本" value={solidworksGeneratorFor(activeCapability)} mono />
            <DetailLine label="FreeCAD 迁移脚本" value={activeCapability.generator} mono />
            <DetailLine label="验证脚本/报告" value={activeCapability.validation} mono />
            <DetailLine label="限制" value={activeCapability.limitation} />
          </div>
          <div className={`generation-route-card route-${generationRouteKind(activeCapability.id)}`}>
            <div>
              <span>当前生成路线</span>
              <strong>{generationRouteTitle(activeCapability.id)}</strong>
            </div>
            <p>{generationRouteDetail(activeCapability.id)}</p>
          </div>
          <EvidenceRow evidence={activeCapability.evidence} />
        </article>

        <article className="section-block compact-block">
          <div className="section-heading">
            <div>
              <h2>参数 Schema</h2>
              <p>后续接入 FastAPI/SQLite 后由 worker 返回实际 schema。</p>
            </div>
            <Settings2 size={20} />
          </div>
          <div className="parameter-list">
            {activeCapability.parameters.map((parameter) => {
              const fixedValue = lockedParameterValue(activeCapability.id, parameter)
              return (
                <div className="parameter-field" key={parameter}>
                  <span>{parameter}</span>
                  {fixedValue ? (
                    <div className="locked-parameter-value">
                      <ShieldCheck size={14} />
                      <code>{fixedValue}</code>
                      <small>固定为门板类型，不是门数</small>
                    </div>
                  ) : parameter === 'door_count' ? (
                    <div className="door-count-control">
                      <div className="door-count-presets" role="group" aria-label="门数预设">
                        {activeDoorCountPresets.map((preset) => (
                          <button
                            key={preset}
                            type="button"
                            className={`door-count-option ${parameterValues[parameter] === preset ? 'active' : ''}`}
                            onClick={() => updateParameterValue(parameter, preset)}
                          >
                            {preset} 门
                          </button>
                        ))}
                      </div>
                      <input
                        type="number"
                        min={numericParameterMin(parameter)}
                        step={numericParameterStep(parameter)}
                        value={parameterValues[parameter] ?? ''}
                        aria-invalid={parameterIssue ? true : undefined}
                        onChange={(event) => updateParameterValue(parameter, event.target.value)}
                      />
                    </div>
                  ) : (
                    <input
                      type={parameterInputType(parameter)}
                      min={numericParameterMin(parameter)}
                      step={numericParameterStep(parameter)}
                      value={parameterValues[parameter] ?? ''}
                      onChange={(event) => updateParameterValue(parameter, event.target.value)}
                    />
                  )}
                </div>
              )
            })}
            <p className={`parameter-hint ${parameterIssue ? 'parameter-hint-risk' : ''}`}>
              {parameterIssue ?? parameterHintFor(activeCapability.id)}
            </p>
          </div>
          {activeCapability.id === 'locker_16029_regression' && (
            <>
              <Locker16029DoorLayoutPanel
                doorCount={parameterValues.door_count}
                verifiedRulePacket={verifiedRulePacket}
                message={verifiedRulePacketMessage}
              />
              <Locker16029QualityMatrixPanel
                matrix={variantQualityMatrix}
                message={variantQualityMessage}
                handoffBundle={engineeringHandoffBundle}
                handoffMessage={engineeringHandoffMessage}
                handoffBusy={Boolean(localActionBusy)}
                onOpenHandoff={(path, mode) => openLocalPath(path, mode)}
              />
            </>
          )}
        </article>
      </section>

      <section className="split-grid model-detail-grid">
        <article className="section-block compact-block">
          <div className="section-heading">
            <div>
              <h2>模型生成入口</h2>
              <p>只有 generatable/reference_only 才能写入队列；queued 模板需先完成规则抽取和验证。</p>
            </div>
            <Play size={20} />
          </div>
          <div className="generator-entry">
            <div className="generator-status">
              <StatusPill tone={canCreateTask ? (queueOnline ? 'good' : 'warn') : 'risk'}>
                {!canCreateTask
                  ? '证据未闭环，暂不可生成'
                  : parameterIssue
                    ? '参数需修正'
                    : firstRunnerIssue
                      ? '部分 CAD 入口需切换'
                    : queueOnline
                    ? '可写入本地生成队列'
                    : '生成队列未连接'}
              </StatusPill>
              <span>{parameterIssue ?? firstRunnerIssue ?? activeCapability.limitation}</span>
            </div>
            {activeCapability.id === LOCKER_16038_RULE_BINDING_CAPABILITY_ID && (
              <GeneratorRuleBindingPanel item={activeRuleBinding} message={quantityFormulaMessage} />
            )}
            <div className="queue-meta" data-queue-state={queueState}>
              <span>队列状态</span>
              <code>{queueMessage}</code>
            </div>
            {generationFeedback && (
              <div className={`generation-feedback feedback-${generationFeedback.tone}`} role="status" aria-live="polite">
                <div>
                  <strong>{generationFeedback.title}</strong>
                  <span>{generationFeedback.detail}</span>
                </div>
                {generationFeedback.outputDir && (
                  <div className="feedback-path">
                    <span>输出目录</span>
                    <code>{generationFeedback.outputDir}</code>
                  </div>
                )}
                {generationFeedback.recommendedFile && (
                  <div className="feedback-path">
                    <span>推荐打开</span>
                    <code>{generationFeedback.recommendedFile}</code>
                  </div>
                )}
                {(generationFeedback.validationReport || generationFeedback.validationData) && (
                  <div className="feedback-path">
                    <span>生成验证</span>
                    <code>
                      {generationFeedback.validationReport ? '验证报告已生成' : '验证报告待生成'}
                      {generationFeedback.validationData ? ' / 验证数据已生成' : ''}
                    </code>
                  </div>
                )}
                <div className="feedback-actions">
                  {generationFeedback.cadRunner === 'solidworks' &&
                    generationFeedback.recommendedFile &&
                    isPowerShellScript(generationFeedback.recommendedFile) && (
                      <button
                        className="secondary-action primary-open-action"
                        type="button"
                        disabled={solidWorksRunTaskId === generationFeedback.taskId}
                        onClick={() => runSolidWorksPackage(generationFeedback.taskId)}
                      >
                        <Play size={16} />
                        {solidWorksRunTaskId === generationFeedback.taskId ? '运行中' : '运行 SolidWorks 生成'}
                      </button>
                    )}
                  {generationFeedback.outputDir && (
                    <button
                      className="secondary-action"
                      type="button"
                      disabled={Boolean(localActionBusy)}
                      onClick={() => openLocalPath(generationFeedback.outputDir as string)}
                    >
                      <FolderOpen size={16} />
                      打开输出目录
                    </button>
                  )}
                  {generationFeedback.recommendedFile && (
                    <button
                      className={`secondary-action ${isSolidWorksNativeFile(generationFeedback.recommendedFile) ? 'primary-open-action' : ''}`}
                      type="button"
                      disabled={Boolean(localActionBusy)}
                      onClick={() => openLocalPath(generationFeedback.recommendedFile as string)}
                    >
                      <Play size={16} />
                      {openButtonLabelFor(generationFeedback.recommendedFile)}
                    </button>
                  )}
                  {generationFeedback.validationReport && (
                    <button
                      className="secondary-action"
                      type="button"
                      disabled={Boolean(localActionBusy)}
                      onClick={() => openLocalPath(generationFeedback.validationReport as string)}
                    >
                      <ClipboardList size={16} />
                      打开验证报告
                    </button>
                  )}
                  {generationFeedback.validationData && (
                    <button
                      className="secondary-action quiet-action"
                      type="button"
                      disabled={Boolean(localActionBusy)}
                      onClick={() => openLocalPath(generationFeedback.validationData as string)}
                    >
                      打开验证数据
                    </button>
                  )}
                  <button
                    className="secondary-action"
                    type="button"
                    onClick={() => {
                      setSelectedTaskId(generationFeedback.taskId)
                      setDrawerMessage(generationFeedback.detail)
                    }}
                  >
                    查看任务
                  </button>
                  <button className="secondary-action quiet-action" type="button" onClick={() => setGenerationFeedback(null)}>
                    关闭反馈
                  </button>
                </div>
              </div>
            )}
            <div className="command-preview">
              <span>任务命令预览（不是推荐交接入口）</span>
              {cadRunners.map((runner) => (
                <code key={runner.id}>{runnerCommandPreviewText(runner, activeCapability, runnerIssueById[runner.id], commandPreview)}</code>
              ))}
            </div>
            <div className="runner-grid" aria-label="CAD 生成入口">
              {cadRunners.map((runner) => {
                const runnerIssue = runnerIssueById[runner.id]
                const solidWorksBusy = runner.id === 'solidworks' && solidWorksAutoRunBusy
                return (
                  <button
                    key={runner.id}
                    className={`runner-action ${runnerIssue ? 'runner-action-blocked' : ''}`}
                    type="button"
                    data-generator-action={`create-${runner.id}`}
                    disabled={!canSubmitTask || Boolean(runnerIssue) || solidWorksBusy}
                    onClick={() =>
                      runner.id === 'solidworks' && activeCapability.id !== 'locker_16029_regression'
                        ? createAndRunSolidWorksTask()
                        : createGenerationTask(runner.id)
                    }
                  >
                    <Play size={17} />
                    <span>{solidWorksBusy ? '正在启动 SOLIDWORKS' : runnerButtonLabel(runner.id, activeCapability.id)}</span>
                    <small>{runnerIssue ?? runnerButtonDetail(runner.id, runner.detail, activeCapability.id)}</small>
                    <code>{runner.shortcut}</code>
                  </button>
                )
              })}
            </div>
          </div>
        </article>

        <article className="section-block compact-block">
          <div className="section-heading">
            <div>
              <h2>生成任务与运行记录</h2>
              <p>默认只显示当前模型/参数的非旧路线任务；旧 direct assembly 试验任务已从主视图隐藏。</p>
            </div>
            <ClipboardList size={20} />
          </div>
          <div className="task-focus-summary">
            <div>
              <span>当前模型任务</span>
              <strong>{activeTaskCount}</strong>
            </div>
            <div>
              <span>当前参数匹配</span>
              <strong>{exactTaskCount}</strong>
            </div>
            <div>
              <span>隐藏历史</span>
              <strong>{hiddenTaskCount}</strong>
            </div>
          </div>
          <div className="task-list">
            {generationTasks.length === 0 ? (
              <div className="empty-state">还没有生成任务。请选择可生成模型后创建任务草稿。</div>
            ) : visibleGenerationTasks.length === 0 ? (
              <div className="empty-state">当前模型/参数没有可显示任务；工程复核请优先使用上方 16029 质量门槛入口。</div>
            ) : (
              visibleGenerationTasks.map((task) => (
                <button
                  key={task.id}
                  type="button"
                  className={`task-card status-${task.status} ${
                    task.capability_id === activeCapability.id && taskMatchesActiveParameters(task, normalizedParameterValues)
                      ? 'current-task-match'
                      : ''
                  }`}
                  data-task-id={task.id}
                  onClick={() => {
                    setSelectedTaskId(task.id)
                    setDrawerMessage('')
                  }}
                >
                  <div>
                    <span>{task.id}</span>
                    <strong>{task.capability_title}</strong>
                    <small>{cadRunnerLabel(task.cad_runner)} / {formatTaskTime(task.created_at)} / {task.output_level}</small>
                  </div>
                  <div className="task-next-action">
                    <span>{taskRelevanceLabel(task, activeCapability.id, normalizedParameterValues)}</span>
                    <strong>{taskNextActionLabel(task)}</strong>
                  </div>
                  <TaskRunSnapshot task={task} />
                  <code>{task.command}</code>
                  <StatusPill tone={taskStatusTone(task.status)}>
                    {task.status}
                  </StatusPill>
                </button>
              ))
            )}
          </div>
        </article>
      </section>
      {selectedTask && (
        <TaskDrawer
          task={selectedTask}
          message={drawerMessage}
          dryRunBusy={dryRunTaskId === selectedTask.id}
          executeBusy={executeTaskId === selectedTask.id}
          solidWorksRunBusy={solidWorksRunTaskId === selectedTask.id}
          localActionBusy={localActionBusy}
          onClose={() => {
            setSelectedTaskId(null)
            setDrawerMessage('')
          }}
          onDryRun={() => runTaskDryRun(selectedTask.id)}
          onExecute={() => executeTask(selectedTask.id)}
          onRunSolidWorksPackage={() => runSolidWorksPackage(selectedTask.id)}
          onOpenPath={openLocalPath}
          onOpenTaskInFreeCad={openTaskInFreeCad}
        />
      )}
    </div>
  )
}

function CurrentSolidWorksGenerationPanel({
  activeCapabilityId,
  currentDoorCount,
  solidWorksIssue,
  freeCadIssue,
}: {
  activeCapabilityId: string
  currentDoorCount: string
  solidWorksIssue: string | null
  freeCadIssue: string | null
}) {
  const mainlineSelected = activeCapabilityId === 'locker_16029_regression'
  const activeSolidWorksIssue = mainlineSelected ? solidWorksIssue : null
  const activeFreeCadIssue = mainlineSelected ? freeCadIssue : null
  const layout = locker16029SolidWorksLayoutSummary(currentDoorCount)
  const supportedDoorCounts = LOCKER_16029_SUPPORTED_SOLIDWORKS_COUNTS.join(' / ')
  const freeCadDoorCounts = LOCKER_16029_SUPPORTED_FREECAD_COUNTS.join(' / ')
  const solidWorksAvailable = mainlineSelected && Boolean(layout) && !activeSolidWorksIssue
  const freeCadAvailable = mainlineSelected && Boolean(layout) && !activeFreeCadIssue
  const statusTone: StatusTone = !mainlineSelected
    ? 'idle'
    : solidWorksAvailable || freeCadAvailable
      ? 'good'
      : activeSolidWorksIssue
        ? 'warn'
        : 'risk'
  const statusText = !mainlineSelected
    ? '先选择 16029'
    : solidWorksAvailable
      ? 'SolidWorks 可启动'
      : freeCadAvailable
        ? 'FreeCAD 可验证'
        : activeSolidWorksIssue
          ? 'SolidWorks 阻止'
          : '门数未放开'

  return (
    <div className={`current-generation-panel current-generation-${statusTone}`}>
      <div className="current-generation-main">
        <div>
          <span>当前工程交接</span>
          <strong>16029 标准寄存柜整柜：{LOCKER_16029_OUTER_SIZE}</strong>
          <p>
            10/12/14 门优先使用原生 SolidWorks 增强矩阵样机 v2。下方 SolidWorks 大按钮开放{' '}
            {supportedDoorCounts} 门原生参考任务；FreeCAD 规则验证开放 {freeCadDoorCounts} 门。
          </p>
        </div>
        <StatusPill tone={statusTone}>{statusText}</StatusPill>
      </div>
      <div className="current-generation-facts">
        <div>
          <span>点击路径</span>
          <strong>可生成模型 → 16029 → SolidWorks 大按钮 → dry-run → 运行 SolidWorks</strong>
        </div>
        <div>
          <span>当前门数配方</span>
          <strong>
            {layout ? `${currentDoorCount} 门 / 每列 ${layout.rowsPerColumn} 排 / ${formatMm(layout.doorHeight)} mm` : '请选择 10 / 12 / 14 门规则样本'}
          </strong>
        </div>
        <div>
          <span>生成后反馈</span>
          <strong>任务卡会显示 SLDASM、STEP、验证报告或阻塞原因；仍按工程参考模型交接</strong>
        </div>
      </div>
      {activeSolidWorksIssue && <p className="current-generation-note">{activeSolidWorksIssue}</p>}
    </div>
  )
}

function TaskRunSnapshot({ task }: { task: GenerationTask }) {
  const summary = taskRunSnapshotFor(task)

  return (
    <div className={`task-run-snapshot snapshot-${summary.tone}`}>
      <div className="task-run-heading">
        <span>{summary.label}</span>
        <strong>{summary.title}</strong>
      </div>
      <div className="task-run-facts">
        {summary.facts.map((fact) => (
          <div key={fact.label}>
            <span>{fact.label}</span>
            <strong>{fact.value}</strong>
          </div>
        ))}
      </div>
      {summary.note && <p>{summary.note}</p>}
    </div>
  )
}

function TaskDrawer({
  task,
  message,
  dryRunBusy,
  executeBusy,
  solidWorksRunBusy,
  localActionBusy,
  onClose,
  onDryRun,
  onExecute,
  onRunSolidWorksPackage,
  onOpenPath,
  onOpenTaskInFreeCad,
}: {
  task: GenerationTask
  message: string
  dryRunBusy: boolean
  executeBusy: boolean
  solidWorksRunBusy: boolean
  localActionBusy: string | null
  onClose: () => void
  onDryRun: () => void
  onExecute: () => void
  onRunSolidWorksPackage: () => void
  onOpenPath: (path: string, mode?: LocalActionMode) => void
  onOpenTaskInFreeCad: (taskId: string) => void
}) {
  const dryRunChecks = task.dry_run_result?.checks ?? []
  const execution = task.execution_result
  const recommendedFile = recommendedOutputFile(task)
  const lowerOutput = (path: string) => path.toLowerCase()
  const freecadMacroFile = execution?.outputs.find((path) => lowerOutput(path).endsWith('show_all_objects_and_fit_view.fcmacro'))
  const solidworksAssemblyFile = execution?.outputs.find((path) => lowerOutput(path).endsWith('.sldasm'))
  const solidworksPartFile = execution?.outputs.find((path) => lowerOutput(path).endsWith('.sldprt'))
  const solidworksValidationReport = execution?.outputs.find((path) => lowerOutput(path).endsWith('_solidworks_validation_report.md'))
  const solidworksValidationCsv = execution?.outputs.find((path) => lowerOutput(path).endsWith('_solidworks_validation.csv'))
  const solidworksQualityReport = execution?.outputs.find((path) => lowerOutput(path).endsWith('_solidworks_quality_report.md'))
  const solidworksBuildReport = execution?.outputs.find((path) => lowerOutput(path).endsWith('_build_report.md'))
  const componentManifest = execution?.outputs.find((path) => lowerOutput(path).endsWith('_component_manifest.csv'))
  const solidworksRunScript = execution?.outputs.find((path) => lowerOutput(path).endsWith('run-solidworks-worker.ps1'))
  const freecadQualityStatus = execution?.freecad_quality_status ?? null
  const freecadQualitySummary = execution?.freecad_quality_summary ?? null
  const freecadQualityReport =
    execution?.freecad_quality_report ?? execution?.outputs.find((path) => lowerOutput(path).endsWith('locker_16029_freecad_postprocess.json'))
  const freecadStepGeometryReport = execution?.outputs.find((path) => lowerOutput(path).endsWith('freecad_geometry_check.json'))
  const freecadFcstdIntegrityReport = execution?.outputs.find((path) => lowerOutput(path).endsWith('_geometry_integrity.md'))
  const hasFreecadQualityPass = freecadQualityStatus === 'freecad_geometry_pass'
  const hasFcstdModel = Boolean(execution?.outputs.some((path) => lowerOutput(path).endsWith('.fcstd')))
  const hasNativeSolidWorksOutput = Boolean(solidworksAssemblyFile || solidworksPartFile)
  const solidworksRunSummary = execution?.solidworks_run_summary ?? null
  const solidworksQualityStatus = execution?.solidworks_quality_status ?? solidworksRunSummary?.quality_status ?? null
  const solidworksQualitySummary = execution?.solidworks_quality_summary ?? solidworksRunSummary?.quality_summary ?? null
  const hasReferenceOnlyQuality = solidworksQualityStatus === 'reference_feature_only'
  const hasValidatedComponentReferences =
    solidworksQualityStatus === 'component_reference_tree' || solidworksQualityStatus === 'component_tree'
  const legacySolidWorksTask = isLegacySolidWorksDirectAssemblyTask(task)

  return (
    <>
      <button className="drawer-scrim" type="button" aria-label="关闭任务详情" onClick={onClose} />
      <aside className="task-drawer" aria-label="生成任务详情">
        <div className="drawer-header">
          <div>
            <span>{task.id}</span>
            <h2>{task.capability_title}</h2>
            <p>{task.product_type} / {task.module}</p>
          </div>
          <button className="icon-button" type="button" aria-label="关闭任务详情" onClick={onClose}>
            <X size={18} />
          </button>
        </div>

        <div className="drawer-body">
          <div className="drawer-status-row">
            <StatusPill tone={taskStatusTone(task.status)}>{task.status}</StatusPill>
            <span>{formatTaskTime(task.updated_at)}</span>
          </div>
          {legacySolidWorksTask && (
            <div className="drawer-message">
              这是早期 direct assembly 试验任务，曾出现装配基准/transform 视觉错乱；当前已停用运行入口，请改用质量门槛里的
              10/12/14 门尝试打开 / 选中 STP。
            </div>
          )}

          <div className="drawer-section">
            <h3>任务边界</h3>
            <DetailLine label="CAD 入口" value={cadRunnerLabel(task.cad_runner)} />
            <DetailLine label="输出级别" value={task.output_level} />
            <DetailLine label="成熟度" value={task.maturity} />
            <DetailLine label="限制说明" value={task.limitation} />
          </div>

          <div className="drawer-section">
            <h3>参数</h3>
            <div className="drawer-param-grid">
              {Object.entries(task.parameters).map(([name, value]) => (
                <div key={name}>
                  <span>{name}</span>
                  <strong>{value}</strong>
                </div>
              ))}
            </div>
          </div>

          <div className="drawer-section">
            <h3>证据</h3>
            <EvidenceRow evidence={task.evidence} />
          </div>

          <div className="drawer-section">
            <h3>预期命令</h3>
            <code>{task.command}</code>
          </div>

          <div className="drawer-section">
            <div className="drawer-section-title">
              <h3>dry-run 检查</h3>
              <div className="drawer-action-row">
                <button
                  className="secondary-action"
                  type="button"
                  data-dry-run-action="run"
                  disabled={dryRunBusy || executeBusy || legacySolidWorksTask}
                  onClick={onDryRun}
                >
                  <Wrench size={16} />
                  {dryRunBusy ? '检查中' : '执行 dry-run'}
                </button>
                {task.cad_runner === 'solidworks' ? (
                  <button
                    className="secondary-action primary-open-action"
                    type="button"
                    data-execute-action="run-solidworks"
                    disabled={solidWorksRunBusy || dryRunBusy || !canRunSolidWorksPackageTask(task)}
                    onClick={onRunSolidWorksPackage}
                  >
                    <Play size={16} />
                    {solidWorksRunBusy ? '运行中' : '运行 SolidWorks 生成'}
                  </button>
                ) : (
                  <button
                    className="secondary-action"
                    type="button"
                    data-execute-action="run"
                    disabled={executeBusy || dryRunBusy || task.status !== 'ready_to_run'}
                    onClick={onExecute}
                  >
                    <Play size={16} />
                    {executeBusy ? '执行中' : '执行 worker'}
                  </button>
                )}
              </div>
            </div>
            {message && <div className="drawer-message">{message}</div>}
            {dryRunChecks.length === 0 ? (
              <div className="empty-state compact">尚未执行 dry-run。</div>
            ) : (
              <div className="dry-run-list">
                {dryRunChecks.map((check) => (
                  <div key={check.name} className={`dry-run-check ${check.ok ? 'pass' : 'fail'}`}>
                    <span>{check.ok ? 'PASS' : 'BLOCK'}</span>
                    <strong>{dryRunCheckLabel(check.name)}</strong>
                    <p>{check.detail}</p>
                  </div>
                ))}
              </div>
            )}
            {task.worker_log_path && (
              <div className="drawer-log-path">
                <span>worker 日志</span>
                <code>{task.worker_log_path}</code>
              </div>
            )}
            {execution && (
              <div className="execution-result">
                <div className="drawer-status-row">
                  <StatusPill tone={taskStatusTone(execution.status)}>{execution.status}</StatusPill>
                  <span>exit={execution.exit_code ?? 'n/a'}</span>
                </div>
                <p>{execution.message}</p>
                {task.cad_runner === 'freecad' && freecadQualityStatus && (
                  <div className={`freecad-run-summary ${hasFreecadQualityPass ? '' : 'quality-warning'}`}>
                    <div className="solidworks-run-heading">
                      <div>
                        <span>FreeCAD 自动复核</span>
                        <strong>{freecadQualityTitle(freecadQualityStatus, task.status)}</strong>
                      </div>
                      <StatusPill tone={freecadQualityTone(freecadQualityStatus, task.status)}>
                        {freecadQualityBadge(freecadQualityStatus)}
                      </StatusPill>
                    </div>
                    <div className="solidworks-run-explain">
                      <p>{freecadQualitySummary ?? execution.message}</p>
                      <div className="solidworks-check-grid freecad-check-grid">
                        <div data-state={freecadQualityCheckState(freecadQualityStatus, 'step')}>
                          <span>STEP 几何</span>
                          <strong>{freecadQualityStatus === 'freecad_run_lock_active' ? '未运行' : '已检查'}</strong>
                        </div>
                        <div data-state={freecadQualityCheckState(freecadQualityStatus, 'fcstd')}>
                          <span>FCStd 完整性</span>
                          <strong>{hasFreecadQualityPass ? 'PASS' : '待复核'}</strong>
                        </div>
                        <div data-state={freecadQualityCheckState(freecadQualityStatus, 'matrix')}>
                          <span>16029 质量矩阵</span>
                          <strong>{hasFreecadQualityPass ? '已刷新' : '待刷新'}</strong>
                        </div>
                      </div>
                      <div className="local-action-row">
                        {freecadQualityReport && (
                          <button
                            className="secondary-action"
                            type="button"
                            disabled={Boolean(localActionBusy)}
                            onClick={() => onOpenPath(freecadQualityReport)}
                          >
                            <ClipboardList size={16} />
                            打开自动复核报告
                          </button>
                        )}
                        {freecadStepGeometryReport && (
                          <button
                            className="secondary-action quiet-action"
                            type="button"
                            disabled={Boolean(localActionBusy)}
                            onClick={() => onOpenPath(freecadStepGeometryReport)}
                          >
                            打开 STEP 检查
                          </button>
                        )}
                        {freecadFcstdIntegrityReport && (
                          <button
                            className="secondary-action quiet-action"
                            type="button"
                            disabled={Boolean(localActionBusy)}
                            onClick={() => onOpenPath(freecadFcstdIntegrityReport)}
                          >
                            打开 FCStd 检查
                          </button>
                        )}
                      </div>
                    </div>
                  </div>
                )}
                {task.cad_runner === 'solidworks' && (
                  <div className={`solidworks-run-summary ${hasReferenceOnlyQuality ? 'quality-warning' : ''}`}>
                    <div className="solidworks-run-heading">
                      <div>
                        <span>SolidWorks 生成反馈</span>
                        <strong>
                          {hasReferenceOnlyQuality
                            ? '工程参考模型，组件树未达标'
                            : hasNativeSolidWorksOutput
                              ? '原生模型已生成'
                              : '等待手动脚本生成原生模型'}
                        </strong>
                      </div>
                      <StatusPill tone={hasReferenceOnlyQuality ? 'warn' : hasNativeSolidWorksOutput ? 'good' : 'warn'}>
                        {hasReferenceOnlyQuality ? 'reference only' : hasNativeSolidWorksOutput ? 'SLDASM ready' : 'manual run'}
                      </StatusPill>
                    </div>
                    {solidworksRunSummary && (
                      <div className="solidworks-run-explain">
                        <div className="solidworks-explain-grid">
                          <div>
                            <span>生成方式</span>
                            <strong>{solidworksGenerationModeLabel(solidworksRunSummary.generation_mode)}</strong>
                          </div>
                          <div>
                            <span>门数 / 模块</span>
                            <strong>{solidworksRunSummary.door_count ?? '-'}</strong>
                          </div>
                          <div>
                            <span>每列门高组合</span>
                            <strong>{solidworksRunSummary.layout_units ?? '-'}</strong>
                          </div>
                          <div>
                            <span>配方成熟度</span>
                            <strong>{solidworksLayoutStatusLabel(solidworksRunSummary.layout_rule_status)}</strong>
                          </div>
                          <div>
                            <span>组件清单</span>
                            <strong>{solidworksNullableCount(solidworksRunSummary.component_manifest_rows)}</strong>
                          </div>
                          <div>
                            <span>组件插入</span>
                            <strong>
                              {solidworksComponentInsertLabel(
                                solidworksRunSummary.added_component_count,
                                solidworksRunSummary.requested_component_count,
                              )}
                            </strong>
                          </div>
                          <div>
                            <span>验证统计</span>
                            <strong>{solidworksValidationCountLabel(solidworksRunSummary)}</strong>
                          </div>
                          <div>
                            <span>组件树</span>
                            <strong>{solidworksComponentTreeLabel(solidworksRunSummary)}</strong>
                          </div>
                        </div>
                        <p>{solidworksRunSummary.interpretation}</p>
                        {(solidworksRunSummary.source_assembly || solidworksRunSummary.source_folder) && (
                          <code>{solidworksRunSummary.source_assembly ?? solidworksRunSummary.source_folder}</code>
                        )}
                        {solidworksRunSummary.reference_sample.length > 0 && (
                          <div className="solidworks-reference-sample">
                            <span>SolidWorks 质量诊断样本</span>
                            <div>
                              {solidworksRunSummary.reference_sample.map((item) => (
                                <small key={item}>{item}</small>
                              ))}
                            </div>
                          </div>
                        )}
                        {solidworksRunSummary.next_action && (
                          <div className="solidworks-next-action">
                            <strong>下一步</strong>
                            <span>{solidworksRunSummary.next_action}</span>
                          </div>
                        )}
                      </div>
                    )}
                    {hasReferenceOnlyQuality && (
                      <div className="solidworks-quality-note">
                        <AlertTriangle size={16} />
                        <span>
                          {solidworksQualitySummary ??
                            '诊断显示该装配能打开查看，但 SolidWorks API 只能看到 Reference 特征，不能当作生产级可编辑组件树。'}
                        </span>
                      </div>
                    )}
                    <div className="solidworks-check-grid">
                      <div data-state={solidworksAssemblyFile ? 'ready' : 'waiting'}>
                        <span>原生装配</span>
                        <strong>{solidworksAssemblyFile ? '已生成' : '未生成'}</strong>
                      </div>
                      <div data-state={hasValidatedComponentReferences ? 'ready' : hasReferenceOnlyQuality ? 'warning' : 'waiting'}>
                        <span>组件树质量</span>
                        <strong>
                          {solidworksQualityStatus === 'component_tree'
                            ? '可遍历'
                            : solidworksQualityStatus === 'component_reference_tree'
                              ? '已验证引用'
                            : hasReferenceOnlyQuality
                              ? 'Reference'
                              : '待诊断'}
                        </strong>
                      </div>
                      <div data-state={solidworksValidationReport ? 'ready' : 'waiting'}>
                        <span>验证报告</span>
                        <strong>{solidworksValidationReport ? '可打开' : '待生成'}</strong>
                      </div>
                      <div data-state={solidworksValidationCsv ? 'ready' : 'waiting'}>
                        <span>验证数据</span>
                        <strong>{solidworksValidationCsv ? '可打开' : '待生成'}</strong>
                      </div>
                      <div data-state={componentManifest ? 'ready' : 'waiting'}>
                        <span>组件清单</span>
                        <strong>{componentManifest ? '可追溯' : '待生成'}</strong>
                      </div>
                    </div>
                    <div className="local-action-row">
                      {solidworksRunScript && (
                        <button
                          className="secondary-action primary-open-action"
                          type="button"
                          disabled={solidWorksRunBusy}
                          onClick={onRunSolidWorksPackage}
                        >
                          <Play size={16} />
                          {solidWorksRunBusy ? '运行中' : hasNativeSolidWorksOutput ? '重新运行 SolidWorks 生成' : '运行 SolidWorks 生成'}
                        </button>
                      )}
                      {solidworksAssemblyFile && (
                        <>
                          <button
                            className="secondary-action primary-open-action"
                            type="button"
                            disabled={Boolean(localActionBusy)}
                            onClick={() => onOpenPath(solidworksAssemblyFile)}
                          >
                            <Play size={16} />
                            打开 SolidWorks 原生装配
                          </button>
                          <button
                            className="secondary-action quiet-action"
                            type="button"
                            disabled={Boolean(localActionBusy)}
                            onClick={() => onOpenPath(solidworksAssemblyFile, 'reveal')}
                          >
                            定位 SLDASM
                          </button>
                        </>
                      )}
                      {solidworksBuildReport && (
                        <button
                          className="secondary-action quiet-action"
                          type="button"
                          disabled={Boolean(localActionBusy)}
                          onClick={() => onOpenPath(solidworksBuildReport)}
                        >
                          打开生成报告
                        </button>
                      )}
                      {solidworksQualityReport && (
                        <button
                          className="secondary-action quiet-action"
                          type="button"
                          disabled={Boolean(localActionBusy)}
                          onClick={() => onOpenPath(solidworksQualityReport)}
                        >
                          打开质量诊断
                        </button>
                      )}
                      {componentManifest && (
                        <button
                          className="secondary-action quiet-action"
                          type="button"
                          disabled={Boolean(localActionBusy)}
                          onClick={() => onOpenPath(componentManifest)}
                        >
                          打开组件清单
                        </button>
                      )}
                      {solidworksRunScript && (
                        <button
                          className="secondary-action quiet-action"
                          type="button"
                          disabled={Boolean(localActionBusy)}
                          onClick={() => onOpenPath(solidworksRunScript)}
                        >
                          查看手动脚本
                        </button>
                      )}
                    </div>
                  </div>
                )}
                <DetailLine label="输出目录" value={execution.output_dir} mono />
                <DetailLine label="执行日志" value={execution.log_path} mono />
                {execution.stdout_path && <DetailLine label="stdout" value={execution.stdout_path} mono />}
                {execution.stderr_path && <DetailLine label="stderr" value={execution.stderr_path} mono />}
                {recommendedFile && (
                  <div className="recommended-output">
                    <span>推荐打开</span>
                    <strong>{outputFileLabel(recommendedFile)}</strong>
                    <code>{recommendedFile}</code>
                  </div>
                )}
                <div className="local-action-row">
                  <button
                    className="secondary-action"
                    type="button"
                    disabled={Boolean(localActionBusy)}
                    onClick={() => onOpenPath(execution.output_dir)}
                  >
                    <FolderOpen size={16} />
                    打开输出目录
                  </button>
                  {recommendedFile && (
                    <>
                      <button
                        className="secondary-action"
                        type="button"
                        disabled={Boolean(localActionBusy)}
                        onClick={() => onOpenPath(recommendedFile)}
                      >
                        <Play size={16} />
                        {openButtonLabelFor(recommendedFile)}
                      </button>
                      <button
                        className="secondary-action quiet-action"
                        type="button"
                        disabled={Boolean(localActionBusy)}
                        onClick={() => onOpenPath(recommendedFile, 'reveal')}
                      >
                        定位文件
                      </button>
                    </>
                  )}
                  {task.cad_runner === 'freecad' && hasFcstdModel && (
                    <button
                      className="secondary-action"
                      type="button"
                      disabled={Boolean(localActionBusy)}
                      onClick={() => onOpenTaskInFreeCad(task.id)}
                    >
                      <Play size={16} />
                      用 FreeCAD 打开模型
                    </button>
                  )}
                  {solidworksValidationReport && (
                    <button
                      className="secondary-action"
                      type="button"
                      disabled={Boolean(localActionBusy)}
                      onClick={() => onOpenPath(solidworksValidationReport)}
                    >
                      <ClipboardList size={16} />
                      打开验证报告
                    </button>
                  )}
                  {solidworksValidationCsv && (
                    <button
                      className="secondary-action quiet-action"
                      type="button"
                      disabled={Boolean(localActionBusy)}
                      onClick={() => onOpenPath(solidworksValidationCsv)}
                    >
                      打开验证数据
                    </button>
                  )}
                  {freecadMacroFile && (
                    <button
                      className="secondary-action quiet-action"
                      type="button"
                      disabled={Boolean(localActionBusy)}
                      onClick={() => onOpenPath(freecadMacroFile)}
                    >
                      打开视图宏
                    </button>
                  )}
                </div>
                {execution.outputs.length > 0 && (
                  <div className="output-file-list">
                    <span>输出文件</span>
                    {execution.outputs.slice(0, 8).map((output) => (
                      <div className="output-file-row" key={output}>
                        <strong>{outputFileLabel(output)}</strong>
                        <code>{output}</code>
                      </div>
                    ))}
                    {execution.outputs.length > 8 && <small>还有 {execution.outputs.length - 8} 个文件未展开显示</small>}
                  </div>
                )}
              </div>
            )}
          </div>
        </div>
      </aside>
    </>
  )
}

function DrawingSheetMetalPage() {
  const readySourceCount = drawingSheetMetalSources.filter((source) => source.status === 'ready_for_intake').length
  const candidateSourceCount = drawingSheetMetalSources.filter((source) => source.status === 'candidate').length
  const activeRoadmapSteps = drawingSheetMetalRoadmap.filter((step) => step.statusTone !== 'idle').length
  const [selectedUploadFiles, setSelectedUploadFiles] = useState<File[]>([])
  const [uploadNotes, setUploadNotes] = useState('')
  const [intakeRecords, setIntakeRecords] = useState<DrawingSheetMetalIntakeRecord[]>([])
  const [uploadState, setUploadState] = useState<'checking' | 'online' | 'offline' | 'saving'>('checking')
  const [uploadMessage, setUploadMessage] = useState<DrawingUploadMessage | null>(null)
  const [runningExtractionId, setRunningExtractionId] = useState<string | null>(null)
  const [runningCadCheckId, setRunningCadCheckId] = useState<string | null>(null)
  const [ruleEvidence, setRuleEvidence] = useState<SheetMetalRuleEvidence16029 | null>(null)
  const [ruleEvidenceState, setRuleEvidenceState] = useState<'checking' | 'ready' | 'missing'>('checking')
  const selectedUploadSize = useMemo(
    () => selectedUploadFiles.reduce((total, file) => total + file.size, 0),
    [selectedUploadFiles],
  )

  const refreshDrawingIntake = useCallback(async () => {
    try {
      const response = await fetch(`${API_BASE_URL}/api/drawing-sheetmetal-intake?limit=8`)
      if (!response.ok) throw new Error(await response.text())
      const records = (await response.json()) as DrawingSheetMetalIntakeRecord[]
      setIntakeRecords(records)
      setUploadState((current) => (current === 'saving' ? current : 'online'))
    } catch (error) {
      setUploadState('offline')
      setUploadMessage({
        tone: 'warn',
        title: '上传 API 未连接',
        detail: `当前只能看到页面说明，不能保存上传文件：${error instanceof Error ? error.message : 'unknown error'}`,
      })
    }
  }, [])

  useEffect(() => {
    const timer = window.setTimeout(() => void refreshDrawingIntake(), 0)
    return () => window.clearTimeout(timer)
  }, [refreshDrawingIntake])

  const refreshRuleEvidence = useCallback(async () => {
    try {
      const response = await fetch(`${API_BASE_URL}/api/sheetmetal-rule-evidence-16029`)
      if (!response.ok) throw new Error(await response.text())
      const evidence = (await response.json()) as SheetMetalRuleEvidence16029
      setRuleEvidence(evidence)
      setRuleEvidenceState(evidence.summary.candidate_count || evidence.summary.formula_count ? 'ready' : 'missing')
    } catch {
      setRuleEvidenceState('missing')
    }
  }, [])

  useEffect(() => {
    const timer = window.setTimeout(() => void refreshRuleEvidence(), 0)
    return () => window.clearTimeout(timer)
  }, [refreshRuleEvidence])

  const submitDrawingUpload = useCallback(async () => {
    if (selectedUploadFiles.length === 0) {
      setUploadMessage({ tone: 'warn', title: '还没有选择文件', detail: '先选择 DXF、PDF、图片、STEP 或 SolidWorks 钣金模型。' })
      return
    }
    if (selectedUploadSize > DRAWING_UPLOAD_MAX_BYTES) {
      setUploadMessage({
        tone: 'risk',
        title: '文件批次太大',
        detail: `本入口限制单批 ${formatBytes(DRAWING_UPLOAD_MAX_BYTES)}，当前为 ${formatBytes(selectedUploadSize)}。请拆成多个批次上传。`,
      })
      return
    }

    setUploadState('saving')
    setUploadMessage({
      tone: 'warn',
      title: '正在写入 intake',
      detail: '正在把文件保存到本地 intake 目录；不会自动启动 SolidWorks 或 FreeCAD 重任务。',
    })

    try {
      const files = await Promise.all(
        selectedUploadFiles.map(async (file) => ({
          name: file.name,
          size_bytes: file.size,
          content_base64: await fileToBase64(file),
        })),
      )
      const response = await fetch(`${API_BASE_URL}/api/drawing-sheetmetal-intake`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          source_type: 'drawing_or_sheetmetal',
          notes: uploadNotes,
          files,
        }),
      })
      if (!response.ok) throw new Error(await response.text())
      const record = (await response.json()) as DrawingSheetMetalIntakeRecord
      setIntakeRecords((records) => [record, ...records.filter((item) => item.id !== record.id)].slice(0, 8))
      setSelectedUploadFiles([])
      setUploadNotes('')
      setUploadState('online')
      setUploadMessage({
        tone: 'good',
        title: '上传已进入 intake',
        detail: `${record.files.length} 个文件已保存。${record.next_action}`,
        outputDir: record.output_dir,
      })
    } catch (error) {
      setUploadState('offline')
      setUploadMessage({
        tone: 'risk',
        title: '上传失败',
        detail: error instanceof Error ? error.message : 'unknown error',
      })
    }
  }, [selectedUploadFiles, selectedUploadSize, uploadNotes])

  const openDrawingIntakeDirectory = useCallback(async (path: string) => {
    try {
      const response = await fetch(`${API_BASE_URL}/api/local-actions/open-path`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ path, mode: 'open' }),
      })
      if (!response.ok) throw new Error(await response.text())
      const result = (await response.json()) as LocalActionResult
      setUploadMessage({ tone: 'good', title: '已打开 intake 目录', detail: result.message, outputDir: path })
    } catch (error) {
      setUploadMessage({
        tone: 'risk',
        title: '打开目录失败',
        detail: error instanceof Error ? error.message : 'unknown error',
        outputDir: path,
      })
    }
  }, [])

  const runDxfExtraction = useCallback(async (record: DrawingSheetMetalIntakeRecord) => {
    if (!record.files.some((file) => file.suffix === '.dxf')) {
      setUploadMessage({
        tone: 'warn',
        title: '没有 DXF 可解析',
        detail: '这个 intake 不是 DXF 文件，后续需要走图片识别、STEP/FCStd 几何检查或 SolidWorks 工程图路线。',
        outputDir: record.output_dir,
      })
      return
    }

    setRunningExtractionId(record.id)
    setUploadMessage({
      tone: 'warn',
      title: '正在运行 DXF 轻量解析',
      detail: '只解析 DXF 文本几何，不启动 SolidWorks/FreeCAD。',
      outputDir: record.output_dir,
    })
    try {
      const response = await fetch(`${API_BASE_URL}/api/drawing-sheetmetal-intake/${record.id}/run-dxf-extraction`, { method: 'POST' })
      if (!response.ok) throw new Error(await response.text())
      const updatedRecord = (await response.json()) as DrawingSheetMetalIntakeRecord
      setIntakeRecords((records) => records.map((item) => (item.id === updatedRecord.id ? updatedRecord : item)))
      setUploadState('online')
      setUploadMessage({
        tone: updatedRecord.dxf_extraction.status === 'completed' ? 'good' : 'warn',
        title: drawingExtractionStatusLabel(updatedRecord.dxf_extraction.status),
        detail: updatedRecord.dxf_extraction.message,
        outputDir: updatedRecord.dxf_extraction.output_dir ?? updatedRecord.output_dir,
      })
    } catch (error) {
      setUploadMessage({
        tone: 'risk',
        title: 'DXF 解析失败',
        detail: error instanceof Error ? error.message : 'unknown error',
        outputDir: record.output_dir,
      })
    } finally {
      setRunningExtractionId(null)
    }
  }, [])

  const runFreeCadCheck = useCallback(async (record: DrawingSheetMetalIntakeRecord) => {
    if (!record.files.some((file) => ['.step', '.stp', '.fcstd'].includes(file.suffix))) {
      setUploadMessage({
        tone: 'warn',
        title: '没有 STEP/FCStd 可检查',
        detail: '这个 intake 不包含 FreeCAD 可直接轻量检查的 STEP/STP/FCStd 文件。',
        outputDir: record.output_dir,
      })
      return
    }

    setRunningCadCheckId(record.id)
    setUploadMessage({
      tone: 'warn',
      title: '正在运行 FreeCAD 几何检查',
      detail: '只启动 FreeCADCmd 读取 STEP/FCStd，统计 bbox、实体数和 invalid shape，不打开 SolidWorks。',
      outputDir: record.output_dir,
    })
    try {
      const response = await fetch(`${API_BASE_URL}/api/drawing-sheetmetal-intake/${record.id}/run-freecad-check`, { method: 'POST' })
      if (!response.ok) throw new Error(await response.text())
      const updatedRecord = (await response.json()) as DrawingSheetMetalIntakeRecord
      setIntakeRecords((records) => records.map((item) => (item.id === updatedRecord.id ? updatedRecord : item)))
      setUploadState('online')
      setUploadMessage({
        tone: updatedRecord.cad_check.status === 'completed' ? 'good' : 'warn',
        title: drawingCadCheckStatusLabel(updatedRecord.cad_check.status),
        detail: updatedRecord.cad_check.message,
        outputDir: updatedRecord.cad_check.output_dir ?? updatedRecord.output_dir,
      })
    } catch (error) {
      setUploadMessage({
        tone: 'risk',
        title: 'FreeCAD 检查失败',
        detail: error instanceof Error ? error.message : 'unknown error',
        outputDir: record.output_dir,
      })
    } finally {
      setRunningCadCheckId(null)
    }
  }, [])

  return (
    <div className="page-grid drawing-page">
      <section className="section-block drawing-hero-block">
        <div className="section-heading">
          <div>
            <h2>图纸生成与钣金出图支线</h2>
            <p>先从单件图纸和钣金展开规则入手，服务 16029 多门数规则，不抢占整柜生成主线资源。</p>
          </div>
          <StatusPill tone="warn">工程参考</StatusPill>
        </div>

        <div className="drawing-hero-grid">
          <article className="drawing-hero-card">
            <div className="drawing-icon">
              <Search size={20} />
            </div>
            <span>图纸/图片识别</span>
            <strong>先抽参数和缺项</strong>
            <p>DXF、PDF、图片先转成外形、孔位、折弯边、材料厚度线索和待确认项，不直接承诺精准建模。</p>
          </article>
          <article className="drawing-hero-card">
            <div className="drawing-icon">
              <Layers3 size={20} />
            </div>
            <span>钣金单件生成</span>
            <strong>先做门板/层板/横隔板</strong>
            <p>优先处理可复核的单件，而不是直接从图片生成整柜，减少错乱装配和电脑资源压力。</p>
          </article>
          <article className="drawing-hero-card">
            <div className="drawing-icon">
              <Wrench size={20} />
            </div>
            <span>展开与出图</span>
            <strong>输出参考 DXF / PDF</strong>
            <p>展开、尺寸标注和折弯/孔位校验先作为工程参考件，正式释放仍需要结构工程标准确认。</p>
          </article>
        </div>
      </section>

      <section className="section-block drawing-upload-block">
        <div className="section-heading">
          <div>
            <h2>上传图纸 / 钣金模型</h2>
            <p>这里才是文件入口：先保存到本地 intake，后续再做 DXF 解析、规则提取或低并发 CAD 检查。</p>
          </div>
          <StatusPill tone={uploadState === 'online' ? 'good' : uploadState === 'saving' ? 'warn' : 'warn'}>
            {uploadState === 'saving' ? 'saving' : uploadState === 'online' ? 'online' : 'offline'}
          </StatusPill>
        </div>

        <div className="drawing-upload-grid">
          <div className="drawing-upload-panel">
            <label className="drawing-upload-dropzone">
              <input
                type="file"
                multiple
                accept={DRAWING_UPLOAD_ACCEPT}
                onChange={(event) => setSelectedUploadFiles(Array.from(event.target.files ?? []))}
              />
              <span>
                <Upload size={20} />
                选择 DXF / PDF / 图片 / STEP / SolidWorks 文件
              </span>
              <small>支持 .dxf、.dwg、.pdf、.png、.jpg、.step、.stp、.sldprt、.sldasm、.slddrw、.fcstd。单批上限 {formatBytes(DRAWING_UPLOAD_MAX_BYTES)}。</small>
            </label>

            <div className="drawing-upload-meta">
              <strong>{selectedUploadFiles.length ? `${selectedUploadFiles.length} 个文件待上传` : '尚未选择文件'}</strong>
              <span>{selectedUploadFiles.length ? `合计 ${formatBytes(selectedUploadSize)}` : '选择后会先进入本地 intake，不会直接启动重 CAD 任务。'}</span>
            </div>

            {selectedUploadFiles.length > 0 && (
              <div className="drawing-upload-file-list">
                {selectedUploadFiles.map((file) => (
                  <div key={`${file.name}-${file.size}-${file.lastModified}`}>
                    <span>{file.name}</span>
                    <small>{formatBytes(file.size)}</small>
                  </div>
                ))}
              </div>
            )}

            <textarea
              className="drawing-upload-notes"
              value={uploadNotes}
              placeholder="可选：写清楚这批文件对应的柜型、门数、尺寸或用途"
              rows={3}
              onChange={(event) => setUploadNotes(event.target.value)}
            />

            <div className="drawing-upload-actions">
              <button className="primary-action" type="button" disabled={uploadState === 'saving'} onClick={submitDrawingUpload}>
                <Upload size={17} />
                上传到 intake
              </button>
              <button className="secondary-action" type="button" disabled={uploadState === 'saving'} onClick={() => void refreshDrawingIntake()}>
                <RefreshCw size={17} />
                刷新记录
              </button>
            </div>

            {uploadMessage && (
              <div className={`generation-feedback feedback-${uploadMessage.tone}`} role="status" aria-live="polite">
                <div>
                  <strong>{uploadMessage.title}</strong>
                  <span>{uploadMessage.detail}</span>
                </div>
                {uploadMessage.outputDir && (
                  <div className="feedback-path">
                    <span>intake 目录</span>
                    <code>{uploadMessage.outputDir}</code>
                  </div>
                )}
              </div>
            )}
          </div>

          <div className="drawing-intake-panel">
            <div className="drawing-intake-heading">
              <div>
                <span>最近 intake</span>
                <strong>{intakeRecords.length} 条记录</strong>
              </div>
              <Archive size={20} />
            </div>
            <div className="drawing-intake-list">
              {intakeRecords.length === 0 && <p>还没有上传记录。选择文件并上传后，这里会显示待解析批次和本地目录。</p>}
              {intakeRecords.map((record) => (
                <article key={record.id} className="drawing-intake-card">
                  <div className="drawing-card-top">
                    <div>
                      <span>{formatTaskTime(record.created_at)}</span>
                      <strong>{record.id}</strong>
                    </div>
                    <StatusPill tone="warn">pending</StatusPill>
                  </div>
                  <div className="drawing-intake-files">
                    {record.files.map((file) => (
                      <span key={file.saved_path}>
                        {drawingUploadCategoryLabel(file.source_category)} / {file.file_name} / {formatBytes(file.size_bytes)}
                      </span>
                    ))}
                  </div>
                  <p>{record.next_action}</p>
                  {record.dxf_extraction.status !== 'not_started' && (
                    <div className="drawing-extraction-summary">
                      <div>
                        <StatusPill tone={drawingExtractionTone(record.dxf_extraction.status)}>
                          {drawingExtractionStatusLabel(record.dxf_extraction.status)}
                        </StatusPill>
                        <strong>
                          {record.dxf_extraction.processed_files} files / {record.dxf_extraction.rule_seed_candidates} seeds
                        </strong>
                      </div>
                      <span>{record.dxf_extraction.message}</span>
                      {record.dxf_extraction.results.slice(0, 3).map((result) => (
                        <small key={`${record.id}-${result.file_name}`}>
                          {result.file_name}: {result.quality_status ?? result.status}
                          {result.manufacturing_bbox_mm.width && result.manufacturing_bbox_mm.height
                            ? ` / ${formatMm(result.manufacturing_bbox_mm.width)} x ${formatMm(result.manufacturing_bbox_mm.height)} mm`
                            : ''}
                        </small>
                      ))}
                    </div>
                  )}
                  {record.cad_check.status !== 'not_started' && (
                    <div className="drawing-extraction-summary">
                      <div>
                        <StatusPill tone={drawingCadCheckTone(record.cad_check.status)}>
                          {drawingCadCheckStatusLabel(record.cad_check.status)}
                        </StatusPill>
                        <strong>
                          {record.cad_check.processed_files} files / {record.cad_check.pass_files} pass
                        </strong>
                      </div>
                      <span>{record.cad_check.message}</span>
                      {record.cad_check.results.slice(0, 3).map((result) => (
                        <small key={`${record.id}-${result.file_name}`}>
                          {result.file_name}: {result.quality_status ?? result.status}
                          {result.assembly_bbox_mm.size_x && result.assembly_bbox_mm.size_y && result.assembly_bbox_mm.size_z
                            ? ` / ${formatMm(result.assembly_bbox_mm.size_x)} x ${formatMm(result.assembly_bbox_mm.size_y)} x ${formatMm(
                                result.assembly_bbox_mm.size_z,
                              )} mm`
                            : ''}
                        </small>
                      ))}
                    </div>
                  )}
                  <div className="drawing-intake-actions">
                    {record.files.some((file) => file.suffix === '.dxf') && (
                      <button
                        className="primary-action"
                        type="button"
                        disabled={runningExtractionId === record.id}
                        onClick={() => void runDxfExtraction(record)}
                      >
                        <Play size={16} />
                        {runningExtractionId === record.id ? '解析中' : '运行 DXF 解析'}
                      </button>
                    )}
                    {record.files.some((file) => ['.step', '.stp', '.fcstd'].includes(file.suffix)) && (
                      <button
                        className="primary-action"
                        type="button"
                        disabled={runningCadCheckId === record.id}
                        onClick={() => void runFreeCadCheck(record)}
                      >
                        <Wrench size={16} />
                        {runningCadCheckId === record.id ? '检查中' : '运行 FreeCAD 检查'}
                      </button>
                    )}
                    <button className="secondary-action quiet-action" type="button" onClick={() => void openDrawingIntakeDirectory(record.output_dir)}>
                      <FolderOpen size={16} />
                      打开 intake 目录
                    </button>
                  </div>
                </article>
              ))}
            </div>
          </div>
        </div>
      </section>

      <div className="split-grid drawing-summary-grid">
        <section className="section-block compact-block">
          <div className="section-heading">
            <div>
              <h2>当前定位</h2>
              <p>这不是新增一个页面给人看，而是给后续规则提取和单件出图留独立工作流。</p>
            </div>
            <Route size={20} />
          </div>
          <div className="detail-list">
            <DetailLine label="主线关系" value="继续推进 16029 同外形 10/12/14 门规则；图纸支线只补单件证据和校验表。" />
            <DetailLine label="首个样板" value="16029 门板，其次是层板、门框横隔板和简单隔板。" />
            <DetailLine label="首个输出" value={sourcePaths.drawingSheetMetalFirstRun} />
            <DetailLine label="暂不承诺" value="不把图片直接转成生产级整柜模型，不自动释放正式展开图和正式工程图。" />
            <DetailLine label="工作目录" value={sourcePaths.drawingSheetMetalWorkspace} />
          </div>
          <EvidenceRow evidence={['DXF', '工程图', 'SolidWorks', 'FreeCAD', '证据闭环记录']} />
        </section>

        <section className="section-block compact-block">
          <div className="section-heading">
            <div>
              <h2>支线状态</h2>
              <p>先做轻量能力和数据闭环，不在前端点击后启动重 CAD 批处理。</p>
            </div>
            <StatusPill tone={readySourceCount ? 'good' : 'warn'}>{readySourceCount} ready</StatusPill>
          </div>
          <div className="drawing-status-grid">
            <div>
              <span>可先接入</span>
              <strong>{readySourceCount}</strong>
              <small>DXF 优先，能给展开和孔位证据</small>
            </div>
            <div>
              <span>候选来源</span>
              <strong>{candidateSourceCount}</strong>
              <small>PDF / SolidWorks 工程图需逐项验证</small>
            </div>
            <div>
              <span>并行步骤</span>
              <strong>{activeRoadmapSteps}</strong>
              <small>不做全量变种库存</small>
            </div>
          </div>
          <div className="drawing-boundary-note">
            当前输出统一标记为工程参考模型或规则证据，正式图纸/BOM/展开尺寸需要工程师按公司规范复核。
          </div>
        </section>
      </div>

      <section className="section-block">
        <div className="section-heading">
          <div>
            <h2>输入来源与处理策略</h2>
            <p>不同来源的可信度不同，系统只把可验证信息写入规则库。</p>
          </div>
          <Database size={20} />
        </div>
        <div className="drawing-source-grid">
          {drawingSheetMetalSources.map((source) => (
            <article key={source.id} className={'drawing-source-card status-' + source.status}>
              <div className="drawing-card-top">
                <div>
                  <span>{source.sourceType}</span>
                  <strong>{source.title}</strong>
                </div>
                <StatusPill tone={drawingSourceTone(source.status)}>{drawingSourceLabel(source.status)}</StatusPill>
              </div>
              <p>{source.currentState}</p>
              <div className="detail-list mini-detail-list">
                <DetailLine label="首个目标" value={source.firstTarget} />
                <DetailLine label="下一步" value={source.nextAction} />
              </div>
              <EvidenceRow evidence={source.evidence} />
            </article>
          ))}
        </div>
      </section>

      <section className="section-block">
        <div className="section-heading">
          <div>
            <h2>16029 DXF 批量解析结果</h2>
            <p>门板、层板、横隔板和竖隔板已先做轻量筛选，用来决定下一步规则学习顺序。</p>
          </div>
          <StatusPill tone={drawingSheetMetalBatchRun.errorCount ? 'warn' : 'good'}>
            {drawingSheetMetalBatchRun.fileCount} files
          </StatusPill>
        </div>
        <div className="drawing-batch-grid">
          <article className="drawing-batch-card">
            <span>可做几何规则种子</span>
            <strong>{drawingSheetMetalBatchRun.ruleSeedCandidateCount}</strong>
            <p>能用于 bbox、孔径、阵列和门板/层板/隔板角色比对。</p>
          </article>
          <article className="drawing-batch-card">
            <span>解析异常</span>
            <strong>{drawingSheetMetalBatchRun.errorCount}</strong>
            <p>本轮没有文件级解析失败，问题集中在图纸空间和轮廓闭合质量。</p>
          </article>
          <article className="drawing-batch-card">
            <span>角色覆盖</span>
            <strong>{drawingSheetMetalBatchRun.roleCounts.map((item) => `${item.label}:${item.value}`).join(' / ')}</strong>
            <p>优先服务 16029 同外形门数变化规则，不扩散到全产品线。</p>
          </article>
        </div>
        <div className="drawing-quality-grid">
          {drawingSheetMetalBatchRun.qualityStatusCounts.map((item) => (
            <div key={item.label}>
              <StatusPill tone={item.tone}>{item.label}</StatusPill>
              <strong>{item.value}</strong>
            </div>
          ))}
        </div>
        <div className="drawing-findings">
          <div>
            <h3>关键发现</h3>
            {drawingSheetMetalBatchRun.keyFindings.map((finding) => (
              <p key={finding}>{finding}</p>
            ))}
          </div>
          <div>
            <h3>下一动作</h3>
            {drawingSheetMetalBatchRun.nextActions.map((action) => (
              <p key={action}>{action}</p>
            ))}
          </div>
        </div>
        <div className="detail-list">
          <DetailLine label="批量输出" value={drawingSheetMetalBatchRun.outputDir} />
          <DetailLine label="摘要文件" value={drawingSheetMetalBatchRun.summaryPath} />
        </div>
      </section>

      <section className="section-block drawing-rule-evidence-block">
        <div className="section-heading">
          <div>
            <h2>16029 规则证据候选</h2>
            <p>把可用 DXF 证据收敛成生成器能读的规则输入，避免继续凭感觉摆装配位置。</p>
          </div>
          <StatusPill tone={ruleEvidenceState === 'ready' ? 'good' : 'warn'}>
            {ruleEvidenceState === 'checking' ? 'checking' : ruleEvidenceState === 'ready' ? 'ready' : 'missing'}
          </StatusPill>
        </div>
        {ruleEvidence && (
          <>
            <div className="drawing-rule-metric-grid">
              <article>
                <span>公式</span>
                <strong>{ruleEvidence.summary.formula_count}</strong>
                <small>门板展开高度序列</small>
              </article>
              <article>
                <span>候选件</span>
                <strong>{ruleEvidence.summary.candidate_count}</strong>
                <small>门板 / 层板 / 隔板 / 加强筋</small>
              </article>
              <article>
                <span>可用证据</span>
                <strong>{ruleEvidence.summary.accepted_rule_seed_file_count}</strong>
                <small>可进入规则池的 DXF 样本</small>
              </article>
              <article>
                <span>排除证据</span>
                <strong>{ruleEvidence.blocked_evidence_summary.blocked_count}</strong>
                <small>图纸空间噪声或轮廓未闭合</small>
              </article>
            </div>

            <div className="drawing-rule-evidence-grid">
              {ruleEvidence.formulas.slice(0, 1).map((formula) => (
                <article key={formula.id} className="drawing-rule-card primary-rule-card">
                  <div className="drawing-card-top">
                    <div>
                      <span>{formula.role} / {formula.confidence}</span>
                      <strong>{formula.title}</strong>
                    </div>
                    <StatusPill tone={formula.confidence === 'high' ? 'good' : 'warn'}>{formula.sample_count} samples</StatusPill>
                  </div>
                  <code>{formula.formula_text}</code>
                  <p>{formula.usage_note}</p>
                  <div className="drawing-rule-samples">
                    {formula.samples.slice(0, 6).map((sample) => (
                      <span key={`${formula.id}-${sample.door_index}`}>
                        {sample.door_index}/12: {formatMm(sample.flat_width_mm)} x {formatMm(sample.flat_height_mm)} mm
                      </span>
                    ))}
                  </div>
                </article>
              ))}

              <article className="drawing-rule-card">
                <div className="drawing-card-top">
                  <div>
                    <span>输出文件</span>
                    <strong>生成器规则输入</strong>
                  </div>
                  <button className="secondary-action quiet-action" type="button" onClick={() => void refreshRuleEvidence()}>
                    <RefreshCw size={16} />
                    刷新
                  </button>
                </div>
                <div className="detail-list mini-detail-list">
                  <DetailLine label="JSON" value={ruleEvidence.output_paths.json ?? ''} />
                  <DetailLine label="Markdown" value={ruleEvidence.output_paths.markdown ?? ''} />
                  <DetailLine label="批次" value={ruleEvidence.source_batch.run_id ?? 'BATCH-16029-SHEETMETAL-20260519'} />
                </div>
              </article>
            </div>

            <div className="drawing-rule-candidate-list">
              {ruleEvidence.rule_candidates.slice(0, 7).map((candidate) => (
                <article key={candidate.id} className="drawing-rule-candidate">
                  <div>
                    <span>{candidate.role}</span>
                    <strong>{candidate.title}</strong>
                    <small>{candidate.rule_seed}</small>
                  </div>
                  <div>
                    <strong>
                      {formatMm(candidate.dimensions_mm.long)} x {formatMm(candidate.dimensions_mm.short)} mm
                    </strong>
                    <small>
                      {candidate.accepted_source_files_count}/{candidate.source_files_count} 可用 / {candidate.confidence}
                    </small>
                  </div>
                </article>
              ))}
            </div>
          </>
        )}
        {!ruleEvidence && (
          <div className="drawing-boundary-note">
            暂未读取到规则证据文件。先运行 workers/drawing_sheetmetal/build_16029_sheetmetal_rule_evidence.py 生成 JSON 后再刷新。
          </div>
        )}
      </section>

      <section className="section-block">
        <div className="section-heading">
          <div>
            <h2>可交付输出</h2>
            <p>先把工程师能复核、能节省时间的中间件做出来，再接 CAD 自动化。</p>
          </div>
          <ClipboardList size={20} />
        </div>
        <div className="drawing-output-grid">
          {drawingSheetMetalOutputs.map((output) => (
            <article key={output.id} className="drawing-output-card">
              <div className="drawing-card-top">
                <div>
                  <span>{output.level}</span>
                  <strong>{output.title}</strong>
                </div>
                <StatusPill tone={output.statusTone}>{output.statusTone === 'good' ? '优先' : '需复核'}</StatusPill>
              </div>
              <p>{output.description}</p>
              <small>{output.boundary}</small>
            </article>
          ))}
        </div>
      </section>

      <div className="split-grid drawing-detail-grid">
        <section className="section-block compact-block">
          <div className="section-heading">
            <div>
              <h2>生产级出图边界</h2>
              <p>这些条件没补齐前，系统不把展开或图纸标成正式释放。</p>
            </div>
            <FileWarning size={20} />
          </div>
          <div className="drawing-risk-list">
            {drawingSheetMetalRisks.map((risk) => (
              <article key={risk.item} className="drawing-risk-card">
                <strong>{risk.item}</strong>
                <p>{risk.reason}</p>
                <div className="detail-list mini-detail-list">
                  <DetailLine label="需要输入" value={risk.requiredInput} />
                  <DetailLine label="确认人" value={risk.owner} />
                </div>
              </article>
            ))}
          </div>
        </section>

        <section className="section-block compact-block">
          <div className="section-heading">
            <div>
              <h2>并行开发节奏</h2>
              <p>支线每一步都要服务模型质量，不做无休止页面展示。</p>
            </div>
            <ListChecks size={20} />
          </div>
          <div className="drawing-roadmap">
            {drawingSheetMetalRoadmap.map((step) => (
              <article key={step.step} className="drawing-roadmap-step">
                <div className="roadmap-index">{step.step}</div>
                <div>
                  <div className="drawing-card-top">
                    <strong>{step.focus}</strong>
                    <StatusPill tone={step.statusTone}>{step.statusTone === 'idle' ? '后续' : '推进'}</StatusPill>
                  </div>
                  <p>{step.deliverable}</p>
                  <small>{step.relationToMainline}</small>
                </div>
              </article>
            ))}
          </div>
        </section>
      </div>
    </div>
  )
}

function drawingSourceTone(status: (typeof drawingSheetMetalSources)[number]['status']): StatusTone {
  if (status === 'ready_for_intake') return 'good'
  if (status === 'candidate') return 'warn'
  if (status === 'blocked') return 'risk'
  return 'idle'
}

function drawingSourceLabel(status: (typeof drawingSheetMetalSources)[number]['status']) {
  if (status === 'ready_for_intake') return '可接入'
  if (status === 'candidate') return '候选'
  if (status === 'blocked') return '阻塞'
  return '规划'
}

function drawingUploadCategoryLabel(category: string) {
  if (category === '2d_drawing') return '二维图'
  if (category === 'drawing_image') return '图纸/图片'
  if (category === 'solidworks_drawing') return 'SolidWorks 工程图'
  if (category === 'cad_model') return 'CAD 模型'
  return category
}

function drawingExtractionTone(status: DrawingSheetMetalExtractionSummary['status']): StatusTone {
  if (status === 'completed') return 'good'
  if (status === 'partial_failed' || status === 'not_applicable') return 'warn'
  if (status === 'failed') return 'risk'
  return 'idle'
}

function drawingExtractionStatusLabel(status: DrawingSheetMetalExtractionSummary['status']) {
  if (status === 'completed') return 'DXF 解析完成'
  if (status === 'partial_failed') return 'DXF 部分完成'
  if (status === 'failed') return 'DXF 解析失败'
  if (status === 'not_applicable') return '无 DXF'
  return '未解析'
}

function drawingCadCheckTone(status: DrawingSheetMetalCadCheckSummary['status']): StatusTone {
  if (status === 'completed') return 'good'
  if (status === 'partial_failed' || status === 'not_applicable') return 'warn'
  if (status === 'failed') return 'risk'
  return 'idle'
}

function drawingCadCheckStatusLabel(status: DrawingSheetMetalCadCheckSummary['status']) {
  if (status === 'completed') return 'FreeCAD 检查完成'
  if (status === 'partial_failed') return 'FreeCAD 部分完成'
  if (status === 'failed') return 'FreeCAD 检查失败'
  if (status === 'not_applicable') return '无 CAD'
  return '未检查'
}

function fileToBase64(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader()
    reader.onload = () => {
      if (typeof reader.result !== 'string') {
        reject(new Error(`无法读取文件: ${file.name}`))
        return
      }
      resolve(reader.result.includes(',') ? reader.result.split(',', 2)[1] : reader.result)
    }
    reader.onerror = () => reject(reader.error ?? new Error(`无法读取文件: ${file.name}`))
    reader.readAsDataURL(file)
  })
}

function formatBytes(value: number) {
  if (value < 1024) return `${value} B`
  const kb = value / 1024
  if (kb < 1024) return `${kb.toFixed(kb >= 100 ? 0 : 1)} KB`
  const mb = kb / 1024
  if (mb < 1024) return `${mb.toFixed(mb >= 100 ? 0 : 1)} MB`
  const gb = mb / 1024
  return `${gb.toFixed(gb >= 100 ? 0 : 1)} GB`
}

function ReviewPage({
  query,
  setQuery,
  filteredReviewItems,
}: {
  query: string
  setQuery: (value: string) => void
  filteredReviewItems: ReviewItem[]
}) {
  const grouped = useMemo(
    () =>
      (['P0', 'P1', 'P2', 'P3'] as const).map((priority) => ({
        priority,
        items: filteredReviewItems.filter((item) => item.priority === priority),
      })),
    [filteredReviewItems],
  )

  return (
    <div className="page-grid">
      <section className="section-block">
        <div className="section-heading">
          <div>
            <h2>待确认项队列</h2>
            <p>P0/P1 影响规则升级；P2/P3 记录为工程参考限制或后续输入条件。</p>
          </div>
          <label className="search-box">
            <Search size={17} />
            <input value={query} onChange={(event) => setQuery(event.target.value)} placeholder="搜索项目、模块、问题" />
          </label>
        </div>
      </section>

      {grouped.map((group) => (
        <section key={group.priority} className="section-block">
          <div className="section-heading">
            <div>
              <h2>{group.priority} 队列</h2>
              <p>{group.items.length} 项</p>
            </div>
          </div>
          {group.items.length === 0 ? (
            <div className="empty-state">当前筛选条件下没有记录。</div>
          ) : (
            <div className="review-list">
              {group.items.map((item) => (
                <ReviewCard key={item.id} item={item} />
              ))}
            </div>
          )}
        </section>
      ))}
    </div>
  )
}

function AgentConsolePage({
  selectedProject,
  reviewCounts,
  onNavigate,
}: {
  selectedProject: Project
  reviewCounts: Record<string, number>
  onNavigate: (page: PageId) => void
}) {
  const generatableCapabilities = capabilities.filter((capability) => capability.status === 'generatable')
  const referenceCapabilities = capabilities.filter((capability) => capability.status === 'reference_only')
  const activePriorityItems = reviewItems.filter((item) => item.priority === 'P0' || item.priority === 'P1')

  return (
    <div className="page-grid console-page">
      <section className="section-block">
        <div className="section-heading">
          <div>
            <h2>结构 Agent 控制台</h2>
            <p>先把生成边界说清楚，再把规则学习和待确认项排队。</p>
          </div>
          <div className="console-actions">
            <button className="primary-action" type="button" onClick={() => onNavigate('models')}>
              <Play size={16} />
              去可生成模型
            </button>
            <button className="secondary-action" type="button" onClick={() => onNavigate('rules')}>
              <ListChecks size={16} />
              去规则库
            </button>
            <button className="secondary-action" type="button" onClick={() => onNavigate('drawings')}>
              <ClipboardList size={16} />
              去图纸出图
            </button>
            <button className="secondary-action" type="button" onClick={() => onNavigate('review')}>
              <ClipboardList size={16} />
              去待确认项
            </button>
          </div>
        </div>

        <div className="console-hero">
          <article className="console-hero-card">
            <div className="console-hero-heading">
              <MessageSquareMore size={18} />
              <div>
                <span>当前结论</span>
                <strong>1917×1000 样本已能学习同尺寸变体，但“门板尺寸自由变化”还没开放。</strong>
              </div>
            </div>
            <p>
              现在能走的是 16038 的 4/7/8/12 门同尺寸入口、16029 的整柜回归和门板单件入口；如果要做
              1000mm 宽、1917mm 高外型下不同柜门尺寸变化，必须先把门框分隔、门板宽高、锁位和铰链阵列的规则闭环。
            </p>
            <div className="console-status-row">
              <StatusPill tone="good">{generatableCapabilities.length} 可生成</StatusPill>
              <StatusPill tone="warn">{referenceCapabilities.length} 参考</StatusPill>
              <StatusPill tone={activePriorityItems.length ? 'risk' : 'good'}>{activePriorityItems.length} 待确认</StatusPill>
            </div>
          </article>

          <article className="console-project-card">
            <div className="console-project-top">
              <ProgressDial value={selectedProject.progress} />
              <div>
                <span>当前项目</span>
                <strong>{selectedProject.name}</strong>
                <p>{selectedProject.productType}</p>
              </div>
            </div>
            <div className="detail-list">
              <DetailLine label="更新时间" value={selectedProject.updatedAt} />
              <DetailLine label="当前能力" value={selectedProject.capability} />
              <DetailLine label="当前风险" value={selectedProject.risk} />
            </div>
          </article>
        </div>
      </section>

      <div className="split-grid console-summary-grid">
        <section className="section-block compact-block">
          <div className="section-heading">
            <div>
              <h2>生成边界</h2>
              <p>模型入口已经分开，参数化边界暂时只放到证据闭环后的项里。</p>
            </div>
            <Route size={20} />
          </div>
          <div className="detail-list">
            <DetailLine
              label="已验证范围"
              value="16038: 4/7/8/12 门；16029 SolidWorks: 10/12/14 门增强整柜样机；16029 单门板可做尺寸零件验证"
            />
            <DetailLine
              label="当前不开放"
              value="1000×1917 外型下任意柜门宽高变化，不进入直接生成器"
            />
            <DetailLine
              label="下一技术门槛"
              value="门框横隔、门板宽高、锁位、铰链阵列、BOM/DXF/STEP 角色证据闭环"
            />
          </div>
          <EvidenceRow evidence={['SolidWorks', 'DXF', 'BOM', 'STEP']} />
        </section>

        <section className="section-block compact-block">
          <div className="section-heading">
            <div>
              <h2>可生成入口</h2>
              <p>把“能生成”和“只能参考”分开，避免把工程参考模型误当成任意参数化生成。</p>
            </div>
            <Boxes size={20} />
          </div>
          <div className="console-capability-grid">
            {generatableCapabilities.concat(referenceCapabilities).map((capability) => (
              <article key={capability.id} className={'console-capability-card status-' + capability.status}>
                <div className="console-capability-top">
                  <div>
                    <span>{capability.productType}</span>
                    <strong>{capability.title}</strong>
                  </div>
                  <StatusPill tone={capability.status === 'generatable' ? 'good' : 'warn'}>{capability.status}</StatusPill>
                </div>
                <p>{capability.variants}</p>
                <small>{capability.limitation}</small>
              </article>
            ))}
          </div>
        </section>
      </div>

      <section className="section-block">
        <div className="section-heading">
          <div>
            <h2>规则学习轴</h2>
            <p>先补门数，再补门尺寸；门尺寸变化要等公式闭环，不靠肉眼猜。</p>
          </div>
          <StatusPill tone="warn">{reviewCounts.P0 ?? 0} P0 / {reviewCounts.P1 ?? 0} P1</StatusPill>
        </div>
        <div className="axis-grid">
          {ruleLearningAxes.map((axis) => (
            <RuleLearningAxisCard key={axis.axis} axis={axis} />
          ))}
        </div>
      </section>

      <section className="section-block">
        <div className="section-heading">
          <div>
            <h2>P0 / P1 待确认项</h2>
            <p>这些项决定不同柜门尺寸变化能不能进入生成器。</p>
          </div>
          <StatusPill tone="risk">{activePriorityItems.length} items</StatusPill>
        </div>
        <div className="review-list">
          {activePriorityItems.map((item) => (
            <ReviewCard key={item.id} item={item} />
          ))}
        </div>
      </section>
    </div>
  )
}

function ProjectCard({ project }: { project: Project }) {
  return (
    <article className={`project-card tone-${project.statusTone}`}>
      <div className="project-card-header">
        <div>
          <span>{project.productType}</span>
          <strong>{project.name}</strong>
        </div>
        <StatusPill tone={project.statusTone}>{project.statusTone}</StatusPill>
      </div>
      <div className="progress-line">
        <span style={{ width: `${project.progress}%` }} />
      </div>
      <p>{project.capability}</p>
      <small>{project.risk}</small>
    </article>
  )
}

function RuleCard({ rule }: { rule: RuleFamily }) {
  return (
    <article className={`rule-card maturity-${rule.maturity}`}>
      <div className="rule-card-header">
        <Layers3 size={18} />
        <div>
          <strong>{rule.family}</strong>
          <span>{rule.module}</span>
        </div>
      </div>
      <div className="rule-meta">
        <span>{rule.rows} rows</span>
        <span>{maturityLabels[rule.maturity]}</span>
      </div>
      <p>{rule.status}</p>
      <EvidenceRow evidence={rule.evidence} />
      <small>{rule.nextAction}</small>
    </article>
  )
}

function TemplateAssetCard({ asset }: { asset: TemplateAsset }) {
  return (
    <article className={`template-card status-${asset.status}`}>
      <div className="template-card-header">
        <Layers3 size={18} />
        <div>
          <span>{asset.id}</span>
          <strong>{asset.title}</strong>
          <small>{asset.productType} / {asset.size} / {asset.variants}</small>
        </div>
        <StatusPill tone={ruleLearningTone(asset.ruleLearningValue)}>{asset.ruleLearningValue}</StatusPill>
      </div>
      <div className="template-focus-list">
        {asset.ruleFocus.map((focus) => (
          <span key={focus}>{focus}</span>
        ))}
      </div>
      <div className="template-meta">
        <DetailLine label="总装配" value={asset.sourceAssemblyPath} mono />
        <DetailLine label="BOM" value={asset.bomPath} />
        <DetailLine label="DXF" value={asset.dxfPath} />
        <DetailLine label="STEP/PDF" value={asset.stepOrPdfPath} />
      </div>
      <EvidenceRow evidence={asset.evidence} />
    </article>
  )
}

function RuleExtractionCard({
  asset,
  latestRun,
  busyKey,
  onPrepare,
  onRun,
  onOpen,
}: {
  asset: TemplateAsset
  latestRun?: RuleExtractionResult
  busyKey: string | null
  onPrepare: (asset: TemplateAsset) => void
  onRun: (run: RuleExtractionResult) => void
  onOpen: (run: RuleExtractionResult) => void
}) {
  const preparing = busyKey === `prepare:${asset.id}`
  const running = latestRun ? busyKey === `run:${latestRun.id}` || latestRun.status === 'running' : false
  const opening = latestRun ? busyKey === `open:${latestRun.id}` : false
  const summary = latestRun?.learning_summary
  const counts = summary?.counts
  const qualityState = ruleQualityState(summary?.qualityGate)

  return (
    <article className={`extraction-card status-${latestRun?.status ?? 'none'}`}>
      <div className="extraction-card-header">
        <Search size={18} />
        <div>
          <span>{asset.id}</span>
          <strong>{asset.title}</strong>
          <small>{asset.size} / {asset.variants}</small>
        </div>
        <StatusPill tone={ruleExtractionTone(latestRun?.status)}>{latestRun?.status ?? '未提取'}</StatusPill>
      </div>
      <div className="extraction-detail">
        <DetailLine label="总装配" value={asset.sourceAssemblyPath} mono />
        <DetailLine label="最近任务" value={latestRun?.id ?? '尚未创建'} mono />
        <DetailLine label="输出目录" value={latestRun?.output_dir ?? '准备后生成'} mono />
        <DetailLine label="更新时间" value={latestRun ? formatTaskTime(latestRun.updated_at) : '尚未运行'} />
        <DetailLine label="退出码" value={latestRun?.exit_code === null || latestRun?.exit_code === undefined ? '运行中/待运行' : String(latestRun.exit_code)} />
      </div>
      {summary ? (
        <div className="extraction-quality" data-state={qualityState}>
          <div>
            <span>质量门</span>
            <strong>{ruleQualityLabel(summary.qualityGate)}</strong>
          </div>
          <div>
            <span>组件 / transform</span>
            <strong>{coverageLabel(counts?.componentsWithTransform, counts?.components)}</strong>
          </div>
          <div>
            <span>bbox / 配合</span>
            <strong>{coverageLabel(counts?.componentsWithBBox, counts?.components)} / {counts?.mateFeatureCount ?? 0}</strong>
          </div>
          <div>
            <span>STEP bbox</span>
            <strong>{counts?.stepObjectBBoxCount ?? 0}</strong>
          </div>
          <div>
            <span>角色绑定</span>
            <strong>{coverageLabel(counts?.stepRoleMatchedComponentCount, counts?.stepRoleBindingCount)}</strong>
          </div>
          <div>
            <span>阵列种子</span>
            <strong>{counts?.patternRuleCount ?? 0}</strong>
          </div>
        </div>
      ) : latestRun?.status === 'completed' ? (
        <small className="extraction-note warning">已完成基础提取，但还没有生成规则学习汇总；请刷新记录或重跑汇总脚本。</small>
      ) : null}
      {summary?.patternRules?.length ? (
        <div className="pattern-seed-list">
          {summary.patternRules.slice(0, 4).map((rule) => (
            <span key={`${rule.featureName}-${rule.instanceCount}-${rule.spacingMm}`}>
              {rule.featureName}: {rule.instanceCount ?? '?'} x {rule.spacingMm ?? '?'}mm
            </span>
          ))}
        </div>
      ) : null}
      {summary?.stepAssemblyBBoxEvidence?.assemblyBBoxMm ? (
        <div className="pattern-seed-list">
          <span>
            STEP 总包络: {bboxSizeLabel(summary.stepAssemblyBBoxEvidence.assemblyBBoxMm)}
          </span>
        </div>
      ) : null}
      <div className="extraction-actions">
        <button className="secondary-action" type="button" disabled={preparing || running} onClick={() => onPrepare(asset)}>
          <Archive size={16} />
          {preparing ? '准备中' : '准备提取包'}
        </button>
        <button
          className="primary-action"
          type="button"
          disabled={!latestRun || preparing || running}
          onClick={() => latestRun && onRun(latestRun)}
        >
          <Play size={16} />
          {running ? '提取中' : '运行 SolidWorks 提取'}
        </button>
        <button
          className="secondary-action"
          type="button"
          disabled={!latestRun || opening}
          onClick={() => latestRun && onOpen(latestRun)}
        >
          <FolderOpen size={16} />
          {opening ? '打开中' : '打开证据目录'}
        </button>
      </div>
      <small className={`extraction-note ${ruleExtractionNoteTone(latestRun?.status)}`} aria-live="polite">
        {latestRun
          ? ruleExtractionFeedbackMessage(latestRun)
          : '提取完成后会生成 components.csv、mates.csv、features.csv、JSON 和报告，用于学习门框分隔、层板节距、门板/锁具映射。'}
        {summary?.nextAction ? ` 下一步：${summary.nextAction}` : ''}
      </small>
    </article>
  )
}

function RuleSeedCard({ candidate }: { candidate: RuleSeedCandidate }) {
  return (
    <article className="rule-seed-card">
      <div className="rule-seed-heading">
        <Wrench size={17} />
        <div>
          <span>{candidate.source}</span>
          <strong>{candidate.label}</strong>
        </div>
        <StatusPill tone="warn">{candidate.confidence}</StatusPill>
      </div>
      <div className="rule-seed-value">
        <span>候选值</span>
        <strong>{candidate.value}</strong>
      </div>
      <small>{candidate.templateTitle}</small>
      {candidate.matchedTemplates?.length ? <small>{candidate.matchedTemplates.join(' / ')}</small> : null}
      <p>{candidate.blocker}</p>
    </article>
  )
}

function RuleSeedReviewCard({ item }: { item: RuleSeedReviewItem }) {
  return (
    <article className={`review-ledger-card priority-${item.priority.toLowerCase()}`}>
      <div className="review-ledger-heading">
        <ShieldCheck size={17} />
        <div>
          <span>{item.id}</span>
          <strong>{item.label}</strong>
        </div>
        <StatusPill tone={item.priority === 'P0' ? 'risk' : item.priority === 'P1' ? 'warn' : 'idle'}>
          {item.priority}
        </StatusPill>
      </div>
      <div className="rule-seed-value">
        <span>候选值</span>
        <strong>{item.value}</strong>
      </div>
      <div className="ledger-meta-grid">
        <div>
          <span>模板</span>
          <strong>{item.matchedTemplates.length}</strong>
        </div>
        <div>
          <span>来源</span>
          <strong>{item.sourceCount}</strong>
        </div>
        <div>
          <span>状态</span>
          <strong>{item.generationGate}</strong>
        </div>
      </div>
      <EvidenceRow evidence={item.requiredEvidence} />
      <p>{item.recommendedAction}</p>
    </article>
  )
}

function RuleSeedEvidenceCard({ item }: { item: RuleSeedEvidenceItem }) {
  const evidenceCounts = Object.entries(item.evidenceCounts)
  const bomLinks = (item.bomQuantityLinks ?? []).slice(0, 2)
  return (
    <article className={`evidence-closure-card state-${item.evidenceState}`}>
      <div className="review-ledger-heading">
        <CheckCircle2 size={17} />
        <div>
          <span>{item.id}</span>
          <strong>{item.label}</strong>
        </div>
        <StatusPill tone={evidenceStateTone(item.evidenceState)}>{evidenceStateLabel(item.evidenceState)}</StatusPill>
      </div>
      <div className="rule-seed-value">
        <span>定标值</span>
        <strong>{item.value}</strong>
      </div>
      <div className="evidence-count-grid">
        {evidenceCounts.length ? (
          evidenceCounts.map(([name, count]) => (
            <div key={name}>
              <span>{name}</span>
              <strong>{count}</strong>
            </div>
          ))
        ) : (
          <div>
            <span>证据文件</span>
            <strong>0</strong>
          </div>
        )}
      </div>
      <EvidenceRow evidence={item.requiredEvidence} />
      {bomLinks.length ? (
        <div className="bom-link-list">
          {bomLinks.map((link) => (
            <div key={`${link.path}-${link.runId ?? ''}`}>
              <strong>{link.name}</strong>
              <span>
                {link.matchedRows} 行 / 数量合计 {link.numericQuantitySum}
              </span>
              <small>
                {link.rows
                  .slice(0, 3)
                  .map((row) => `${row.name}=${row.quantity ?? 'n/a'}`)
                  .join('；')}
              </small>
            </div>
          ))}
        </div>
      ) : null}
      {item.matchedTemplates?.length ? <small>{item.matchedTemplates.join(' / ')}</small> : null}
      <p>{item.automationNextAction}</p>
    </article>
  )
}

function GeneratorRuleBindingPanel({
  item,
  message,
}: {
  item: RuleSeedQuantityFormulaItem | null
  message: string
}) {
  const primaryFormula = item?.templateFormulas[0] ?? null
  const derived = primaryFormula?.derived ?? {}
  const variantRows = primaryFormula?.variantRows ?? []
  const checks = primaryFormula?.checks ?? []
  const passCount = checks.filter((check) => check.ok).length

  if (!item || !primaryFormula) {
    return (
      <div className="generator-rule-binding binding-loading">
        <div className="rule-binding-heading">
          <Wrench size={17} />
          <div>
            <span>16038 规则绑定</span>
            <strong>正在等待公式证据</strong>
          </div>
          <StatusPill tone="warn">loading</StatusPill>
        </div>
        <p>{message}</p>
      </div>
    )
  }

  const derivedCards = [
    ['整柜证据', `${derived.closedFullAssemblyVariants ?? '4 / 7 / 8'} 门 STEP`],
    ['门数公式', derived.doorTotalFormula ? '小门装配 + 高门装配' : '待绑定'],
    ['锁钩公式', derived.lockHookFormula ? '锁钩数 = 门数' : '待绑定'],
    ['插销固定板', derived.latchPlateFormula ? '2 x 门数' : '待绑定'],
    ['12/12 模块', `装配 ${derived.module12DoorAssemblies ?? 1} / 门板 ${derived.module12DoorPanels ?? 1}`],
  ]

  return (
    <div className={`generator-rule-binding status-${item.formulaStatus}`}>
      <div className="rule-binding-heading">
        <Wrench size={17} />
        <div>
          <span>{item.id}</span>
          <strong>{item.label}</strong>
        </div>
        <StatusPill tone={formulaStatusTone(item.formulaStatus)}>{formulaStatusLabel(item.formulaStatus)}</StatusPill>
      </div>
      <div className="rule-binding-summary">
        {derivedCards.map(([label, value]) => (
          <div key={label}>
            <span>{label}</span>
            <strong>{value}</strong>
          </div>
        ))}
      </div>
      {variantRows.length ? (
        <div className="rule-binding-variants" aria-label="16038 已闭环门数">
          {variantRows.map((row) => (
            <div key={row.variant}>
              <span>{variantLabel(row.variant)}</span>
              <strong>{row.doorTotal} 门</strong>
              <small>
                小门 {row.smallDoorAssemblies} / 高门 {row.tallDoorAssemblies} / 锁钩 {row.lockHookCount} / 插销板 {row.latchPlateCount}
              </small>
            </div>
          ))}
        </div>
      ) : null}
      <div className="rule-binding-footer">
        <span>{passCount}/{checks.length} 证据检查 PASS</span>
        <small>{message}</small>
      </div>
      <p>{item.formulaSummary}</p>
    </div>
  )
}

function Locker16029DoorLayoutPanel({
  doorCount,
  verifiedRulePacket,
  message,
}: {
  doorCount?: string
  verifiedRulePacket: Locker16029VerifiedRulePacket | null
  message: string
}) {
  const numericDoorCount = Number(doorCount)
  const verifiedVariant =
    verifiedRulePacket?.status === 'PASS'
      ? verifiedRulePacket.verified_variants.find((variant) => variant.door_count === numericDoorCount)
      : null
  const fallbackLayout = locker16029SolidWorksLayoutSummary(doorCount)
  const layout = verifiedVariant
    ? {
        doorCount: verifiedVariant.door_count,
        rowsPerColumn: verifiedVariant.layout_rule.rows_per_column,
        doorHeight: verifiedVariant.layout_rule.door_height_mm,
        doorPitch: verifiedVariant.layout_rule.door_pitch_mm,
        doorWidth: verifiedVariant.layout_rule.door_width_mm,
        exactUnit: locker16029ExactUnitForHeight(verifiedVariant.layout_rule.door_height_mm),
        sourceMode: 'Verified rule packet / SolidWorks Pack-and-Go',
        rowLabels: Array.from(
          { length: verifiedVariant.layout_rule.rows_per_column },
          (_, index) => `${index + 1}: ${formatMm(verifiedVariant.layout_rule.door_height_mm)} mm`,
        ),
      }
    : fallbackLayout
  const supported = (verifiedRulePacket?.verified_door_counts.length
    ? verifiedRulePacket.verified_door_counts
    : LOCKER_16029_SUPPORTED_RULE_COUNTS
  ).join(' / ')
  const solidWorksSupported = LOCKER_16029_SUPPORTED_SOLIDWORKS_COUNTS.join(' / ')
  const freeCadSupported = LOCKER_16029_SUPPORTED_FREECAD_COUNTS.join(' / ')
  const lockHookCount = verifiedVariant?.component_count_rule.electric_lock_hooks ?? layout?.doorCount
  const shelfCount = verifiedVariant?.component_count_rule.shelves
  const crossbarCount = verifiedVariant?.component_count_rule.front_frame_crossbars

  return (
    <div className={`door-layout-panel ${layout ? '' : 'layout-warning'}`}>
      <div className="door-layout-heading">
        <div>
          <span>16029 门数/尺寸规则</span>
          <strong>{layout ? `${layout.doorCount} 门 / ${layout.rowsPerColumn} 排每列` : '当前门数未开放'}</strong>
        </div>
        <StatusPill tone={layout ? 'good' : 'risk'}>{layout ? '规则可用' : 'blocked'}</StatusPill>
      </div>
      {layout ? (
        <>
          <div className="door-layout-stats">
            <div>
              <span>外型尺寸</span>
              <strong>{LOCKER_16029_OUTER_SIZE}</strong>
            </div>
            <div>
              <span>单门高度</span>
              <strong>{formatMm(layout.doorHeight)} mm</strong>
            </div>
            <div>
              <span>门板来源</span>
              <strong>{layout.sourceMode}</strong>
            </div>
            {verifiedVariant && (
              <div>
                <span>层板 / 横隔板</span>
                <strong>{shelfCount} / {crossbarCount}</strong>
              </div>
            )}
            {verifiedVariant && (
              <div>
                <span>锁钩 / 右门镜像</span>
                <strong>
                  {lockHookCount} / {verifiedVariant.measured_gate.right_column_rotation_ok}
                </strong>
              </div>
            )}
          </div>
          <div className="door-layout-rows" aria-label="每列门尺寸序列">
            {layout.rowLabels.map((label) => (
              <span key={label}>
                {label}
                {layout.exactUnit ? ` · ${layout.exactUnit}/12` : ''}
              </span>
            ))}
          </div>
          <p>
            {message} FreeCAD 规则验证支持 {freeCadSupported} 门；SolidWorks 原生整柜骨架当前支持 {solidWorksSupported} 门。其它门数先进入规则学习队列。
          </p>
          {verifiedVariant && (
            <p>
              Pack-and-Go: {verifiedVariant.pack_and_go_handoff.ok}，文件 {verifiedVariant.pack_and_go_handoff.file_count} 个，外部引用 {verifiedVariant.pack_and_go_handoff.external_top_reference_count}。
            </p>
          )}
        </>
      ) : (
        <p>{message} 当前 16029 规则收敛样本只放开 {supported} 门。其它门数先进入规则学习队列，暂不直接生成。</p>
      )}
    </div>
  )
}

function Locker16029QualityMatrixPanel({
  matrix,
  message,
  handoffBundle,
  handoffMessage,
  handoffBusy,
  onOpenHandoff,
}: {
  matrix: Locker16029VariantQualityMatrix | null
  message: string
  handoffBundle: Locker16029EngineeringHandoffBundle | null
  handoffMessage: string
  handoffBusy: boolean
  onOpenHandoff: (path: string, mode?: LocalActionMode) => void
}) {
  const summary = matrix?.summary
  const readinessSummary = handoffBundle?.readiness_summary
  const solidWorksOpenVerified = readinessSummary?.solidworks_open_verified ?? false
  const solidWorksRepairPromptRequired = readinessSummary?.solidworks_open_status === 'manual_repair_prompt_required'
  const solidWorksStepApiBlocked =
    readinessSummary?.solidworks_open_status === 'solidworks_step_api_blocked_manual_open_required'
  const handoffVariants = [...(handoffBundle?.variants ?? [])].sort((left, right) => left.door_count - right.door_count)
  const handoffByDoorCount = new Map((handoffBundle?.variants ?? []).map((variant) => [variant.door_count, variant]))
  const rootLauncherByDoorCount = new Map(
    (handoffBundle?.root_launchers ?? []).map((launcher) => [launcher.door_count, launcher.solidworks_launcher]),
  )
  const enrichedReferences = LOCKER_16029_ENRICHED_REFERENCES
  const tone: StatusTone = !summary
    ? 'idle'
    : summary.fail_count || summary.missing_output_count
      ? 'risk'
      : summary.pass_rule_counts_needs_fcstd_audit_count
        ? 'warn'
        : 'good'

  return (
    <div className={`variant-quality-panel variant-quality-${tone}`}>
      <div className="variant-quality-heading">
        <div>
          <span>16029 生成质量门槛</span>
          <strong>10/12/14 门规则结果</strong>
        </div>
        <StatusPill tone={tone}>{summary ? '已读取' : 'loading'}</StatusPill>
      </div>
      <div className="variant-quality-summary">
        <div>
          <span>工程复核就绪</span>
          <strong>{summary?.pass_ready_count ?? '-'}</strong>
        </div>
        <div>
          <span>待完整性审计</span>
          <strong>{summary?.pass_rule_counts_needs_fcstd_audit_count ?? '-'}</strong>
        </div>
        <div>
          <span>失败/缺失</span>
          <strong>{summary ? summary.fail_count + summary.missing_output_count : '-'}</strong>
        </div>
      </div>
      <div className="engineer-handoff-direct enriched-reference-direct" aria-label="16029 当前推荐工程样机">
        {enrichedReferences.map((enrichedReference) => (
          <article key={enrichedReference.doorCount} className="engineer-handoff-card handoff-ready enriched-reference-card">
            <div className="engineer-handoff-top">
              <div>
                <span>当前推荐样机</span>
                <strong>{enrichedReference.title}</strong>
              </div>
              <StatusPill tone="good">PASS</StatusPill>
            </div>
            <div className="engineer-handoff-metrics">
              <div>
                <span>门数</span>
                <strong>{enrichedReference.doorCount} 门</strong>
              </div>
              <div>
                <span>固定模块</span>
                <strong>9/9</strong>
              </div>
              <div>
                <span>外型</span>
                <strong>1000×1917×550</strong>
              </div>
            </div>
            <small>包含维护门、插销、锁控板、M9 板、电源、WIFI 串口服务器等 transform-backed 固定模块；仍是工程参考模型。</small>
            <p className="engineer-handoff-gate-note">
              这是比骨架更完整的 SolidWorks 样机。先让结构工程师复核固定模块位置，再补后侧、电气和交接封包。
            </p>
            <div className="variant-quality-row-actions">
              <button
                type="button"
                className="mini-action primary-mini-action"
                disabled={handoffBusy}
                onClick={() => onOpenHandoff(enrichedReference.assembly)}
              >
                <Play size={14} />
                打开 SolidWorks 样机
              </button>
              <button
                type="button"
                className="mini-action quiet-mini-action"
                disabled={handoffBusy}
                onClick={() => onOpenHandoff(enrichedReference.step, 'reveal')}
              >
                定位 STEP
              </button>
              <button
                type="button"
                className="mini-action quiet-mini-action"
                disabled={handoffBusy}
                onClick={() => onOpenHandoff(enrichedReference.validationReport)}
              >
                验证报告
              </button>
              <button
                type="button"
                className="mini-action quiet-mini-action"
                disabled={handoffBusy}
                onClick={() => onOpenHandoff(enrichedReference.candidateMap)}
              >
                模块证据
              </button>
              <button
                type="button"
                className="mini-action quiet-mini-action"
                disabled={handoffBusy}
                onClick={() => onOpenHandoff(enrichedReference.outputDir)}
              >
                <FolderOpen size={14} />
                目录
              </button>
            </div>
          </article>
        ))}
      </div>
      {readinessSummary ? (
        <div className={`handoff-readiness-summary ${readinessSummary.all_ready ? 'handoff-all-ready' : 'handoff-has-blockers'}`}>
          <div className="handoff-readiness-title">
            <span>工程交接总览</span>
            <strong>
              {readinessSummary.model_file_ready_count ?? readinessSummary.ready_count}/
              {readinessSummary.variant_count || readinessSummary.target_door_counts.length} 个模型文件就绪
            </strong>
          </div>
          <div className="handoff-readiness-stats">
            <div>
              <span>模型文件</span>
              <strong>{readinessSummary.ready_door_counts.length ? `${readinessSummary.ready_door_counts.join('/')} 门` : '-'}</strong>
            </div>
            <div>
              <span>SolidWorks 交接</span>
              <strong>
                {solidWorksOpenVerified
                  ? '已确认'
                  : solidWorksStepApiBlocked
                    ? '需手动打开'
                    : solidWorksRepairPromptRequired
                      ? '需点修复'
                      : '待修复'}
              </strong>
            </div>
            <div>
              <span>缺文件</span>
              <strong>{readinessSummary.missing_file_count}</strong>
            </div>
          </div>
          <StatusPill tone={readinessSummary.all_ready ? 'good' : solidWorksOpenVerified ? 'warn' : 'risk'}>
            {readinessSummary.all_ready
              ? '交接就绪'
              : solidWorksOpenVerified
                ? '需查看'
                : solidWorksStepApiBlocked
                  ? 'API 导入阻塞'
                  : solidWorksRepairPromptRequired
                    ? '需人工修复确认'
                    : '自动打开待修复'}
          </StatusPill>
          {readinessSummary.missing_file_count ? (
            <small>
              首项缺失：{readinessSummary.missing_files[0]?.door_count ?? '-'} 门 / {readinessSummary.missing_files[0]?.key ?? '-'}
            </small>
          ) : (
            <small>{readinessSummary.solidworks_open_message ?? '工程师优先打开 SolidWorks STP 入口；当前仍按工程参考模型复核。'}</small>
          )}
        </div>
      ) : null}
      {handoffVariants.length ? (
        <div className="engineer-handoff-direct" aria-label="16029 工程交接直达入口">
          {handoffVariants.map((handoff) => {
            const solidworksLauncher = rootLauncherByDoorCount.get(handoff.door_count) ?? handoff.solidworks_launcher
            const metrics = handoff.metrics
            const handoffStep = handoff.stp ?? handoff.step
            const files = handoff.file_status
            const gate = handoffGateState(handoff)
            const cardReady = gate.ready && solidWorksOpenVerified
            return (
              <article
                key={handoff.door_count}
                className={`engineer-handoff-card ${cardReady ? 'handoff-ready' : 'handoff-warning'}`}
              >
                <div className="engineer-handoff-top">
                  <div>
                    <span>工程交接</span>
                    <strong>{handoff.door_count} 门 / 1000W</strong>
                  </div>
                  <StatusPill tone={cardReady ? gate.tone : 'warn'}>{cardReady ? gate.badge : gate.ready ? '文件就绪' : gate.badge}</StatusPill>
                </div>
                <div className="engineer-handoff-metrics">
                  <div>
                    <span>门高</span>
                    <strong>{formatMm(metrics?.door_height_mm)} mm</strong>
                  </div>
                  <div>
                    <span>STEP</span>
                    <strong>{metrics?.step_mb ? `${formatMm(metrics.step_mb)} MB` : 'ready'}</strong>
                  </div>
                  <div>
                    <span>invalid</span>
                    <strong>{metrics?.step_invalid_shape_count ?? 0}</strong>
                  </div>
                </div>
                <small>
                  门宽 {formatMm(metrics?.door_width_mm)} mm / 锁孔X {formatAbsMetric(metrics?.lock_center_x_abs_mm)} / 铰链X{' '}
                  {formatAbsMetric(metrics?.hinge_axis_x_abs_mm)}
                </small>
                <p className="engineer-handoff-gate-note">
                  {gate.ready && !solidWorksOpenVerified
                    ? solidWorksStepApiBlocked
                      ? '模型文件和校验已就绪；SolidWorks API 自动导入 STEP 阻塞，请用 SolidWorks 手动打开/修复。'
                      : solidWorksRepairPromptRequired
                        ? '模型文件和校验已就绪；SolidWorks 导入 STEP 会弹修复确认，需点击“是”后再复核。'
                        : '模型文件和校验已就绪；SolidWorks 自动打开 STP 未确认，按钮会尝试打开，失败时选中文件供手动打开。'
                    : gate.detail}
                </p>
                <div className="variant-quality-row-actions">
                  <button
                    type="button"
                    className="mini-action primary-mini-action"
                    disabled={!gate.ready || !solidworksLauncher || handoffBusy}
                    onClick={() => gate.ready && solidworksLauncher && onOpenHandoff(solidworksLauncher)}
                  >
                    <Play size={14} />
                    {gate.ready ? (solidWorksOpenVerified ? 'SolidWorks 打开 STP' : '尝试打开 / 选中 STP') : '暂不交接'}
                  </button>
                  <button
                    type="button"
                    className="mini-action quiet-mini-action"
                    disabled={!handoffStep || files?.stp === false || handoffBusy}
                    onClick={() => handoffStep && onOpenHandoff(handoffStep, 'reveal')}
                  >
                    定位 STP
                  </button>
                  <button
                    type="button"
                    className="mini-action quiet-mini-action"
                    disabled={!handoff.report_md || files?.report_md === false || handoffBusy}
                    onClick={() => handoff.report_md && onOpenHandoff(handoff.report_md)}
                  >
                    报告
                  </button>
                  <button
                    type="button"
                    className="mini-action quiet-mini-action"
                    disabled={!handoff.verify_csv || files?.verify_csv === false || handoffBusy}
                    onClick={() => handoff.verify_csv && onOpenHandoff(handoff.verify_csv)}
                  >
                    验证数据
                  </button>
                  <button
                    type="button"
                    className="mini-action quiet-mini-action"
                    disabled={!handoff.handoff_dir || files?.handoff_dir === false || handoffBusy}
                    onClick={() => handoff.handoff_dir && onOpenHandoff(handoff.handoff_dir)}
                  >
                    <FolderOpen size={14} />
                    目录
                  </button>
                </div>
              </article>
            )
          })}
        </div>
      ) : null}
      {matrix?.variants.length ? (
        <div className="variant-quality-list" aria-label="16029 10 12 14 门质量矩阵">
          {matrix.variants.map((variant) => {
            const handoff = handoffByDoorCount.get(variant.door_count)
            const solidworksLauncher = rootLauncherByDoorCount.get(variant.door_count) ?? handoff?.solidworks_launcher
            const freecadLauncher = handoff?.freecad_launcher
            const audit = variant.structural_rule_audit
            const metrics = handoff?.metrics
            return (
              <div key={variant.door_count} className={`variant-quality-row status-${variant.status.toLowerCase()}`}>
                <span>{variant.door_count} 门</span>
                <strong>{variantQualityStatusLabel(variant.status)}</strong>
                <small>
                  bbox X {formatMm(variant.verify_bbox_x_len)} mm / 门宽{' '}
                  {formatMm(audit?.door_width_mm ?? metrics?.door_width_mm ?? null)} / 门高{' '}
                  {formatMm(audit?.door_height_mm ?? metrics?.door_height_mm ?? null)} / 锁孔X{' '}
                  {formatAbsMetric(audit?.lock_center_x_abs_mm)} / 铰链X {formatAbsMetric(audit?.hinge_axis_x_abs_mm)}
                  {' / '}STEP invalid {variant.step_geometry_check?.invalid_shape_count ?? metrics?.step_invalid_shape_count ?? '-'}
                </small>
                <div className="variant-quality-row-actions">
                  <button
                    type="button"
                    className="mini-action"
                    disabled={!solidworksLauncher || handoffBusy}
                    onClick={() => solidworksLauncher && onOpenHandoff(solidworksLauncher)}
                  >
                    <Play size={14} />
                    {solidWorksOpenVerified ? 'SolidWorks 打开' : '尝试打开 / 选中'}
                  </button>
                  <button
                    type="button"
                    className="mini-action quiet-mini-action"
                    disabled={!freecadLauncher || handoffBusy}
                    onClick={() => freecadLauncher && onOpenHandoff(freecadLauncher)}
                  >
                    <Play size={14} />
                    FreeCAD
                  </button>
                  <button
                    type="button"
                    className="mini-action quiet-mini-action"
                    disabled={!handoff?.handoff_dir || handoffBusy}
                    onClick={() => handoff?.handoff_dir && onOpenHandoff(handoff.handoff_dir)}
                  >
                    <FolderOpen size={14} />
                    目录
                  </button>
                </div>
              </div>
            )
          })}
        </div>
      ) : (
        <p>{message}</p>
      )}
      {matrix?.variants.length ? <p>{message} 操作建议：一次只打开一个门数，确认后再切换 SolidWorks 或 FreeCAD。</p> : null}
      <div className="variant-quality-handoff">
        <div>
          <span>工程交接包</span>
          <strong>{handoffBundle?.variants.length ? '10/12/14 门 STEP 交接已整理' : handoffMessage}</strong>
          {handoffBundle?.handoff_dir ? <small>{handoffBundle.handoff_dir}</small> : null}
        </div>
        <div className="variant-quality-row-actions">
          <button
            type="button"
            className="ghost-button"
            disabled={!handoffBundle?.engineer_open_index || handoffBusy}
            onClick={() => handoffBundle?.engineer_open_index && onOpenHandoff(handoffBundle.engineer_open_index)}
          >
            <ClipboardList size={16} />
            工程师清单
          </button>
          <button
            type="button"
            className="ghost-button"
            disabled={!handoffBundle?.handoff_dir || handoffBusy}
            onClick={() => handoffBundle?.handoff_dir && onOpenHandoff(handoffBundle.handoff_dir)}
          >
            <FolderOpen size={16} />
            {handoffBusy ? '打开中' : '打开交接包'}
          </button>
        </div>
      </div>
    </div>
  )
}

function RuleSeedQuantityFormulaCard({ item }: { item: RuleSeedQuantityFormulaItem }) {
  const primaryFormula = item.templateFormulas[0]
  const derivedEntries = Object.entries(primaryFormula?.derived ?? {}).slice(0, 6)
  const checks = primaryFormula?.checks ?? []
  const variantRows = primaryFormula?.variantRows ?? []
  return (
    <article className={`quantity-formula-card status-${item.formulaStatus}`}>
      <div className="review-ledger-heading">
        <Wrench size={17} />
        <div>
          <span>{item.id}</span>
          <strong>{item.label}</strong>
        </div>
        <StatusPill tone={formulaStatusTone(item.formulaStatus)}>{formulaStatusLabel(item.formulaStatus)}</StatusPill>
      </div>
      <div className="rule-seed-value">
        <span>规则值</span>
        <strong>{item.value}</strong>
      </div>
      <div className="formula-derived-grid">
        {derivedEntries.map(([key, value]) => (
          <div key={key}>
            <span>{formulaDerivedLabel(key)}</span>
            <strong>{value ?? 'n/a'}</strong>
          </div>
        ))}
      </div>
      {variantRows.length ? (
        <div className="formula-variant-list">
          {variantRows.map((row) => (
            <div key={row.variant}>
              <span>{variantLabel(row.variant)}</span>
              <strong>{row.doorTotal} 门</strong>
              <small>
                小门 {row.smallDoorAssemblies} / 高门 {row.tallDoorAssemblies} / 锁钩 {row.lockHookCount} / 插销板 {row.latchPlateCount}
              </small>
            </div>
          ))}
        </div>
      ) : null}
      <div className="formula-check-list">
        {checks.map((check) => (
          <div key={check.name} className={check.ok ? 'check-pass' : 'check-open'}>
            <span>{check.ok ? 'PASS' : 'OPEN'}</span>
            <strong>{formulaCheckLabel(check.name)}</strong>
            <small>
              {check.actual ?? 'n/a'} / {check.expected ?? 'n/a'}
            </small>
          </div>
        ))}
      </div>
      <p>{item.formulaSummary}</p>
    </article>
  )
}

function evidenceStateLabel(state: string) {
  const labels: Record<string, string> = {
    auto_calibration_candidate: '可自动定标',
    quantity_evidence_linked: 'BOM 数量已连',
    needs_bom_or_quantity_link: '需连 BOM/数量',
    partial_evidence: '证据部分匹配',
    evidence_gap: '证据缺口',
  }
  return labels[state] ?? state
}

function evidenceStateTone(state: string): StatusTone {
  if (state === 'auto_calibration_candidate') return 'good'
  if (state === 'evidence_gap') return 'risk'
  return 'warn'
}

function formulaStatusLabel(status: string) {
  const labels: Record<string, string> = {
    formula_consistent_candidate: '公式一致候选',
    mixed_formula_candidate: '混合候选',
    quantity_formula_partial: '数量部分闭环',
    step_role_formula_candidate: '角色公式候选',
    variant_formula_candidate: '变体公式候选',
    variant_formula_partial: '变体部分闭环',
  }
  return labels[status] ?? status
}

function formulaStatusTone(status: string): StatusTone {
  if (status === 'formula_consistent_candidate') return 'good'
  if (status === 'variant_formula_candidate') return 'good'
  if (status === 'step_role_formula_candidate') return 'warn'
  if (status === 'quantity_formula_partial') return 'warn'
  if (status === 'variant_formula_partial') return 'warn'
  return 'idle'
}

function formulaDerivedLabel(key: string) {
  const labels: Record<string, string> = {
    patternInstanceCount: '阵列实例',
    pitchMm: '节距 mm',
    shelfPanelsPerColumn: '每列层板',
    inferredDoorRows: '推导行数',
    totalShelfPanels: '层板总数',
    horizontalDividersPerColumn: '横隔板/列',
    doorTotalFromBomName: 'BOM 总门数',
    inferredColumns: '推导列数',
    smallDoorAssemblies: '小门组',
    tallDoorAssemblies: '高门组',
    electricLockSets: '电控锁组',
    lockHookCount: '锁钩数',
    latchPlateCount: '插销固定板',
    horizontalDividers: '门框横隔板',
    verticalDividers: '门框竖隔板',
    shelfPanelObjects: '层板对象',
    repeatedRoleGroups: '重复角色组',
    closedFullAssemblyVariants: '总装变体',
    doorTotalFormula: '门数公式',
    lockHookFormula: '锁钩公式',
    latchPlateFormula: '插销板公式',
    module12DoorAssemblies: '12/12装配',
    module12DoorPanels: '12/12门板',
  }
  return labels[key] ?? key
}

function variantLabel(variant: string) {
  const labels: Record<string, string> = {
    '4door': '4 门 STEP',
    '7door_simplified': '7 门 STEP',
    '8door': '8 门 STEP',
  }
  return labels[variant] ?? variant
}

function formulaCheckLabel(name: string) {
  const labels: Record<string, string> = {
    shelf_panels_per_column_consistent: 'L/R 层板数量一致',
    shelf_rows_inferred_from_panels: '层板推导行数',
    horizontal_dividers_infer_rows: '横隔板推导行数',
    pattern_count_matches_inferred_rows: '阵列数匹配行数',
    door_total_divisible_by_rows: '总门数可整除行数',
    small_door_panel_matches_assembly: '小门板匹配装配',
    lock_hook_matches_electric_lock_sets: '锁钩匹配电控锁',
    latch_plates_two_per_lock_hook: '插销固定板成对',
    frame_dividers_present: '门框分隔证据',
    variant_formula_requires_more_templates: '需绑定多门数变体',
    variant_door_total_matches_step: '门数匹配 STEP',
    variant_lock_hook_matches_door_total: '锁钩数匹配门数',
    variant_latch_plates_two_per_door: '插销板=2×门数',
    module_12door_evidence_available: '12/12 模块证据',
  }
  return labels[name] ?? name
}

function dryRunCheckLabel(name: string) {
  const labels: Record<string, string> = {
    freecad_command: 'FreeCAD 命令',
    solidworks_shortcut: 'SolidWorks 桌面入口',
    solidworks_gui: 'SolidWorks 程序',
    solidworks_automation_host: '脚本宿主',
    solidworks_command: 'SolidWorks 命令',
    solidworks_single_run_guard: 'SolidWorks 单任务保护',
    solidworks_16029_source_files: '16029 源文件清单',
    solidworks_16029_spec_preview: '16029 装配清单预检',
    solidworks_16029_native_reference_geometry_gate: '16029 原生参考质量门',
    solidworks_16029_native_skeleton_geometry_gate: '16029 原生骨架质量门',
    generator_script: '生成脚本',
    parameters: '参数',
    evidence: '证据',
    output_boundary: '输出边界',
    worker_command: 'worker 命令',
  }
  return labels[name] ?? name
}

function RuleLearningAxisCard({ axis }: { axis: RuleLearningAxis }) {
  return (
    <article className={`axis-card maturity-${axis.maturity}`}>
      <div className="axis-card-header">
        <Wrench size={18} />
        <div>
          <strong>{axis.axis}</strong>
          <span>{axis.sourceTemplates}</span>
        </div>
        <StatusPill tone={maturityTone(axis.maturity)}>{maturityLabels[axis.maturity]}</StatusPill>
      </div>
      <p>{axis.purpose}</p>
      <div className="axis-state">
        <span>当前状态</span>
        <strong>{axis.currentState}</strong>
      </div>
      <small>{axis.nextAction}</small>
      <EvidenceRow evidence={axis.evidence} />
    </article>
  )
}

function ReviewCard({ item }: { item: ReviewItem }) {
  return (
    <article className={`review-card priority-${item.priority.toLowerCase()}`}>
      <div className="review-main">
        <div>
          <span>{item.id}</span>
          <strong>{item.issue}</strong>
          <p>{item.project} / {item.module}</p>
        </div>
        <StatusPill tone={item.priority === 'P0' ? 'risk' : item.priority === 'P1' ? 'warn' : 'idle'}>
          {item.severity}
        </StatusPill>
      </div>
      <div className="review-action">
        <Archive size={17} />
        <span>{item.nextAction}</span>
      </div>
      <EvidenceRow evidence={item.evidence} />
    </article>
  )
}

function StatusPill({
  tone,
  children,
}: {
  tone: 'good' | 'warn' | 'risk' | 'idle'
  children: React.ReactNode
}) {
  return <span className={`status-pill tone-${tone}`}>{children}</span>
}

function EvidenceRow({ evidence }: { evidence: Array<Evidence | string> }) {
  return (
    <div className="evidence-row">
      {evidence.map((item) => (
        <span key={item} className={`evidence-chip tone-${evidenceToneFor(item)}`}>
          {item}
        </span>
      ))}
    </div>
  )
}

function evidenceToneFor(item: Evidence | string) {
  return evidenceTone[item as Evidence] ?? 'slate'
}

function taskStatusTone(status: GenerationTask['status']): 'good' | 'warn' | 'risk' | 'idle' {
  if (status === 'ready_to_run' || status === 'completed_reference') return 'good'
  if (status === 'draft_pending_worker' || status === 'running' || status === 'requires_manual_run') return 'warn'
  if (status === 'blocked_pending_evidence' || status === 'blocked_preflight_failed') return 'risk'
  if (status === 'failed_worker') return 'risk'
  return 'idle'
}

function canRunSolidWorksPackageStatus(status: GenerationTask['status']) {
  return status === 'ready_to_run' || status === 'requires_manual_run' || status === 'completed_reference' || status === 'failed_worker'
}

function canRunSolidWorksPackageTask(task: GenerationTask) {
  return canRunSolidWorksPackageStatus(task.status) && !isLegacySolidWorksDirectAssemblyTask(task)
}

function isLegacySolidWorksDirectAssemblyTask(task: GenerationTask) {
  return task.cad_runner === 'solidworks' && task.command.includes('sw_build_locker_16029_direct_assembly')
}

function generationRouteKind(capabilityId: string) {
  if (capabilityId === 'locker_16029_regression') return 'template'
  if (capabilityId === 'locker_16038_variant_template') return 'template'
  if (capabilityId === 'locker_16029_door_panel') return 'part'
  return 'queued'
}

function generationRouteTitle(capabilityId: string) {
  if (capabilityId === 'locker_16029_regression') return '16029 原生 SolidWorks 参考样机 / FreeCAD 规则验证'
  if (capabilityId === 'locker_16038_variant_template') return 'SolidWorks 标准模板克隆 / 证据绑定'
  if (capabilityId === 'locker_16029_door_panel') return 'SolidWorks 单门板参数化零件'
  return '规则学习中，暂不进入生成器'
}

function generationRouteDetail(capabilityId: string) {
  if (capabilityId === 'locker_16029_regression') {
    return 'SolidWorks 大按钮开放 10/12/14 门原生整柜参考，并优先使用增强矩阵样机 v2。FreeCAD 保留同门数规则验证件。当前仍是工程参考模型，不是生产图纸/BOM。'
  }
  if (capabilityId === 'locker_16038_variant_template') {
    return '点击 SolidWorks 后会打开并保存已验证的 4/7/8 门整柜或 12/12 模块母版；这是同尺寸模板参考，不是任意门数自动重排。'
  }
  if (capabilityId === 'locker_16029_door_panel') {
    return '用于门板尺寸零件验证；门数变化请走整柜入口，避免把门板类型误当成门数。'
  }
  return '需要先完成 SolidWorks/STEP/BOM 证据闭环和规则抽取，之后才会开放生成按钮。'
}

function solidworksGenerationModeLabel(mode: string) {
  if (mode === 'native_skeleton_reference') return '原生整柜参考'
  if (mode === 'template_clone') return '标准总装模板克隆'
  if (mode === 'direct_component_assembly') return '按规则逐件装配'
  if (mode === 'manual_package') return '待本地 SolidWorks 执行'
  return '待判定'
}

function solidworksNullableCount(value: number | null | undefined) {
  return typeof value === 'number' ? String(value) : '-'
}

function solidworksLayoutStatusLabel(status: string | null | undefined) {
  if (status === 'verified_equal_row_native_source') return '等高原生门组件'
  if (status === 'equal_row_rebuilt_panel_candidate') return '等高重建门板'
  if (status === 'equal_row_parametric_panel_reference') return '等高参数门板'
  if (status === 'verified_same_envelope_layout') return '已验证配方'
  if (status === 'formula_composed_from_1_12_to_6_12_source_modules') return '源模块公式组合'
  return '-'
}

function solidworksComponentInsertLabel(added: number | null | undefined, requested: number | null | undefined) {
  if (typeof added === 'number' && typeof requested === 'number') return `${added}/${requested}`
  if (typeof added === 'number') return String(added)
  return '-'
}

function solidworksValidationCountLabel(summary: SolidWorksRunSummary) {
  const pass = solidworksNullableCount(summary.validation_pass_count)
  const warn = solidworksNullableCount(summary.validation_warn_count)
  const fail = solidworksNullableCount(summary.validation_fail_count)
  if (pass === '-' && warn === '-' && fail === '-') return '-'
  return `pass ${pass} / warn ${warn} / fail ${fail}`
}

function solidworksComponentTreeLabel(summary: SolidWorksRunSummary) {
  if (summary.quality_status === 'reference_feature_only') {
    return `Reference ${solidworksNullableCount(summary.reference_like_feature_count)}`
  }
  if (summary.quality_status === 'component_reference_tree') {
    return `validated refs ${solidworksNullableCount(summary.reference_like_feature_count)}`
  }
  if (summary.quality_status === 'component_tree') {
    return `${solidworksNullableCount(summary.all_component_count)} components`
  }
  if (typeof summary.all_component_count === 'number') return `${summary.all_component_count} components`
  return '-'
}

function cadRunnerLabel(cadRunner: CadRunner) {
  return cadRunner === 'solidworks' ? 'SOLIDWORKS 2025' : 'FreeCAD 1.1.1'
}

function variantQualityStatusLabel(status: string) {
  if (status === 'PASS_READY_FOR_ENGINEERING_REVIEW') return '可工程复核'
  if (status === 'PASS_STEP_GEOMETRY_NEEDS_FCSTD_AUDIT') return 'STEP 通过 / 待完整性审计'
  if (status === 'PASS_RULE_COUNTS_NEEDS_FCSTD_AUDIT') return '数量通过 / 待完整性审计'
  if (status === 'MISSING_OUTPUT') return '缺少输出'
  if (status === 'FAIL') return '质量失败'
  return status
}

function handoffGateState(handoff: Locker16029EngineeringHandoffBundle['variants'][number]): {
  ready: boolean
  tone: StatusTone
  badge: string
  detail: string
} {
  const metrics = handoff.metrics
  const fileStatus = handoff.file_status
  const blockers: string[] = []
  const invalidShapeCount = metrics?.step_invalid_shape_count
  const handoffStep = handoff.stp ?? handoff.step

  if (handoff.status !== 'PASS_READY_FOR_ENGINEERING_REVIEW') blockers.push(variantQualityStatusLabel(handoff.status ?? 'UNKNOWN'))
  if (!handoffStep) blockers.push('缺少 STP')
  if (handoff.handoff_files_ready === false) blockers.push('交接文件不齐')
  if (fileStatus) {
    if (fileStatus.stp === false) blockers.push('STP 文件不存在')
    if (fileStatus.root_solidworks_launcher === false) blockers.push('SolidWorks 根启动入口不存在')
    if (fileStatus.solidworks_launcher === false) blockers.push('SolidWorks 子启动入口不存在')
    if (fileStatus.report_md === false) blockers.push('报告文件不存在')
    if (fileStatus.verify_csv === false) blockers.push('验证数据不存在')
    if (fileStatus.handoff_dir === false) blockers.push('交接目录不存在')
  }
  if (metrics?.step_geometry_status !== 'geometry_check_pass') blockers.push('STEP 几何未通过')
  if (typeof invalidShapeCount !== 'number' || invalidShapeCount !== 0) blockers.push('STEP invalid 未归零')
  if (metrics?.fcstd_integrity_status !== 'PASS') blockers.push('FCStd 完整性未通过')
  if (metrics?.structural_rule_status !== 'PASS') blockers.push('结构规则未通过')

  const uniqueBlockers = [...new Set(blockers)]

  if (!uniqueBlockers.length) {
    return {
      ready: true,
      tone: 'good',
      badge: 'PASS',
      detail: '可交给结构工程师用 SolidWorks 打开 STP 复核；仍不是正式生产图纸。',
    }
  }

  return {
    ready: false,
    tone: 'warn',
    badge: '需查看',
    detail: `暂不作为工程交接模型：${uniqueBlockers.join('，')}。`,
  }
}

function runnerButtonLabel(cadRunner: CadRunner, capabilityId?: string) {
  if (capabilityId === 'locker_16029_regression' && cadRunner === 'solidworks') {
    return '生成原生参考模型'
  }
  if (capabilityId === 'locker_16029_regression' && cadRunner === 'freecad') {
    return '生成规则 STEP / FCStd'
  }
  return cadRunner === 'solidworks' ? '一键运行 SOLIDWORKS 2025' : '创建 FreeCAD 任务草稿'
}

function runnerButtonDetail(cadRunner: CadRunner, fallback: string, capabilityId?: string) {
  if (capabilityId === 'locker_16029_regression' && cadRunner === 'solidworks') {
    return '10/12/14 门使用增强矩阵样机 v2。先 dry-run，再启动 SolidWorks 打开并另存。'
  }
  if (capabilityId === 'locker_16029_regression' && cadRunner === 'freecad') {
    return '生成 10/12/14 门规则参考模型，并进入 STEP/FCStd 质量校验。'
  }
  return cadRunner === 'solidworks' ? '创建任务、自动 dry-run，通过后直接启动 SolidWorks。' : fallback
}

function runnerCommandPreviewText(
  runner: (typeof cadRunners)[number],
  capability: Capability,
  runnerIssue: string | null,
  commandPreview: (cadRunner: CadRunner) => string,
) {
  if (capability.generator === 'not_enabled') {
    return `${runner.label}: not_enabled: 需要先完成证据修复或模块接口确认`
  }
  if (runnerIssue) {
    return `${runner.label}: 当前参数不支持该入口 - ${runnerIssue}`
  }
  if (capability.id === 'locker_16029_regression' && runner.id === 'solidworks') {
    return `${runner.label}: 10/12/14门使用 enriched_v2.SLDASM，打开并另存当前门数原生参考。`
  }
  return `${runner.label}: ${commandPreview(runner.id)}`
}

function taskMatchesActiveParameters(task: GenerationTask, activeParameters: ParameterValues) {
  const activeEntries = Object.entries(activeParameters)
  if (!activeEntries.length) return false
  return activeEntries.every(([name, value]) => String(task.parameters[name] ?? '') === String(value))
}

function taskMatchesPrimaryParameters(task: GenerationTask, activeCapabilityId: string, activeParameters: ParameterValues) {
  if (task.capability_id !== activeCapabilityId) return false
  if (activeCapabilityId === 'locker_16029_regression') {
    return (
      String(task.parameters.door_count ?? '') === String(activeParameters.door_count ?? '') &&
      String(task.parameters.cabinet_width ?? '') === String(activeParameters.cabinet_width ?? '')
    )
  }
  return taskMatchesActiveParameters(task, activeParameters)
}

function taskPriorityScore(task: GenerationTask, activeCapabilityId: string, activeParameters: ParameterValues) {
  let score = 0
  if (task.capability_id === activeCapabilityId) score += 1000
  if (task.capability_id === activeCapabilityId && taskMatchesActiveParameters(task, activeParameters)) score += 260
  if (task.cad_runner === 'solidworks') score += 60
  if (task.status === 'ready_to_run' || task.status === 'requires_manual_run') score += 45
  if (task.status === 'completed_reference') score += 30
  if (task.status === 'running') score += 20
  if (isLegacySolidWorksDirectAssemblyTask(task)) score -= 1200
  return score
}

function prioritizeGenerationTasks(tasks: GenerationTask[], activeCapabilityId: string, activeParameters: ParameterValues) {
  return [...tasks].sort((left, right) => {
    const scoreDelta =
      taskPriorityScore(right, activeCapabilityId, activeParameters) -
      taskPriorityScore(left, activeCapabilityId, activeParameters)
    if (scoreDelta !== 0) return scoreDelta
    return new Date(right.created_at).getTime() - new Date(left.created_at).getTime()
  })
}

function taskRelevanceLabel(task: GenerationTask, activeCapabilityId: string, activeParameters: ParameterValues) {
  if (task.capability_id === activeCapabilityId && taskMatchesActiveParameters(task, activeParameters)) return '当前模型 / 当前参数'
  if (task.capability_id === activeCapabilityId) return '当前模型历史任务'
  return '其他模型历史任务'
}

function taskNextActionLabel(task: GenerationTask) {
  if (isLegacySolidWorksDirectAssemblyTask(task)) return '历史错乱路线，已停用'
  if (task.status === 'draft_pending_worker') return '下一步：dry-run'
  if (task.status === 'ready_to_run' && task.cad_runner === 'solidworks') return '下一步：运行 SolidWorks'
  if (task.status === 'ready_to_run') return '下一步：执行 worker'
  if (task.status === 'requires_manual_run') return '下一步：运行 SolidWorks'
  if (task.status === 'completed_reference') return '可打开输出'
  if (task.status === 'blocked_preflight_failed') return '查看阻塞项'
  if (task.status === 'failed_worker') return '查看日志后重试'
  if (task.status === 'running') return '运行中'
  return '查看详情'
}

function freecadQualityTitle(status?: string | null, taskStatus?: GenerationTask['status']) {
  if (status === 'freecad_geometry_pass') return 'STEP/FCStd 自动复核通过'
  if (status === 'freecad_step_pass_fcstd_pending') return 'STEP 通过，FCStd 待复核'
  if (status === 'freecad_run_lock_active') return '已有 FreeCAD 任务运行'
  if (status === 'freecad_postprocess_needs_review') return '自动复核需查看'
  if (status === 'freecad_postprocess_not_run') return '自动复核未运行'
  if (taskStatus === 'failed_worker') return 'worker 执行失败'
  return '自动复核状态待确认'
}

function freecadQualityTone(status?: string | null, taskStatus?: GenerationTask['status']): StatusTone {
  if (status === 'freecad_geometry_pass') return 'good'
  if (status === 'freecad_run_lock_active' || taskStatus === 'failed_worker') return 'risk'
  if (status) return 'warn'
  return 'idle'
}

function freecadQualityBadge(status?: string | null) {
  if (status === 'freecad_geometry_pass') return 'PASS'
  if (status === 'freecad_step_pass_fcstd_pending') return 'STEP PASS'
  if (status === 'freecad_run_lock_active') return 'LOCKED'
  if (status === 'freecad_postprocess_needs_review') return 'REVIEW'
  if (status === 'freecad_postprocess_not_run') return 'PENDING'
  return status ?? 'pending'
}

function freecadQualityCheckState(
  status: string | null,
  check: 'step' | 'fcstd' | 'matrix',
): 'ready' | 'warning' | 'waiting' {
  if (status === 'freecad_geometry_pass') return 'ready'
  if (status === 'freecad_step_pass_fcstd_pending') return check === 'step' ? 'ready' : 'warning'
  if (status === 'freecad_postprocess_needs_review') return 'warning'
  return 'waiting'
}

function taskRunSnapshotFor(task: GenerationTask): {
  tone: StatusTone
  label: string
  title: string
  facts: Array<{ label: string; value: string }>
  note?: string
} {
  const execution = task.execution_result
  const outputs = execution?.outputs ?? []
  const lowerOutputs = outputs.map((path) => path.toLowerCase())
  const hasNativeSolidWorksOutput = lowerOutputs.some((path) => path.endsWith('.sldasm') || path.endsWith('.sldprt'))
  const hasFcstdOutput = lowerOutputs.some((path) => path.endsWith('.fcstd'))
  const hasStepOutput = lowerOutputs.some((path) => path.endsWith('.step') || path.endsWith('.stp'))
  const hasValidationReport = lowerOutputs.some((path) => path.endsWith('_solidworks_validation_report.md') || path.endsWith('_report.md'))
  const summary = execution?.solidworks_run_summary ?? null
  const qualityStatus = execution?.solidworks_quality_status ?? summary?.quality_status ?? null
  const freecadQualityStatus = execution?.freecad_quality_status ?? null

  if (isLegacySolidWorksDirectAssemblyTask(task)) {
    return {
      tone: 'warn',
      label: '历史路线',
      title: '已停用，不再作为当前生成入口',
      facts: [
        { label: '原因', value: 'direct assembly 曾出现错乱' },
        { label: '建议', value: '改用增强样机入口' },
        { label: '门数', value: task.parameters.door_count ?? '-' },
      ],
      note: '当前工程复核请用 16029 的 10/12/14 门原生 SolidWorks 增强样机入口，历史 direct assembly 不再作为交接路线。',
    }
  }

  if (task.cad_runner === 'solidworks') {
    if (!execution) {
      return {
        tone: task.status === 'ready_to_run' ? 'warn' : 'idle',
        label: 'SolidWorks 反馈',
        title: task.status === 'ready_to_run' ? 'dry-run 已通过，等待启动' : '尚未运行 SolidWorks',
        facts: [
          { label: '路线', value: generationRouteTitle(task.capability_id) },
          { label: '门数', value: task.parameters.door_count ?? '-' },
          { label: '输出', value: '待生成' },
        ],
        note: task.status === 'draft_pending_worker' ? '先执行 dry-run，确认脚本、参数和证据门。' : undefined,
      }
    }

    if (task.status === 'failed_worker') {
      return {
        tone: 'risk',
        label: 'SolidWorks 反馈',
        title: '生成失败，需看 stderr / 日志',
        facts: [
          { label: 'exit', value: String(execution.exit_code ?? 'n/a') },
          { label: '输出', value: `${outputs.length} 个文件` },
          { label: '门数', value: summary?.door_count ?? task.parameters.door_count ?? '-' },
        ],
        note: execution.message,
      }
    }

    if (task.status === 'requires_manual_run') {
      return {
        tone: 'warn',
        label: 'SolidWorks 反馈',
        title: '任务包已准备，等待运行',
        facts: [
          { label: '模式', value: solidworksGenerationModeLabel(summary?.generation_mode ?? 'manual_package') },
          { label: '门数', value: summary?.door_count ?? task.parameters.door_count ?? '-' },
          { label: '输出', value: `${outputs.length} 个文件` },
        ],
        note: '点击运行 SolidWorks 生成后，才会写出原生 SLDASM/SLDPRT 和验证报告。',
      }
    }

    if (qualityStatus === 'reference_feature_only') {
      return {
        tone: 'warn',
        label: 'SolidWorks 反馈',
        title: '可打开查看，但组件树未达标',
        facts: [
          { label: '模式', value: solidworksGenerationModeLabel(summary?.generation_mode ?? 'unknown') },
          { label: '门数', value: summary?.door_count ?? task.parameters.door_count ?? '-' },
          { label: '质量', value: 'Reference' },
        ],
        note: execution.solidworks_quality_summary ?? summary?.quality_summary ?? '工程参考模型，不作为生产级可编辑组件树。',
      }
    }

    return {
      tone: hasNativeSolidWorksOutput ? 'good' : 'warn',
      label: 'SolidWorks 反馈',
      title: hasNativeSolidWorksOutput ? '原生模型已生成' : '执行完成但未找到原生模型',
      facts: [
        { label: '模式', value: solidworksGenerationModeLabel(summary?.generation_mode ?? 'unknown') },
        { label: '验证', value: summary ? solidworksValidationCountLabel(summary) : hasValidationReport ? '报告可打开' : '待汇总' },
        { label: '输出', value: `${outputs.length} 个文件` },
      ],
      note: summary?.next_action ?? execution.message,
    }
  }

  if (!execution) {
    return {
      tone: task.status === 'ready_to_run' ? 'warn' : 'idle',
      label: 'FreeCAD / STEP 反馈',
      title: task.status === 'ready_to_run' ? 'dry-run 已通过，等待 worker' : '尚未执行 worker',
      facts: [
        { label: '门数', value: task.parameters.door_count ?? task.parameters.door_height ?? '-' },
        { label: '输出', value: '待生成' },
        { label: '定位', value: '开源迁移路线' },
      ],
    }
  }

  if (task.cad_runner === 'freecad' && freecadQualityStatus) {
    return {
      tone: freecadQualityTone(freecadQualityStatus, task.status),
      label: 'FreeCAD 自动复核',
      title: freecadQualityTitle(freecadQualityStatus, task.status),
      facts: [
        { label: '门数', value: task.parameters.door_count ?? '-' },
        { label: '复核', value: freecadQualityBadge(freecadQualityStatus) },
        { label: '输出', value: `${outputs.length} 个文件` },
      ],
      note: execution.freecad_quality_summary ?? execution.message,
    }
  }

  return {
    tone: task.status === 'failed_worker' ? 'risk' : hasFcstdOutput || hasStepOutput ? 'good' : 'warn',
    label: 'FreeCAD / STEP 反馈',
    title: task.status === 'failed_worker' ? 'worker 执行失败' : hasFcstdOutput ? 'FCStd 参考模型已生成' : 'STEP/报告已生成',
    facts: [
      { label: 'FCStd', value: hasFcstdOutput ? 'ready' : 'n/a' },
      { label: 'STEP', value: hasStepOutput ? 'ready' : 'n/a' },
      { label: '输出', value: `${outputs.length} 个文件` },
    ],
    note: execution.message,
  }
}

function ruleLearningTone(value: TemplateAsset['ruleLearningValue']): 'good' | 'warn' | 'risk' | 'idle' {
  if (value === '高') return 'good'
  if (value === '中高' || value === '中') return 'warn'
  return 'idle'
}

function ruleExtractionTone(status?: RuleExtractionResult['status']): 'good' | 'warn' | 'risk' | 'idle' {
  if (status === 'completed') return 'good'
  if (status === 'failed') return 'risk'
  if (status === 'running' || status === 'requires_solidworks_run') return 'warn'
  return 'idle'
}

function ruleExtractionNoteTone(status?: RuleExtractionResult['status']): string {
  if (status === 'completed') return 'success'
  if (status === 'failed') return 'error'
  if (status === 'running') return 'warning'
  return ''
}

function ruleExtractionListMessage(runs: RuleExtractionResult[]) {
  if (!runs.length) return '暂无规则提取记录，先从 16038/16029 建立证据包。'
  const runningRuns = runs.filter((run) => run.status === 'running')
  if (runningRuns.length) return `${runningRuns.length} 个 SolidWorks 提取任务正在运行，页面每 3 秒自动刷新。`
  return ruleExtractionFeedbackMessage(runs[0])
}

function ruleExtractionFeedbackMessage(run: RuleExtractionResult) {
  if (run.status === 'running') {
    return `${run.template_title} 正在提取：SolidWorks 已开始执行，完成后会自动刷新为 completed，并显示组件、配合、特征和阵列种子统计。`
  }

  if (run.status === 'completed') {
    const counts = run.learning_summary?.counts
    const evidenceParts = [
      typeof counts?.components === 'number' ? `组件 ${counts.components}` : null,
      typeof counts?.mateFeatureCount === 'number' ? `配合 ${counts.mateFeatureCount}` : null,
      typeof counts?.features === 'number' ? `特征 ${counts.features}` : null,
      typeof counts?.dimensions === 'number' ? `尺寸 ${counts.dimensions}` : null,
      typeof counts?.stepObjectBBoxCount === 'number' ? `STEP bbox ${counts.stepObjectBBoxCount}` : null,
      typeof counts?.stepRoleBindingCount === 'number' ? `角色绑定 ${counts.stepRoleBindingCount}` : null,
      typeof counts?.patternRuleCount === 'number' ? `阵列种子 ${counts.patternRuleCount}` : null,
    ].filter(Boolean)
    const evidenceText = evidenceParts.length ? evidenceParts.join('，') : `${run.outputs.length} 个输出文件`
    return `${run.template_title} 提取完成：${evidenceText}。质量门：${ruleQualityLabel(run.learning_summary?.qualityGate)}。`
  }

  if (run.status === 'failed') {
    const exitText = run.exit_code === null || run.exit_code === undefined ? '无退出码' : `退出码 ${run.exit_code}`
    return `${run.template_title} 提取失败：${exitText}。打开证据目录查看 stdout/stderr。`
  }

  return `${run.template_title} 已准备提取包，点击“运行 SolidWorks 提取”后会自动跟踪完成状态。`
}

function ruleQualityState(qualityGate?: string): 'ready' | 'warning' | 'pending' {
  if (qualityGate === 'ready_for_rule_learning') return 'ready'
  if (qualityGate === 'ready_for_rule_binding') return 'ready'
  if (qualityGate === 'step_role_binding_available_needs_formula_derivation') return 'warning'
  if (qualityGate === 'step_bbox_available_needs_component_mapping') return 'warning'
  if (qualityGate === 'needs_mate_or_relation_binding') return 'warning'
  if (qualityGate === 'component_tree_available_needs_position_evidence') return 'warning'
  if (qualityGate === 'needs_component_tree_or_bbox_repair') return 'warning'
  return 'pending'
}

function ruleQualityLabel(qualityGate?: string): string {
  if (qualityGate === 'ready_for_rule_learning') return '可进入规则学习'
  if (qualityGate === 'ready_for_rule_binding') return '可绑定生成规则'
  if (qualityGate === 'step_role_binding_available_needs_formula_derivation') return '角色绑定可用'
  if (qualityGate === 'step_bbox_available_needs_component_mapping') return 'STEP位置证据可用'
  if (qualityGate === 'needs_mate_or_relation_binding') return '需绑定配合语义'
  if (qualityGate === 'component_tree_available_needs_position_evidence') return '组件树已读，需补位置证据'
  if (qualityGate === 'needs_component_tree_or_bbox_repair') return '需修复位置证据'
  return qualityGate ?? '待汇总'
}

function coverageLabel(value?: number, total?: number): string {
  if (typeof value !== 'number' || typeof total !== 'number') return '待汇总'
  return `${value}/${total}`
}

function bboxSizeLabel(bbox?: { sizeX?: number; sizeY?: number; sizeZ?: number } | null): string {
  if (!bbox) return '待汇总'
  const sizes = [bbox.sizeX, bbox.sizeY, bbox.sizeZ]
  if (sizes.some((value) => typeof value !== 'number')) return '待汇总'
  return `${sizes.map((value) => Number(value).toFixed(1)).join(' x ')} mm`
}

function maturityTone(maturity: Maturity): 'good' | 'warn' | 'risk' | 'idle' {
  if (maturity === 'engineering_reference' || maturity === 'production_candidate') return 'good'
  if (maturity === 'mapped' || maturity === 'dxf_parsed' || maturity === 'solidworks_extracted' || maturity === 'step_bbox_measured') {
    return 'warn'
  }
  if (maturity === 'manual_review_required' || maturity === 'blocked') return 'risk'
  return 'idle'
}

function feedbackForExecution(task: GenerationTask): GenerationFeedback {
  const recommendedFile = recommendedOutputFile(task)
  const outputDir = task.execution_result?.output_dir
  const validationReport = solidworksValidationReportFile(task)
  const validationData = solidworksValidationDataFile(task)
  const freecadQualityStatus = task.execution_result?.freecad_quality_status ?? null
  const freecadQualityNeedsReview =
    task.cad_runner === 'freecad' &&
    Boolean(freecadQualityStatus) &&
    freecadQualityStatus !== 'freecad_geometry_pass'

  if (task.status === 'completed_reference') {
    const cleanedSolidWorksAssembly =
      task.cad_runner === 'solidworks' && Boolean(recommendedFile?.toLowerCase().endsWith('.sldasm'))
    const referenceFeatureOnly = task.execution_result?.solidworks_quality_status === 'reference_feature_only'
    const validatedComponentReferences = task.execution_result?.solidworks_quality_status === 'component_reference_tree'
    let detail = '工程参考模型已写入输出目录。'
    if (validatedComponentReferences) {
      detail =
        task.execution_result?.solidworks_quality_summary ??
        'SolidWorks 装配已生成，组件引用和验证清单匹配，可作为结构工程参考装配打开复核。'
    } else if (referenceFeatureOnly) {
      detail =
        task.execution_result?.solidworks_quality_summary ??
        'SolidWorks 装配已生成并清理显示，但质量诊断显示它是 Reference 特征模型，只能作为视觉/摆放工程参考。'
    } else if (cleanedSolidWorksAssembly && recommendedFile) {
      detail = `SolidWorks 原生装配已生成，并已清理参考面/草图显示；验证报告已同步生成，建议直接打开 ${outputFileLabel(
        recommendedFile,
      )}。`
    } else if (task.cad_runner === 'freecad' && task.execution_result?.freecad_quality_summary) {
      detail = task.execution_result.freecad_quality_summary
    } else if (recommendedFile) {
      detail = `工程参考模型已写入输出目录，建议打开：${outputFileLabel(recommendedFile)}。`
    }
    return {
      taskId: task.id,
      tone: referenceFeatureOnly || freecadQualityNeedsReview ? 'warn' : 'good',
      title: referenceFeatureOnly
        ? '生成完成但仅限工程参考'
        : freecadQualityNeedsReview
          ? '生成完成但自动复核需查看'
          : '生成完成',
      detail,
      cadRunner: task.cad_runner,
      outputDir,
      recommendedFile,
      validationReport: validationReport ?? task.execution_result?.freecad_quality_report ?? undefined,
      validationData,
    }
  }

  if (task.status === 'requires_manual_run') {
    return {
      taskId: task.id,
      tone: 'warn',
      title: 'SolidWorks 手动包已生成',
      detail: recommendedFile
        ? 'API 未自动启动 SolidWorks；请确认许可和会话状态后运行推荐脚本。'
        : 'API 未自动启动 SolidWorks；请在输出目录中运行手动脚本。',
      outputDir,
      recommendedFile,
      cadRunner: task.cad_runner,
      validationReport,
      validationData,
    }
  }

  if (task.status === 'failed_worker') {
    return {
      taskId: task.id,
      tone: 'risk',
      title: '生成失败',
      detail: 'worker 执行失败，请查看 stderr 和执行日志。',
      cadRunner: task.cad_runner,
      outputDir,
      recommendedFile,
      validationReport,
      validationData,
    }
  }

  return {
    taskId: task.id,
    tone: 'warn',
    title: '生成状态已更新',
    detail: `worker 状态: ${task.status}`,
    cadRunner: task.cad_runner,
    outputDir,
    recommendedFile,
    validationReport,
    validationData,
  }
}

function recommendedOutputFile(task: GenerationTask) {
  const outputs = task.execution_result?.outputs ?? []
  const lower = (path: string) => path.toLowerCase()

  if (task.cad_runner === 'solidworks') {
    return (
      outputs.find((path) => lower(path).endsWith('.sldasm')) ??
      outputs.find((path) => lower(path).endsWith('.sldprt')) ??
      outputs.find((path) => lower(path).endsWith('.ps1')) ??
      outputs[0]
    )
  }

  return (
    outputs.find((path) => lower(path).endsWith('_solidworks_import.stp')) ??
    outputs.find((path) => lower(path).endsWith('.step') || lower(path).endsWith('.stp')) ??
    outputs.find((path) => lower(path).endsWith('.fcstd')) ??
    outputs[0]
  )
}

function solidworksValidationReportFile(task: GenerationTask) {
  return task.execution_result?.outputs.find((path) => path.toLowerCase().endsWith('_solidworks_validation_report.md'))
}

function solidworksValidationDataFile(task: GenerationTask) {
  return task.execution_result?.outputs.find((path) => path.toLowerCase().endsWith('_solidworks_validation.csv'))
}

function isSolidWorksNativeFile(path: string) {
  const lower = path.toLowerCase()
  return lower.endsWith('.sldasm') || lower.endsWith('.sldprt')
}

function isPowerShellScript(path: string) {
  return path.toLowerCase().endsWith('.ps1')
}

function openButtonLabelFor(path: string) {
  const lower = path.toLowerCase()
  if (lower.endsWith('.sldasm')) return '打开 SolidWorks 原生装配'
  if (lower.endsWith('.sldprt')) return '打开 SolidWorks 原生零件'
  if (lower.endsWith('.ps1')) return '查看手动脚本'
  if (lower.endsWith('.fcstd')) return '打开 FreeCAD 原生文件'
  if (lower.endsWith('.step') || lower.endsWith('.stp')) return '打开 STEP 几何'
  if (lower.endsWith('locker_16029_freecad_postprocess.json')) return '打开自动复核报告'
  if (lower.endsWith('freecad_geometry_check.json')) return '打开 STEP 检查'
  if (lower.endsWith('_geometry_integrity.md')) return '打开 FCStd 检查'
  return '打开推荐文件'
}

function solidworksScriptFor(capabilityId: string) {
  if (capabilityId === 'locker_16029_door_panel') return 'scripts\\sw_make_parametric_door_panel.js'
  if (capabilityId === 'locker_16038_variant_template') return 'scripts\\sw_clone_16038_variant_template.js'
  return 'scripts\\sw_clone_16029_baseline_template.js'
}

function solidworksGeneratorFor(capability: { id: string; generator: string }) {
  if (capability.generator === 'not_enabled') return 'not_enabled'
  return solidworksScriptFor(capability.id)
}

function freecadScriptFor(capabilityId: string, fallback: string) {
  if (capabilityId === 'locker_16029_door_panel') return 'scripts\\generate_locker_16029_freecad.py'
  return fallback
}

const numericParameterNames = new Set(['door_count', 'cabinet_width', 'door_width', 'door_height', 'flat_holes'])
const defaultDoorCountPresets = LOCKER_16029_SUPPORTED_RULE_COUNTS.map(String)
const doorCountPresetsByCapability: Record<string, string[]> = {
  locker_16038_variant_template: ['4', '7', '8', '12'],
  locker_16029_regression: defaultDoorCountPresets,
}

function doorCountPresetsFor(capabilityId: string) {
  return doorCountPresetsByCapability[capabilityId] ?? defaultDoorCountPresets
}

function locker16029EqualRowHeight(rowsPerColumn: number) {
  return (
    LOCKER_16029_DOOR_AREA_HEIGHT_MM -
    LOCKER_16029_GRID_EDGE_GAP_MM * 2 -
    (rowsPerColumn - 1) * LOCKER_16029_DOOR_GAP_MM
  ) / rowsPerColumn
}

function formatMm(value?: number | null) {
  if (typeof value !== 'number' || Number.isNaN(value)) return '-'
  return Number.isInteger(value) ? value.toFixed(0) : value.toFixed(1)
}

function formatAbsMetric(metric?: { min?: number | null; max?: number | null } | null) {
  const min = metric?.min
  const max = metric?.max
  if (typeof min !== 'number' || Number.isNaN(min)) return '-'
  if (typeof max !== 'number' || Number.isNaN(max) || Math.abs(max - min) < 0.001) return `±${formatMm(min)}`
  return `±${formatMm(min)}-${formatMm(max)}`
}

function locker16029ExactUnitForHeight(height: number) {
  const unit = (height + LOCKER_16029_DOOR_GAP_MM) / LOCKER_16029_UNIT_HEIGHT_MM
  const rounded = Math.round(unit)
  return rounded >= 1 && rounded <= 6 && Math.abs(unit - rounded) < 0.01 ? rounded : null
}

function locker16029SolidWorksLayoutSummary(doorCountText?: string) {
  const doorCount = Number(doorCountText)
  if (!Number.isInteger(doorCount)) return null
  if (!LOCKER_16029_SUPPORTED_RULE_COUNTS.includes(doorCount)) return null
  const rowsPerColumn = doorCount / 2
  const doorHeight = locker16029EqualRowHeight(rowsPerColumn)
  const exactUnit = locker16029ExactUnitForHeight(doorHeight)
  const sourceMode =
    doorCount === 10
      ? '10门练习副本模板 / 2/10 门型'
      : doorCount === 12 && exactUnit
        ? `标准总装模板 / ${exactUnit}/12 门行`
        : 'FreeCAD 规则验证 / 派生门型'

  return {
    doorCount,
    rowsPerColumn,
    doorHeight,
    exactUnit,
    sourceMode,
    rowLabels: Array.from({ length: rowsPerColumn }, (_, index) => `${index + 1}: ${formatMm(doorHeight)} mm`),
  }
}

function supportedDoorCountsFor(capabilityId: string) {
  return new Set(doorCountPresetsFor(capabilityId).map(Number))
}

function defaultParameterValues(capabilityId: string, parameters: string[]) {
  return Object.fromEntries(parameters.map((parameter) => [parameter, sampleParameterValue(parameter, capabilityId)])) as ParameterValues
}

function parameterValue(parameters: ParameterValues, name: string, fallback: string) {
  const value = parameters[name]
  return value && value.trim() ? value.trim() : fallback
}

function lockedParameterValue(capabilityId: string, parameter: string) {
  if (capabilityId === 'locker_16029_door_panel' && parameter === 'category') return 'ordinary_door_panel'
  return null
}

function parameterIssueFor(capabilityId: string, parameters: ParameterValues) {
  if (capabilityId === 'locker_16029_door_panel') {
    const category = parameterValue(parameters, 'category', 'ordinary_door_panel')
    if (category !== 'ordinary_door_panel') {
      return '16029 单门板入口的 category 固定为 ordinary_door_panel；整柜门数请使用 16029 整柜入口。'
    }

    const doorWidth = Number(parameterValue(parameters, 'door_width', '437'))
    if (!Number.isFinite(doorWidth) || doorWidth <= 0) return 'door_width 必须是大于 0 的数值。'
    const doorHeight = Number(parameterValue(parameters, 'door_height', '298'))
    if (!Number.isFinite(doorHeight) || doorHeight <= 0) return 'door_height 必须是大于 0 的数值。'
    return null
  }

  if (capabilityId !== 'locker_16029_regression' && capabilityId !== 'locker_16038_variant_template') return null

  const rawDoorCount = parameterValue(parameters, 'door_count', '')
  const doorCount = Number(rawDoorCount)
  if (!Number.isInteger(doorCount)) return 'door_count 必须是整数。'
  if (doorCount < 2) return 'door_count 必须至少为 2。'
  if (capabilityId === 'locker_16029_regression') {
    if (doorCount % 2 !== 0) return '当前 16029 整柜脚本是左右两列布局，door_count 必须是偶数。'
    if (!supportedDoorCountsFor(capabilityId).has(doorCount)) {
      return '当前 16029 规则收敛样本先开放 10、12、14 门；其它门数先进入规则学习队列，暂不直接生成。'
    }
  }
  if (capabilityId === 'locker_16038_variant_template' && !supportedDoorCountsFor(capabilityId).has(doorCount)) {
    return '16038 模板证据生成当前支持 4、7、8 门整柜和 12/12 单门模块。'
  }
  return null
}

function runnerIssueFor(capabilityId: string, cadRunner: CadRunner, parameters: ParameterValues) {
  if (capabilityId === 'locker_16029_regression' && cadRunner === 'solidworks') {
    const doorCount = Number(parameterValue(parameters, 'door_count', '12'))
    if (!LOCKER_16029_SUPPORTED_SOLIDWORKS_COUNTS.includes(doorCount)) {
      return 'SolidWorks 16029 当前只开放 10/12/14 门原生参考模型；其它门数先走规则学习队列。'
    }
    const cabinetWidth = Number(parameterValue(parameters, 'cabinet_width', '1000'))
    if (!Number.isFinite(cabinetWidth) || Math.abs(cabinetWidth - 1000) > 0.001) {
      return 'SolidWorks 主线当前只开放 1000mm 宽外型；宽度派生请先走 FreeCAD/STEP 规则实验。'
    }
    const geometrySource = parameterValue(parameters, 'geometry_source', 'auto')
    if (geometrySource !== 'auto') {
      return 'SolidWorks 主线当前只使用 16029 已验证原生骨架源，geometry_source 请保持 auto。'
    }
  }
  if (capabilityId === 'locker_16029_regression' && cadRunner === 'freecad') {
    const doorCount = Number(parameterValue(parameters, 'door_count', '12'))
    if (!LOCKER_16029_SUPPORTED_FREECAD_COUNTS.includes(doorCount)) {
      return 'FreeCAD 16029 规则验证当前先开放 10/12/14 门。'
    }
    const cabinetWidth = Number(parameterValue(parameters, 'cabinet_width', '1000'))
    if (!Number.isFinite(cabinetWidth) || Math.abs(cabinetWidth - 1000) > 0.001) {
      return 'FreeCAD 16029 工程交接当前只开放 1000mm 宽外型；宽度派生先留在规则学习队列。'
    }
    const geometrySource = parameterValue(parameters, 'geometry_source', 'auto')
    if (geometrySource !== 'auto') {
      return 'FreeCAD 16029 工程交接当前只使用 auto 证据路线，避免实验几何进入交接包。'
    }
  }
  if (capabilityId === 'locker_16038_variant_template' && cadRunner === 'freecad') {
    const doorCount = Number(parameterValue(parameters, 'door_count', '7'))
    if (doorCount === 12) return '12/12 目前只有 SolidWorks 单门模块；FreeCAD/STEP 只支持 4/7/8 门整柜。'
  }
  return null
}

function parameterHintFor(capabilityId: string) {
  if (capabilityId === 'locker_16038_variant_template') {
    return '参数会写入任务 payload；16038 已绑定 4/7/8 门整柜模板和 12/12 单门模块证据，SolidWorks 为原生模板参考，FreeCAD 为 STEP 参考。'
  }
  if (capabilityId === 'locker_16029_regression') {
    return '16029 外型固定为 1000mm 宽、1917mm 高、550mm 深；10/12/14 门已开放原生 SolidWorks 增强矩阵样机 v2，FreeCAD 保留规则验证件。其它宽高/门数仍需先补规则证据。'
  }
  if (capabilityId === 'locker_16029_door_panel') {
    return '16029 单门板入口只生成 ordinary_door_panel；门数变化请走 16029 整柜入口，避免把 category 误填成 8/12。'
  }
  return '参数会写入任务 payload，并同步刷新 SolidWorks/FreeCAD 双轨命令预览；16029 当前按 FreeCAD 规则验证、SolidWorks 工程交接两条路线收敛。'
}

function parameterInputType(parameter: string) {
  return numericParameterNames.has(parameter) ? 'number' : 'text'
}

function numericParameterMin(parameter: string) {
  if (!numericParameterNames.has(parameter)) return undefined
  if (parameter === 'door_count') return 2
  if (parameter === 'flat_holes') return 0
  return 1
}

function numericParameterStep(parameter: string) {
  if (parameter === 'door_count') return 1
  return numericParameterNames.has(parameter) ? 1 : undefined
}

function commandArgumentsFor(capabilityId: string, cadRunner: CadRunner, parameters: ParameterValues) {
  if (cadRunner === 'solidworks') {
    if (capabilityId === 'locker_16029_door_panel') {
      return `%SOLIDWORKS_TEMPLATE% %TASK_OUTPUT_PART% %TASK_OUTPUT_STEP% ${parameterValue(
        parameters,
        'door_height',
        '298',
      )} %TASK_RESULT_JSON%`
    }
    if (capabilityId === 'locker_16038_variant_template') {
      return `%TASK_OUTPUT_DIR% %TASK_ID% ${parameterValue(parameters, 'door_count', '7')}`
    }
    return `%TASK_OUTPUT_DIR% %TASK_ID% %SOLIDWORKS_16029_TEMPLATE% ${parameterValue(
      parameters,
      'door_count',
      '12',
    )}`
  }

  if (capabilityId === 'locker_16038_variant_template') {
    return `--door-count ${parameterValue(parameters, 'door_count', '7')}`
  }
  if (capabilityId === 'locker_16029_regression') {
    return `--door-count ${parameterValue(parameters, 'door_count', '12')} --cabinet-width ${parameterValue(
      parameters,
      'cabinet_width',
      '1000',
    )} --geometry-source ${parameterValue(parameters, 'geometry_source', 'auto')}`
  }
  if (capabilityId === 'locker_16029_door_panel') {
    return `--category ${parameterValue(parameters, 'category', 'ordinary_door_panel')} --door-width ${parameterValue(
      parameters,
      'door_width',
      '437',
    )} --door-height ${parameterValue(parameters, 'door_height', '298')} --geometry-source ${parameterValue(
      parameters,
      'geometry_source',
      'auto',
    )}`
  }
  if (capabilityId === 'outdoor_waterproof_door_1_12_r') {
    return `--door-fraction ${parameterValue(parameters, 'door_fraction', '1/12')} --side ${parameterValue(
      parameters,
      'side',
      'R',
    )} --formed-bbox-x ${parameterValue(parameters, 'formed_bbox_x', 'from_STEP')} --formed-bbox-y ${parameterValue(
      parameters,
      'formed_bbox_y',
      'from_STEP',
    )} --formed-bbox-z ${parameterValue(parameters, 'formed_bbox_z', 'from_STEP')} --flat-holes ${parameterValue(
      parameters,
      'flat_holes',
      '11',
    )}`
  }
  return ''
}

function StatusDot({ status }: { status: 'pass' | 'partial' | 'blocked' | 'waiting' }) {
  return <span className={`status-dot status-${status}`} title={status} />
}

function ProgressDial({ value }: { value: number }) {
  return (
    <div className="progress-dial" style={{ '--value': `${value * 3.6}deg` } as React.CSSProperties}>
      <strong>{value}</strong>
      <span>/100</span>
    </div>
  )
}

function DetailLine({ label, value, mono }: { label: string; value: string; mono?: boolean }) {
  return (
    <div className="detail-line">
      <span>{label}</span>
      <strong className={mono ? 'mono' : ''}>{value}</strong>
    </div>
  )
}

function formatTaskTime(value: string) {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  return date.toLocaleString('zh-CN', { hour12: false })
}

function outputFileLabel(path: string) {
  const lower = path.toLowerCase()
  if (lower.endsWith('.sldasm')) return 'SolidWorks 原生装配（已清理参考显示）'
  if (lower.endsWith('.sldprt')) return 'SolidWorks 原生零件'
  if (lower.endsWith('_solidworks_validation_report.md')) return 'SolidWorks 生成验证报告'
  if (lower.endsWith('_solidworks_quality_report.md')) return 'SolidWorks 质量诊断报告'
  if (lower.endsWith('_solidworks_quality.json')) return 'SolidWorks 质量诊断数据'
  if (lower.endsWith('_solidworks_validation.csv')) return 'SolidWorks 生成验证数据'
  if (lower.endsWith('_solidworks_import.stp')) return 'SolidWorks 手动导入 STEP'
  if (lower.endsWith('locker_16029_freecad_postprocess.json')) return 'FreeCAD 生成后自动复核报告'
  if (lower.endsWith('freecad_geometry_check.json')) return 'FreeCAD STEP 几何检查'
  if (lower.endsWith('_geometry_integrity.md')) return 'FreeCAD FCStd 完整性检查'
  if (lower.endsWith('.step') || lower.endsWith('.stp')) return '中性 STEP 几何'
  if (lower.endsWith('.fcstd')) return 'FreeCAD 原生文件'
  if (lower.endsWith('.ps1')) return 'SolidWorks 手动 worker 脚本'
  if (lower.endsWith('.cmd')) return 'CAD 启动辅助脚本'
  if (lower.endsWith('.py')) return 'CAD 启动脚本'
  if (lower.endsWith('.md')) return '生成/打开说明'
  if (lower.endsWith('.csv')) return '验证数据'
  return '输出文件'
}

function sampleParameterValue(parameter: string, capabilityId?: string) {
  if (parameter === 'category') return 'ordinary_door_panel'
  if (parameter === 'geometry_source') return 'auto'
  if (parameter === 'cabinet_width') return '1000'
  if (parameter === 'door_width') return '437'
  if (parameter === 'door_height') return '298'
  if (parameter === 'formed_bbox_x' || parameter === 'formed_bbox_y' || parameter === 'formed_bbox_z') return 'from_STEP'
  if (parameter === 'flat_holes') return '11'
  if (parameter.includes('door_count') && capabilityId === 'locker_16038_variant_template') return '7'
  if (parameter.includes('door_count')) return '12'
  if (parameter.includes('width')) return '1000'
  if (parameter.includes('height')) return '1939'
  if (parameter.includes('depth')) return '550'
  if (parameter.includes('fraction')) return '1/12'
  if (parameter.includes('side')) return 'R'
  if (parameter.includes('bbox')) return 'from STEP'
  return 'pending'
}

export default App
