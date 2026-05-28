import { spawn } from 'node:child_process';
import { existsSync, mkdirSync, openSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const scriptDir = dirname(fileURLToPath(import.meta.url));
const projectRoot = resolve(scriptDir, '..');
const serverScript = resolve(scriptDir, 'serve_16029_review_downloads.mjs');
const logDir = resolve(projectRoot, 'workers', 'generation_logs');
const stdoutPath = resolve(logDir, 'review_portal_5180_stdout.log');
const stderrPath = resolve(logDir, 'review_portal_5180_stderr.log');
const pidPath = resolve(logDir, 'review_portal_5180.pid');

if (!existsSync(serverScript)) {
  throw new Error(`Review server script not found: ${serverScript}`);
}

mkdirSync(logDir, { recursive: true });

const stdout = openSync(stdoutPath, 'a');
const stderr = openSync(stderrPath, 'a');
const env = {
  SystemRoot: process.env.SystemRoot || 'C:\\Windows',
  ComSpec: process.env.ComSpec || 'C:\\Windows\\System32\\cmd.exe',
  TEMP: process.env.TEMP || process.env.TMP || 'C:\\Windows\\Temp',
  TMP: process.env.TMP || process.env.TEMP || 'C:\\Windows\\Temp',
  PATH: process.env.Path || process.env.PATH || 'C:\\Windows\\System32;C:\\Windows;C:\\Program Files\\nodejs',
  STUDIO_REVIEW_PORT: process.env.STUDIO_REVIEW_PORT || '5180',
};

const child = spawn(process.execPath, [serverScript], {
  cwd: projectRoot,
  detached: true,
  stdio: ['ignore', stdout, stderr],
  windowsHide: true,
  env,
});

child.unref();
writeFileSync(pidPath, `${child.pid}\n`, 'utf8');

console.log(`16029 review portal started: pid=${child.pid}`);
console.log(`logs: ${stdoutPath}`);
