import {
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
  Menu,
  Play,
  Search,
  Settings2,
  ShieldCheck,
  Wrench,
  X,
} from 'lucide-react'
import { useCallback, useEffect, useMemo, useState } from 'react'
import './App.css'
import {
  capabilities,
  maturityDistribution,
  metricCards,
  pipelineRows,
  projects,
  reviewItems,
  ruleLearningAxes,
  ruleFamilies,
  sourcePaths,
  templateAssets,
  type Evidence,
  type Maturity,
  type Project,
  type ReviewItem,
  type RuleLearningAxis,
  type RuleFamily,
  type TemplateAsset,
} from './data/studioData'

type PageId = 'overview' | 'intake' | 'rules' | 'models' | 'review'
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
  message: string
}

type LocalActionResult = {
  status: string
  path: string
  message: string
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
const DEFAULT_MODEL_CAPABILITY_ID = 'locker_16038_variant_template'
const LOCKER_16038_RULE_BINDING_ID = 'STEP-VARIANT-16038-4-7-8-12'

const cadRunners: Array<{
  id: CadRunner
  label: string
  detail: string
  shortcut: string
}> = [
  {
    id: 'solidworks',
    label: 'SOLIDWORKS 2025',
    detail: '当前工程主线 / SLDASM 原生输出',
    shortcut: SOLIDWORKS_SHORTCUT,
  },
  {
    id: 'freecad',
    label: 'FreeCAD 1.1.1',
    detail: '开源替代路线 / FCStd 与 STEP 输出',
    shortcut: FREECAD_SHORTCUT,
  },
]

const pages: Array<{ id: PageId; label: string; description: string; icon: typeof Gauge }> = [
  { id: 'overview', label: '项目总览', description: '项目健康度、证据覆盖和风险入口', icon: Gauge },
  { id: 'intake', label: '数据录入状态', description: 'BOM / DXF / SolidWorks / STEP / FreeCAD 迁移流水线', icon: Database },
  { id: 'rules', label: '规则库成熟度', description: '模块规则、来源证据和成熟度分布', icon: ShieldCheck },
  { id: 'models', label: '可生成模型', description: 'SolidWorks 主线 / FreeCAD 开源替代路线', icon: Boxes },
  { id: 'review', label: '待确认项', description: 'P0/P1/P2 队列和规则定标事项', icon: FileWarning },
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
          {pages.map((page) => {
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
                <span>{page.label}</span>
              </button>
            )
          })}
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
      setExtractionRuns((runs) => [run, ...runs.filter((item) => item.id !== run.id)])
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
  const activeTaskCount = prioritizedGenerationTasks.filter((task) => task.capability_id === activeCapability.id).length
  const exactTaskCount = prioritizedGenerationTasks.filter(
    (task) => task.capability_id === activeCapability.id && taskMatchesActiveParameters(task, normalizedParameterValues),
  ).length
  const activeRuleBinding = useMemo(() => {
    if (activeCapability.id !== DEFAULT_MODEL_CAPABILITY_ID) return null
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
            {activeCapability.id === DEFAULT_MODEL_CAPABILITY_ID && (
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
              <span>按所选 CAD 生成器预期命令</span>
              {cadRunners.map((runner) => (
                <code key={runner.id}>
                  {activeCapability.generator === 'not_enabled'
                    ? `${runner.label}: not_enabled: 需要先完成证据修复或模块接口确认`
                    : runnerIssueById[runner.id]
                      ? `${runner.label}: 当前参数不支持该入口 - ${runnerIssueById[runner.id]}`
                    : `${runner.label}: ${commandPreview(runner.id)}`}
                </code>
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
                    onClick={() => (runner.id === 'solidworks' ? createAndRunSolidWorksTask() : createGenerationTask(runner.id))}
                  >
                    <Play size={17} />
                    <span>{solidWorksBusy ? '正在启动 SOLIDWORKS' : runnerButtonLabel(runner.id)}</span>
                    <small>{runnerIssue ?? runnerButtonDetail(runner.id, runner.detail)}</small>
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
              <p>当前模型和当前门数的任务会排在前面；SolidWorks 任务可以直接启动生成，也可以打开历史输出。</p>
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
              <span>当前门数</span>
              <strong>{normalizedParameterValues.door_count ?? '-'}</strong>
            </div>
          </div>
          <div className="task-list">
            {generationTasks.length === 0 ? (
              <div className="empty-state">还没有生成任务。请选择可生成模型后创建任务草稿。</div>
            ) : (
              prioritizedGenerationTasks.map((task) => (
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
  const hasFcstdModel = Boolean(execution?.outputs.some((path) => lowerOutput(path).endsWith('.fcstd')))
  const hasNativeSolidWorksOutput = Boolean(solidworksAssemblyFile || solidworksPartFile)
  const solidworksRunSummary = execution?.solidworks_run_summary ?? null
  const solidworksQualityStatus = execution?.solidworks_quality_status ?? solidworksRunSummary?.quality_status ?? null
  const solidworksQualitySummary = execution?.solidworks_quality_summary ?? solidworksRunSummary?.quality_summary ?? null
  const hasReferenceOnlyQuality = solidworksQualityStatus === 'reference_feature_only'

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
                  disabled={dryRunBusy || executeBusy}
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
                    disabled={solidWorksRunBusy || dryRunBusy || !canRunSolidWorksPackageStatus(task.status)}
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
                    <strong>{check.name}</strong>
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
                      <div data-state={solidworksQualityStatus === 'component_tree' ? 'ready' : hasReferenceOnlyQuality ? 'warning' : 'waiting'}>
                        <span>组件树质量</span>
                        <strong>
                          {solidworksQualityStatus === 'component_tree'
                            ? '可遍历'
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

function generationRouteKind(capabilityId: string) {
  if (capabilityId === 'locker_16029_regression') return 'direct'
  if (capabilityId === 'locker_16038_variant_template') return 'template'
  if (capabilityId === 'locker_16029_door_panel') return 'part'
  return 'queued'
}

function generationRouteTitle(capabilityId: string) {
  if (capabilityId === 'locker_16029_regression') return 'SolidWorks 按规则逐件装配'
  if (capabilityId === 'locker_16038_variant_template') return 'SolidWorks 标准模板克隆 / 证据绑定'
  if (capabilityId === 'locker_16029_door_panel') return 'SolidWorks 单门板参数化零件'
  return '规则学习中，暂不进入生成器'
}

function generationRouteDetail(capabilityId: string) {
  if (capabilityId === 'locker_16029_regression') {
    return '点击 SolidWorks 后会按门数插入门框、柜体、层板和门装配，并生成组件清单、验证 CSV 与报告。'
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
  if (mode === 'template_clone') return '标准总装模板克隆'
  if (mode === 'direct_component_assembly') return '按规则逐件装配'
  if (mode === 'manual_package') return '待本地 SolidWorks 执行'
  return '待判定'
}

function solidworksNullableCount(value: number | null | undefined) {
  return typeof value === 'number' ? String(value) : '-'
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
  if (summary.quality_status === 'component_tree') {
    return `${solidworksNullableCount(summary.all_component_count)} components`
  }
  if (typeof summary.all_component_count === 'number') return `${summary.all_component_count} components`
  return '-'
}

function cadRunnerLabel(cadRunner: CadRunner) {
  return cadRunner === 'solidworks' ? 'SOLIDWORKS 2025' : 'FreeCAD 1.1.1'
}

function runnerButtonLabel(cadRunner: CadRunner) {
  return cadRunner === 'solidworks' ? '一键运行 SOLIDWORKS 2025' : '创建 FreeCAD 任务草稿'
}

function runnerButtonDetail(cadRunner: CadRunner, fallback: string) {
  return cadRunner === 'solidworks' ? '创建任务、自动 dry-run，通过后直接启动 SolidWorks。' : fallback
}

function taskMatchesActiveParameters(task: GenerationTask, activeParameters: ParameterValues) {
  const activeEntries = Object.entries(activeParameters)
  if (!activeEntries.length) return false
  return activeEntries.every(([name, value]) => String(task.parameters[name] ?? '') === String(value))
}

function taskPriorityScore(task: GenerationTask, activeCapabilityId: string, activeParameters: ParameterValues) {
  let score = 0
  if (task.capability_id === activeCapabilityId) score += 1000
  if (task.capability_id === activeCapabilityId && taskMatchesActiveParameters(task, activeParameters)) score += 260
  if (task.cad_runner === 'solidworks') score += 60
  if (task.status === 'ready_to_run' || task.status === 'requires_manual_run') score += 45
  if (task.status === 'completed_reference') score += 30
  if (task.status === 'running') score += 20
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

  if (task.status === 'completed_reference') {
    const cleanedSolidWorksAssembly =
      task.cad_runner === 'solidworks' && Boolean(recommendedFile?.toLowerCase().endsWith('.sldasm'))
    const referenceFeatureOnly = task.execution_result?.solidworks_quality_status === 'reference_feature_only'
    let detail = '工程参考模型已写入输出目录。'
    if (referenceFeatureOnly) {
      detail =
        task.execution_result?.solidworks_quality_summary ??
        'SolidWorks 装配已生成并清理显示，但质量诊断显示它是 Reference 特征模型，只能作为视觉/摆放工程参考。'
    } else if (cleanedSolidWorksAssembly && recommendedFile) {
      detail = `SolidWorks 原生装配已生成，并已清理参考面/草图显示；验证报告已同步生成，建议直接打开 ${outputFileLabel(
        recommendedFile,
      )}。`
    } else if (recommendedFile) {
      detail = `工程参考模型已写入输出目录，建议打开：${outputFileLabel(recommendedFile)}。`
    }
    return {
      taskId: task.id,
      tone: referenceFeatureOnly ? 'warn' : 'good',
      title: referenceFeatureOnly ? '生成完成但仅限工程参考' : '生成完成',
      detail,
      cadRunner: task.cad_runner,
      outputDir,
      recommendedFile,
      validationReport,
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
  return '打开推荐文件'
}

function solidworksScriptFor(capabilityId: string) {
  if (capabilityId === 'locker_16029_door_panel') return 'scripts\\sw_make_parametric_door_panel.js'
  if (capabilityId === 'locker_16038_variant_template') return 'scripts\\sw_clone_16038_variant_template.js'
  return 'scripts\\sw_build_locker_16029_direct_assembly.js'
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
const defaultDoorCountPresets = ['8', '12', '14', '16', '18']
const doorCountPresetsByCapability: Record<string, string[]> = {
  locker_16038_variant_template: ['4', '7', '8', '12'],
  locker_16029_regression: defaultDoorCountPresets,
}

function doorCountPresetsFor(capabilityId: string) {
  return doorCountPresetsByCapability[capabilityId] ?? defaultDoorCountPresets
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
    if (!supportedDoorCountsFor(capabilityId).has(doorCount)) return '当前 16029 MVP 已验证门数为 8、12、14、16、18。'
  }
  if (capabilityId === 'locker_16038_variant_template' && !supportedDoorCountsFor(capabilityId).has(doorCount)) {
    return '16038 模板证据生成当前支持 4、7、8 门整柜和 12/12 单门模块。'
  }
  return null
}

function runnerIssueFor(capabilityId: string, cadRunner: CadRunner, parameters: ParameterValues) {
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
  if (capabilityId === 'locker_16029_door_panel') {
    return '16029 单门板入口只生成 ordinary_door_panel；门数变化请走 16029 整柜入口，避免把 category 误填成 8/12。'
  }
  return '参数会写入任务 payload，并同步刷新 SolidWorks/FreeCAD 双轨命令预览；16029 整柜脚本已验证 8/12/14/16/18 门。'
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
    return `%TASK_OUTPUT_DIR% %TASK_ID% C:\\sw16029_direct_18door\\source ${parameterValue(parameters, 'door_count', '18')}`
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
  if (lower.endsWith('_solidworks_import.stp')) return 'SolidWorks 2025 导入文件'
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
