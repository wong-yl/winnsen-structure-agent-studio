# right_identity_v1：新右板身份警告的只读隔离诊断

临时诊断准备，供主代理审核后独占执行。本目录离线检查不能证明 CAD 运行结果，不启用或发布新版。原始失败 attempt 和 precision3 冻结源码均只读。

固定来源是 `PARAM-20261110111643911-CE439B`。本次 `locks` 原生成功，L4/R9 几何及内存镜向引用 `[3,3]` 已留证；下一独立会话 `locks-verify` 左板打开 warnings=0，右板打开 warnings=33。安装包的 `swFileLoadWarning_e` 中 1=IdMismatch、32=NeedsRegen，所以 33 含两者。原 Open helper 在 actual path 检查前因 bit1 停止；现有失败不等于已证明返回了错误路径。

直接责任链：precision3 `NativeParametricLocks.cs:129` 保存参数源，`:135` `MirrorPart2(false)` 保留关联，`:155-159` 在临时右板最终保存前检查引用，`:161` 保存临时右板，`:167-168` 按标题关闭文档，`:172` `File.Replace` 后没有重开。`:27/31/52` 在每个阶段内按左→关闭左→右的顺序读取，但没有记录关闭左后的残留文档。`NativeParametricAssembly.cs:318/360-361` 每阶段一个会话，`:449-458` 记录原始 warning、检查 bit1 后才检查实际返回路径。本工具不改这些文件和门禁。

三个串行诊断臂，每臂一个独立的新 CAD 会话：

1. `right-only`：直接只读打开新右板。
2. `parameter-source-only`：直接只读打开新右板参数源。
3. `left-close-right`：只读打开左板，按标题调用 `CloseDoc`，记录残留文档，再只读打开右板；中间不使用 `CloseAllDocuments`。

所有 `OpenDoc6` 固定 part type=1、options=3，禁止 rebuild/save/replace。记录每次打开前后的 loaded documents（当前加载文档）、实际路径/标题/只读状态/SaveFlag（内存是否待保存）、原始 errors/warnings、文件及特征外部引用和状态、`GetDocumentDependencies2` 的原始返回。返回的未知或越界引用只报告，不加载、不加入允许来源、不放行验收。特征仅遍历当前相关零件，不遍历整机装配；遍历去重的 IUnknown（COM 对象身份指针）保持到整个作用域结束，避免释放后地址复用。

三臂可以证伪的假设：若 `right-only` 也带 bit1，“必须先开左板才触发”的说法即不成立；若参数源单独打开也带 bit1，应继续定位参数源及其祖先引用；若参数源无 bit1 而右板带 bit1，优先检查镜向保存后的引用身份配对；若仅 `left-close-right` 带 bit1，再结合关闭后残留清单定位会话内依赖冲突。单次结果不证明最终根因，不构成整机验收。

运行复用项目 `dataDir` 的全局 worker lock（独占锁），不使用私有锁替代；串行 heartbeat（续期），每臂前再次只读检查 portal（任务入口）active 状态及任何非空 lease（任务持有权），不创建 task 或 attempt、不写旧任务。全局保护按成熟 runner 的保护路径及整个 PROBE 重新采当前字节，包括最新 PROGRESS；旧保护清单只供路径，不把其旧哈希当作本次基线。

observer（外部进程观察程序）采 PID、启动时间、创建时间、路径等精确身份。C# 在首次 OpenDoc6 前落 `session_owner` 并持有操作系统进程句柄，防止 PID 复用。每臂诊断期限180秒，自然退出等待45秒；超时落 `timeout/manual_disposition_required` 并停止后续臂，不杀 CAD 或未确认进程。runner 保留全局锁、继续心跳和等待人工处置。只有已外部目击、父 CAD 已自然退出且父进程已不存在的同源孤立 `sldProcMon` 才可按原成熟 observer 的精确身份方式处理。

observer 缺少结束报告或子进程身份未确认时，runner 同样保锁；若只有 PID，就只观察该 PID 是否仍存活，缺少 PID 或进程状态无法读取时继续等待。不会按未确认身份终止任何进程。每次预检同时核对静态检查所覆盖的脚本版本，避免后改脚本沿用旧报告。

离线准备命令（实际项目根目录）：

```powershell
Set-Location -LiteralPath 'D:\Winnsen_Structure_Agent_Studio'
powershell.exe -NoProfile -NonInteractive -File data/indoor_workbench_repair_20261110/obsolete_door_probe/right_identity_v1/build.ps1
powershell.exe -NoProfile -NonInteractive -File data/indoor_workbench_repair_20261110/obsolete_door_probe/right_identity_v1/static-check.ps1
node --check data/indoor_workbench_repair_20261110/obsolete_door_probe/right_identity_v1/run-right-identity.mjs
node data/indoor_workbench_repair_20261110/obsolete_door_probe/right_identity_v1/run-right-identity.mjs --dry-run
```

build 不重复覆盖已生成的 build.json。编译的 CS0649 是 JSON 反序列化字段未在源码直接赋值的警告，不是 CAD 打开警告。静态检查反射编译文件的 IL（实际指令），核对 CAD API 白名单及无模型修改调用；只调用纯方法/只读文件绑定，不执行 Main。Python 语法用 `ast.parse`，避免生成目录外缓存。

主代理审核 freeze-manifest 后才可用同一 runner 的 `--run-native`。执行者本次绝不启动 CAD。所有报告保持 `modelAccepted=false`、`operationComplete=false`、`strictColdAcceptancePassed=false`；原 Open 门禁、结构、生产/default profile、portal 及室外 watcher 均不改。
