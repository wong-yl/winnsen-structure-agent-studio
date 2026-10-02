# 888x14 原生 seed stage

`SeedPack888Native.exe` 是 888×14 结构工程辅助模型的 seed 阶段。它把受保护的 V37 final exact75 原生 CAD 文件逐文件复制到当前 attempt 的 `native_cad/working_pack`，先完成完整只读迁移诊断，再按正式写入门执行根装配稳定化、fresh read-only reopen 和 evidence/receipt 成对提交。

该阶段不复用旧生成器，不使用 Pack-and-Go，不修改受保护 source，也不表示结构工程辅助模型已经完成；后续宽度、门模块、锁拓扑、根装配和最终迁移重开阶段仍需完成，且需要结构工程师复核。

当前优先执行临时、不可消费的“上盖前侧板 V37/1000W B-rep 只读采集”探针；其余测量、脱链、全量重建、master-reference 采集和旧装配重建模式均关闭。`direct-copy` 后仅在 V37 `working_pack` 与 attempt-local `gold_capture_pack` 中各以一个 owned read-only SolidWorks 会话打开 `上盖壳体前侧板.sldprt`，绝不打开 gold/source。探针采集一个实体 body 的 bbox、48/112（V37）或 52/120（1000W）面/边拓扑，以及每个 Face2、Surface、Loop2、Edge、Curve、CurveParamData、Vertex 的原始 SI B-rep 数据：面面积/法向/平面或圆柱参数、loop edge 顺序、曲线参数与线/圆参数、端点。circle edge 仅保存原始边列表和稳定 SHA-256 geometry signature，不把重复圆边误计为孔数；后续可据此同 gold DXF 的 14 个圆进行中心对应。探针禁止 `Save3`、`SaveAs`、`BreakAllExternalFileReferences2`、新建文档和加组件；完成后强制以 exit `65` 结束并回滚 `working_pack` 与 `gold_capture_pack`，只写 `evidence/private/topcover_native_front_brep_capture_probe_only.json` 与 cleanup proof，不写共享 evidence/receipt，也不表示模型完成。

## 固定输入与输出

- source：`workers/generated_models/review_generation_requests/v43-int-v37-760w-six-door-l642-r246-r1/native_cad/final_native_hierarchical_pack_and_go`
- source inventory：flat `75 = 22 SLDASM + 53 SLDPRT`
- canonical `name|SHA256\n` digest：`9E9CF3485CF3A819C14F0720E3A2C3FA2B2994DFC8E82280DCC774EBF36F067B`
- source root：`标准寄存柜1917×760×550(总装配).SLDASM`
- target：`<attempt>/native_cad/working_pack`
- evidence：`<attempt>/evidence/seed_pack_888_native_v1.json`
- receipt：`<attempt>/receipts/clone_native_seed.json`

## 执行边界

运行时必须同时满足：

- `native_build_plan.json` 仍为 `planningOnly=true`、`executorImplemented=false`，并绑定可信 888x14 recipe、9 阶段 receipt contract 和 6 个 toolchain manifest。
- `WINNSEN_NATIVE_EXECUTION_AUTH_PATH` 指向当前 attempt 中 `execution_authorizations/native_seed_pack_888x14_v1.json`。
- `WINNSEN_NATIVE_EXECUTION_AUTH_SHA256`、`WINNSEN_NATIVE_16029_PLAN_SHA256` 与实时文件完全一致。
- 授权为短期 shared contract，并绑定当前 task/revision/request/lease/plan/recipe/tool/source inventory/attempt/phase。
- 运行前 `SLDWORKS`、`sldProcMon` 均为 0；只允许 `SldWorks.Application.28` 与固定 exe SHA-256。

运行命令由可信 worker 填充环境变量后调用：

```powershell
& '.\SeedPack888Native.exe' `
  --source '<exact-v37-final-directory>' `
  --working-pack '<attempt>\native_cad\working_pack' `
  --out '<attempt>\evidence\seed_pack_888_native_v1.json' `
  --confirm-task '<task-id>'
```

## CAD 验证门

1. 逐文件 direct-copy protected source 到当前 attempt 的 `working_pack`；必须保持 exact75 名称、文件身份和 SHA-256，受保护 source 前后不变。
2. owned SolidWorks 只读打开 relocated root。初始打开只允许 `0/32` 或 `0/96`，但完整 75-document relocation diagnostic 必须是 `warning64Docs=none`、无 document fault；否则立即 fail-closed，不得保存。
3. warning64 路径必须完成 assembly component path、fresh read-only、cache、traversal、rebuild 和 hash 不变检查；任何不完整、非本地或无法证明的结果都拒绝进入写入阶段。
4. 只读诊断包含 top-cover load-order/root-context 条件实验、dependency closure、external reference 和唯一已知 root `Reference 51` 诊断；这些实验不得调用保存、更新引用或断链 API。
5. 只有完整诊断通过后才允许 root writable stabilization；写入前仍须通过 quiescence、dependency、feature、authorization 和 toolchain identity gates。保存后 fresh read-only reopen 必须为 `errors=0 / warnings=32`，并保持 root hash 不变。

进程清理采用 PID、start time、exe path/hash 的三态判定。`sldProcMon` 还必须同时匹配 parent PID 与 exact `--ppid=<pid>`；不能证明身份时拒绝 kill，也拒绝回滚文件。

## 不可变提交

任何失败都会在 CAD 进程稳定归零并再次复核后删除本次未配对 artifact，并安全回滚本次创建的 `working_pack` 与 `gold_capture_pack`；当前采集探针写入 `<attempt>/evidence/private/topcover_native_rebuild_capture_probe_cleanup.json`，其中 `cleanupPass` 仅在进程归零、protected source 不变且两个 attempt-local pack 都已回滚时为真。rollback 遇到 reparse、hardlink、非平面内容、realpath 逃逸、未知 CAD basename、大小写重复或无法证明的进程身份会拒绝执行；仅在 CAD 已稳定归零时允许清理由 source basename 一一配对、单链接且不超过 4 KB 的 `~$` SolidWorks 锁文件。

共享 evidence / receipt 必须通过 CreateNew、成对写入、无 hash cycle 和提交前 process/source gates；未通过完整诊断时不得生成成功 evidence 或 receipt。

## 静态构建与验证

```powershell
& '.\Build-SeedPack888Native.ps1'
& '.\Verify-SeedPack888Native.ps1'
```

构建固定 `/platform:x64 /warnaserror+`，生成 `toolchain_manifest.json`。静态验证执行真实 `--self-test`、无参数 fail-closed、共享 contract/commitment 对照、ECMAScript JSON number/string 跨语言向量，并验证前后 CAD 进程均为 0；不会启动 SolidWorks。
