import { existsSync, readdirSync } from 'node:fs'
import { homedir } from 'node:os'
import { join, resolve } from 'node:path'
import { pathToFileURL } from 'node:url'

export const GOLD_SOURCE_MANIFEST_SCHEMA = 'winnsen.locker16029.gold_source_manifest.v1'

export const GOLD_SOURCE_FOLDER_NAME = '参数化模板素材_U盘原始_20260526'
export const GOLD_16029_ENGINEERING_RELATIVE_DIR = join('16029 寄存柜(标准组合式 1917×1000×550)', '1.工程图')

export const GOLD_SOURCE_MODULES = Object.freeze([
  {
    key: 'cabinet_left_side_weldment',
    role: '箱体左侧板焊接',
    file: '箱体左侧板焊接.sldasm',
    requiredChildren: ['箱体左侧板', '箱体侧板加强筋1', '箱体侧板加强筋2', '衣杆固定支架'],
  },
  {
    key: 'cabinet_right_side_weldment',
    role: '箱体右侧板焊接',
    file: '箱体右侧板焊接.SLDASM',
    requiredChildren: ['箱体右侧板', '箱体侧板加强筋1', '箱体侧板加强筋2', '衣杆固定支架'],
  },
  {
    key: 'cabinet_left_shelf_weldment',
    role: '箱体横层板L焊接',
    file: '箱体横层板L焊接.SLDASM',
    requiredChildren: ['箱体横层板L', '箱体横层板加强筋'],
  },
  {
    key: 'cabinet_right_shelf_weldment',
    role: '箱体横层板R焊接',
    file: '箱体横层板R焊接.SLDASM',
    requiredChildren: ['箱体横层板R', '箱体横层板加强筋'],
  },
  {
    key: 'cabinet_left_partition_weldment',
    role: '箱体竖隔板L焊接',
    file: '箱体竖隔板L焊接.SLDASM',
    requiredChildren: ['箱体竖隔板L', '箱体侧板加强筋1', '衣杆固定支架'],
  },
  {
    key: 'cabinet_right_partition_weldment',
    role: '箱体竖隔板R焊接',
    file: '箱体竖隔板R焊接.SLDASM',
    requiredChildren: ['箱体竖隔板R', '箱体侧板加强筋1', '衣杆固定支架'],
  },
  {
    key: 'front_frame_weldment',
    role: '门框焊接',
    file: '门框焊接.sldasm',
    requiredChildren: ['门框 上', '门框 下', '门框 左', '门框 右', '门框 竖隔板L', '门框 竖隔板R'],
  },
  {
    key: 'base_weldment',
    role: '底座焊接',
    file: '底座焊接.SLDASM',
    requiredChildren: ['底座底板', '底座外框', '底座加强筋', '螺母M12'],
    hiddenReferenceChildren: ['底座 模型'],
  },
  {
    key: 'top_cover_weldment',
    role: '上盖焊接',
    file: '上盖焊接.SLDASM',
    requiredChildren: ['上盖壳体底板', '上盖壳体左侧板', '上盖壳体右侧板', '上盖壳体前侧板', '上盖壳体后侧板'],
    hiddenReferenceChildren: ['上盖 模型'],
  },
])

export const GOLD_DOOR_MODULES = Object.freeze([2, 4, 6].flatMap((ratio) => [
  {
    key: `door_${ratio}_12_assembly`,
    role: `储物柜门${ratio}╱12装配`,
    file: `储物柜门${ratio}╱12装配.SLDASM`,
    requiredChildren: [`储物柜门${ratio}╱12焊接`, '塑料轴套(云绅模具)', '门轴销', '电控U型锁钩ZJA-S500', '开口挡圈 5'],
  },
  {
    key: `door_${ratio}_12_weldment`,
    role: `储物柜门${ratio}╱12焊接`,
    file: `储物柜门${ratio}╱12焊接.SLDASM`,
    requiredChildren: [`储物柜门板${ratio}╱12`, `柜门加强筋${ratio}╱12`, '插销固定板', 'U型锁钩垫板'],
  },
]))

export function defaultGoldSourceRoot() {
  return process.env.WINNSEN_16029_GOLD_SOURCE_ROOT ||
    join(homedir(), 'Desktop', GOLD_SOURCE_FOLDER_NAME)
}

function findCaseInsensitiveFile(root, expectedName) {
  if (!existsSync(root)) return ''
  const expected = expectedName.toLocaleLowerCase('zh-CN')
  const match = readdirSync(root, { withFileTypes: true })
    .filter((entry) => entry.isFile())
    .find((entry) => entry.name.toLocaleLowerCase('zh-CN') === expected)
  return match ? join(root, match.name) : ''
}

export function buildGoldSourceManifest(root = defaultGoldSourceRoot()) {
  const sourceRoot = resolve(root)
  const engineeringDir = join(sourceRoot, GOLD_16029_ENGINEERING_RELATIVE_DIR)
  const modules = [...GOLD_SOURCE_MODULES, ...GOLD_DOOR_MODULES].map((module) => {
    const sourcePath = findCaseInsensitiveFile(engineeringDir, module.file)
    return {
      ...module,
      relativePath: join(GOLD_16029_ENGINEERING_RELATIVE_DIR, module.file),
      sourcePath,
      exists: Boolean(sourcePath && existsSync(sourcePath)),
    }
  })

  return {
    schema: GOLD_SOURCE_MANIFEST_SCHEMA,
    cadMainline: 'SolidWorks 2020',
    sourceRoot,
    sourceRootExists: existsSync(sourceRoot),
    engineeringDir,
    engineeringDirExists: existsSync(engineeringDir),
    note: 'Gold-source files are used as structural semantics and module hierarchy references; generated CAD outputs must still be width/height parameterized before engineer handoff.',
    modules,
  }
}

export function main(argv = process.argv.slice(2)) {
  const rootIndex = argv.indexOf('--root')
  const root = rootIndex >= 0 ? argv[rootIndex + 1] : defaultGoldSourceRoot()
  process.stdout.write(`${JSON.stringify(buildGoldSourceManifest(root), null, 2)}\n`)
}

if (import.meta.url === pathToFileURL(process.argv[1]).href) {
  main()
}
