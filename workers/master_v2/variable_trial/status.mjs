import { readdirSync, existsSync, statSync } from 'node:fs'
import { join } from 'node:path'
import { DATA,read } from './common.mjs'
const dir=join(DATA,'runs',readdirSync(join(DATA,'runs')).sort().at(-1))
const result=join(dir,'native-result.json')
if(!existsSync(result)){console.log(JSON.stringify({dir,state:'native_not_started'}));process.exit(0)}
const data=read(result),last=data.events.slice(-5).map(e=>({inspection:e.inspection,index:e.index,stage:e.stage,path:e.path,success:e.success,cadExited:e.cadExited,error:e.error,result:e.result?.success}))
console.log(JSON.stringify({dir,modifiedAt:statSync(result).mtime.toISOString(),cadPid:data.cadPid,last}))
