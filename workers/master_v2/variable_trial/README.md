# 16029 独立变量试验运行说明

本目录用于在独立副本上验证门板变量、展开输出、最小装配和换目录重开。它不是正式订单生成入口，也不构成生产放行；原生结果中的 `production_release` 必须始终为 `false`。

当前状态（2026-11-13）：六例门板展开几何检查通过；T3三组最小装配测量齐全，结构验收均未通过。T4 `f58333e9` 已在新目录、新CAD进程完成六门板、六展开及三组装配读回，原生/监督正常结束、保护通过；最终复比较中case2测量与文档属性一致，case1/case3因4处重量属性读回差异拒绝，迁移整体仍未通过。门板变量及展开限定范围值得继续，整装配推广仍需原结构整改和稳定属性补证，不接正式工作台、不生产。图纸/PDF按用户要求暂缓，`fullPlanPass=false,production_release=false`。当前证据以[今日报告](../../../data/master_v2/variable_trial_20261112/REPORT_全局变量试验_20261113.md)及对应运行目录为准，以下2026-11-12内容为历史冻结记录。

截至 2026-11-12，本任务已有通过证据覆盖 T0、T1 和独立 T1 重开；T2/T3/T4 尚未通过。本次文档纳入的最后一个已结束原生运行是 `2026-11-12T15-05-22-423Z-drawing-frame-diagnostic-5b79f9e9`，末事件为 `2026-11-12T15:09:53.3685206Z`：14:55 的 2.5 mm 字高请求返回 true，但实际仍为 3.5 mm 文档格式；15:05 保留原生字体后又因强制所有弯注使用文档格式而失败，实际各注源格式不同。两次均无 SLDDRW/PDF，不能作为 T2 通过；14:55 监督有 1 次进程查询警告并已恢复。14:10 的 L0 PDF 保存虽为 `true/0/0`、中文可见且有文字提取，图框缩在左下、弯注重叠仍未通过，新增实际 PDF 八线检查匹配 0/8。13:56 的新 L0 DXF 已取得修复后真实导出及完整历史几何仅平移相等证据，折弯方向仍未通过、六例仍未完成。13:17 的 case1 完成 108 实例、8 组接口和 5778 对测量、0 unknown，仍有 50 个未批准交集体（专家归并为 22 对）、顶门轴支撑缺项及锁特征检测问题，T3 未通过。用户已选择保留原结构继续完成三组测量；截至本次冻结尚未全部执行完成。完整证据见[隔离试验报告](../../../data/master_v2/variable_trial_20261112/REPORT_全局变量试验_20261112.md)。下面列出实际入口和运行方法，每次运行仍以该次目录中的结果和检查证据为准。

2026-11-12 用户要求暂停，计划 2026-11-13 继续。本次只冻结上述已结束运行及对应离线证据；后续源码修改尚未编译或原生执行，不沿用旧版本的通过结论。

2026-11-13 用户明确“图纸标注先别管，等以后处理”“能展开就行”，因此新增独立的 `outputs-dxf` 分支，只生成六例原生门板和 DXF。工程图/PDF 记录为 `drawingStatus="deferred_by_user_20261113"`；旧 `outputs` 失败、旧 PDF 缺项及上述历史记录均保留。分支是否实际通过，以新运行自身的原生结果和审计为准；`fullPlanPass=false`、`production_release=false`，展开几何检查不能代替方向、完整方案或生产验收。

## 2026-11-13 展开与第三组续验

六例实际门板/DXF已导出，完整轮廓与孔集合审计通过；原导出批次的监督失败永久保留，方向、工程与生产结论仍为 false。当前结果见 `data/master_v2/variable_trial_20261112/REPORT_全局变量试验_20261113.md`。图纸/PDF继续暂缓。

失败T3中的case1/case2已完整测量，技术读回成功、结构验收失败。专用入口在新副本按完整文件指纹复用这两组，只生成case3；来源布局、原生事件、工具链、保护与退出证据必须同时吻合。旧失败目录不删除、不改写：

```powershell
node workers/master_v2/variable_trial/recover.mjs resume-assembly <原六例DXF运行目录> <六例几何审计JSON> <两组完成且case3宽度失败的T3目录>
```

新T3技术续跑正常结束后，迁移到新目录、新CAD进程重开：

```powershell
node workers/master_v2/variable_trial/recover.mjs relocate <同一原六例DXF运行目录> <同一六例几何审计JSON>
python -B workers/master_v2/variable_trial/compare_resumed_relocation.py <新T3副本/cad/assemblies/caseN/assembly-evidence.json> <T4副本/cad/assemblies/caseN/relocation-evidence.json> --out <新比较报告.json>
```

三组分别运行比较。case1/case2的基线参数必须指向新T3内保留原字节的证据；包装器通过两跳复制、重接回执及原始证据追溯真实工具链，原比较器保持固定指纹。case3使用原比较规则。命令成功、技术测量成功、工程通过和生产放行分别记录，`production_release=false`不变。

实际T4 `runs/2026-11-13T05-08-41-897Z-recovery-T4-f58333e9` 于2026-11-13 15:00:05（中国时间）结束，原生耗时1小时50分49.527秒；真实result为code=0、native finished success/cadExited=true、监督cad_exited/cad=[]/warnings=[]，保护pass=true/changed=[]，仅索引合法rebuiltAt变化。主CAD27564及monitor29884自然退出，没有运行监控处置脚本。三组冷重开读回均完整，结构验收仍false。

三组几何六部分相等，毫米读数最大差0、变换最大差 `2.220446049250313e-16`；case2属性相等。case1的 `crossbar_R_3`、`shelf_R_2`、`structure_1000_2` 与case3的 `crossbar_R_2`，Default“重量”属性resolved分别为 `0.00→208.30`、`0.00→1457.78`、`1913.32→0.00`、`184.23→0.00`。这些是表达式读回字符串，Get6（自定义属性读取接口）单次读取不证明实际质量；根因未验证，不能忽略该字段或填入非零值。首次比较 `analysis-T4-relocation-20261113-150112-6b32bf6d` 失败保留：来源包装器漏计12个新flat-reopen记录，且真实重量属性差异仍在；来源包装器及测试已作最小修正，固定比较器未改，重量resolved比较未放宽。

[最终迁移汇总](../../../data/master_v2/variable_trial_20261112/analysis-T4-closeout-20261113-a972db6e/closeout.json)绑定三组真实复比较：来源校验均verified/errors=[]；case2迁移测量与属性一致、pass=true，case1/case3仅因上述属性差异拒绝，T4整体false，三组原结构验收仍false。来源校验限于同次诊断trace、原生结果和清单内容绑定及比较期间不变，不具有整条证据链的独立签名证明。[最终包装器验证](../../../data/master_v2/variable_trial_20261112/analysis-T4-closeout-20261113-a972db6e/wrapper-test-verification.json)记录49/49离线行为测试、244.554秒，绑定最终包装器`5E65DD3D…C4BBE2E`与测试`6DDDD613…55D6225`；完整合并工具输出已归档。文档代理只核证据和SHA，未重跑程序、编译或CAD。

下一步只补必要证据：四对源/目标零件各作两次独立进程只读冷读和独立质量核对，再补受影响装配的完整属性证据；未变化且仍适用的几何证据可复用。工厂可制造性阈值尚未定义，右门材料与制造方向未确认，原结构缺陷未批准整改。当前不具备整装配推广、正式工作台接入或生产资格。

`probe_structure.mjs`是一次性下框A/B诊断入口，归档证据保留；它不发布阶段指针。`close_owned_monitor.ps1`仅执行用户已持续批准的本试验残留监控处置，要求主CAD已退出、进程身份与监督记录完全绑定，保存前后证据；不处理其他进程。

## 路径和环境

以下命令在 Windows PowerShell 中运行，工作目录固定为：

```powershell
Set-Location -LiteralPath 'D:\Winnsen_Structure_Agent_Studio'
$trialData = 'D:\Winnsen_Structure_Agent_Studio\data\master_v2\variable_trial_20261112'
```

| 用途 | 路径 |
| --- | --- |
| 程序和默认输入 | `workers/master_v2/variable_trial/` |
| 独立试验数据 | `data/master_v2/variable_trial_20261112/` |
| T0 的只读来源 | `data/parametric_attempts/PARAM-20261107121230852-8D5DE5/package/` |
| 共享锁（防止多个 CAD 作业同时占用资源） | `data/native_model_requests/.native-worker-instance.lock/` |

需要已安装的 Node.js、Python、SOLIDWORKS 2020，以及 Windows 自带的 .NET Framework 编译器。Node.js 运行器和 `dxf_check.py` 使用标准库；`audit_outputs.py` 和 `audit_pdf_probe.py` 另依赖本机 Codex 已捆绑的 `pdfplumber`（读取 PDF 文字及页面）和 `pypdfium2`（渲染 PDF）。本次没有安装新依赖，普通 `python` 命令不一定能导入这两个包，PDF 检查请使用下文指定的捆绑 Python。当前原生入口核对的 CAD 路径为 `D:\soildworks2020\SOLIDWORKS\SLDWORKS.exe`，版本主号必须为 28；生成工程图还需要 SOLIDWORKS 中已有的默认工程图模板。

先做只读检查：

```powershell
node --version
python --version
Test-Path -LiteralPath 'D:\soildworks2020\SOLIDWORKS\SLDWORKS.exe'
Test-Path -LiteralPath 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
Test-Path -LiteralPath 'D:\soildworks2020\SOLIDWORKS\api\redist\SolidWorks.Interop.sldworks.dll'
Test-Path -LiteralPath 'D:\soildworks2020\SOLIDWORKS\api\redist\SolidWorks.Interop.swconst.dll'
node .\workers\master_v2\variable_trial\run.mjs check
```

`check` 检查进程查询是否可用、是否存在 CAD 进程或活动正式任务，并输出来源 CAD 文件数和 `compiler` 布尔值。它不启动 CAD，不取得共享锁，也不验证许可证、默认工程图模板或已编译程序；`compiler: false` 需要先解决。

工具链（本次使用的原生程序、接口 DLL 和 C# 源文件集合）由 `toolchain-current.json` 指定。可单独校验其 SHA256（文件字节指纹）：

```powershell
node --input-type=module -e "import {DATA,read,validateToolchain} from './workers/master_v2/variable_trial/common.mjs'; console.log(JSON.stringify(validateToolchain(read(DATA + '/toolchain-current.json')), null, 2))"
```

清单必须为 `winnsen.variable_trial.toolchain.v2`。校验同时检查当前全部 `.cs`、归档的原始源字节、`VariableTrial.exe` 和两个指定 DLL；文件集合或指纹变化会拒绝运行。缺少清单、首次使用或 C# 修改后，可在没有其他试验正在运行时重新编译：

```powershell
node .\workers\master_v2\variable_trial\build.mjs
```

该命令不启动 CAD，会在 `toolchain/<新时间戳和随机后缀>/` 归档源文件、编译并写入清单，成功后更新 `toolchain-current.json`。不要手工修改清单来绕过校验。

运行器另已加入每次启动前的版本留证：校验当前工具链后，将完整清单写入该次 `run/toolchain.json`，由 `job.json.toolchainPath` 指向它，内容包含本次源码、EXE 和 DLL 版本。已有同名快照会拒绝覆盖；旧运行没有该文件时不补造，仍按既有原始证据复查。

## 串行运行

下面的 CAD 命令必须逐条执行；仅上一条正常退出且输出 `pass: true` 后继续。运行前保存个人文档并正常退出已有 SOLIDWORKS；不要并行运行其他柜体作业。

| 命令 | 来源 | 成功后更新的指针 |
| --- | --- | --- |
| `run.mjs prepare` | 固定的只读来源 | `T0-current.json` |
| `run.mjs variables` | `T0-current.json` | `T1-current.json` |
| `run.mjs matrix` | `T0-current.json` | `T1-current.json` |
| `run.mjs reopen` | `T1-current.json` | `reopen-current.json` |
| `execute.mjs outputs` | `T1-current.json` | `outputs-current.json` |
| `execute.mjs outputs-dxf` | `T1-current.json` | `outputs-dxf-current.json` |
| `execute.mjs assembly` | 有效 `outputs-current.json` 优先，否则有效 `outputs-dxf-current.json`，再否则 `T1-current.json` | `assembly-current.json` |
| `execute.mjs relocate` | `assembly-current.json` | `relocate-current.json` |

这里的指针是记录成功运行目录的 JSON 文件，均位于 `$trialData` 下。`variables` 和 `matrix` 分别从 T0 创建副本，不是先后修改同一份 CAD；两者都会覆盖 `T1-current.json`，因此需要矩阵证据时应保留 `matrix` 返回的目录。

```powershell
node .\workers\master_v2\variable_trial\run.mjs prepare
node .\workers\master_v2\variable_trial\run.mjs variables
node .\workers\master_v2\variable_trial\run.mjs matrix
node .\workers\master_v2\variable_trial\run.mjs reopen
node .\workers\master_v2\variable_trial\execute.mjs outputs
node .\workers\master_v2\variable_trial\execute.mjs assembly
node .\workers\master_v2\variable_trial\execute.mjs relocate
```

`prepare` 复制、改接副本引用并检查目标；`variables` 绑定左右门板变量；`matrix` 在左右门板上依次检查 `437×471.25 → 287×200 → 437×471.25 → 537×1000 → 437×471.25 mm`，包含尺寸恢复。`reopen` 在新副本、新 CAD 会话中检查 T1 保存结果。

`outputs` 生成六个左右门板用例及原生 DXF、工程图和 PDF；`assembly` 尝试生成三个最小装配用例；`relocate` 再复制到新运行目录，检查六个输出门板、工程图和三个装配的换目录重开。要完整执行 `relocate`，应按上面的 `outputs → assembly → relocate` 顺序取得所需产物。最小装配采用明确位置固定组件，不代表已建立运动配合或完成整机工程验收。

当前用户暂缓图纸时，独立入口为：

```powershell
node .\workers\master_v2\variable_trial\execute.mjs outputs-dxf
node .\workers\master_v2\variable_trial\execute.mjs assembly
node .\workers\master_v2\variable_trial\execute.mjs relocate
```

`outputs-dxf` 的任务必须同时包含 `action="outputs-dxf"`、`outputScope="parts_dxf_only"`，成功只更新 `outputs-dxf-current.json`。输出清单使用 `16029-variable-output-manifest/v2`，必须完整包含六例、`drawing=null`、同一暂缓标记和 `production_release=false`；`parts_dxf_pdf` 的完整图纸分支仍使用 v1 和原指针。装配/迁移从来源清单读取实际范围，v2 仅跳过明确暂缓的工程图；没有清单的 T1 装配不宣称六例输出已完成。两类指针并存时，有效完整输出优先；失效指针不会盖过有效 DXF 来源。

另有 `drawing-diagnostic` 故障定位动作：09:58、11:03、11:41、12:01 四次已结束诊断均复制 08:34 T2 运行中已有的 L0，仅重试其工程图，不重新完成六个输出用例。`pdf-diagnostic` 和 `pdf-cjk-diagnostic` 则用既有工程图副本做导出条件对比，纸张格式和文字改动仅在内存中进行，不保存回原生输入文件。诊断事件明确记录 `stageAcceptance=false`，PDF 内容仍需独立检查；诊断正常退出、部分文件落盘或尺寸读回均不能替代 T2 阶段验收。

`run.mjs` 仅接收上述一个模式参数；`execute.mjs` 支持以下位置参数，没有 `--spec` 或 `--process` 开关：

```text
node execute.mjs <outputs|outputs-dxf|assembly|relocate> [specification.json] [process.json]
```

例如，在仓库根目录显式使用默认输入：

```powershell
node .\workers\master_v2\variable_trial\execute.mjs outputs .\workers\master_v2\variable_trial\inputs\specification.json .\workers\master_v2\variable_trial\inputs\process.json
```

执行前会校验输入。当前门板试验范围为宽 `287–537 mm`、高 `200–1000 mm`、方向 `L/R`；装配执行入口限定高 `2000 mm`、深 `500 mm`、宽 `900/1000 mm`，并要求合法门高组合。默认输入包含六个门板和三个装配；换目录检查也要求这些数量。不要将此试验范围理解为已验证的全尺寸产品范围。

每个阶段都会新建 `runs/<UTC时间戳>-<阶段标记>-<随机后缀>/`，来源 CAD 先逐文件校验复制到其中的 `cad/`。旧运行目录保留，只有成功指针更新；不要复用旧目录原地重试。`outputs` 产物在该次 `cad/outputs/`，装配在 `cad/assemblies/`。`job.json` 中的 `inputVersions` 记录 T2/T3/T4 使用的规格、工艺文件路径与 SHA256。

运行期间共享锁持续更新心跳，并监督原生程序及 CAD 进程。原生程序结束不代表 CAD 已退出；终端会继续等待 CAD 自然退出，完成保护文件对比后才释放锁。启动前进程查询失败会拒绝启动；运行中查询暂时失败则保锁重试，在 `supervision.json` 的 `warnings` 中记录次数与恢复时间，查询持续不可用时持续等待。不要删除仍在监督中的锁或强制结束进程。`Ctrl+C` 请求协作取消并写入 `cancel.json`，清理和退出仍可能需要时间。

## 离线检查

以下检查不启动 CAD。先确认所选运行已经结束；不要对仍在写入的结果执行验收。

`verify.mjs` 只验证 `matrix` 的 `native-result.json`，检查两侧各五个尺寸行、几何恢复、孔位、特征状态和固定工艺值。它不是 T0、普通 `variables`、T2/T3/T4 的通用验收器。

若 `T1-current.json` 仍指向刚完成的 `matrix`，可执行：

```powershell
$matrixRun = Get-Content -LiteralPath (Join-Path $trialData 'T1-current.json') -Raw | ConvertFrom-Json
$matrixReport = Join-Path $matrixRun.dir ('matrix-offline-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff') + '.json')
if (Test-Path -LiteralPath $matrixReport) { throw '报告路径必须为新文件' }
node .\workers\master_v2\variable_trial\verify.mjs $matrixRun.nativeResult $matrixReport
```

否则应把输入改为保留的矩阵运行目录下的 `native-result.json`。输出报告必须在 `$trialData` 内，使用新文件名保留旧证据；成功输出包含 `pass: true`、`cases: 10`，报告中的 `sourceSha256` 指向被检查结果的字节版本。

DXF 离线检查入口为 `dxf_check.py`。先可运行纯内存自检：

```powershell
python .\workers\master_v2\variable_trial\dxf_check.py --self-test
```

T2 成功后，下面以第一个 `flat.dxf` 为例，使用同一次运行的原生折弯证据检查：

```powershell
$outputsRun = Get-Content -LiteralPath (Join-Path $trialData 'outputs-current.json') -Raw | ConvertFrom-Json
$flatFile = Get-ChildItem -LiteralPath (Join-Path $outputsRun.cadDirectory 'outputs') -Recurse -File -Filter 'flat.dxf' | Select-Object -First 1
if ($null -eq $flatFile) { throw '未找到本次运行的 flat.dxf' }
$dxfReport = Join-Path $flatFile.DirectoryName ('dxf-audit-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff') + '.json')
python .\workers\master_v2\variable_trial\dxf_check.py $flatFile.FullName --native-bends $outputsRun.nativeResult --require-bend-directions --out $dxfReport
```

`--out` 必须是试验数据根目录内某个 `outputs/` 子目录中的全新文件；已有报告、目录重定向或越界写入都会拒绝。省略 `--out` 只向终端输出 JSON。成功退出码为 `0`，审计失败为 `2`；查看 `auditPassed`、`errors` 和 `nativeBendComparison`，不要只统计折弯线数量。上述命令只检查一个文件，六个输出需要逐一检查。

可选的 `--expect-width`、`--expect-height` 指展开外包尺寸，单位 mm，不能直接填门板名义宽高；`--expect-bends` 是预期折弯线数。只有取得独立设计依据后才填写这些数值。默认连接点容差为 `0.01 mm`（`--tolerance-mm`），尺寸容差为 `0.05 mm`（`--dimension-tolerance-mm`）。缺少原生逐线位置和方向证据时，严格方向检查必须失败，不能用图层名或线数补推方向。

`audit_outputs.py` 综合读取已经原生成功的六个 T2 输出，核对 DXF 几何、原生折弯及孔位证据、基准历史几何和 PDF 页面结构，并生成页面 PNG。它不启动 CAD，也不改写 CAD、DXF 或 PDF。使用当前已核对可导入两个 PDF 包的捆绑 Python：

```powershell
$trialPython = 'C:\Users\Administrator\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
& $trialPython -B -c "import pdfplumber, pypdfium2; print('PDF audit imports OK')"
$outputsRun = Get-Content -LiteralPath (Join-Path $trialData 'outputs-current.json') -Raw | ConvertFrom-Json
$outputAudit = Join-Path $outputsRun.dir ('output-audit-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
& $trialPython -B .\workers\master_v2\variable_trial\audit_outputs.py $outputsRun.dir --out $outputAudit
```

输入是包含 `native-result.json` 的已结束运行目录；该结果必须成功退出 CAD，并包含规定三尺寸×左右手的六个用例。`--out` 是试验数据根目录内尚不存在的新目录，脚本创建它并写入总表 `output-audit.json`、每例 `dxf-audit.json`、`pdf-audit.json` 和 `page-*.png`；旧审计目录不得覆盖。基准几何核对还需要本机既有历史 DXF 及证书，不能仅搬走试验目录后假定综合审计仍具备这些输入。

报告生成或命令退出码 `0` 表示审计已执行，不表示所有检查通过。逐例查看总表的 `dxfGeometryPass`、`dxfDirectionPass`、`pdfStructuralPass`、`pdfFramePass`，以及明细中的错误、原生证据和 `matchesNativeSha256`。PDF 被替换后即使文字、纸型相同，指纹不符也不能结构通过；历史轮廓比较也保留圆弧朝向，不能把上下半圆视为相同。

`pdfStructuralPass=true` 只表示程序检查的页数、纸型、文字范围、原生 PDF 指纹及实际 PDF 八条图框线满足条件；图面裁切、重叠、尺寸与标题栏仍需查看生成的 PNG。`visualReview: "pending"` 表示人工视觉检查尚未完成，PDF 明细的 `pass` 仍为 `false`；`production_release: false` 始终保留，综合审计不授予制造或生产资格。

仅 DXF 分支使用同一审计入口，读取独立指针，输出到全新目录：

```powershell
$dxfRun = Get-Content -LiteralPath (Join-Path $trialData 'outputs-dxf-current.json') -Raw | ConvertFrom-Json
$dxfAudit = Join-Path $trialData ('analysis-dxf-only-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
& $trialPython -B .\workers\master_v2\variable_trial\audit_outputs.py $dxfRun.dir --out $dxfAudit
```

审计只在 `job.json`、原生动作与首个范围事件、v2 完整清单、六例逐一暂缓事件及 `drawing=null` 全部相符时跳过 PDF。少 scope、错 schema、混入图纸或指纹不符均拒绝；完整 v1 缺图纸或 PDF 仍失败。DXF 的单位、轮廓、折弯线数、原生展开尺寸及基准历史完整几何检查保留；无工程图时，孔位与孔径改由原生 flat 证明中的完整 CUT 几何核对。

准备展开与导出前会分别重新读取原生边界；两份快照的 `modelPath/modelHand/configuration/schema/complete/units/errors`、字段集合、列表长度及顺序、ID、类型、`curveType`、计数、采样状态与 API 等仍精确核对。仅白名单中的几何 double（双精度数值）允许读取尾差：`topZMm`、primitive 的坐标/长度/半径、弧采样的 `pointMm` 最大差 `1e-9 mm`；primitive 的 `parameterMin/parameterMax`、弧采样的 `edgeParameterRange`、`curveParameterDomain.minimum/maximum` 与采样 `parameter` 最大差为曲线原单位 `1e-12`。`topToleranceMm` 及其他未列数字仍精确；所有数值必须有限，NaN/Infinity、缺键、多键、索引互换或超阈值均拒绝，不舍弃任何字段，也不全局四舍五入。

`wholeDictionaryEqual` 保留整份快照的严格相等结果；`identityExact` 表示来源与非白名单字段精确一致，`nativeReadbackEquivalent` 还要求白名单几何数值满足上述读取阈值。明细 `readbackDifferences` 逐项保留路径、前后值、差值、单位、阈值与是否接受；读取等价不改写旧严格结果。最终原生 CUT 与 DXF 的 `0.01 mm` 几何比较不变，上述读取阈值不是制造公差。

v2 将“完整几何等价”与“坐标映射唯一”分开：仅搜索八种单位比例的轴向变换（含镜像）与平移，每个有效候选必须将全部 CUT 直线、圆弧端点和中点、完整圆逐一匹配，不能缩放或只比元素数量。存在完整候选时 `completeCutGeometryEquivalent=true`；仅一个候选才有 `coordinateMappingUniqueInSearchedFamily=true` 和旧严格字段 `verified=true`。对称门板可能有两个完整候选，报告保留每个候选的变换矩阵、行列式（变换是否含镜像的符号）及数量，不任选一个来推定孔身份或方向。

孔几何须在所有完整候选下分别满足原生与 DXF 都恰有两个孔、孔心/孔径匹配且 DXF 索引唯一覆盖全孔，才有 `holesEquivalentUnderAllCandidates=true`。v2 展开几何可据此通过；两候选时旧 `uniqueCentersAndDiametersMatchInFullCutMapping` 仍为 `false`，孔身份、左右手和制造方向均不因此通过。缺孔、孔移动、缩放、圆弧中点或裁剪范围不符均不能通过。原 v1 和 `dxf_check.py` 的严格唯一映射与方向检查不变。

总表的 `dxfGeometryScopePass` 只覆盖上述展开几何范围；`dxfDirectionPass` 继续要求原生逐线位置和方向证明，`dxfTechnicalPass` 要求两者都通过。当前直接 DXF 导出只记录 `UNKNOWN`，缺方向证明时这两个方向相关结论为 `false`，不能改写为通过。暂缓分支的 PDF 字段为 `null`、`pdfCheckSkipped=true`，不生成 PDF 审计或页面图片；`drawingStatus="deferred_by_user_20261113"`、`fullPlanPass=false` 和 `production_release=false` 始终保留。离线测试使用人工构造文件，仅证明工具行为，不能当作真实原生 CAD 证据。

`nativeBatchSupervisionAccepted` 另行报告监督批次结果：只有本次运行已有 `result.json`，其中 `code=0,cadExited=true` 才为 `true`。缺少该成功记录时仍可只读检查原生已结束的六例输出，但该字段为 `false`，展开几何通过不能改写旧监督失败，也不生成成功指针。

`2026-11-13T01-00-00-839Z-T2-outputs-dxf-e377825c` 已有原生六例完成事件，但原 CAD 退出后 PID 被 `git.exe` 复用，旧监督失败记录保留。没有对应成功指针时直接执行 `execute.mjs assembly` 会回退 T1，不能用它代表这批 DXF 的后续验证。专用恢复入口为 `node workers/master_v2/variable_trial/recover.mjs assembly <failed-run> <new-output-audit.json>`，迁移使用相同参数将 `assembly` 换为 `relocate`；它只在来源证据和清单未变、新运行的真实监督与保护检查满足后发布独立 `recovery-T3-current.json` / `recovery-T4-current.json`。未真实跑完前不能记录恢复阶段通过，原运行和旧 FAIL 不回写。

公开审计入口的离线回归测试（拒绝错误输入并检查上述边界）可单独运行：

```powershell
& $trialPython -B .\workers\master_v2\variable_trial\audit_outputs.test.py
```

`audit_pdf_probe.py` 是独立 PDF 对照诊断入口，可后续合入正式审计；它不要求六例 T2 成功，也不授予内容验收。下面以已结束的 13:10 三分支为例，输出放在试验根目录下的新目录：

```powershell
$pdfProbeRun = Join-Path $trialData 'runs\2026-11-12T13-10-12-990Z-pdf-cjk-diagnostic-ae46d1a9'
$pdfProbeAudit = Join-Path $trialData ('pdf-probe-analysis-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
& $trialPython -B .\workers\master_v2\variable_trial\audit_pdf_probe.py $pdfProbeRun --out $pdfProbeAudit --dpi 120
```

输入只接受本试验 `runs/` 的直接子目录，要求 `supervision.json` 为 `state="cad_exited",nativeEnded=true,cad=[]`，原生末事件为 `finished,cadExited=true`；原生失败但已经退出的运行仍可诊断。脚本逐项核对 `probe-result.json` 与 receipt（每次导出的独立 JSON 凭据）一致、PDF 的当前长度和 SHA256 与凭据一致，保存脚本、监督、原生结果、probe、receipt 和 PDF 指纹，并在分析结束时再次核对输入未变。`--out` 必须尚不存在、位于试验根目录内且不在 `runs/`、CAD 或 PDF 输入路径下；已有输出、越界路径、符号链接和 Windows 目录联接都会拒绝。

输出为 `analysis.json` 和各分支 `attempt-*/page-*.png`，包括 API 返回值/错误/警告、页数、`chars/lines/curves/rects`、`fontnames`、逐页文字、PNG 文件 SHA256、RGB 像素 SHA256、尺寸及模式。`--dpi` 默认为 120，只接受 72–300。`requestedNotes` 优先使用新版 `requestedText`，兼容旧 `noteRewrite.notes[].text` 并注明来源；`nativeBeforeNotes` 单独报告全部原生非空备注，不把旧模板文字冒充独立请求。匹配率分别给完整字符串原样匹配及去空白后的完整匹配，仅在单页内匹配；无目标文字时为 `null`，提取不完整时标明阴性结果不确定。解析或文字提取失败单独记录，不能称为视觉失败。

固定脚本 SHA256 为 `FBCF973CD5908C30983683A66211848AC8F1D5AB09512213DEFFBF692976B940`。其[13:10 真实三分支分析](../../../data/master_v2/variable_trial_20261112/pdf-probe-analysis-1310-ae46d1a9-offline-v1/analysis.json)和[12:27 真实十四分支分析](../../../data/master_v2/variable_trial_20261112/pdf-probe-analysis-1227-662c179d-offline-v1/analysis.json)均退出 0、输入指纹未变、全部页面 PNG 及像素指纹复核通过：前者每份 204 字符、1 条线、1 条曲线、中文全文匹配 0/7；后者两组 ASCII 试验备注均完整匹配 7/7，纯线条分支的匹配率为 `null`。[18 项命令行守卫/格式检查](../../../data/master_v2/variable_trial_20261112/pdf-probe-offline-cli-verification-v1/verification.json)及[2 项多页/损坏 PDF 检查](../../../data/master_v2/variable_trial_20261112/pdf-probe-offline-cli-verification-v1/additional-verification.json)通过，其中四分支/七分支兼容检查使用合成输入；随后[13:50 真实四臂分析](../../../data/master_v2/variable_trial_20261112/pdf-probe-analysis-1350-local-font-20261112/analysis.json)由同一版本执行、输入指纹未变，但不替代完整七条中文或六例 T2。所有报告保持 `contentAcceptance=false,visualReview="pending",production_release=false`；退出 0 只表示诊断执行完毕且输入未变。

## 证据和工艺边界

每次运行检查 `native-result.json` 的末尾 `finished` 事件、`supervision.json`、实际生成的 `result.json`（若有）以及 `protection-result.json`。08:34/13:56 T2、09:06/10:23/11:45/12:10/12:44 T3、09:58/11:03/11:41/12:01 四次工程图诊断及 14:10/14:36/14:45/14:55/15:05 图框诊断均没有 `result.json`；其原生末事件为 `success=false,cadExited=true`，不能补推退出码。12:27、12:41、13:02、13:10 四次 PDF 诊断虽有 `finished.success=true` 和 `result.json.code=0`，`stageAcceptance`、`contentAcceptance` 仍为 `false`；13:02 的 `completed=false` 明确记录序列未完成，旧退出状态不改写。12:35 的 T3 在工具链校验时因 `BUILD_CHANGED_REBUILD_REQUIRED` 被拒绝，未启动 CAD；目录没有 `job.json`、`native-result.json`、`result.json` 或 `toolchain.json`，不得补造运行成功或旧版本快照。

上述已结束运行及新增 13:17 装配测量、13:50 本地字体诊断、13:56 T2 和 14:10/14:36/14:45 图框诊断的保护结果均为 `pass=true,changed=[]`，监督结果均为 `state="cad_exited",nativeEnded=true,warnings=[]`；12:35 只代表启动前清理结束。13:17/13:50 的原生末事件均为 `success=true,cadExited=true`，`result.json.code=0`，但诊断及阶段不验收标记保留。保护清单覆盖范围以各次 `protected-before.json` 为准，不能扩大解释为整个工作区无修改。`copy-manifest.json` 记录来源 CAD 的复制字节指纹；编译目录的 `manifest.json` 和 `sources/` 保留工具链版本，具备留证机制的每次运行另以自身 `toolchain.json` 绑定实际版本，旧运行不补造。文件存在或某个程序返回成功，不能替代相应原生重开、DXF、工程图和制造验证。

当前工艺草案为厚度 `0.8 mm`、内半径 `0.2 mm`、折弯扣除 `1.4 mm`，状态固定为 `draft_pending_factory_confirmation`。材料沿用来源模型并记录；缺失时标记为 `UNSPECIFIED`，不能据此宣称材料已确认。

`DOOR_W/DOOR_H/T/BEND_R` 绑定到相应原生尺寸；`BEND_DED` 当前仅记录为全局变量，未绑定原生折弯扣除。原生扣除保持 `1.4 mm` 并接受一致性检查，不能声称修改 `BEND_DED` 就会联动展开。改变输入工艺数值会被当前入口拒绝，不是工厂工艺切换功能。

08:34 T2 的 `geometry-audit.json` 为 `auditPassed=true,manufacturingAccepted=false`，仅覆盖 L0 的 473.4×507.65 mm 展开：CUT 68、BEND 12、MARK 0、两个直径 10.2 mm 完整孔，折弯方向仍为 `UNKNOWN`。该次旧圆弧中点证据不能证明裁剪弧朝向；其后四次工程图诊断没有重新导出 DXF。13:56 的新 L0 已有 `IEdge.Evaluate2` 采样读回及真实 DXF，见下文；未验证：缺严格折弯方向及完整六例证据。

12:01 工程图诊断在原生保存和 PDF 保存前均读回折弯态/展开态轮廓折线数 `110/68`，七条纸张备注全部存在、可见且未悬空；SLDDRW 保存为 `true/0/0`，PDF 仍为 `false/1/0`（返回值/错误/警告）。这些局部读回不代表导出内容完整。

12:44 T3 的 case1 有 108 条成功重命名读回，全部完成定位、固定与原生装配保存（`true/0/0`）；`minimum_assembly_built.componentCount=108`。只读重开记录 `errors=0,warnings=32`，随后 `ReopenAndMeasure` 按 `ActualName==component.Name2` 的 `Single` 查找无匹配而失败，未形成 `instances`、接口测量或干涉结果。该运行在 `2026-11-12T13:01:14.4867644Z` 自然结束，`success=false,production_release=false`；其真实工具链为 12:40:54 版本。已有装配落盘可供后续在新隔离副本中重测，不等于 T3 或工程验收通过。

13:17 的[新副本测量凭据](../../../data/master_v2/variable_trial_20261112/runs/2026-11-12T13-17-05-991Z-assembly-measure-diagnostic-3b3fd948/cad/assemblies/case1/measurement-evidence.json)复用该已保存 case1 的隔离副本，真实绑定 13:09:57 工具链；108 个实例的身份、固定状态和变换读回通过，8 组接口均写出，`reopen.coldSession=true`。干涉枚举为 5778/5778 对全部 measured、0 unknown，原生返回 50 个相交实体，`interference.pass=false`，没有继承任何零件类型或体积豁免；相交实体数不能直接写成不同组件对数。L4/R4 顶门的上轴套均为 `spatiallyOverlappingPinCandidateCount=0`（无正轴向重叠销候选），8 组 `panelLockBoundaryPass=false`；配合候选唯一性、有效面选择、制造阈值、开闭/锁闭全行程仍未验收。`success=true` 仅表示诊断完成，`stageAcceptance=false,acceptancePass=false,production_release=false` 不变，不能填成三组 T3 通过。

[独立专家复核](../../../data/master_v2/variable_trial_20261112/runs/2026-11-12T13-17-05-991Z-assembly-measure-diagnostic-3b3fd948/cad/assemblies/case1/expert-review-native-measurement-20261112.md)将 50 个交集体归并为 22 对、总 `16.145468500542 mm³`：6 对卡簧/相邻门轴共 18 体，16 对铰链片/本门板共 32 体，全部未获批准。8 处锁特征的原始失败标记保留，但其 π 圆弧半圆柱面支持边缘缺口解释，原完整圆孔规则及通用圆柱候选唯一性规则需要区分几何角色；检测问题不证明锁闭合格，也不自动授权改 CAD。2026-11-12 用户已选择保留原结构继续三组测量、T3 如实判未通过；尚未全部执行完成，不修改原件或正式链路。

12:27 PDF 诊断完整执行 14 个分支；既有[独立矩阵分析](../../../data/master_v2/variable_trial_20261112/runs/2026-11-12T12-27-25-444Z-pdf-diagnostic-662c179d/cad/pdf-probe/analysis-full-matrix-20261112-204304/matrix.json)记录：实际原中文的四个路径/导出参数分支，以及去除图纸格式的两个分支，均返回 `false/1/0`，缺门板/孔轮廓、尺寸线和七条中文备注；把七条备注改为 ASCII 的两个分支返回 `true/0/0` 且几何恢复，但标题栏仍有重叠和旧内容。最小 YaHei 中文分支虽返回 `true/0/0`，整条中文连尾随 `123` 都在 PDF 中丢失。以上是既有分析的记录，本次文档整理未重新做图像审查；`contentAcceptance=false` 保留。

12:41 中文 A1/B/A2 诊断仅生成 A1 的一次导出（`false/1/0`）；B 在 `PDF_PROBE_CHINESE_NOTE_FINAL_READBACK_FAILED` 中止，A2 未执行。旧版 `probe-result.json.completed=true` 没有序列数量门禁，不能作为 3/3 分支通过。13:02 使用 12:48 的 AF9 更正版重试，正确记录 `completed=false,chineseSequenceCompleted=false,actualAttemptCount=1`；B 的完整失败凭据显示标题 `CharHeight=0.004 m` 和按米输入模式未变，差异是非输入的 `CharHeightInPts:14→13`。13:09 编译的 `8FC7ADBF…013D38BEC` 仅对 B 按实际输入单位严格比较字高并保留另一单位的差值，A1/A2 恢复和其他格式仍严格；入口增加未完成序列拒绝。

13:10 使用 8FC 版本完整执行 A1/B/A2，`completed=true,chineseSequenceCompleted=true,actualAttemptCount=expectedAttemptCount=3`。三次导出均为 `false/1/0`；既有[三分支独立分析](../../../data/master_v2/variable_trial_20261112/runs/2026-11-12T13-10-12-990Z-pdf-cjk-diagnostic-ae46d1a9/cad/pdf-probe/analysis-cjk-three-arms-20261112-211526/comparison.json)记录每份 PDF 都只有 204 字符、1 条线、1 条曲线，七条中文备注提取 0/7，并记录三张旧 PNG 像素相同。B 的七条输入格式读回和 A1/A2 严格恢复均通过，该分析仍记录门板/孔/尺寸线、图框和中文内容缺失，SimSun 未进入 PDF 字体资源。诊断流程完成不代表 PDF 通过，`contentAcceptance=false` 保留；本次文档未重新做图面验收。

新增固定 CLI 对同一批未变 PDF 的 120 dpi RGB 渲染中，A1/A2 像素 SHA256 相同、B 不同；旧 PNG 与旧比较表则确实记录另一组相同像素 SHA256。新旧分析保留各自指纹，不能再把“三分支像素完全一致”作为不带口径的结论。未验证：缺渲染差异原因和逐页视觉复核；两份分析的中文全文匹配均为 0/7，均不授予 PDF 内容通过。

13:50 的[本地字体四臂结果](../../../data/master_v2/variable_trial_20261112/runs/2026-11-12T13-50-06-381Z-pdf-local-font-diagnostic-11ea4661/cad/pdf-probe/probe-result.json)真实绑定 13:47:54 工具链，`completed=true,localizedFontSequenceCompleted=true,actualAttemptCount=expectedAttemptCount=4`。只保留第 1 条中文孔表，其余六条为 ASCII；仅请求并读回“汉仪长仿宋体”的分支返回 `true/0/0`，622 字符、275 条线、43 条曲线，ASCII 全文提取 6/6，中文仍 0/1。主代理已查看该分支整页 PNG，确认孔表中文可见、两视图恢复，旧图框重叠及旧内容仍在；其余 `Microsoft YaHei`、`HYSWLongFangSong`、`微软雅黑` 三臂仍为 `false/1/0`、204 字符、1 条线、1 条曲线。中文可见不等于全文提取或可搜索通过；该次四臂的历史局限保留。后续 14:10 已有新图框和七条中文的实际输出，但布局未通过，见下文，`contentAcceptance=false` 保留。

13:56 的[新 L0 局部 DXF 分析](../../../data/master_v2/variable_trial_20261112/dxf-first-output-analysis-1356-20261112/analysis.json)绑定已结束 T2 运行：展开 473.4×507.65 mm、CUT 68、BEND 12、两个直径 10.2 mm 完整孔，`auditPassed=true,manufacturingAccepted=false`；完整历史几何仅平移后相等、缺失和多余实体均为 0。原生 C43 采样的起点/中点/终点状态均为 1、`endpointsVerified=true`，补上修复后真实导出证据。随后图框检查报 `TRIAL_FRAME_OLD_FORMAT_OR_REFERENCE_REMAINS`；当次格式路径已为空、模板枚举为 None、模板几何为零，却仍把树显示名“图纸格式1”当作文件残留；[本机官方 API 语义凭据](../../../data/master_v2/variable_trial_20261112/official-drawing-api-evidence-20261112-1428/evidence.json)区分 `GetSheetFormatName` 显示名和 `GetTemplateName` 模板路径。14:09 归档实现已去掉这个显示名条件，保留路径、None 枚举及零模板几何检查。该次 T2 仍失败，折弯方向为 `UNKNOWN`，不能扩展为六例通过。

14:10 的[图框诊断分析](../../../data/master_v2/variable_trial_20261112/drawing-frame-analysis-1410-20261112/analysis.json)对应 L0 新副本，真实绑定 14:09:21 工具链。复制来源自带的 `trace-001..008` 字节未变，新诊断写同名日志时触发 `PRESERVE_EXISTING_OUTPUT` / `DRAWING_DIAGNOSTIC_WRITE_FAILED`；这是诊断日志写入失败，不是 PDF 保存失败。原生 SLDDRW 和 PDF 均为 `true/0/0`，PDF 为 58743 字节；[trace-038 保存凭据](../../../data/master_v2/variable_trial_20261112/runs/2026-11-12T14-10-08-649Z-drawing-frame-diagnostic-9d5f724a/cad/outputs/L0/door_left-drawing.trace-038.json)与文件指纹一致。提取为 632 字符、205 条线、43 条曲线，包含七条中文备注及孔表；主代理已查看[整页 PNG](../../../data/master_v2/variable_trial_20261112/drawing-frame-analysis-1410-20261112/page-001.png)，中文和两视图可见，但图框缩在左下约 1/5，局部折弯注释重叠，提取中的孔表末行也与弯注混行。`stageAcceptance=false,contentAcceptance=false,visualReview="pending",production_release=false` 保留；局部查看不等于完整图面验收。该次日志、图框比例和排版失败保留；14:36/14:45 的后续已结束运行另列如下，不把旧运行回写为通过。

14:36 和 14:45 图框诊断均复制 13:56 L0，来源 `trace-001..008` 字节不变，新日志从 009 续写，未再发生该次重名错误。14:36 的八条原生图框线及格式清除已读回，随后报 `TRIAL_SHEET_SCALE_CHANGED_INDEPENDENT_VIEW`。14:45 的[trace-018](../../../data/master_v2/variable_trial_20261112/runs/2026-11-12T14-45-08-361Z-drawing-frame-diagnostic-ead5112f/cad/outputs/L0/door_left-drawing.trace-018.json)记录 `SetScale(1,1,false,false)` 将折弯视图的 `UseSheetScale:0→1`、比例 `0.2→1`，位置未变；显式恢复后回到独立比例 `0/0.2`。展开视图创建后的 trace-030/031 记录纸张仍为 1:1、折弯视图比例 `0.2784708608091249` 未变，但 `UseSheetScale` 仍需显式恢复为 0。随后[trace-043](../../../data/master_v2/variable_trial_20261112/runs/2026-11-12T14-45-08-361Z-drawing-frame-diagnostic-ead5112f/cad/outputs/L0/door_left-drawing.trace-043.json)读取 12 条有原生附着对象的折弯注释，`Font(note.Annotation,0.0025)` 在 `ANNOTATION_FONT_READBACK_FAILED` 中止，尚无该次 SLDDRW/PDF，弯注排版未完成；这些读回不代替 DXF 严格方向或六例验收。两次真实工具链分别为 14:33:07、14:42:52 归档，不记录当前仍在编辑的 C# 为最终源码。

14:55 的[字体请求读回](../../../data/master_v2/variable_trial_20261112/runs/2026-11-12T14-55-01-379Z-drawing-frame-diagnostic-20b3b104/cad/outputs/L0/door_left-drawing.trace-046.json)绑定 14:54:50 归档：trace-044 原生弯注为“汉仪长仿宋体”、3.5 mm、`useDocumentFormat=true`，trace-045 请求 index 0、2.5 mm、`useDocumentFormat=false`；trace-046 虽 `returned=true`，实际格式和字高仍未变，随后 `ANNOTATION_FONT_READBACK_FAILED`，没有完成布局或保存。[该次监督](../../../data/master_v2/variable_trial_20261112/runs/2026-11-12T14-55-01-379Z-drawing-frame-diagnostic-20b3b104/supervision.json)最终为 `state="cad_exited",cad=[],nativeEnded=true`，但保留 1 次 `PROCESS_QUERY_UNAVAILABLE`（`attempts=1`）：首次 `2026-11-12T15:00:20.085Z`、恢复 `15:00:21.522Z`；不能写成 `warnings=[]`，原生末事件 `15:00:20.0173481Z` 也不是监督释放锁时间。

15:05 的[源字体诊断](../../../data/master_v2/variable_trial_20261112/runs/2026-11-12T15-05-22-423Z-drawing-frame-diagnostic-5b79f9e9/native-result.json)绑定 15:05:07 归档，报 `NATIVE_BEND_NOTE_SOURCE_DOCUMENT_FONT_REQUIRED`。[trace-044](../../../data/master_v2/variable_trial_20261112/runs/2026-11-12T15-05-22-423Z-drawing-frame-diagnostic-5b79f9e9/cad/outputs/L0/door_left-drawing.trace-044.json)中“细节项目2”为汉仪、3.5 mm、`useDocumentFormat=true,escapement=0`；[trace-045](../../../data/master_v2/variable_trial_20261112/runs/2026-11-12T15-05-22-423Z-drawing-frame-diagnostic-5b79f9e9/cad/outputs/L0/door_left-drawing.trace-045.json)中“细节项目3”仍为汉仪、3.5 mm，但 `useDocumentFormat=false,escapement=1.5707963267949`。该版本把所有源注限定为文档格式，不能据此认定第二种原生样式错误；本次在源格式阶段停止，尚未执行后续角度归零、引线或布局。监督 `warnings=[]`、保护 `pass=true,changed=[]`，仍无 SLDDRW/PDF。后续须按逐条源样式核对，并单独记录角度调整的预期差异；当前修正不预记通过。

`audit_outputs.py` 新增实际 PDF 八线图框检查：从 PDF 矢量线（文件里保存的线段）读取纸面毫米端点，每条须在 0.2 mm 读取容差内唯一匹配且八线不复用；该容差不是制造公差，不使用原生草图端点，也不缩放旧图来凑匹配。[旧 14:10 PDF 的真实检查](../../../data/master_v2/variable_trial_20261112/analysis-pdf-frame-gate-20261112-223001/pdf-frame-diagnostic.json)确认输入未变、A3 和文字旧检查满足，但八线唯一匹配 0/8、`pdfFramePass=false,pass=false`，实测线长约为预期 0.2 倍。执行代理记录当前审计脚本与测试版本的 7 项测试通过，涵盖合格八线、缺线、0.2 倍缩小、重线、读取容差和旧有失败/替换边界；本次文档整理核对文件指纹并复用该证据，没有重跑测试。`visualReview="pending",production_release=false` 保留，程序八线检查不代替逐页视觉验收。

装配迁移比较器的[离线验证](../../../data/master_v2/variable_trial_20261112/analysis-relocation-comparator-20261112-225719/validation.json)为 9 项检查通过，[自测](../../../data/master_v2/variable_trial_20261112/analysis-relocation-comparator-20261112-225719/self-test.json)为 72/72，通过范围仅为工具行为，未启动 CAD。比较器版本 SHA256 为 `A87E15C600BF007279E232A18BE917771761F5169891E0DED007DB217B94E2FE`；验证时 `TrialAssembly.cs` 为 `6010BD32…B34E2A`，与 `664DE204…5C8FC08F` 的 14 个既有工程方法内容一致，这不是 13:17 原生测量的生成版本，也不是最终源码冻结。

[13:17 真实旧基线自比较](../../../data/master_v2/variable_trial_20261112/analysis-relocation-comparator-20261112-225719/legacy-baseline-self-comparison.json)的六部分测量相等、472317 个数字最大差均为 0，但命令退出 2、`pass=false,validationStatus="unverified_no_cache_reuse"`；实际缓存命中/复用均为 0，文档属性未记录、覆盖 0/108、`includedInPass=false`。合成迁移正例退出 0、属性差异和复制来源字节变化负例均退出 1，只证明比较器识别这些输入，不能代替真实 T3/T4 迁移、文档属性读回或 CAD 通过。未验证：缺真实 T3/T4 文档属性和换目录原生重开证据。

2026-11-12冻结时点曾缺合格中文PDF、工程图原生重开、完整T2六例逐页内容验收、T3三例及T4迁移重开结果。2026-11-13图纸/PDF已由用户暂缓，T3三组测量与T4原生冷读现已齐全；当前迁移属性差异和工程剩余项以上文及今日报告为准，不撤销旧失败。

未验证：缺工厂确认的材料、折弯工艺、制造阈值、实物试制和生产放行依据。T1 尺寸矩阵或离线 DXF 审计通过，也不意味着 PDF 已逐页视觉验收、全尺寸联动已覆盖或 `production_release` 可以改为 `true`。
