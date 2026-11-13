# WeightReadback 离线交接

此目录是四对重量属性差异的临时诊断实现及独立构建适配器（temporary diagnostic adapter）。尚未连接、启动或附着 CAD；不表示重量差异已经闭环。诊断完成后应移除或归并此实现，避免双源漂移。`run.mjs` 由主代理独占，不属于本文件的验证范围。

入口兼容 .NET Framework 4 的本机 `csc.exe`：

```csharp
WeightReadback.Run(ISldWorks sw, string cadRoot,
    Dictionary<string, object> job, Action<object> progress = null)
```

调用方须建立全新、独占且开始时没有文档的受控 CAD 会话，并维持 `VariableTrial.RuntimeOwnership()` 的进程及共享锁合同。工具不创建、附着、退出 CAD，也不更改可见性、`UserControl`、账号或应用设置。工具会核对同一 PID、启动 ticks 和 EXE。`progress` 在引用重接与闭包完成后，以及每目标开始、完成时调用；回调异常会令返回结果失败，不会继续下一目标。主代理用现有 `Record` 持续落盘即可。

`job.weightTargets` 的数组顺序就是本轮执行顺序。下一轮由调用方建立新副本、新进程并反转数组。每轮必须恰好有以下 8 项：

| origin | identity | relativePath |
|---|---|---|
| T3/T4 各一项 | crossbar_R_3 | T3 或 T4/case1/门框 横隔板R.SLDPRT |
| T3/T4 各一项 | shelf_R_2 | T3 或 T4/case1/箱体横层板R.SLDPRT |
| T3/T4 各一项 | structure_1000_2 | T3 或 T4/case1/门框 右.sldprt |
| T3/T4 各一项 | crossbar_R_2 | T3 或 T4/case3/门框 横隔板R.SLDPRT |

每项还必须有 `sourceSha256`，表示进入本轮新副本、尚未重接引用时的字节。`job.weightNamespaces` 必须恰好是 `T3/case1`、`T3/case3`、`T4/case1`、`T4/case3`，每项提供 `relativeDirectory` 和对应的原 `structure` 绝对目录 `originalDirectory`。原目录必须互不重叠，且与新 `cadRoot` 完全分离。所有输入只允许 `.SLDPRT`/`.SLDASM`，必须属于唯一 namespace；不会按同名文件猜配。

流程和证据边界：

1. 先核所有复制文件与对应原文件的 SHA，以及 8 个入场目标 SHA。收集全体 CAD 依赖，所有依赖必须在本 namespace 的原根或复制根内且有精确相对映射；外部路径、跨 namespace、缺件、歧义均立即失败。全部依赖预检完成后才开始修改新副本的引用。
2. 仅在新副本调用 `ReplaceReferencedDocument`，记录每次重写前后 SHA。再核实际依赖集合完全一致，并建立重写后的全体只读 SHA 基线。此后目标打开校验使用该基线；不会错误地继续使用重接前的 `sourceSha256`。
3. 每目标以 `Silent | ReadOnly` 打开。核实际完整路径、只读状态、两个配置名和 SHA。只允许目标依赖闭包内的已加载文档，均要求只读；只登记本轮空会话开始后由本次打开产生的这些文档。完整枚举后才汇总拒绝外来文档、身份或只读错误，确保排在外来文档之后的允许身份仍被登记；外来文档不被登记、关闭。
4. 在任何非缓存属性读取、显式配置激活或重建之前，先对文档级、`Default` 和 `DefaultSM-FLAT-PATTERN` 的“重量”调用 `Get6("重量", true, ...)`，保存 `cachedWeightBaseline`。随后保留原有两遍 `Get6(..., false, ...)`；每次读取都保存 raw/resolved/status/statusName/wasResolved/linked 和前后活动配置。文档级“重量”不存在可保留其真实状态；配置级缺失仍令诊断失败。缓存与非缓存值仅提供对照证据，不预判差异根因。
5. 通过同一配置确保函数核对目标配置对象存在、活动文档完整路径、`ISldWorks.IsSame` 身份、文档仍在会话且只读。若目标已经 active，则跳过 `ShowConfiguration2`，只核活动配置前后一致；若确实需要切换，则保留 API 原始布尔返回，必须为 `true` 且切换后活动配置精确等于目标，`false` 即使活动状态碰巧到达目标也失败。随后 `ForceRebuild3(false)` 后读首次和重复属性，再读独立质量和属性。flat 配置重复同一流程。最后用同一函数确保恢复 Default；不保存。
6. 独立质量来自 `CreateMassProperty2`，强制该质量对象使用 SI，并包含隐藏实体后 `Recalculate`。读取 kg、m³、kg/m³、三个覆盖标志与质量覆盖原值、文档质量/体积/长度/小数位/单位制偏好。质量覆盖原值只有 `OverrideMass=true` 时生效；本次不展开重心或惯量数值。没有调用材料、密度或质量覆盖的 setter。`Body2.GetMassProperties(1)` 只用来取得实体体积，密度参数 1 不是实际材料密度。
7. 清理先关闭当前目标，再关其已登记依赖；关闭前核完整路径和 COM 身份（应用返回的文档实例标识），标题必须唯一。只在完整枚举确认某身份已消失后释放其登记；关闭无效、标题冲突或枚举不完整时保留尚未关闭的身份和闭包，供最终清理重试。遇到外来文档会保留并失败。关闭后核同一进程和空文档集合；每目标及最终核原文件和重写后副本 SHA。初始缓存基线未采完时，不执行配置恢复。

API 失败会记录具体 error；对应缺失值保持 null/缺失，不使用 0 或默认值代填。返回的实际 0 数值仍原样保留。安全、身份和闭包错误立即中止；普通读 API 错误允许保留其他可读项，但 `readbackComplete=false`。`success` 表示本轮诊断所需读取完整，不表示两轮相等、重量正确或制造认可。所有验收、全计划及生产放行标志始终 false。

SW2020 的 `swUserUnitsType_e` 只有长度与角度，因此不调用不存在的 mass `GetUserUnit(2)`。记录 `userUnitMassApi.supported=false`，并通过真实文档偏好 `swUnitsMassPropMass` 读取质量单位；枚举只支持已核实的 mg、g、kg、lb 到 kg 换算，未知返回 null 并标错。raw/resolved 的显示格式不重新解释或替换。

质量一致性仅是诊断：本机官方帮助说明混合密度时 `MassProperty2.Density = Mass / Volume`。所以密度乘实体体积即使吻合，也不能单独证明材料密度正确；必须同时看质量覆盖、焊缝质量、材料名和源数据。未把该比较用作旧 comparator 的豁免。

独立构建不修改根 `VariableTrial.cs`、`TrialAssembly.cs`、`TrialOutput.cs`，也不写 `toolchain-current.json`：

```javascript
import { buildWeightToolchain } from './build.mjs'
const { manifest, sourceRoot, provenancePath } = buildWeightToolchain()
```

每次在现有 `DATA/toolchain/weight-<时间>-<随机值>` 下创建 `generated-inputs` 源束，复制根三份 C# 及当前 `WeightReadback.cs`。只对生成的 `VariableTrial.cs` 作两处 exact-one（必须且只能匹配一次）受控适配：增加 `weight-readback-diagnostic` action 与专用最终清理分支。原其他动作及清理块保留原字节，进程激活、所有权、共享锁和退出等待协议沿用原 Main。使用原有 x64 .NET 编译器及 SW2020 interop，清单通过现有 `validateToolchain(manifest, { sourceRoot })` 校验。`adapter-provenance.json` 保存原根源码 SHA、生成源码 SHA、替换块前后 SHA 和可执行文件 SHA；临时源束保留在该独立目录内。

专用 action 把进度记录为 `weight_readback_progress`，最终结果记录为 `weight_readback_diagnostic`。只有结果是字典且 `success`、`readbackComplete` 均为布尔 `true` 才继续成功；缺失、字符串、整数和 false 均失败。其最外层 finally 只枚举、记录剩余文档，校验枚举数量与 `GetDocumentCount()` 一致且均为空才调用 `ExitApp`；不按 `cadDirectory` 范围补关文档。残留任何文档都会失败并保留会话。

调用者应将返回的清单和源束显式传给现有执行器：`runNativeJob(job, dir, { ...options, toolchainManifest: manifest, sourceRoot })`。本构建不会更新或临时替换全局工具链指针。

离线验证（只编译与托管假接口，不运行生成的原生诊断程序）：

```powershell
& workers/master_v2/variable_trial/mass_readback/test-offline.ps1
```

脚本把本实现及 `WeightReadback.OfflineTests.cs` 与本机 SW2020 interop 隔离编译到此目录的唯一临时文件夹，验证路径、哈希、单位、文档所有权及属性缓存顺序。所有假接口由 .NET `RealProxy` 在托管内存中提供，并检查 `Marshal.IsComObject=false`；仅读取离线测试自身的进程身份以经过原 Guard，不创建或连接真实 COM。`RuntimeOwnership()` 默认抛错，仅在这些受控假接口用例中临时允许。文本 fixture 使用 `.fixture`，不创建 CAD 模型；自身临时测试目录在结束时清理。

随后运行 `node --test build.test.mjs`：验证两处精确替换、其他原字节可恢复、重量清理无宽关闭路径；抽取实际生成的 action 和专用 finally，编译托管测试程序验证 18 个行为断言；最后编译独立适配工具链并验证清单、来源证据、根三份 SHA 和 current 指针未变。该独立工具链和证据保留供复核，不运行其 `VariableTrial.exe`。只需编译而不执行测试可运行 `node build.mjs`。

接口依据是本机 `D:\soildworks2020\SOLIDWORKS\api\redist\SolidWorks.Interop.sldworks.dll`，版本 `28.5.0.78`，以及 `api\sldworksapi.chm`。已核 `CreateMassProperty2`、`UseSystemUnits`、`Recalculate`、三个覆盖标志及质量覆盖 getter、文档单位偏好和 `Body2.GetMassProperties` 返回数组。官方网页尝试返回 HTTP 403，使用了本机官方 CHM 补证。CHM 提示后台 `UserControl=false` 的会话关闭唯一文档可能退出；本实现不预先修改会话设置，发生时按身份失效停止，由主代理结合实际运行处理。

未验证：缺真实 CAD 两轮新进程运行、派生依赖的只读打开行为、覆盖 getter 在本批文件的实际返回、跨轮属性/独立质量比较，以及受影响整装配的完整属性补证。
