# 16029 全局变量隔离试验：暂停交接

暂停时间：2026-11-12 23:16 左右（Asia/Shanghai）。用户明确要求“暂停吧，写总结，明天继续”。本文件供 2026-11-13 续接；暂停期间不再启动 CAD、编译或继续实施。

## 当前结论

- T0：独立副本、完整依赖复制和重定向已通过。
- T1：左右门板内部变量、变尺寸和恢复基准已通过；独立新目录、新 CAD 进程重开已通过。
- T2：未通过。左基准门 DXF 已取得与历史完整几何一致的局部证据；六例 DXF/PDF 尚未完成。工程图图框缩放原因已定位并修复到原生读回通过，PDF 最终显示仍待新实跑。原生折弯注释排版仍未完成。
- T3：未通过。第一组已完成原生全量测量；第二、第三组仍未完成。用户已明确决定：**保留原结构完成三组测量，T3 如实判未通过**。
- T4：未完成。已补写冷重开和迁移比较检查，最新源码未完成编译及原生运行验证。
- 所有试验仍为 `production_release=false`。技术运行完成、迁移一致和工程验收分别记录，不能相互替代。

## 保护与暂停状态

暂停收尾只读运行 `node workers/master_v2/variable_trial/run.mjs check`，返回 `idle=true`、来源 CAD 60 个、无活动 CAD/正式任务/锁冲突。最后四次诊断的 `supervision.state=cad_exited`、`nativeEnded=true`、`cad=[]`，各自 `protection-result.pass=true,changed=[]`；没有强制终止 CAD 或接管旧锁。

最后四次诊断均失败、无成功 `result.json`。14:55 一次监督查询出现 `PROCESS_IDENTITY_UNAVAILABLE`，15:00:20.085Z 发生、15:00:21.522Z 恢复，最终确认 CAD 已退出；其他三次 `warnings=[]`。不能把该警告抹去。

不修改正式工作台、门户、生成器、版本选择、任务、账户、自启和服务，不恢复暂停任务，不提交试验到工作台。原母版、种子包、旧 A+ 计划、已有 P0 报告只读。不涉及户外柜或 R/U 盘，不 commit/push/release。工作区已有大量其他 dirty 文件，不能据此还原或清理。

## 可复用原生证据

数据根目录：`D:/Winnsen_Structure_Agent_Studio/data/master_v2/variable_trial_20261112/`。以下路径相对此目录。

| 内容 | 运行或文件 | 实际覆盖 |
| --- | --- | --- |
| T0 | `runs/2026-11-12T06-05-30-625Z-T0/` | 60 CAD、101 引用重定向、7 个目标独立打开、保护通过 |
| T1 矩阵 | `runs/2026-11-12T06-16-10-718Z-T1-matrix/` | 左右各 `437×471.25 → 287×200 → 437×471.25 → 537×1000 → 437×471.25`；10 行重建保存、4 次恢复 |
| T1 冷重开 | `runs/2026-11-12T06-55-59-448Z-reopen-3d1c0b07/` | 基准左右零件、变量/配置/属性/几何/工艺、60 CAD 指纹不变；不替代六图纸或 T4 |
| 第一组完整测量 | `runs/2026-11-12T13-17-05-991Z-assembly-measure-diagnostic-3b3fd948/cad/assemblies/case1/measurement-evidence.json` | 108 实例、8 门接口、5778/5778 对、unknown=0；测量成功、工程失败 |
| 左基准 DXF | `runs/2026-11-12T13-56-57-250Z-T2-outputs-708c3284/cad/outputs/L0/flat.dxf` | 473.4×507.65 mm、12 折弯线、2 圆孔；完整 CUT 几何与历史一致，分析在 `dxf-first-output-analysis-1356-20261112/analysis.json` |
| 有内容但排版失败的 PDF | `runs/2026-11-12T14-10-08-649Z-drawing-frame-diagnostic-9d5f724a/cad/outputs/L0/door_left.pdf` | A3、中文可见、两视图及尺寸可见；图框缩为约 0.2 倍、弯注重叠，不可交付 |

第一组测量 SHA256：`13D80A0FC0616BEAC34979C9A8FF4BB02D485EB9730ABE17059E1567C91D9F4A`。独立专家复核在同一 case1 下 `expert-review-native-measurement-20261112.md/.json`。

已确认两列顶门缺上销轴；6 对卡簧/相邻门轴存在错槽交集；16 对铰链片/本门板相交。共 50 个交集体、22 对不同组件，总体积 `16.145468500542 mm³`，均未批准。锁特征为 R2 半圆柱边缘缺口，不能继续把旧完整圆孔检测结果当真实结构失败或通过；仍缺用途及有效配合面确认。加强筋/横梁本次原生闭门检测没有碰撞，不能继续引用旧 bbox 预测为事实。

材料与工艺：左原生为冷轧钢板/云绅材料库；右材料为空，身份 `UNSPECIFIED`。固定 t0.8、R0.2、扣除1.4；`BEND_DED` 仅记录变量、未与原生扣除方程绑定，不宣称可切换工艺。制造方向、DFM 阈值和工厂资格仍未定义/未验证。

## 工程图最新失败与修复点

1. 14:36 `7f0cd415`：`TRIAL_SHEET_SCALE_CHANGED_INDEPENDENT_VIEW`。首视图自动把纸张改 1:5；`SetScale(1,1,false,false)` 又可能把 view 切回跟随纸张。现已显式恢复独立比例/位置，14:45 后读回通过。仍须新 PDF 实际 8 线位置检查。
2. 14:45 `ead5112f`：`ANNOTATION_FONT_READBACK_FAILED`，执行到原生弯注字体设置；12 条原生注释、折弯值和附着已读到。
3. 14:55 `20b3b104`：完整字体日志证明 `SetTextFormat` 请求 2.5 mm 返回 true，但原生弯注仍沿用汉仪长仿宋体 3.5 mm；`HasMultipleFonts=false`，不是 rich font 问题。无 SLDDRW/PDF。
4. 15:05 `5b79f9e9`：保留 3.5 mm 字体后，硬要求全部 `useDocumentFont=true` 导致 `NATIVE_BEND_NOTE_SOURCE_DOCUMENT_FONT_REQUIRED`。trace044 第一条 true/escapement0，trace045 第二条 false/escapementπ/2，字体均正确。无 SLDDRW/PDF。

最新 `TrialOutput.cs` 已改为逐条保留来源 useDoc true/false 和字体，只允许计划中的横排角度变化（target escapement=0/Note.Angle=0）；保留原生 Z，只改 XY/引线；保存完整实际格式供 T4 精确比较。角度、引线、位置每个操作均先后落日志。**本版未编译、未运行；SetLeader/Position/Extent 仍待首次有效执行。**

`GetSheetFormatName` 是结构树显示名，不能据其非空判断旧图框文件引用。外部路径须读 `GetTemplateName` 并核 None 枚举及实际空模板内容。官方本机 CHM 摘录：`official-drawing-api-evidence-20261112-1428/`。

现有 DxfTrace 已支持复制旧日志后续编号，保留旧文件并拒绝覆盖。离线验证在 `trace-resume-offline-final-20261112-223558-211/result.json`；不能重新从 001 覆盖。

## 暂停冻结源码与工具链

源码目录：`D:/Winnsen_Structure_Agent_Studio/workers/master_v2/variable_trial/`。

| 文件 | 当前 SHA256 | 验证状态 |
| --- | --- | --- |
| `TrialOutput.cs` | `335EE316820BA6BCAAA179C68BFB8F0C95DBE874063B02573AFEA0D4A3D3F6FF` | 最新逐条格式/横排修正，未编译/未 CAD |
| `TrialAssembly.cs` | `EA07A7A9FC654F06849D5487FE773032188050778FA2F305CFCD99BB2684DF2F` | 全配置材料新增后未编译/未 CAD |
| `VariableTrial.cs` | `756BBEB8FDF14543916D2E535BC9EA012F95621DA99E623FE16FA0BA1FF50D69` | 15:05 已编译；新 T2/T4 分支未原生验收 |
| `compare_assembly_measurements.py` | `0920CDD6E3EEACE21ED36F40D4A0D64EBC2CD73B4DED063AF0FE2F55C1B57B27` | 执行代理报告当前 84 自测通过，仅聊天工具输出；最新运行归属门禁未完成落盘/public CLI 负测 |
| `audit_outputs.py` | `AA17676CB871EC6B1399568517816BF403CFB75E0C93BF08B58848050AEE316A` | 7 项离线行为测试；实际旧 PDF 8 线 0 匹配，正确拒绝；视觉仍 pending |

`toolchain-current.json` 仍指向 **15:05:07-919Z-8c81fa25**，EXE SHA256 `C77640A2F9860B13786A13AE6A03637D6275C24969F9DB95C49E2FFAED8D22AE`。它归档的是旧 `TrialOutput=929506...`、`TrialAssembly=6010BD...`，与当前源码不同。明天必须统一重新 build；不得直接绕过 `BUILD_CHANGED_REBUILD_REQUIRED` 或使用旧 EXE 冒充新修复。

## T4 新增检查和未完成测试

- `VariableTrial.Properties` 覆盖全部配置材料与自定义属性；属性读取后恢复原活动配置；Get6 只接受状态0/2。T2 manifest 记录保存活动配置，T4 在切配置前核对。
- `ReadReopenedDrawing` 要求当前文档只读且可见，重建后只读检查 A3、420×297、sheet1:1、8线、两独立视图、12原生弯注、原文/方向/几何/字体/位置/引线/附着。不能在 T4 自动排版修复；旧 proof 缺新字段须拒绝。
- `TrialAssembly` 记录每实例完整配置、材料和所有配置属性，读取后恢复配置；全量干涉必须 complete/errors空/unknown0/完整n(n−1)/2，但不要求无碰撞，工程 FAIL 保留。
- 比较器新增 `--purpose relocation`，比较真实内容、来源链与直接复制的源证据字节；原始 Get6 状态/临时配置仍留证，内容比较排除读取瞬态。
- 独立专家查出并已写修复：relocation 必须消费 `reopen.pass/coldSession/count`，并核 job/native/run/toolchain/result/supervision、唯一 cad_owned 和 top-level进程身份；原版比较器可在缺冷开证据时误判通过。**最新硬门禁尚缺充分 public CLI 正负测。**
- 最后完整落盘离线证据 `analysis-relocation-comparator-20261112-225719/validation.json`（9项）、`self-test.json`（72项），覆盖旧 A87E 比较器/6010 C#，不能冒充当前0920/EA07版本。旧 `verify-offline.py` 的 synthetic native/result fixture 未适配新运行门禁，明天需在新目录补齐，不覆盖旧证据。
- 13:17 真实基线自比较六部分、472317 数值差0，但0缓存命中正确返回 `unverified_no_cache_reuse/pass=false`，旧数据缺新 metadata，不能补造覆盖。

## 2026-11-13 续接顺序

1. 先读本交接、REPORT 最新结论和用户已选结构范围；只读检查 CAD/任务/锁及当前源码哈希。不接管已有会话或旧锁。
2. 在新分析目录验证最新比较器 84 自测及 runtime 归属公开 CLI 正负案例；保留所有历史失败。核缺 reopen/cold、native failure、错 PID/start、错工具链、错误 result/supervision、属性瞬态允许、真实材料变化拒绝。
3. `node workers/master_v2/variable_trial/build.mjs` 统一构建当前三 C#，再运行单件工程图诊断。**种子用13:56的 cad；不要用14:10已有 SLDDRW/PDF 的 cad。**
4. 单件成功后读回实际 PDF 8线、中文和尺寸，渲染逐页查看裁切/重叠/引线。普通程序错误继续定位，不扩大重建或关闭未知特征。
5. 完成六例 `execute.mjs outputs`、`audit_outputs.py` 和六 PDF 视觉审查。当前无 `outputs-current.json`，不得补造成功指针。
6. 依用户选择执行三组 `execute.mjs assembly`。对新 case1 用13:17原生基线完整比较缓存路径/耗时；0 hit不能宣称缓存已验证。case2/3照常测量，工程 FAIL 不阻断技术测量完成。
7. `execute.mjs relocate` 新目录、新 CAD 进程冷重开；各 case T3 `assembly-evidence.json` 对应 T4 `relocation-evidence.json`，以 `--purpose relocation` 比较。不能用13:17诊断证据代替新 T3 迁移基线。
8. 汇总完整独立试验包、保护前后对比、版本与耗时、仍需人工/工程处置项，作主代理最终验收。本轮试验尚未完成，不接入工作台。

单件诊断入口（明天重新 build 并确认 idle 后执行）：

```powershell
node --input-type=module -e "import {runProtectedPhase} from './workers/master_v2/variable_trial/run.mjs'; console.log(JSON.stringify(await runProtectedPhase({tag:'drawing-frame-diagnostic',sourceDirectory:'D:/Winnsen_Structure_Agent_Studio/data/master_v2/variable_trial_20261112/runs/2026-11-12T13-56-57-250Z-T2-outputs-708c3284/cad',job:{action:'drawing-diagnostic',production_release:false}})))"
```

捆绑 Python：`C:/Users/Administrator/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe`。已有 pdfplumber/pypdfium2，无需安装依赖。当前无活动 CAD、测试或需要继续等待的终端；不建立明日自动任务，等用户继续。
