import { existsSync, readFileSync, renameSync, writeFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import {
  NATIVE_16029_GENERATOR,
  normalize16029NativeModelRequest,
} from './locker_16029_native_generator.mjs'
const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..')
function usage() {
  return 'Usage: node tools/process_16029_native_generation_request.mjs --request <request.json> [--output <result.json>]'
}
function parseArgs(argv) {
  const result = { requestPath: '', outputPath: '' }
  for (let index = 0; index < argv.length; index += 1) {
    const value = argv[index]
    if (value === '--request') result.requestPath = argv[++index] || ''
    else if (value === '--output') result.outputPath = argv[++index] || ''
    else throw new Error(`unknown argument: ${value}`)
  }
  if (!result.requestPath) throw new Error(usage())
  return result
}
function writeJsonAtomic(path, value) {
  const target = resolve(path)
  const temporary = `${target}.tmp-${process.pid}`
  writeFileSync(temporary, `${JSON.stringify(value, null, 2)}\n`, 'utf8')
  renameSync(temporary, target)
}
let exitCode = 0
try {
  const args = parseArgs(process.argv.slice(2))
  const requestPath = resolve(ROOT, args.requestPath)
  if (!existsSync(requestPath)) throw new Error(`request file not found: ${requestPath}`)
  const request = JSON.parse(readFileSync(requestPath, 'utf8'))
  const resolution = normalize16029NativeModelRequest(request)
  const result = {
    schema: 'winnsen.native_16029_generation_resolution.v1',
    generator: NATIVE_16029_GENERATOR,
    resolvedAt: new Date().toISOString(),
    ...resolution,
  }
  if (args.outputPath) writeJsonAtomic(resolve(ROOT, args.outputPath), result)
  process.stdout.write(`${JSON.stringify(result, null, 2)}\n`)
  exitCode = resolution.ok ? 0 : 3
} catch (error) {
  process.stderr.write(`${error instanceof Error ? error.message : String(error)}\n`)
  exitCode = 2
}
process.exit(exitCode)
