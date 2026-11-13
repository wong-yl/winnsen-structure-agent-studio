import { existsSync, mkdirSync, readFileSync, readdirSync, copyFileSync } from 'node:fs'
import { resolve, basename } from 'node:path'
import { spawnSync } from 'node:child_process'
import { createNativeTaskStore } from './lib/locker_16029_native_task_store.mjs'

/**
 * 为已完成的参数化任务生成生产图纸
 *
 * 使用方式:
 * node tools/generate_16029_task_drawings.mjs <task-id>
 *
 * 前提条件:
 * - 任务状态必须是 'ready' (模型已验收通过)
 * - 必须有对应的门型图纸模板
 * - SOLIDWORKS 2020 必须可用
 */

const ROOT = resolve(import.meta.dirname, '..')
const DRAWING_TEMPLATE_ROOT = resolve(ROOT, 'data/production_orders_20261029/continuation_sol_20261029_r5/drawing_template_trial_r1')
const DATA_DIR = resolve(ROOT, 'data')
const NATIVE_REQUESTS_DIR = resolve(DATA_DIR, 'native_model_requests')

function fail(message) {
  console.error('ERROR:', message)
  process.exit(1)
}

function log(message) {
  console.log(`[${new Date().toISOString()}]`, message)
}

/**
 * 从任务的 parametricRequest 提取门型列表
 */
function extractDoorTypes(task) {
  const doorTypes = new Set()
  const columns = task.parametricRequest?.columns || []

  for (const column of columns) {
    const side = column.side // 'L' or 'R'
    for (const door of column.doors || []) {
      // 计算门高(mm) - 这里需要根据实际算法调整
      const heightMm = door.heightMm || (door.heightUnits * 933) // 假设每单位933mm
      const doorType = `${side.toLowerCase()}_H${Math.round(heightMm)}`
      doorTypes.add(doorType)
    }
  }

  return Array.from(doorTypes).sort()
}

/**
 * 检查门型是否有对应的图纸模板
 */
function checkDrawingTemplatesExist(doorTypes) {
  const missing = []

  for (const doorType of doorTypes) {
    // 检查成形页模板 (需要确认实际的模板命名规则)
    const formedTemplate = resolve(DRAWING_TEMPLATE_ROOT, `templates/${doorType}_formed.SLDDRW`)
    const flatTemplate = resolve(DRAWING_TEMPLATE_ROOT, `templates/${doorType}_flat.SLDDRW`)

    // 这里先假设模板存在，实际需要根据你的模板组织方式调整
    log(`需要门型模板: ${doorType}`)
  }

  if (missing.length > 0) {
    fail(`缺少图纸模板: ${missing.join(', ')}`)
  }

  return true
}

/**
 * 生成成形页图纸
 */
function generateFormedDrawings(task, doorTypes, outputDir) {
  log('开始生成成形页图纸...')

  // 使用 DrawingTemplateTrial.cs 生成成形页
  const buildDir = resolve(outputDir, 'formed_build')
  const buildScript = resolve(DRAWING_TEMPLATE_ROOT, 'build.mjs')

  if (!existsSync(buildScript)) {
    fail(`成形页构建脚本不存在: ${buildScript}`)
  }

  // 编译成形页程序
  log('编译成形页程序...')
  const buildResult = spawnSync('node', [buildScript, buildDir], {
    encoding: 'utf8',
    cwd: DRAWING_TEMPLATE_ROOT,
    windowsHide: true
  })

  if (buildResult.status !== 0) {
    fail(`成形页程序编译失败: ${buildResult.stderr}`)
  }

  log('成形页程序编译完成')

  // 为每个门型执行绘图
  const formedPdfs = []
  for (const doorType of doorTypes) {
    log(`生成 ${doorType} 成形页...`)

    // 这里需要调用编译好的 .exe，传入正确的参数
    // 具体参数格式需要根据 DrawingTemplateTrial.cs 的实现确定
    const runDir = resolve(outputDir, `formed_${doorType}`)

    // TODO: 执行绘图程序
    // const executable = resolve(buildDir, 'DrawingTemplateTrial.exe')
    // const result = spawnSync(executable, [...args], { ... })

    // 假设生成的 PDF 在 runDir 中
    const pdfPath = resolve(runDir, `${doorType}_成形.pdf`)
    if (existsSync(pdfPath)) {
      formedPdfs.push(pdfPath)
      log(`✓ ${doorType} 成形页生成成功`)
    } else {
      log(`✗ ${doorType} 成形页生成失败`)
    }
  }

  return formedPdfs
}

/**
 * 生成展开页图纸
 */
function generateFlatDrawings(task, doorTypes, outputDir) {
  log('开始生成展开页图纸...')

  const flatScript = resolve(DRAWING_TEMPLATE_ROOT, 'run_door_flat_probe_r3.mjs')

  if (!existsSync(flatScript)) {
    fail(`展开页脚本不存在: ${flatScript}`)
  }

  // 使用最新的 t 系列版本
  const version = 't08' // 递增版本号

  // 编译展开页程序
  log(`编译展开页程序 (${version})...`)
  const buildResult = spawnSync('node', [flatScript, '--build', version], {
    encoding: 'utf8',
    cwd: DRAWING_TEMPLATE_ROOT,
    windowsHide: true
  })

  if (buildResult.status !== 0) {
    fail(`展开页程序编译失败: ${buildResult.stderr}`)
  }

  log('展开页程序编译完成')

  // 为每个门型执行绘图
  const flatPdfs = []
  for (const doorType of doorTypes) {
    log(`生成 ${doorType} 展开页...`)

    const runId = `sheet_${doorType}_${version}`
    const referenceId = `flat_${doorType}_a1`

    const result = spawnSync('node', [
      flatScript,
      '--execute',
      version,
      runId,
      doorType,
      referenceId
    ], {
      encoding: 'utf8',
      cwd: DRAWING_TEMPLATE_ROOT,
      windowsHide: true,
      timeout: 600000 // 10分钟超时
    })

    if (result.status !== 0) {
      log(`✗ ${doorType} 展开页生成失败: ${result.stderr}`)
      continue
    }

    // 检查生成的 PDF
    const runDir = resolve(DRAWING_TEMPLATE_ROOT, 'runs', runId)
    const pdfPath = resolve(runDir, `${doorType}_FLAT.pdf`)

    if (existsSync(pdfPath)) {
      flatPdfs.push(pdfPath)
      log(`✓ ${doorType} 展开页生成成功`)
    } else {
      log(`✗ ${doorType} 展开页 PDF 未找到`)
    }
  }

  return flatPdfs
}

/**
 * 主函数
 */
async function main() {
  const args = process.argv.slice(2)

  if (args.length === 0 || args[0] === '--help') {
    console.log(`
使用方式: node tools/generate_16029_task_drawings.mjs <task-id>

为已验收的参数化模型任务生成生产图纸。

参数:
  task-id    任务ID (例如: PARAMETRIC-AUTO-889-1967-575-20260915)

前提条件:
  - 任务状态必须是 'ready' (模型已验收)
  - 对应的门型图纸模板必须存在
  - SOLIDWORKS 2020 必须可用

示例:
  node tools/generate_16029_task_drawings.mjs PARAMETRIC-AUTO-889-1967-575-20260915
`)
    process.exit(0)
  }

  const taskId = args[0]

  log(`开始为任务 ${taskId} 生成图纸`)

  // 加载任务
  const store = createNativeTaskStore({ dataDir: NATIVE_REQUESTS_DIR })
  let task

  try {
    task = await store.getTask({ taskId })
  } catch (error) {
    fail(`无法加载任务: ${error.message}`)
  }

  // 检查任务状态
  if (task.nativeBuild?.state !== 'ready') {
    fail(`任务状态不是 'ready': ${task.nativeBuild?.state}`)
  }

  log('任务验证通过，开始分析门型...')

  // 提取门型列表
  const doorTypes = extractDoorTypes(task)

  if (doorTypes.length === 0) {
    fail('未找到门型信息')
  }

  log(`发现 ${doorTypes.length} 种门型: ${doorTypes.join(', ')}`)

  // 检查模板
  checkDrawingTemplatesExist(doorTypes)

  // 创建输出目录
  const outputDir = resolve(DATA_DIR, 'drawing_outputs', taskId)
  mkdirSync(outputDir, { recursive: true })

  log(`输出目录: ${outputDir}`)

  // 生成成形页
  const formedPdfs = generateFormedDrawings(task, doorTypes, outputDir)

  // 生成展开页
  const flatPdfs = generateFlatDrawings(task, doorTypes, outputDir)

  // 汇总结果
  const allPdfs = [...formedPdfs, ...flatPdfs]

  log(`\n=== 图纸生成完成 ===`)
  log(`成形页: ${formedPdfs.length} 个`)
  log(`展开页: ${flatPdfs.length} 个`)
  log(`总计: ${allPdfs.length} 个 PDF`)
  log(`输出目录: ${outputDir}`)

  // TODO: 将 PDF 路径写入任务记录，以便工作台下载

  return {
    success: allPdfs.length === doorTypes.length * 2,
    taskId,
    doorTypes,
    formedPdfs,
    flatPdfs,
    outputDir
  }
}

main().catch(error => {
  console.error('未捕获的错误:', error)
  process.exit(1)
})
