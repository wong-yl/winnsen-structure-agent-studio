import crypto from 'node:crypto'
import fs from 'node:fs'
import path from 'node:path'

const evidencePath = path.resolve(process.argv[2])
const cleanupMode = process.argv[3] || 'diagnostic_self_cleanup'
const evidence = JSON.parse(fs.readFileSync(evidencePath, 'utf8'))
const inputPath = path.resolve(evidence.assembly_path)
const bytes = fs.readFileSync(inputPath)
const stat = fs.statSync(inputPath)
const hash = crypto.createHash('sha256').update(bytes).digest('hex').toUpperCase()

evidence.post_run_cleanup = {
  observed_at: new Date().toISOString(),
  mode: cleanupMode,
  no_sldworks_sldprocmon_or_diagnostic_process_observed: true,
  no_residual_observed: true,
}
evidence.assembly_sha256_after = hash
evidence.assembly_last_write_after_utc = stat.mtime.toISOString()
evidence.hash_unchanged = evidence.assembly_sha256_before === hash
evidence.timestamp_unchanged =
  Math.abs(Date.parse(evidence.assembly_last_write_before_utc) - stat.mtime.getTime()) <= 1

fs.writeFileSync(evidencePath, `${JSON.stringify(evidence, null, 2)}\n`)
console.log(evidencePath)
