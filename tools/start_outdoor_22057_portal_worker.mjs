import { closeSync, mkdirSync, openSync, writeFileSync } from 'node:fs'
import { resolve, dirname } from 'node:path'
import { fileURLToPath } from 'node:url'
import { spawn } from 'node:child_process'
const root = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const runtime = resolve(root, 'data/review_portal_runtime')
mkdirSync(runtime, { recursive: true })
const stdout = openSync(resolve(runtime, 'outdoor-worker.stdout.log'), 'a')
const stderr = openSync(resolve(runtime, 'outdoor-worker.stderr.log'), 'a')
const child = spawn(process.execPath, [resolve(root, 'tools/run_outdoor_22057_portal_worker.mjs'), '--watch'], { cwd: root, detached: true, windowsHide: true, stdio: ['ignore', stdout, stderr], env: process.env })
child.unref(); closeSync(stdout); closeSync(stderr)
writeFileSync(resolve(runtime, 'outdoor-worker.pid'), String(child.pid) + '\n')
console.log(JSON.stringify({ started: true, pid: child.pid }))
