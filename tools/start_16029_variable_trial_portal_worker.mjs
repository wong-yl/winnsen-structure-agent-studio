import { spawn } from 'node:child_process'
import { closeSync, mkdirSync, openSync, writeFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const runtimeDir = resolve(ROOT, 'data/review_portal_runtime')
mkdirSync(runtimeDir, { recursive: true })
const stdout = openSync(resolve(runtimeDir, 'variable-trial-worker.stdout.log'), 'a')
const stderr = openSync(resolve(runtimeDir, 'variable-trial-worker.stderr.log'), 'a')

const child = spawn(process.execPath, [resolve(ROOT, 'tools/run_16029_variable_trial_portal_worker.mjs'), '--watch'], {
  cwd: ROOT,
  detached: true,
  windowsHide: true,
  stdio: ['ignore', stdout, stderr],
  env: process.env,
})
child.unref()
closeSync(stdout)
closeSync(stderr)
writeFileSync(resolve(runtimeDir, 'variable-trial-worker.pid'), String(child.pid) + '\n')
console.log(JSON.stringify({ started: true, pid: child.pid, mode: 'watch' }))
