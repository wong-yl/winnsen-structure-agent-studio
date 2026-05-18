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

const snapshot = generatedSnapshot
const lockerSnapshot = snapshot.projects.locker16029
const outdoorSnapshot = snapshot.projects.outdoorCourier
const formatNumber = (value: number) => value.toLocaleString('en-US')

export const assetSnapshot = snapshot

export const sourcePaths = {
  appRoot: snapshot.sourcePaths.appRoot,
  cadWorkspace: snapshot.sourcePaths.cadWorkspace,
  startupBrief: 'C:\\Users\\Administrator\\Desktop\\Winnsen_Structure_Agent_Studio_项目启动说明.md',
  parametricTemplateRoot: 'C:\\Users\\Administrator\\Desktop\\参数化模板素材',
  intakeStatus: snapshot.sourcePaths.intakeStatus,
  outdoorStatus: snapshot.sourcePaths.outdoorStatus,
  strictQueue: snapshot.sourcePaths.strictQueueMd,
  outdoorGate: snapshot.sourcePaths.outdoorGateMd,
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
    name: '16029 标准寄存柜',
    productType: '标准组合式寄存柜',
    status: '基线已冻结，SolidWorks 主线已接入，FreeCAD 迁移回归 PASS',
    statusTone: 'good',
    sourceRoot: 'C:\\Users\\Administrator\\Desktop\\参数化模板素材\\16029 寄存柜(标准组合式 1917×1000×550)',
    modelStatus: 'SolidWorks 原生装配与 FreeCAD 开源迁移模型均为工程参考',
    progress: 91,
    capability: '8门、12门、14门、16门、18门 W784 回归模型',
    risk: '37 条 W784 宽度派生 X 仍需宽度专用来源或证据闭环',
    updatedAt: '2026-05-15 17:24',
    stats: [
      { label: 'Indexed files', value: formatNumber(lockerSnapshot.indexedFiles), tone: 'good' },
      { label: 'DXF parsed', value: `${lockerSnapshot.dxfParsed}/${lockerSnapshot.dxfFiles}`, tone: 'good' },
      { label: 'BOM mapped', value: `${lockerSnapshot.bomMapped}/${lockerSnapshot.bomRows}`, tone: 'good' },
      { label: 'Strict P0', value: `${lockerSnapshot.strictPriorityCounts.P0 ?? 0}`, tone: 'good' },
      { label: 'Strict P2', value: `${lockerSnapshot.strictPriorityCounts.P2 ?? 0}`, tone: 'warn' },
    ],
    notes: [
      'Rule seed coverage gate: 8门 170/170, 12门 386/386, 14门 418/418, 18门 W784 530/530.',
      '门板折弯、锁中心关系、门体切孔、硬件 bbox、结构 STEP 关系已纳入多轮验证。',
      '当前工程使用 SolidWorks；FreeCAD 是未来开源替代路线，不作为 SolidWorks 输出的主检查器。',
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
    locker16029: '8/12/14/16/18 W784 engineering reference',
    outdoor: '1/12 R waterproof door reference',
    next: '新增模板先进入规则学习队列，通过后再开放同尺寸补模型',
    evidence: ['SolidWorks', 'FreeCAD', 'STEP', 'DXF'],
    risk: '不能把工程参考模型描述为自动生产图纸',
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
    nextAction: 'W784 width-derived rows need source confirmation before production promotion.',
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
    purpose: '同一外形尺寸下补 4/7/8/10/12/14 门模型时，门框分隔、层板数量、门板高度、锁位和铰链阵列不能靠猜。',
    sourceTemplates: '16038 1917x1000x550 + 16029 1917x1000x550',
    currentState: '候选总装配、BOM、DXF、PDF/STEP 已定位；尚未抽取完整 transform/mate/展开图映射。',
    nextAction: '优先抽 16038 的 4门/8门/主版本，再用 16029 备份订单变体补 7/10/14 门规则。',
    maturity: 'mapped',
    evidence: ['SolidWorks', 'DXF', 'BOM', '工程图'],
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
    variants: '16038: 4/7/8/12门；16029: 4/7/10/12/14门线索',
    status: 'queued',
    maturity: 'mapped',
    generator: 'not_enabled',
    validation: 'pending SolidWorks transform + BOM/DXF cross-check',
    parameters: ['template_id', 'target_door_count', 'same_size_constraint', 'rule_axis'],
    evidence: ['SolidWorks', 'DXF', 'BOM', '工程图'],
    limitation: '这是规则学习入口，不是立即复制总装配。需要先抽取门框分隔、层板节距、门板高度和锁位映射后才开放生成。',
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
    title: '16029 标准寄存柜整柜回归模型',
    productType: '标准寄存柜',
    module: '整柜 / 门板 / 柜体 / 锁具 / 铰链',
    variants: '8门, 12门, 14门, 16门, 18门 W784',
    status: 'generatable',
    maturity: 'engineering_reference',
    generator: 'scripts\\generate_locker_16029_freecad.py',
    validation: 'scripts\\run_locker_16029_regression_checks.py',
    parameters: ['door_count', 'cabinet_width', 'geometry_source'],
    evidence: ['DXF', 'STEP', 'SolidWorks', 'FreeCAD', 'BOM'],
    limitation: 'SolidWorks 是当前工程主线，FreeCAD 是开源迁移路线；严格队列 P0=0，但 W784 仍有 37 条宽度派生 P2；不可称为自动生产图纸。',
  },
  {
    id: 'locker_16029_door_panel',
    title: '16029 普通门板单件/系列模型',
    productType: '标准寄存柜',
    module: 'ordinary_door',
    variants: 'W329/W437, 多高度门板',
    status: 'generatable',
    maturity: 'engineering_reference',
    generator: 'scripts\\generate_locker_16029_freecad.py',
    validation: 'door body cut trace / bend edge trace / panel source bbox release',
    parameters: ['category', 'door_width', 'door_height', 'geometry_source'],
    evidence: ['DXF', 'STEP', 'SolidWorks', 'FreeCAD'],
    limitation: '锁中心仅为装配关系 datum；不能自动新增可见锁孔。',
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

export const reviewItems: ReviewItem[] = [
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
    label: '已接入项目',
    value: `${snapshot.summary.projectSlots}`,
    detail: `${snapshot.summary.activeProjects} active + ${snapshot.summary.futureSlots} future slots + ${snapshot.summary.reservedFamilies} reserved families`,
    tone: 'good',
  },
  {
    label: 'CAD 证据资产',
    value: snapshot.summary.cadEvidenceAssetsLabel,
    detail: 'SolidWorks / DXF / BOM / STEP / 工程图 / images',
    tone: 'good',
  },
  {
    label: '规则候选',
    value: formatNumber(lockerSnapshot.strictRows + outdoorSnapshot.familyRuleCandidates),
    detail: '16029 strict rows + outdoor family candidates',
    tone: 'warn',
  },
  {
    label: '可生成模型',
    value: '5',
    detail: `工程参考优先，production_candidate=${snapshot.summary.productionCandidateRows}`,
    tone: 'good',
  },
  { label: 'P0 阻塞', value: `${outdoorSnapshot.p0Blockers}`, detail: '全部来自户外柜 STEP export blockers', tone: 'risk' },
]
