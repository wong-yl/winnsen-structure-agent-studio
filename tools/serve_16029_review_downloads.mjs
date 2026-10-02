import { closeSync, copyFileSync, createReadStream, existsSync, fstatSync, mkdirSync, openSync, readFileSync, readSync, readdirSync, renameSync, rmSync, statSync, writeFileSync } from 'node:fs'
import { createServer } from 'node:http'
import { networkInterfaces } from 'node:os'
import { basename, dirname, extname, isAbsolute, relative, resolve } from 'node:path'
import { createHash, pbkdf2Sync, randomBytes, timingSafeEqual } from 'node:crypto'
import { fileURLToPath } from 'node:url'
import {
  is16029StructureAssistanceReady,
  native16029RequestFingerprint,
  normalize16029NativeModelRequest,
} from './locker_16029_native_generator.mjs'
import {
  createNativeTaskStore,
  DEFAULT_ACTIVE_NATIVE_TASK_STATES,
} from './lib/locker_16029_native_task_store.mjs'
import {
  inspectNativeStructureAssistancePublication,
  openVerifiedNativeStructureAssistanceArchive,
} from './lib/locker_16029_native_publication.mjs'
import { inspect16029ParametricTaskPublication, open16029ParametricTaskArchive } from './lib/locker_16029_parametric_publication.mjs'
import { render16029AuthCabinet, render16029AuthCabinetScript, authCabinetShowcaseCss } from './lib/locker_16029_auth_showcase.mjs'

const SCRIPT_DIR = dirname(fileURLToPath(import.meta.url))
const ROOT = resolve(process.env.STUDIO_REVIEW_ROOT || resolve(SCRIPT_DIR, '..'))
const LOCAL_CONFIG_PATH = resolve(
  process.env.STUDIO_REVIEW_CONFIG || resolve(ROOT, 'data/review_portal.local.json'),
)
const localConfig = existsSync(LOCAL_CONFIG_PATH)
  ? JSON.parse(readFileSync(LOCAL_CONFIG_PATH, 'utf8'))
  : {}
const PORT = Number(process.env.STUDIO_REVIEW_PORT || localConfig.port || 5180)
const HOST = process.env.STUDIO_REVIEW_HOST || localConfig.host || '0.0.0.0'
const DATA_DIR = resolve(process.env.STUDIO_REVIEW_DATA_DIR || resolve(ROOT, 'data'))
const REVIEW_STORAGE_ROOT = resolve(
  process.env.STUDIO_REVIEW_STORAGE_ROOT ||
    localConfig.storageRoot ||
    resolve(ROOT, 'data/review_portal_storage'),
)
const MODEL_DOWNLOAD_DIR = resolve(REVIEW_STORAGE_ROOT, '模型下载')
const FEEDBACK_DIR = resolve(REVIEW_STORAGE_ROOT, '工程反馈')
const GENERATION_REQUEST_DIR = resolve(DATA_DIR, 'native_model_requests')
const USER_DB_PATH = resolve(DATA_DIR, 'review_download_users.json')
const FEEDBACK_INDEX_PATH = resolve(FEEDBACK_DIR, 'feedback_index.json')
const nativeTaskStore = createNativeTaskStore({ dataDir: GENERATION_REQUEST_DIR })
const INVITE_CODE_PATH = resolve(DATA_DIR, 'review_download_invite_code.txt')
const BRAND_LOGO_PATH = resolve(ROOT, 'apps/web/public/brand/winnsen-logo.jpg')
const BRAND_MARK_PATH = resolve(ROOT, 'apps/web/public/brand/winnsen-mark.png')
const CLARIFICATION_PACKAGE_ROOT = resolve(
  ROOT,
  'workers/generated_models/review_generation_requests/v43-int-v37-760w-six-door-l642-r246-r1/review_package_v37_20260811',
)
const CLARIFICATION_QUESTIONS_PATH = resolve(CLARIFICATION_PACKAGE_ROOT, 'clarification_questions_v37.json')
const CLARIFICATION_IMAGE_DIR = resolve(CLARIFICATION_PACKAGE_ROOT, 'decision_reference')
const SESSION_COOKIE = 'review_session'
const SESSION_TTL_MS = 8 * 60 * 60 * 1000
const PASSWORD_ITERATIONS = 120000
const MAX_BODY_BYTES = 32 * 1024 * 1024
const MAX_ATTACHMENT_BYTES = 10 * 1024 * 1024
const MAX_ATTACHMENT_TOTAL_BYTES = 20 * 1024 * 1024
const MAX_ATTACHMENT_COUNT = 6
const FEEDBACK_ID_PREFIX = 'V37-Q-'
const sessions = new Map()

const reviewRound = {
  id: '16029-v43-v37-760w-six-door-engineering-assistance-20260811',
  title: '16029 V37 760宽六门工程辅助模型反馈',
  project: '16029 参数化工程辅助建模',
  cadMainline: 'SolidWorks 2020',
  feedbackIdPrefix: FEEDBACK_ID_PREFIX,
  boundary: '客户先提交目标宽度、柜深和门数；系统只在原生SolidWorks生成与结构门禁通过后提供工程深化模型。当前V37精确配方为760W x 1917H x 550D、2列6门、L642-R246、单门317 x 908mm；V36四门与V35六门模型保留为历史已验证精确配方。',
  gateStatus: 'V37已通过SolidWorks 2020原生层级、递归引用、一门一锁孔、关闭保存重开及最终宽度族门禁；V36四门与V35六门基线继续可用，其他参数组合仍需逐族完成原生生成器绑定与回归门禁。V37打开后必须重建，且保留唯一已知Reference code 51，因此warningFree=false。',
  gateBlocker: '结构工程辅助基础模型用于减少重复建模；材料、工艺、图纸、BOM及项目细节由结构工程师在此基础上继续深化。',
  instruction: '填写脱敏客户需求和结构参数，核对预览，生成并下载本次任务模型，再由结构工程师继续深化。模型问题和应用效果都应绑定到对应生成任务。',
}

const assets = [
  {
    id: '16029-v43-internal-sheetmetal-lockfix-zip',
    title: '16029 740W v43 历史工程复核包（已目视确认）',
    category: '历史工程复核包',
    description: 'v43-int-v18-lockfix：SolidWorks 2020 Pack-and-Go，沿用 740W / L642-R246 / v43 柜门路线，恢复 6 个机械锁舌，后背接缝按侧板钣金居中，电器板、电控锁、电控锁钩排除。',
    fileName: 'review_generation_v43-int-v18-lockfix_solidworks2020_full_assembly.zip',
    sourcePath: resolve(ROOT, 'workers/generation_logs/review_generation_v43-int-v18-lockfix_solidworks2020_full_assembly.zip'),
    path: resolve(MODEL_DOWNLOAD_DIR, 'review_generation_v43-int-v18-lockfix_solidworks2020_full_assembly.zip'),
  },
  {
    id: '16029-v43-v37-760w-six-door-engineering-assistance-zip',
    title: '16029 V37 760W 六门 L642-R246 工程辅助模型',
    category: '当前已验证工程辅助模型｜V37',
    description: '760W x 1917H x 550D、2列6门、L642-R246，单门317 x 908mm；六扇门各对应一个原生异形锁孔和一个锁舌。原生SolidWorks 2020层级、包内引用、保存重开与宽度族检查均已完成，可作为结构工程师继续深化的基础模型；打开后请先重建。',
    fileName: '16029_v37_760宽6门L642-R246_工程辅助模型_20260811.zip',
    sha256: '6B9F62E4F697B96909131D81EAD0FA341E059530204DB76480148460883CD1C0',
    expectedSizeBytes: 24535798,
    engineeringUseEligible: true,
    openRebuildRequired: true,
    warningFree: false,
    fullAssemblyValidationAllGreen: false,
    knownIssue: {
      component: '箱体右侧板焊接-1',
      type: 'Reference',
      code: 51,
      warning: true,
    },
    sourcePath: resolve(ROOT, 'workers/generated_models/review_generation_requests/v43-int-v37-760w-six-door-l642-r246-r1/16029_v37_760宽6门L642-R246_工程辅助模型_20260811.zip'),
    path: resolve(MODEL_DOWNLOAD_DIR, '16029_v37_760宽6门L642-R246_工程辅助模型_20260811.zip'),
  },
  {
    id: '16029-v43-v36-four-door-engineering-assistance-zip',
    title: '16029 V36 740W 四门 L66-R66 工程辅助模型',
    category: '历史已验证四门基线｜V36',
    description: '740W x 1917H x 550D、2列4门、L66-R66，单门307 x 908mm；四扇门各对应一个原生异形锁孔和一个锁舌，左右列门缝各7mm。原生SolidWorks 2020层级、递归引用、关闭保存重开与结构接口检查均已完成，可作为结构工程师继续深化的基础模型。',
    fileName: '16029_v36_740宽4门L66-R66_工程辅助模型_20260810.zip',
    sha256: 'DEF241BB9B0D530EB73130C5A3FCBDC22F206B6A59C65EBC5F6CE16B985C3AA4',
    expectedSizeBytes: 25096688,
    engineeringUseEligible: true,
    sourcePath: resolve(ROOT, 'workers/generated_models/review_generation_requests/v43-int-v36-four-door-l66-r66-r2/16029_v36_740宽4门L66-R66_工程辅助模型_20260810.zip'),
    path: resolve(MODEL_DOWNLOAD_DIR, '16029_v36_740宽4门L66-R66_工程辅助模型_20260810.zip'),
  },
  {
    id: '16029-v43-v35-one-door-one-lock-hole-rereview-zip',
    title: '16029 740W v43 V35 一门一锁孔工程辅助模型',
    category: '历史已验证六门基线｜V35',
    description: '这是740W、2列6门参数族的已验证生成模板和质量回归基准；当客户参数与它完全匹配时，系统可直接提供这份结构工程辅助基础模型。L/R隔板各3个原生异形槽和3个配套Ø5圆孔，共六槽六圆。',
    fileName: '16029_v35_一门一锁孔工程辅助模型_待工程确认_20260809.zip',
    sha256: '440596F0C9E321D0FF04EC978EEBAB31DDFA6D542E337BDA00C5A3B9701963FA',
    expectedSizeBytes: 24741785,
    engineeringUseEligible: true,
    sourcePath: resolve(ROOT, 'workers/generated_models/review_generation_requests/v43-int-v35-one-door-one-lock-hole-fix-r1/16029_v35_一门一锁孔工程辅助模型_待工程确认_20260809.zip'),
    path: resolve(MODEL_DOWNLOAD_DIR, '16029_v35_一门一锁孔工程辅助模型_待工程确认_20260809.zip'),
  },
  {
    id: '16029-v43-v34-lock-hole-center-rereview-zip',
    title: '16029 740W v43 v34 锁孔中心匹配修正复核候选',
    category: '历史工程反馈记录',
    description: '已被 V35 替代。V34 保留原 16029 跨折弯异形锁孔定义并补齐六处实际锁舌中心行，但 L/R 各仍有 8 行通用孔。V34-C1 后续已选择 remove_unused_rows，并在 V35 落实为一门一锁孔。V34 仅用于追溯，不作为当前结构工程辅助基础模型。',
    fileName: '16029_v34_锁孔中心匹配修正复核候选_非生产_20260809.zip',
    sha256: 'BFBC62CA9E714A135801BCEDB49480B8689885BB5243D956ECD112959985F06E',
    sourcePath: resolve(ROOT, 'workers/generated_models/review_generation_requests/v43-int-v34-lock-hole-center-fix-r1/16029_v34_锁孔中心匹配修正复核候选_非生产_20260809.zip'),
    path: resolve(MODEL_DOWNLOAD_DIR, '16029_v34_锁孔中心匹配修正复核候选_非生产_20260809.zip'),
  },
  {
    id: '16029-v43-v33-engineering-feedback-rereview-zip',
    title: '16029 740W v43 v33 底座接口与层板加强筋修正复核候选',
    category: '历史工程反馈记录',
    description: '已被V34替代。V33完成底座双孔组、层板双加强筋、底部Ø3.5连接孔及无效X=±74.8浮动锁孔基准清理；后续两条锁孔反馈已在V34中按原生标准槽形补齐实际中心行。V33仅用于追溯，不作为当前结构工程辅助基础模型。',
    fileName: '16029_v33_底座接口与层板加强筋修正复核候选_非生产_20260805.zip',
    sha256: 'EFCC48EEEB2C6C6FA1C815A229587AE8D406BCDCF160A51550CC61D08F758BE9',
    sourcePath: resolve(ROOT, 'workers/generated_models/review_generation_requests/v43-int-v33-engineering-feedback-fix-r1/16029_v33_底座接口与层板加强筋修正复核候选_非生产_20260805.zip'),
    path: resolve(MODEL_DOWNLOAD_DIR, '16029_v33_底座接口与层板加强筋修正复核候选_非生产_20260805.zip'),
  },
  {
    id: '16029-v43-v32-lockdatum-crossbar-rereview-zip',
    title: '16029 740W v43 v32 锁孔X向回归修正与横档确认复核候选',
    category: '历史工程反馈记录',
    description: '已被V33替代。V32曾放置6个X=±74.8浮动锁孔基准；后续工程反馈和边界复核确认其左右位置错误且落在箱体竖隔板材料范围外，不能作为加工依据。V32仅用于追溯，不作为当前结构工程辅助基础模型。',
    fileName: '16029_v32_锁孔X向回归修正与横档确认复核候选_非生产_20260805.zip',
    sha256: '36BAA5091BD6B3D37F17D909A7FDE9C112CFF90F57E0169376DDDC9415210254',
    sourcePath: resolve(ROOT, 'workers/generated_models/review_generation_requests/v43-int-v32-lockdatum-x-regression-fix-r1/16029_v32_锁孔X向回归修正与横档确认复核候选_非生产_20260805.zip'),
    path: resolve(MODEL_DOWNLOAD_DIR, '16029_v32_锁孔X向回归修正与横档确认复核候选_非生产_20260805.zip'),
  },
  {
    id: '16029-v43-v31-engineering-feedback-rereview-zip',
    title: '16029 740W v43 v31 门框与底座联动修正复核候选',
    category: '历史工程反馈记录',
    description: 'V30 的可确定反馈已写入隔离的 SolidWorks 2020 原生模型：调整脚保持 X=±335 mm，底板敲落孔和加强筋 Ø15 孔恢复 X=±295 mm；门框仅保留 4 根与 L642/R246 门分界匹配的横隔板；恢复 6 个随锁舌定位的承接基准件。包内 77 个原生 CAD、160 个递归组件；缺失和包外引用为 0。最终锁孔加工定义、底部安装孔比例基准和 Ø3.2/Ø3.5 工艺孔径归并为 3 组带截图的工程确认问题。',
    fileName: '16029_v31_门框底座联动修正复核候选_非生产_20260804.zip',
    sha256: '441F32DDC57E0CE871734D6B31C582ABF7F31E1557632AF6A7E3286624C89F24',
    sourcePath: resolve(ROOT, 'workers/generated_models/review_generation_requests/v43-int-v31-engineering-feedback-fix-r1/16029_v31_门框底座联动修正复核候选_非生产_20260804.zip'),
    path: resolve(MODEL_DOWNLOAD_DIR, '16029_v31_门框底座联动修正复核候选_非生产_20260804.zip'),
  },
  {
    id: '16029-v43-v30-confirmed-linkage-rereview-zip',
    title: '16029 740W v43 v30 已确认联动修正与待确认问题复核候选',
    category: '历史工程反馈记录',
    description: 'v29 的 7 条 P1 反馈已完成可确定部分：上盖壳体底板与门框上 4 组孔同步为 200 mm 节距，门框下与底座底板 4 组孔也同步为 200 mm 节距。包内 75 个原生 CAD、154 个递归组件；缺失和包外引用为 0。其余 4 条反馈归并为 3 组带原截图的工程确认问题。',
    fileName: '16029_v30_已确认联动修正与待确认问题复核候选_非生产_20260730.zip',
    sha256: '314102A28FB01C113CC4535C863DA5F8F7C28162A94D6617A3CE9A5122106359',
    sourcePath: resolve(ROOT, 'workers/generated_models/review_generation_requests/v43-int-v30-confirmed-linkage-fix-r1/16029_v30_已确认联动修正与待确认问题复核候选_非生产_20260730.zip'),
    path: resolve(MODEL_DOWNLOAD_DIR, '16029_v30_已确认联动修正与待确认问题复核候选_非生产_20260730.zip'),
  },
  {
    id: '16029-v43-v29-three-feedback-native-rereview-zip',
    title: '16029 740W v43 v29 三项反馈原生修正复核候选',
    category: '历史工程反馈记录',
    description: 'v28 收到的 3 条 P1 反馈已完成处理：门框上拉铆孔距改为 200 mm，底座底板 4 个敲落孔中心移至 X=±335 mm；底座底板与门框下指定拉铆孔轴复核为 0 mm 偏差，孔径 Ø3.2/Ø3.5 待工程确认。包内含 85 个原生 CAD 文件和 154 个递归组件；缺失、包外引用及 3 组指定实体干涉均为 0，只用于结构复核。',
    fileName: '16029_v29_三项反馈原生修正复核候选_非生产_20260730.zip',
    sha256: 'A81FAEA5916115C7B94F7743EA1E97D64113887C2249E436CDE7C0DC23ECB706',
    sourcePath: resolve(ROOT, 'workers/generated_models/review_generation_requests/v43-int-v29-three-feedback-fix-r1/16029_v29_三项反馈原生修正复核候选_非生产_20260730.zip'),
    path: resolve(MODEL_DOWNLOAD_DIR, '16029_v29_三项反馈原生修正复核候选_非生产_20260730.zip'),
  },
  {
    id: '16029-v43-v28-five-feedback-native-rereview-zip',
    title: '16029 740W v43 v28 五项反馈原生修正复核候选',
    category: '历史工程反馈记录',
    description: 'v27 收到的 5 条 P1 反馈均已写入隔离的 SolidWorks 2020 原生 CAD：底座孔位、上盖压铆螺钉、衣杆及支架取消、右层板两排及内外两侧卡接均已处理。包内含 85 个原生 CAD 文件和 154 个递归组件；缺失、包外引用及指定实体干涉均为 0，只用于结构复核。',
    fileName: '16029_v28_五项反馈原生修正复核候选_非生产_20260729.zip',
    sha256: '3526FF9E4B7EA384C88CC3A7DA583566D927816C348CBB70D09F94A365DE979B',
    sourcePath: resolve(ROOT, 'workers/generated_models/review_generation_requests/v43-int-v28-five-feedback-fix-r1/16029_v28_五项反馈原生修正复核候选_非生产_20260729.zip'),
    path: resolve(MODEL_DOWNLOAD_DIR, '16029_v28_五项反馈原生修正复核候选_非生产_20260729.zip'),
  },
  {
    id: '16029-v43-v27-late-feedback-geometry-rereview-zip',
    title: '16029 740W v43 v27 末轮反馈几何复核候选',
    category: '历史工程反馈记录',
    description: '追踪 v25 共 13 条反馈：11 条已落实到原生 SolidWorks 2020 隔离基线，另有 R6 右门板定位孔和右层板贴合两项独立 Parasolid 几何候选，尚未写回原生 CAD。包内含 85 个原生 CAD 文件、166 个递归组件和 153/153 条成功引用替换；只用于结构复核。',
    fileName: '16029_v27_末轮反馈几何复核候选_非生产_20260727.zip',
    sha256: 'A9A9A0B1EC158F9831D6E1656D084A4CEB69AE64D8182F20F9C59F4C9F430E2A',
    sourcePath: resolve(ROOT, 'workers/generated_models/review_generation_requests/v43-int-v27-late-feedback-fix-r1/16029_v27_末轮反馈几何复核候选_非生产_20260727.zip'),
    path: resolve(MODEL_DOWNLOAD_DIR, '16029_v27_末轮反馈几何复核候选_非生产_20260727.zip'),
  },
  {
    id: '16029-v43-v26-native-hierarchy-feedback-rereview-zip',
    title: '16029 740W v43 v26 原生层级反馈修复复核候选',
    category: '历史工程反馈记录',
    description: '按 v25 的 9 条反馈完成原生层级修复。包内含 85 个 SolidWorks 2020 CAD 文件和 166 个递归组件；154/154 条引用已重定向，6 个门装配、4 个层板模块、单一维护条及 R2/R4/R6 锁钩垫板均已纳入复核。首次打开和关闭后重开均无包外、缺失或路径错配组件。',
    fileName: '16029_v26_原生层级反馈修复复核候选_非生产_20260727.zip',
    sha256: '5E780060F751F3C0E1B12B666545C1FAB302E276D0CC45450E4F26F84B93C5AB',
    sourcePath: resolve(ROOT, 'workers/generated_models/review_generation_requests/v43-int-v26-structure-hierarchy-feedback-fix/16029_v26_原生层级反馈修复复核候选_非生产_20260727.zip'),
    path: resolve(MODEL_DOWNLOAD_DIR, '16029_v26_原生层级反馈修复复核候选_非生产_20260727.zip'),
  },
  {
    id: '16029-v43-v25-engineering-feedback-rereview-zip',
    title: '16029 740W v43 v25 工程反馈修复复核候选',
    category: '历史工程反馈记录',
    description: '针对 v24 平台 9 条反馈完成修复：重复加强板和调节脚已清理，锁孔与锁舌 X 向对齐，右门 R2/R4/R6 锁钩垫板方向修正，层板贴平、侧板中心缝和上盖两侧接口已复核。SolidWorks 2020 原生装配 167 个组件，反馈门禁 20/20 通过。',
    fileName: '16029_v25_工程反馈修复复核候选_非生产_20260717.zip',
    sourcePath: resolve(ROOT, 'workers/generated_models/review_generation_requests/v43-int-v25-engineering-feedback-fix/16029_v25_工程反馈修复复核候选_非生产_20260717.zip'),
    path: resolve(MODEL_DOWNLOAD_DIR, '16029_v25_工程反馈修复复核候选_非生产_20260717.zip'),
  },
  {
    id: '16029-v43-v24-native-sheetmetal-engineering-review-zip',
    title: '16029 740W v43 v24 原生钣金工程审图候选',
    category: '历史工程审图记录',
    description: '7 件整改件已恢复为 SolidWorks 2020 原生单实体钣金，SheetMetal/FlatPattern 与特征树检查通过；T01-T08 精确接口回归 21/21 通过、实体干涉为 0。保留给结构工程师作历史结构对照。',
    fileName: '16029_v24_原生钣金工程审图候选_非生产_20260715.zip',
    sourcePath: resolve(ROOT, 'workers/generated_models/review_generation_requests/v43-int-v24-engineering-feedback-fix/16029_v24_原生钣金工程审图候选_非生产_20260715.zip'),
    path: resolve(MODEL_DOWNLOAD_DIR, '16029_v24_原生钣金工程审图候选_非生产_20260715.zip'),
  },
  {
    id: '16029-v43-internal-sheetmetal-v23-controlled-candidate-zip',
    title: '16029 740W v43 v23 受控候选复核包',
    category: '历史结构验证资料',
    description: 'v43-int-v23-all-sources-isolated：精确结构检查 11/11 通过，302 个受控源文件未改动；39 个模块与 7 个恢复放置均使用候选本地副本，最终装配外部引用为 0。保留给结构工程师作历史对照。',
    fileName: 'review_generation_v43-int-v23-all-sources-isolated_solidworks2020_full_assembly.zip',
    sourcePath: resolve(ROOT, 'workers/generation_logs/review_generation_v43-int-v23-all-sources-isolated_solidworks2020_full_assembly.zip'),
    path: resolve(MODEL_DOWNLOAD_DIR, 'review_generation_v43-int-v23-all-sources-isolated_solidworks2020_full_assembly.zip'),
  },
  {
    id: '16029-v43-internal-sheetmetal-v23-pre-signoff-review-zip',
    title: '16029 740W v43 v23 历史结构验证资料',
    category: '历史工程记录',
    description: '包含 6 张隐藏柜门后的内部结构视图、历史源文件几何指标对比、零写入 manifest 和工程确认清单，供结构工程师追溯和对照。',
    fileName: 'review_generation_v43-int-v23-all-sources-isolated_pre_signoff_review.zip',
    sourcePath: resolve(ROOT, 'workers/generation_logs/review_generation_v43-int-v23-all-sources-isolated_pre_signoff_review.zip'),
    path: resolve(MODEL_DOWNLOAD_DIR, 'review_generation_v43-int-v23-all-sources-isolated_pre_signoff_review.zip'),
  },
  {
    id: '16029-v43-internal-sheetmetal-v23-engineering-signoff-template',
    title: '16029 740W v43 v23 结构工程确认记录模板',
    category: '历史工程记录模板',
    description: '模板绑定当前 v23 摘要、结构检查、源完整性、来源报告和两个 ZIP 的 SHA256。模板仅用于历史追溯，不作为当前结构工程辅助基础模型。',
    fileName: 'locker_16029_v23_engineering_signoff.template.json',
    sourcePath: resolve(ROOT, 'data/locker_16029_v23_engineering_signoff.template.json'),
    path: resolve(MODEL_DOWNLOAD_DIR, 'locker_16029_v23_engineering_signoff.template.json'),
  },
  {
    id: 'lms-gold-variable',
    title: '800W LMS 历史参考包',
    category: '保留参考',
    description: '旧 800W gold-variable LMS 审核包，保留作历史对照；不是当前 v43 内部钣金交付入口。',
    fileName: '16029_800W_LMS_GOLD_VARIABLE_REVIEW_20260528.zip',
    sourcePath: resolve(ROOT, 'workers/handoffs/16029_800W_LMS_GOLD_VARIABLE_REVIEW_20260528.zip'),
    path: resolve(MODEL_DOWNLOAD_DIR, '16029_800W_LMS_GOLD_VARIABLE_REVIEW_20260528.zip'),
  },
  {
    id: 'sml-gold-variable',
    title: '800W SML 历史参考包',
    category: '保留参考',
    description: '旧 800W gold-variable SML 审核包，保留作历史对照；不是当前 v43 内部钣金交付入口。',
    fileName: '16029_800W_SML_GOLD_VARIABLE_REVIEW_20260528.zip',
    sourcePath: resolve(ROOT, 'workers/handoffs/16029_800W_SML_GOLD_VARIABLE_REVIEW_20260528.zip'),
    path: resolve(MODEL_DOWNLOAD_DIR, '16029_800W_SML_GOLD_VARIABLE_REVIEW_20260528.zip'),
  },
  {
    id: 'dual-gold-variable',
    title: '800W LMS/SML 历史合包',
    category: '保留参考',
    description: '旧 LMS/SML 双方案合包，保留作历史对照；不是当前 v43 内部钣金交付入口。',
    fileName: '16029_800W_DUAL_GOLD_VARIABLE_REVIEW_20260528.zip',
    sourcePath: resolve(ROOT, 'workers/handoffs/16029_800W_DUAL_GOLD_VARIABLE_REVIEW_20260528.zip'),
    path: resolve(MODEL_DOWNLOAD_DIR, '16029_800W_DUAL_GOLD_VARIABLE_REVIEW_20260528.zip'),
  },
]

const CURRENT_REVIEW_ASSET_ID = '16029-v43-v37-760w-six-door-engineering-assistance-zip'
const V36_REVIEW_ASSET_ID = '16029-v43-v36-four-door-engineering-assistance-zip'
const V35_REVIEW_ASSET_ID = '16029-v43-v35-one-door-one-lock-hole-rereview-zip'
const STARTUP_PRIORITY_ASSET_IDS = new Set([
  CURRENT_REVIEW_ASSET_ID,
  V36_REVIEW_ASSET_ID,
  V35_REVIEW_ASSET_ID,
])
const RECORD_TYPE_LABELS = {
  structure_issue: '结构问题',
  efficiency_validation: '生成模型应用效果记录',
}
const TASK_KIND_LABELS = {
  real_order: '真实订单',
  typical_change: '典型结构变更',
}
const DELIVERABLE_STATUS_LABELS = {
  completed: '已完成',
  not_completed: '未完成',
  not_required: '本任务不要求',
}
const EFFICIENCY_PROBLEM_CATEGORY_LABELS = {
  no_blocking_problem: '没有阻塞问题',
  reference_or_rebuild: '引用或重建',
  parameter_adaptation: '参数适配',
  geometry_or_sheetmetal: '几何或钣金结构',
  assembly_or_mate: '装配或配合',
  drawing_output: '工程图输出',
  bom_output: 'BOM输出',
  missing_or_incorrect_component: '零件缺失或错误',
  performance_or_operation: '性能或操作体验',
  other: '其他',
}
const ISSUE_CATEGORY_LABELS = {
  lock_common_datum_and_engagement: '锁舌/锁孔/定位基准与啮合',
  shelf_front_frame_interface: '层板与前框定位接口',
  partition_reinforcement_and_weldability: '竖隔板加强与焊接可达性',
  external_through_hole_disposition: '非预期外穿孔',
  door_gap_sag_and_collision: '门缝/下垂/碰撞',
  sheetmetal_process_and_tolerance: '钣金工艺与公差',
  touched_source_file_disposition: '历史受触碰源文件处置',
  other_structure_issue: '其他结构问题',
}
const SEVERITY_LABELS = { P0: 'P0 阻断/安全', P1: 'P1 样机前必须修改', P2: 'P2 优化项' }
const DECISION_LABELS = { pass: '通过反馈检查', needs_changes: '需修改', blocked: '阻塞', cannot_judge: '无法判断' }
const byId = new Map(assets.map((asset) => [asset.id, asset]))
const assetIntegrityCache = new Map()

function ensureDirs() {
  mkdirSync(DATA_DIR, { recursive: true })
  mkdirSync(REVIEW_STORAGE_ROOT, { recursive: true })
  mkdirSync(MODEL_DOWNLOAD_DIR, { recursive: true })
  mkdirSync(FEEDBACK_DIR, { recursive: true })
  mkdirSync(GENERATION_REQUEST_DIR, { recursive: true })
}

function fileSha256(path) {
  return createHash('sha256').update(readFileSync(path)).digest('hex').toUpperCase()
}

function fileDescriptorSha256(fd, sizeBytes) {
  const hash = createHash('sha256')
  const buffer = Buffer.allocUnsafe(1024 * 1024)
  let position = 0
  while (position < sizeBytes) {
    const bytesRead = readSync(fd, buffer, 0, Math.min(buffer.length, sizeBytes - position), position)
    if (bytesRead <= 0) throw new Error('asset changed while hashing')
    hash.update(buffer.subarray(0, bytesRead))
    position += bytesRead
  }
  return hash.digest('hex').toUpperCase()
}

function openFreshExactAssetSnapshot(asset) {
  let fd = null
  try {
    fd = openSync(asset.path, 'r')
  } catch (error) {
    if (error?.code === 'ENOENT') {
      return { ok: false, reason: 'missing', stat: null, fd: null, actualSha256: '' }
    }
    throw error
  }

  try {
    const stat = fstatSync(fd)
    const expectedSizeBytes = Number(asset.expectedSizeBytes || 0)
    const expectedSha256 = String(asset.sha256 || '').toUpperCase()
    let reason = ''
    let actualSha256 = ''
    if (!stat.isFile()) reason = 'not_regular_file'
    else if (expectedSizeBytes && stat.size !== expectedSizeBytes) reason = 'size_mismatch'
    else {
      actualSha256 = fileDescriptorSha256(fd, stat.size)
      if (!expectedSha256 || actualSha256 !== expectedSha256) reason = expectedSha256 ? 'sha256_mismatch' : 'sha256_unpinned'
    }
    return { ok: !reason, reason, stat, fd, actualSha256 }
  } catch (error) {
    closeSync(fd)
    throw error
  }
}

function closeExactAssetSnapshot(snapshot) {
  if (!snapshot || !Number.isInteger(snapshot.fd)) return
  const fd = snapshot.fd
  snapshot.fd = null
  closeSync(fd)
}

function freshExactAssetIntegrity(asset) {
  const snapshot = openFreshExactAssetSnapshot(asset)
  try {
    return {
      available: snapshot.ok,
      integrityVerified: snapshot.ok,
      integrityError: snapshot.reason,
      sizeBytes: snapshot.stat?.size ?? null,
      modifiedAt: snapshot.stat?.mtime?.toISOString?.() || null,
      actualSha256: snapshot.actualSha256,
    }
  } finally {
    closeExactAssetSnapshot(snapshot)
  }
}

function sharedAssetIntegrity(asset) {
  if (!existsSync(asset.path)) {
    assetIntegrityCache.delete(asset.id)
    return { ok: false, reason: 'missing', stat: null }
  }
  const stat = statSync(asset.path)
  const expectedSha256 = String(asset.sha256 || '').toUpperCase()
  const expectedSizeBytes = Number(asset.expectedSizeBytes || 0)
  const cacheKey = `${stat.size}:${stat.mtimeMs}:${expectedSizeBytes}:${expectedSha256}`
  const cached = assetIntegrityCache.get(asset.id)
  if (cached?.key === cacheKey) return { ...cached.result, stat }

  let reason = ''
  if (expectedSizeBytes && stat.size !== expectedSizeBytes) reason = 'size_mismatch'
  else if (expectedSha256 && fileSha256(asset.path) !== expectedSha256) reason = 'sha256_mismatch'
  const result = { ok: !reason, reason, stat }
  assetIntegrityCache.set(asset.id, { key: cacheKey, result: { ok: result.ok, reason } })
  return result
}

function syncAssetsToSharedStorage(assetIds = null) {
  if (process.env.STUDIO_REVIEW_SKIP_ASSET_SYNC === '1') return
  const selectedAssets = assetIds
    ? assets.filter((asset) => assetIds.has(asset.id))
    : assets
  for (const asset of selectedAssets) {
    const expectedSha256 = String(asset.sha256 || '').toUpperCase()
    const expectedSizeBytes = Number(asset.expectedSizeBytes || 0)
    const existingTargetIntegrity = sharedAssetIntegrity(asset)
    if (expectedSha256 && existingTargetIntegrity.ok) {
      asset.integrityVerified = true
      continue
    }
    if (!asset.sourcePath || !existsSync(asset.sourcePath)) {
      asset.integrityVerified = existingTargetIntegrity.ok && asset.sha256 ? true : null
      if (!existingTargetIntegrity.ok && STARTUP_PRIORITY_ASSET_IDS.has(asset.id)) {
        throw new Error(`current shared asset integrity mismatch: ${asset.fileName}`)
      }
      continue
    }
    const source = statSync(asset.sourcePath)
    const target = existsSync(asset.path) ? statSync(asset.path) : null
    if (expectedSizeBytes && source.size !== expectedSizeBytes) {
      throw new Error(`source asset size mismatch: ${asset.fileName}`)
    }
    if (expectedSha256 && fileSha256(asset.sourcePath) !== expectedSha256) {
      throw new Error(`source asset SHA-256 mismatch: ${asset.fileName}`)
    }
    const targetHashMatches = !expectedSha256 || (target && fileSha256(asset.path) === expectedSha256)
    if (target && target.size === source.size && target.mtimeMs >= source.mtimeMs && targetHashMatches) {
      asset.integrityVerified = expectedSha256 ? true : null
      continue
    }
    copyFileSync(asset.sourcePath, asset.path)
    assetIntegrityCache.delete(asset.id)
    if (statSync(asset.path).size !== source.size) throw new Error(`shared asset copy size mismatch: ${asset.fileName}`)
    if (expectedSha256 && fileSha256(asset.path) !== expectedSha256) {
      throw new Error(`shared asset SHA-256 mismatch: ${asset.fileName}`)
    }
    asset.integrityVerified = expectedSha256 ? true : null
  }
}

function readJson(path, fallback) {
  try {
    return JSON.parse(readFileSync(path, 'utf8'))
  } catch {
    return fallback
  }
}

function writeJson(path, value) {
  const target = resolve(path)
  mkdirSync(dirname(target), { recursive: true })
  const temporary = `${target}.tmp-${process.pid}-${randomBytes(6).toString('hex')}`
  try {
    writeFileSync(temporary, `${JSON.stringify(value, null, 2)}\n`, 'utf8')
    renameSync(temporary, target)
  } finally {
    rmSync(temporary, { force: true })
  }
}

function htmlEscape(value) {
  return String(value ?? '').replace(/[&<>"']/g, (char) => (
    { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[char]
  ))
}

function normalizeUsername(value) {
  return String(value || '').trim().toLowerCase().replace(/[^a-z0-9._@-]/g, '')
}

function readUsers() {
  const db = readJson(USER_DB_PATH, { users: [] })
  return Array.isArray(db.users) ? db : { users: [] }
}

function saveUsers(db) {
  writeJson(USER_DB_PATH, db)
}

function hashPassword(password, salt, iterations = PASSWORD_ITERATIONS) {
  return pbkdf2Sync(String(password), Buffer.from(salt, 'hex'), iterations, 32, 'sha256').toString('hex')
}

function verifyPassword(user, password) {
  const expected = Buffer.from(user.hash, 'hex')
  const actual = Buffer.from(hashPassword(password, user.salt, user.iterations || PASSWORD_ITERATIONS), 'hex')
  return expected.length === actual.length && timingSafeEqual(expected, actual)
}

function createUser(username, password) {
  const salt = randomBytes(16).toString('hex')
  return {
    username,
    salt,
    hash: hashPassword(password, salt),
    iterations: PASSWORD_ITERATIONS,
    createdAt: new Date().toISOString(),
  }
}

function parseCookies(request) {
  return Object.fromEntries(
    String(request.headers.cookie || '')
      .split(';')
      .map((part) => part.trim())
      .filter(Boolean)
      .map((part) => {
        const index = part.indexOf('=')
        if (index < 0) return [part, '']
        try {
          return [part.slice(0, index), decodeURIComponent(part.slice(index + 1))]
        } catch {
          return [part.slice(0, index), '']
        }
      }),
  )
}

function createSession(response, username) {
  const token = randomBytes(24).toString('hex')
  sessions.set(token, { username, expiresAt: Date.now() + SESSION_TTL_MS })
  response.setHeader('Set-Cookie', `${SESSION_COOKIE}=${encodeURIComponent(token)}; HttpOnly; Path=/; SameSite=Lax; Max-Age=${Math.floor(SESSION_TTL_MS / 1000)}`)
}

function currentUser(request) {
  const token = parseCookies(request)[SESSION_COOKIE]
  if (!token) return null
  const session = sessions.get(token)
  if (!session || session.expiresAt < Date.now()) {
    sessions.delete(token)
    return null
  }
  session.expiresAt = Date.now() + SESSION_TTL_MS
  return session.username
}

function sendHtml(response, status, html) {
  response.writeHead(status, { 'Content-Type': 'text/html; charset=utf-8', 'Cache-Control': 'no-store' })
  response.end(html)
}

function sendJson(response, status, body) {
  response.writeHead(status, { 'Content-Type': 'application/json; charset=utf-8', 'Cache-Control': 'no-store' })
  response.end(JSON.stringify(body, null, 2))
}

function redirect(response, location) {
  response.writeHead(302, { Location: location })
  response.end()
}

function readBody(request, maxBytes = MAX_BODY_BYTES) {
  return new Promise((resolveBody, reject) => {
    const chunks = []
    let total = 0
    request.on('data', (chunk) => {
      total += chunk.length
      if (total > maxBytes) {
        reject(new Error('request body too large'))
        request.destroy()
        return
      }
      chunks.push(chunk)
    })
    request.on('end', () => resolveBody(Buffer.concat(chunks)))
    request.on('error', reject)
  })
}

async function readJsonBody(request, maxBytes = MAX_BODY_BYTES) {
  const body = (await readBody(request, maxBytes)).toString('utf8')
  try {
    const payload = JSON.parse(body)
    if (!payload || typeof payload !== 'object' || Array.isArray(payload)) {
      const error = new Error('JSON body must be an object')
      error.statusCode = 400
      throw error
    }
    return payload
  } catch (error) {
    if (error.statusCode) throw error
    const parseError = new Error('invalid JSON body')
    parseError.statusCode = 400
    throw parseError
  }
}

function formatBytes(value) {
  if (value < 1024) return `${value} B`
  const kb = value / 1024
  if (kb < 1024) return `${kb.toFixed(kb >= 100 ? 0 : 1)} KB`
  const mb = kb / 1024
  if (mb < 1024) return `${mb.toFixed(mb >= 100 ? 0 : 1)} MB`
  return `${(mb / 1024).toFixed(1)} GB`
}

function visibleAssetFileName(fileName) {
  return String(fileName || '')
    .replace(/_?非生产_?/g, '_')
    .replace(/_{2,}/g, '_')
}

function contentTypeForPath(path) {
  const extension = extname(path).toLowerCase()
  if (extension === '.json') return 'application/json; charset=utf-8'
  if (extension === '.png') return 'image/png'
  if (extension === '.jpg' || extension === '.jpeg') return 'image/jpeg'
  if (extension === '.webp') return 'image/webp'
  if (extension === '.pdf') return 'application/pdf'
  if (extension === '.zip') return 'application/zip'
  return 'application/octet-stream'
}

function safeResponseFilename(value, fallback = 'download') {
  const name = basename(String(value || fallback)).replace(/["\r\n]/g, '_')
  return name || fallback
}

function attachmentContentDisposition(value, fallback = 'download') {
  const name = safeResponseFilename(value, fallback)
  const asciiFallback = name.replace(/[^\x20-\x7e]/g, '_') || fallback
  const encoded = encodeURIComponent(name).replace(/['()*]/g, (char) => `%${char.charCodeAt(0).toString(16).toUpperCase()}`)
  return `attachment; filename="${asciiFallback}"; filename*=UTF-8''${encoded}`
}

function inlineContentDisposition(value, fallback = 'preview') {
  const name = safeResponseFilename(value, fallback)
  const asciiFallback = name.replace(/[^\x20-\x7e]/g, '_') || fallback
  const encoded = encodeURIComponent(name).replace(/['()*]/g, (char) => `%${char.charCodeAt(0).toString(16).toUpperCase()}`)
  return `inline; filename="${asciiFallback}"; filename*=UTF-8''${encoded}`
}

function assetInfo(asset) {
  const integrity = sharedAssetIntegrity(asset)
  if (!integrity.stat) {
    return { ...asset, available: false, integrityVerified: false, integrityError: 'missing', sizeBytes: null, modifiedAt: null }
  }
  return {
    ...asset,
    available: integrity.ok,
    integrityVerified: asset.sha256 ? integrity.ok : null,
    integrityError: integrity.reason || '',
    sizeBytes: integrity.stat.size,
    modifiedAt: integrity.stat.mtime.toISOString(),
  }
}

function listingAssetInfo(asset) {
  if (STARTUP_PRIORITY_ASSET_IDS.has(asset.id)) return assetInfo(asset)

  const targetExists = existsSync(asset.path)
  const sourceExists = !targetExists && asset.sourcePath && existsSync(asset.sourcePath)
  const availablePath = targetExists ? asset.path : (sourceExists ? asset.sourcePath : '')
  if (!availablePath) {
    return {
      ...asset,
      available: false,
      integrityVerified: false,
      integrityError: 'missing',
      integrityCheckDeferred: true,
      sizeBytes: null,
      modifiedAt: null,
    }
  }

  const stat = statSync(availablePath)
  let integrityVerified = null
  let integrityError = ''
  if (targetExists) {
    const expectedSha256 = String(asset.sha256 || '').toUpperCase()
    const expectedSizeBytes = Number(asset.expectedSizeBytes || 0)
    const cacheKey = `${stat.size}:${stat.mtimeMs}:${expectedSizeBytes}:${expectedSha256}`
    const cached = assetIntegrityCache.get(asset.id)
    if (cached?.key === cacheKey) {
      integrityVerified = asset.sha256 ? cached.result.ok : null
      integrityError = cached.result.reason || ''
    }
  }
  return {
    ...asset,
    available: stat.isFile() && integrityVerified !== false,
    integrityVerified,
    integrityError,
    integrityCheckDeferred: integrityVerified === null,
    sizeBytes: stat.size,
    modifiedAt: stat.mtime.toISOString(),
  }
}

function publicAssetInfo(asset) {
  const {
    path,
    sourcePath,
    productionReleaseEligible,
    releaseReady,
    ...publicAsset
  } = listingAssetInfo(asset)
  return { ...publicAsset, fileName: visibleAssetFileName(publicAsset.fileName) }
}

function orderedAssets() {
  const priority = new Map([
    [CURRENT_REVIEW_ASSET_ID, 0],
    [V36_REVIEW_ASSET_ID, 1],
    [V35_REVIEW_ASSET_ID, 2],
    ['16029-v43-v34-lock-hole-center-rereview-zip', 3],
    ['16029-v43-v33-engineering-feedback-rereview-zip', 4],
    ['16029-v43-v32-lockdatum-crossbar-rereview-zip', 5],
    ['16029-v43-v31-engineering-feedback-rereview-zip', 6],
    ['16029-v43-v30-confirmed-linkage-rereview-zip', 7],
    ['16029-v43-v29-three-feedback-native-rereview-zip', 8],
    ['16029-v43-v28-five-feedback-native-rereview-zip', 9],
    ['16029-v43-v27-late-feedback-geometry-rereview-zip', 10],
    ['16029-v43-v26-native-hierarchy-feedback-rereview-zip', 11],
    ['16029-v43-v25-engineering-feedback-rereview-zip', 12],
    ['16029-v43-v24-native-sheetmetal-engineering-review-zip', 13],
    ['16029-v43-internal-sheetmetal-v23-controlled-candidate-zip', 14],
    ['16029-v43-internal-sheetmetal-v23-pre-signoff-review-zip', 15],
    ['16029-v43-internal-sheetmetal-v23-engineering-signoff-template', 16],
    ['16029-v43-internal-sheetmetal-lockfix-zip', 17],
  ])
  return [...assets].sort((left, right) => (priority.get(left.id) ?? 17) - (priority.get(right.id) ?? 17))
}

function readFeedbackIndex() {
  const index = readJson(FEEDBACK_INDEX_PATH, { feedback: [] })
  return Array.isArray(index.feedback) ? index : { feedback: [] }
}

function currentRoundFeedback(username = null) {
  return readFeedbackIndex().feedback
    .filter((item) => item.reviewRoundId === reviewRound.id)
    .filter((item) => !username || item.username === username)
    .slice(0, 40)
}

function historicalRoundFeedback() {
  return readFeedbackIndex().feedback
    .filter((item) => item.reviewRoundId !== reviewRound.id)
    .slice(0, 100)
    .map((item) => ({ ...item, attachments: [] }))
}

function readClarificationQuestions() {
  const payload = readJson(CLARIFICATION_QUESTIONS_PATH, { questions: [] })
  if (payload.review_round_id !== reviewRound.id) return []
  return Array.isArray(payload.questions) ? payload.questions : []
}

function clarificationImageInfo(questionId, imageIndex) {
  const question = readClarificationQuestions().find((item) => item.id === questionId)
  const image = question?.images?.[imageIndex]
  if (!question || !image) return null
  const fileName = basename(String(image.file_name || ''))
  if (!fileName || fileName !== String(image.file_name || '')) return null
  const filePath = resolve(CLARIFICATION_IMAGE_DIR, fileName)
  if (!isUnderRoot(filePath, CLARIFICATION_IMAGE_DIR) || !existsSync(filePath)) return null
  return { question, image, filePath }
}

function isLoopbackRequest(request) {
  const address = String(request.socket.remoteAddress || '').toLowerCase()
  return address === '127.0.0.1' || address === '::1' || address === '::ffff:127.0.0.1'
}

function localFeedbackAudit() {
  const feedback = readFeedbackIndex().feedback
    .map((item) => ({
      id: item.id,
      submittedAt: item.submittedAt,
      reviewRoundId: item.reviewRoundId,
      reviewTarget: item.reviewTarget,
      reviewTargetLabel: item.reviewTargetLabel,
      reviewerName: item.reviewerName,
      discipline: item.discipline,
      schema: item.schema || 'winnsen.review.structure_issue.legacy',
      recordType: item.recordType || 'structure_issue',
      recordTypeLabel: item.recordTypeLabel || RECORD_TYPE_LABELS.structure_issue,
      severity: item.severity,
      decision: item.decision,
      issueCategory: item.issueCategory,
      componentName: item.componentName,
      modelLocation: item.modelLocation,
      summary: item.currentProblem || item.summary,
      expectedResult: item.expectedResult,
      acceptanceCriteria: item.acceptanceCriteria,
      clarificationId: item.clarificationId,
      clarificationOption: item.clarificationOption,
      efficiencyValidation: item.efficiencyValidation,
      attachmentCount: Number(item.attachmentCount || 0),
      folderName: item.folderName,
    }))
    .sort((left, right) => String(right.submittedAt || '').localeCompare(String(left.submittedAt || '')))

  const indexedFolderNames = new Set(feedback.map((item) => item.folderName).filter(Boolean))
  let physicalFolderNames = []
  let folderAuditError = ''
  try {
    physicalFolderNames = readdirSync(FEEDBACK_DIR, { withFileTypes: true })
      .filter((entry) => entry.isDirectory() && /^V\d+-Q-/i.test(entry.name))
      .map((entry) => entry.name)
      .sort()
  } catch (error) {
    folderAuditError = error instanceof Error ? error.message : String(error)
  }
  const physicalFolderNameSet = new Set(physicalFolderNames)

  const byRound = Object.values(feedback.reduce((groups, item) => {
    const roundId = item.reviewRoundId || 'unknown'
    if (!groups[roundId]) {
      groups[roundId] = { reviewRoundId: roundId, count: 0, latestSubmittedAt: '', ids: [] }
    }
    groups[roundId].count += 1
    groups[roundId].ids.push(item.id)
    if (String(item.submittedAt || '') > groups[roundId].latestSubmittedAt) {
      groups[roundId].latestSubmittedAt = item.submittedAt
    }
    return groups
  }, {})).sort((left, right) => String(right.latestSubmittedAt).localeCompare(String(left.latestSubmittedAt)))

  return {
    generatedAt: new Date().toISOString(),
    currentReviewRoundId: reviewRound.id,
    totalSubmissionCount: feedback.length,
    currentRoundSubmissionCount: feedback.filter((item) => item.reviewRoundId === reviewRound.id).length,
    physicalFeedbackFolderCount: physicalFolderNames.length,
    indexRowsWithoutFolder: feedback
      .filter((item) => item.folderName && !physicalFolderNameSet.has(item.folderName))
      .map((item) => item.id),
    foldersWithoutIndexRow: physicalFolderNames.filter((folderName) => !indexedFolderNames.has(folderName)),
    folderAuditError,
    byRound,
    feedback,
  }
}

function feedbackAttachmentInfo(feedbackId, attachmentIndex, currentRoundOnly = true) {
  const item = readFeedbackIndex().feedback.find((entry) =>
    entry.id === feedbackId && (!currentRoundOnly || entry.reviewRoundId === reviewRound.id))
  if (!item) return null
  const folderPath = resolve(FEEDBACK_DIR, String(item.folderName || ''))
  if (!isUnderRoot(folderPath, FEEDBACK_DIR)) return null
  const feedback = readJson(resolve(folderPath, 'feedback.json'), null)
  const attachment = feedback?.attachments?.[attachmentIndex]
  const filePath = attachment ? resolve(folderPath, String(attachment.savedAs || '')) : ''
  if (!attachment || !filePath || !isUnderRoot(filePath, folderPath) || !existsSync(filePath)) return null
  return { attachment, filePath }
}

async function generationTaskSnapshot() {
  return nativeTaskStore.listSnapshot()
}

function isDeletableGenerationRequest(item) {
  if (!item) return false
  return new Set([
    'native_assistance_model_ready',
    'native_task_created',
    'native_task_claimed',
    'native_source_planning',
    'native_build_in_progress',
    'native_validation_in_progress',
    'native_task_needs_input',
    'native_task_blocked',
    'native_task_failed',
  ]).has(String(item.status || '').toLowerCase())
}

async function currentGenerationRequests(username = null) {
  return (await generationTaskSnapshot())
    .filter((item) => item.nativeBuild?.archived !== true)
    .filter((item) => item.nativeBuild?.state !== 'cancelled')
    .filter((item) => !username || item.username === username || item.username === '__global__')
    .slice(0, 40)
    .map(publicGenerationRequest)
}

function publicProgress(progress) {
  if (!progress || typeof progress !== 'object' || Array.isArray(progress)) return null
  const safe = {}
  for (const key of ['stage', 'phase', 'step']) {
    const value = progress[key]
    if (typeof value === 'string' && /^[a-z0-9_.:-]{1,120}$/i.test(value)) safe[key] = value
  }
  for (const key of ['completed', 'total', 'percent']) {
    const value = progress[key]
    if (typeof value === 'number' && Number.isFinite(value)) safe[key] = value
  }
  return Object.keys(safe).length ? safe : null
}

function publicPlanArtifact(planArtifact) {
  if (!planArtifact || typeof planArtifact !== 'object' || Array.isArray(planArtifact)) return null
  const safe = {}
  for (const key of [
    'schema',
    'id',
    'planId',
    'recipeId',
    'recipeVersion',
    'recipeDigest',
    'digest',
    'sha256',
    'fileSha256',
    'attempt',
    'immutable',
    'status',
  ]) {
    const value = planArtifact[key]
    if (typeof value === 'string' || typeof value === 'number' || typeof value === 'boolean') safe[key] = value
  }
  if (typeof planArtifact.fileName === 'string') safe.fileName = basename(planArtifact.fileName).slice(0, 240)
  return Object.keys(safe).length ? safe : null
}

function publicNativeBuild(nativeBuild) {
  if (!nativeBuild || typeof nativeBuild !== 'object' || Array.isArray(nativeBuild)) return null
  const blockerCode = typeof nativeBuild.blocker?.code === 'string' &&
    /^[a-z0-9_.:-]{1,200}$/i.test(nativeBuild.blocker.code)
    ? nativeBuild.blocker.code.slice(0, 200)
    : ''
  return {
    schema: String(nativeBuild.schema || ''),
    state: String(nativeBuild.state || ''),
    attempt: Number(nativeBuild.attempt || 0),
    modelReady: nativeBuild.modelReady === true,
    executionStarted: nativeBuild.executionStarted === true,
    archived: nativeBuild.archived === true,
    requiredCad: String(nativeBuild.requiredCad || ''),
    requiredChecks: Array.isArray(nativeBuild.requiredChecks)
      ? nativeBuild.requiredChecks.map((value) => String(value)).slice(0, 100)
      : [],
    progress: publicProgress(nativeBuild.progress),
    blocker: blockerCode ? { code: blockerCode } : null,
    planArtifact: publicPlanArtifact(nativeBuild.planArtifact),
  }
}

function publicGenerationRequest(item) {
  const verifiedSeedReady = is16029StructureAssistanceReady(item)
  const nativePublication = verifiedSeedReady
    ? { ok: false }
    : item.parametricRequest ? inspect16029ParametricTaskPublication(item) : inspectNativeStructureAssistancePublication({ dataDir: DATA_DIR, task: item })
  const verifiedNativeReady = nativePublication.ok === true
  const ready = verifiedSeedReady || verifiedNativeReady
  const invalidNativeCompletion = item.nativeBuild?.state === 'ready' &&
    item.taskType === 'native_solidworks_build_task' && !verifiedNativeReady
  const publicBuild = publicNativeBuild(item.nativeBuild)
  if (publicBuild && invalidNativeCompletion) {
    publicBuild.state = 'validating'
    publicBuild.modelReady = false
  } else if (publicBuild && verifiedNativeReady) {
    publicBuild.modelReady = true
  }
  return {
    schema: String(item.schema || ''),
    id: String(item.id || ''),
    createdAt: String(item.createdAt || ''),
    updatedAt: String(item.updatedAt || ''),
    revision: Number(item.revision || 0),
    username: String(item.username || ''),
    status: invalidNativeCompletion ? 'native_validation_in_progress' : String(item.status || ''),
    purpose: String(item.purpose || ''),
    customerParameterFlow: String(item.customerParameterFlow || ''),
    customerRequirementReference: String(item.customerRequirementReference || ''),
    widthInputMode: String(item.widthInputMode || ''),
    requestedWidthMm: item.requestedWidthMm ?? '',
    requestedWidthSemantics: String(item.requestedWidthSemantics || ''),
    generationTemplateId: String(item.generationTemplateId || ''),
    generationTemplateLabel: String(item.generationTemplateLabel || ''),
    candidateRecipeId: String(item.candidateRecipeId || ''),
    candidateSourceSeedIds: Array.isArray(item.candidateSourceSeedIds) ? [...item.candidateSourceSeedIds] : [],
    sourceBaselineAssetId: String(item.sourceBaselineAssetId || ''),
    generationResultKind: String(item.generationResultKind || ''),
    resultKind: String(item.resultKind || ''),
    verifiedRecipeMatched: item.verifiedRecipeMatched === true,
    nativeTaskAccepted: item.nativeTaskAccepted === true,
    legacyFallbackUsed: item.legacyFallbackUsed === true,
    engineeringDeepeningRequired: item.engineeringDeepeningRequired === true,
    deliveryMode: verifiedNativeReady
      ? 'verified_native_structure_assistance_model'
      : String(item.deliveryMode || ''),
    resolutionDurationMs: Number(item.resolutionDurationMs || 0),
    taskMode: String(item.taskMode || ''),
    taskType: String(item.taskType || ''),
    cabinetWidth: item.cabinetWidth ?? '',
    cabinetHeight: item.cabinetHeight ?? '',
    cabinetDepth: item.cabinetDepth ?? '',
    columns: item.columns ?? '',
    doorCount: item.doorCount ?? '',
    columnDoorCounts: Array.isArray(item.columnDoorCounts) ? [...item.columnDoorCounts] : [],
    doorWidth: item.doorWidth ?? '',
    doorHeight: item.doorHeight ?? '',
    estimatedDoorPanelWidthMm: item.estimatedDoorPanelWidthMm ?? null,
    estimatedDoorHeightMm: item.estimatedDoorHeightMm ?? null,
    previewDimensionsValidated: item.previewDimensionsValidated !== false,
    rowSequence: String(item.rowSequence || ''),
    ...(item.parametricRequest ? { parametricRequest: structuredClone(item.parametricRequest) } : {}),
    doorType: String(item.doorType || ''),
    lockType: String(item.lockType || ''),
    hingeType: String(item.hingeType || ''),
    latchType: String(item.latchType || ''),
    reinforcement: String(item.reinforcement || ''),
    openings: String(item.openings || ''),
    material: String(item.material || ''),
    thickness: String(item.thickness || ''),
    previewType: String(item.previewType || ''),
    validationRequired: Array.isArray(item.validationRequired) ? [...item.validationRequired] : [],
    boundary: String(item.boundary || ''),
    deduplicated: item.deduplicated === true,
    nativeBuild: publicBuild,
    modelReady: ready,
    engineeringAssistanceReady: ready,
    engineeringContinuationEligible: ready,
    engineeringAssistanceEligible: ready,
    engineeringUseEligible: ready,
    downloadUrl: verifiedNativeReady
      ? `/native-assistance-download/${encodeURIComponent(String(item.id || ''))}`
      : ready ? item.downloadUrl : '',
  }
}

function isEngineeringContinuationEligible(item) {
  return is16029StructureAssistanceReady(item)
}

function isGenerationDownloadReady(item) {
  return item?.engineeringAssistanceReady === true && Boolean(item.downloadUrl)
}

function generationTaskStatusInfo(item) {
  if (isGenerationDownloadReady(item)) {
    return {
      key: 'ready',
      label: '可下载',
      detail: '已完成原生模型与交付检查，可下载后继续深化。',
    }
  }
  switch (String(item?.status || '').toLowerCase()) {
    case 'native_task_created':
      return { key: 'queued', label: '排队', detail: '规格已登记，等待原生建模工作器接收。' }
    case 'native_task_claimed':
    case 'native_source_planning':
      return { key: 'queued', label: '排队', detail: '工作器已接收，正在准备本次原生建模。' }
    case 'native_build_in_progress':
      return { key: 'building', label: '构建', detail: 'SolidWorks 原生模型构建中。' }
    case 'native_validation_in_progress':
      return { key: 'checking', label: '检查', detail: '正在核对尺寸、装配、展开与迁移重开。' }
    case 'native_task_needs_input':
      return { key: 'hold', label: '待补充', detail: '需要补充规格信息后才能继续。' }
    case 'native_task_cancel_requested':
      return { key: 'hold', label: '取消中', detail: '已提交取消请求，当前原生步骤结束后停止。' }
    case 'native_task_blocked':
      return { key: 'failed', label: '待处理', detail: '本次任务被结构检查阻断，请核对参数或提交问题反馈。' }
    case 'native_task_failed':
      return { key: 'failed', label: '未通过', detail: '本次模型未通过交付检查，不能下载；可提交问题反馈。' }
    default:
      return { key: 'queued', label: '待更新', detail: '正在等待任务状态更新。' }
  }
}

function generationRequestTargetLabel(item) {
  const reference = String(item.customerRequirementReference || item.id || '').trim()
  return `${reference}｜${item.cabinetWidth || '-'}W × ${item.cabinetHeight || '-'}H × ${item.cabinetDepth || '-'}D｜${item.doorCount || '-'}门`
}

function generationRequestSnapshot(item) {
  return {
    id: String(item.id || ''),
    customerRequirementReference: String(item.customerRequirementReference || ''),
    widthInputMode: String(item.widthInputMode || ''),
    requestedWidthMm: item.requestedWidthMm ?? '',
    requestedWidthSemantics: String(item.requestedWidthSemantics || ''),
    cabinetWidth: item.cabinetWidth ?? '',
    cabinetHeight: item.cabinetHeight ?? '',
    cabinetDepth: item.cabinetDepth ?? '',
    columns: item.columns ?? '',
    doorCount: item.doorCount ?? '',
    doorWidth: item.doorWidth ?? '',
    rowSequence: String(item.rowSequence || ''),
    generationTemplateId: String(item.generationTemplateId || ''),
    generationTemplateLabel: String(item.generationTemplateLabel || ''),
    sourceBaselineAssetId: String(item.sourceBaselineAssetId || ''),
    resultKind: String(item.resultKind || ''),
    deliveryMode: String(item.deliveryMode || ''),
    completedAt: String(item.modelGeneratedAt || item.processedAt || item.createdAt || ''),
    engineeringAssistanceReady: item.engineeringAssistanceReady === true,
    engineeringDeepeningRequired: item.engineeringDeepeningRequired === true,
  }
}

function isUnderRoot(path, root) {
  const rel = relative(root, path)
  return rel === '' || (rel && !rel.startsWith('..') && !isAbsolute(rel))
}

function generationStatusText(item) {
  return generationTaskStatusInfo(item).detail
}

function generationActionHtml(item) {
  if (isGenerationDownloadReady(item)) {
    return `<a class="button" href="${htmlEscape(item.downloadUrl)}">下载结构工程辅助基础模型</a>`
  }
  return '<button type="button" disabled>未通过检查前不可下载</button>'
}

function generationDeleteActionHtml(item) {
  if (!isDeletableGenerationRequest(item)) return ''
  if (item.status === 'native_assistance_model_ready') {
    return `<button class="secondary danger generation-delete-button" type="button" data-request-id="${htmlEscape(item.id)}">归档记录</button>`
  }
  return `<button class="secondary danger generation-delete-button" type="button" data-request-id="${htmlEscape(item.id)}">取消任务</button>`
}

async function findGenerationRequestForUser(requestId, username, { includeHidden = false } = {}) {
  let item = null
  try {
    item = await nativeTaskStore.read(requestId)
  } catch (error) {
    if (error?.statusCode !== 404) throw error
  }
  const visibleToUser = item && (item.username === username || item.username === '__global__')
  const hidden = item?.nativeBuild?.archived === true || item?.nativeBuild?.state === 'cancelled'
  if (!visibleToUser || (!includeHidden && hidden)) {
    const error = new Error('generation request not found')
    error.statusCode = 404
    throw error
  }
  return item
}

async function deleteGenerationRequest(username, requestId) {
  const item = await findGenerationRequestForUser(requestId, username)
  if (!isDeletableGenerationRequest(item)) {
    const error = new Error('generation request is not deletable')
    error.statusCode = 409
    throw error
  }
  if (item.username !== username) {
    const error = new Error('generation request not found')
    error.statusCode = 404
    throw error
  }
  const transitionId = `portal-remove:${requestId}:r${item.revision}`
  if (item.nativeBuild?.state === 'ready') {
    const task = await nativeTaskStore.archive({
      taskId: requestId,
      username,
      expectedRevision: item.revision,
      transitionId,
      reason: 'owner_removed_from_portal_list',
    })
    return { task, action: 'archived' }
  }
  const task = await nativeTaskStore.cancel({
    taskId: requestId,
    username,
    expectedRevision: item.revision,
    transitionId,
    reason: 'owner_cancelled_from_portal',
  })
  return {
    task,
    action: task.nativeBuild?.state === 'cancel_requested' ? 'cancel_requested' : 'cancelled',
  }
}

function clampText(value, maxLength = 4000) {
  return String(value || '').trim().slice(0, maxLength)
}

const PORTAL_REUSABLE_NATIVE_TASK_STATES = Object.freeze([
  ...DEFAULT_ACTIVE_NATIVE_TASK_STATES,
  'ready',
])

async function submitGenerationRequest(username, payload) {
  const resolutionStartedAt = Date.now()
  mkdirSync(GENERATION_REQUEST_DIR, { recursive: true })
  const customerPlan = normalize16029NativeModelRequest(payload)
  if (!customerPlan.ok) {
    const error = new Error(customerPlan.message)
    error.statusCode = 422
    throw error
  }
  const sourcePayload = customerPlan.normalizedRequest
  const requestFingerprint = sourcePayload.requestFingerprint || native16029RequestFingerprint(sourcePayload)
  const createdAt = new Date().toISOString()
  const requestId = `NATIVE-${createdAt.replace(/[-:.]/g, '').slice(0, 15)}-${randomBytes(3).toString('hex').toUpperCase()}`
  const verifiedRecipeMatched = customerPlan.matchType === 'verified_native_seed'
  const normalized = {
    schema: sourcePayload.schema || 'winnsen.native_16029_request.v1',
    id: requestId,
    createdAt,
    username,
    storageMode: 'task_file_source',
    requestFingerprint,
    generatorId: clampText(sourcePayload.generatorId, 100),
    status: verifiedRecipeMatched ? 'native_assistance_model_ready' : 'native_task_created',
    purpose: clampText(sourcePayload.purpose || 'engineering_assistance', 80),
    customerParameterFlow: 'native_v1',
    customerRequirementReference: clampText(sourcePayload.customerRequirementReference, 200),
    widthInputMode: clampText(sourcePayload.widthInputMode, 80),
    requestedWidthMm: clampText(sourcePayload.requestedWidthMm, 40),
    requestedWidthSemantics: clampText(sourcePayload.requestedWidthSemantics, 100),
    generationTemplateId: clampText(sourcePayload.generationTemplateId, 100),
    generationTemplateLabel: clampText(sourcePayload.generationTemplateLabel, 160),
    candidateRecipeId: clampText(sourcePayload.candidateRecipeId, 100),
    candidateSourceSeedIds: Array.isArray(customerPlan.pendingRecipe?.sourceSeedIds)
      ? [...customerPlan.pendingRecipe.sourceSeedIds]
      : [],
    sourceBaselineAssetId: clampText(sourcePayload.sourceBaselineAssetId, 160),
    generationResultKind: clampText(sourcePayload.resultKind, 160),
    verifiedRecipeMatched,
    modelReady: verifiedRecipeMatched,
    nativeTaskAccepted: !verifiedRecipeMatched,
    legacyFallbackUsed: false,
    engineeringDeepeningRequired: sourcePayload.engineeringDeepeningRequired === true,
    engineeringAssistanceReady: verifiedRecipeMatched,
    engineeringContinuationEligible: verifiedRecipeMatched,
    engineeringAssistanceEligible: verifiedRecipeMatched,
    engineeringUseEligible: verifiedRecipeMatched,
    downloadUrl: '',
    deliveryMode: verifiedRecipeMatched ? 'verified_native_model' : 'native_task_pending',
    resolutionDurationMs: 0,
    taskMode: clampText(sourcePayload.taskMode || 'full_assembly', 40),
    taskType: verifiedRecipeMatched ? 'verified_native_structure_assistance_model' : 'native_solidworks_build_task',
    prompt: clampText(payload.prompt),
    cabinetWidth: clampText(sourcePayload.cabinetWidth, 40),
    cabinetHeight: clampText(sourcePayload.cabinetHeight, 40),
    cabinetDepth: clampText(sourcePayload.cabinetDepth, 40),
    columns: clampText(sourcePayload.columns, 40),
    doorCount: clampText(sourcePayload.doorCount, 40),
    columnDoorCounts: Array.isArray(sourcePayload.columnDoorCounts) ? [...sourcePayload.columnDoorCounts] : [],
    doorWidth: clampText(sourcePayload.doorWidth, 40),
    doorHeight: clampText(sourcePayload.doorHeight, 40),
    estimatedDoorPanelWidthMm: sourcePayload.estimatedDoorPanelWidthMm ?? null,
    estimatedDoorHeightMm: sourcePayload.estimatedDoorHeightMm ?? null,
    previewDimensionsValidated: sourcePayload.previewDimensionsValidated !== false,
    rowSequence: clampText(sourcePayload.rowSequence, 200),
    ...(sourcePayload.parametricRequest ? { parametricRequest: structuredClone(sourcePayload.parametricRequest) } : {}),
    doorType: clampText(payload.doorType, 100),
    lockType: clampText(payload.lockType, 100),
    hingeType: clampText(payload.hingeType, 100),
    latchType: clampText(payload.latchType, 100),
    reinforcement: clampText(payload.reinforcement, 100),
    openings: clampText(payload.openings, 200),
    material: clampText(sourcePayload.material, 100),
    thickness: clampText(sourcePayload.thickness, 60),
    previewType: 'browser_parametric_3d_viewport_reference',
    workerRecommendation: verifiedRecipeMatched
      ? 'continue_engineering_deepening_in_solidworks'
      : 'build_new_native_solidworks_model_from_verified_native_sources',
    requestedOutputs: [
      'browser_parametric_3d_preview',
      'solidworks_native_structure_assistance_model',
    ],
    validationRequired: [
      'door_gap_and_clearance',
      'hinge_hole_pattern',
      'one_door_one_lock_opening',
      'lock_and_latch_interface',
      'reinforcement_rib_clearance',
      'material_thickness_and_bend_rules',
      'engineering_deepening_review',
    ],
    boundary: verifiedRecipeMatched
      ? '本记录只绑定已完成结构接口验证的原生基础模型；结构工程师下载后继续完成项目细节。'
      : '本记录表示原生新任务已建立；模型尚未构建，预估门板尺寸仅用于任务预览。',
    message: verifiedRecipeMatched ? '已找到同规格结构工程辅助基础模型。' : customerPlan.message,
  }

  if (verifiedRecipeMatched) {
    const sourceAsset = byId.get(normalized.sourceBaselineAssetId)
    if (!sourceAsset || sourceAsset.engineeringUseEligible !== true) {
      const error = new Error('verified engineering-assistance asset binding is invalid')
      error.statusCode = 500
      throw error
    }
    const sourceAssetInfo = freshExactAssetIntegrity(sourceAsset)
    if (sourceAssetInfo.available !== true || sourceAssetInfo.integrityVerified !== true) {
      const missing = sourceAssetInfo.integrityError === 'missing'
      const error = new Error(missing
        ? 'verified engineering-assistance asset is unavailable'
        : 'verified engineering-assistance asset integrity verification failed')
      error.statusCode = missing ? 503 : 409
      throw error
    }
    normalized.status = 'native_assistance_model_ready'
    normalized.taskType = 'verified_native_structure_assistance_model'
    normalized.resultKind = normalized.generationResultKind
    normalized.downloadUrl = `/download/${normalized.sourceBaselineAssetId}`
    normalized.engineeringAssistanceReady = true
    normalized.engineeringContinuationEligible = true
    normalized.engineeringAssistanceEligible = true
    normalized.engineeringUseEligible = true
    normalized.deliveryMode = 'verified_native_model'
    normalized.message = `已找到同规格${normalized.generationTemplateLabel}，可下载后由结构工程师继续深化。`
  } else {
    normalized.nativeBuild = {
      schema: 'winnsen.native_solidworks_build_task.v1',
      state: 'created',
      createdAt,
      executionStarted: false,
      modelReady: false,
      legacyFallbackUsed: false,
      requiredCad: 'SolidWorks 2020 native',
      requiredChecks: [...new Set([
        ...(Array.isArray(customerPlan.pendingRecipe?.requiredValidation) ? customerPlan.pendingRecipe.requiredValidation : []),
        ...normalized.validationRequired,
      ])],
      statusHistory: [{ state: 'created', at: createdAt }],
    }
  }
  normalized.resolutionDurationMs = Math.max(0, Date.now() - resolutionStartedAt)

  const stored = await nativeTaskStore.createOrReuseByFingerprint(normalized, {
    username,
    activeStates: PORTAL_REUSABLE_NATIVE_TASK_STATES,
  })
  return {
    record: { ...stored.task, deduplicated: stored.created !== true },
    created: stored.created === true,
    httpStatus: verifiedRecipeMatched || stored.created !== true ? 200 : 201,
  }
}

function renderCabinetStudy() {
  return render16029AuthCabinet()
}

function renderAuthMotion() {
  return render16029AuthCabinetScript()
}

function renderAuthPage(error = '', mode = 'login') {
  const isRegister = mode === 'register'
  const clarificationCount = readClarificationQuestions().length
  return renderShell(`
    <main class="auth-wrap">
      <header class="auth-masthead">
        <div class="studio-brand"><img src="/brand/winnsen-logo.jpg" alt="Winnsen" /><span>Structure workspace</span></div>
        <span class="auth-edition"><i aria-hidden="true"></i>16029 工程辅助</span>
      </header>
      <section class="auth-study" aria-label="16029 柜体设计门户">
        <div class="auth-study-title">
          <p class="eyebrow">从规格开始，让结构成形</p>
          <h2>把想法，<br />做成结构。</h2>
          <p>定义每一列，核对每一扇门。<br />让重复建模，回到有据可依的设计。</p>
        </div>
        ${renderCabinetStudy()}
        <ol class="auth-process" aria-label="工具使用流程"><li><b>01</b><span>定义规格</span></li><li><b>02</b><span>生成与校核</span></li><li><b>03</b><span>工程深化</span></li></ol>
      </section>
      <section class="auth-entry">
        <div class="auth-card">
          <div class="auth-card-symbol" aria-hidden="true"><svg viewBox="0 0 32 32"><path d="M5 9 16 3l11 6v14l-11 6L5 23Z M5 9l11 6 11-6M16 15v14" fill="none" stroke="currentColor" stroke-width="1.6" /></svg></div>
          <p class="eyebrow">${isRegister ? '加入你的工程工作区' : '你的下一次设计，从这里开始'}</p>
          <h1>${isRegister ? '建立工作台账号' : '登录工作台'}</h1>
          <p class="muted auth-description">${isRegister ? '使用维护人提供的邀请码，创建个人账号。' : '继续你的规格定义、模型任务与结构修订。'}${clarificationCount ? `当前有 ${clarificationCount} 项待工程师确认。` : ''}</p>
          ${error ? `<div class="alert">${htmlEscape(error)}</div>` : ''}
          <form method="post" action="${isRegister ? '/register' : '/login'}" class="auth-form">
            <label><span class="field-label">账号</span><input name="username" autocomplete="username" placeholder="输入你的账号" required /></label>
            <label><span class="field-label">密码</span><input name="password" type="password" autocomplete="${isRegister ? 'new-password' : 'current-password'}" placeholder="${isRegister ? '设置密码，至少 6 位' : '输入密码'}" required /></label>
            ${isRegister ? '<label><span class="field-label">邀请码</span><input name="inviteCode" autocomplete="off" placeholder="输入维护人提供的邀请码" required /></label>' : ''}
            <button type="submit">${isRegister ? '注册并进入工作台' : '进入工作台'}<span aria-hidden="true">↗</span></button>
          </form>
          <p class="auth-switch">${isRegister ? '已有账号？' : '第一次使用？'}<a href="${isRegister ? '/login' : '/register'}">${isRegister ? '返回登录' : '使用邀请码注册'}</a></p>
          <div class="auth-note"><span class="auth-note-dot" aria-hidden="true"></span><p>原生模型通过交付检查后开放下载。<br />由结构工程师接收并继续深化。</p></div>
        </div>
      </section>
      <footer class="auth-footer"><span>WINNSEN / STRUCTURE WORKSPACE</span><span>规格 · 模型 · 校核 · 修订</span></footer>
    </main>
    ${renderAuthMotion()}
  `)
}


function renderShell(content, username = '') {
  return `<!doctype html>
<html lang="zh-CN">
<head>
<meta charset="utf-8" />
<meta name="viewport" content="width=device-width, initial-scale=1" />
<title>16029 参数化工程工作台 · Winnsen</title>
<style>
  :root {
    color-scheme:light;
    --ink:#173546; --muted:#5e6d72; --line:#cbd1cd; --soft:#f3f1e9; --paper:#fcfbf7;
    --brand:#173546; --navy:#142f3d; --hot:#c24e27; --good:#216849; --warn:#8c5e16; --risk:#aa3632;
    --mono:"Cascadia Code", "Cascadia Mono", Consolas, "Microsoft YaHei UI", monospace;
    --sans:"Bahnschrift", "Microsoft YaHei UI", "Microsoft YaHei", "Noto Sans CJK SC", sans-serif;
  }
  * { box-sizing:border-box; }
  [hidden] { display:none !important; }
  body { margin:0; color:var(--ink); background:var(--soft); font:15px/1.65 var(--sans); -webkit-font-smoothing:antialiased; }
  a { color:var(--brand); text-underline-offset:4px; }
  a:hover { color:var(--hot); }
  h1, h2, h3, p { overflow-wrap:break-word; }
  h1, h2, h3 { margin:0; font-weight:600; line-height:1.35; }
  h1 { font-size:36px; letter-spacing:-.03em; }
  h2 { font-size:24px; letter-spacing:-.02em; }
  h3 { font-size:18px; }
  .muted { color:var(--muted); }
  .eyebrow, .portal-kicker, .sheet-label { margin:0 0 10px; color:var(--hot); font:11px/1.5 var(--mono); letter-spacing:.1em; }
  .sheet-label { color:var(--muted); }
  input, select, textarea { width:100%; min-width:0; min-height:46px; padding:10px 12px; border:1px solid #b9c3c2; border-radius:3px; background:#fffefa; color:var(--ink); font:15px/1.5 var(--sans); transition:border-color .16s, box-shadow .16s; }
  input::placeholder, textarea::placeholder { color:#768185; font-size:13px; }
  input:hover, select:hover, textarea:hover { border-color:#809693; }
  input:focus, select:focus, textarea:focus { border-color:var(--brand); box-shadow:0 0 0 3px #1735460d; outline:none; }
  textarea { min-height:116px; resize:vertical; }
  label { min-width:0; display:grid; align-content:start; gap:8px; color:var(--ink); font-size:13px; font-weight:500; }
  input[type="number"], .task-search input { font-family:var(--mono); font-variant-numeric:tabular-nums; }
  input[type="file"] { padding:8px; background:var(--soft); border-style:dashed; }
  input[type="file"]::file-selector-button { min-height:30px; margin-right:12px; padding:4px 12px; border:1px solid var(--line); border-radius:2px; background:var(--paper); color:var(--ink); font:inherit; cursor:pointer; }
  input[type="range"] { min-height:32px; padding:0; border:0; box-shadow:none; accent-color:var(--brand); background:transparent; }
  input[type="radio"] { width:18px; height:18px; min-height:18px; flex:0 0 auto; accent-color:var(--brand); }
  button, .button { min-height:44px; padding:10px 16px; display:inline-flex; align-items:center; justify-content:center; gap:8px; border:1px solid transparent; border-radius:3px; color:#fff; background:var(--hot); font:500 14px/1.4 var(--sans); text-decoration:none; cursor:pointer; transition:background-color .16s, border-color .16s, color .16s; }
  button:hover, .button:hover { color:#fff; background:#a63e1c; }
  button:disabled, .button[aria-disabled="true"] { cursor:not-allowed; color:#637176; background:#e9ece5; border-color:#d2d9d2; }
  .button.secondary, button.secondary { background:transparent; border-color:#b9c3c2; color:var(--brand); }
  .button.secondary:hover, button.secondary:hover { background:#e8ede7; border-color:#809693; }
  button.danger, .button.danger { color:var(--risk); background:transparent; border-color:#dec6bb; }
  button.danger:hover, .button.danger:hover { background:#f9eae3; }
  :where(a,button,input,select,textarea,summary):focus-visible { outline:3px solid #c24e2766; outline-offset:3px; }
  summary { cursor:pointer; font-size:14px; font-weight:500; }
  details > summary { list-style:none; }
  details > summary::-webkit-details-marker { display:none; }
  details > summary::after { content:'+'; margin-left:auto; color:var(--muted); font:20px/1 var(--mono); }
  details[open] > summary::after { content:'−'; }
  .chip, .engineering-chip { display:inline-flex; align-items:center; gap:5px; padding:3px 8px; border:1px solid var(--line); border-radius:2px; color:#455e65; background:#edf0e9; font-size:11px; font-weight:500; line-height:1.6; }
  .chip.good { color:var(--good); border-color:#bdd5c3; background:#edf4eb; }
  .chip.warn { color:var(--warn); border-color:#deceaa; background:#fbf4e5; }
  .alert, .ok, .feedback-boundary, .gate-warning, .request-warning { padding:12px 14px; margin:12px 0 0; border:1px solid #decdaa; border-radius:2px; color:var(--warn); background:#fbf6e8; font-size:13px; line-height:1.65; }
  .alert { color:var(--risk); border-color:#dfb8ae; background:#fbefe9; }
  .ok, .generation-capability-ready { color:var(--good); border-color:#bdd5c3; background:#eff5ec; }
  .generation-capability-pending { color:var(--warn); border-color:#decdaa; background:#fbf6e8; }
  .shell { min-height:100vh; display:grid; grid-template-columns:224px minmax(0,1fr); }
  .portal-sidebar { height:100vh; position:sticky; top:0; padding:28px 20px 20px; display:flex; flex-direction:column; overflow-y:auto; scrollbar-width:thin; background:var(--navy); color:#e7e9df; }
  .portal-logo { width:145px; height:48px; flex:0 0 auto; display:flex; align-items:center; background:#fff; border-radius:2px; }
  .portal-logo img { width:131px; max-height:38px; margin:auto; object-fit:contain; }
  .portal-sidebar .portal-kicker { margin:28px 0 8px; color:#a7babb; font-size:10px; letter-spacing:.08em; }
  .portal-sidebar h2 { color:#eef0e6; font-size:18px; letter-spacing:0; }
  .side-intro { margin:12px 0 0; color:#a7babb; font-size:12px; line-height:1.85; }
  .portal-nav { display:grid; gap:4px; margin:28px -20px 0; }
  .portal-nav a { min-height:66px; padding:12px 18px; display:flex; align-items:center; gap:12px; border-left:3px solid transparent; color:#c4d1d0; text-decoration:none; }
  .portal-nav a:hover { background:#1d3b47; color:#fff; }
  .portal-nav a.active { background:#284854; border-left-color:#ee936e; color:#fff; }
  .portal-nav-index { width:26px; flex:0 0 auto; color:#9db2b2; font:12px/1 var(--mono); }
  .portal-nav a.active .portal-nav-index { color:#ffb492; }
  .portal-nav-copy { display:grid; gap:2px; }
  .portal-nav-copy strong { font-weight:500; font-size:14px; }
  .portal-nav-copy small { color:#b0c2c3; font:9px/1.5 var(--mono); letter-spacing:.06em; }
  .side-study { margin:32px 0 20px; border-top:1px solid #46616a; padding-top:20px; }
  .side-study-code { color:#839d9f; font:42px/1 var(--sans); letter-spacing:.09em; font-weight:300; }
  .side-study span { display:block; margin-top:10px; color:#a7babb; font:9px/1.5 var(--mono); letter-spacing:.06em; }
  .side-boundary { color:#a7babb; font-size:11px; line-height:1.8; }
  .side-boundary strong { display:block; margin-bottom:6px; color:#d9e2db; font-weight:500; }
  .account-card { display:flex; align-items:center; gap:10px; margin-top:auto; padding-top:24px; }
  .account-avatar { width:32px; height:32px; display:grid; place-items:center; border:1px solid #668087; color:#dce4da; font:12px var(--mono); }
  .account-copy { min-width:0; flex:1; }
  .account-copy span { display:block; color:#a7babb; font-size:10px; }
  .account-copy strong { display:block; overflow:hidden; text-overflow:ellipsis; color:#eef0e6; font:12px var(--mono); }
  .account-card a { color:#c4d1d0; font-size:11px; text-decoration:none; min-height:44px; display:flex; align-items:center; }
  .account-card a:hover { color:#ffb492; }
  .workspace-main { min-width:0; width:100%; max-width:1600px; padding:0 36px 36px; }
  .workspace-topline { min-height:64px; display:flex; align-items:center; justify-content:space-between; gap:16px; border-bottom:1px solid var(--line); color:var(--muted); font:10px/1.5 var(--mono); letter-spacing:.06em; }
  .workspace-topline b { color:var(--ink); font-weight:500; }
  .workspace-topline span:first-child { display:flex; align-items:center; gap:12px; }
  .workspace-topline i { width:6px; height:6px; background:var(--hot); }
  .workspace-view { display:none; min-width:0; flex-direction:column; }
  .workspace-view.active { display:flex; }
  .panel { min-width:0; margin:0; padding:28px; background:var(--paper); border:1px solid var(--line); border-top:0; }
  .review-overview, .workspace-view > .panel:first-child { border:0; background:transparent; padding:32px 0 24px; }
  .workspace-title-row, .workspace-view-heading { display:flex; align-items:start; justify-content:space-between; gap:28px; }
  .simple-intro, .workspace-heading-copy { min-width:0; max-width:780px; }
  .simple-intro .muted, .workspace-heading-copy > .muted { margin:12px 0 0; max-width:69ch; font-size:14px; }
  .workspace-stamp { min-width:158px; border:1px solid #93a6a7; display:grid; grid-template-columns:1fr auto; align-self:flex-start; }
  .workspace-stamp span, .workspace-stamp strong { display:block; padding:7px 10px; font:10px/1.4 var(--mono); }
  .workspace-stamp span { color:var(--muted); }
  .workspace-stamp strong { border-left:1px solid #93a6a7; color:var(--ink); }
  .workspace-stamp .stamp-title { grid-column:1 / -1; border-top:1px solid #93a6a7; color:var(--ink); font:12px/1.5 var(--sans); }
  .workflow-rail { margin:24px 0 0; padding:0; display:grid; grid-template-columns:repeat(5,minmax(0,1fr)); border-top:1px solid #aebbb8; border-bottom:1px solid #aebbb8; list-style:none; }
  .workflow-rail li { position:relative; min-height:68px; padding:12px 16px; display:grid; grid-template-columns:22px minmax(0,1fr); column-gap:8px; align-content:center; border-left:1px solid var(--line); color:var(--muted); }
  .workflow-rail li:first-child { border-left:0; padding-left:0; }
  .workflow-rail li.active { color:var(--ink); }
  .workflow-rail li.active::after { content:''; position:absolute; bottom:-1px; left:0; right:0; height:2px; background:var(--hot); }
  .workflow-rail b { grid-row:1 / 3; font:11px/1.8 var(--mono); color:var(--muted); font-weight:400; }
  .workflow-rail .active b { color:var(--hot); }
  .workflow-rail span { font-size:13px; font-weight:500; }
  .workflow-rail small { font-size:11px; }
  .engineering-boundary { margin-top:14px; display:flex; gap:14px; color:var(--muted); font-size:12px; line-height:1.8; }
  .engineering-boundary strong { color:var(--ink); font-weight:500; white-space:nowrap; }
  .section-heading { display:flex; align-items:start; justify-content:space-between; gap:20px; padding-bottom:24px; border-bottom:1px solid var(--line); }
  .section-heading .eyebrow { margin-bottom:5px; }
  .section-heading h2 { font-size:20px; }
  .section-heading p.muted { margin:8px 0 0; max-width:78ch; font-size:13px; }
  .auxiliary-body { padding-top:24px; }
  .studio-grid { min-width:0; display:grid; grid-template-columns:minmax(300px,.82fr) minmax(0,1.18fr); gap:32px; align-items:start; }
  .generator-form { min-width:0; display:grid; gap:16px; }
  .door-layout-editor { min-width:0; border-top:1px solid var(--line); padding-top:16px; }
  .door-layout-heading { display:flex; align-items:baseline; justify-content:space-between; gap:12px; }
  .door-layout-heading h3 { margin:0; font-size:16px; font-weight:500; }
  .door-layout-heading span { color:var(--muted); font:10px var(--mono); }
  .door-layout-note { margin:8px 0 12px; color:var(--muted); font-size:12px; line-height:1.7; }
  .door-layout-presets { display:flex; flex-wrap:wrap; gap:8px; margin-bottom:12px; }
  .door-layout-presets button, .door-column-actions button { min-height:44px; padding:8px 10px; font-size:12px; }
  .door-columns { display:grid; grid-template-columns:minmax(0,1fr); gap:12px; }
  .door-column { min-width:0; margin:0; padding:12px; border:1px solid var(--line); background:var(--paper); }
  .door-column legend { padding:0 6px; color:var(--ink); font:13px var(--mono); }
  .door-column-count { display:grid; grid-template-columns:minmax(0,1fr) 86px; align-items:center; gap:12px; font-size:13px; }
  .door-column-count input { text-align:right; font-family:var(--mono); }
  .door-height-list { display:grid; gap:8px; list-style:none; margin:12px 0; padding:2px 4px 2px 0; max-height:304px; overflow:auto; }
  .door-height-row { display:grid; grid-template-columns:38px minmax(72px,1fr) 20px; align-items:center; gap:6px; }
  .door-height-row label { gap:0; color:var(--ink); font:12px/1.5 var(--mono); }
  .door-height-row label small { display:block; color:var(--muted); font:10px/1.4 var(--sans); }
  .door-height-row input { min-width:0; min-height:44px; padding:8px; text-align:right; font:13px var(--mono); }
  .door-height-row > span { color:var(--muted); font:10px var(--mono); }
  .door-column input[aria-invalid="true"] { border-color:var(--risk); }
  .door-column-summary { min-height:36px; margin:0; color:var(--muted); font:11px/1.7 var(--mono); overflow-wrap:anywhere; }
  .door-column-summary[data-valid="true"] { color:var(--good); }
  .door-column-summary[data-valid="false"] { color:var(--risk); }
  .door-column-mode { min-height:34px; margin:6px 0 8px; color:var(--muted); font-size:11px; line-height:1.6; }
  .door-column-actions { display:flex; flex-wrap:wrap; gap:6px; }
  .door-layout-status { margin:12px 0 0; padding:10px 12px; border-left:2px solid var(--good); background:#edf3eb; color:var(--good); font-size:12px; line-height:1.7; }
  .door-layout-status[data-valid="false"] { border-color:var(--risk); background:#fbf0e9; color:var(--risk); }
  #doorCount[readonly] { background:#f0f2eb; font-family:var(--mono); }
  @media(min-width:1440px), (min-width:541px) and (max-width:1024px) { .door-columns { grid-template-columns:repeat(2,minmax(0,1fr)); } }
  .field-grid { display:grid; grid-template-columns:repeat(3,minmax(0,1fr)); gap:16px; }
  .field-grid.two, .customer-parameter-grid { grid-template-columns:repeat(2,minmax(0,1fr)); }
  .customer-parameter-grid .wide { grid-column:1 / -1; }
  .field-label { display:flex; align-items:center; justify-content:space-between; gap:8px; }
  .field-label small { color:var(--muted); font:10px/1.5 var(--mono); font-weight:400; }
  .generator-form .feedback-boundary { margin:0; }
  .generated-prompt { padding:14px 16px; border:1px solid var(--line); border-left:2px solid var(--brand); background:#f0f2eb; color:#3e5962; font:12px/1.8 var(--mono); white-space:pre-wrap; overflow-wrap:anywhere; }
  .estimate-note { margin:0; color:var(--muted); font-size:12px; }
  .estimate-note strong { color:var(--ink); font-weight:500; }
  .advanced-fields { border:1px solid var(--line); border-radius:2px; background:var(--paper); }
  .advanced-fields > summary { min-height:46px; padding:10px 14px; display:flex; align-items:center; gap:14px; }
  .advanced-fields[open] > summary { border-bottom:1px solid var(--line); }
  .advanced-fields .field-grid { padding:16px 14px; }
  .advanced-fields > .muted { padding:0 14px 14px; margin:0; font-size:12px; }
  #generationSubmitButton { justify-content:space-between; }
  #generationSubmitButton::after { content:'→'; font:22px/1 var(--mono); }
  .layout-review-panel { min-width:0; margin:0; padding:0; background:transparent; color:var(--ink); position:sticky; top:24px; }
  .preview-panel { overflow:hidden; border:1px solid #99adac; border-radius:2px; background:var(--paper); }
  .preview-head { min-height:64px; padding:14px 18px; display:flex; align-items:center; justify-content:space-between; gap:12px; background:var(--navy); color:#edf0e5; }
  .preview-head strong { display:block; font-size:15px; font-weight:500; }
  .preview-head span:not(.chip) { display:block; margin-bottom:2px; color:#b4c5c2; font:10px/1.5 var(--mono); letter-spacing:.08em; }
  .preview-head .chip { flex:0 0 auto; background:transparent; color:#ffbc9c; border-color:#8b7970; font-size:10px; }
  .viewport-toolbar { min-height:58px; padding:8px 12px; display:flex; align-items:center; justify-content:space-between; flex-wrap:wrap; gap:8px; border-bottom:1px solid var(--line); }
  .view-button-row { display:flex; gap:4px; }
  .view-button-row .button { min-height:36px; padding:7px 10px; border-color:transparent; font:11px/1.5 var(--mono); }
  .view-button-row .view-active { background:var(--brand); border-color:var(--brand); color:#fff; }
  .preview-legend { color:var(--muted); font:10px/1.5 var(--mono); }
  .preview-canvas-wrap { position:relative; overflow:hidden; background:#f5f6ef; }
  #cabinetPreview { width:100%; height:auto; aspect-ratio:760 / 560; display:block; cursor:grab; touch-action:none; }
  #cabinetPreview:active { cursor:grabbing; }
  .drawing-corner { position:absolute; top:14px; left:16px; display:grid; gap:2px; color:#4e6971; font:9px/1.6 var(--mono); letter-spacing:.04em; pointer-events:none; }
  .drawing-corner b { font-weight:500; color:var(--ink); }
  .drawing-scale { position:absolute; top:14px; right:16px; color:#4e6971; font:9px var(--mono); pointer-events:none; }
  .viewport-hud { display:grid; grid-template-columns:repeat(2,minmax(0,1fr)); gap:0; border-top:1px solid #99adac; background:var(--paper); }
  .viewport-hud div { min-width:0; padding:10px 14px; border-bottom:1px solid var(--line); border-left:1px solid var(--line); }
  .viewport-hud div:nth-child(odd) { border-left:0; }
  .viewport-hud div:nth-last-child(-n+2) { border-bottom:0; }
  .viewport-hud span { display:block; color:var(--muted); font-size:10px; }
  .viewport-hud strong { display:block; margin-top:3px; color:var(--ink); font:11px/1.65 var(--mono); overflow-wrap:anywhere; }
  .preview-controls { padding:12px 14px; display:grid; grid-template-columns:30px minmax(0,1fr) 42px 30px minmax(0,1fr) 42px; gap:8px; align-items:center; border-top:1px solid #99adac; }
  .preview-controls label { color:var(--muted); font-size:11px; }
  .preview-controls .muted { text-align:right; font:10px/1.5 var(--mono); }
  .preview-note { margin:0; padding:12px 14px; border-top:1px solid var(--line); color:var(--muted); font-size:11px; line-height:1.8; }
  .progress-wrap { display:grid; gap:8px; }
  .progress-track { height:3px; overflow:hidden; background:#dbe1d8; }
  .progress-track span { display:block; height:100%; width:0; background:var(--hot); transition:width .3s; }
  .progress-label { color:var(--muted); font:11px/1.5 var(--mono); }
  .generation-confirm { padding:20px; border:1px solid #cc8e71; background:#fcf3e8; }
  .generation-confirm-head { display:flex; align-items:start; justify-content:space-between; gap:14px; }
  .generation-confirm-head span:first-child { display:block; margin-bottom:4px; color:#92513a; font:10px/1.5 var(--mono); letter-spacing:.06em; }
  .generation-confirm-head strong { font-size:16px; font-weight:500; }
  .generation-confirm-summary { margin:16px 0 0; display:grid; grid-template-columns:repeat(2,minmax(0,1fr)); gap:0; border-top:1px solid #dec6b3; }
  .generation-confirm-summary div { min-width:0; padding:10px 0; border-bottom:1px solid #dec6b3; }
  .generation-confirm-summary div:nth-child(odd) { padding-right:12px; }
  .generation-confirm-summary dt { color:#735c4e; font-size:11px; }
  .generation-confirm-summary dd { margin:3px 0 0; color:var(--ink); font:12px/1.65 var(--mono); overflow-wrap:anywhere; }
  .generation-confirm-boundary { color:#735c4e; font-size:12px; }
  .generation-confirm-actions { display:flex; flex-wrap:wrap; gap:8px; }
  .task-toolbar { display:flex; align-items:end; justify-content:space-between; flex-wrap:wrap; gap:20px; padding:20px 0; border-top:1px solid #aebbb8; border-bottom:1px solid #aebbb8; }
  .task-search { width:min(310px,100%); }
  .task-filter-buttons { display:flex; flex-wrap:wrap; gap:4px; }
  .task-filter-buttons button { min-height:44px; padding:8px 12px; border-color:transparent; font-size:12px; }
  .task-filter-buttons button.active { color:#fff; background:var(--brand); border-color:var(--brand); }
  .task-state-key { display:flex; flex-wrap:wrap; gap:8px 20px; margin:16px 0; color:var(--muted); font-size:11px; }
  .task-state-key i { width:6px; height:6px; margin-right:6px; display:inline-block; background:#798c8e; }
  .task-state-key i.ready, .task-ready .task-state-rail, .task-state-rail.ready { background:var(--good); }
  .task-state-key i.building, .task-building .task-state-rail, .task-state-rail.building { background:#306d89; }
  .task-state-key i.checking, .task-checking .task-state-rail, .task-state-rail.checking { background:var(--warn); }
  .task-failed .task-state-rail, .task-state-rail.failed { background:var(--risk); }
  #taskRefreshStatus { margin:16px 0; color:var(--muted); font:11px/1.5 var(--mono); }
  .request-list { display:grid; gap:12px; }
  .task-row { display:grid; grid-template-columns:3px minmax(0,1fr); border:1px solid var(--line); background:var(--paper); }
  .task-state-rail { grid-row:1 / 3; background:#798c8e; }
  .task-state-rail span { display:none; }
  .task-row-main { min-width:0; padding:20px 24px 16px; }
  .task-row-title { display:flex; align-items:center; gap:10px; flex-wrap:wrap; }
  .task-row-title strong { flex:1; font-size:17px; font-weight:500; }
  .task-row-title code { width:100%; order:3; color:var(--muted); font:10px/1.5 var(--mono); overflow-wrap:anywhere; }
  .task-status { display:inline-flex; align-items:center; gap:5px; min-height:24px; padding:2px 8px; color:#536c70; background:#e9eee7; border:1px solid #c6d0c6; border-radius:2px; font-size:11px; }
  .task-status::before { content:''; width:5px; height:5px; background:currentColor; }
  .task-status.ready { color:var(--good); border-color:#bdd5c3; background:#edf4eb; }
  .task-status.building { color:#306d89; border-color:#c0d3d7; background:#edf3f3; }
  .task-status.checking, .task-status.hold { color:var(--warn); border-color:#deceaa; background:#fbf4e5; }
  .task-status.failed { color:var(--risk); border-color:#dfb8ae; background:#fbefe9; }
  .task-dimensions { margin-top:12px; padding:10px 0; display:flex; flex-wrap:wrap; gap:6px 20px; border-top:1px solid #dfe3db; border-bottom:1px solid #dfe3db; color:#385761; font:12px/1.7 var(--mono); }
  .task-row p { margin:10px 0 0; color:var(--muted); font-size:12px; }
  .task-row-actions, .request-row-actions { display:flex; flex-wrap:wrap; gap:8px; margin-top:16px; }
  .task-row-actions .button, .task-row-actions button { font-size:12px; }
  .task-row time { grid-column:2; padding:0 24px 16px; color:var(--muted); font:10px/1.5 var(--mono); }
  .task-empty { padding:40px 24px; margin:0; border:1px dashed #9babaa; text-align:center; background:var(--paper); }
  .task-empty::before { content:'□'; display:block; margin-bottom:12px; color:#859c9f; font:40px/1 var(--mono); }
  .task-empty strong { display:block; margin-bottom:8px; color:var(--ink); font-size:17px; font-weight:500; }
  .task-empty span { display:block; color:var(--muted); font-size:13px; }
  .task-empty .button { margin-top:20px; }
  .feedback-tabs { display:flex; gap:20px; margin-top:24px; }
  .feedback-tab { position:relative; min-height:44px; padding:8px 0; border:0; border-bottom:2px solid transparent; border-radius:0; color:var(--muted); background:transparent; }
  .feedback-tab.active, .feedback-tab:hover { color:var(--ink); background:transparent; border-bottom-color:var(--hot); }
  .feedback-pane { display:none; }
  .feedback-pane.active { display:block; }
  .feedback-pane > .panel, #clarifications, #downloads, #feedback-history { margin-bottom:16px; border:1px solid var(--line); }
  .feedback-pane h2, #clarifications h2, #downloads h2, #feedback-history h2 { font-size:20px; }
  .feedback-simple-intro { margin:8px 0 24px; font-size:13px; }
  .feedback-layout { display:grid; grid-template-columns:minmax(0,1fr) minmax(200px,.32fr); gap:40px; }
  .feedback-guide { min-width:0; padding:0 0 0 24px; border-left:1px solid var(--line); }
  .feedback-guide h3 { margin-bottom:14px; font-size:14px; }
  .feedback-guide p { color:var(--muted); font-size:12px; line-height:1.85; }
  .evidence-sketch { width:100%; max-width:220px; height:auto; margin:20px 0; color:var(--brand); }
  .feedback-guide .sheet-label { margin-top:12px; font-size:9px; }
  .feedback-form { min-width:0; display:grid; gap:20px; }
  .feedback-core { display:grid; gap:20px; }
  .feedback-core label { gap:8px; }
  .feedback-field-number { margin-right:8px; color:var(--hot); font:11px var(--mono); }
  .feedback-core textarea { min-height:140px; }
  .feedback-field-hint, .feedback-upload-note { color:var(--muted); font-size:11px; font-weight:400; line-height:1.7; }
  .feedback-options { border-top:1px solid var(--line); border-bottom:1px solid var(--line); }
  .feedback-options > summary { min-height:48px; display:flex; align-items:center; gap:12px; }
  .feedback-options-grid { display:grid; grid-template-columns:repeat(2,minmax(0,1fr)); gap:18px; padding-bottom:20px; }
  .feedback-options-grid .wide { grid-column:1 / -1; }
  .form-subheading { display:flex; gap:12px; align-items:center; padding:12px 0 0; border-top:1px solid var(--line); font-size:14px; }
  .form-subheading span { color:var(--hot); font:11px var(--mono); }
  .feedback-simple .feedback-boundary { margin:0; padding:0; background:transparent; color:var(--muted); border:0; font-size:11px; }
  .feedback-submit-row { display:flex; flex-wrap:wrap; align-items:center; gap:16px; }
  .feedback-submit-row .muted { margin:0; font-size:11px; }
  .feedback-task-context { padding:12px 14px; border-left:2px solid var(--hot); background:#f5eee3; font-size:12px; }
  .feedback-history-block { margin:24px 0 0; padding-top:20px; border-top:1px solid var(--line); }
  .feedback-history-block h3 { font-size:16px; margin:0 0 12px; }
  .advanced-fields .feedback-history-block { margin:0; padding:16px; border:0; }
  .feedback-row { padding:20px; margin-top:12px; border:1px solid var(--line); border-radius:2px; background:var(--paper); }
  .feedback-row-head { display:flex; flex-wrap:wrap; align-items:center; gap:8px; }
  .feedback-row-head strong { flex:1 1 240px; color:var(--ink); font:12px/1.6 var(--mono); overflow-wrap:anywhere; }
  .feedback-row p { margin:8px 0 0; color:var(--muted); font-size:12px; }
  .feedback-detail { display:grid; grid-template-columns:repeat(2,minmax(0,1fr)); gap:0 20px; margin-top:16px; }
  .feedback-detail div { min-width:0; padding:12px 0; border-top:1px solid var(--line); }
  .feedback-detail span { display:block; color:var(--muted); font-size:11px; }
  .feedback-detail b { display:block; margin-top:4px; font-size:13px; font-weight:500; white-space:pre-wrap; overflow-wrap:anywhere; }
  .attachment-links { display:flex; flex-wrap:wrap; gap:8px; margin-top:12px; }
  .attachment-links a { min-height:44px; padding:8px 12px; display:inline-flex; align-items:center; border:1px solid var(--line); border-radius:2px; font-size:12px; text-decoration:none; }
  .clarification-list { display:grid; gap:16px; margin-top:20px; }
  .clarification-card { padding:20px; border:1px solid var(--line); border-left:2px solid var(--hot); background:var(--paper); }
  .clarification-head { display:flex; align-items:start; justify-content:space-between; gap:16px; }
  .clarification-head h3 { margin-top:4px; font-size:18px; }
  .clarification-kicker { margin:0; color:var(--hot); font:11px/1.6 var(--mono); }
  .clarification-source { margin:12px 0; color:var(--muted); font-size:11px; }
  .clarification-source code { background:var(--soft); padding:2px 4px; }
  .clarification-facts { margin:12px 0; padding:14px; background:#eef1e8; font-size:13px; }
  .clarification-facts ul { margin:8px 0 0; padding-left:20px; }
  .clarification-facts li + li { margin-top:6px; }
  .clarification-prompt { font-size:13px; }
  .clarification-images { display:grid; grid-template-columns:repeat(2,minmax(0,1fr)); gap:16px; margin:20px 0; }
  .clarification-images figure { min-width:0; margin:0; padding:8px; border:1px solid var(--line); background:var(--soft); }
  .clarification-images img { width:100%; max-height:360px; display:block; object-fit:contain; background:#fff; }
  .clarification-images figcaption { margin-top:8px; color:var(--muted); font-size:11px; }
  .clarification-response { display:grid; gap:16px; padding-top:20px; border-top:1px solid var(--line); }
  .clarification-response fieldset { min-width:0; margin:0; padding:0; border:0; }
  .clarification-response legend { margin-bottom:10px; font-size:13px; }
  .clarification-options { display:grid; gap:8px; }
  .clarification-options label { min-height:44px; padding:12px; display:flex; align-items:start; gap:10px; border:1px solid var(--line); cursor:pointer; }
  .clarification-options label:has(input:checked) { border-color:var(--brand); background:#eaf0e8; }
  .clarification-options input { margin:2px 0 0; }
  .clarification-submit-row { display:flex; align-items:center; flex-wrap:wrap; gap:14px; }
  .clarification-status { font-size:11px; }
  .archive-index { display:grid; grid-template-columns:repeat(3,minmax(0,1fr)); margin:24px 0; border:1px solid #aebbb8; }
  .archive-index div { padding:16px 20px; border-left:1px solid #aebbb8; }
  .archive-index div:first-child { border-left:0; }
  .archive-index span { color:var(--muted); font-size:11px; }
  .archive-index strong { display:block; margin-top:4px; color:var(--ink); font:24px/1.3 var(--sans); font-weight:300; }
  .archive-index strong small { color:var(--muted); font:11px var(--sans); }
  .auxiliary-panel { padding:0; }
  .auxiliary-summary { min-height:80px; padding:20px 28px; display:flex; align-items:center; justify-content:space-between; gap:16px; }
  .auxiliary-summary-copy p { margin:4px 0 0; font-size:12px; }
  .auxiliary-summary-action { color:var(--muted); font-size:11px; }
  .auxiliary-panel[open] > summary { border-bottom:1px solid var(--line); }
  .advanced-panel-body { padding:0 28px 24px; }
  .asset-library { display:grid; gap:20px; }
  .asset-row { display:grid; grid-template-columns:40px minmax(0,1fr) auto; gap:16px; align-items:center; padding:22px 0; border-bottom:1px solid var(--line); }
  .asset-kind { width:40px; height:48px; position:relative; display:grid; place-items:center; color:#627c82; border:1px solid #9bafae; font:9px/1 var(--mono); }
  .asset-kind::after { content:''; position:absolute; top:5px; right:5px; width:6px; height:6px; border-top:1px solid #9bafae; border-right:1px solid #9bafae; }
  .asset-copy { min-width:0; }
  .asset-title-line { display:flex; flex-wrap:wrap; align-items:center; gap:8px; }
  .asset-title-line h3 { font-size:15px; }
  .asset-row p { margin:6px 0; max-width:86ch; color:var(--muted); font-size:12px; }
  .asset-file-meta { display:flex; flex-wrap:wrap; gap:8px 16px; color:var(--muted); font:10px/1.8 var(--mono); overflow-wrap:anywhere; }
  .asset-file-meta span { min-width:0; }
  .asset-download { min-width:110px; font-size:12px; }
  .history-assets { border:1px solid var(--line); }
  .history-assets > summary { min-height:46px; padding:12px 16px; display:flex; align-items:center; gap:12px; font-size:12px; }
  .history-assets .asset-list { margin:0 16px; }
  .history-assets .asset-row:last-child { border-bottom:0; }
  #feedback-history > .muted { font-size:12px; }
  .workspace-footer { display:flex; justify-content:space-between; gap:12px; margin-top:28px; padding-top:16px; border-top:1px solid var(--line); color:var(--muted); font:9px/1.6 var(--mono); letter-spacing:.04em; }
  @media(min-width:1600px) { .workspace-main { padding-left:48px; padding-right:48px; } .studio-grid { gap:40px; } }
  @media(max-width:1200px) { .shell { grid-template-columns:200px minmax(0,1fr); } .workspace-main { padding:0 24px 28px; } .panel { padding:24px; } .studio-grid { gap:24px; grid-template-columns:minmax(280px,.9fr) minmax(0,1.1fr); } .preview-controls { grid-template-columns:30px minmax(0,1fr) 42px; } .workspace-stamp { min-width:142px; } .auth-study { padding:32px; } .auth-entry { padding:36px; } }
  @media(max-width:1024px) { .studio-grid { grid-template-columns:minmax(0,1fr); } .layout-review-panel { width:100%; position:static; } .feedback-layout { grid-template-columns:minmax(0,1fr); } .feedback-guide { display:none; } .workspace-stamp { display:none; } .auth-study-title h2 { font-size:28px; } }
  @media(max-width:820px) {
    .shell { grid-template-columns:minmax(0,1fr); }
    .portal-sidebar { height:auto; min-height:0; position:sticky; top:0; z-index:20; padding:12px 20px 0; display:grid; grid-template-columns:104px minmax(0,1fr) auto; gap:12px; align-items:center; }
    .portal-logo { width:104px; height:36px; }
    .portal-sidebar > .portal-logo { grid-column:1; grid-row:1; }
    .portal-logo img { width:94px; max-height:30px; }
    .portal-sidebar h2 { grid-column:2; grid-row:1; font-size:13px; }
    .portal-sidebar .portal-kicker, .side-intro, .side-study, .side-boundary, .account-avatar, .account-copy { display:none; }
    .account-card { grid-column:3; grid-row:1; padding:0; margin:0; }
    .account-card a { min-height:44px; }
    .portal-nav { grid-column:1 / -1; grid-row:2; margin:0 -20px; display:grid; grid-template-columns:repeat(4,minmax(0,1fr)); gap:0; }
    .portal-nav a { min-height:48px; justify-content:center; padding:8px 2px; gap:6px; border-left:0; border-bottom:2px solid transparent; }
    .portal-nav a.active { border-left:0; border-bottom-color:#ee936e; }
    .portal-nav-copy strong { font-size:12px; }
    .portal-nav-copy small { display:none; }
    .portal-nav-index { width:auto; font-size:9px; }
    .workspace-topline { min-height:48px; font-size:9px; }
    .workspace-main { padding:0 20px 24px; }
    .review-overview, .workspace-view > .panel:first-child { padding:28px 0 20px; }
    h1 { font-size:28px; }
    .workflow-rail { margin-top:20px; }
    .workflow-rail li { display:flex; min-height:60px; flex-direction:column; align-items:center; gap:4px; padding:10px 4px; }
    .workflow-rail li:first-child { padding-left:4px; }
    .workflow-rail small { display:none; }
    .view-button-row .button { min-height:44px; }
    .preview-controls input[type="range"] { min-height:44px; }
    .workflow-rail span { font-size:11px; }
    .engineering-boundary { font-size:11px; gap:8px; }
    .task-row-main { padding:18px; }
    .task-row time { padding:0 18px 16px; }
    .auth-wrap { grid-template-columns:minmax(0,1fr); }
    .auth-study { min-height:0; padding:24px; }
    .auth-study-title { margin-top:28px; }
    .auth-study-title h2 { font-size:24px; }
    .auth-study-title h2 br { display:none; }
    .auth-study-title p:last-child { max-width:80%; font-size:12px; }
    .auth-visual { margin:22px auto 12px; max-width:600px; }
    .auth-visual-stage { height:320px; margin-top:0; }
    .auth-study-hint { margin-bottom:10px; }
    .auth-study-footer { margin-top:24px; padding-top:14px; }
    .auth-entry { padding:36px 24px; }
    .auth-entry { grid-row:1; }
    .auth-study { grid-row:2; }
    .auth-note { margin-top:24px; }
    .feedback-pane > .panel, #clarifications { scroll-margin-top:130px; }
  }
  @media(max-width:540px) {
    .workspace-main { padding:0 16px 24px; }
    .workspace-topline > span:last-child { font-size:8px; letter-spacing:0; }
    .simple-intro .muted, .workspace-heading-copy > .muted { font-size:12px; }
    .panel { padding:20px 16px; }
    .section-heading { flex-direction:column; gap:12px; padding-bottom:16px; }
    .section-heading .chip { display:none; }
    .section-heading h2 { font-size:18px; }
    .section-heading p.muted { font-size:12px; }
    .auxiliary-body { padding-top:20px; }
    .studio-grid { gap:24px; }
    .field-grid, .field-grid.two, .customer-parameter-grid { grid-template-columns:repeat(2,minmax(0,1fr)); gap:14px 12px; }
    .customer-parameter-grid label:nth-child(2) { grid-column:1 / -1; }
    .field-label small { font-size:9px; }
    .engineering-boundary { display:grid; gap:4px; }
    .preview-head { padding:12px; }
    .preview-head strong { font-size:13px; }
    .preview-head .chip { font-size:9px; }
    .viewport-toolbar { padding:6px 8px; }
    .preview-legend { display:none; }
    .view-button-row { width:100%; justify-content:space-between; }
    .view-button-row .button { min-height:44px; padding:6px 14px; }
    .drawing-corner { top:8px; left:10px; font-size:8px; }
    .drawing-scale { top:8px; right:10px; font-size:8px; }
    .viewport-hud div { padding:9px 10px; }
    .viewport-hud strong { font-size:10px; }
    .preview-controls { gap:8px; padding:10px; }
    .generation-confirm { padding:16px; }
    .generation-confirm-summary { grid-template-columns:1fr; }
    .task-toolbar { align-items:stretch; gap:14px; }
    .task-search { width:100%; }
    .task-filter-buttons { width:100%; gap:0; justify-content:space-between; }
    .task-filter-buttons button { padding:8px; }
    .task-row-main { padding:16px 14px; }
    .task-row-title strong { font-size:15px; }
    .task-dimensions { font-size:11px; }
    .task-row-actions { gap:8px; }
    .task-row-actions .button, .task-row-actions > button { flex:1 1 auto; }
    .task-row time { padding:0 14px 14px; }
    .task-state-key { gap:8px 12px; font-size:10px; }
    .feedback-options-grid, .feedback-detail, .clarification-images { grid-template-columns:minmax(0,1fr); }
    .feedback-options-grid .wide { grid-column:auto; }
    .feedback-row, .clarification-card { padding:16px; }
    .clarification-head { flex-direction:column; gap:10px; }
    .archive-index div { padding:12px; }
    .archive-index span { font-size:10px; }
    .archive-index strong { font-size:20px; }
    .auxiliary-summary { padding:16px; gap:10px; }
    .auxiliary-summary-action { display:none; }
    .advanced-panel-body { padding:0 16px 16px; }
    .asset-row { grid-template-columns:28px minmax(0,1fr); gap:12px; padding:18px 0; }
    .asset-kind { width:28px; height:36px; font-size:8px; }
    .asset-download { grid-column:2; justify-self:start; }
    .asset-title-line h3 { font-size:13px; }
    .workspace-footer { flex-direction:column; gap:4px; }
    .auth-study-top > span { font-size:8px; }
    .auth-study-title h2 { max-width:100%; font-size:23px; }
    .auth-card h1 { font-size:28px; }
    .auth-study-footer { font-size:8px; }
    .auth-study { padding:20px; }
    .auth-study-title { margin-top:20px; }
    .auth-visual-stage { height:246px; }
    .auth-visual-meta { font-size:9px; }
    .auth-study-controls button { padding:8px 10px; }
    .auth-explode-control { gap:8px; }
  }
  @media(prefers-reduced-motion:reduce) { .auth-visual-orbit, .auth-scan { animation:none !important; } }
  @media(prefers-reduced-motion:reduce) { *, *::before, *::after { transition:none !important; scroll-behavior:auto !important; } }
  :root {
    --ink:#222733; --muted:#68717f; --line:#dfe3e9; --soft:#f3f4f7; --paper:#fff;
    --brand:#315bea; --navy:#242a34; --hot:#315bea; --good:#237558; --warn:#90621d; --risk:#b03945;
    --sans:"Segoe UI Variable Text", "Segoe UI", "Microsoft YaHei UI", "Microsoft YaHei", sans-serif;
  }
  body { font-size:14px; background:var(--soft); }
  h1 { font-size:34px; letter-spacing:-.035em; font-weight:650; }
  h2 { font-weight:600; }
  .eyebrow { color:var(--muted); font:500 12px/1.6 var(--sans); letter-spacing:.04em; }
  input, select, textarea { border-color:#d8dde6; border-radius:8px; background:#fafbfd; font-size:14px; }
  input:hover, select:hover, textarea:hover { border-color:#a8b2c4; }
  input:focus, select:focus, textarea:focus { background:white; border-color:var(--brand); box-shadow:0 0 0 3px #315bea12; }
  button, .button { border-radius:8px; font-size:13px; font-weight:600; }
  button:hover, .button:hover { background:#254ad3; }
  .button.secondary, button.secondary { border-color:var(--line); background:#fff; color:#4b5565; }
  .button.secondary:hover, button.secondary:hover { background:#f1f4fb; border-color:#b7c5e6; color:var(--brand); }
  :where(a,button,input,select,textarea,summary):focus-visible { outline:3px solid #315bea66; }
  button:disabled, .button[aria-disabled="true"] { color:#7b8391; background:#eff1f5; border-color:#e0e5ec; }
  .chip, .engineering-chip { border-radius:6px; border-color:#e1e5ee; background:#f1f3f8; color:#566279; padding:4px 8px; }
  .chip.good, .task-status.ready { background:#eaf7f1; border-color:#d3e9dd; }
  .chip.warn, .task-status.checking, .task-status.hold { background:#fff5e5; border-color:#f0dfbf; }
  .alert, .ok, .feedback-boundary, .gate-warning, .request-warning { border-radius:9px; }
  .alert { background:#fff0f2; border-color:#f1cdd3; }
  .ok, .generation-capability-ready { background:#edf8f2; border-color:#d2e9dd; }
  .shell { display:block; }
  .portal-sidebar { height:80px; min-height:80px; padding:0 max(28px,calc((100vw - 1520px) / 2)); display:flex; flex-direction:row; align-items:center; gap:28px; overflow:visible; z-index:30; background:#fff; color:var(--ink); border-bottom:1px solid var(--line); }
  .workspace-brand { display:flex; align-items:center; gap:16px; flex:0 0 auto; }
  .portal-logo { width:82px; height:44px; border-radius:0; background:white; }
  .portal-logo img { width:76px; max-height:38px; }
  .workspace-brand-copy { display:grid; gap:2px; border-left:1px solid var(--line); padding-left:16px; }
  .workspace-brand-copy strong { font-size:13px; font-weight:650; }
  .workspace-brand-copy span { font-size:10px; color:var(--muted); }
  .portal-nav { display:flex; gap:4px; margin:0 auto; }
  .portal-nav a { min-height:44px; padding:11px 15px; gap:8px; border:0; border-radius:8px; color:#697181; }
  .portal-nav a:hover { background:#f3f5fa; color:var(--ink); }
  .portal-nav a.active { color:var(--brand); background:#eef2ff; border:0; }
  .portal-nav-icon { width:17px; height:17px; flex:0 0 auto; }
  .portal-nav-index, .portal-nav-copy small { display:none; }
  .portal-nav-copy strong { font-size:13px; font-weight:600; }
  .account-card { flex:0 0 auto; margin:0; padding:0; gap:10px; }
  .account-avatar { width:32px; height:32px; border:0; border-radius:50%; background:#e8edf6; color:#53647c; }
  .account-copy span { display:none; }
  .account-copy strong { max-width:96px; color:var(--ink); font:12px var(--sans); }
  .account-card a { color:var(--muted); font-size:12px; margin-left:6px; }
  .account-card a:hover { color:var(--brand); }
  .workspace-main { max-width:1520px; margin:auto; padding:0 36px 32px; }
  .workspace-topline { min-height:52px; border:0; font:11px var(--sans); letter-spacing:0; }
  .workspace-topline i { width:6px; height:6px; border-radius:50%; background:#81978d; }
  .workspace-topline span:last-child { color:#8b929f; }
  .workspace-topline b { color:#596477; }
  .review-overview, .workspace-view > .panel:first-child { padding:18px 0 28px; }
  .workspace-stamp { min-width:158px; border:0; padding:10px 14px; border-radius:10px; background:#e8ecf3; }
  .workspace-stamp span, .workspace-stamp strong { padding:0; font:11px/1.7 var(--sans); }
  .workspace-stamp strong, .workspace-stamp .stamp-title { border:0; }
  .workspace-stamp .stamp-title { margin-top:3px; font-size:12px; }
  .simple-intro .muted, .workspace-heading-copy > .muted { max-width:76ch; margin-top:10px; font-size:13px; }
  .workflow-rail { border:0; gap:8px; margin-top:26px; }
  .workflow-rail li { min-height:58px; padding:10px 14px; grid-template-columns:28px minmax(0,1fr); border:0; border-radius:10px; background:#e9ecf2; }
  .workflow-rail li:first-child { padding-left:14px; }
  .workflow-rail b { width:26px; height:26px; border-radius:50%; background:#dfe4ed; display:grid; place-items:center; align-self:center; font-size:10px; }
  .workflow-rail li.active { background:#eaf0ff; }
  .workflow-rail li.active::after { display:none; }
  .workflow-rail .active b { color:#fff; background:var(--brand); }
  .workflow-rail span { font-size:12px; }
  .workflow-rail small { font-size:10px; color:#78828f; }
  .engineering-boundary { margin-top:16px; font-size:11px; }
  .panel { border:1px solid var(--line); border-radius:16px; background:#fff; }
  .generation-primary { padding:0; border:0; background:transparent; }
  .generation-primary > .section-heading { display:none; }
  .auxiliary-body { padding-top:0; }
  .studio-grid { grid-template-columns:minmax(390px,.9fr) minmax(0,1.1fr); gap:24px; }
  .generator-form { padding:26px; border:1px solid var(--line); border-radius:16px; background:#fff; gap:18px; }
  .generator-title { color:var(--ink); font-size:18px; font-weight:600; margin-bottom:2px; }
  .width-basis-note { color:var(--muted); font-size:11px; font-weight:400; }
  .field-label { font-size:12px; }
  .field-label small { font-size:10px; color:#8d96a5; }
  .customer-parameter-grid { gap:18px 16px; }
  .door-layout-editor { padding-top:20px; }
  .door-layout-heading h3 { font-size:15px; font-weight:600; }
  .door-layout-note { font-size:11px; }
  .door-layout-presets button { padding:9px 12px; }
  .door-columns { grid-template-columns:repeat(2,minmax(0,1fr)); gap:14px; }
  .door-column { border-color:#e2e6ed; border-radius:10px; background:#fafbfd; padding:12px; }
  .door-column legend { font:600 12px var(--sans); color:#53627a; }
  .door-column-count { grid-template-columns:minmax(0,1fr) 72px; font-size:12px; gap:6px; }
  .door-column-count input { background:#fff; }
  .door-height-row { grid-template-columns:28px minmax(65px,1fr) 19px; gap:5px; }
  .door-height-row input { background:#fff; }
  .door-allocation-switch { display:flex; gap:3px; padding:3px; margin:12px 0; border-radius:8px; background:#edf0f6; }
  .door-allocation-switch button { flex:1; min-height:44px; padding:7px 4px; border:0; border-radius:6px; color:#78859a; background:transparent; font-size:11px; }
  .door-allocation-switch button[aria-pressed="true"] { color:var(--brand); background:#fff; box-shadow:0 1px 4px #20305008; }
  .door-value-field { min-width:0; display:grid; gap:4px; }
  .door-value-field small { font:10px/1.4 var(--mono); color:#73849d; text-align:right; }
  .door-height-row { grid-template-columns:28px minmax(65px,1fr) 46px; gap:6px; }
  .door-height-row input[readonly] { color:#5077b6; background:#edf3fc; border-color:#d5e1f5; }
  .door-height-toggle { min-height:44px; padding:6px 3px; background:transparent; color:#8590a1; border:1px solid #e2e6ed; border-radius:7px; font-size:10px; }
  .door-height-toggle[aria-pressed="true"] { color:#456fc0; border-color:#c4d7f5; background:#eff5ff; }
  .door-height-toggle:hover { color:var(--brand); background:#e7eefb; border-color:#b2c9ee; }
  .door-column[data-allocation-mode="ratio"] .door-height-row { grid-template-columns:28px minmax(65px,1fr) 46px; }
  .door-column-actions { display:grid; grid-template-columns:1fr; gap:6px; }
  .door-column-actions button { font-size:11px; padding:8px 6px; }
  .door-column-summary, .door-column-mode { font-size:10px; }
  .door-layout-status { border:0; border-radius:8px; background:#edf8f2; }
  .door-layout-status[data-valid="false"] { background:#fff0f2; }
  #doorCount[readonly] { color:#748099; background:#eff2f7; }
  .generated-prompt { border:0; border-radius:8px; color:#616d80; background:#f2f4f8; font-size:11px; padding:12px; }
  .advanced-fields, .history-assets { border-radius:10px; }
  .layout-review-panel { top:102px; }
  .preview-panel { border:1px solid #dfe4ec; border-radius:16px; background:white; box-shadow:0 12px 32px #26344706; }
  .preview-head { min-height:72px; background:#fff; color:var(--ink); padding:18px 22px; }
  .preview-head span:not(.chip) { color:#8791a1; font:10px/1.5 var(--sans); letter-spacing:.02em; }
  .preview-head strong { font-size:17px; font-weight:600; }
  .preview-head .chip { color:#708097; border-color:#dfe5ee; background:#f3f5f8; border-radius:6px; }
  .viewport-toolbar { padding:8px 16px; background:#f7f8fa; border:0; }
  .view-button-row { padding:3px; background:#e9edf3; border-radius:8px; gap:0; }
  .view-button-row .button { min-height:44px; padding:7px 12px; border-radius:6px; font:12px var(--sans); background:transparent; color:#7a8494; }
  .view-button-row .view-active { background:#fff; border-color:transparent; color:var(--brand); box-shadow:0 1px 4px #29374d10; }
  .preview-legend, .drawing-corner, .drawing-scale { color:#778498; }
  .preview-canvas-wrap { background:#f4f5f7; }
  .viewport-hud { border-color:var(--line); grid-template-columns:repeat(2,minmax(0,1fr)); padding:8px; }
  .viewport-hud div { padding:8px 12px; border:0; }
  .viewport-hud strong { color:#465369; font-size:11px; }
  .preview-controls { border-color:var(--line); padding:12px 20px; }
  .preview-note { background:#fafbfd; padding:14px 20px; font-size:10px; }
  .generation-confirm { border:1px solid #b8c9ff; border-radius:10px; background:#f0f4ff; }
  .generation-confirm-head span:first-child { color:#5770ab; }
  .generation-confirm-summary, .generation-confirm-summary div { border-color:#d7e1fa; }
  .generation-confirm-summary dt, .generation-confirm-boundary { color:#677694; }
  .task-toolbar { margin-top:24px; padding:18px; border:1px solid var(--line); border-radius:12px; background:white; }
  .task-filter-buttons { padding:3px; background:#f0f2f7; border-radius:8px; }
  .task-filter-buttons button { min-height:44px; }
  .task-filter-buttons button.active { color:var(--brand); background:#fff; border-color:#e0e6f1; }
  .task-state-key { padding:0 4px; }
  .task-state-key i { border-radius:50%; }
  .task-row { overflow:hidden; border-radius:12px; grid-template-columns:4px minmax(0,1fr); }
  .task-row-main { padding:22px 24px 16px; }
  .task-row-title strong { font-size:16px; font-weight:600; }
  .task-status { border-radius:6px; }
  .task-dimensions { background:#f6f8fb; padding:10px 12px; border:0; border-radius:7px; color:#54647c; }
  .task-empty { border-color:#d1d8e3; border-radius:12px; }
  .feedback-tabs { gap:6px; padding:4px; width:fit-content; background:#e7ebf2; border-radius:9px; }
  .feedback-tab { min-width:120px; border:0; border-radius:7px; padding:10px 14px; }
  .feedback-tab.active, .feedback-tab:hover { border:0; background:#fff; color:var(--brand); }
  .feedback-pane > .panel, #clarifications, #downloads, #feedback-history { border-radius:14px; }
  .feedback-layout { gap:32px; grid-template-columns:minmax(0,1fr) minmax(220px,.38fr); }
  .feedback-guide { padding:20px; border:0; border-radius:12px; background:#f3f5f9; align-self:start; }
  .feedback-guide h3 { font-weight:600; }
  .feedback-task-context { border:0; border-radius:8px; background:#eef2ff; color:#4c64a2; }
  .feedback-row { border-radius:10px; }
  .clarification-card { border-radius:12px; }
  .clarification-facts { background:#f3f5f9; border-radius:8px; }
  .archive-index { border:0; gap:16px; }
  .archive-index div { border:1px solid var(--line); border-radius:12px; padding:20px; background:white; }
  .archive-index div:first-child { border:1px solid var(--line); }
  .archive-index strong { font-size:28px; font-weight:600; }
  .asset-kind { border-color:#c5d2ed; background:#f0f4ff; color:#6880b5; border-radius:5px; }
  .workspace-footer { border:0; font:10px/1.6 var(--sans); letter-spacing:0; }

  .auth-wrap { display:grid; min-height:100vh; max-width:1920px; margin:auto; padding:0 40px; background:#f6f7f9; grid-template-columns:minmax(0,1.25fr) minmax(0,1fr); grid-template-rows:92px minmax(720px,1fr) 58px; column-gap:0; }
  .auth-masthead { grid-column:1/-1; display:flex; align-items:center; justify-content:space-between; gap:16px; }
  .studio-brand { display:flex; align-items:center; gap:18px; color:#4d5767; font-size:13px; font-weight:500; }
  .studio-brand img { width:96px; height:48px; object-fit:contain; mix-blend-mode:multiply; }
  .studio-brand > span { padding-left:18px; border-left:1px solid #d6dce5; }
  .auth-edition { display:flex; align-items:center; gap:8px; color:#758093; font-size:11px; }
  .auth-edition i { width:6px; height:6px; background:#82958c; border-radius:50%; }
  .auth-study { position:relative; min-width:0; display:flex; flex-direction:column; min-height:0; padding:36px 36px 20px; border-radius:22px 0 0 22px; background:#e8ebef; color:var(--ink); overflow:hidden; }
  .auth-study::before { display:none; }
  .auth-study-title { margin:0; z-index:2; pointer-events:none; }
  .auth-study-title .eyebrow { color:#7b8592; margin-bottom:18px; font-size:12px; }
  .auth-study-title h2 { color:#282f3b; font-size:clamp(40px,4vw,58px); line-height:1.2; font-weight:600; letter-spacing:-.055em; }
  .auth-study-title p:last-child { font-size:12px; color:#6b7585; line-height:1.9; margin-top:20px; }
  .auth-process { display:flex; gap:20px; list-style:none; margin:24px 0 0; padding:0; }
  .auth-process li { display:flex; align-items:center; gap:8px; color:#525f72; font-size:11px; }
  .auth-process b { color:#8f99a7; font:9px var(--mono); }
  .auth-process li + li::before { content:'→'; color:#99a3b0; margin-right:4px; }
  .auth-entry { display:flex; align-items:center; justify-content:center; min-width:0; padding:48px; border-radius:0 22px 22px 0; background:white; }
  .auth-card { width:min(100%,370px); }
  .auth-card-symbol { width:46px; height:46px; border-radius:13px; background:#f0f4ff; color:var(--brand); display:grid; place-items:center; margin-bottom:32px; }
  .auth-card-symbol svg { width:29px; height:29px; }
  .auth-card .eyebrow { font-size:11px; margin-bottom:8px; color:#8390a3; }
  .auth-card h1 { font-size:34px; font-weight:650; letter-spacing:-.04em; }
  .auth-description { margin-top:14px; font-size:12px; line-height:1.8; }
  .auth-form { display:grid; gap:22px; margin-top:32px; }
  .auth-form label { gap:9px; }
  .auth-form input { min-height:52px; background:#f7f8fb; border-color:#e2e6ed; border-radius:9px; padding:12px 14px; }
  .auth-form button { min-height:52px; justify-content:space-between; border-radius:9px; margin-top:4px; }
  .auth-form button::after { display:none; }
  .auth-form button > span { font-size:20px; font-weight:400; }
  .auth-switch { display:flex; align-items:center; border:0; padding:0; justify-content:center; gap:8px; font-size:11px; margin-top:22px; }
  .auth-switch a { display:inline-flex; align-items:center; min-height:44px; }
  .auth-switch a { color:var(--brand); text-decoration:none; }
  .auth-note { margin-top:44px; display:flex; align-items:start; gap:10px; font:10px/1.8 var(--sans); color:#9099a7; }
  .auth-note p { margin:0; }
  .auth-note-dot { margin-top:6px; width:5px; height:5px; border-radius:50%; background:#a7b1c0; flex:0 0 auto; }
  .auth-footer { grid-column:1/-1; display:flex; justify-content:space-between; align-items:center; color:#9ba3af; font-size:9px; letter-spacing:.02em; }
  @media(min-width:1600px) { .workspace-main { padding-left:40px; padding-right:40px; } .studio-grid { gap:28px; } .auth-wrap { padding:0 64px; } .auth-study { padding:48px 48px 26px; } .auth-study-title h2 { font-size:64px; } .product-cabinet { max-height:620px; } .product-scene { margin-top:-80px; } }
  @media(max-width:1200px) { .portal-sidebar { gap:12px; padding:0 24px; } .workspace-brand-copy { display:none; } .portal-nav a { padding:10px 12px; } .workspace-main { padding:0 24px 28px; } .studio-grid { grid-template-columns:minmax(360px,1fr) minmax(0,1.08fr); gap:20px; } .generator-form { padding:20px; } .door-columns { gap:10px; } .auth-wrap { padding:0 24px; grid-template-columns:minmax(0,1.08fr) minmax(0,1fr); } .auth-study { padding:30px 26px 20px; } .auth-entry { padding:40px; } .product-series { display:none; } .product-scene-toolbar { justify-content:flex-end; } .product-caption > span { width:100%; } .product-scene { margin-top:-24px; } }
  @media(max-width:1024px) { .studio-grid { grid-template-columns:1fr; } .layout-review-panel { position:static; } .feedback-layout { grid-template-columns:1fr; } .feedback-guide { display:none; } .auth-wrap { grid-template-rows:80px minmax(650px,1fr) 50px; } .auth-study-title h2 { font-size:44px; } .product-stage { min-height:340px; } .product-scene { min-height:360px; margin-top:0; } .auth-process { gap:8px; } .auth-process li { gap:4px; font-size:10px; } .auth-entry { padding:30px; } }
  @media(max-width:820px) { .portal-sidebar { height:auto; padding:12px 20px 0; display:grid; grid-template-columns:1fr auto; gap:8px; } .workspace-brand { grid-column:1; grid-row:1; } .workspace-brand-copy { display:grid; } .portal-sidebar .portal-logo { width:70px; height:36px; } .portal-sidebar .portal-logo img { width:66px; max-height:32px; } .account-card { grid-column:2; grid-row:1; padding:0; } .account-avatar, .account-copy { display:none; } .portal-nav { grid-column:1/-1; grid-row:2; width:100%; margin:0; display:grid; grid-template-columns:repeat(4,minmax(0,1fr)); gap:2px; padding-bottom:8px; } .portal-nav a, .portal-nav a.active { min-height:44px; padding:8px 4px; border:0; justify-content:center; gap:6px; } .portal-nav-icon { width:15px; height:15px; } .portal-nav-copy strong { font-size:12px; } .workspace-main { padding:0 20px 24px; } .workspace-topline { font-size:10px; } .workflow-rail { gap:5px; margin-top:20px; } .workflow-rail li, .workflow-rail li:first-child { display:flex; min-height:68px; padding:10px 4px; gap:6px; } .workflow-rail small { display:none; } .workflow-rail span { font-size:10px; } .engineering-boundary { font-size:10px; } .auth-wrap { display:grid; grid-template-columns:1fr; grid-template-rows:72px auto auto 48px; padding:0 20px; } .auth-masthead { grid-row:1; } .auth-entry { grid-row:2; border-radius:18px; padding:36px 28px; } .auth-study { grid-row:3; margin-top:16px; border-radius:18px; padding:28px; } .auth-study-title h2 { font-size:40px; } .auth-study-title h2 br { display:block; } .auth-study-title p:last-child { max-width:none; } .auth-card-symbol { margin-bottom:24px; } .auth-note { margin-top:30px; } .product-scene { margin-top:-130px; min-height:400px; } .product-stage { min-height:420px; } .product-scene-toolbar { margin-top:-12px; } .product-cabinet { max-height:460px; } .product-series { display:inline; } .product-caption > span { width:auto; } .auth-process { gap:20px; } .auth-process li { gap:6px; font-size:11px; } .auth-footer { grid-row:4; } .auth-footer span:last-child { display:none; } }
  @media(max-width:540px) { .workspace-main { padding:0 16px 24px; } h1 { font-size:28px; } .workspace-brand-copy strong { font-size:12px; } .workspace-brand-copy span { font-size:9px; } .portal-sidebar { padding-left:16px; padding-right:16px; } .portal-nav-copy strong { font-size:11px; } .portal-nav-icon { display:none; } .workspace-topline span:last-child { display:none; } .generator-form { padding:18px 14px; } .customer-parameter-grid { gap:16px 12px; } .door-columns { grid-template-columns:1fr; } .door-column-actions { grid-template-columns:1fr 1fr; } .door-height-row { grid-template-columns:36px minmax(70px,1fr) 20px; } .door-column-count { grid-template-columns:minmax(0,1fr) 86px; } .task-toolbar { padding:14px; } .task-filter-buttons { width:100%; gap:0; } .task-filter-buttons button { flex:1; padding:8px 5px; font-size:11px; } .archive-index { gap:8px; } .archive-index div { padding:14px 10px; } .archive-index strong { font-size:24px; } .auth-wrap { padding:0 12px; } .studio-brand { gap:10px; font-size:11px; } .studio-brand img { width:70px; height:40px; } .studio-brand > span { padding-left:10px; } .auth-edition { font-size:9px; gap:5px; } .auth-entry { padding:32px 24px; } .auth-card h1 { font-size:30px; } .auth-study { padding:26px 20px 20px; } .auth-study-title h2 { font-size:38px; } .auth-study-title p:last-child { font-size:11px; } .product-scene { margin-top:-50px; min-height:330px; } .product-stage { min-height:330px; } .product-cabinet { max-height:360px; } .product-series { display:none; } .product-caption { gap:4px; } .product-caption p { font-size:10px; } .product-caption > span { font-size:9px; } .auth-process { gap:10px; flex-wrap:wrap; } .auth-process li { font-size:10px; } .auth-process li + li::before { margin-right:0; } }
  @media(prefers-reduced-motion:reduce) { .product-model, .product-doors, .product-interior-callout { transition:none; } }

  @media(max-width:540px) { .door-height-row { grid-template-columns:36px minmax(70px,1fr) 46px; } }
${authCabinetShowcaseCss}
</style>
</head>
<body>${content}</body>
</html>`
}

function renderAssetCards() {
  const assets = orderedAssets().map(listingAssetInfo)
  const renderRow = (asset, current) => `
    <article class="asset-row${current ? ' current' : ''}">
      <div class="asset-kind" aria-hidden="true">${htmlEscape(extname(asset.fileName).slice(1).toUpperCase() || 'FILE')}</div>
      <div class="asset-copy">
        <div class="asset-title-line">
          <h3>${htmlEscape(asset.title)}</h3>
          <span class="chip${current && asset.id === CURRENT_REVIEW_ASSET_ID ? ' good' : ''}">${htmlEscape(asset.category)}</span>
        </div>
        <p>${htmlEscape(asset.description)}</p>
        <div class="asset-file-meta">
          <span>${htmlEscape(visibleAssetFileName(asset.fileName))}</span>
          ${asset.available ? `<span>${formatBytes(asset.sizeBytes)}</span>` : '<span>共享目录文件缺失</span>'}
        </div>
      </div>
      ${asset.available ? `<a class="button${current ? '' : ' secondary'} asset-download" href="/download/${asset.id}">下载文件</a>` : '<button class="asset-download" disabled>文件缺失</button>'}
    </article>
  `
  const reviewAssets = assets.slice(1, 3)
  const historicalAssets = assets.slice(3)
  return `
    <div class="asset-list current-assets">${reviewAssets.map((asset) => renderRow(asset, false)).join('')}</div>
    <details class="history-assets">
      <summary>历史参考文件（${historicalAssets.length}）</summary>
      <div class="asset-list">${historicalAssets.map((asset) => renderRow(asset, false)).join('')}</div>
    </details>
  `
}

function feedbackRows(rows) {
  if (!rows.length) return '<p class="muted">当前轮次还没有提交记录。</p>'
  return rows.map((item) => `
    <div class="feedback-row">
      <div class="feedback-row-head">
        <strong>${htmlEscape(item.id)} / ${htmlEscape(item.reviewerName || item.username)}</strong>
        <span class="chip">${htmlEscape(item.recordTypeLabel || RECORD_TYPE_LABELS.structure_issue)}</span>
        ${item.severity ? `<span class="chip ${item.severity === 'P0' ? 'warn' : ''}">${htmlEscape(item.severity)}</span>` : ''}
        <span class="chip">${htmlEscape(item.decisionLabel || item.decision)}</span>
        ${item.clarificationId ? `<span class="chip good">确认答复 · ${htmlEscape(item.clarificationId)}</span>` : ''}
      </div>
      <p>${htmlEscape(item.issueCategoryLabel || '结构反馈')} / ${htmlEscape(item.reviewTargetLabel || item.reviewTarget)}</p>
      <div class="feedback-detail">
        <div><span>模型/零件</span><b>${htmlEscape(item.componentName || '-')}</b></div>
        <div><span>位置</span><b>${htmlEscape(item.modelLocation || '-')}</b></div>
        <div><span>当前问题</span><b>${htmlEscape(item.currentProblem || item.summary || '已提交反馈')}</b></div>
        <div><span>期望与验收</span><b>${htmlEscape(item.expectedResult || '-')}\n${htmlEscape(item.acceptanceCriteria || '')}</b></div>
      </div>
      ${item.attachmentCount ? `<div class="attachment-links">${Array.from({ length: item.attachmentCount }, (_, index) => `<a href="/feedback/${encodeURIComponent(item.id)}/attachment/${index + 1}" target="_blank" rel="noopener">查看附件 ${index + 1}</a>`).join('')}</div>` : ''}
      <p>${new Date(item.submittedAt).toLocaleString('zh-CN', { hour12: false })} · 用于记录结构工程师需要继续修改或确认的内容。</p>
    </div>
  `).join('')
}

function efficiencyValidationRows(rows) {
  if (!rows.length) return '<p class="muted">当前还没有生成模型应用效果记录。</p>'
  return rows.map((item) => {
    const value = item.efficiencyValidation || {}
    const actualMinutes = value.generatedModelActualMinutes ?? value.v35ActualMinutes ?? '-'
    const savingsText = value.measurementComplete
      ? Number(value.timeSavingsMinutes) > 0
        ? `节省 ${htmlEscape(value.timeSavingsMinutes)} 分钟（${htmlEscape(value.timeSavingsPercent)}%）`
        : Number(value.timeSavingsMinutes) < 0
          ? `增加 ${htmlEscape(Math.abs(Number(value.timeSavingsMinutes)))} 分钟（${htmlEscape(Math.abs(Number(value.timeSavingsPercent)))}%）`
          : '耗时持平'
      : '任务成果未完整，不计算节省时间'
    const manualItems = Array.isArray(value.manualModificationItems) ? value.manualModificationItems : []
    const deliverables = value.deliverableLabels || {}
    return `
      <div class="feedback-row efficiency-row">
        <div class="feedback-row-head">
          <strong>${htmlEscape(item.id)} / ${htmlEscape(item.reviewerName || item.username)}</strong>
          <span class="chip good">生成模型应用效果记录</span>
          <span class="chip">${htmlEscape(value.taskKindLabel || TASK_KIND_LABELS[value.taskKind] || value.taskKind || '-')}</span>
        </div>
        <p>${htmlEscape(item.reviewTargetLabel || item.reviewTarget)}</p>
        <div class="feedback-detail">
          <div><span>任务代号/名称</span><b>${htmlEscape(value.taskReference || '-')}</b></div>
          <div><span>任务范围</span><b>${htmlEscape(value.taskScope || '-')}</b></div>
          <div><span>传统预计/生成模型实际</span><b>${htmlEscape(value.traditionalEstimatedMinutes)} / ${htmlEscape(actualMinutes)} 分钟；${savingsText}</b></div>
          <div><span>直接复用比例</span><b>${htmlEscape(value.directReusePercent)}%</b></div>
          <div><span>人工修改项</span><b>${htmlEscape(manualItems.join('；') || '无')}</b></div>
          <div><span>成果状态</span><b>模型：${htmlEscape(deliverables.rebuildableModel || '-')}；工程图：${htmlEscape(deliverables.engineeringDrawing || '-')}；BOM：${htmlEscape(deliverables.bom || '-')}</b></div>
          <div><span>主要问题</span><b>${htmlEscape(value.primaryProblemCategoryLabel || EFFICIENCY_PROBLEM_CATEGORY_LABELS[value.primaryProblemCategory] || '-')}</b></div>
          <div><span>补充说明</span><b>${htmlEscape(value.problemNotes || '无')}</b></div>
        </div>
        ${item.attachmentCount ? `<div class="attachment-links">${Array.from({ length: item.attachmentCount }, (_, index) => `<a href="/feedback/${encodeURIComponent(item.id)}/attachment/${index + 1}" target="_blank" rel="noopener">查看附件 ${index + 1}</a>`).join('')}</div>` : ''}
        <p>${new Date(item.submittedAt).toLocaleString('zh-CN', { hour12: false })} · 用于记录结构工程辅助效果。</p>
      </div>
    `
  }).join('')
}

function clarificationCards() {
  const questions = readClarificationQuestions()
  if (!questions.length) return '<p class="muted">当前没有待确认问题。</p>'
  const roundFeedback = currentRoundFeedback()
  return questions.map((question) => {
    const replies = roundFeedback.filter((item) => item.clarificationId === question.id)
    const sourceFeedbackIds = Array.isArray(question.source_feedback_ids) ? question.source_feedback_ids : []
    const facts = Array.isArray(question.verified_facts) ? question.verified_facts : []
    const options = Array.isArray(question.options) ? question.options : []
    const images = Array.isArray(question.images) ? question.images : []
    return `
      <article class="clarification-card" id="clarification-${htmlEscape(question.id)}">
        <div class="clarification-head">
          <div>
            <p class="clarification-kicker">待工程师确认 · ${htmlEscape(question.id)}</p>
            <h3>${htmlEscape(question.title)}</h3>
          </div>
          <span class="chip ${replies.length ? 'good' : 'warn'}">${replies.length ? `已收到 ${replies.length} 条答复` : '等待答复'}</span>
        </div>
        <p class="clarification-source">关联原反馈：${sourceFeedbackIds.map((id) => `<code>${htmlEscape(id)}</code>`).join('、')}</p>
        <div class="clarification-facts">
          <strong>已经核对的事实</strong>
          <ul>${facts.map((fact) => `<li>${htmlEscape(fact)}</li>`).join('')}</ul>
        </div>
        <p class="clarification-prompt"><strong>请确认：</strong>${htmlEscape(question.prompt)}</p>
        <div class="clarification-images">
          ${images.map((image, index) => `
            <figure>
              <a href="/clarification/${encodeURIComponent(question.id)}/image/${index + 1}" target="_blank" rel="noopener">
                <img src="/clarification/${encodeURIComponent(question.id)}/image/${index + 1}" alt="${htmlEscape(image.caption || question.title)}" loading="lazy" />
              </a>
              <figcaption>${htmlEscape(image.caption || `问题截图 ${index + 1}`)}</figcaption>
            </figure>
          `).join('')}
        </div>
        <form class="clarification-response"
          data-clarification-id="${htmlEscape(question.id)}"
          data-question-title="${htmlEscape(question.title)}"
          data-component-name="${htmlEscape(question.component_name || question.title)}"
          data-question-prompt="${htmlEscape(question.prompt)}"
          data-source-feedback-ids="${htmlEscape(sourceFeedbackIds.join(', '))}"
          data-image-count="${images.length}">
          <fieldset>
            <legend>选择方案</legend>
            <div class="clarification-options">
              ${options.map((option) => `
                <label>
                  <input type="radio" name="clarificationOption" value="${htmlEscape(option.value)}" data-label="${htmlEscape(option.label)}" required />
                  <span>${htmlEscape(option.label)}</span>
                </label>
              `).join('')}
            </div>
          </fieldset>
          <label>补充位置和最终尺寸
            <textarea name="clarificationDetail" required placeholder="请写清具体零件、位置、最终尺寸或间隙；若选 A/B，也请写“确认按此方案执行”。"></textarea>
          </label>
          <div class="clarification-submit-row">
            <button type="submit">提交这条确认答复</button>
            <span class="clarification-status muted">原截图会自动随答复保存，不需要重新上传。</span>
          </div>
        </form>
      </article>
    `
  }).join('')
}

function generationRows(rows) {
  if (!rows.length) return '<div class="task-empty"><strong>开始你的第一组结构规格</strong><span>定义外形与门数，核对布局后建立模型任务。</span><a class="button secondary" href="#new-model">定义第一组规格 →</a></div>'
  return rows.map((item) => {
    const status = generationTaskStatusInfo(item)
    const taskName = item.customerRequirementReference || (item.taskMode === 'single_model' ? '单个模型' : '完整装配体')
    const dimensions = `${item.cabinetWidth || '-'}W × ${item.cabinetHeight || '-'}H × ${item.cabinetDepth || '-'}D`
    const doorText = `${item.doorCount || '-'} 门｜${item.previewDimensionsValidated === false ? '布局预估' : '已记录门板'} ${item.doorWidth || '-'}W × ${item.doorHeight || '-'}H`
    const searchText = `${taskName} ${item.id} ${dimensions} ${status.label}`.toLowerCase()
    return `
      <article class="task-row task-${htmlEscape(status.key)}" data-task-row data-task-state="${htmlEscape(status.key)}" data-task-search="${htmlEscape(searchText)}">
        <div class="task-state-rail" aria-label="任务状态 ${htmlEscape(status.label)}"><span>${htmlEscape(status.label)}</span></div>
        <div class="task-row-main">
          <div class="task-row-title">
            <strong>${htmlEscape(taskName)}</strong>
            <span class="task-status ${htmlEscape(status.key)}">${htmlEscape(status.label)}</span>
            <code>${htmlEscape(item.id)}</code>
          </div>
          <div class="task-dimensions"><span>${htmlEscape(dimensions)}</span><span>${htmlEscape(doorText)}</span></div>
          <p>${htmlEscape(generationStatusText(item))}</p>
          <div class="task-row-actions">
            ${generationActionHtml(item)}
            <button class="secondary task-feedback-button" type="button" data-task-feedback-id="${htmlEscape(item.id)}">反馈修订</button>
            ${generationDeleteActionHtml(item)}
          </div>
        </div>
        <time datetime="${htmlEscape(item.createdAt || '')}">${item.createdAt ? htmlEscape(new Date(item.createdAt).toLocaleString('zh-CN', { hour12: false })) : '刚刚提交'}</time>
      </article>
    `
  }).join('')
}

async function renderPage(request) {
  const username = currentUser(request)
  const assetCards = renderAssetCards()
  const myGenerationRequestItems = await currentGenerationRequests(username)
  const myGenerationRequests = generationRows(myGenerationRequestItems)
  const eligibleGeneratedModels = myGenerationRequestItems
    .filter((item) => item.username === username)
    .filter(isEngineeringContinuationEligible)
  const generatedModelOptionHtml = eligibleGeneratedModels.length
    ? eligibleGeneratedModels.map((item) => `<option value="${htmlEscape(item.id)}">${htmlEscape(generationRequestTargetLabel(item))}</option>`).join('')
    : '<option value="" disabled selected>请先获取 V35 / V36 / V37 已验证精确规格模型</option>'
  const issueGenerationOptionHtml = myGenerationRequestItems
    .filter((item) => item.username === username)
    .map((item) => `<option value="${htmlEscape(item.id)}">${htmlEscape(generationRequestTargetLabel(item))}｜${htmlEscape(generationStatusText(item))}</option>`)
    .join('')
  const teamRecords = currentRoundFeedback()
  const efficiencyRecords = teamRecords.filter((item) => item.recordType === 'efficiency_validation')
  const structureFeedback = teamRecords.filter((item) => (item.recordType || 'structure_issue') === 'structure_issue')
  const efficiencyRows = efficiencyValidationRows(efficiencyRecords)
  const teamFeedbackRows = feedbackRows(structureFeedback)
  const historicalRecords = historicalRoundFeedback()
  const historicalStructureRecords = historicalRecords.filter((item) => (item.recordType || 'structure_issue') === 'structure_issue')
  const historicalEfficiencyRecords = historicalRecords.filter((item) => item.recordType === 'efficiency_validation')
  const historicalFeedbackRows = feedbackRows(historicalStructureRecords)
  const historicalEfficiencyRows = efficiencyValidationRows(historicalEfficiencyRecords)
  const clarificationCardsHtml = clarificationCards()
  const clarificationCount = readClarificationQuestions().length
  return renderShell(`
    <div class="shell">
      <aside class="portal-sidebar" aria-label="门户导航">
        <div class="workspace-brand"><div class="portal-logo"><img src="/brand/winnsen-logo.jpg" alt="Winnsen" /></div><div class="workspace-brand-copy"><strong>结构设计工作台</strong><span>16029 / Structure workspace</span></div></div>
        <nav class="portal-nav" aria-label="模型助手功能">
          <a href="#new-model" data-workspace-nav="new-model" class="active"><svg class="portal-nav-icon" viewBox="0 0 18 18" aria-hidden="true"><path d="M3 4h12v12H3zM7 4v12M3 9h12" fill="none" stroke="currentColor" stroke-width="1.4" stroke-linecap="round" stroke-linejoin="round" /></svg><span class="portal-nav-copy"><strong>定义规格</strong></span></a>
          <a href="#my-models" data-workspace-nav="my-models"><svg class="portal-nav-icon" viewBox="0 0 18 18" aria-hidden="true"><path d="M3 5h12v10H3zM6 2h6M6 8h6M6 12h4" fill="none" stroke="currentColor" stroke-width="1.4" stroke-linecap="round" stroke-linejoin="round" /></svg><span class="portal-nav-copy"><strong>任务工作台</strong></span></a>
          <a href="#model-feedback" data-workspace-nav="model-feedback"><svg class="portal-nav-icon" viewBox="0 0 18 18" aria-hidden="true"><path d="M3 3h12v10H8l-4 3v-3H3zM6 6h6M6 9h4" fill="none" stroke="currentColor" stroke-width="1.4" stroke-linecap="round" stroke-linejoin="round" /></svg><span class="portal-nav-copy"><strong>反馈修订</strong></span></a>
          <a href="#archive" data-workspace-nav="archive"><svg class="portal-nav-icon" viewBox="0 0 18 18" aria-hidden="true"><path d="M2 5h5l2 2h7v8H2zM2 5V3h5l2 2" fill="none" stroke="currentColor" stroke-width="1.4" stroke-linecap="round" stroke-linejoin="round" /></svg><span class="portal-nav-copy"><strong>资料档案</strong></span></a>
        </nav>
        <div class="account-card">
          <div class="account-avatar" aria-hidden="true">${htmlEscape(username.slice(0, 2).toUpperCase())}</div>
          <div class="account-copy"><strong>${htmlEscape(username)}</strong></div>
          <a href="/logout">退出</a>
        </div>
      </aside>
      <main class="workspace-main">
        <div class="workspace-topline"><span><i aria-hidden="true"></i><b>16029</b> / 两列柜体产品族</span><span>原生建模 · 逐单校核 · 工程深化</span></div>
        <div class="workspace-view active" data-workspace-view="new-model">
        <section id="new-model" class="panel review-overview">
          <div class="workspace-title-row">
            <div class="simple-intro">
              <p class="eyebrow">新建模型 / Specification</p>
              <h1>从规格，到模型。</h1>
              <p class="muted">从外形尺寸与门板布局出发，建立可追溯的原生建模任务。通过工程校核后，下载基础模型继续深化。</p>
            </div>
            <div class="workspace-stamp" aria-label="当前页：规格定义"><span>SHEET</span><strong>01 / 04</strong><span class="stamp-title">规格定义 / 布局估算</span></div>
          </div>
          <ol class="workflow-rail" aria-label="参数化任务流程">
            <li class="active"><b>01</b><span>定义规格</span><small>外形与门数</small></li>
            <li><b>02</b><span>核对布局</span><small>预览为估算</small></li>
            <li><b>03</b><span>生成校核</span><small>原生模型检查</small></li>
            <li><b>04</b><span>下载深化</span><small>通过后开放</small></li>
            <li><b>05</b><span>反馈修订</span><small>绑定任务记录</small></li>
          </ol>
          <div class="engineering-boundary"><strong>适用范围</strong><span>宽 700–1200 mm · 高 1700–2200 mm · 深 250–650 mm · 两列独立门序（共 2–34 门）。每扇门高至少 100 mm，每列须闭合；布局预览不替代原生校核。</span></div>
        </section>

        <section id="generate" class="panel generation-primary">
          <div class="section-heading">
            <div>
              <p class="eyebrow">INPUT / DIMENSIONS &amp; LAYOUT</p>
              <h2>规格输入与布局核对</h2>
              <p class="muted">“门宽”指单扇门板成品外宽，不是净开口宽。预览为布局估算；原生模型在生成阶段重新计算和校核。</p>
            </div>
            <span class="chip engineering-chip">参数 → 布局 → 确认</span>
          </div>
          <div class="auxiliary-body">
          <div class="studio-grid">
            <form id="generationForm" class="generator-form">
              <h2 class="generator-title">规格与门板布局</h2>
              <input type="hidden" name="customerParameterFlow" value="v1" />
              <input type="hidden" name="taskMode" id="taskMode" value="full_assembly" />
              <input type="hidden" name="prompt" id="doorPrompt" value="" />
              <input type="hidden" name="cabinetWidth" id="cabinetWidth" value="760" />
              <input type="hidden" name="columns" id="columns" value="2" />
              <input type="hidden" name="doorWidth" id="doorWidth" value="317" />
              <input type="hidden" name="doorHeight" id="doorHeight" value="908" />
              <input type="hidden" name="rowSequence" id="rowSequence" value="L66-R66" />
              <input type="hidden" name="doorType" id="doorType" value="ordinary_door_panel" />
              <input type="hidden" name="lockType" id="lockType" value="机械锁舌；排除电控锁实体" />
              <input type="hidden" name="hingeType" id="hingeType" value="暗铰链" />
              <input type="hidden" name="latchType" id="latchType" value="锁舌接口" />
              <input type="hidden" name="reinforcement" id="reinforcement" value="按模板规则生成" />
              <input type="hidden" name="openings" id="openings" value="一门一锁孔；保留已验证定位基准" />

              <div class="field-grid customer-parameter-grid">
                <label class="wide"><span class="field-label">需求代号 / 任务名称<small>REFERENCE</small></span>
                  <input name="customerRequirementReference" required maxlength="200" placeholder="例如：项目A-两列柜-01；不要填写完整客户名称或图号" />
                </label>
                <label class="wide"><span class="field-label">宽度口径<small>WIDTH BASIS</small></span>
                  <select name="widthInputMode" id="widthInputMode">
                    <option value="cabinet_outer_width">柜体成品外宽</option>
                    <option value="installed_door_panel_width">单扇门板成品外宽</option>
                  </select>
                  <span class="width-basis-note">单扇门宽指门板成品外宽，不是净开口宽。</span>
                </label>
                <label><span class="field-label">目标宽度<small>W / mm</small></span>
                  <input name="requestedWidthMm" id="requestedWidthMm" type="number" min="120" max="2000" step="0.1" value="760" required />
                </label>
                <label><span class="field-label">总门数<small>自动汇总</small></span>
                  <input name="doorCount" id="doorCount" type="number" min="2" max="34" step="1" value="6" readonly />
                </label>
                <label><span class="field-label">柜体高度<small>H / mm</small></span>
                  <input name="cabinetHeight" id="cabinetHeight" type="number" min="1700" max="2200" step="0.1" value="1917" required />
                </label>
                <label><span class="field-label">柜体深度<small>D / mm</small></span>
                  <input name="cabinetDepth" id="cabinetDepth" type="number" min="250" max="800" step="0.1" value="550" required />
                </label>
              </div>
              <section class="door-layout-editor" aria-label="左右列独立门板编辑">
                <div class="door-layout-heading"><h3>左右列布局</h3><span>L / R · mm</span></div>
                <p class="door-layout-note">不用手算：指定已知门高，自动门补齐剩余；或输入 3 : 2 : 1 等分格比例，系统换算门高并扣除 7 mm 门缝。01 为最下门。</p>
                <div class="door-layout-presets" role="group" aria-label="常用门板布局">
                  <button class="secondary" type="button" data-layout-preset="mixed-six">六门混合 L642 / R246</button>
                  <button class="secondary" type="button" data-layout-preset="equal-four">四门等高</button>
                </div>
                <div class="door-columns">
                  <fieldset class="door-column" data-door-column="L">
                    <legend>左列 / L</legend>
                    <label class="door-column-count" for="leftDoorCount"><span>左列门数</span><input id="leftDoorCount" type="number" min="1" max="24" step="1" value="3" required aria-describedby="leftDoorSummary" /></label>
                    <div class="door-allocation-switch" role="group" aria-label="左列门高设置方式"><button type="button" data-column-mode="L" data-allocation-mode="height" aria-pressed="true">按高度</button><button type="button" data-column-mode="L" data-allocation-mode="ratio" aria-pressed="false">按比例</button></div>
                    <ol id="leftDoorHeights" class="door-height-list" aria-label="左列门高，从下向上"></ol>
                    <p id="leftDoorSummary" class="door-column-summary" role="status"></p>
                    <p id="leftDoorMode" class="door-column-mode"></p>
                    <div class="door-column-actions"><button class="secondary" type="button" data-column-equalize="L">均分本列</button><button class="secondary" type="button" data-column-fill-top="L">补齐最上门</button></div>
                  </fieldset>
                  <fieldset class="door-column" data-door-column="R">
                    <legend>右列 / R</legend>
                    <label class="door-column-count" for="rightDoorCount"><span>右列门数</span><input id="rightDoorCount" type="number" min="1" max="24" step="1" value="3" required aria-describedby="rightDoorSummary" /></label>
                    <div class="door-allocation-switch" role="group" aria-label="右列门高设置方式"><button type="button" data-column-mode="R" data-allocation-mode="height" aria-pressed="true">按高度</button><button type="button" data-column-mode="R" data-allocation-mode="ratio" aria-pressed="false">按比例</button></div>
                    <ol id="rightDoorHeights" class="door-height-list" aria-label="右列门高，从下向上"></ol>
                    <p id="rightDoorSummary" class="door-column-summary" role="status"></p>
                    <p id="rightDoorMode" class="door-column-mode"></p>
                    <div class="door-column-actions"><button class="secondary" type="button" data-column-equalize="R">均分本列</button><button class="secondary" type="button" data-column-fill-top="R">补齐最上门</button></div>
                  </fieldset>
                </div>
                <p id="doorLayoutStatus" class="door-layout-status" role="status" aria-live="polite"></p>
              </section>
              <div id="generationCapabilityStatus" class="feedback-boundary">正在核对当前可生成范围…</div>
              <div id="generatedPrompt" class="generated-prompt" aria-label="本次参数摘要"></div>
              <p class="estimate-note"><strong>布局说明：</strong>此处门板尺寸和预览是参数估算，未通过原生模型检查前不可作为出图或下载依据。</p>
              <details class="advanced-fields">
                <summary>可选：材料和板厚备注</summary>
                <div class="field-grid two">
                  <label>材料<input name="material" id="material" value="待工程深化确认" /></label>
                  <label>板厚<input name="thickness" id="thickness" value="待工程深化确认" /></label>
                </div>
                <p class="muted">这些字段作为深化备注保存，不会在没有工艺规则证据时自动改写钣金厚度。</p>
              </details>
              <button id="generationSubmitButton" type="submit">核对参数并进入确认</button>
              <section id="generationConfirm" class="generation-confirm" hidden aria-live="polite">
                <div class="generation-confirm-head">
                  <div><span>02 / LAYOUT REVIEW</span><strong>确认本次任务参数</strong></div>
                  <span id="generationConfirmState" class="task-status checking">待确认</span>
                </div>
                <dl id="generationConfirmSummary" class="generation-confirm-summary"></dl>
                <p id="generationConfirmBoundary" class="generation-confirm-boundary"></p>
                <div class="generation-confirm-actions">
                  <button id="returnToSpecButton" class="secondary" type="button">返回编辑</button>
                  <button id="confirmGenerationSubmit" type="button">确认提交生成</button>
                </div>
              </section>
              <div class="progress-wrap" aria-live="polite">
                <div class="progress-track"><span id="generationProgressBar"></span></div>
                <div id="generationProgressLabel" class="progress-label">等待提交</div>
              </div>
              <div id="generationStatus"></div>
            </form>

            <aside class="layout-review-panel" aria-label="布局核对预览">
              <div class="preview-panel">
                <div class="preview-head">
                  <div>
                    <span>模型预览 / Layout preview</span>
                    <strong>查看你的柜体布局</strong>
                  </div>
                  <span class="chip engineering-chip">未验收预览</span>
                </div>
                <div class="viewport-toolbar">
                  <div class="view-button-row" aria-label="3D viewport view controls">
                    <button class="button secondary view-active" type="button" data-view="iso">ISO</button>
                    <button class="button secondary" type="button" data-view="front">前视</button>
                    <button class="button secondary" type="button" data-view="right">右视</button>
                    <button class="button secondary" type="button" data-view="top">俯视</button>
                  </div>
                  <span class="preview-legend">视图 / 旋转 / 缩放</span>
                </div>
                <div class="preview-canvas-wrap">
                  <div class="drawing-corner" aria-hidden="true"><b>16029 / LAYOUT</b><span>PARAMETRIC STUDY</span></div>
                  <span class="drawing-scale">布局估算 · 单位 mm</span>
                  <canvas id="cabinetPreview" width="760" height="560" role="img" aria-label="当前输入参数对应的柜体布局估算，可使用上方按钮切换视角"></canvas>
                </div>
                <div class="viewport-hud" aria-label="布局估算参数">
                    <div><span>外形估算</span><strong id="hudCabinetSize">-</strong></div>
                    <div><span>左列最下门估算</span><strong id="hudDoorSize">-</strong></div>
                    <div><span>布局</span><strong id="hudDoorLayout">-</strong></div>
                    <div><span>五金</span><strong id="hudHardware">-</strong></div>
                </div>
                <div class="preview-controls">
                  <label for="previewRotation">旋转</label>
                  <input id="previewRotation" type="range" min="-90" max="90" value="-28" />
                  <span id="rotationLabel" class="muted">-28°</span>
                  <label for="previewZoom">缩放</label>
                  <input id="previewZoom" type="range" min="70" max="150" value="100" />
                  <span id="zoomLabel" class="muted">100%</span>
                </div>
                <p class="preview-note">拖动旋转，滚轮缩放；前视、右视、俯视为正投影。门序从下向上，外观与材质为示意；布局估算须经原生模型构建与检查。</p>
              </div>
            </aside>
          </div>
          </div>
        </section>
        </div>

        <div class="workspace-view" data-workspace-view="my-models">
          <section id="my-models" class="panel">
            <div class="workspace-view-heading">
              <div class="workspace-heading-copy">
              <p class="eyebrow">我的模型 / Build &amp; validate</p>
              <h1>任务工作台</h1>
              <p class="muted">跟踪每一组规格的构建与校核。通过原生交付检查的任务，才会开放模型下载。</p>
              </div>
              <div class="workspace-stamp" aria-label="当前页：任务工作台"><span>SHEET</span><strong>02 / 04</strong><span class="stamp-title">任务状态 / 原生校核</span></div>
            </div>
            <div class="task-toolbar" aria-label="任务筛选">
              <label class="task-search"><span>检索</span><input id="taskFilterInput" type="search" placeholder="任务代号、尺寸或任务编号" autocomplete="off" /></label>
              <div class="task-filter-buttons" role="group" aria-label="按状态筛选">
                <button class="secondary active" type="button" data-task-filter="all">全部</button>
                <button class="secondary" type="button" data-task-filter="queued">排队</button>
                <button class="secondary" type="button" data-task-filter="building">构建</button>
                <button class="secondary" type="button" data-task-filter="checking">检查</button>
                <button class="secondary" type="button" data-task-filter="ready">可下载</button>
                <button class="secondary" type="button" data-task-filter="failed">待处理</button>
              </div>
            </div>
            <div class="task-state-key" aria-label="状态说明"><span><i class="queued"></i>排队：等待工作器</span><span><i class="building"></i>构建：原生模型中</span><span><i class="checking"></i>检查：交付校核中</span><span><i class="ready"></i>可下载：已通过</span></div>
            <p id="taskRefreshStatus" class="muted" role="status">任务状态每 5 秒自动更新</p>
            <div class="request-list" id="generationRequestList">${myGenerationRequests}</div>
            <p id="generationRequestEmptyFilter" class="task-empty" hidden><strong>没有匹配的任务</strong><span>调整关键词或状态筛选后再查看。</span></p>
          </section>
        </div>

        <div class="workspace-view" data-workspace-view="model-feedback">
          <section id="model-feedback" class="panel">
            <div class="workspace-view-heading">
              <div class="workspace-heading-copy">
              <p class="eyebrow">工程协作 / Feedback &amp; revision</p>
              <h1>反馈修订</h1>
              <p class="muted">将问题和使用效果关联到具体任务，保留可复核的结构修订输入。</p>
              <div class="feedback-tabs" role="tablist" aria-label="反馈类型">
                <button id="feedback-tab-issue" type="button" class="feedback-tab active" data-feedback-tab="issue" role="tab" aria-selected="true" aria-controls="feedback-pane-issue">模型有问题</button>
                <button id="feedback-tab-effect" type="button" class="feedback-tab" data-feedback-tab="effect" role="tab" aria-selected="false" aria-controls="feedback-pane-effect" tabindex="-1">使用效果</button>
              </div>
              </div>
              <div class="workspace-stamp" aria-label="当前页：反馈修订"><span>SHEET</span><strong>03 / 04</strong><span class="stamp-title">问题证据 / 修订输入</span></div>
            </div>
          </section>

          ${clarificationCount ? `
          <section id="clarifications" class="panel" aria-label="待工程师确认问题">
            <h2>待工程师确认（${clarificationCount} 项）</h2>
            <div class="clarification-list">${clarificationCardsHtml}</div>
          </section>` : ''}

        <div id="feedback-pane-effect" class="feedback-pane" data-feedback-pane="effect" role="tabpanel" aria-labelledby="feedback-tab-effect">
        <section id="efficiency-trial" class="panel" aria-label="生成模型应用效果记录">
          <p class="eyebrow">MEASUREMENT / APPLICATION RESULTS</p>
          <h2>记录使用效果</h2>
          <p class="muted feedback-simple-intro">完成一次真实任务后，记录传统建模耗时、使用本模型的耗时和直接复用比例。</p>
          <form id="efficiencyForm" class="feedback-form">
            <input type="hidden" name="reviewerName" value="${htmlEscape(username)}" />
            <input type="hidden" name="reviewTarget" value="${htmlEscape(CURRENT_REVIEW_ASSET_ID)}" />
            <div class="feedback-options-grid efficiency-grid">
              <div class="form-subheading wide"><span>01</span>任务与范围</div>
              <label class="wide">关联已验证精确规格模型（V35 / V36 / V37）
                <select name="generationRequestId" required>${generatedModelOptionHtml}</select>
              </label>
              <label>任务类型
                <select name="taskKind" required>
                  ${Object.entries(TASK_KIND_LABELS).map(([value, label], index) => `<option value="${value}"${index === 0 ? ' selected' : ''}>${htmlEscape(label)}</option>`).join('')}
                </select>
              </label>
              <label>脱敏任务代号或名称<input name="taskReference" required maxlength="200" placeholder="例如：典型变更A / 左列中门高度调整" /></label>
              <label class="wide">任务范围<textarea name="taskScope" required maxlength="2000" placeholder="写清具体改动，以及下面两项耗时共同包含哪些成果；不要填写客户名称、完整图号或供应商敏感信息。"></textarea></label>
              <div class="form-subheading wide"><span>02</span>耗时与复用</div>
              <label>传统方式预计耗时（分钟）<input name="traditionalEstimatedMinutes" type="number" required min="1" max="43200" step="1" /></label>
              <label>使用生成模型实际耗时（分钟）<input name="generatedModelActualMinutes" type="number" required min="1" max="43200" step="1" /></label>
              <label>直接复用比例（%）<input name="directReusePercent" type="number" required min="0" max="100" step="1" /></label>
              <div class="form-subheading wide"><span>03</span>成果与问题记录</div>
              <label>可重建SolidWorks模型
                <select name="rebuildableModel" required>
                  <option value="completed">已完成</option>
                  <option value="not_completed">未完成</option>
                </select>
              </label>
              <label>工程图
                <select name="engineeringDrawing" required>
                  ${Object.entries(DELIVERABLE_STATUS_LABELS).map(([value, label]) => `<option value="${value}">${htmlEscape(label)}</option>`).join('')}
                </select>
              </label>
              <label>BOM
                <select name="bom" required>
                  ${Object.entries(DELIVERABLE_STATUS_LABELS).map(([value, label]) => `<option value="${value}">${htmlEscape(label)}</option>`).join('')}
                </select>
              </label>
              <label>主要问题或耗时来源
                <select name="primaryProblemCategory" required>
                  ${Object.entries(EFFICIENCY_PROBLEM_CATEGORY_LABELS).map(([value, label]) => `<option value="${value}">${htmlEscape(label)}</option>`).join('')}
                </select>
              </label>
              <label class="wide">必须人工修改的项目（复用率低于100%时必填）<textarea name="manualModificationItems" maxlength="9300" placeholder="每行一项，例如：\n调整门板高度\n修改隔板位置\n更新工程图尺寸"></textarea></label>
              <label class="wide">问题补充说明<textarea name="problemNotes" maxlength="2000" placeholder="分类选择“其他”时必填；没有阻塞问题可留空。"></textarea></label>
              <label class="wide">脱敏结果截图（可选）
                <input name="attachments" type="file" accept=".png,.jpg,.jpeg,.webp,.pdf" multiple />
                <span class="feedback-field-hint">如需佐证，只上传必要模型区域；不要上传完整客户图纸、完整BOM、客户名称或供应商信息。</span>
              </label>
            </div>
            <div class="feedback-boundary">基础模型用于减少重复建模；结构工程师在此基础上继续完成材料、工艺、图纸、BOM和项目细节。</div>
            <div class="feedback-submit-row">
              <button id="efficiencySubmitButton" type="submit"${eligibleGeneratedModels.length ? '' : ' disabled'}>提交应用效果记录</button>
              <p class="muted">${eligibleGeneratedModels.length ? '成果状态完整时系统自动计算节省时间和比例。' : '本页目前仅支持 V35 / V36 / V37 精确规格模型的使用效果记录；参数化任务可在“模型有问题”中提交结构反馈。'}</p>
            </div>
            <div id="efficiencyStatus"></div>
          </form>
          <div class="feedback-history-block">
            <h3>本轮效率记录（${efficiencyRecords.length}）</h3>
            <div id="efficiencyList">${efficiencyRows}</div>
          </div>
        </section>
        </div>

        <div id="feedback-pane-issue" class="feedback-pane active" data-feedback-pane="issue" role="tabpanel" aria-labelledby="feedback-tab-issue">
        <section id="feedback" class="panel" aria-label="上传结构问题反馈">
          <p class="eyebrow">REVISION / ISSUE REPORT</p>
          <h2>提交模型问题</h2>
          <p class="muted feedback-simple-intro">选择对应任务，说明问题位置并上传一张带红框或箭头的截图。</p>
          <p id="feedbackTaskContext" class="feedback-task-context" hidden></p>
          <div class="feedback-layout">
          <form id="feedbackForm" class="feedback-form feedback-simple">
            <input type="hidden" name="reviewerName" value="${htmlEscape(username)}" />
            <input type="hidden" name="discipline" value="结构审核" />
            <div class="feedback-core">
              <label><span><span class="feedback-field-number">01</span>关联模型</span>
                <select name="generationRequestId" id="feedbackGenerationRequestId">
                  <option value="">V37当前工程辅助模型（不是某次生成任务）</option>
                  ${issueGenerationOptionHtml}
                </select>
                <span class="feedback-field-hint">生成结果有问题时必须选对应任务，反馈会保存一份脱敏参数快照。</span>
              </label>
              <label><span><span class="feedback-field-number">02</span>问题位置</span>
                <input name="componentName" required placeholder="例如：左列第 6 门 / 锁孔附近" />
                <span class="feedback-field-hint">写到能让工程师在模型里找到即可。</span>
              </label>
              <label><span><span class="feedback-field-number">03</span>问题描述</span>
                <textarea name="currentProblem" required placeholder="例如：锁舌中心与锁孔中心没有对齐，关门时会碰撞。"></textarea>
                <span class="feedback-field-hint">说明现在看到了什么；一次只提交一个问题。</span>
              </label>
              <label><span><span class="feedback-field-number">04</span>问题截图或 PDF</span>
                <input name="attachments" type="file" accept=".png,.jpg,.jpeg,.webp,.pdf" multiple required />
                <span class="feedback-field-hint">请尽量画红框或箭头。最多 ${MAX_ATTACHMENT_COUNT} 个，合计不超过 ${formatBytes(MAX_ATTACHMENT_TOTAL_BYTES)}。</span>
              </label>
            </div>
            <details class="feedback-options">
              <summary>补充信息（可选）</summary>
              <div class="feedback-options-grid">
                <label>更具体的位置<input name="modelLocation" placeholder="例如：左列 6/12 门后，锁侧中部" /></label>
                <label>审核对象
                  <select name="reviewTarget">
                    ${orderedAssets().map((asset) => `<option value="${asset.id}"${asset.id === CURRENT_REVIEW_ASSET_ID ? ' selected' : ''}>${htmlEscape(asset.title)}</option>`).join('')}
                  </select>
                </label>
                <label>问题分类
                  <select name="issueCategory">
                    ${Object.entries(ISSUE_CATEGORY_LABELS).map(([value, label]) => `<option value="${value}"${value === 'other_structure_issue' ? ' selected' : ''}>${htmlEscape(label)}</option>`).join('')}
                  </select>
                </label>
                <label>严重度
                  <select name="severity">
                    ${Object.entries(SEVERITY_LABELS).map(([value, label]) => `<option value="${value}"${value === 'P1' ? ' selected' : ''}>${htmlEscape(label)}</option>`).join('')}
                  </select>
                </label>
                <label>处理结论
                  <select name="decision">
                    <option value="needs_changes" selected>需修改</option>
                    <option value="blocked">阻塞</option>
                    <option value="cannot_judge">无法判断</option>
                    <option value="pass">未发现问题（仅反馈）</option>
                  </select>
                </label>
                <label>关键尺寸/公差<input name="keyDimensionTolerance" placeholder="没有确认尺寸可留空" /></label>
                <label class="wide">期望处理<textarea name="expectedResult" placeholder="如有明确处理方式可填写；不填则按问题描述和截图处理。"></textarea></label>
                <label class="wide">验收说明<textarea name="acceptanceCriteria" placeholder="如有明确验收条件可填写；不填则由提交人复核确认。"></textarea></label>
                <label class="wide">参考模型或图纸<input name="referenceBasis" placeholder="正式图号、参考模型或现场样件" /></label>
              </div>
            </details>
            <div class="feedback-boundary" data-storage-path="参数化模型下载及反馈/工程反馈">反馈会保存到共享目录，作为后续原生模型修正与结构深化的输入。</div>
            <div class="feedback-submit-row">
              <button id="feedbackSubmitButton" type="submit">上传问题</button>
              <p class="muted">成功后会生成 V37-Q 编号。</p>
            </div>
            <div id="feedbackStatus"></div>
          </form>
          <aside class="feedback-guide" aria-label="反馈截图标注指南">
            <p class="sheet-label">EVIDENCE / ANNOTATION</p>
            <h3>把问题标在结构上。</h3>
            <svg class="evidence-sketch" viewBox="0 0 220 230" role="img" aria-label="示意：在门板锁孔附近用橙色方框与箭头标注问题">
              <g fill="none" stroke="currentColor" stroke-width="1">
                <path d="M36 24H155V190H36ZM43 31H148V183H43ZM136 93V111M35 199H156M28 24V190" />
                <path stroke-dasharray="3 4" d="M25 102H168M96 14V203" opacity=".5" />
                <circle cx="138" cy="102" r="7" />
              </g>
              <g fill="none" stroke="#c24e27" stroke-width="1.5"><path d="M122 84H156V121H122ZM156 84L181 55H203M175 57L181 55L179 62" /></g>
              <text x="183" y="47" fill="#c24e27" font-size="10" font-family="Consolas,monospace">A</text>
              <text x="36" y="219" fill="currentColor" font-size="9" font-family="Consolas,monospace">DETAIL / ISSUE LOCATION</text>
            </svg>
            <p>截取必要的模型区域，用红框或箭头标出问题。描述零件、位置和实际现象。</p>
            <p>一个问题对应一条记录，便于修订后逐项复核。</p>
            <p class="sheet-label">示意标注 / 非尺寸依据</p>
          </aside>
          </div>
        </section>
        </div>
        </div>

        <div class="workspace-view" data-workspace-view="archive">
        <section id="archive" class="panel">
          <div class="workspace-view-heading">
            <div class="workspace-heading-copy">
            <p class="eyebrow">04 / DOCUMENT ARCHIVE</p>
            <h1>资料档案</h1>
            <p class="muted">按版本追溯模型，按记录复核修订。已验证文件、技术说明和往期反馈集中保存在这里。</p>
            </div>
            <div class="workspace-stamp" aria-label="当前页：资料档案"><span>SHEET</span><strong>04 / 04</strong><span class="stamp-title">文件版本 / 历史记录</span></div>
          </div>
          <div class="archive-index" aria-label="资料记录统计">
            <div><span>档案文件</span><strong>${Math.max(0, orderedAssets().length - 1)} <small>份</small></strong></div>
            <div><span>本轮结构反馈</span><strong>${structureFeedback.length} <small>条</small></strong></div>
            <div><span>历史反馈</span><strong>${historicalRecords.length} <small>条</small></strong></div>
          </div>
          <details class="advanced-fields">
            <summary>技术验证与使用边界</summary>
            <div style="padding:0 12px 12px">
              <p class="muted">V37的760W×1917H×550D、2列6门、L642-R246、单门317×908mm已通过原生SolidWorks、一门一锁孔、包内引用和关闭重开门禁；打开后必须重建，且保留唯一已知Reference code 51，因此warningFree=false。V36的740W四门L66-R66和V35的740W六门L642-R246继续作为历史已验证精确配方提供。其他组合会建立原生新任务，在结构辅助检查完成前不提供下载。</p>
            </div>
          </details>
        </section>

        <details id="downloads" class="panel auxiliary-panel advanced-panel" aria-label="模型与审核资料" open>
          <summary class="auxiliary-summary">
            <div class="auxiliary-summary-copy">
              <h2>已验证模型文件</h2>
              <p class="muted">当前与历史精确规格均保留，可按任务需要下载。</p>
            </div>
          </summary>
          <div class="advanced-panel-body">
            <div class="asset-library">${assetCards}</div>
          </div>
        </details>

        <section id="feedback-history" class="panel">
          <h2>反馈记录</h2>
          <p class="muted">本轮结构问题 ${structureFeedback.length} 条；往期记录和附件继续保留。</p>
          <div id="feedbackList">${teamFeedbackRows}</div>
          <details class="advanced-fields" style="margin-top:18px">
            <summary>历史反馈轮次（${historicalRecords.length} 条，附件仍保留在原共享目录）</summary>
            <div class="feedback-history-block">
              <h3>历史结构问题（${historicalStructureRecords.length}）</h3>
              ${historicalFeedbackRows}
              <h3>历史应用效果记录（${historicalEfficiencyRecords.length}）</h3>
              ${historicalEfficiencyRows}
            </div>
          </details>
        </section>
        </div>
        <footer class="workspace-footer"><span>WINNSEN / 16029 PARAMETRIC ENGINEERING</span><span>基础模型 · 工程师深化 · 可追溯修订</span></footer>
      </main>
    </div>
    <script>
      const workspaceViews = Array.from(document.querySelectorAll('[data-workspace-view]'))
      const workspaceNavLinks = Array.from(document.querySelectorAll('[data-workspace-nav]'))
      const feedbackTabs = Array.from(document.querySelectorAll('[data-feedback-tab]'))
      const feedbackPanes = Array.from(document.querySelectorAll('[data-feedback-pane]'))
      const workspaceHashMap = {
        'new-model': 'new-model',
        generate: 'new-model',
        'my-models': 'my-models',
        'generation-results': 'my-models',
        'model-feedback': 'model-feedback',
        feedback: 'model-feedback',
        'efficiency-trial': 'model-feedback',
        clarifications: 'model-feedback',
        archive: 'archive',
        downloads: 'archive',
        'feedback-history': 'archive',
      }
      function activeWorkspaceFromHash() {
        return workspaceHashMap[String(location.hash || '').replace(/^#/, '')] || 'new-model'
      }
      function activeFeedbackPaneFromHash() {
        return String(location.hash || '').replace(/^#/, '') === 'efficiency-trial' ? 'effect' : 'issue'
      }
      function activateWorkspace(name, updateHash = false) {
        const target = workspaceHashMap[name] || 'new-model'
        workspaceViews.forEach((view) => view.classList.toggle('active', view.dataset.workspaceView === target))
        workspaceNavLinks.forEach((link) => {
          const active = link.dataset.workspaceNav === target
          link.classList.toggle('active', active)
          if (active) link.setAttribute('aria-current', 'page')
          else link.removeAttribute('aria-current')
        })
        if (updateHash && location.hash !== '#' + target) history.pushState(null, '', '#' + target)
        if (target === 'new-model' && typeof drawPreview === 'function') requestAnimationFrame(drawPreview)
      }
      function activateFeedbackPane(name) {
        const target = name === 'effect' ? 'effect' : 'issue'
        feedbackTabs.forEach((tab) => {
          const active = tab.dataset.feedbackTab === target
          tab.classList.toggle('active', active)
          tab.setAttribute('aria-selected', String(active))
          tab.tabIndex = active ? 0 : -1
        })
        feedbackPanes.forEach((pane) => pane.classList.toggle('active', pane.dataset.feedbackPane === target))
      }
      workspaceNavLinks.forEach((link) => {
        link.addEventListener('click', (event) => {
          event.preventDefault()
          activateWorkspace(link.dataset.workspaceNav, true)
          window.scrollTo({ top: 0, behavior: matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth' })
        })
      })
      function selectFeedbackTab(tab) {
        activateFeedbackPane(tab.dataset.feedbackTab)
        history.replaceState(null, '', tab.dataset.feedbackTab === 'effect' ? '#efficiency-trial' : '#model-feedback')
      }
      feedbackTabs.forEach((tab, index) => {
        tab.addEventListener('click', () => selectFeedbackTab(tab))
        tab.addEventListener('keydown', (event) => {
          const nextIndex = event.key === 'ArrowRight' ? (index + 1) % feedbackTabs.length
            : event.key === 'ArrowLeft' ? (index + feedbackTabs.length - 1) % feedbackTabs.length
            : event.key === 'Home' ? 0 : event.key === 'End' ? feedbackTabs.length - 1 : -1
          if (nextIndex < 0) return
          event.preventDefault()
          const next = feedbackTabs[nextIndex]
          next.focus()
          selectFeedbackTab(next)
        })
      })
      window.addEventListener('hashchange', () => {
        activateWorkspace(activeWorkspaceFromHash())
        activateFeedbackPane(activeFeedbackPaneFromHash())
      })
      window.addEventListener('popstate', () => {
        activateWorkspace(activeWorkspaceFromHash())
        activateFeedbackPane(activeFeedbackPaneFromHash())
      })
      activateWorkspace(activeWorkspaceFromHash())
      activateFeedbackPane(activeFeedbackPaneFromHash())
      const promptInput = document.getElementById('doorPrompt')
      const generationForm = document.getElementById('generationForm')
      const generatedPromptBox = document.getElementById('generatedPrompt')
      const generationStatus = document.getElementById('generationStatus')
      const taskModeInput = document.getElementById('taskMode')
      const taskModeCards = Array.from(document.querySelectorAll('[data-task-mode]'))
      const generationProgressBar = document.getElementById('generationProgressBar')
      const generationProgressLabel = document.getElementById('generationProgressLabel')
      const generationSubmitButton = document.getElementById('generationSubmitButton')
      const generationCapabilityStatus = document.getElementById('generationCapabilityStatus')
      const generationRequestList = document.getElementById('generationRequestList')
      const generationConfirm = document.getElementById('generationConfirm')
      const generationConfirmSummary = document.getElementById('generationConfirmSummary')
      const generationConfirmBoundary = document.getElementById('generationConfirmBoundary')
      const generationConfirmState = document.getElementById('generationConfirmState')
      const returnToSpecButton = document.getElementById('returnToSpecButton')
      const confirmGenerationSubmit = document.getElementById('confirmGenerationSubmit')
      const taskFilterInput = document.getElementById('taskFilterInput')
      const taskFilterButtons = Array.from(document.querySelectorAll('[data-task-filter]'))
      const generationRequestEmptyFilter = document.getElementById('generationRequestEmptyFilter')
      const widthInputMode = document.getElementById('widthInputMode')
      const requestedWidthInput = document.getElementById('requestedWidthMm')
      const rotationInput = document.getElementById('previewRotation')
      const rotationLabel = document.getElementById('rotationLabel')
      const zoomInput = document.getElementById('previewZoom')
      const zoomLabel = document.getElementById('zoomLabel')
      const previewCanvas = document.getElementById('cabinetPreview')
      const previewContext = previewCanvas.getContext('2d')
      const hudCabinetSize = document.getElementById('hudCabinetSize')
      const hudDoorSize = document.getElementById('hudDoorSize')
      const hudDoorLayout = document.getElementById('hudDoorLayout')
      const hudHardware = document.getElementById('hudHardware')
      const fieldIds = ['cabinetWidth', 'cabinetHeight', 'cabinetDepth', 'columns', 'doorCount', 'doorWidth', 'doorHeight', 'rowSequence', 'doorType', 'lockType', 'hingeType', 'latchType', 'reinforcement', 'openings', 'material', 'thickness']
      const fields = Object.fromEntries(fieldIds.map((id) => [id, document.getElementById(id)]))
      const doorLayoutStatus = document.getElementById('doorLayoutStatus')
      const doorColumns = ['L', 'R'].map((side, index) => {
        const prefix = index === 0 ? 'left' : 'right'
        return { side, label:index === 0 ? '左列' : '右列', countInput:document.getElementById(prefix + 'DoorCount'),
          list:document.getElementById(prefix + 'DoorHeights'), summary:document.getElementById(prefix + 'DoorSummary'),
          mode:document.getElementById(prefix + 'DoorMode'), allocationMode:'height', ratios:index === 0 ? ['3', '2', '1'] : ['1', '2', '3'],
          automatic:[false, false, true], heightValues:[], allocationError:'' }
      })
      let previewTiltDeg = 18
      let isDraggingPreview = false
      let dragStartX = 0
      let dragStartRotation = Number(rotationInput.value)
      let isSyncingFields = false
      let manualTaskMode = false
      let lastAutoDoorWidth = fields.doorWidth.value
      let lastAutoDoorHeight = fields.doorHeight.value
      let pendingGenerationPayload = null
      let pendingGenerationSignature = ''
      let generationSubmissionInFlight = false
      let activeTaskFilter = 'all'
      setGenerationProgress(0, '等待提交')

      function setGenerationProgress(percent, label) {
        generationProgressBar.style.width = Math.max(0, Math.min(100, percent)) + '%'
        generationProgressLabel.textContent = label
      }

      function generationStatusTextForClient(item) {
        if (item.status === 'native_task_claimed') return '原生任务已由工作器接收'
        if (item.status === 'native_task_cancel_requested') return '已提交取消请求，等待当前原生步骤安全结束'
        if (item.engineeringAssistanceReady === true && item.downloadUrl) {
          return '同规格结构工程辅助基础模型已找到，可下载后继续深化'
        }
        if (item.status === 'native_task_created') return '原生新任务已建立，等待原生建模与结构检查'
        if (item.status === 'native_source_planning') return '正在整理原生建模来源与结构规则'
        if (item.status === 'native_build_in_progress') return '原生SolidWorks模型构建中'
        if (item.status === 'native_validation_in_progress') return '原生模型结构检查中'
        if (item.status === 'native_task_needs_input') return '原生任务需要补充参数'
        return item.message || '原生任务状态待更新'
      }

      function isDeletableGenerationRequestForClient(item) {
        if (!item) return false
        return [
          'native_assistance_model_ready',
          'native_task_created',
          'native_task_claimed',
          'native_source_planning',
          'native_build_in_progress',
          'native_validation_in_progress',
          'native_task_needs_input',
          'native_task_blocked',
          'native_task_failed',
        ].includes(String(item.status || '').toLowerCase())
      }

      function generationActionForClient(item) {
        const wrapper = document.createElement('div')
        wrapper.className = 'task-row-actions'
        if (item.engineeringAssistanceReady === true && item.downloadUrl) {
          const link = document.createElement('a')
          link.className = 'button'
          link.href = item.downloadUrl
          link.textContent = '下载结构工程辅助基础模型'
          wrapper.append(link)
        } else {
          const button = document.createElement('button')
          button.type = 'button'
          button.disabled = true
          button.textContent = '等待原生模型构建与检查'
          wrapper.append(button)
        }
        if (isDeletableGenerationRequestForClient(item)) {
          const deleteButton = document.createElement('button')
          deleteButton.type = 'button'
          deleteButton.textContent = item.status === 'native_assistance_model_ready' ? '归档记录' : '取消任务'
          deleteButton.className = 'secondary danger generation-delete-button'
          deleteButton.dataset.requestId = item.id
          wrapper.append(deleteButton)
        }
        return wrapper
      }

      function generationWarningForClient(item) {
        return ''
      }

      function generationRequestRow(item) {
        const status = taskStatusForClient(item)
        const modeLabel = item.taskMode === 'single_model' ? '单个模型' : '完整装配体'
        const taskName = item.customerRequirementReference || modeLabel
        const dimensions = (item.cabinetWidth || '-') + 'W × ' + (item.cabinetHeight || '-') + 'H × ' + (item.cabinetDepth || '-') + 'D'
        const doorText = (item.doorCount || '-') + ' 门｜' + (item.previewDimensionsValidated === false ? '布局预估' : '已记录门板') + ' ' + (item.doorWidth || '-') + 'W × ' + (item.doorHeight || '-') + 'H'
        const row = document.createElement('article')
        row.className = 'task-row task-' + status.key
        row.dataset.taskRow = ''
        row.dataset.taskState = status.key
        row.dataset.taskSearch = (taskName + ' ' + item.id + ' ' + dimensions + ' ' + status.label).toLowerCase()
        const rail = document.createElement('div')
        rail.className = 'task-state-rail ' + status.key
        rail.setAttribute('aria-label', '任务状态 ' + status.label)
        const main = document.createElement('div')
        main.className = 'task-row-main'
        const title = document.createElement('div')
        title.className = 'task-row-title'
        const titleText = document.createElement('strong')
        titleText.textContent = taskName
        const code = document.createElement('code')
        code.textContent = item.id || ''
        const badge = document.createElement('span')
        badge.className = 'task-status ' + status.key
        badge.textContent = status.label
        title.append(titleText, badge, code)
        const dims = document.createElement('div')
        dims.className = 'task-dimensions'
        const size = document.createElement('span')
        size.textContent = dimensions
        const doors = document.createElement('span')
        doors.textContent = doorText
        dims.append(size, doors)
        const detail = document.createElement('p')
        detail.textContent = generationStatusTextForClient(item)
        const actions = generationActionForClient(item)
        const feedbackButton = document.createElement('button')
        feedbackButton.className = 'secondary task-feedback-button'
        feedbackButton.type = 'button'
        feedbackButton.dataset.taskFeedbackId = item.id || ''
        feedbackButton.textContent = '反馈修订'
        actions.append(feedbackButton)
        main.append(title, dims, detail, actions)
        row.append(rail, main)
        const timestamp = document.createElement('time')
        timestamp.dateTime = item.createdAt || ''
        timestamp.textContent = item.createdAt ? new Date(item.createdAt).toLocaleString('zh-CN', { hour12:false }) : '刚刚提交'
        row.append(timestamp)
        return row
      }

      function generationEmptyStateForClient() {
        const empty = document.createElement('div')
        empty.className = 'task-empty'
        const title = document.createElement('strong')
        title.textContent = '开始你的第一组结构规格'
        const description = document.createElement('span')
        description.textContent = '定义外形与门数，核对布局后建立模型任务。'
        const link = document.createElement('a')
        link.className = 'button secondary'
        link.href = '#new-model'
        link.textContent = '定义第一组规格 →'
        empty.append(title, description, link)
        return empty
      }

      function taskStatusForClient(item) {
        if (item && item.engineeringAssistanceReady === true && item.downloadUrl) return { key:'ready', label:'可下载' }
        switch (String(item && item.status || '').toLowerCase()) {
          case 'native_task_created':
          case 'native_task_claimed':
          case 'native_source_planning': return { key:'queued', label:'排队' }
          case 'native_build_in_progress': return { key:'building', label:'构建' }
          case 'native_validation_in_progress': return { key:'checking', label:'检查' }
          case 'native_task_blocked': return { key:'failed', label:'待处理' }
          case 'native_task_failed': return { key:'failed', label:'未通过' }
          case 'native_task_needs_input': return { key:'hold', label:'待补充' }
          case 'native_task_cancel_requested': return { key:'hold', label:'取消中' }
          default: return { key:'queued', label:'待更新' }
        }
      }

      function applyTaskFilters() {
        const query = String(taskFilterInput && taskFilterInput.value || '').trim().toLowerCase()
        let visible = 0
        document.querySelectorAll('[data-task-row]').forEach((row) => {
          const state = row.dataset.taskState || 'queued'
          const matchesState = activeTaskFilter === 'all' || state === activeTaskFilter || (activeTaskFilter === 'failed' && state === 'hold')
          const matchesQuery = !query || String(row.dataset.taskSearch || '').includes(query)
          const show = matchesState && matchesQuery
          row.hidden = !show
          if (show) visible += 1
        })
        if (generationRequestEmptyFilter) generationRequestEmptyFilter.hidden = visible !== 0 || !generationRequestList.querySelector('[data-task-row]')
      }

      let taskRefreshPending = null
      let lastTaskSnapshot = ''
      async function refreshGenerationRequestList() {
        if (!generationRequestList) return []
        if (taskRefreshPending) return taskRefreshPending
        taskRefreshPending = (async () => {
          const response = await fetch('/generation-requests.json', { cache:'no-store', signal:AbortSignal.timeout(10000) })
          if (!response.ok) throw new Error('任务状态刷新失败，请检查登录或连接')
          const data = await response.json()
          const requests = Array.isArray(data.requests) ? data.requests : []
          const snapshot = JSON.stringify(requests)
          if (snapshot !== lastTaskSnapshot) {
            generationRequestList.replaceChildren(...(requests.length ? requests.map(generationRequestRow) : [generationEmptyStateForClient()]))
            lastTaskSnapshot = snapshot
            applyTaskFilters()
          }
          document.getElementById('taskRefreshStatus').textContent = '状态已更新 ' + new Date().toLocaleTimeString('zh-CN', { hour12:false }) + ' · 每 5 秒自动刷新'
          return requests
        })()
        try { return await taskRefreshPending } finally { taskRefreshPending = null }
      }
      function refreshVisibleTasks() {
        if (document.hidden) return
        refreshGenerationRequestList().catch(() => {
          document.getElementById('taskRefreshStatus').textContent = '状态暂未更新，请检查连接；当前显示的是上次结果'
        })
      }
      setInterval(refreshVisibleTasks, 5000)
      document.addEventListener('visibilitychange', refreshVisibleTasks)
      window.addEventListener('focus', refreshVisibleTasks)

      async function updateGenerationRequestAction(requestId, action) {
        const response = await fetch('/generation-request/' + encodeURIComponent(requestId) + '/' + action, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
        })
        const result = await response.json()
        if (!response.ok) throw new Error(result.error || '任务操作失败')
        return result
      }

      generationRequestList?.addEventListener('click', async (event) => {
        const feedbackButton = event.target.closest('.task-feedback-button')
        if (feedbackButton) {
          const requestId = feedbackButton.dataset.taskFeedbackId
          const feedbackSelect = document.getElementById('feedbackGenerationRequestId')
          const feedbackContext = document.getElementById('feedbackTaskContext')
          if (requestId && feedbackSelect) {
            if (!Array.from(feedbackSelect.options).some(option => option.value === requestId)) {
              const option = document.createElement('option')
              option.value = requestId
              option.textContent = feedbackButton.closest('[data-task-row]')?.querySelector('strong')?.textContent || requestId
              feedbackSelect.append(option)
            }
            feedbackSelect.value = requestId
            if (feedbackContext) {
              feedbackContext.hidden = false
              feedbackContext.textContent = '已关联任务：' + (feedbackSelect.selectedOptions[0] ? feedbackSelect.selectedOptions[0].textContent : requestId)
            }
          }
          activateWorkspace('model-feedback', true)
          activateFeedbackPane('issue')
          requestAnimationFrame(() => document.getElementById('feedback')?.scrollIntoView({ behavior:'smooth', block:'start' }))
          return
        }
        const deleteButton = event.target.closest('.generation-delete-button')
        if (!deleteButton) return
        const button = deleteButton
        const requestId = button.dataset.requestId
        if (!requestId) return
        button.disabled = true
        try {
          button.textContent = '处理中...'
          const result = await updateGenerationRequestAction(requestId, 'delete')
          generationStatus.className = 'ok'
          generationStatus.textContent = result.action === 'cancel_requested'
            ? '已请求取消任务：' + requestId + '。任务仍会显示到当前原生步骤安全结束。'
            : result.action === 'archived'
              ? '已从列表归档记录：' + requestId + '。任务审计文件和基础模型均保留。'
              : '已取消任务：' + requestId + '。任务审计文件仍保留。'
          await refreshGenerationRequestList()
        } catch (error) {
          generationStatus.className = 'alert'
          generationStatus.textContent = error instanceof Error ? error.message : '任务操作失败'
          await refreshGenerationRequestList()
        }
      })

      taskFilterInput?.addEventListener('input', applyTaskFilters)
      taskFilterButtons.forEach((button) => button.addEventListener('click', () => {
        activeTaskFilter = button.dataset.taskFilter || 'all'
        taskFilterButtons.forEach((item) => item.classList.toggle('active', item === button))
        applyTaskFilters()
      }))
      applyTaskFilters()

      function inferTaskMode(text) {
        const value = String(text || '').toLowerCase()
        if (/单个|单件|单门|门板|柜门|single|panel|door\\s*panel|one\\s*door/.test(value)) return 'single_model'
        if (/完整|整柜|装配体|assembly|cabinet|locker/.test(value)) return 'full_assembly'
        return 'full_assembly'
      }

      function setTaskMode(mode, isManual = false) {
        const normalized = mode === 'single_model' ? 'single_model' : 'full_assembly'
        if (isManual) manualTaskMode = true
        taskModeInput.value = normalized
        taskModeCards.forEach((card) => card.classList.toggle('active', card.getAttribute('data-task-mode') === normalized))
        if (normalized === 'single_model') {
          fields.columns.value = '1'
          fields.doorCount.value = '1'
          fields.rowSequence.value = 'single panel'
          lastAutoDoorWidth = String(Math.round(numberValue('cabinetWidth', 800)))
          lastAutoDoorHeight = String(Math.round(numberValue('cabinetHeight', 1917)))
          fields.doorWidth.value = lastAutoDoorWidth
          fields.doorHeight.value = lastAutoDoorHeight
        } else if (fields.columns.value === '1') {
          fields.columns.value = '2'
          const rowUnits = explicitRowUnits(fields.rowSequence.value)
          fields.doorCount.value = String(rowUnits.length ? rowUnits.length * 2 : 6)
          if (fields.rowSequence.value === 'single panel') fields.rowSequence.value = '6/12, 4/12, 2/12'
        }
      }

      function numberValue(id, fallback) {
        const value = Number(fields[id].value)
        return Number.isFinite(value) && value > 0 ? value : fallback
      }

      function firstMatch(text, patterns) {
        for (const pattern of patterns) {
          const match = pattern.exec(text)
          if (match && match[1]) return Number(match[1])
        }
        return null
      }

      function inferLockType(text, lower) {
        const electricLockExcluded =
          /\\bno\\s+(?:cabinet-side\\s+)?electric\\s+lock(?:\\s+(?:body|hook))?\\b/.test(lower) ||
          /\\bno\\s+electric\\s+hardware\\b/.test(lower) ||
          /(?:不生成|不包含|不要|无|排除).*电控锁/.test(text) ||
          /电控锁.*(?:排除|不生成|不包含|不要)/.test(text)
        const hasElectricLock = !electricLockExcluded && (/\\belectric\\s+lock\\b/.test(lower) || text.includes('电控锁') || text.includes('电控'))
        const hasMechanicalLock = /\\bmechanical\\b/.test(lower) || text.includes('机械') || text.includes('锁舌')

        if (hasElectricLock) return '电控锁'
        if (hasMechanicalLock && electricLockExcluded) return '机械锁舌；电控锁实体排除'
        if (hasMechanicalLock) return '机械锁'
        if (electricLockExcluded) return '电控锁实体排除'
        return '待确认'
      }

      function parseDimensions(text) {
        const match = text.match(/(\\d+(?:\\.\\d+)?)\\s*(?:mm)?\\s*[x×*]\\s*(\\d+(?:\\.\\d+)?)\\s*(?:mm)?\\s*[x×*]\\s*(\\d+(?:\\.\\d+)?)/i)
        return match ? { width: Number(match[1]), height: Number(match[2]), depth: Number(match[3]) } : null
      }

      function parseRowSequence(text) {
        const compact = text.match(/\\bL\\s*([0-9]+(?:[.-][0-9]+)*)\\s*[-_/]\\s*R\\s*([0-9]+(?:[.-][0-9]+)*)\\b/i)
        if (compact) return 'L' + compact[1].replace(/[^0-9.]/g, '') + '-R' + compact[2].replace(/[^0-9.]/g, '')
        const left = labeledRowUnits(text, ['left', 'L'])
        const right = labeledRowUnits(text, ['right', 'R'])
        if (left.length && right.length) return 'L: ' + left.map((unit) => unit + '/12').join(', ') + '; R: ' + right.map((unit) => unit + '/12').join(', ')
        if (/LMS/i.test(text)) return '6/12, 4/12, 2/12'
        if (/SML/i.test(text)) return '2/12, 4/12, 6/12'
        const explicit = text.match(/(?:门高序列|高度序列|row\\s*units?)\\s*[:：=]\\s*([^。；;]+)/i)
        if (explicit && explicit[1]) return explicit[1].trim()
        const fractions = Array.from(text.matchAll(/([1-6])\\s*\\/\\s*12/g), (match) => match[1] + '/12')
        return fractions.length ? Array.from(new Set(fractions)).join(', ') : 'equal rows'
      }

      function explicitRowUnits(sequence) {
        const units = Array.from(String(sequence || '').matchAll(/([1-6])\\s*\\/\\s*12/g), (match) => Number(match[1]))
        return units
      }

      function plainRowUnits(sequence) {
        if (/\\d+\\s*\\/\\s*12/.test(String(sequence || ''))) return []
        return Array.from(String(sequence || '').matchAll(/(?<![0-9.])([1-9](?:\\.[0-9]+)?)(?![0-9.])/g), (match) => Number(match[1]))
          .filter((unit) => Number.isFinite(unit) && unit > 0 && unit <= 12)
      }

      function compactCodeUnits(value) {
        const cleaned = String(value || '').replace(/[^0-9.]/g, '')
        if (!cleaned) return []
        if (cleaned.includes('.')) return cleaned.split('.').map(Number).filter((unit) => Number.isFinite(unit) && unit > 0)
        return Array.from(cleaned, (char) => Number(char)).filter((unit) => Number.isFinite(unit) && unit > 0)
      }

      function normalizeRatioUnits(units) {
        const sum = units.reduce((total, unit) => total + unit, 0)
        if (!sum) return []
        return units.map((unit) => Math.round((unit / sum) * 12000) / 1000)
      }

      function unitsFromAnySequence(value) {
        const fractionUnits = explicitRowUnits(value)
        if (fractionUnits.length) return fractionUnits
        return plainRowUnits(value)
      }

      function labeledRowUnits(text, labels) {
        const source = String(text || '')
        for (const label of labels) {
          const escaped = String(label)
          const prefix = '(?:^|[^A-Za-z])' + escaped
          const bracket = new RegExp(prefix + '\\\\s*(?:column)?\\\\s*[:=]?\\\\s*[\\\\[(]\\\\s*([^\\\\])]+)\\\\s*[\\\\])]', 'i')
          const bracketMatch = source.match(bracket)
          if (bracketMatch && bracketMatch[1]) return unitsFromAnySequence(bracketMatch[1])
          const inline = new RegExp(prefix + '\\\\s*(?:column)?\\\\s*[:=]?\\\\s*((?:[0-9]+(?:\\\\.[0-9]+)?\\\\s*(?:/\\\\s*12)?\\\\s*[,;\\\\s-]+){1,8}[0-9]+(?:\\\\.[0-9]+)?\\\\s*(?:/\\\\s*12)?)', 'i')
          const inlineMatch = source.match(inline)
          if (inlineMatch && inlineMatch[1]) return unitsFromAnySequence(inlineMatch[1])
        }
        return []
      }

      function explicitColumnRowUnits(sequence, columns) {
        const source = String(sequence || '')
        const compact = source.match(/\\bL\\s*([0-9]+(?:[.-][0-9]+)*)\\s*[-_/]\\s*R\\s*([0-9]+(?:[.-][0-9]+)*)\\b/i)
        if (compact) {
          const left = compactCodeUnits(compact[1])
          const right = compactCodeUnits(compact[2])
          if (left.length && right.length) return [normalizeRatioUnits(left), normalizeRatioUnits(right)].slice(0, columns)
        }
        const left = labeledRowUnits(source, ['left', 'L'])
        const right = labeledRowUnits(source, ['right', 'R'])
        if (left.length && right.length) return [normalizeRatioUnits(left), normalizeRatioUnits(right)].slice(0, columns)
        return []
      }

      function hasExplicitRowSequence(sequence) {
        return explicitColumnRowUnits(sequence, 2).length > 0 || explicitRowUnits(sequence).length > 0 || plainRowUnits(sequence).length > 0
      }

      function rowUnitsFromSequence(sequence, doorCount, columns) {
        const units = explicitRowUnits(sequence)
        if (units.length) return normalizeRatioUnits(units)
        const plainUnits = plainRowUnits(sequence)
        if (plainUnits.length) return normalizeRatioUnits(plainUnits)
        const rows = Math.max(1, Math.ceil(doorCount / columns))
        return Array.from({ length: rows }, () => 1)
      }

      function columnRowUnitsFromSequence(sequence, doorCount, columns) {
        const explicitColumns = explicitColumnRowUnits(sequence, columns)
        if (explicitColumns.length) return explicitColumns
        const shared = rowUnitsFromSequence(sequence, doorCount, columns)
        return Array.from({ length: columns }, () => shared)
      }

      function doorCountFromColumnUnits(columns) {
        return columns.reduce((total, units) => total + units.length, 0)
      }

      function sequenceSummaryForHud(columns) {
        return columns.map((units, index) => (index === 0 ? 'L ' : index === 1 ? 'R ' : 'C' + (index + 1) + ' ') + units.map((unit) => String(unit).replace(/\\.0$/, '')).join('/')).join(' | ')
      }

      function estimateDoorWidthFor(cabinetWidth, columns) {
        if (Math.abs(cabinetWidth - 800) < 1 && columns === 2) return 337
        if (Math.abs(cabinetWidth - 1000) < 1 && columns === 2) return 437
        return Math.max(80, Math.round((cabinetWidth - 126) / columns))
      }

      function estimateDoorHeightFor(cabinetHeight, doorCount, columns, rowSequence) {
        const columnUnits = columnRowUnitsFromSequence(rowSequence, doorCount, columns)
        const explicitUnits = columnUnits[0] || []
        const units = explicitUnits.length ? explicitUnits : rowUnitsFromSequence(rowSequence, doorCount, columns)
        const rows = Math.max(1, units.length || Math.ceil(doorCount / columns))
        if (explicitUnits.length) return Math.max(80, Math.round(explicitUnits[0] * 152.5 - 7))
        const bodyHeight = Math.min(cabinetHeight - 90, 1827)
        return Math.max(80, Math.round((bodyHeight - (rows - 1) * 7) / rows))
      }

      function customerRowSequenceForDoorCount(doorCount) {
        if (doorCount === 4) return 'L66-R66'
        if (doorCount === 6) return 'L642-R246'
        const rowsPerColumn = Math.max(1, Math.round(doorCount / 2))
        const code = '1'.repeat(rowsPerColumn)
        return 'L' + code + '-R' + code
      }

      function formatDoorMm(value) {
        return Number.isFinite(value) ? String(Math.round(value * 1000) / 1000) : '—'
      }

      function doorHeightsForUnits(height, units) {
        return distributeDoorBudget(height - 87, units).map((pitch) => Number(formatDoorMm(pitch - 7)))
      }

      // Allocate whole thousandths so displayed, previewed and submitted heights agree.
      function distributeDoorBudget(budgetMm, weights) {
        const total = weights.reduce((sum, value) => sum + value, 0)
        const budget = Math.round(budgetMm * 1000)
        if (!Number.isSafeInteger(budget) || budget < 0 || !Number.isFinite(total) || total <= 0 || weights.some((value) => !Number.isFinite(value) || value <= 0)) return weights.map(() => NaN)
        const shares = weights.map((value) => budget * (value / total))
        const allocated = shares.map(Math.floor)
        const order = shares.map((value, index) => ({ index, fraction:value - allocated[index] })).sort((a, b) => b.fraction - a.fraction || a.index - b.index)
        const remainder = budget - allocated.reduce((sum, value) => sum + value, 0)
        for (let index = 0; index < remainder; index++) allocated[order[index % order.length].index]++
        return allocated.map((value) => value / 1000)
      }

      function ratiosForDoorHeights(heights) {
        const pitches = heights.map((value) => Math.round((value + 7) * 1000))
        if (pitches.some((value) => !Number.isSafeInteger(value) || value <= 0)) return heights.map(() => '1')
        const gcd = (a, b) => { while (b) { const next = a % b; a = b; b = next } return a }
        const divisor = pitches.reduce(gcd)
        return pitches.map((value) => String(value / divisor))
      }

      function resolveDoorColumn(column, height) {
        const count = column.heightValues.length
        const available = height - 87 - 7 * count
        column.allocationError = ''
        if (column.allocationMode === 'ratio') {
          const weights = column.ratios.map((value) => value.trim() ? Number(value) : NaN)
          if (weights.some((value) => !Number.isFinite(value) || value < .001) || !Number.isFinite(weights.reduce((sum, value) => sum + value, 0))) {
            column.allocationError = '每扇门的比例须大于 0，至少 0.001。'
            column.heightValues = weights.map(() => '')
          } else column.heightValues = distributeDoorBudget(height - 87, weights).map((pitch) => formatDoorMm(pitch - 7))
          return
        }
        const autoRows = column.automatic.map((automatic, index) => automatic ? index : -1).filter((index) => index >= 0)
        if (!autoRows.length) return
        const fixed = column.heightValues.filter((value, index) => !column.automatic[index]).map((value) => value.trim() ? Number(value) : NaN)
        if (fixed.some((value) => !Number.isFinite(value) || value < 100)) {
          column.allocationError = '先填写固定门高，每扇至少 100 mm；余下由自动门分配。'
          autoRows.forEach((index) => { column.heightValues[index] = '' })
          return
        }
        const remainder = (Math.round(available * 1000) - fixed.reduce((sum, value) => sum + Math.round(value * 1000), 0)) / 1000
        const assigned = distributeDoorBudget(remainder, autoRows.map(() => 1))
        if (remainder < autoRows.length * 100) column.allocationError = '固定门高占用过多，' + autoRows.length + ' 扇自动门还需 ' + formatDoorMm(autoRows.length * 100 - remainder) + ' mm 才能达到每门 100 mm；请减小固定门高或增加柜高。'
        autoRows.forEach((index, row) => { column.heightValues[index] = Number.isFinite(assigned[row]) ? formatDoorMm(assigned[row]) : '' })
      }

      function syncDoorColumnInputs(height) {
        for (const column of doorColumns) {
          if (!column.heightValues.length) column.heightValues = doorHeightsForUnits(height, column.ratios.map(Number)).map(formatDoorMm)
          resolveDoorColumn(column, height)
          const ratioMode = column.allocationMode === 'ratio'
          const container = column.list.closest('[data-door-column]')
          container.dataset.allocationMode = column.allocationMode
          container.querySelectorAll('[data-column-mode]').forEach((button) => button.setAttribute('aria-pressed', String(button.dataset.allocationMode === column.allocationMode)))
          if (column.list.children.length !== column.heightValues.length) {
            column.list.replaceChildren(...column.heightValues.map((value, index) => {
              const row = document.createElement('li')
              row.className = 'door-height-row'
              const label = document.createElement('label')
              const valueField = document.createElement('div')
              valueField.className = 'door-value-field'
              const input = document.createElement('input')
              input.type = 'number'
              input.step = '.001'
              input.required = true
              input.dataset.row = String(index + 1)
              input.setAttribute('aria-describedby', column.summary.id + ' ' + column.mode.id)
              label.append(document.createTextNode(String(index + 1).padStart(2, '0')))
              if (index === 0 || index === column.heightValues.length - 1) {
                const position = document.createElement('small')
                position.textContent = column.heightValues.length === 1 ? '整列' : index === 0 ? '最下' : '最上'
                label.append(position)
              }
              const result = document.createElement('small')
              result.dataset.doorResult = column.side
              const unit = document.createElement('span')
              unit.dataset.doorUnit = column.side
              const toggle = document.createElement('button')
              toggle.type = 'button'
              toggle.className = 'door-height-toggle'
              toggle.dataset.doorAuto = column.side
              toggle.dataset.row = String(index + 1)
              valueField.append(input, result)
              row.append(label, valueField, unit, toggle)
              return row
            }))
          }
          column.list.querySelectorAll('input').forEach((input, index) => {
            input.id = (ratioMode ? 'doorRatio' : 'doorHeight') + column.side + (index + 1)
            input.closest('li').querySelector('label').htmlFor = input.id
            input.min = ratioMode ? '.001' : '100'
            input.readOnly = !ratioMode && column.automatic[index]
            input.dataset.doorHeight = column.side
            input.setAttribute('aria-label', column.label + '第' + (index + 1) + '门' + (ratioMode ? '分格比例' : '成品高度（mm）'))
            const value = ratioMode ? column.ratios[index] : column.heightValues[index]
            if (input.value !== value) input.value = value === '—' ? '' : value
            const row = input.closest('li')
            const result = row.querySelector('[data-door-result]')
            result.hidden = !ratioMode
            result.textContent = formatDoorMm(Number(column.heightValues[index] || NaN)) + ' mm'
            row.querySelector('[data-door-unit]').textContent = ratioMode ? '份' : ''
            row.querySelector('[data-door-unit]').hidden = !ratioMode
            const toggle = row.querySelector('[data-door-auto]')
            toggle.hidden = ratioMode
            toggle.textContent = column.automatic[index] ? '自动' : '固定'
            toggle.setAttribute('aria-pressed', String(column.automatic[index]))
            toggle.setAttribute('aria-label', column.label + '第' + (index + 1) + '门' + (column.automatic[index] ? '自动分配中，改为固定门高' : '固定门高中，改为自动分配'))
            toggle.title = column.automatic[index] ? '点此指定固定门高' : '点此参与剩余高度的自动分配'
          })
          const autoCount = column.automatic.filter(Boolean).length
          column.mode.textContent = ratioMode ? '输入大小比例，如 3 : 2 : 1；下方显示成品门高，已扣门缝。'
            : autoCount ? '填写固定门高；' + autoCount + ' 扇自动门分配余量。点“固定 / 自动”可切换。'
            : '全部门高已固定；可将任意一扇改为“自动”补齐余量。'
        }
      }

      function currentDoorLayout(height = Number(fields.cabinetHeight.value)) {
        const columns = doorColumns.map((column) => {
          const count = Number(column.countInput.value)
          const countValid = Number.isInteger(count) && count >= 1 && count <= 24 && count === column.heightValues.length
          const heights = column.heightValues.map((value) => value.trim() ? Number(value) : NaN)
          const heightsValid = heights.length > 0 && heights.every((value) => Number.isFinite(value) && value >= 100)
          const precisionValid = Array.from(column.list.querySelectorAll('input')).every((input) => !input.validity.stepMismatch)
          const used = heights.reduce((sum, value) => sum + value, 0)
          const available = height - 87 - 7 * heights.length
          const difference = available - used
          const closed = Number.isFinite(difference) && Math.abs(difference) < .0005
          return { side:column.side, label:column.label, count, countValid, heights, heightsValid, precisionValid, used, available, difference, closed, allocationError:column.allocationError,
            doors:heights.map((heightMm) => ({ heightMm })) }
        })
        const countValid = columns.every((column) => column.countValid)
        const total = countValid ? columns.reduce((sum, column) => sum + column.count, 0) : 0
        const errors = []
        for (const column of columns) {
          if (!column.countValid) errors.push(column.label + '门数须为 1–24 的整数。')
          else if (column.allocationError) errors.push(column.label + '：' + column.allocationError)
          else if (!column.heightsValid) errors.push(column.label + '：门高至少 100 mm，请调整比例、固定门高或柜高。')
          else if (!column.precisionValid) errors.push(column.label + '门高或比例最多保留三位小数。')
          else if (!column.closed) errors.push(column.label + (column.difference > 0 ? '还差 ' : '超出 ') + formatDoorMm(Math.abs(column.difference)) + ' mm，将一扇门改为“自动”即可补齐。')
        }
        if (countValid && total > 34) errors.push('总门数最多 34 门，请调整左右列门数。')
        return { columns, countValid, total, valid:countValid && total >= 2 && total <= 34 && errors.length === 0, errors }
      }

      function legacySequenceForLayout(layout, height) {
        if (!layout.valid || layout.columns[0].count !== layout.columns[1].count) return ''
        const rows = layout.columns[0].count
        const units = layout.total === 6 ? [[6, 4, 2], [2, 4, 6]] : [Array(rows).fill(1), Array(rows).fill(1)]
        const same = layout.columns.every((column, index) => {
          const expected = doorHeightsForUnits(height, units[index])
          return column.heights.every((value, row) => Math.abs(value - expected[row]) < .0005)
        })
        return same ? customerRowSequenceForDoorCount(layout.total) : ''
      }

      function updateDoorLayoutStatus(layout, heightValid) {
        layout.columns.forEach((column, index) => {
          const editor = doorColumns[index]
          editor.countInput.setAttribute('aria-invalid', String(!column.countValid))
          editor.list.querySelectorAll('input').forEach((input, row) => input.setAttribute('aria-invalid', String(!Number.isFinite(column.heights[row]) || column.heights[row] < 100 || input.validity.stepMismatch || !input.checkValidity())))
          const valid = column.countValid && column.heightsValid && column.precisionValid && column.closed && heightValid
          editor.summary.dataset.valid = String(valid)
          editor.summary.textContent = !column.countValid ? '请填写有效门数。' : column.allocationError ? column.allocationError : !Number.isFinite(column.used) ? '请填完门高或比例。'
            : '门高合计 ' + formatDoorMm(column.used) + ' / ' + formatDoorMm(column.available) + ' mm · ' +
              (column.closed ? '已闭合' : column.difference > 0 ? '还差 ' + formatDoorMm(column.difference) : '超出 ' + formatDoorMm(-column.difference))
          document.querySelector('[data-column-equalize="' + column.side + '"]').disabled = !column.countValid || !heightValid
          const top = column.available - column.heights.slice(0, -1).reduce((sum, value) => sum + value, 0)
          document.querySelector('[data-column-fill-top="' + column.side + '"]').disabled = !column.countValid || !heightValid || !Number.isFinite(top) || top < 100
        })
        doorLayoutStatus.dataset.valid = String(layout.valid && heightValid)
        doorLayoutStatus.textContent = !heightValid ? '柜高须在 1700–2200 mm 范围内。' : layout.valid
          ? '左 ' + layout.columns[0].count + ' 门 / 右 ' + layout.columns[1].count + ' 门，共 ' + layout.total + ' 门。两列门高与门缝均已闭合。'
          : layout.errors.join(' ')
      }

      function syncCustomerGenerationFields() {
        const requestedWidth = Number(requestedWidthInput.value)
        const mode = widthInputMode.value
        const cabinetDepth = Number(fields.cabinetDepth.value)
        const cabinetHeight = Number(fields.cabinetHeight.value)
        syncDoorColumnInputs(cabinetHeight)
        const layout = currentDoorLayout(cabinetHeight)
        const doorCount = layout.total
        const cabinetWidth = mode === 'installed_door_panel_width'
          ? requestedWidth * 2 + 126
          : requestedWidth
        const doorWidth = mode === 'installed_door_panel_width'
          ? requestedWidth
          : (requestedWidth - 126) / 2
        const legacySequence = legacySequenceForLayout(layout, cabinetHeight)
        const rowSequence = legacySequence || layout.columns.map((column) => column.side + '[' + column.heights.map(formatDoorMm).join(',') + ']').join('-')
        const doorHeight = layout.columns[0].heights[0]
        const formatMm = formatDoorMm

        fields.cabinetWidth.value = formatMm(cabinetWidth)
        fields.columns.value = '2'
        fields.doorCount.value = layout.countValid ? String(doorCount) : ''
        fields.doorWidth.value = formatMm(doorWidth)
        fields.doorHeight.value = formatMm(doorHeight)
        fields.rowSequence.value = rowSequence
        lastAutoDoorWidth = fields.doorWidth.value
        lastAutoDoorHeight = fields.doorHeight.value
        promptInput.value = [
          formatMm(cabinetWidth) + 'W x ' + formatMm(cabinetHeight) + 'H x ' + formatMm(cabinetDepth) + 'D',
          'two columns',
          doorCount + ' doors',
          rowSequence,
          'installed door panel width ' + formatMm(doorWidth) + 'mm',
          'ordinary locker doors',
          'mechanical door lock tongue',
          'one door maps to one lock opening',
          'no electrical board',
          'no cabinet-side electric lock body',
          'no cabinet-side electric lock hook',
        ].join(', ')

        const isV37Recipe = Math.abs(cabinetWidth - 760) < 0.001 &&
          Math.abs(cabinetDepth - 550) < 0.001 && doorCount === 6 && Math.abs(doorWidth - 317) < 0.001
        const isV36Recipe = Math.abs(cabinetWidth - 740) < 0.001 &&
          Math.abs(cabinetDepth - 550) < 0.001 && doorCount === 4 && Math.abs(doorWidth - 307) < 0.001
        const isV35Recipe = Math.abs(cabinetWidth - 740) < 0.001 &&
          Math.abs(cabinetDepth - 550) < 0.001 && doorCount === 6 && Math.abs(doorWidth - 307) < 0.001
        const exactVerifiedRecipe = layout.valid && Boolean(legacySequence) && cabinetHeight === 1917 && (isV37Recipe || isV36Recipe || isV35Recipe)
        const matchedRecipeLabel = isV37Recipe ? 'V37六门L642-R246' : (isV36Recipe ? 'V36四门L66-R66' : 'V35六门L642-R246')
        const widthValid = Number.isFinite(requestedWidth) && requestedWidth >= 120 && requestedWidth <= 2000 && Number.isFinite(doorWidth) && doorWidth > 0
        const depthValid = Number.isFinite(cabinetDepth) && cabinetDepth >= 250 && cabinetDepth <= 800
        const heightValid = Number.isFinite(cabinetHeight) && cabinetHeight >= 1700 && cabinetHeight <= 2200
        const parametricRange = cabinetWidth >= 700 && cabinetWidth <= 1200 && cabinetHeight >= 1700 && cabinetHeight <= 2200 && cabinetDepth >= 250 && cabinetDepth <= 650 && doorCount <= 34
        const legacyRegistration = Boolean(legacySequence) && cabinetHeight === 1917 && cabinetDepth >= 300 && cabinetDepth <= 800
        const requestValid = widthValid && depthValid && heightValid && layout.valid && (parametricRange || legacyRegistration)
        updateDoorLayoutStatus(layout, heightValid)
        generationSubmitButton.disabled = !requestValid || generationSubmissionInFlight
        generationSubmitButton.textContent = !requestValid
          ? '请检查尺寸与门高闭合'
          : exactVerifiedRecipe
          ? '核对同规格模型'
          : '核对参数与布局'
        generationForm.dataset.deliveryMode = exactVerifiedRecipe ? 'verified_native_model' : 'native_task_pending'
        generationForm.dataset.parametricEligible = String(parametricRange && layout.valid)
        generationCapabilityStatus.className = exactVerifiedRecipe ? 'feedback-boundary generation-capability-ready' : 'feedback-boundary generation-capability-pending'
        generationCapabilityStatus.textContent = !requestValid
          ? !widthValid ? '目标宽度须在 120–2000 mm 内，换算后门板外宽须为正数。'
            : !heightValid ? '柜高须在 1700–2200 mm 范围内。'
            : !depthValid ? '柜深须在 250–800 mm 范围内。'
            : !layout.valid ? layout.errors.join(' ')
            : '自定义门序须在宽 700–1200、高 1700–2200、深 250–650 mm 的参数化范围内提交。'
          : exactVerifiedRecipe
          ? '已找到同规格' + matchedRecipeLabel + '结构工程辅助基础模型。系统会先核对文件完整性，再提供原生SolidWorks模型供结构工程师继续深化。'
          : parametricRange
          ? '当前规格将进入原生参数化生成队列。只有实际模型通过尺寸、装配、钣金展开、干涉和迁移重开检查后才提供下载。'
          : '当前规格超出已开放的参数化范围，提交后仅登记原生建模需求，尚不能自动生成下载。'
        updateGeneratedPrompt()
        drawPreview()
      }

      function syncDependentFields(sourceId) {
        if (isSyncingFields) return
        isSyncingFields = true
        const W = numberValue('cabinetWidth', 800)
        const H = numberValue('cabinetHeight', 1917)
        const columns = Math.max(1, Math.min(6, Math.round(numberValue('columns', 2))))
        const columnUnits = columnRowUnitsFromSequence(fields.rowSequence.value, numberValue('doorCount', 6), columns)
        const sequenceDoorCount = doorCountFromColumnUnits(columnUnits)
        const sequenceDefinesRows = hasExplicitRowSequence(fields.rowSequence.value)
        if (sourceId === 'columns' && fields.columns.value !== String(columns)) fields.columns.value = String(columns)
        if (sourceId === 'rowSequence' && sequenceDefinesRows) fields.doorCount.value = String(sequenceDoorCount)
        const doorCount = Math.max(columns, Math.round(numberValue('doorCount', sequenceDoorCount || 6)))
        const shouldUpdateDoorWidth = ['cabinetWidth', 'columns'].includes(sourceId) || fields.doorWidth.value === lastAutoDoorWidth
        const shouldUpdateDoorHeight = ['cabinetHeight', 'doorCount', 'columns', 'rowSequence'].includes(sourceId) || fields.doorHeight.value === lastAutoDoorHeight
        if (shouldUpdateDoorWidth) {
          lastAutoDoorWidth = String(estimateDoorWidthFor(W, columns))
          fields.doorWidth.value = lastAutoDoorWidth
        }
        if (shouldUpdateDoorHeight) {
          lastAutoDoorHeight = String(estimateDoorHeightFor(H, doorCount, columns, fields.rowSequence.value))
          fields.doorHeight.value = lastAutoDoorHeight
        }
        updateGeneratedPrompt()
        drawPreview()
        isSyncingFields = false
      }

      function applyPromptToFields() {
        const text = promptInput.value || ''
        const lower = text.toLowerCase()
        const taskMode = manualTaskMode ? taskModeInput.value : inferTaskMode(text)
        if (!manualTaskMode) setTaskMode(taskMode)
        const dimensions = parseDimensions(text)
        const cabinetWidth = firstMatch(text, [/外形宽\\s*[:：=]?\\s*(\\d+(?:\\.\\d+)?)/i, /柜宽\\s*[:：=]?\\s*(\\d+(?:\\.\\d+)?)/i, /(\\d+(?:\\.\\d+)?)\\s*(?:mm)?\\s*(?:w|W|宽)/]) || (dimensions && dimensions.width) || 800
        const cabinetHeight = firstMatch(text, [/外形高\\s*[:：=]?\\s*(\\d+(?:\\.\\d+)?)/i, /柜高\\s*[:：=]?\\s*(\\d+(?:\\.\\d+)?)/i, /(\\d+(?:\\.\\d+)?)\\s*(?:mm)?\\s*(?:h|H|高)/]) || (dimensions && dimensions.height) || 1917
        const cabinetDepth = firstMatch(text, [/外形深\\s*[:：=]?\\s*(\\d+(?:\\.\\d+)?)/i, /柜深\\s*[:：=]?\\s*(\\d+(?:\\.\\d+)?)/i, /(\\d+(?:\\.\\d+)?)\\s*(?:mm)?\\s*(?:d|D|深)/]) || (dimensions && dimensions.depth) || 550
        const isSingleModel = taskMode === 'single_model'
        const autoColumns = isSingleModel ? 1 : 2
        const columns = Math.max(1, Math.min(6, Math.round(firstMatch(text, [/(\\d+)\\s*(?:列|columns?|cols?)/i]) || autoColumns)))
        const rowSequence = isSingleModel ? 'single panel' : parseRowSequence(text)
        const sequenceColumns = isSingleModel ? [[1]] : columnRowUnitsFromSequence(rowSequence, 12, columns)
        const sequenceDoorCount = isSingleModel ? 1 : doorCountFromColumnUnits(sequenceColumns)
        const explicitDoorCount = firstMatch(text, [/门数\\s*[:：=]?\\s*(\\d+)/i, /door\\s*count\\s*[:=]?\\s*(\\d+)/i, /(\\d+)\\s*门(?:柜|整柜|布局|方案)/])
        const doorCount = isSingleModel ? 1 : Math.max(columns, Math.round(explicitDoorCount || sequenceDoorCount || 12))
        const explicitDoorWidth = firstMatch(text, [/门宽\\s*[:：=]?\\s*(\\d+(?:\\.\\d+)?)/i, /door\\s*width\\s*[:=]?\\s*(\\d+(?:\\.\\d+)?)/i, /W\\s*(\\d+(?:\\.\\d+)?)/])
        const explicitDoorHeight = firstMatch(text, [/门高\\s*[:：=]?\\s*(\\d+(?:\\.\\d+)?)/i, /door\\s*height\\s*[:=]?\\s*(\\d+(?:\\.\\d+)?)/i, /H\\s*(\\d+(?:\\.\\d+)?)/])
        const estimatedDoorWidth = isSingleModel ? cabinetWidth : estimateDoorWidthFor(cabinetWidth, columns)
        const estimatedDoorHeight = isSingleModel ? cabinetHeight : estimateDoorHeightFor(cabinetHeight, doorCount, columns, rowSequence)
        fields.cabinetWidth.value = String(Math.round(cabinetWidth))
        fields.cabinetHeight.value = String(Math.round(cabinetHeight))
        fields.cabinetDepth.value = String(Math.round(cabinetDepth))
        fields.columns.value = String(columns)
        fields.doorCount.value = String(doorCount)
        fields.doorWidth.value = String(Math.round(explicitDoorWidth || estimatedDoorWidth))
        fields.doorHeight.value = String(Math.round(explicitDoorHeight || estimatedDoorHeight))
        lastAutoDoorWidth = String(Math.round(explicitDoorWidth || estimatedDoorWidth))
        lastAutoDoorHeight = String(Math.round(explicitDoorHeight || estimatedDoorHeight))
        fields.rowSequence.value = rowSequence
        fields.doorType.value = lower.includes('control') || text.includes('中控') ? 'control_door' : 'ordinary_door_panel'
        fields.lockType.value = inferLockType(text, lower)
        fields.hingeType.value = lower.includes('concealed') || text.includes('暗铰') ? '暗铰链' : lower.includes('piano') || text.includes('长铰') ? '长铰链' : '待确认'
        fields.latchType.value = lower.includes('latch') || text.includes('插销') ? '插销/锁扣需确认' : '待确认'
        fields.reinforcement.value = lower.includes('rib') || text.includes('加强') ? '加强筋' : '待确认'
        fields.openings.value = lower.includes('hole') || text.includes('孔') ? '锁孔/铰链孔需确认' : '待确认'
        fields.material.value = lower.includes('galvanized') || text.includes('镀锌') ? '镀锌板' : text.includes('不锈钢') ? '不锈钢' : '待确认'
        const thickness = firstMatch(text, [/(\\d+(?:\\.\\d+)?)\\s*(?:mm)?\\s*(?:厚|thickness|板厚)/i])
        fields.thickness.value = thickness ? thickness + ' mm' : '待确认'
        updateGeneratedPrompt()
        drawPreview()
      }

      function updateGeneratedPrompt() {
        const layout = currentDoorLayout()
        generatedPromptBox.textContent = [
          '外形  /  ' + fields.cabinetWidth.value + ' W × ' + fields.cabinetHeight.value + ' H × ' + fields.cabinetDepth.value + ' D mm',
          '布局  /  左 ' + doorColumns[0].countInput.value + ' 门 · 右 ' + doorColumns[1].countInput.value + ' 门 · 共 ' + fields.doorCount.value + ' 门',
          ...layout.columns.map((column) => column.label + '  /  ' + column.heights.map(formatDoorMm).join(' / ') + ' mm（自下向上）'),
          '门板  /  成品外宽 ' + fields.doorWidth.value + ' mm',
          '结构  /  普通门板 · 机械锁舌 · ' + fields.hingeType.value,
          '备注  /  材料：' + fields.material.value + '；板厚：' + fields.thickness.value,
        ].join('\\n')
      }

      function rotatePreviewPoint(point, angleDeg) {
        const angle = angleDeg * Math.PI / 180
        const tilt = previewTiltDeg * Math.PI / 180
        const cos = Math.cos(angle)
        const sin = Math.sin(angle)
        const x = point.x * cos - point.z * sin
        const z = point.x * sin + point.z * cos
        return { x, y:point.y * Math.cos(tilt) - z * Math.sin(tilt), z:point.y * Math.sin(tilt) + z * Math.cos(tilt) }
      }

      function projectPoint(point, angleDeg) {
        const rotated = rotatePreviewPoint(point, angleDeg)
        return { x:previewCanvas.width / 2 + rotated.x, y:previewCanvas.height / 2 - rotated.y, depth:rotated.z }
      }

      function facePath(face, angleDeg) {
        const normal = rotatePreviewPoint(face.normal, angleDeg)
        if (normal.z <= .00001) return null
        const projected = face.points.map((point) => projectPoint(point, angleDeg))
        return { projected, normal, depth:projected.reduce((sum, point) => sum + point.depth, 0) / projected.length, color:face.color, stroke:face.stroke }
      }

      function addBox(faces, cx, cy, cz, w, h, d, colors, stroke) {
        const x0 = cx - w / 2, x1 = cx + w / 2
        const y0 = cy - h / 2, y1 = cy + h / 2
        const z0 = cz - d / 2, z1 = cz + d / 2
        faces.push({ color:colors.front, stroke, normal:{ x:0, y:0, z:1 }, points:[{ x:x0, y:y0, z:z1 }, { x:x1, y:y0, z:z1 }, { x:x1, y:y1, z:z1 }, { x:x0, y:y1, z:z1 }] })
        faces.push({ color:colors.right, stroke, normal:{ x:1, y:0, z:0 }, points:[{ x:x1, y:y0, z:z1 }, { x:x1, y:y0, z:z0 }, { x:x1, y:y1, z:z0 }, { x:x1, y:y1, z:z1 }] })
        faces.push({ color:colors.left, stroke, normal:{ x:-1, y:0, z:0 }, points:[{ x:x0, y:y0, z:z0 }, { x:x0, y:y0, z:z1 }, { x:x0, y:y1, z:z1 }, { x:x0, y:y1, z:z0 }] })
        faces.push({ color:colors.top, stroke, normal:{ x:0, y:1, z:0 }, points:[{ x:x0, y:y1, z:z1 }, { x:x1, y:y1, z:z1 }, { x:x1, y:y1, z:z0 }, { x:x0, y:y1, z:z0 }] })
        faces.push({ color:colors.bottom, stroke, normal:{ x:0, y:-1, z:0 }, points:[{ x:x0, y:y0, z:z0 }, { x:x1, y:y0, z:z0 }, { x:x1, y:y0, z:z1 }, { x:x0, y:y0, z:z1 }] })
        faces.push({ color:colors.back || colors.right, stroke, normal:{ x:0, y:0, z:-1 }, points:[{ x:x1, y:y0, z:z0 }, { x:x0, y:y0, z:z0 }, { x:x0, y:y1, z:z0 }, { x:x1, y:y1, z:z0 }] })
      }

      function addPreviewFoot(faces, x, y, z, scale) {
        const ring = (height, radius) => Array.from({ length:16 }, (_, index) => {
          const angle = index * Math.PI / 8
          return { x:x + Math.cos(angle) * radius * scale, y:y + height * scale, z:z + Math.sin(angle) * radius * scale }
        })
        const rings = [ring(-30, 18), ring(-25, 18), ring(-19, 9), ring(-19, 6), ring(0, 6)]
        for (let band = 0; band < rings.length - 1; band++) {
          for (let index = 0; index < 16; index++) {
            const next = (index + 1) % 16
            const a = rings[band][index], b = rings[band][next], c = rings[band + 1][next], d = rings[band + 1][index]
            const normal = { x:(a.x + b.x) / 2 - x, y:0, z:(a.z + b.z) / 2 - z }
            const length = Math.hypot(normal.x, normal.z)
            normal.x /= length
            normal.z /= length
            faces.push({ points:[a, b, c, d], normal, color:band < 2 ? '#a9b2b3' : '#c7cecf', stroke:null })
          }
        }
        faces.push({ points:rings[3], normal:{ x:0, y:1, z:0 }, color:'#cdd3d3', stroke:null })
      }

      function shadePreviewColor(color, amount) {
        const channels = [1, 3, 5].map((start) => Math.min(255, Math.max(0, Math.round(parseInt(color.slice(start, start + 2), 16) * amount))))
        return 'rgb(' + channels.join(',') + ')'
      }

      function paintPreviewFace(face) {
        const ctx = previewContext
        ctx.beginPath()
        face.projected.forEach((point, index) => index ? ctx.lineTo(point.x, point.y) : ctx.moveTo(point.x, point.y))
        ctx.closePath()
        const light = .80 + .18 * Math.max(0, -face.normal.x * .4 + face.normal.y * .65 + face.normal.z * .7)
        const minX = Math.min(...face.projected.map((point) => point.x)), maxX = Math.max(...face.projected.map((point) => point.x))
        const minY = Math.min(...face.projected.map((point) => point.y)), maxY = Math.max(...face.projected.map((point) => point.y))
        const gradient = ctx.createLinearGradient(minX, minY, maxX + 1, maxY + 1)
        gradient.addColorStop(0, shadePreviewColor(face.color, light + .065))
        gradient.addColorStop(.45, shadePreviewColor(face.color, light + .02))
        gradient.addColorStop(1, shadePreviewColor(face.color, light - .025))
        ctx.fillStyle = gradient
        ctx.fill()
        if (face.stroke) {
          ctx.strokeStyle = face.stroke
          ctx.lineWidth = .65
          ctx.stroke()
        }
      }

      function drawLine3d(points, angleDeg, color, width, dash) {
        const projected = points.map((point) => projectPoint(point, angleDeg))
        previewContext.save()
        previewContext.beginPath()
        projected.forEach((point, index) => index ? previewContext.lineTo(point.x, point.y) : previewContext.moveTo(point.x, point.y))
        previewContext.strokeStyle = color
        previewContext.lineWidth = width || 1
        previewContext.setLineDash(dash || [])
        previewContext.stroke()
        previewContext.restore()
      }

      function drawViewportGrid(angleDeg, modelW, modelH, modelD, scale) {
        if (Math.abs(previewTiltDeg) < 1 || Math.abs(previewTiltDeg) > 89) return
        const y = -modelH / 2 - 30 * scale
        const extentX = modelW * .85
        const extentZ = modelD * 1.15
        const step = Math.max(28, Math.min(modelW, modelD) / 7)
        for (let x = -extentX; x <= extentX + 1; x += step) {
          drawLine3d([{ x, y, z:-extentZ }, { x, y, z:extentZ }], angleDeg, 'rgba(53,98,114,.07)', .7)
        }
        for (let z = -extentZ; z <= extentZ + 1; z += step) {
          drawLine3d([{ x:-extentX, y, z }, { x:extentX, y, z }], angleDeg, 'rgba(53,98,114,.07)', .7)
        }
      }

      function drawPreviewShadow(angleDeg, w, h, d, scale) {
        if (Math.abs(previewTiltDeg) < 1 || Math.abs(previewTiltDeg) > 89) return
        const ctx = previewContext
        const points = [{ x:-w / 2, z:-d / 2 }, { x:w / 2, z:-d / 2 }, { x:w / 2, z:d / 2 }, { x:-w / 2, z:d / 2 }]
          .map((point) => projectPoint({ ...point, y:-h / 2 - 30 * scale }, angleDeg))
        ctx.save()
        ctx.beginPath()
        points.forEach((point, index) => index ? ctx.lineTo(point.x, point.y) : ctx.moveTo(point.x, point.y))
        ctx.closePath()
        ctx.filter = 'blur(10px)'
        ctx.fillStyle = 'rgba(28,48,54,.17)'
        ctx.fill()
        ctx.filter = 'blur(3px)'
        ctx.fillStyle = 'rgba(28,48,54,.09)'
        ctx.fill()
        ctx.restore()
      }

      function drawPreviewAxes(angleDeg) {
        const ctx = previewContext
        const origin = { x:52, y:previewCanvas.height - 49 }
        const axes = [{ vector:{ x:25, y:0, z:0 }, name:'X', color:'#ad6148' }, { vector:{ x:0, y:25, z:0 }, name:'Y', color:'#568370' }, { vector:{ x:0, y:0, z:25 }, name:'Z', color:'#5b7e97' }]
        ctx.save()
        ctx.font = '10px Consolas, monospace'
        ctx.textAlign = 'center'
        ctx.textBaseline = 'middle'
        for (const axis of axes) {
          const vector = rotatePreviewPoint(axis.vector, angleDeg)
          if (Math.hypot(vector.x, vector.y) < 2) continue
          ctx.beginPath()
          ctx.moveTo(origin.x, origin.y)
          ctx.lineTo(origin.x + vector.x, origin.y - vector.y)
          ctx.strokeStyle = axis.color
          ctx.lineWidth = 1.3
          ctx.stroke()
          ctx.fillStyle = axis.color
          ctx.fillText(axis.name, origin.x + vector.x * 1.35, origin.y - vector.y * 1.35)
        }
        ctx.restore()
      }

      function drawDimension3d(start, end, label, angleDeg) {
        const a = projectPoint(start, angleDeg), b = projectPoint(end, angleDeg)
        const length = Math.hypot(b.x - a.x, b.y - a.y)
        if (length < 16) return
        const ux = (b.x - a.x) / length, uy = (b.y - a.y) / length
        const nx = -uy, ny = ux
        const ctx = previewContext
        ctx.save()
        ctx.strokeStyle = '#587680'
        ctx.lineWidth = .8
        ctx.beginPath()
        ctx.moveTo(a.x, a.y)
        ctx.lineTo(b.x, b.y)
        for (const point of [a, b]) {
          ctx.moveTo(point.x - (ux + nx) * 4, point.y - (uy + ny) * 4)
          ctx.lineTo(point.x + (ux + nx) * 4, point.y + (uy + ny) * 4)
        }
        ctx.stroke()
        const fontSize = Math.max(12, Math.min(28, 11 * previewCanvas.width / (previewCanvas.getBoundingClientRect().width || previewCanvas.width)))
        const x = (a.x + b.x) / 2 + nx * (fontSize + 2), y = (a.y + b.y) / 2 + ny * (fontSize + 2)
        ctx.font = fontSize + 'px Consolas, monospace'
        const textWidth = ctx.measureText(label).width
        ctx.fillStyle = '#f5f6ef'
        ctx.fillRect(x - textWidth / 2 - 4, y - fontSize / 2 - 2, textWidth + 8, fontSize + 4)
        ctx.fillStyle = '#264f5d'
        ctx.textAlign = 'center'
        ctx.textBaseline = 'middle'
        ctx.fillText(label, x, y)
        ctx.restore()
      }

      function drawPreview() {
        const ctx = previewContext
        const angle = Number(rotationInput.value)
        const zoom = Math.max(.7, Math.min(1.5, Number(zoomInput.value) / 100 || 1))
        rotationLabel.textContent = Math.round(angle) + '°'
        zoomLabel.textContent = Math.round(zoom * 100) + '%'
        ctx.clearRect(0, 0, previewCanvas.width, previewCanvas.height)
        const paper = ctx.createLinearGradient(0, 0, 0, previewCanvas.height)
        paper.addColorStop(0, '#f5f6f8')
        paper.addColorStop(1, '#e8ecf1')
        ctx.fillStyle = paper
        ctx.fillRect(0, 0, previewCanvas.width, previewCanvas.height)
        ctx.save()
        ctx.strokeStyle = 'rgba(53,98,114,.035)'
        ctx.lineWidth = 1
        ctx.beginPath()
        for (let x = 28; x < previewCanvas.width; x += 40) { ctx.moveTo(x, 0); ctx.lineTo(x, previewCanvas.height) }
        for (let y = 28; y < previewCanvas.height; y += 40) { ctx.moveTo(0, y); ctx.lineTo(previewCanvas.width, y) }
        ctx.stroke()
        ctx.strokeStyle = '#d4dbe5'
        ctx.strokeRect(12.5, 12.5, previewCanvas.width - 25, previewCanvas.height - 25)
        ctx.restore()
        const W = numberValue('cabinetWidth', 800)
        const H = numberValue('cabinetHeight', 1917)
        const D = numberValue('cabinetDepth', 550)
        const layout = currentDoorLayout(H)
        const columns = layout.columns.length
        const doorCount = layout.columns.reduce((sum, column) => sum + column.heights.length, 0)
        const corners = [-1, 1].flatMap((x) => [-1, 1].flatMap((y) => [-1, 1].map((z) => rotatePreviewPoint({ x:x * W / 2, y:y * H / 2, z:z * D / 2 }, angle))))
        const projectedW = Math.max(...corners.map((point) => point.x)) - Math.min(...corners.map((point) => point.x))
        const projectedH = Math.max(...corners.map((point) => point.y)) - Math.min(...corners.map((point) => point.y))
        const scale = Math.min(440 / projectedW, 398 / projectedH, Math.abs(previewTiltDeg) > 89 ? Infinity : 430 / H) * zoom
        const modelW = W * scale
        const modelH = H * scale
        const modelD = D * scale
        const faces = []
        const trimFaces = [], bandFaces = [], doorFaces = [], footFaces = []
        drawViewportGrid(angle, modelW, modelH, modelD, scale)
        drawPreviewShadow(angle, modelW, modelH, modelD, scale)
        const metal = { front:'#e1e6e3', right:'#cbd4d2', left:'#cbd4d2', top:'#f1f3ed', bottom:'#aebabb', back:'#ccd3d1' }
        const panel = { front:'#f8f8f2', right:'#c6cfcc', left:'#c6cfcc', top:'#fafbf6', bottom:'#abb8b6' }
        addBox(faces, 0, 0, 0, modelW, modelH, modelD, metal, '#778684')
        const frontZ = modelD / 2
        const gap = 7 * scale
        const doorW = Math.min(modelW / columns - gap * 2, numberValue('doorWidth', W / columns) * scale)
        // Fixed frame and bottom-up door positions follow the native 16029 contract.
        addBox(bandFaces, 0, modelH / 2 - 27.5 * scale, 0, modelW, 55 * scale, modelD + .8 * scale, metal, '#778684')
        addBox(bandFaces, 0, -modelH / 2 + 12.5 * scale, 0, modelW, 25 * scale, modelD + .8 * scale, metal, '#778684')
        trimFaces.push(...bandFaces.filter((face) => face.normal.y === 0))
        if (columns === 2) {
          addBox(trimFaces, 0, -15 * scale, frontZ + .4 * scale, 80 * scale, (H - 80) * scale, .8 * scale, metal, '#85928d')
          for (const side of [-1, 1]) addBox(trimFaces, side * (W / 2 - 10) * scale, -15 * scale, frontZ + .4 * scale, 20 * scale, (H - 80) * scale, .8 * scale, metal, '#85928d')
        }
        const footXs = [-(W / 2 - 35), W / 2 - 35]
        const footZs = [D / 2 - 74.5, -D / 2 + 49.5]
        for (const x of footXs) for (const z of footZs) addPreviewFoot(footFaces, x * scale, -modelH / 2, z * scale, scale)
        for (let col = 0; col < columns; col += 1) {
          let bottom = 32
          const cx = columns === 2 ? (col === 0 ? -1 : 1) * (W / 4 + 8.5) * scale : -modelW / 2 + (col + .5) * (modelW / columns)
          for (const height of layout.columns[col].heights) {
            if (!Number.isFinite(height) || height <= 0) continue
            const rowH = height * scale
            const cy = (-H / 2 + bottom + height / 2) * scale
            addBox(doorFaces, cx, cy, frontZ + .1 * scale, doorW + 2 * scale, rowH + 2 * scale, .2 * scale,
              { front:'#667773', right:'#667773', left:'#667773', top:'#667773', bottom:'#667773' }, null)
            addBox(doorFaces, cx, cy, frontZ + .8 * scale, doorW, rowH, .8 * scale, panel, '#657773')
            bottom += height + 7
          }
        }
        // Exterior panels always cover their support face, even when their mean depth is lower.
        for (const layer of [footFaces, faces, trimFaces, doorFaces]) {
          layer.map((face) => facePath(face, angle)).filter(Boolean).sort((a, b) => a.depth - b.depth).forEach(paintPreviewFace)
        }
        drawPreviewAxes(angle)
        const topView = Math.abs(previewTiltDeg) > 89
        const widthStart = { x:-modelW / 2, y:topView ? modelH / 2 : -modelH / 2 - 100 * scale, z:modelD / 2 + (topView ? 85 * scale : 0) }
        const widthEnd = { x:modelW / 2, y:widthStart.y, z:widthStart.z }
        const heightStart = { x:modelW / 2 + 125 * scale, y:-modelH / 2, z:modelD / 2 + (Math.abs(angle) > 89 ? 90 * scale : 0) }
        const heightEnd = { x:heightStart.x, y:modelH / 2, z:heightStart.z }
        const depthStart = { x:modelW / 2 + 85 * scale, y:topView ? modelH / 2 : -modelH / 2 - 75 * scale, z:-modelD / 2 }
        const depthEnd = { x:depthStart.x, y:depthStart.y, z:modelD / 2 }
        drawLine3d([{ x:-modelW / 2, y:-modelH / 2, z:modelD / 2 }, widthStart], angle, '#8ca19e', .8)
        drawLine3d([{ x:modelW / 2, y:-modelH / 2, z:modelD / 2 }, widthEnd], angle, '#8ca19e', .8)
        drawLine3d([{ x:modelW / 2, y:-modelH / 2, z:modelD / 2 }, heightStart], angle, '#8ca19e', .8)
        drawLine3d([{ x:modelW / 2, y:modelH / 2, z:modelD / 2 }, heightEnd], angle, '#8ca19e', .8)
        drawDimension3d(widthStart, widthEnd, 'W ' + W, angle)
        drawDimension3d(heightStart, heightEnd, 'H ' + H, angle)
        drawDimension3d(depthStart, depthEnd, 'D ' + D, angle)
        hudCabinetSize.textContent = W + 'W x ' + H + 'H x ' + D + 'D'
        hudDoorSize.textContent = fields.doorWidth.value + 'W x ' + fields.doorHeight.value + 'H'
        hudDoorLayout.textContent = '左 ' + layout.columns[0].heights.length + ' / 右 ' + layout.columns[1].heights.length + ' / 共 ' + doorCount + ' 门' + (layout.valid ? '' : ' · 布局待核对')
        hudHardware.textContent = fields.lockType.value + ' / ' + fields.hingeType.value
      }

      document.querySelectorAll('[data-example]').forEach((button) => {
        button.addEventListener('click', () => {
          promptInput.value = button.getAttribute('data-example') || ''
          applyPromptToFields()
        })
      })
      promptInput.addEventListener('input', applyPromptToFields)
      taskModeCards.forEach((card) => {
        card.addEventListener('click', () => {
          setTaskMode(card.getAttribute('data-task-mode'), true)
          updateGeneratedPrompt()
          drawPreview()
        })
      })
      Object.values(fields).forEach((input) => input.addEventListener('input', syncCustomerGenerationFields))
      doorColumns.forEach((column) => {
        const updateCount = () => {
          const count = Number(column.countInput.value)
          if (Number.isInteger(count) && count >= 1 && count <= 24 && count !== column.heightValues.length) {
            column.ratios = Array(count).fill('1')
            column.automatic = Array(count).fill(true)
            column.heightValues = doorHeightsForUnits(Number(fields.cabinetHeight.value), Array(count).fill(1)).map(formatDoorMm)
          }
          syncCustomerGenerationFields()
        }
        column.countInput.addEventListener('input', updateCount)
        column.countInput.addEventListener('change', updateCount)
        column.list.addEventListener('input', (event) => {
          const input = event.target
          if (!input.matches('[data-door-height]')) return
          const row = Number(input.dataset.row) - 1
          if (column.allocationMode === 'ratio') column.ratios[row] = input.value
          else {
            column.automatic[row] = false
            column.heightValues[row] = input.value
          }
          syncCustomerGenerationFields()
        })
        column.list.addEventListener('click', (event) => {
          const button = event.target.closest('[data-door-auto]')
          if (!button) return
          invalidateGenerationConfirmation()
          const row = Number(button.dataset.row) - 1
          column.automatic[row] = !column.automatic[row]
          syncCustomerGenerationFields()
          if (!column.automatic[row]) column.list.querySelector('#doorHeight' + column.side + (row + 1)).focus()
        })
        document.querySelectorAll('[data-column-mode="' + column.side + '"]').forEach((button) => button.addEventListener('click', () => {
          const mode = button.dataset.allocationMode
          if (mode === column.allocationMode) return
          invalidateGenerationConfirmation()
          if (mode === 'ratio') column.ratios = ratiosForDoorHeights(column.heightValues.map((value) => value.trim() ? Number(value) : NaN))
          else column.automatic = column.heightValues.map((value, index) => index === column.heightValues.length - 1)
          column.allocationMode = mode
          syncCustomerGenerationFields()
        }))
        document.querySelector('[data-column-equalize="' + column.side + '"]').addEventListener('click', () => {
          invalidateGenerationConfirmation()
          const count = Number(column.countInput.value)
          column.ratios = Array(count).fill('1')
          column.automatic = Array(count).fill(true)
          column.heightValues = doorHeightsForUnits(Number(fields.cabinetHeight.value), Array(count).fill(1)).map(formatDoorMm)
          syncCustomerGenerationFields()
        })
        document.querySelector('[data-column-fill-top="' + column.side + '"]').addEventListener('click', () => {
          invalidateGenerationConfirmation()
          column.allocationMode = 'height'
          column.automatic = column.heightValues.map((value, index) => index === column.heightValues.length - 1)
          syncCustomerGenerationFields()
        })
      })
      document.querySelectorAll('[data-layout-preset]').forEach((button) => {
        button.addEventListener('click', () => {
          invalidateGenerationConfirmation()
          const units = button.dataset.layoutPreset === 'mixed-six' ? [[6, 4, 2], [2, 4, 6]] : [[1, 1], [1, 1]]
          doorColumns.forEach((column, index) => {
            column.countInput.value = String(units[index].length)
            column.allocationMode = 'height'
            column.ratios = units[index].map(String)
            column.automatic = units[index].map((value, row) => row === units[index].length - 1)
            column.heightValues = doorHeightsForUnits(Number(fields.cabinetHeight.value), units[index]).map(formatDoorMm)
          })
          syncCustomerGenerationFields()
        })
      })
      let previousWidthInputMode = widthInputMode.value
      widthInputMode.addEventListener('change', () => {
        const value = Number(requestedWidthInput.value)
        if (Number.isFinite(value) && value > 0) {
          requestedWidthInput.value = previousWidthInputMode === 'cabinet_outer_width'
            ? String(Math.round(((value - 126) / 2) * 1000) / 1000)
            : String(Math.round((value * 2 + 126) * 1000) / 1000)
        }
        previousWidthInputMode = widthInputMode.value
        syncCustomerGenerationFields()
      })
      requestedWidthInput.addEventListener('input', syncCustomerGenerationFields)
      rotationInput.addEventListener('input', drawPreview)
      zoomInput.addEventListener('input', drawPreview)
      window.addEventListener('resize', drawPreview)
      document.querySelectorAll('[data-view]').forEach((button) => {
        button.addEventListener('click', () => {
          document.querySelectorAll('[data-view]').forEach((item) => item.classList.remove('view-active'))
          button.classList.add('view-active')
          const view = button.getAttribute('data-view')
          const viewState = {
            iso: { rotation:-28, tilt:18 },
            front: { rotation:0, tilt:0 },
            right: { rotation:90, tilt:0 },
            top: { rotation:0, tilt:90 },
          }[view] || { rotation:-28, tilt:18 }
          previewTiltDeg = viewState.tilt
          rotationInput.value = String(viewState.rotation)
          drawPreview()
        })
      })
      previewCanvas.addEventListener('pointerdown', (event) => {
        isDraggingPreview = true
        dragStartX = event.clientX
        dragStartRotation = Number(rotationInput.value)
        previewCanvas.setPointerCapture(event.pointerId)
      })
      previewCanvas.addEventListener('pointermove', (event) => {
        if (!isDraggingPreview) return
        rotationInput.value = String(Math.max(-90, Math.min(90, dragStartRotation + (event.clientX - dragStartX) / 4)))
        drawPreview()
      })
      previewCanvas.addEventListener('pointerup', () => { isDraggingPreview = false })
      previewCanvas.addEventListener('pointercancel', () => { isDraggingPreview = false })
      previewCanvas.addEventListener('wheel', (event) => {
        event.preventDefault()
        const nextZoom = Math.max(70, Math.min(150, Number(zoomInput.value) + (event.deltaY > 0 ? -5 : 5)))
        zoomInput.value = String(nextZoom)
        drawPreview()
      }, { passive:false })
      function buildGenerationPayload() {
        const payload = Object.fromEntries(new FormData(generationForm).entries())
        const expectsVerifiedNativeModel = generationForm.dataset.deliveryMode === 'verified_native_model'
        const layout = currentDoorLayout()
        if (!expectsVerifiedNativeModel && generationForm.dataset.parametricEligible === 'true' && layout.valid) {
          payload.parametricRequest = {
            cabinet: { widthMm:Number(payload.cabinetWidth), heightMm:Number(payload.cabinetHeight), depthMm:Number(payload.cabinetDepth) },
            columns: layout.columns.map((column) => ({ side:column.side, doors:column.doors })),
          }
        }
        return { payload, expectsVerifiedNativeModel }
      }

      function clearGenerationConfirmation({ focus = false } = {}) {
        pendingGenerationPayload = null
        pendingGenerationSignature = ''
        generationConfirm.hidden = true
        generationSubmitButton.hidden = false
        confirmGenerationSubmit.disabled = false
        if (focus) requestedWidthInput.focus()
      }

      function showGenerationConfirmation() {
        const prepared = buildGenerationPayload()
        const payload = prepared.payload
        const layout = currentDoorLayout()
        pendingGenerationPayload = payload
        pendingGenerationSignature = JSON.stringify(payload)
        generationStatus.className = ''
        generationStatus.textContent = ''
        generationConfirmSummary.replaceChildren(...[
          ['需求代号', payload.customerRequirementReference || '未填写'],
          ['柜体外形', payload.cabinetWidth + 'W × ' + payload.cabinetHeight + 'H × ' + payload.cabinetDepth + 'D'],
          ['门板布局', '左 ' + layout.columns[0].count + ' 门 / 右 ' + layout.columns[1].count + ' 门 / 共 ' + payload.doorCount + ' 门'],
          ...layout.columns.map((column) => [column.label + '门高', column.heights.map(formatDoorMm).join(' / ') + ' mm（自下向上）']),
          ['门板外宽', payload.doorWidth + ' mm'],
          ['任务路径', prepared.expectsVerifiedNativeModel ? '同规格基础模型完整性核对' : (payload.parametricRequest ? '原生参数化生成队列' : '原生建模需求登记')],
          ['下载条件', '仅原生交付检查通过后开放'],
        ].map(([term, value]) => {
          const item = document.createElement('div')
          const dt = document.createElement('dt')
          const dd = document.createElement('dd')
          dt.textContent = term
          dd.textContent = value
          item.append(dt, dd)
          return item
        }))
        generationConfirmBoundary.textContent = prepared.expectsVerifiedNativeModel
          ? '确认后仅核对已验证基础模型的完整性；通过后可下载继续深化。'
          : '确认后才会建立任务。排队、构建与检查阶段均不提供下载，也不会显示为 100% 完成。'
        generationConfirmState.className = 'task-status checking'
        generationConfirmState.textContent = '待确认'
        generationConfirm.hidden = false
        generationSubmitButton.hidden = true
        confirmGenerationSubmit.focus()
      }

      function invalidateGenerationConfirmation() {
        if (!pendingGenerationPayload || generationSubmissionInFlight) return
        clearGenerationConfirmation()
        generationStatus.className = ''
        generationStatus.textContent = '参数已修改，请重新核对后提交。'
        setGenerationProgress(0, '等待重新核对')
      }

      async function submitConfirmedGenerationRequest() {
        if (generationSubmissionInFlight || !pendingGenerationPayload) return
        const current = buildGenerationPayload().payload
        if (JSON.stringify(current) !== pendingGenerationSignature) {
          invalidateGenerationConfirmation()
          return
        }
        generationSubmissionInFlight = true
        generationSubmitButton.disabled = true
        confirmGenerationSubmit.disabled = true
        generationConfirmState.textContent = '已提交'
        generationStatus.className = ''
        const payload = pendingGenerationPayload
        const expectsVerifiedNativeModel = generationForm.dataset.deliveryMode === 'verified_native_model'
        try {
          if (expectsVerifiedNativeModel) {
            generationStatus.textContent = '正在核对基础模型文件完整性...'
            setGenerationProgress(60, '正在核对结构工程辅助基础模型')
          } else {
            generationStatus.textContent = '正在保存参数并建立原生新任务...'
            setGenerationProgress(20, '正在建立原生新任务')
          }
          const response = await fetch('/generation-request', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload),
          })
          const result = await response.json()
          if (!response.ok) throw new Error(result.error || '保存失败')
          generationStatus.className = 'ok'
          const directUrl = result.directUrl || result.downloadUrl
          const nativeModelReady = result.engineeringAssistanceReady === true && Boolean(directUrl)
          if (nativeModelReady) {
            setGenerationProgress(100, '结构工程辅助基础模型已准备好')
            const readyText = document.createElement('span')
            readyText.textContent = '已找到同规格' + (result.generationTemplateLabel || '原生基础模型') + '。'
            const downloadLink = document.createElement('a')
            downloadLink.className = 'button'
            downloadLink.href = directUrl
            downloadLink.textContent = '下载结构工程辅助基础模型'
            generationStatus.replaceChildren(readyText, document.createTextNode(' '), downloadLink)
          } else if (result.status === 'native_task_created' && result.taskType === 'native_solidworks_build_task') {
            setGenerationProgress(20, '任务已排队，等待原生模型构建与检查')
            const taskText = document.createElement('span')
            taskText.textContent = (result.deduplicated ? '已复用相同的原生任务：' : '原生新任务已建立：') + result.requestId + '。完成前不可下载。'
            generationStatus.replaceChildren(taskText)
          } else {
            setGenerationProgress(0, '任务状态异常')
            generationStatus.textContent = result.error || '原生任务未能建立。'
          }
          if (generationRequestList) await refreshGenerationRequestList()
          if (result.requestId) activateWorkspace('my-models', true)
          clearGenerationConfirmation()
          syncCustomerGenerationFields()
        } catch (error) {
          generationStatus.className = 'alert'
          generationStatus.textContent = error instanceof Error ? error.message : '保存失败'
          generationConfirmState.textContent = '待重试'
          setGenerationProgress(0, '提交失败')
        } finally {
          generationSubmissionInFlight = false
          confirmGenerationSubmit.disabled = false
          syncCustomerGenerationFields()
        }
      }

      generationForm.addEventListener('input', invalidateGenerationConfirmation)
      generationForm.addEventListener('change', invalidateGenerationConfirmation)
      generationForm.addEventListener('submit', (event) => {
        event.preventDefault()
        if (!generationSubmissionInFlight && !generationSubmitButton.disabled && generationForm.checkValidity()) showGenerationConfirmation()
      })
      returnToSpecButton.addEventListener('click', () => clearGenerationConfirmation({ focus:true }))
      confirmGenerationSubmit.addEventListener('click', submitConfirmedGenerationRequest)
      syncCustomerGenerationFields()

      const form = document.getElementById('feedbackForm')
      const statusBox = document.getElementById('feedbackStatus')
      const feedbackSubmitButton = document.getElementById('feedbackSubmitButton')
      const efficiencyForm = document.getElementById('efficiencyForm')
      const efficiencyStatus = document.getElementById('efficiencyStatus')
      const efficiencySubmitButton = document.getElementById('efficiencySubmitButton')
      const feedbackMaxAttachmentCount = ${MAX_ATTACHMENT_COUNT}
      const feedbackMaxAttachmentBytes = ${MAX_ATTACHMENT_BYTES}
      const feedbackMaxAttachmentTotalBytes = ${MAX_ATTACHMENT_TOTAL_BYTES}
      function fileToEntry(file) {
        return new Promise((resolve, reject) => {
          const reader = new FileReader()
          reader.onload = () => resolve({ name: file.name, type: file.type || 'application/octet-stream', dataUrl: reader.result })
          reader.onerror = reject
          reader.readAsDataURL(file)
        })
      }
      function blobToEntry(blob, name) {
        return new Promise((resolve, reject) => {
          const reader = new FileReader()
          reader.onload = () => resolve({ name, type: blob.type || 'application/octet-stream', dataUrl: reader.result })
          reader.onerror = reject
          reader.readAsDataURL(blob)
        })
      }
      const clarificationReviewerName = ${JSON.stringify(username)}
      const clarificationReviewTarget = ${JSON.stringify(CURRENT_REVIEW_ASSET_ID)}
      for (const clarificationForm of document.querySelectorAll('.clarification-response')) {
        clarificationForm.addEventListener('submit', async (event) => {
          event.preventDefault()
          const status = clarificationForm.querySelector('.clarification-status')
          const submitButton = clarificationForm.querySelector('button[type="submit"]')
          const selected = clarificationForm.querySelector('input[name="clarificationOption"]:checked')
          const detail = String(new FormData(clarificationForm).get('clarificationDetail') || '').trim()
          if (!selected || !detail) {
            status.className = 'clarification-status alert'
            status.textContent = '请选择一个方案，并填写最终位置或尺寸。'
            return
          }
          submitButton.disabled = true
          status.className = 'clarification-status muted'
          status.textContent = '正在附上原截图并提交...'
          try {
            const clarificationId = clarificationForm.dataset.clarificationId
            const selectedLabel = selected.dataset.label || selected.value
            const imageCount = Number(clarificationForm.dataset.imageCount || 0)
            const imageEntries = await Promise.all(Array.from({ length: imageCount }, async (_, index) => {
              const imageResponse = await fetch('/clarification/' + encodeURIComponent(clarificationId) + '/image/' + (index + 1))
              if (!imageResponse.ok) throw new Error('读取问题原截图失败')
              return blobToEntry(await imageResponse.blob(), clarificationId + '-reference-' + (index + 1) + '.png')
            }))
            const response = await fetch('/feedback', {
              method: 'POST',
              headers: { 'Content-Type': 'application/json' },
              body: JSON.stringify({
                reviewerName: clarificationReviewerName,
                discipline: '结构确认答复',
                reviewTarget: clarificationReviewTarget,
                issueCategory: 'other_structure_issue',
                severity: 'P1',
                decision: 'needs_changes',
                componentName: clarificationForm.dataset.componentName,
                modelLocation: clarificationForm.dataset.questionTitle,
                currentProblem: '【' + clarificationId + ' 工程确认答复】' + selectedLabel + '\\n补充说明：' + detail,
                expectedResult: '请按本答复处理；关联原反馈：' + clarificationForm.dataset.sourceFeedbackIds,
                referenceBasis: '待确认问题 ' + clarificationId,
                acceptanceCriteria: clarificationForm.dataset.questionPrompt,
                attachments: imageEntries,
                clarificationId,
                clarificationOption: selected.value,
              }),
            })
            const result = await response.json().catch(() => ({ error: '服务器返回了不可读取的结果' }))
            if (!response.ok) throw new Error(result.error || '提交失败')
            status.className = 'clarification-status ok'
            status.textContent = '已提交：' + result.feedbackId + '。原截图已自动保存。'
            setTimeout(() => location.reload(), 900)
          } catch (error) {
            status.className = 'clarification-status alert'
            status.textContent = error instanceof Error ? error.message : '提交失败'
          } finally {
            submitButton.disabled = false
          }
        })
      }
      efficiencyForm.addEventListener('submit', async (event) => {
        event.preventDefault()
        efficiencyStatus.className = ''
        efficiencyStatus.textContent = '正在校验并提交效率记录...'
        const data = new FormData(efficiencyForm)
        const files = Array.from(efficiencyForm.elements.attachments.files || [])
        const totalBytes = files.reduce((total, file) => total + file.size, 0)
        if (files.length > feedbackMaxAttachmentCount) {
          efficiencyStatus.className = 'alert'
          efficiencyStatus.textContent = '可选附件最多 ' + feedbackMaxAttachmentCount + ' 个。'
          return
        }
        if (files.some((file) => file.size > feedbackMaxAttachmentBytes) || totalBytes > feedbackMaxAttachmentTotalBytes) {
          efficiencyStatus.className = 'alert'
          efficiencyStatus.textContent = '附件大小超过限制，请压缩图片或不上传附件。'
          return
        }
        efficiencySubmitButton.disabled = true
        try {
          const attachments = await Promise.all(files.map(fileToEntry))
          const manualModificationItems = String(data.get('manualModificationItems') || '')
            .split(/\\r?\\n/)
            .map((value) => value.trim())
            .filter(Boolean)
          const response = await fetch('/feedback', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
              recordType: 'efficiency_validation',
              reviewerName: data.get('reviewerName'),
              discipline: '结构设计效率验证',
              reviewTarget: data.get('reviewTarget'),
              generationRequestId: data.get('generationRequestId'),
              taskKind: data.get('taskKind'),
              taskReference: data.get('taskReference'),
              taskScope: data.get('taskScope'),
              traditionalEstimatedMinutes: data.get('traditionalEstimatedMinutes'),
              generatedModelActualMinutes: data.get('generatedModelActualMinutes'),
              directReusePercent: data.get('directReusePercent'),
              manualModificationItems,
              deliverables: {
                rebuildableModel: data.get('rebuildableModel'),
                engineeringDrawing: data.get('engineeringDrawing'),
                bom: data.get('bom'),
              },
              primaryProblemCategory: data.get('primaryProblemCategory'),
              problemNotes: data.get('problemNotes'),
              attachments,
            }),
          })
          const result = await response.json().catch(() => ({ error: '服务器返回了不可读取的结果' }))
          if (!response.ok) throw new Error(result.error || '提交失败')
          efficiencyStatus.className = 'ok'
          efficiencyStatus.textContent = '已提交：' + result.feedbackId + '。'
          setTimeout(() => location.reload(), 900)
        } catch (error) {
          efficiencyStatus.className = 'alert'
          efficiencyStatus.textContent = error instanceof Error ? error.message : '提交失败'
        } finally {
          efficiencySubmitButton.disabled = false
        }
      })
      form.addEventListener('submit', async (event) => {
        event.preventDefault()
        statusBox.className = ''
        statusBox.textContent = '正在校验并上传反馈...'
        const data = new FormData(form)
        const files = Array.from(form.elements.attachments.files || [])
        const totalBytes = files.reduce((total, file) => total + file.size, 0)
        if (!files.length || files.length > feedbackMaxAttachmentCount) {
          statusBox.className = 'alert'
          statusBox.textContent = '请上传 1-' + feedbackMaxAttachmentCount + ' 个标注截图或 PDF。'
          return
        }
        if (files.some((file) => file.size > feedbackMaxAttachmentBytes) || totalBytes > feedbackMaxAttachmentTotalBytes) {
          statusBox.className = 'alert'
          statusBox.textContent = '附件大小超过限制，请压缩图片或拆分为多个问题。'
          return
        }

        feedbackSubmitButton.disabled = true
        try {
          const attachments = await Promise.all(files.map(fileToEntry))
          const componentName = String(data.get('componentName') || '').trim()
          const response = await fetch('/feedback', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
              reviewerName: data.get('reviewerName'),
              discipline: data.get('discipline'),
              reviewTarget: data.get('reviewTarget'),
              generationRequestId: data.get('generationRequestId'),
              issueCategory: data.get('issueCategory'),
              severity: data.get('severity'),
              decision: data.get('decision'),
              componentName,
              modelLocation: data.get('modelLocation') || componentName,
              currentProblem: data.get('currentProblem'),
              expectedResult: data.get('expectedResult') || '请按问题描述和标注截图修正',
              keyDimensionTolerance: data.get('keyDimensionTolerance'),
              referenceBasis: data.get('referenceBasis'),
              acceptanceCriteria: data.get('acceptanceCriteria') || '修改后由提交人复核确认',
              attachments,
            }),
          })
          const result = await response.json().catch(() => ({ error: '服务器返回了不可读取的结果' }))
          if (!response.ok) throw new Error(result.error || '提交失败')
          statusBox.className = 'ok'
          statusBox.textContent = '已提交：' + result.feedbackId + '，附件 ' + result.savedFiles + ' 个。'
          setTimeout(() => location.reload(), 900)
        } catch (error) {
          statusBox.className = 'alert'
          statusBox.textContent = error instanceof Error ? error.message : '提交失败'
        } finally {
          feedbackSubmitButton.disabled = false
        }
      })
    </script>
  `, username)
}

async function parseForm(request) {
  const body = await readBody(request, 128 * 1024)
  return new URLSearchParams(body.toString('utf8'))
}

function readInviteCode() {
  try {
    return readFileSync(INVITE_CODE_PATH, 'utf8').trim()
  } catch {
    return ''
  }
}

function safeSlug(value) {
  return String(value || 'user').replace(/[^a-zA-Z0-9_-]/g, '_').slice(0, 48)
}

function badRequest(message) {
  const error = new Error(message)
  error.statusCode = 400
  return error
}

function textField(payload, name, { required = false, maxLength = 1000, fallback = '' } = {}) {
  const value = String(payload[name] ?? fallback).trim()
  if (required && !value) throw badRequest(`${name} is required`)
  if (value.length > maxLength) throw badRequest(`${name} is too long (max ${maxLength})`)
  return value
}

function numberField(payload, name, { required = false, min = -Infinity, max = Infinity, integer = false } = {}) {
  const raw = payload[name]
  if (raw === undefined || raw === null || String(raw).trim() === '') {
    if (required) throw badRequest(`${name} is required`)
    return null
  }
  const value = Number(raw)
  if (!Number.isFinite(value) || (integer && !Number.isInteger(value)) || value < min || value > max) {
    throw badRequest(`${name} must be a number between ${min} and ${max}`)
  }
  return value
}

function enumField(payload, name, labels, { required = false, fallback = '' } = {}) {
  const value = String(payload[name] ?? fallback).trim()
  if (required && !value) throw badRequest(`${name} is required`)
  if (value && !labels[value]) throw badRequest(`invalid ${name}`)
  return value
}

function textListField(payload, name, { required = false, maxItems = 30, maxItemLength = 300 } = {}) {
  const values = Array.isArray(payload[name])
    ? payload[name].map((value) => String(value || '').trim()).filter(Boolean)
    : []
  if (required && !values.length) throw badRequest(`${name} must contain at least one item`)
  if (values.length > maxItems) throw badRequest(`${name} contains too many items`)
  if (values.some((value) => value.length > maxItemLength)) throw badRequest(`${name} contains an item that is too long`)
  return values
}

function dataUrlToBuffer(dataUrl) {
  const match = String(dataUrl || '').match(/^data:([^;,]+)?;base64,(.*)$/)
  if (!match) throw badRequest('invalid attachment data')
  const encoded = match[2].replace(/\s/g, '')
  if (!encoded || encoded.length % 4 !== 0 || !/^[a-zA-Z0-9+/]*={0,2}$/.test(encoded)) {
    throw badRequest('invalid attachment base64')
  }
  return Buffer.from(encoded, 'base64')
}

function detectAttachmentKind(buffer) {
  if (buffer.length >= 8 && buffer.subarray(0, 8).equals(Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]))) {
    return { extension: '.png', extensions: new Set(['.png']), mime: 'image/png' }
  }
  if (buffer.length >= 3 && buffer[0] === 0xff && buffer[1] === 0xd8 && buffer[2] === 0xff) {
    return { extension: '.jpg', extensions: new Set(['.jpg', '.jpeg']), mime: 'image/jpeg' }
  }
  if (buffer.length >= 12 && buffer.subarray(0, 4).toString('ascii') === 'RIFF' && buffer.subarray(8, 12).toString('ascii') === 'WEBP') {
    return { extension: '.webp', extensions: new Set(['.webp']), mime: 'image/webp' }
  }
  if (buffer.length >= 5 && buffer.subarray(0, 5).toString('ascii') === '%PDF-') {
    return { extension: '.pdf', extensions: new Set(['.pdf']), mime: 'application/pdf' }
  }
  throw badRequest('attachments must be PNG, JPG, WEBP, or PDF files')
}

function prepareFeedbackAttachments(rawAttachments, { required = true } = {}) {
  const attachments = Array.isArray(rawAttachments) ? rawAttachments : []
  if ((required && attachments.length < 1) || attachments.length > MAX_ATTACHMENT_COUNT) {
    throw badRequest(required
      ? `attachments must contain 1-${MAX_ATTACHMENT_COUNT} files`
      : `attachments must contain no more than ${MAX_ATTACHMENT_COUNT} files`)
  }
  let totalBytes = 0
  return attachments.map((attachment, index) => {
    const originalName = String(attachment?.name || `attachment-${index + 1}`).trim().slice(0, 240)
    const buffer = dataUrlToBuffer(attachment?.dataUrl)
    if (buffer.length > MAX_ATTACHMENT_BYTES) throw badRequest(`attachment ${index + 1} is too large`)
    totalBytes += buffer.length
    if (totalBytes > MAX_ATTACHMENT_TOTAL_BYTES) throw badRequest('total attachment size is too large')
    const kind = detectAttachmentKind(buffer)
    const declaredExtension = extname(originalName).toLowerCase()
    if (declaredExtension && !kind.extensions.has(declaredExtension)) {
      throw badRequest(`attachment ${index + 1} extension does not match its content`)
    }
    return {
      originalName: originalName || `attachment-${index + 1}${kind.extension}`,
      savedAs: `attachment-${String(index + 1).padStart(2, '0')}${kind.extension}`,
      mime: kind.mime,
      bytes: buffer.length,
      buffer,
    }
  })
}

async function submitFeedback(username, payload) {
  const recordType = enumField(payload, 'recordType', RECORD_TYPE_LABELS, {
    required: true,
    fallback: 'structure_issue',
  })
  const generationRequestId = textField(payload, 'generationRequestId', { maxLength: 120 })
  let generationRequest = null
  let reviewTargetType = 'asset'
  let reviewTarget = String(payload.reviewTarget || '').trim()
  let reviewTargetLabel = ''
  if (generationRequestId) {
    try {
      generationRequest = await findGenerationRequestForUser(generationRequestId, username)
    } catch (error) {
      if (error?.statusCode === 404) throw badRequest('invalid generationRequestId')
      throw error
    }
    if (recordType === 'efficiency_validation' && !isEngineeringContinuationEligible(generationRequest)) {
      throw badRequest('efficiency validation requires an engineering-continuation-eligible generated model')
    }
    reviewTargetType = 'generation_request'
    reviewTarget = `generation-request:${generationRequest.id}`
    reviewTargetLabel = generationRequestTargetLabel(generationRequest)
  } else {
    if (recordType === 'efficiency_validation') {
      throw badRequest('generationRequestId is required for efficiency validation')
    }
    const asset = byId.get(reviewTarget)
    if (!asset) throw badRequest('invalid review target')
    reviewTargetLabel = asset.title
  }
  const reviewerName = textField(payload, 'reviewerName', { required: true, maxLength: 80, fallback: username })
  const discipline = textField(payload, 'discipline', { maxLength: 80 })
    || (recordType === 'efficiency_validation' ? '结构设计效率验证' : '')
  let structureIssue = null
  let efficiencyValidation = null

  if (recordType === 'efficiency_validation') {
    const taskKind = enumField(payload, 'taskKind', TASK_KIND_LABELS, { required: true })
    const taskReference = textField(payload, 'taskReference', { required: true, maxLength: 200 })
    const taskScope = textField(payload, 'taskScope', { required: true, maxLength: 2000 })
    const traditionalEstimatedMinutes = numberField(payload, 'traditionalEstimatedMinutes', {
      required: true,
      min: 1,
      max: 43200,
      integer: true,
    })
    const generatedModelActualMinutes = numberField({
      ...payload,
      generatedModelActualMinutes: payload.generatedModelActualMinutes ?? payload.v35ActualMinutes,
    }, 'generatedModelActualMinutes', {
      required: true,
      min: 1,
      max: 43200,
      integer: true,
    })
    const directReusePercent = numberField(payload, 'directReusePercent', {
      required: true,
      min: 0,
      max: 100,
      integer: true,
    })
    const manualModificationItems = textListField(payload, 'manualModificationItems', {
      required: directReusePercent < 100,
      maxItems: 30,
      maxItemLength: 300,
    })
    const rawDeliverables = payload.deliverables && typeof payload.deliverables === 'object' && !Array.isArray(payload.deliverables)
      ? payload.deliverables
      : {}
    const rebuildableModel = enumField(rawDeliverables, 'rebuildableModel', DELIVERABLE_STATUS_LABELS, { required: true })
    if (rebuildableModel === 'not_required') throw badRequest('rebuildableModel cannot be not_required')
    const engineeringDrawing = enumField(rawDeliverables, 'engineeringDrawing', DELIVERABLE_STATUS_LABELS, { required: true })
    const bom = enumField(rawDeliverables, 'bom', DELIVERABLE_STATUS_LABELS, { required: true })
    const primaryProblemCategory = enumField(
      payload,
      'primaryProblemCategory',
      EFFICIENCY_PROBLEM_CATEGORY_LABELS,
      { required: true },
    )
    const problemNotes = textField(payload, 'problemNotes', {
      required: primaryProblemCategory === 'other',
      maxLength: 2000,
    })
    const measurementComplete = rebuildableModel === 'completed'
      && [engineeringDrawing, bom].every((value) => value === 'completed' || value === 'not_required')
    const timeSavingsMinutes = measurementComplete ? traditionalEstimatedMinutes - generatedModelActualMinutes : null
    const timeSavingsPercent = measurementComplete
      ? Math.round((timeSavingsMinutes / traditionalEstimatedMinutes) * 1000) / 10
      : null
    efficiencyValidation = {
      taskKind,
      taskKindLabel: TASK_KIND_LABELS[taskKind],
      taskReference,
      taskScope,
      traditionalEstimatedMinutes,
      generatedModelActualMinutes,
      directReusePercent,
      manualModificationItems,
      deliverables: { rebuildableModel, engineeringDrawing, bom },
      deliverableLabels: {
        rebuildableModel: DELIVERABLE_STATUS_LABELS[rebuildableModel],
        engineeringDrawing: DELIVERABLE_STATUS_LABELS[engineeringDrawing],
        bom: DELIVERABLE_STATUS_LABELS[bom],
      },
      primaryProblemCategory,
      primaryProblemCategoryLabel: EFFICIENCY_PROBLEM_CATEGORY_LABELS[primaryProblemCategory],
      problemNotes,
      measurementComplete,
      timeSavingsMinutes,
      timeSavingsPercent,
    }
  } else {
    const decision = String(payload.decision || '').trim()
    if (!DECISION_LABELS[decision]) throw badRequest('invalid decision')
    const issueCategory = String(payload.issueCategory || '').trim()
    if (!ISSUE_CATEGORY_LABELS[issueCategory]) throw badRequest('invalid issue category')
    const severity = String(payload.severity || '').trim().toUpperCase()
    if (!SEVERITY_LABELS[severity]) throw badRequest('invalid severity')
    const clarificationId = textField(payload, 'clarificationId', { maxLength: 80 })
    const clarificationOption = textField(payload, 'clarificationOption', { maxLength: 120 })
    if (clarificationId) {
      const question = readClarificationQuestions().find((item) => item.id === clarificationId)
      if (!question) throw badRequest('invalid clarification id')
      const validOptions = new Set((question.options || []).map((item) => String(item.value || '')))
      if (!clarificationOption || !validOptions.has(clarificationOption)) {
        throw badRequest('invalid clarification option')
      }
    } else if (clarificationOption) {
      throw badRequest('clarification id is required')
    }
    const currentProblem = textField(payload, 'currentProblem', { required: true, maxLength: 2000, fallback: payload.summary })
    structureIssue = {
      decision,
      decisionLabel: DECISION_LABELS[decision],
      issueCategory,
      issueCategoryLabel: ISSUE_CATEGORY_LABELS[issueCategory],
      severity,
      severityLabel: SEVERITY_LABELS[severity],
      componentName: textField(payload, 'componentName', { required: true, maxLength: 200 }),
      modelLocation: textField(payload, 'modelLocation', { required: true, maxLength: 400 }),
      currentProblem,
      expectedResult: textField(payload, 'expectedResult', { required: true, maxLength: 2000 }),
      keyDimensionTolerance: textField(payload, 'keyDimensionTolerance', { maxLength: 1000 }),
      referenceBasis: textField(payload, 'referenceBasis', { maxLength: 1000 }),
      acceptanceCriteria: textField(payload, 'acceptanceCriteria', { required: true, maxLength: 1500 }),
      clarificationId,
      clarificationOption,
      summary: currentProblem,
    }
  }
  const preparedAttachments = prepareFeedbackAttachments(payload.attachments, {
    required: recordType === 'structure_issue',
  })

  const submittedAt = new Date().toISOString()
  const stamp = submittedAt.replace(/[-:.]/g, '').slice(0, 15)
  const feedbackId = `${FEEDBACK_ID_PREFIX}${stamp}-${randomBytes(3).toString('hex').toUpperCase()}`
  const folderName = `${feedbackId}-${safeSlug(username)}`
  const folderPath = resolve(FEEDBACK_DIR, folderName)
  mkdirSync(folderPath, { recursive: true })

  const attachments = preparedAttachments.map(({ buffer, originalName, ...attachment }) => {
    writeFileSync(resolve(folderPath, attachment.savedAs), buffer)
    return { name: originalName, ...attachment }
  })

  const feedback = {
    schema: recordType === 'efficiency_validation'
      ? 'winnsen.review.efficiency_validation.v1'
      : 'winnsen.review.structure_issue.v1',
    recordType,
    recordTypeLabel: RECORD_TYPE_LABELS[recordType],
    id: feedbackId,
    submittedAt,
    username,
    reviewerName,
    discipline,
    ...(structureIssue || {}),
    ...(efficiencyValidation ? { efficiencyValidation } : {}),
    summary: efficiencyValidation?.taskScope || structureIssue?.summary || '',
    reviewRound,
    reviewRoundId: reviewRound.id,
    reviewTarget,
    reviewTargetType,
    reviewTargetLabel,
    ...(generationRequest ? {
      generationRequestId: generationRequest.id,
      generationRequestSnapshot: generationRequestSnapshot(generationRequest),
    } : {}),
    attachments,
    feedbackIsEngineeringSignoff: false,
  }
  writeJson(resolve(folderPath, 'feedback.json'), feedback)

  const index = readFeedbackIndex()
  const { reviewRound: ignoredReviewRound, attachments: ignoredAttachments, ...summaryFields } = feedback
  const summary = {
    ...summaryFields,
    folderName,
    attachmentCount: attachments.length,
  }
  index.feedback = [summary, ...index.feedback.filter((item) => item.id !== summary.id)].slice(0, 300)
  writeJson(FEEDBACK_INDEX_PATH, index)
  return feedback
}

function localAddresses() {
  const addresses = new Set(['127.0.0.1'])
  for (const infos of Object.values(networkInterfaces())) {
    for (const info of infos || []) {
      if (info.family === 'IPv4' && !info.internal) addresses.add(info.address)
    }
  }
  return [...addresses]
}

ensureDirs()
syncAssetsToSharedStorage(STARTUP_PRIORITY_ASSET_IDS)

const server = createServer(async (request, response) => {
  try {
    const url = new URL(request.url || '/', `http://${request.headers.host || '127.0.0.1'}`)
    if ((request.method === 'GET' || request.method === 'HEAD') && url.pathname === '/favicon.ico') {
      response.writeHead(204, { 'Cache-Control': 'public, max-age=86400' })
      response.end()
      return
    }
    if (request.method === 'GET' && url.pathname === '/brand/winnsen-logo.jpg') {
      if (!existsSync(BRAND_LOGO_PATH)) {
        response.writeHead(404)
        response.end()
        return
      }
      response.writeHead(200, { 'Content-Type': 'image/jpeg', 'Cache-Control': 'public, max-age=3600' })
      createReadStream(BRAND_LOGO_PATH).pipe(response)
      return
    }
    if (request.method === 'GET' && url.pathname === '/brand/winnsen-mark.png') {
      if (!existsSync(BRAND_MARK_PATH)) {
        response.writeHead(404)
        response.end()
        return
      }
      response.writeHead(200, { 'Content-Type': 'image/png', 'Cache-Control': 'public, max-age=3600' })
      createReadStream(BRAND_MARK_PATH).pipe(response)
      return
    }
    if (request.method === 'GET' && url.pathname === '/status.json') {
      sendJson(response, 200, {
        status: 'ok',
        auth: 'enabled',
        reviewRound: reviewRound.id,
        sharedStorageReady: true,
        assets: orderedAssets().map(publicAssetInfo),
      })
      return
    }
    if (request.method === 'GET' && url.pathname === '/local-audit/feedback-summary.json') {
      if (!isLoopbackRequest(request)) {
        sendJson(response, 404, { error: 'not found' })
        return
      }
      sendJson(response, 200, localFeedbackAudit())
      return
    }
    const localFeedbackAttachmentMatch = url.pathname.match(
      /^\/local-audit\/feedback\/([a-zA-Z0-9_.-]+)\/attachment\/([1-9][0-9]*)$/,
    )
    if (request.method === 'GET' && localFeedbackAttachmentMatch) {
      if (!isLoopbackRequest(request)) {
        sendJson(response, 404, { error: 'not found' })
        return
      }
      const attachmentInfo = feedbackAttachmentInfo(
        localFeedbackAttachmentMatch[1],
        Number(localFeedbackAttachmentMatch[2]) - 1,
        false,
      )
      if (!attachmentInfo) {
        sendJson(response, 404, { error: 'feedback attachment not found' })
        return
      }
      const stat = statSync(attachmentInfo.filePath)
      response.writeHead(200, {
        'Content-Type': String(
          attachmentInfo.attachment.mime || contentTypeForPath(attachmentInfo.filePath),
        ),
        'Content-Length': stat.size,
        'Content-Disposition': `inline; filename="${safeResponseFilename(
          attachmentInfo.attachment.savedAs,
          'attachment',
        )}"`,
        'Cache-Control': 'private, no-store',
      })
      createReadStream(attachmentInfo.filePath).pipe(response)
      return
    }
    if (request.method === 'GET' && url.pathname === '/login') {
      sendHtml(response, 200, renderAuthPage('', 'login'))
      return
    }
    if (request.method === 'GET' && url.pathname === '/register') {
      sendHtml(response, 200, renderAuthPage('', 'register'))
      return
    }
    if (request.method === 'POST' && url.pathname === '/login') {
      const params = await parseForm(request)
      const username = normalizeUsername(params.get('username'))
      const password = params.get('password') || ''
      const user = readUsers().users.find((item) => item.username === username)
      if (!user || !verifyPassword(user, password)) {
        sendHtml(response, 401, renderAuthPage('账号或密码不正确。', 'login'))
        return
      }
      createSession(response, username)
      redirect(response, '/')
      return
    }
    if (request.method === 'POST' && url.pathname === '/register') {
      const params = await parseForm(request)
      const username = normalizeUsername(params.get('username'))
      const password = String(params.get('password') || '')
      const inviteCode = String(params.get('inviteCode') || '').trim()
      if (!username || password.length < 6) {
        sendHtml(response, 400, renderAuthPage('账号不能为空，密码至少 6 位。', 'register'))
        return
      }
      if (readInviteCode() && inviteCode !== readInviteCode()) {
        sendHtml(response, 403, renderAuthPage('邀请码不正确。', 'register'))
        return
      }
      const db = readUsers()
      if (db.users.some((item) => item.username === username)) {
        sendHtml(response, 409, renderAuthPage('账号已存在，请直接登录。', 'register'))
        return
      }
      db.users.push(createUser(username, password))
      saveUsers(db)
      createSession(response, username)
      redirect(response, '/')
      return
    }
    if (request.method === 'GET' && url.pathname === '/logout') {
      const token = parseCookies(request)[SESSION_COOKIE]
      if (token) sessions.delete(token)
      response.setHeader('Set-Cookie', `${SESSION_COOKIE}=; HttpOnly; Path=/; Max-Age=0; SameSite=Lax`)
      redirect(response, '/login')
      return
    }

    const username = currentUser(request)
    if (!username) {
      if (request.method === 'GET') redirect(response, '/login')
      else sendJson(response, 401, { error: 'login required' })
      return
    }

    if (request.method === 'GET' && url.pathname === '/') {
      sendHtml(response, 200, await renderPage(request))
      return
    }
    if (request.method === 'GET' && url.pathname === '/assets.json') {
      sendJson(response, 200, { reviewRound, assets: orderedAssets().map(publicAssetInfo).map((asset) => ({ ...asset, downloadUrl: `/download/${asset.id}` })) })
      return
    }
    if (request.method === 'GET' && url.pathname === '/feedback.json') {
      sendJson(response, 200, { feedback: currentRoundFeedback(username) })
      return
    }
    const clarificationImageMatch = url.pathname.match(/^\/clarification\/([a-zA-Z0-9_.-]+)\/image\/([1-9][0-9]*)$/)
    if (request.method === 'GET' && clarificationImageMatch) {
      const imageInfo = clarificationImageInfo(clarificationImageMatch[1], Number(clarificationImageMatch[2]) - 1)
      if (!imageInfo) {
        sendJson(response, 404, { error: 'clarification image not found' })
        return
      }
      const stat = statSync(imageInfo.filePath)
      response.writeHead(200, {
        'Content-Type': contentTypeForPath(imageInfo.filePath),
        'Content-Length': stat.size,
        'Content-Disposition': inlineContentDisposition(basename(imageInfo.filePath), 'clarification-image'),
        'Cache-Control': 'private, no-store',
      })
      createReadStream(imageInfo.filePath).pipe(response)
      return
    }
    const feedbackAttachmentMatch = url.pathname.match(/^\/feedback\/([a-zA-Z0-9_.-]+)\/attachment\/([1-9][0-9]*)$/)
    if (request.method === 'GET' && feedbackAttachmentMatch) {
      const feedbackId = feedbackAttachmentMatch[1]
      const attachmentIndex = Number(feedbackAttachmentMatch[2]) - 1
      const attachmentInfo = feedbackAttachmentInfo(feedbackId, attachmentIndex)
      if (!attachmentInfo) {
        sendJson(response, 404, { error: 'feedback attachment not found' })
        return
      }
      const stat = statSync(attachmentInfo.filePath)
      response.writeHead(200, {
        'Content-Type': String(
          attachmentInfo.attachment.mime || contentTypeForPath(attachmentInfo.filePath),
        ),
        'Content-Length': stat.size,
        'Content-Disposition': `inline; filename="${safeResponseFilename(
          attachmentInfo.attachment.savedAs,
          'attachment',
        )}"`,
        'Cache-Control': 'private, no-store',
      })
      createReadStream(attachmentInfo.filePath).pipe(response)
      return
    }
    if (request.method === 'GET' && url.pathname === '/generation-requests.json') {
      sendJson(response, 200, { requests: await currentGenerationRequests(username) })
      return
    }
    if (request.method === 'GET' && url.pathname === '/team-feedback.json') {
      sendJson(response, 200, { feedback: currentRoundFeedback() })
      return
    }
    if (request.method === 'POST' && url.pathname === '/generation-request') {
      if (!String(request.headers['content-type'] || '').includes('application/json')) {
        sendJson(response, 415, { error: 'generation request expects application/json' })
        return
      }
      const payload = await readJsonBody(request, 512 * 1024)
      const submission = await submitGenerationRequest(username, payload)
      const requestRecord = publicGenerationRequest(submission.record)
      if (submission.httpStatus === 201) response.setHeader('Location', `/generation-request/${requestRecord.id}`)
      sendJson(response, submission.httpStatus, {
        ok: true,
        requestId: requestRecord.id,
        status: requestRecord.status,
        taskType: requestRecord.taskType,
        created: submission.created,
        deduplicated: requestRecord.deduplicated === true,
        modelReady: requestRecord.modelReady === true,
        legacyFallbackUsed: requestRecord.legacyFallbackUsed === true,
        nativeBuild: requestRecord.nativeBuild || null,
        downloadUrl: requestRecord.downloadUrl || '',
        directUrl: requestRecord.downloadUrl || '',
        generationTemplateLabel: requestRecord.generationTemplateLabel || '',
        sourceBaselineAssetId: requestRecord.sourceBaselineAssetId || '',
        deliveryMode: requestRecord.deliveryMode,
        resolutionDurationMs: requestRecord.resolutionDurationMs,
        engineeringAssistanceReady: requestRecord.engineeringAssistanceReady === true,
        engineeringContinuationEligible: requestRecord.engineeringContinuationEligible === true,
        engineeringUseEligible: requestRecord.engineeringUseEligible === true,
      })
      return
    }
    const generationDetailMatch = url.pathname.match(/^\/generation-request\/([a-zA-Z0-9_.-]+)$/)
    if (request.method === 'GET' && generationDetailMatch) {
      sendJson(response, 200, { request: publicGenerationRequest(await findGenerationRequestForUser(generationDetailMatch[1], username)) })
      return
    }
    const generationActionMatch = url.pathname.match(/^\/generation-request\/([a-zA-Z0-9_.-]+)\/delete$/)
    if (request.method === 'POST' && generationActionMatch) {
      const requestId = generationActionMatch[1]
      const removal = await deleteGenerationRequest(username, requestId)
      sendJson(response, 200, {
        ok: true,
        requestId,
        action: removal.action,
        cancelRequested: removal.action === 'cancel_requested',
        cancelled: removal.action === 'cancelled',
        archived: removal.action === 'archived',
        hidden: removal.action !== 'cancel_requested',
        preservedTaskRecord: true,
        preservedGeneratedFiles: true,
      })
      return
    }
    if (request.method === 'POST' && url.pathname === '/feedback') {
      if (!String(request.headers['content-type'] || '').includes('application/json')) {
        sendJson(response, 415, { error: 'feedback expects application/json' })
        return
      }
      const payload = await readJsonBody(request)
      const feedback = await submitFeedback(username, payload)
      sendJson(response, 200, { ok: true, feedbackId: feedback.id, savedFiles: feedback.attachments.length })
      return
    }
    const nativeAssistanceDownloadMatch = url.pathname.match(/^\/native-assistance-download\/([a-zA-Z0-9_.-]+)$/)
    if (request.method === 'GET' && nativeAssistanceDownloadMatch) {
      const item = await findGenerationRequestForUser(nativeAssistanceDownloadMatch[1], username)
      let snapshot
      try {
        snapshot = item.parametricRequest ? open16029ParametricTaskArchive(item) : openVerifiedNativeStructureAssistanceArchive({ dataDir: DATA_DIR, task: item })
      } catch {
        sendJson(response, 409, { error: 'native assistance archive integrity verification failed' })
        return
      }
      const fd = snapshot.fd
      snapshot.fd = null
      let stream
      try {
        stream = createReadStream(snapshot.archivePath, {
          fd,
          autoClose: true,
          start: 0,
          end: snapshot.stat.size - 1,
        })
      } catch (error) {
        closeSync(fd)
        throw error
      }
      response.writeHead(200, {
        'Content-Type': 'application/zip',
        'Content-Length': snapshot.stat.size,
        'Content-Disposition': attachmentContentDisposition(snapshot.archiveFileName),
        'Cache-Control': 'no-store',
      })
      stream.on('error', (error) => response.destroy(error))
      response.on('close', () => {
        if (!stream.destroyed) stream.destroy()
      })
      stream.pipe(response)
      return
    }
    const downloadMatch = url.pathname.match(/^\/download\/([a-z0-9-]+)$/)
    if (request.method === 'GET' && downloadMatch) {
      const asset = byId.get(downloadMatch[1])
      if (!asset) {
        sendJson(response, 404, { error: 'asset not found' })
        return
      }
      const pinnedAsset = Boolean(asset.sha256)
      if (pinnedAsset) {
        if (!existsSync(asset.path)) syncAssetsToSharedStorage(new Set([asset.id]))
        const snapshot = openFreshExactAssetSnapshot(asset)
        if (!snapshot.ok) {
          closeExactAssetSnapshot(snapshot)
          sendJson(response, snapshot.reason === 'missing' ? 404 : 409, {
            error: snapshot.reason === 'missing' ? 'asset not found' : 'asset integrity verification failed',
          })
          return
        }
        const fd = snapshot.fd
        snapshot.fd = null
        let stream
        try {
          stream = createReadStream(asset.path, { fd, autoClose: true, start: 0, end: snapshot.stat.size - 1 })
        } catch (error) {
          closeSync(fd)
          throw error
        }
        response.writeHead(200, {
          'Content-Type': contentTypeForPath(asset.path),
          'Content-Length': snapshot.stat.size,
          'Content-Disposition': attachmentContentDisposition(visibleAssetFileName(asset.fileName)),
          'Cache-Control': 'no-store',
        })
        stream.on('error', (error) => response.destroy(error))
        response.on('close', () => {
          if (!stream.destroyed) stream.destroy()
        })
        stream.pipe(response)
        return
      }

      syncAssetsToSharedStorage(new Set([asset.id]))
      const info = assetInfo(asset)
      if (!info.available) {
        sendJson(response, info.integrityError === 'missing' ? 404 : 409, {
          error: info.integrityError === 'missing' ? 'asset not found' : 'asset integrity verification failed',
        })
        return
      }
      const stat = statSync(asset.path)
      response.writeHead(200, {
        'Content-Type': contentTypeForPath(asset.path),
        'Content-Length': stat.size,
        'Content-Disposition': attachmentContentDisposition(visibleAssetFileName(asset.fileName)),
        'Cache-Control': 'no-store',
      })
      createReadStream(asset.path).pipe(response)
      return
    }
    sendJson(response, 404, { error: 'not found' })
  } catch (error) {
    const statusCode = Number.isInteger(error.statusCode) ? error.statusCode : 500
    sendJson(response, statusCode, { error: error.message || 'server error' })
  }
})

server.listen(PORT, HOST, () => {
  console.log(`16029 review login server listening on ${HOST}:${PORT}`)
  for (const address of localAddresses()) console.log(`- http://${address}:${PORT}/`)
})
