import assert from 'node:assert/strict'
import { randomBytes, randomUUID, createHash } from 'node:crypto'
import { spawn } from 'node:child_process'
import { copyFileSync, existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { readOutdoorTask, updateOutdoorTask, writeOutdoorJson } from './lib/outdoor_22057_tasks.mjs'

const root = resolve(import.meta.dirname, '..')
const evidence = resolve('D:/机械结构工程师智能体/outputs/outdoor_22057_range_20261011_v12/evidence')
const fixture = resolve(evidence, 'portal_fixture_r13')
assert.ok(!existsSync(fixture), 'Preserve previous fixture')
const dataDir = resolve(fixture, 'data'), release = resolve(dataDir, 'outdoor_22057_release')
mkdirSync(resolve(release, 'portal'), { recursive: true })
const old = resolve(root, 'data/outdoor_22057_release')
const gate = JSON.parse(readFileSync(resolve(old, 'release_gate.json'), 'utf8'))
const sourceStage = 'D:/机械结构工程师智能体/outputs/outdoor_22057_range_20261011_v13'
const script = 'D:/机械结构工程师智能体/outputs/outdoor_22057_range_20261011_v13/scripts/portal_generate_r13.py'
const hash = path => createHash('sha256').update(readFileSync(path)).digest('hex')
gate.generation = { enabled: true, ranges_mm: { height: [1700,2000], width: [900,1200], depth: [540,650] }, source_stage: sourceStage, qualification_sha256: hash(resolve(sourceStage, 'qualification.json')), script, script_sha256: hash(script), python: 'C:/Users/Administrator/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe' }
writeOutdoorJson(resolve(release, 'release_gate.json'), gate)
copyFileSync(resolve(old, gate.archive), resolve(release, gate.archive))
copyFileSync(resolve(old, 'portal/preview.png'), resolve(release, 'portal/preview.png'))
writeFileSync(resolve(dataDir, 'review_download_invite_code.txt'), 'outdoor-local-fixture')
const child = spawn(process.execPath, [resolve(root, 'tools/serve_16029_review_downloads.mjs')], { cwd: root, windowsHide: true, stdio: ['ignore','ignore','pipe'], env: { ...process.env, STUDIO_REVIEW_ROOT: fixture, STUDIO_REVIEW_DATA_DIR: dataDir, STUDIO_REVIEW_STORAGE_ROOT: resolve(fixture, 'storage'), STUDIO_REVIEW_CONFIG: resolve(fixture, 'absent.json'), STUDIO_REVIEW_HOST: '127.0.0.1', STUDIO_REVIEW_PORT: '5182', STUDIO_REVIEW_SKIP_ASSET_SYNC: '1' } })
let stderr = ''; child.stderr.on('data', data => { stderr += data.toString() })
const base = 'http://127.0.0.1:5182', password = randomBytes(24).toString('base64url')
const checks = []
const check = async (name, run) => { await run(); checks.push({ name, pass: true }) }
const post = (path, fields, cookie) => fetch(base + path, { method: 'POST', redirect: 'manual', headers: cookie ? { cookie } : {}, body: new URLSearchParams(fields) })
try {
  let ready = false
  for (let i=0;i<80;i++) { try { ready = (await fetch(base + '/status.json')).ok } catch {} if (ready) break; await new Promise(yes => setTimeout(yes,100)) }
  assert.ok(ready, stderr)
  await check('unauthenticated_model_route_requires_existing_auth', async () => { const r = await fetch(base + '/outdoor-22057', { redirect: 'manual' }); assert.equal(r.status,302); assert.equal(r.headers.get('location'),'/login'); assert.equal((await post('/outdoor-22057/task', {})).status,401) })
  const register = async username => { const r = await post('/register', { username, password, inviteCode: 'outdoor-local-fixture' }); assert.equal(r.status,302); return r.headers.get('set-cookie').split(';')[0] }
  const owner = await register('outdoor-fixture-owner'), other = await register('outdoor-fixture-other')
  const spec = { reference: '真实原生生成闭环核对', height: '1825', width: '1050', depth: '595', doors: '14' }
  await check('form_serves_qualified_range_and_number_controls', async () => { const text = await (await fetch(base + '/outdoor-22057', { headers: { cookie: owner } })).text(); assert.ok(text.includes('type="number" min="1700" max="2000"')); assert.ok(text.includes('当前开放固定规格任务') === false); writeFileSync(resolve(evidence, 'portal_r13_rendered.html'), text) })
  await check('server_rejects_out_of_range_nonfinite_empty_and_door_change', async () => { for (const invalid of [{height:'1699'}, {width:'1201'}, {depth:'539'}, {depth:'250'}, {width:'NaN'}, {height:''}, {depth:'Infinity'}, {doors:'12'}]) { assert.equal((await post('/outdoor-22057/task', {...spec,...invalid},owner)).status,422) } })
  const r = await post('/outdoor-22057/task', spec, owner); assert.equal(r.status,201)
  const realTask = (await r.json()).task
  await check('new_task_queued_without_download', async () => { assert.equal(realTask.state,'queued'); assert.equal(realTask.downloadUrl,null); assert.equal((await fetch(base + '/outdoor-22057/task/' + realTask.id + '/download',{headers:{cookie:owner}})).status,409) })
  await check('task_and_feedback_are_account_owned', async () => { const list = await (await fetch(base + '/outdoor-22057/tasks.json', {headers:{cookie:other}})).json(); assert.equal(list.tasks.length,0); assert.equal((await fetch(base + '/outdoor-22057/task/' + realTask.id + '/download',{headers:{cookie:other}})).status,404); assert.equal((await post('/outdoor-22057/task/' + realTask.id + '/feedback',{message:'x'},other)).status,404); assert.equal((await post('/outdoor-22057/task/' + realTask.id + '/feedback',{message:'本机验收反馈'},owner)).status,201) })
  await check('legacy_fixed_tasks_keep_verified_download', async () => { const id='OUT22057-'+randomUUID(); await updateOutdoorTask(dataDir,id,()=>({id,username:'outdoor-fixture-owner',reference:'历史固定包',createdAt:new Date().toISOString(),specification:{height:1939,width:1000,depth:550,doors:14},feedback:[],artifact:{archive:gate.archive,bytes:gate.bytes,sha256:gate.sha256}})); const downloaded=await fetch(base+'/outdoor-22057/task/'+id+'/download',{headers:{cookie:owner}}); assert.equal(downloaded.status,200); const bytes=Buffer.from(await downloaded.arrayBuffer()); assert.equal(createHash('sha256').update(bytes).digest('hex'),gate.sha256) })
  await check('failed_tasks_have_no_download_even_with_artifact', async () => { const id='OUT22057-'+randomUUID(); await updateOutdoorTask(dataDir,id,()=>({id,username:'outdoor-fixture-owner',reference:'失败拦截',createdAt:new Date().toISOString(),specification:{height:1939,width:1000,depth:550,doors:14},state:'failed',feedback:[],artifact:{archive:gate.archive,bytes:gate.bytes,sha256:gate.sha256}})); assert.equal((await fetch(base+'/outdoor-22057/task/'+id+'/download',{headers:{cookie:owner}})).status,409) })
  // Verify worker failure through its public task runner without starting CAD.
  process.env.STUDIO_REVIEW_DATA_DIR=dataDir
  const { runNextOutdoorTask }=await import('./run_outdoor_22057_portal_worker.mjs')
  await check('shared_CAD_busy_preserves_queue', async()=>{assert.equal((await runNextOutdoorTask({available:()=>false})).state,'waiting_for_cad');assert.equal(readOutdoorTask(dataDir,realTask.id).state,'queued')})
  await check('native_failure_preserves_feedback_and_blocks_download', async()=>{const result=await runNextOutdoorTask({available:()=>true,execute:async()=>1});assert.equal(result.state,'failed');const current=readOutdoorTask(dataDir,realTask.id);assert.equal(current.feedback.length,1);assert.equal(current.artifact,null)})
  await updateOutdoorTask(dataDir,realTask.id,current=>({...current,state:'queued',workerPid:null,error:null,startedAt:null,finishedAt:null}))
  // This one fixture task is subsequently executed through the actual native worker.
  writeOutdoorJson(resolve(evidence,'portal_r13_fixture_runtime.json'),{server_pid:child.pid,base,dataDir,realTaskId:realTask.id,browserUsername:'outdoor-fixture-owner',browserPassword:password})
  writeOutdoorJson(resolve(evidence,'portal_r13_api_acceptance.json'),{status:'PASS',checks,scope:'Actual portal HTTP/auth/download behavior with isolated accounts; native task remains queued for real CAD acceptance.',taskId:realTask.id})
  console.log(JSON.stringify({status:'PASS',checks:checks.length,serverPid:child.pid,fixture,taskId:realTask.id}))
  child.unref(); child.stderr.unref()
} catch(error) { child.kill(); console.error(error); process.exitCode=1 }
