import { generatedSnapshot } from './generatedSnapshot'

export type Maturity =
  | 'raw_imported'
  | 'mapped'
  | 'dxf_parsed'
  | 'solidworks_extracted'
  | 'step_bbox_measured'
  | 'engineering_reference'
  | 'production_candidate'
  | 'manual_review_required'
  | 'blocked'

export type Evidence = 'DXF' | 'STEP' | 'SolidWorks' | 'FreeCAD' | 'BOM' | '工程图' | '证据闭环记录'

export type Project = {
  id: string
  name: string
  productType: string
  status: string
  statusTone: 'good' | 'warn' | 'risk' | 'idle'
  sourceRoot: string
  modelStatus: string
  progress: number
  capability: string
  risk: string
  updatedAt: string
  stats: Array<{ label: string; value: string; tone?: 'good' | 'warn' | 'risk' | 'idle' }>
  notes: string[]
}

export type PipelineRow = {
  stage: string
  owner: string
  locker16029: string
  outdoor: string
  next: string
  evidence: Evidence[]
  risk: string
  status: 'pass' | 'partial' | 'blocked' | 'waiting'
}

export type RuleFamily = {
  family: string
  module: string
  rows: number
  maturity: Maturity
  evidence: Evidence[]
  status: string
  nextAction: string
}

export type TemplateAsset = {
  id: string
  title: string
  productType: string
  size: string
  variants: string
  sourceAssemblyPath: string
  bomPath: string
  dxfPath: string
  stepOrPdfPath: string
  ruleLearningValue: '高' | '中高' | '中' | '低'
  ruleFocus: string[]
  evidence: Evidence[]
  status: 'ready_for_rule_learning' | 'needs_open_check' | 'empty_source'
}

export type RuleLearningAxis = {
  axis: string
  purpose: string
  sourceTemplates: string
  currentState: string
  nextAction: string
  maturity: Maturity
  evidence: Evidence[]
}

export type Capability = {
  id: string
  title: string
  productType: string
  module: string
  variants: string
  status: 'generatable' | 'reference_only' | 'blocked' | 'queued'
  maturity: Maturity
  generator: string
  validation: string
  parameters: string[]
  evidence: Evidence[]
  limitation: string
}

export type ReviewItem = {
  id: string
  project: string
  module: string
  priority: 'P0' | 'P1' | 'P2' | 'P3'
  severity: '阻塞' | '严重' | '一般' | '观察'
  issue: string
  nextAction: string
  evidence: Evidence[]
  status: 'open' | 'watching' | 'waiting_source'
}

export type DrawingSheetMetalSource = {
  id: string
  title: string
  sourceType: string
  firstTarget: string
  currentState: string
  nextAction: string
  evidence: Evidence[]
  status: 'ready_for_intake' | 'candidate' | 'planned' | 'blocked'
}

export type DrawingSheetMetalOutput = {
  id: string
  title: string
  level: string
  description: string
  boundary: string
  statusTone: 'good' | 'warn' | 'risk' | 'idle'
}

export type DrawingSheetMetalRisk = {
  item: string
  requiredInput: string
  reason: string
  owner: string
}

export type DrawingSheetMetalRoadmapStep = {
  step: string
  focus: string
  deliverable: string
  relationToMainline: string
  statusTone: 'good' | 'warn' | 'risk' | 'idle'
}

export type DrawingSheetMetalBatchRun = {
  runId: string
  sourceRoot: string
  outputDir: string
  summaryPath: string
  fileCount: number
  errorCount: number
  ruleSeedCandidateCount: number
  qualityStatusCounts: Array<{ label: string; value: number; tone: 'good' | 'warn' | 'risk' | 'idle' }>
  roleCounts: Array<{ label: string; value: number }>
  keyFindings: string[]
  nextActions: string[]
}

const snapshot = generatedSnapshot
const outdoorSnapshot = snapshot.projects.outdoorCourier

export const assetSnapshot = snapshot

export const sourcePaths = {
  appRoot: snapshot.sourcePaths.appRoot,
  cadWorkspace: snapshot.sourcePaths.cadWorkspace,
  startupBrief: 'C:\\Users\\Administrator\\Desktop\\Winnsen_Structure_Agent_Studio_项目启动说明.md',
  parametricTemplateRoot: 'C:\\Users\\Administrator\\Desktop\\参数化模板素材',
  drawingSheetMetalWorkspace: 'D:\\Winnsen_Structure_Agent_Studio\\workers\\drawing_sheetmetal',
  drawingSheetMetalFirstRun:
    'D:\\Winnsen_Structure_Agent_Studio\\workers\\drawing_sheetmetal\\runs\\DXF-16029-DOOR-PANEL-1-12-20260519',
  drawingSheetMetalBatchRun:
    'D:\\Winnsen_Structure_Agent_Studio\\workers\\drawing_sheetmetal\\runs\\BATCH-16029-SHEETMETAL-20260519',
  intakeStatus: snapshot.sourcePaths.intakeStatus,
  outdoorStatus: snapshot.sourcePaths.outdoorStatus,
  strictQueue: snapshot.sourcePaths.strictQueueMd,
  outdoorGate: snapshot.sourcePaths.outdoorGateMd,
  solidworks16029Handoff: 'D:\\Winnsen_Structure_Agent_Studio\\data\\solidworks_16029_engineering_handoff.md',
  solidworks16029RoleRules: 'D:\\Winnsen_Structure_Agent_Studio\\data\\solidworks_16029_role_rules.md',
  solidworks16029TenDoorRecipe: 'D:\\Winnsen_Structure_Agent_Studio\\data\\solidworks_16029_10door_mutator_recipe.md',
}

export const projects: Project[] = [
  {
    id: 'parametric_template_library',
    name: '参数化模板素材库',
    productType: '寄存柜 / 主控柜 / 同尺寸变体',
    status: '新素材已盘点，进入规则学习而非直接复制生成',
    statusTone: 'warn',
    sourceRoot: 'C:\\Users\\Administrator\\Desktop\\参数化模板素材',
    modelStatus: '9 个项目根目录，优先学习 16038、16029、16028、25072、23054 的钣金规则',
    progress: 38,
    capability: '同尺寸门数组合、柜深变化、主控柜配置、DXF/BOM/SLDASM 证据链',
    risk: '尚未抽取完整装配矩阵、门框/层板/锁位规则，不能直接承诺生产级模型',
    updatedAt: '2026-05-16 23:18',
    stats: [
      { label: 'Project roots', value: '9', tone: 'good' },
      { label: 'SLDASM', value: '559', tone: 'good' },
      { label: 'SLDPRT', value: '1,797', tone: 'good' },
      { label: 'DXF', value: '793', tone: 'good' },
      { label: 'BOM XLSX', value: '28', tone: 'warn' },
    ],
    notes: [
      '16038 具备 1917x1000x550 的 4/7/8/12 门对比证据，是同尺寸不同门数规则学习优先级最高的样本。',
      '16029 具备 550 深标准柜和历史订单变体线索；16028/16029 组合可学习 485/550 柜深变化。',
      '25072/23054 适合学习控制主柜、操作区、电气区、不锈钢及海外订单配置规则。',
    ],
  },
  {
    id: 'locker_16029_baseline_v1',
    name: '16029 800W gold-variable 双方案',
    productType: '标准寄存柜 / LMS-SML 双方案',
    status: 'LMS 与 SML 已收敛为当前审核包；CAD 主线为 SolidWorks 2020，打开截图证据已补齐',
    statusTone: 'good',
    sourceRoot: 'D:\\Winnsen_Structure_Agent_Studio\\workers\\handoffs',
    modelStatus: 'LMS: 大 6/12、中 4/12、小 2/12；SML: 小 2/12、中 4/12、大 6/12；统一规则 800W×1917H×550D / W337 / 内部间隙 2+3+2=7',
    progress: 86,
    capability: 'gold/source STEP、层板焊件、前框 STEP、验证过的五金模板、verify CSV、model gate、bbox gate、handoff zip、SolidWorks 2020 打开截图',
    risk: '当前 scope gate = PASS：LMS/SML 已有 SolidWorks 2020 打开截图和 JSON 证据；仍需结构工程师做门序、五金和生产图纸签核',
    updatedAt: '2026-05-28 12:43',
    stats: [
      { label: 'LMS', value: '6 / 12', tone: 'good' },
      { label: 'SML', value: '6 / 12', tone: 'good' },
      { label: 'Outer size', value: '800×1917×550', tone: 'good' },
      { label: 'W337', value: 'locked', tone: 'warn' },
      { label: 'CAD', value: 'SW2020', tone: 'good' },
      { label: 'Gate', value: 'PASS', tone: 'good' },
    ],
    notes: [
      '当前只保留 gold/source STEP、层板焊件、前框 STEP、验证过的五金模板，不再把旧的宽度试错线放进主界面。',
      'SolidWorks 当前主线按 2020 环境执行，旧 CAD 环境线索不写入当前工程师交付口径。',
      'LMS / SML 双方案分别按大-中-小与小-中-大排布，工程师只需要看当前候选审核包。',
      '统一边界是 800W × 1917H × 550D，门宽 W337，内部间隙 2+3+2=7。',
      'LMS/SML 的 SolidWorks 2020 打开截图和 JSON 证据已补齐；当前仍是工程审核包，不是生产图纸释放。',
    ],
  },
  {
    id: 'outdoor_courier_family',
    name: '户外快递柜系列',
    productType: '户外副柜 / 主柜 / 雨棚定制版',
    status: 'Stage 2 完成，STEP bbox gate 已建立',
    statusTone: 'warn',
    sourceRoot: 'C:\\Users\\Administrator\\Desktop\\户外柜参数化模板素材',
    modelStatus: '单件工程参考模型已跑通，整柜未进入生产级生成',
    progress: 68,
    capability: '1/12 R 户外防水柜门单件 FreeCAD 参考模型',
    risk: '32 条 P0 STEP 导出阻塞，另有语义/拓扑升级门槛',
    updatedAt: '2026-05-15 22:41',
    stats: [
      { label: 'Projects', value: `${outdoorSnapshot.activeProjectCount}`, tone: 'good' },
      { label: 'DXF parsed', value: `${outdoorSnapshot.dxfParsed}/${outdoorSnapshot.dxfTotal}`, tone: 'good' },
      { label: 'BOM mapped', value: `${outdoorSnapshot.bomMapped}/${outdoorSnapshot.bomRows}`, tone: 'good' },
      { label: 'Measured STEP rows', value: `${outdoorSnapshot.formedBboxMeasuredRows}`, tone: 'warn' },
      { label: 'P0 blockers', value: `${outdoorSnapshot.p0Blockers}`, tone: 'risk' },
    ],
    notes: [
      '22057、23020、24067 QTRAK、26015 不锈钢雨棚版已接入同一 intake pipeline.',
      '当前 formed bbox 可用于工程参考生成，还不能直接标为生产图纸。',
    ],
  },
  {
    id: 'main_control_cabinet_standard_v1',
    name: '未来主控柜',
    productType: '主控柜 / 操作区设备',
    status: '等待 source root',
    statusTone: 'idle',
    sourceRoot: 'outputs\\intake\\next_equipment_project_registry.csv',
    modelStatus: '未入库',
    progress: 8,
    capability: '预留 operation-panel / electronics intake 模块',
    risk: '需要标准装配包、BOM、DXF、STEP 或工程图来源',
    updatedAt: '待接入',
    stats: [
      { label: 'Registered', value: 'yes', tone: 'idle' },
      { label: 'Files indexed', value: '0', tone: 'idle' },
      { label: 'Rules', value: '0', tone: 'idle' },
    ],
    notes: ['接入后先走同一套 intake pipeline，不直接手工建模。'],
  },
  {
    id: 'vending_machine_standard_v1',
    name: '未来贩卖机',
    productType: '贩卖机 / 自助设备',
    status: '等待 source root',
    statusTone: 'idle',
    sourceRoot: 'outputs\\intake\\next_equipment_project_registry.csv',
    modelStatus: '未入库',
    progress: 6,
    capability: '预留货道、出货机构、传动、维护门模块',
    risk: '机电接口、传动、传感器和维修路径需要单独任务书',
    updatedAt: '待接入',
    stats: [
      { label: 'Registered', value: 'yes', tone: 'idle' },
      { label: 'Files indexed', value: '0', tone: 'idle' },
      { label: 'Rules', value: '0', tone: 'idle' },
    ],
    notes: ['后续应区分货道机构规则与普通钣金柜体规则。'],
  },
]

export const pipelineRows: PipelineRow[] = [
  {
    stage: '文件扫描',
    owner: 'intake',
    locker16029: '16029 已有 1,385 文件索引；新增模板库 9 项目/559 装配待入库',
    outdoor: '4 套项目全部索引',
    next: '先把 C:\\Users\\Administrator\\Desktop\\参数化模板素材 写入规则学习索引',
    evidence: ['SolidWorks', 'DXF', 'BOM', '工程图'],
    risk: '新增总装配需先分清规则样本、订单变体、包装/作废/备份文件',
    status: 'pass',
  },
  {
    stage: 'BOM 映射',
    owner: 'intake',
    locker16029: '134/134 mapped',
    outdoor: '611/611 mapped',
    next: '等待输入',
    evidence: ['BOM', 'SolidWorks', '工程图'],
    risk: '外购件/标准件仍需角色确认',
    status: 'pass',
  },
  {
    stage: 'DXF 展开解析',
    owner: 'parser',
    locker16029: '142/142 parsed',
    outdoor: '283/283 parsed',
    next: '等待输入',
    evidence: ['DXF'],
    risk: 'DXF flat extents 不能直接当 formed bbox',
    status: 'pass',
  },
  {
    stage: 'SolidWorks API 抽取',
    owner: 'worker',
    locker16029: '206 evidence rows',
    outdoor: 'targeted probes passed',
    next: '按模块补队列',
    evidence: ['SolidWorks'],
    risk: '当前 COM/JScript 路径缺少直接 bbox；SolidWorks 原生验证优先，STEP/FreeCAD 仅作为迁移路线的中性几何补充',
    status: 'partial',
  },
  {
    stage: 'STEP 导出',
    owner: 'worker',
    locker16029: '67 SW API module rows bbox trace passed',
    outdoor: '67 exported/existing, 10 failed',
    next: '修复户外左门/锁钩/导水槽导出',
    evidence: ['STEP', 'SolidWorks'],
    risk: '户外柜 32 条 P0 导出阻塞影响生产候选升级',
    status: 'blocked',
  },
  {
    stage: 'FreeCAD 开源迁移验证',
    owner: 'migration',
    locker16029: 'latest regression PASS',
    outdoor: '67 valid STEP measured',
    next: '保持与 SolidWorks/STEP 证据并行，不替代当前 SolidWorks 工程验证',
    evidence: ['FreeCAD', 'STEP'],
    risk: '户外单门 bbox PASS 但拓扑未复刻；迁移路线不得反向覆盖 SolidWorks 主线结论',
    status: 'partial',
  },
  {
    stage: '规则沉淀',
    owner: 'rules',
    locker16029: 'param rules v6 + strict queue；新增 16038/16028/25072 对比轴',
    outdoor: '25 family rule candidates',
    next: '建立门数变化、柜深变化、主控柜配置变化三条学习轴',
    evidence: ['DXF', 'STEP', 'SolidWorks', 'FreeCAD', 'BOM'],
    risk: '先抽规则再生成；不能把单个总装配复制当作参数化能力',
    status: 'partial',
  },
  {
    stage: '可生成模型',
    owner: 'generator',
    locker16029: '800W gold-variable LMS/SML review packages gated; older same-size model work retained as legacy evidence',
    outdoor: '1/12 R waterproof door reference',
    next: '继续围绕 LMS/SML 审核包补结构签核、门序、五金计数和生产图纸边界',
    evidence: ['SolidWorks', 'FreeCAD', 'STEP', 'DXF'],
    risk: '骨架已可工程参考，但仍缺后侧/电气/出图交付模块，不能标为生产图纸',
    status: 'partial',
  },
]

export const maturityDistribution: Array<{ maturity: Maturity; count: number; tone: string }> = [
  { maturity: 'raw_imported', count: 199, tone: 'idle' },
  { maturity: 'mapped', count: 745, tone: 'info' },
  { maturity: 'dxf_parsed', count: 425, tone: 'info' },
  { maturity: 'solidworks_extracted', count: 206, tone: 'info' },
  { maturity: 'step_bbox_measured', count: 131, tone: 'warn' },
  { maturity: 'engineering_reference', count: 5, tone: 'good' },
  { maturity: 'production_candidate', count: 0, tone: 'risk' },
  { maturity: 'manual_review_required', count: 79, tone: 'risk' },
]

export const ruleFamilies: RuleFamily[] = [
  {
    family: 'same_size_door_count_variant_layout',
    module: 'cabinet_body / door_frame / shelves / doors',
    rows: 13,
    maturity: 'mapped',
    evidence: ['SolidWorks', 'DXF', 'BOM', '工程图'],
    status: '16038 and 16029 variant assemblies identified',
    nextAction: 'Extract top-level component transforms, door row heights, shelf pitch, lock side, and frame divider rules before generation.',
  },
  {
    family: 'cabinet_depth_variant_485_550',
    module: 'cabinet_body / base / top_cover / side_panels',
    rows: 2,
    maturity: 'mapped',
    evidence: ['SolidWorks', 'DXF', 'BOM'],
    status: '16028 485-deep and 16029 550-deep standard lockers paired',
    nextAction: 'Compare side panels, base, top cover, shelf depth, and rear service parts to isolate depth-dependent sheet-metal rules.',
  },
  {
    family: 'main_control_cabinet_configuration',
    module: 'operation_panel / electronics_mount / control_cabinet_body',
    rows: 5,
    maturity: 'raw_imported',
    evidence: ['SolidWorks', 'DXF', 'BOM', '工程图'],
    status: '23054, 24030, 25072 control-cabinet templates discovered',
    nextAction: 'Separate operation area, electronics tray, wiring/service access, and cabinet shell rules from ordinary locker-door rules.',
  },
  {
    family: 'standard_locker_door_panel',
    module: 'ordinary_door',
    rows: 530,
    maturity: 'engineering_reference',
    evidence: ['DXF', 'STEP', 'SolidWorks', 'FreeCAD'],
    status: '16029 regression gate PASS',
    nextAction: 'Historical single-panel and width-candidate references are legacy evidence only; current handoff uses W337 gold-variable packages and still requires structural sign-off before production release.',
  },
  {
    family: 'door_bend_semantic_feature',
    module: 'ordinary_door',
    rows: 208,
    maturity: 'engineering_reference',
    evidence: ['FreeCAD', 'STEP'],
    status: 'R1 cylindrical faces traced on folded body',
    nextAction: 'Keep as verified reference; do not infer new visible cuts without drawing evidence.',
  },
  {
    family: 'lock_center_relation',
    module: 'lock_system',
    rows: 52,
    maturity: 'engineering_reference',
    evidence: ['SolidWorks', 'STEP', 'FreeCAD'],
    status: 'Relation datum only, hardware centers verified',
    nextAction: 'Block from visible cut generation unless source evidence defines cut shape.',
  },
  {
    family: 'waterproof_door_panel',
    module: 'ordinary_door',
    rows: 72,
    maturity: 'step_bbox_measured',
    evidence: ['DXF', 'STEP', 'SolidWorks', 'FreeCAD'],
    status: '52 measured rows, 20 export blockers',
    nextAction: 'Repair left-side STEP exports and add bend/hole/placement semantic validation.',
  },
  {
    family: 'u_lock_hook_pad',
    module: 'lock_system',
    rows: 9,
    maturity: 'manual_review_required',
    evidence: ['SolidWorks', 'BOM'],
    status: '8 export blockers, 1 measured semantic gate',
    nextAction: 'Repair OpenDoc/STEP export or approve sibling project evidence.',
  },
  {
    family: 'water_guide_channel',
    module: 'rainproof_structure',
    rows: 12,
    maturity: 'manual_review_required',
    evidence: ['SolidWorks', 'DXF'],
    status: '4 export blockers; 26015 导水槽L failed OpenDoc twice',
    nextAction: 'Review source file or substitute verified sibling evidence before rule promotion.',
  },
  {
    family: 'display_recess_or_mount',
    module: 'operation_panel',
    rows: 7,
    maturity: 'step_bbox_measured',
    evidence: ['STEP', 'SolidWorks', 'FreeCAD'],
    status: 'QTRAK operation panel evidence separated from auxiliary rules',
    nextAction: 'Keep as operation-panel module; invalid vendor STEP remains reference envelope only.',
  },
  {
    family: 'cabinet_body_or_base',
    module: 'cabinet_body',
    rows: 24,
    maturity: 'manual_review_required',
    evidence: ['BOM', 'DXF', 'SolidWorks'],
    status: 'Missing STEP export source-type decision',
    nextAction: 'Decide assembly envelope vs welded body vs part-level sheet-metal source.',
  },
]

export const templateAssets: TemplateAsset[] = [
  {
    id: 'TPL-16038-XY-LAUNDRY-1917x1000x550',
    title: '16038 洗衣寄存柜同尺寸多门数',
    productType: '洗衣寄存柜',
    size: '1917x1000x550',
    variants: '4门 / 7门 / 8门 / 12门',
    sourceAssemblyPath:
      'C:\\Users\\Administrator\\Desktop\\参数化模板素材\\16038 寄存柜XY(标准组合式 1917×1000×550)\\1.工程图\\标准洗衣寄存柜(总装配).SLDASM',
    bomPath: '4门/8门/主版本 BOM 均存在',
    dxfPath: '2.钣金展开图 + 4门/8门独立展开图',
    stepOrPdfPath: '3.PDF图纸 + 4门/8门 STEP 目录',
    ruleLearningValue: '高',
    ruleFocus: ['同尺寸门数变化', '门框分隔', '层板节距', '锁位/门板映射'],
    evidence: ['SolidWorks', 'DXF', 'BOM', 'STEP', '工程图'],
    status: 'ready_for_rule_learning',
  },
  {
    id: 'TPL-16029-STD-1917x1000x550',
    title: '16029 标准寄存柜 550 深基型',
    productType: '标准副柜 / 寄存柜',
    size: '1917x1000x550',
    variants: '12门，备份含 4/7/10/14 门线索',
    sourceAssemblyPath:
      'C:\\Users\\Administrator\\Desktop\\参数化模板素材\\16029 寄存柜(标准组合式 1917×1000×550)\\1.工程图\\标准寄存柜1917×1000×550(总装配).SLDASM',
    bomPath: '3.BOM\\16029 1917X1000X550标准柜BOM表.xlsx',
    dxfPath: '2.钣金展开图',
    stepOrPdfPath: '8.产品尺寸图；备份含参考 STEP',
    ruleLearningValue: '高',
    ruleFocus: ['标准柜基线', '历史订单变体', '柜体/门框/层板通用件', '锁中心关系'],
    evidence: ['SolidWorks', 'DXF', 'BOM', '工程图'],
    status: 'ready_for_rule_learning',
  },
  {
    id: 'TPL-16028-STD-1917x1000x485',
    title: '16028 标准寄存柜 485 深基型',
    productType: '标准副柜 / 寄存柜',
    size: '1917x1000x485',
    variants: '12门',
    sourceAssemblyPath:
      'C:\\Users\\Administrator\\Desktop\\参数化模板素材\\16028 寄存柜(标准组合式 1917×1000×485)\\1.工程图\\标准寄存柜(总装配).SLDASM',
    bomPath: '3.BOM\\16028 标准寄存柜BOM表.xlsx',
    dxfPath: '2.钣金展开图',
    stepOrPdfPath: '未见独立 STEP/PDF 交付目录',
    ruleLearningValue: '高',
    ruleFocus: ['柜深变化', '侧板/底座/上盖深度规则', '与 16029 对比'],
    evidence: ['SolidWorks', 'DXF', 'BOM'],
    status: 'ready_for_rule_learning',
  },
  {
    id: 'TPL-25072-SS-MAIN-1917x650x650',
    title: '25072 不锈钢控制主柜及海外变体',
    productType: '控制主柜 / 不锈钢主柜',
    size: '1917x650x650',
    variants: '独立主柜，4/5/6/9/17/21 门派生',
    sourceAssemblyPath:
      'C:\\Users\\Administrator\\Desktop\\参数化模板素材\\25072 寄存柜控制主柜(1917×650×650)（不锈钢）\\1.工程图，独立主柜\\寄存柜主柜1917×650×650(总装配).SLDASM',
    bomPath: '4.BOM\\25072 寄存柜控制主柜总装配BOM.xlsx',
    dxfPath: '2.展开图',
    stepOrPdfPath: '5.STP + 3.PDF',
    ruleLearningValue: '高',
    ruleFocus: ['主控柜配置', '不锈钢钣金', '海外订单变体', '主副柜组合'],
    evidence: ['SolidWorks', 'DXF', 'BOM', 'STEP', '工程图'],
    status: 'ready_for_rule_learning',
  },
  {
    id: 'TPL-23054-MAIN-1917x300x400',
    title: '23054 窄控制主柜订单配置',
    productType: '控制主柜',
    size: '1917x300x400',
    variants: '捷克 / 罗马尼亚订单配置',
    sourceAssemblyPath:
      'C:\\Users\\Administrator\\Desktop\\参数化模板素材\\23054 寄存柜控制主柜(1917×300×400)\\Ⅰ.工程图\\寄存柜控制主柜1917×300×400(总装配).SLDASM',
    bomPath: 'Ⅲ.BOM\\总装配BOM表.xlsx + 机箱配件BOM表.xlsx',
    dxfPath: 'Ⅱ.钣金展开图 + 订单展开图',
    stepOrPdfPath: 'Ⅳ.PDF图纸；未见 STEP',
    ruleLearningValue: '中高',
    ruleFocus: ['操作区开孔', '电气安装板', '维修门', '订单差异'],
    evidence: ['SolidWorks', 'DXF', 'BOM', '工程图'],
    status: 'ready_for_rule_learning',
  },
  {
    id: 'TPL-16028A-LIGHT-1917x1000x485',
    title: '16028A 带灯透明门标准寄存柜',
    productType: '标准寄存柜带灯',
    size: '1917x1000x485',
    variants: '12门透明 / 带灯 / 作废加强筋反例',
    sourceAssemblyPath:
      'C:\\Users\\Administrator\\Desktop\\参数化模板素材\\16028A 寄存柜(标准组合式带灯1917×1000×485) #\\1.工程图\\标准寄存柜带灯(总装配).SLDASM',
    bomPath: '3.BOM + 订单 BOM',
    dxfPath: '2.钣金展开图',
    stepOrPdfPath: '订单 STEP/PDF 存在',
    ruleLearningValue: '中高',
    ruleFocus: ['灯条增配', '透明门', '加强筋反例', '订单配置差异'],
    evidence: ['SolidWorks', 'DXF', 'BOM', 'STEP', '工程图'],
    status: 'ready_for_rule_learning',
  },
  {
    id: 'TPL-26001-STD-1917x736x350',
    title: '26001 窄宽浅深标准寄存柜',
    productType: '标准副柜 / 寄存柜',
    size: '1917x736x350',
    variants: '门数需打开确认',
    sourceAssemblyPath:
      'C:\\Users\\Administrator\\Desktop\\参数化模板素材\\26001 寄存柜(标准组合式 1917×736×350) #\\1.工程图\\Harness_1-标准寄存柜(1917×736×350).SLDASM',
    bomPath: '3.BOM\\26001 寄存柜BOM表.xlsx',
    dxfPath: '2.钣金展开图',
    stepOrPdfPath: '5.step简化图 + 4.PDF图',
    ruleLearningValue: '中',
    ruleFocus: ['窄宽浅深尺寸族', '线束/顶层装配确认', '简化 STEP 对照'],
    evidence: ['SolidWorks', 'DXF', 'BOM', 'STEP', '工程图'],
    status: 'needs_open_check',
  },
]

export const ruleLearningAxes: RuleLearningAxis[] = [
  {
    axis: '同尺寸不同门数',
    purpose: '同一外形尺寸下扩展门数组合时，门框分隔、层板数量、门板高度、锁位和铰链阵列不能靠猜。',
    sourceTemplates: '16038 1917x1000x550 + 16029 1917x1000x550',
    currentState: '16029 同尺寸门数样机只保留为 legacy evidence；当前工程交付口径已切到 800W gold-variable LMS/SML。',
    nextAction: '旧门数样机只用于追溯规则来源；新增交付必须回到 gold-variable 生成与 finalize 固定入口。',
    maturity: 'step_bbox_measured',
    evidence: ['SolidWorks', 'STEP', 'DXF', 'BOM', '证据闭环记录'],
  },
  {
    axis: '同系列不同柜深',
    purpose: '同宽高下从 485 深补到 550 深时，侧板、底座、上盖、层板、后门/维护件的深度变化要可追踪。',
    sourceTemplates: '16028 1917x1000x485 + 16029 1917x1000x550',
    currentState: '两个标准组合式样本证据齐；需要比较同名零件、DXF 外形、BOM 物料变化。',
    nextAction: '建立 depth_delta 规则表：固定项、随深度变化项、需要证据闭环项分开。',
    maturity: 'mapped',
    evidence: ['SolidWorks', 'DXF', 'BOM'],
  },
  {
    axis: '主控柜/操作区配置',
    purpose: '主控柜补模型时，屏幕、扫码、电源、控制板、维修门和线束空间要作为模块接口处理。',
    sourceTemplates: '23054 / 24030 / 25072',
    currentState: '总装配、DXF、BOM/PDF 已定位；操作区和电气区还未从普通柜体规则中拆开。',
    nextAction: '先做主控柜模块拆分表，再决定哪些参数能生成，哪些必须进入证据闭环。',
    maturity: 'raw_imported',
    evidence: ['SolidWorks', 'DXF', 'BOM', '工程图'],
  },
  {
    axis: '材料/工艺变体',
    purpose: '不锈钢、带灯透明门、作废加强筋方案不能和普通喷涂钢板柜混用规则。',
    sourceTemplates: '25072 / 16028A / 16028',
    currentState: '可学习样本已识别；作废目录仅作为反例，不进入自动生成候选。',
    nextAction: '给每条规则增加 material/process/config 标签，避免跨工艺套用。',
    maturity: 'raw_imported',
    evidence: ['SolidWorks', 'DXF', 'BOM', 'STEP'],
  },
]

export const capabilities: Capability[] = [
  {
    id: 'same_size_variant_rule_learning',
    title: '同尺寸门数变体规则学习队列',
    productType: '标准寄存柜 / 洗衣寄存柜',
    module: '柜体 / 门框 / 层板 / 门板 / 锁具 / 铰链',
    variants: '16038 多门数样本；16029 同尺寸旧样机仅作规则来源',
    status: 'queued',
    maturity: 'mapped',
    generator: 'not_enabled',
    validation: 'pending SolidWorks transform + BOM/DXF cross-check',
    parameters: ['template_id', 'target_door_count', 'same_size_constraint', 'rule_axis'],
    evidence: ['SolidWorks', 'DXF', 'BOM', '工程图'],
    limitation: '这是规则学习入口，不是立即复制总装配。需要先抽取门框分隔、层板节距、门板高度和锁位映射后才开放生成。',
  },
  {
    id: 'locker_16029_gold_variable_current',
    title: '16029 800W gold-variable 当前审核包',
    productType: '标准寄存柜',
    module: '整柜 / 变高门序 / 层板 / 前框 / 五金',
    variants: 'LMS: 大 6/12、中 4/12、小 2/12；SML: 小 2/12、中 4/12、大 6/12',
    status: 'generatable',
    maturity: 'manual_review_required',
    generator: 'workers\\maintenance\\generate_16029_800w_gold_variable_model_freecad.py',
    validation: 'model gate + STEP bbox gate + SolidWorks 2020 open screenshot gate + current handoff scope gate',
    parameters: ['variant_token', 'row_units'],
    evidence: ['STEP', 'SolidWorks', '证据闭环记录'],
    limitation: '当前是可审核包，不是生产图纸释放；新增变体仍只能改 variant_token 和 row_units，并重新跑 model gate、STEP bbox gate、SolidWorks 2020 open gate 与 handoff scope gate。',
  },
  {
    id: 'locker_16038_variant_template',
    title: '16038 洗衣寄存柜同尺寸变体模板',
    productType: '洗衣寄存柜',
    module: '整柜 / 12/12 门模块 / 门锁数量规则',
    variants: '4门, 7门, 8门整柜；12/12 单门模块',
    status: 'generatable',
    maturity: 'engineering_reference',
    generator: 'D:\\Winnsen_Structure_Agent_Studio\\workers\\maintenance\\clone_16038_variant_step_reference.py',
    validation: 'STEP variant formula candidate + SolidWorks component manifest / quality report',
    parameters: ['door_count'],
    evidence: ['SolidWorks', 'STEP', 'BOM', '工程图', '证据闭环记录'],
    limitation:
      '16038 已有 4/7/8 门 STEP 变体证据闭环，12/12 为 SolidWorks 单门模块证据；当前入口是模板派生/证据绑定，不是任意门数参数化重排。',
  },
  {
    id: 'locker_16029_regression',
    title: '16029 标准寄存柜整柜回归模型（legacy evidence）',
    productType: '标准寄存柜',
    module: '整柜 / 门板 / 柜体 / 锁具 / 铰链',
    variants: '旧同尺寸门数样机；只作为规则追溯和回归证据',
    status: 'reference_only',
    maturity: 'engineering_reference',
    generator: 'legacy evidence only',
    validation: 'historical SolidWorks visual QA + STEP/FCStd evidence, superseded for current handoff',
    parameters: ['door_count', 'cabinet_width', 'geometry_source'],
    evidence: ['DXF', 'STEP', 'SolidWorks', 'FreeCAD', 'BOM'],
    limitation:
      '该路线只保留为旧证据来源，不再作为工程师入口；当前交付以 800W gold-variable LMS/SML/DUAL 审核包为准。',
  },
  {
    id: 'locker_16029_door_panel',
    title: '16029 普通门板单件/系列模型（legacy evidence）',
    productType: '标准寄存柜',
    module: 'ordinary_door',
    variants: '历史单门板与宽度候选证据；当前审核包使用 W337',
    status: 'reference_only',
    maturity: 'engineering_reference',
    generator: 'legacy evidence only',
    validation: 'door body trace and panel bbox evidence retained for audit',
    parameters: ['category', 'door_width', 'door_height', 'geometry_source'],
    evidence: ['DXF', 'STEP', 'SolidWorks', 'FreeCAD'],
    limitation: '该能力不再作为当前生成入口；锁中心仅为装配关系 datum，不能自动新增可见锁孔。',
  },
  {
    id: 'outdoor_waterproof_door_1_12_r',
    title: '户外防水柜门 1/12 R 单件参考模型',
    productType: '户外快递柜',
    module: 'waterproof_door_panel',
    variants: '1/12 R',
    status: 'reference_only',
    maturity: 'step_bbox_measured',
    generator: 'scripts\\generate_outdoor_courier_single_waterproof_door_freecad.py',
    validation: 'outdoor_courier_single_door source signature audit',
    parameters: ['door_fraction', 'side', 'formed_bbox_x', 'formed_bbox_y', 'formed_bbox_z', 'flat_holes'],
    evidence: ['DXF', 'STEP', 'SolidWorks', 'FreeCAD'],
    limitation: 'bbox 和 11 个圆孔通过；源 STEP 164/398 faces/edges，生成模型 33/90，拓扑需升级。',
  },
  {
    id: 'outdoor_lock_water_path',
    title: '户外锁钩/导水槽规则候选',
    productType: '户外快递柜',
    module: 'u_lock_hook_pad / water_guide_channel',
    variants: '跨 22057/23020/26015',
    status: 'blocked',
    maturity: 'manual_review_required',
    generator: 'not_enabled',
    validation: 'pending STEP export + semantic placement gate',
    parameters: ['source_project', 'sibling_evidence', 'lock_engagement_reference', 'water_path_role'],
    evidence: ['SolidWorks', 'DXF', 'BOM'],
    limitation: '12 条 P0 导出阻塞影响锁配合和防水路径规则。',
  },
  {
    id: 'qtrak_operation_panel',
    title: 'QTRAK 主柜操作区模块候选',
    productType: '户外主控柜',
    module: 'display_operation_panel / electronics_mount',
    variants: '15寸触摸屏、读卡器、摄像头、电源安装',
    status: 'queued',
    maturity: 'step_bbox_measured',
    generator: 'not_enabled',
    validation: 'pending operation-panel interface separation',
    parameters: ['display_size', 'mount_recess', 'service_side', 'wire_exit', 'electronics_tray'],
    evidence: ['SolidWorks', 'STEP', 'FreeCAD'],
    limitation: '必须与副柜通用钣金规则分离；无效 vendor STEP 只能作为参考包络。',
  },
]

export const drawingSheetMetalSources: DrawingSheetMetalSource[] = [
  {
    id: 'dxf-to-sheetmetal-reference',
    title: 'DXF 展开图转参考钣金件',
    sourceType: 'DXF',
    firstTarget: '16029 门板 / 层板 / 横隔板',
    currentState: '已跑通 16029 1/12 门板 DXF 解析卡，可提取 bbox、实体数量、圆孔半径和质量警告。',
    nextAction: '补闭合轮廓重建和 model/layout 空间判定，再把孔位、折弯线和尺寸基准写入规则表。',
    evidence: ['DXF', '工程图', '证据闭环记录'],
    status: 'ready_for_intake',
  },
  {
    id: 'pdf-drawing-to-parameters',
    title: 'PDF 工程图转参数与待确认项',
    sourceType: 'PDF / 工程图',
    firstTarget: '有标题栏、材料、厚度或折弯说明的单件图',
    currentState: '适合提取尺寸、材料、厚度、版本和公差，但图层/线型信息不如 DXF 稳定。',
    nextAction: '先做 OCR + 尺寸表提取，无法确认的孔径、折弯方向和基准进入待确认项。',
    evidence: ['工程图', '证据闭环记录'],
    status: 'candidate',
  },
  {
    id: 'image-to-cad-reference',
    title: '图片识别转结构草案',
    sourceType: '截图 / 拍照图片',
    firstTarget: '外观草图、局部结构截图、旧图纸截图',
    currentState: '只能做结构意图识别和缺项列表，不能直接作为精准建模依据。',
    nextAction: '先输出轮廓、孔位疑点、折弯疑点和需要补充的标尺/基准，不直接生成生产图。',
    evidence: ['工程图', '证据闭环记录'],
    status: 'planned',
  },
  {
    id: 'solidworks-drawing-to-handoff',
    title: 'SolidWorks 工程图/模型反推规则',
    sourceType: 'SLDDRW / SLDPRT / SLDASM',
    firstTarget: '现有标准件的展开、孔位、折弯和工程图交接',
    currentState: '可以作为工程交接主通道，但自动展开和出图需要 SolidWorks API 稳定性验证。',
    nextAction: '先保持低并发，单件验证展开、尺寸标注和保存流程，再接入整柜规则。',
    evidence: ['SolidWorks', 'DXF', '工程图'],
    status: 'candidate',
  },
]

export const drawingSheetMetalOutputs: DrawingSheetMetalOutput[] = [
  {
    id: 'parameter-review-table',
    title: '图纸参数表',
    level: '可先交付',
    description: '把外形、孔位、折弯边、材料/厚度线索和缺失项整理成结构工程师可复核表。',
    boundary: '来自图片或 PDF 的尺寸需要人工确认比例和基准。',
    statusTone: 'good',
  },
  {
    id: 'reference-unfold-dxf',
    title: '参考展开 DXF',
    level: '工程参考',
    description: '面向门板、层板、横隔板等单件，输出可复核的展开轮廓和孔位参考。',
    boundary: '未确认 K 因子、折弯扣除、材料和厚度前，不标记为正式展开图。',
    statusTone: 'warn',
  },
  {
    id: 'dimensioned-reference-pdf',
    title: '尺寸标注 PDF',
    level: '工程参考',
    description: '自动生成主要外形、孔距、折弯位置和关键间距标注，帮助工程师快速校对。',
    boundary: '标题栏、版本、BOM 和公差体系未确认前不能作为生产释放图纸。',
    statusTone: 'warn',
  },
  {
    id: 'bend-hole-checklist',
    title: '折弯/孔位校验表',
    level: '规则证据',
    description: '检查孔到折弯线距离、对称孔、锁孔/铰链孔阵列和外形 bbox 是否符合模板规则。',
    boundary: '异常项进入待确认项，不自动修改原工程规则。',
    statusTone: 'good',
  },
]

export const drawingSheetMetalRisks: DrawingSheetMetalRisk[] = [
  {
    item: '材料与厚度',
    requiredInput: '材质牌号、板厚、表面处理',
    reason: '展开尺寸、折弯补偿和外观面风险都依赖材料与厚度。',
    owner: '结构工程师 / 工艺',
  },
  {
    item: '折弯半径与 K 因子',
    requiredInput: '内 R、K 因子或折弯扣除表',
    reason: '没有折弯参数只能生成参考展开，不能输出生产级展开尺寸。',
    owner: '结构工程师 / 钣金供应商',
  },
  {
    item: '尺寸基准与公差',
    requiredInput: '孔位基准、关键尺寸公差、装配间隙要求',
    reason: '图片/PDF 识别容易丢失基准关系，必须把关键尺寸和功能孔分开。',
    owner: '结构工程师',
  },
  {
    item: '图纸版本与标题栏',
    requiredInput: '图号、版本、BOM 关系、公司图框规范',
    reason: '自动标注 PDF 只能做复核件，正式图纸需要版本和审批链。',
    owner: '工程资料 / 结构工程师',
  },
]

export const drawingSheetMetalRoadmap: DrawingSheetMetalRoadmapStep[] = [
  {
    step: '1',
    focus: '单件图纸解析',
    deliverable: '16029 门板 DXF/PDF 参数卡和待确认项',
    relationToMainline: '不占用 SolidWorks 整柜生成资源，先补规则证据。',
    statusTone: 'good',
  },
  {
    step: '2',
    focus: '参考展开与标注',
    deliverable: '门板/层板/横隔板参考展开 DXF 与尺寸标注 PDF',
    relationToMainline: '用于校验门数变化时的门板、层板和横隔阵列规则。',
    statusTone: 'warn',
  },
  {
    step: '3',
    focus: '规则回写生成器',
    deliverable: '把孔位、折弯边、bbox、阵列节距写入 16029 10/12/14 门规则。',
    relationToMainline: '服务同外形不同门数生成，不做全量变种库存。',
    statusTone: 'warn',
  },
  {
    step: '4',
    focus: 'SolidWorks 工程交接',
    deliverable: '单件展开和尺寸图通过工程师复核后，再接入 SolidWorks 出图流程。',
    relationToMainline: 'SolidWorks 继续作为工程交接通道，FreeCAD/解析线做规则验证。',
    statusTone: 'idle',
  },
]

export const drawingSheetMetalBatchRun: DrawingSheetMetalBatchRun = {
  runId: 'BATCH-16029-SHEETMETAL-20260519',
  sourceRoot: 'C:\\Users\\Administrator\\Desktop\\参数化模板素材\\16029 寄存柜(标准组合式 1917×1000×550)',
  outputDir: sourcePaths.drawingSheetMetalBatchRun,
  summaryPath: 'D:\\Winnsen_Structure_Agent_Studio\\workers\\drawing_sheetmetal\\runs\\BATCH-16029-SHEETMETAL-20260519\\BATCH_SUMMARY.md',
  fileCount: 49,
  errorCount: 0,
  ruleSeedCandidateCount: 34,
  qualityStatusCounts: [
    { label: 'rule_seed_candidate', value: 2, tone: 'good' },
    { label: 'geometry_rule_seed_only', value: 32, tone: 'good' },
    { label: 'needs_layout_filter', value: 7, tone: 'warn' },
    { label: 'needs_closed_loop_rebuild', value: 8, tone: 'warn' },
  ],
  roleCounts: [
    { label: 'door_panel', value: 17 },
    { label: 'shelf', value: 9 },
    { label: 'divider', value: 23 },
  ],
  keyFindings: [
    '16029 门板、层板、竖/横隔板 DXF 共 49 个样本完成轻量解析，未出现解析异常。',
    '34 个样本具备几何规则种子价值，可用于门板高度、层板/隔板 bbox、孔径和阵列关系比对。',
    '2 个样本带板厚 0.8 且可作为优先规则种子：12/12 门板和门框加强筋。',
    '脚本已把小孔闭合环排除在规则 bbox 外；仍有 7 个样本存在 paper-space / VIEWPORT 干扰，8 个样本需要 LINE/ARC 闭合轮廓重建。',
  ],
  nextActions: [
    '先对 needs_layout_filter 文件过滤图纸空间，避免把布局视图尺寸写入规则库。',
    '对 needs_closed_loop_rebuild 文件做闭合轮廓重建，尤其是 1/12 到 6/12 门板序列。',
    '把 25 个几何规则种子的 bbox、孔径、闭合轮廓与 SolidWorks/BOM 角色绑定交叉验证。',
  ],
}

export const reviewItems: ReviewItem[] = [
  {
    id: 'STD-P0-16029-SW2020-OPEN-SCREENSHOT-GATE',
    project: '16029 800W gold-variable',
    module: 'SolidWorks 2020 open evidence',
    priority: 'P2',
    severity: '观察',
    issue: 'LMS/SML 已用 SolidWorks 2020 打开 STEP，并保存可读截图与 JSON 打开记录；current handoff scope gate 已 PASS',
    nextAction: '保留该证据作为审核依据；后续任何模型重出都必须重新生成 SolidWorks 2020 打开截图。',
    evidence: ['SolidWorks', 'STEP', '证据闭环记录'],
    status: 'watching',
  },
  {
    id: 'STD-P1-16029-GOLD-VARIABLE-SIGNOFF',
    project: '16029 800W gold-variable',
    module: 'LMS / SML handoff signoff',
    priority: 'P1',
    severity: '严重',
    issue: 'LMS 与 SML 候选审核包已生成，工程确认重点是门序、门宽 W337、内部间隙 2+3+2 与五金计数是否一致',
    nextAction: '按 handoff ZIP 内的 STEP、自审图、verify CSV、model gate、bbox gate 与 SolidWorks 2020 打开证据逐项签字。',
    evidence: ['STEP', 'SolidWorks', '证据闭环记录'],
    status: 'open',
  },
  {
    id: 'STD-P1-16029-HANDOFF-PACKAGE-CONTROL',
    project: '16029 800W gold-variable',
    module: 'handoff package control',
    priority: 'P1',
    severity: '严重',
    issue: '工程师审核输入只使用 LMS、SML、DUAL 三个候选 ZIP，避免散文件或历史候选混入评审',
    nextAction: '交付时发送审核包下载页地址和三个 ZIP 文件名，任何新增修正都重新走 generate 与 finalize 固定入口。',
    evidence: ['STEP', '工程图', '证据闭环记录'],
    status: 'open',
  },
  {
    id: 'OUT-P0-DOOR-L',
    project: '户外快递柜系列',
    module: 'waterproof_door_panel',
    priority: 'P0',
    severity: '阻塞',
    issue: '20 条左侧防水门板 STEP 导出阻塞',
    nextAction: 'Repair or bypass SolidWorks STEP export; compare same fraction/side measured siblings before generator use.',
    evidence: ['SolidWorks', 'STEP'],
    status: 'open',
  },
  {
    id: 'OUT-P0-LOCK-WATER',
    project: '户外快递柜系列',
    module: 'u_lock_hook_pad / water_guide_channel',
    priority: 'P0',
    severity: '阻塞',
    issue: '12 条锁钩垫板/导水槽 STEP 导出阻塞',
    nextAction: 'Repair OpenDoc/STEP export or approve sibling evidence because this affects lock engagement and water-path rules.',
    evidence: ['SolidWorks', 'STEP', 'DXF'],
    status: 'open',
  },
  {
    id: 'OUT-P1-DISPLAY',
    project: '24067 QTRAK 主柜',
    module: 'display_recess_or_mount',
    priority: 'P1',
    severity: '严重',
    issue: '15寸户外高亮触摸显示器 STEP 在 FreeCAD 中无效',
    nextAction: 'Keep as external/reference envelope until a valid vendor model or simplified envelope rule is approved.',
    evidence: ['STEP', 'FreeCAD'],
    status: 'watching',
  },
  {
    id: 'OUT-P2-BODY-BASE',
    project: '户外快递柜系列',
    module: 'cabinet_body / base_structure',
    priority: 'P2',
    severity: '一般',
    issue: '24 条柜体/底座缺少 STEP export source-type decision',
    nextAction: 'Decide whether each row is assembly envelope, welded body, or part-level sheet-metal rule.',
    evidence: ['BOM', 'DXF', 'SolidWorks'],
    status: 'open',
  },
  {
    id: 'STD-P2-W784',
    project: '16029 标准寄存柜',
    module: 'cabinet_body / door_frame',
    priority: 'P2',
    severity: '一般',
    issue: '37 条 18门 W784 宽度派生 X 行仍需确认',
    nextAction: 'Provide width-specific source or drawing rule before promoting these rows beyond engineering reference.',
    evidence: ['STEP', 'SolidWorks', 'FreeCAD'],
    status: 'watching',
  },
  {
    id: 'STD-LOCK-CUT',
    project: '16029 标准寄存柜',
    module: 'lock_system',
    priority: 'P2',
    severity: '一般',
    issue: '8/14/18 门高度上的额外可见锁孔不得从锁中心关系猜测生成',
    nextAction: 'Use SolidWorks feature, DXF, or engineering drawing evidence before adding visible lock cuts.',
    evidence: ['SolidWorks', 'DXF', '工程图'],
    status: 'watching',
  },
  {
    id: 'NEXT-MAIN-CONTROL',
    project: '未来主控柜',
    module: 'operation_panel / electronics',
    priority: 'P3',
    severity: '观察',
    issue: '已登记但未提供 source root',
    nextAction: 'Provide desensitized source package path, then run generic intake pipeline.',
    evidence: ['证据闭环记录'],
    status: 'waiting_source',
  },
  {
    id: 'NEXT-VENDING',
    project: '未来贩卖机',
    module: 'dispensing / transmission / service door',
    priority: 'P3',
    severity: '观察',
    issue: '已登记但未提供 source root',
    nextAction: 'Start from standard assembly, BOM, DXF, STEP and module interface notes.',
    evidence: ['证据闭环记录'],
    status: 'waiting_source',
  },
]

export const metricCards = [
  {
    label: '当前主线',
    value: '16029 800W',
    detail: 'LMS / SML gold-variable 双方案',
    tone: 'good',
  },
  {
    label: '统一外形',
    value: '800W',
    detail: '1917H × 550D / W337 / gap 2+3+2=7',
    tone: 'good',
  },
  {
    label: '审核包',
    value: '3 ZIP',
    detail: 'LMS / SML / DUAL current candidates',
    tone: 'warn',
  },
  {
    label: '生成入口',
    value: '2 scripts',
    detail: 'generate + finalize 固定入口',
    tone: 'warn',
  },
  { label: '当前 Gate', value: 'PASS', detail: 'LMS/SML SW2020 打开截图已补齐', tone: 'good' },
]
