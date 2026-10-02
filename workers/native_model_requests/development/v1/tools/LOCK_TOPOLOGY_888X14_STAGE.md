# 888×14 原生锁拓扑阶段 v2

结论：本目录已改为静态可审计的 `L 原生建槽/孔 → MirrorPart2 临时 R → 只读验证 → 同名事务替换` 路线。本轮只完成源码、编译和无 CAD 自测，没有启动 SolidWorks，也没有生成或修改模型。因此这里不是模型验收结论，只是后续受控任务可调用的阶段工具。

## 已删除的错误路线

V37 最终 `箱体竖隔板R.SLDPRT` 的 7 条镜向引用已经是 `Broken(0)`，不能先更新为 `status=3`。v2 不再：

- 打开或更新旧 R；
- 调用 `UpdateExternalFileReferences`；
- 调用 `BreakAllExternalFileReferences2` / `BreakAllExternalReferences`；
- 要求旧 R 提供 `InContext(3)` 引用；
- 从旧 R 继承任何几何或引用。

旧 R 只作为 `working_pack` 中待事务替换的同名文件存在。

## 固定输入和边界

任务目录形状：

```text
<WINNSEN_NATIVE_16029_ATTEMPTS_ROOT>\<task-id>\attempt-NNNN
└─ native_cad\working_pack
   ├─ 箱体竖隔板L.sldprt
   ├─ 箱体竖隔板R.SLDPRT
   └─ 其余 73 个原生 SolidWorks 文件
```

`working_pack` 必须恰好 75 个顶层 CAD 文件：53 `SLDPRT`、22 `SLDASM`。文件名集合不允许变化；阶段结束只允许 L/R 两个文件的哈希变化。目录、祖先和文件不得是 reparse point/symlink，文件 link count 必须为 1；发现 STEP/STP/FCStd/FreeCAD 立即拒绝。

固定 recipe：

- `winnsen-16029-888w-14door-native-v1`
- digest：`F07C5497F9DE7D02727D8C084E5A7969A862C44C7990858CDEF8E8E54FEACEB8`
- 888×1917×550，2 列，14 门，`[7,7]`
- row sequence：`L1111111-R1111111`
- 门板宽 381 mm

锁槽中心 Y：

```text
159.2142857142857
420.6428571428571
682.0714285714284
943.5
1204.9285714285713
1466.3571428571427
1727.7857142857142
```

每行必须有一个 22-edge 原生跨折弯锁槽和一组 Ø5 双圆边；Ø5 圆心 Y = 槽中心 Y - 60 mm。

## 不可变计划和独立短期授权

计划永久保持：

```text
qualityBoundary.planningOnly = true
executionBoundary.executorImplemented = false
```

计划本身不是执行许可。工具必须同时取得：

```text
WINNSEN_NATIVE_16029_PLAN_SHA256
WINNSEN_NATIVE_EXECUTION_AUTH_PATH
WINNSEN_NATIVE_EXECUTION_AUTH_SHA256
```

授权路径只能是：

```text
<attempt>\execution_authorizations\native_lock_topology_888x14_v1.json
```

授权 schema 为 `winnsen.native_execution_authorization.v1`，必须逐字段匹配共享授权模块：顶层和各嵌套对象禁止额外键，`purpose=structure_engineering_assistance`，quality boundary 固定为两项 false/true，task 必含 `leaseExpiresAt`，tool 固定为 `native_lock_topology_888x14_v1`，`execution.phases` 必须严格等于 `["lock_topology_888x14"]`。工具会复算 `authorizationId`，并要求授权到期不晚于 lease；CAD 阶段和证据提交必须在二者到期前完成。

inventory digest 的固定算法：按顶层文件名 `OrdinalIgnoreCase` 排序，每行写成 `name|UPPER(SHA256)\n`，包括末尾 `LF`，再对完整 UTF-8 文本计算 SHA-256；不得加入 size，也不得改写文件名大小写。

## 原生 R 生成事务

1. 对完整 `working_pack` 建立逐文件 path/size/SHA-256 备份并验证。
2. 独占 SolidWorks 2020 会话编辑 L：抑制旧补充阵列，只保留已验证原生 seed，按真实 edge 位移，创建 7 实例原生阵列，保存后退出。
3. 新独占会话只读打开 L，选择 Right plane，调用：

```csharp
IPartDoc.MirrorPart2(
  true,
  ImportSolids | ImportSMInfo | ImportIndProps | ImportCutListProperties,
  out temporaryRight)
```

4. 临时 R 必须通过：1 body；`MirrorStock` 或 `MirrorPart`；`SheetMetal`；`FlatPattern`；无 `BaseBody`/`Imported`；无 feature error/warning；外链数 0；真实 edge 为 7×22 槽和 7 组 Ø5。
5. 临时 R 保存到同目录随机临时名，关闭 CAD；再用新的独占 SolidWorks 2020 只读会话重开验证，哈希不得改变。
6. 所有 CAD 进程退出后，使用文件系统事务替换同名旧 `箱体竖隔板R.SLDPRT`。
7. 再分别以新会话只读重开 L 和已提交 R。五个阶段 PID 必须互不相同，均为 `SldWorks.Application.28`、`RevisionNumber()` 以 `28.` 开头，exe 路径和 SHA 必须命中已审计 SolidWorks 2020。
8. 后验 inventory 必须仍为同名 75 件，临时 R 必须消失，新增/删除为空，只允许 L/R 哈希变化。
9. 任一门失败且 owned CAD/monitor 已清理时，删除本轮 `working_pack` 并从全包备份恢复，恢复后逐文件比对；无法证明进程已清理时拒绝递归恢复并保留备份。
10. 结果仅以新文件原子提交一次：`<attempt>\evidence\lock_topology_888x14.v2.json`，不覆盖既有证据。

## SolidWorks 运行边界

静态验证命令不会启动 SolidWorks：

```powershell
node D:\Winnsen_Structure_Agent_Studio\workers\native_model_requests\development\v1\tools\VerifyLockTopology888x14Static.mjs
```

真实阶段命令会启动 SolidWorks，只能由持有有效 lease 和阶段授权的 worker 调用：

```powershell
& D:\Winnsen_Structure_Agent_Studio\workers\native_model_requests\development\v1\tools\bin\BuildLockTopology888x14.exe <attempt-root>
```

## 独立验证器契约

```powershell
node ValidateLockTopology888x14.mjs `
  --build <attempt>\evidence\lock_topology_888x14.v2.json `
  --inspector <attempt>\evidence\lock_topology_888x14.inspector.v1.json `
  --tongues <attempt>\evidence\assembly_lock_tongues_888x14.json `
  --output <attempt>\evidence\lock_topology_888x14.validation.v2.json
```

验证器拒绝用模板 JSON 或自报工具路径代替真实证据。两项只读检查器固定为本目录源码和 x64 EXE：`InspectLockTopology888x14` 递归读取 L/R feature type、真实 edges/circles、外链及文件哈希；`InspectAssemblyTongues888x14` 从固定 `root_assembly_888x14.v1.json` 读取总装契约并逐组件读取 `Transform2`。两者均使用独立 SolidWorks 2020 只读会话、零保存调用、PID/start time/parent monitor 身份和原子新文件提交。它要求：

三套工具的 normalized source SHA、EXE SHA 和 validator SHA 固定记录在 `lock_toolchain_manifest.json`。worker 必须把该 manifest 的 SHA 作为不可变阶段输入传给 validator；validator 不接受任意路径或动态自报 identity。两份 inspector 证据都带 canonical payload `self_digest` 并由 validator 重算。

总装锁舌检查递归使用 `GetComponents(false)`，以 SolidWorks 组件实例路径去重，要求恰好 14 个锁舌。`root_assembly_888x14.v1.json` 必须绑定 task、recipe、plan、`native_root_assembly_888x14_v1` 工具 identity、root-stage evidence hash 和 root live hash。当前 root stage 尚未实现，因此缺少这份受信 manifest 时运行验证会 fail-closed。

- build/inspector/tongue 三份输入的 schema、固定路径和互相 SHA 绑定；
- build 的计划、阶段授权、tool exe、75 个 live CAD 文件逐一重算哈希；
- inspector 的源码和 exe live hash，独立 SolidWorks 2020 只读重开、零保存、零残留；
- L/R 真实 edge 各 7×22 槽、各 7 组 Ø5，R 具原生 MirrorPart 特征且外链 0；
- assembly tongue 证据绑定 build SHA、inspector SHA 和总装 root live SHA；
- 14 个机械锁舌实例完整读回 3×3 rotation 与 XYZ；L/R 各 7 个，X=-55/+55，Y 对应 7 行，Z=-11.3；rotation 必须为正交矩阵，组件未抑制、未隐藏；
- 锁舌来源 basename 为 `锁舌.SLDPRT`，live SHA 为 `26603389858218CE45164EEA57294016830D671B00B689982B1483B1FDE7E719`。

静态通过不等于结构工程证据通过。只有真实运行、独立 inspector、总装 14 锁舌 transform 和后续 relocation/reopen 全部通过，才能把该任务结果交给结构工程师继续判断和深化。
