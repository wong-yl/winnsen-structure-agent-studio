import { spawn } from 'node:child_process'
import { mkdirSync, openSync, closeSync, writeFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const runtime = resolve(root, 'data/review_portal_runtime')
mkdirSync(runtime, { recursive: true })
const stdout = openSync(resolve(runtime, 'parametric-worker.stdout.log'), 'a')
const stderr = openSync(resolve(runtime, 'parametric-worker.stderr.log'), 'a')
const child = spawn(process.execPath, [resolve(root, 'tools/run_16029_parametric_portal_worker.mjs'), '--watch'], {
  cwd: root, detached: true, windowsHide: true, stdio: ['ignore', stdout, stderr], env: process.env,
})
child.unref(); closeSync(stdout); closeSync(stderr)
writeFileSync(resolve(runtime, 'parametric-worker.pid'), String(child.pid) + '\n')
console.log(JSON.stringify({ started: true, pid: child.pid }))
