# 16029 `/indoor-v2` 审核整改回传报告（2026-11-19）

> 对应交接单：`docs/HANDOFF_16029_indoor-v2_审核整改_20261119.md`
> 状态：`production_release = false` · 本轮**未新建任何下单任务、未启动原生 SolidWorks CAD**，全部基于现有报告、交付 ZIP 与离线测试完成。

## 〇、基线、提交与保护核对

- **审核基线提交**：`a6c9332cb10b7c4045e831edccf6777c25ff1f52`（特性分支 `codex/16029-indoor-v2-full-capability-20261119`）
- **本轮代码与离线测试整改提交**：`e6f6adaf81babf9f8d5e06371993b2376f189359`（仅改 4 个文件：`TrialAssembly.cs`、`portal_pipeline.mjs`、`portal_pipeline.test.mjs`、`tests/TrialAssembly.OfflineTests.cs`，`check_project_file_scope.ps1` 与 `git diff --check` 均通过）
- **既有样机 ZIP 保护复核**：`data/variable_trial_portal_tasks/VT16029-20261117T112858Z-FE4372/16029_全参数_860W_1800H_450D_L6-R5_3D与DXF_VT16029-20261117T112858Z-FE4372.zip`
  - 复核后 SHA-256：`7244EDE86546FADA158424C89A7815B97BE9F5ACEE47B36491AD67C662CD596E`（与原始交付记录完全一致，0 字节修改）
- **工具链与正式工作台保护**：未覆盖 `data/master_v2/variable_trial_20261112/toolchain/current` 指针（`mass_readback` 离线测试验证 `currentPointerUnchanged = true, solidWorksStarted = false`），未改动原母版和正式工作台运行态。
- **离线全量回归结果**（均在代码提交 `e6f6ada` 上执行）：
  - `powershell -ExecutionPolicy Bypass -File workers/master_v2/variable_trial/tests/trial-assembly-offline.ps1` → **`TRIAL_ASSEMBLY_OFFLINE_PASS 86`**（原 52 项 + 新增 18 项干涉位置门禁 + 新增 16 项配置级材料门禁）
  - `node --test workers/master_v2/variable_trial/*.test.mjs workers/master_v2/variable_trial/**/*.test.mjs` → **`241 / 241 PASS`**（含新增 `portal_pipeline.test.mjs` 7/7 通过，含 FE4372 真包 34 份 DXF 1 对 1 实检）
  - `node tools/verify_16029_variable_trial_portal.mjs` → **`pass: true`, `presetsVerified: 5`, `cadExecuted: false`**
  - `powershell -ExecutionPolicy Bypass -File workers/master_v2/variable_trial/mass_readback/test-offline.ps1` → **`OFFLINE_TESTS_PASS 54` + `ADAPTER_BEHAVIOR_PASS 18` + `6 / 6 PASS`**
  - `python -m unittest discover -s workers/master_v2/variable_trial -p "test_*.py"` → **`57 / 57 OK`**
  - 注：`audit_outputs.test.py` 因系统默认 Python 3.10 未安装 `pdfplumber` 未运行，留作环境说明。

---

## 一、38 处干涉逐条空间复核记录与位置门禁整改

- **所用提交**：证据来源于 `VT16029-20261117T112858Z-FE4372`（交付 ZIP 内 `reports/assembly-evidence.json`）；位置判定逻辑与离线测试位于提交 `e6f6adaf81babf9f8d5e06371993b2376f189359`。
- **证据文件**：
  - `data/variable_trial_portal_tasks/VT16029-20261117T112858Z-FE4372/16029_全参数_860W_1800H_450D_L6-R5_3D与DXF_VT16029-20261117T112858Z-FE4372.zip` → `reports/assembly-evidence.json`（字段：`interferenceClassification.collisions`、`events[inspection=full_component_rename_readback]`、`expectedManifest`、`layout`、`parts`）
- **核查总结与缺项说明**：
  1. **组件身份 38 / 38 全部精确追溯**：通过 `full_component_rename_readback`（`before -> requested`）与 `expectedManifest`（含每个实例的 `side`、`index`、`kind`、`transform`），38 条相交记录全部精确映射至具体门列与门序。
  2. **11 处门板 ↔ 上轴套相交（`approved_upper_pin_prebias_0p5_fit`）**：覆盖全柜 11 扇门（`L01–L06`、`R01–R05`），每扇门精确 1 处，相交体积全部一致为 `1.809930 mm³`；世界坐标 `Y` 全部精准落在门顶翻边板厚 `[topY-0.80, topY]`、`Z` 中心均为 `-7.00 mm`（名义铰链轴），`X` 均位于名义销轴外侧 `[±398.75, ±401.90]`（相对上轴套原点 `d=(±3.32, -1.90, 0.00) mm`）。
     - **缺失的局部证据**：`assembly-evidence.json` 仅记录了门板外包络 `boxMm` 及 `productionCraft.upperPinPrebiasMm=0.5 / pinDiameterMm=10.2`、塑料轴套外包络 `boxMm=[-6.5, 0, -6.5 .. 6.5, 7.0, 6.5]`，**缺少门板上销孔实测 3D 圆柱中心/半径读回**以及**塑料轴套颈部（Ø10）与法兰（Ø13）的台阶局部几何尺寸**。因此 11 条记录均标记为 **待验证（待结构/工艺负责人确认允许区域）**。
  3. **27 处卡簧 ↔ 铰链销相交（`approved_circlip_hinge_pin_groove_fit`）**：全部发生在 **9 扇非最顶层门的上卡簧**（`L01–L05`、`R01–R04`）与其**正上方相邻门的下伸铰链销**（`L02–L06`、`R02–R05`）之间（每对 3 个 E 型卡簧卡爪相交体：`2 × 0.928393 mm³` + `1 × 0.673889 mm³`，`9 × 3 = 27`；最顶层门 `L06` / `R05` 上方无相邻门铰链销，且 11 处下卡簧 `circlip_bottom` 均无相交）。所有 27 条记录在 `Y` 向上均位于卡簧中心 `dY=0.00 mm`（跨度 `0.80 mm`）、相对铰链销原点 **`dY = -27.00 mm`**，径向落入铰链销外径 `R=3.00 mm` 内。
     - **缺失的局部证据**：`assembly-evidence.json` 仅记录了铰链销外包络 `boxMm=[-3, -43.5, -3 .. 3, 43.5, 3]` 与卡簧外包络 `boxMm=[-5, -0.4, -4.276 .. 5, 0.4, 4.276]`，**缺少铰链销上卡槽的轴向 Y 坐标与槽底直径读回**、**缺少卡簧内爪轮廓局部几何**，无法仅凭现有报告独立证明 `dY=-27.00 mm` 对应已切槽区而非未切槽杆段。因此 27 条记录均标记为 **待验证（待结构负责人确认允许卡槽区域并补齐局部几何读回）**。
  4. **代码整改与离线测试结果**（`TrialAssembly.cs` & `tests/TrialAssembly.OfflineTests.cs`）：
     - 保留原始相交字段（`componentA`、`componentB`、`volumeMm3`、`intersectionBoundsMm`）和原始体积上限（`1.2 mm³` 与 `5.0 mm³`）。
     - 新增 `TrialAssembly.FullCabinetAssembly.EvaluateFullInterferenceCollisions(..., requireConfirmedPositionZones)` 与 `IsBoundsWithinAllowedZone`：强制校验 `intersectionBoundsMm` 合法性及是否落入负责人已确认（`ZoneConfirmed = true`）的对应允许区间；位置越界、缺 `intersectionBoundsMm`、缺允许区域或允许区域未确认时均**拒绝自动批准**（`approved=false, interferencePass=false`）。
     - 当前线上 `FullClassifyInterference` 暂以 `requireConfirmedPositionZones=false` 保持既有工作台生成链路兼容，并输出 `componentARole`、`componentBRole`、`doorSide`、`doorIndex` 与 `positionCheckStatus="pending_owner_zone_confirmation"`，待结构/工艺负责人确认允许区域坐标口径后开启硬拦截。
     - 离线测试 `TestFullInterferencePositionZones`（18 项断言全部通过）：覆盖两类合法位置通过、相同体积（`0.9284` / `1.8099`）但位置偏移拒批、缺 `intersectionBoundsMm` 拒批、缺/未确认允许区域拒批、超体积上限（`1.25` / `5.05`）拒批。

### 1.1 全部 38 条干涉记录逐条复核明细

| # | 分类 | 组件 A（装配身份） | 组件 B（装配身份） | 门列/门序 | 体积 (mm³) | 相交范围 `intersectionBoundsMm` `[minX,minY,minZ .. maxX,maxY,maxZ]` | 对应特征与相对偏移 | 缺失局部证据 | 复核结论 |
|---:|---|---|---|---|---:|---|---|---|---|
| 01 | `approved_upper_pin_prebias_0p5_fit` | `R_W367_H335p6_panel-4` (`door_R_04_panel`) | `trial_plastic_bushing-19` (`door_R_04_bushing_top`) | R04 | 1.8099 | `[398.75, 1394.60, -11.58 .. 401.90, 1395.40, -2.42]` | 门 R04 上销孔内偏 0.50 mm 预偏孔（门顶翻边 Y=[1394.60, 1395.40]，名义销轴 X=+397.00, Z=-7.00）；相对上轴套原点 d=(+3.32, -1.90, +0.00) mm；Z中心=-7.00 mm；Y跨度=0.80 mm | 缺少门板上销孔实测3D圆柱中心/直径读回及塑料轴套台阶颈部局部几何尺寸，仅含零件外包络 boxMm | 待验证（几何区间与上销预偏孔一致，缺局部特征坐标读回，待负责人确认允许区域） |
| 02 | `approved_upper_pin_prebias_0p5_fit` | `L_W367_H278p5_panel-4` (`door_L_04_panel`) | `trial_plastic_bushing-7` (`door_L_04_bushing_top`) | L04 | 1.8099 | `[-401.90, 1166.20, -11.58 .. -398.75, 1167.00, -2.42]` | 门 L04 上销孔内偏 0.50 mm 预偏孔（门顶翻边 Y=[1166.20, 1167.00]，名义销轴 X=-397.00, Z=-7.00）；相对上轴套原点 d=(-3.32, -1.90, +0.00) mm；Z中心=-7.00 mm；Y跨度=0.80 mm | 缺少门板上销孔实测3D圆柱中心/直径读回及塑料轴套台阶颈部局部几何尺寸，仅含零件外包络 boxMm | 待验证（几何区间与上销预偏孔一致，缺局部特征坐标读回，待负责人确认允许区域） |
| 03 | `approved_upper_pin_prebias_0p5_fit` | `L_W367_H278p5_panel-2` (`door_L_02_panel`) | `trial_plastic_bushing-3` (`door_L_02_bushing_top`) | L02 | 1.8099 | `[-401.90, 595.20, -11.58 .. -398.75, 596.00, -2.42]` | 门 L02 上销孔内偏 0.50 mm 预偏孔（门顶翻边 Y=[595.20, 596.00]，名义销轴 X=-397.00, Z=-7.00）；相对上轴套原点 d=(-3.32, -1.90, +0.00) mm；Z中心=-7.00 mm；Y跨度=0.80 mm | 缺少门板上销孔实测3D圆柱中心/直径读回及塑料轴套台阶颈部局部几何尺寸，仅含零件外包络 boxMm | 待验证（几何区间与上销预偏孔一致，缺局部特征坐标读回，待负责人确认允许区域） |
| 04 | `approved_upper_pin_prebias_0p5_fit` | `R_W367_H335p6_panel-3` (`door_R_03_panel`) | `trial_plastic_bushing-17` (`door_R_03_bushing_top`) | R03 | 1.8099 | `[398.75, 1052.00, -11.58 .. 401.90, 1052.80, -2.42]` | 门 R03 上销孔内偏 0.50 mm 预偏孔（门顶翻边 Y=[1052.00, 1052.80]，名义销轴 X=+397.00, Z=-7.00）；相对上轴套原点 d=(+3.32, -1.90, +0.00) mm；Z中心=-7.00 mm；Y跨度=0.80 mm | 缺少门板上销孔实测3D圆柱中心/直径读回及塑料轴套台阶颈部局部几何尺寸，仅含零件外包络 boxMm | 待验证（几何区间与上销预偏孔一致，缺局部特征坐标读回，待负责人确认允许区域） |
| 05 | `approved_upper_pin_prebias_0p5_fit` | `L_W367_H278p5_panel-3` (`door_L_03_panel`) | `trial_plastic_bushing-5` (`door_L_03_bushing_top`) | L03 | 1.8099 | `[-401.90, 880.70, -11.58 .. -398.75, 881.50, -2.42]` | 门 L03 上销孔内偏 0.50 mm 预偏孔（门顶翻边 Y=[880.70, 881.50]，名义销轴 X=-397.00, Z=-7.00）；相对上轴套原点 d=(-3.32, -1.90, +0.00) mm；Z中心=-7.00 mm；Y跨度=0.80 mm | 缺少门板上销孔实测3D圆柱中心/直径读回及塑料轴套台阶颈部局部几何尺寸，仅含零件外包络 boxMm | 待验证（几何区间与上销预偏孔一致，缺局部特征坐标读回，待负责人确认允许区域） |
| 06 | `approved_upper_pin_prebias_0p5_fit` | `L_W367_H278p5_panel-5` (`door_L_05_panel`) | `trial_plastic_bushing-9` (`door_L_05_bushing_top`) | L05 | 1.8099 | `[-401.90, 1451.70, -11.58 .. -398.75, 1452.50, -2.42]` | 门 L05 上销孔内偏 0.50 mm 预偏孔（门顶翻边 Y=[1451.70, 1452.50]，名义销轴 X=-397.00, Z=-7.00）；相对上轴套原点 d=(-3.32, -1.90, +0.00) mm；Z中心=-7.00 mm；Y跨度=0.80 mm | 缺少门板上销孔实测3D圆柱中心/直径读回及塑料轴套台阶颈部局部几何尺寸，仅含零件外包络 boxMm | 待验证（几何区间与上销预偏孔一致，缺局部特征坐标读回，待负责人确认允许区域） |
| 07 | `approved_upper_pin_prebias_0p5_fit` | `R_W367_H335p6_panel-2` (`door_R_02_panel`) | `trial_plastic_bushing-15` (`door_R_02_bushing_top`) | R02 | 1.8099 | `[398.75, 709.40, -11.58 .. 401.90, 710.20, -2.42]` | 门 R02 上销孔内偏 0.50 mm 预偏孔（门顶翻边 Y=[709.40, 710.20]，名义销轴 X=+397.00, Z=-7.00）；相对上轴套原点 d=(+3.32, -1.90, +0.00) mm；Z中心=-7.00 mm；Y跨度=0.80 mm | 缺少门板上销孔实测3D圆柱中心/直径读回及塑料轴套台阶颈部局部几何尺寸，仅含零件外包络 boxMm | 待验证（几何区间与上销预偏孔一致，缺局部特征坐标读回，待负责人确认允许区域） |
| 08 | `approved_upper_pin_prebias_0p5_fit` | `R_W367_H335p6_panel-5` (`door_R_05_panel`) | `trial_plastic_bushing-21` (`door_R_05_bushing_top`) | R05 | 1.8099 | `[398.75, 1737.20, -11.58 .. 401.90, 1738.00, -2.42]` | 门 R05 上销孔内偏 0.50 mm 预偏孔（门顶翻边 Y=[1737.20, 1738.00]，名义销轴 X=+397.00, Z=-7.00）；相对上轴套原点 d=(+3.32, -1.90, +0.00) mm；Z中心=-7.00 mm；Y跨度=0.80 mm | 缺少门板上销孔实测3D圆柱中心/直径读回及塑料轴套台阶颈部局部几何尺寸，仅含零件外包络 boxMm | 待验证（几何区间与上销预偏孔一致，缺局部特征坐标读回，待负责人确认允许区域） |
| 09 | `approved_upper_pin_prebias_0p5_fit` | `L_W367_H278p5_panel-6` (`door_L_06_panel`) | `trial_plastic_bushing-11` (`door_L_06_bushing_top`) | L06 | 1.8099 | `[-401.90, 1737.20, -11.58 .. -398.75, 1738.00, -2.42]` | 门 L06 上销孔内偏 0.50 mm 预偏孔（门顶翻边 Y=[1737.20, 1738.00]，名义销轴 X=-397.00, Z=-7.00）；相对上轴套原点 d=(-3.32, -1.90, +0.00) mm；Z中心=-7.00 mm；Y跨度=0.80 mm | 缺少门板上销孔实测3D圆柱中心/直径读回及塑料轴套台阶颈部局部几何尺寸，仅含零件外包络 boxMm | 待验证（几何区间与上销预偏孔一致，缺局部特征坐标读回，待负责人确认允许区域） |
| 10 | `approved_upper_pin_prebias_0p5_fit` | `R_W367_H335p6_panel-1` (`door_R_01_panel`) | `trial_plastic_bushing-13` (`door_R_01_bushing_top`) | R01 | 1.8099 | `[398.75, 366.80, -11.58 .. 401.90, 367.60, -2.42]` | 门 R01 上销孔内偏 0.50 mm 预偏孔（门顶翻边 Y=[366.80, 367.60]，名义销轴 X=+397.00, Z=-7.00）；相对上轴套原点 d=(+3.32, -1.90, +0.00) mm；Z中心=-7.00 mm；Y跨度=0.80 mm | 缺少门板上销孔实测3D圆柱中心/直径读回及塑料轴套台阶颈部局部几何尺寸，仅含零件外包络 boxMm | 待验证（几何区间与上销预偏孔一致，缺局部特征坐标读回，待负责人确认允许区域） |
| 11 | `approved_upper_pin_prebias_0p5_fit` | `L_W367_H278p5_panel-1` (`door_L_01_panel`) | `trial_plastic_bushing-1` (`door_L_01_bushing_top`) | L01 | 1.8099 | `[-401.90, 309.70, -11.58 .. -398.75, 310.50, -2.42]` | 门 L01 上销孔内偏 0.50 mm 预偏孔（门顶翻边 Y=[309.70, 310.50]，名义销轴 X=-397.00, Z=-7.00）；相对上轴套原点 d=(-3.32, -1.90, +0.00) mm；Z中心=-7.00 mm；Y跨度=0.80 mm | 缺少门板上销孔实测3D圆柱中心/直径读回及塑料轴套台阶颈部局部几何尺寸，仅含零件外包络 boxMm | 待验证（几何区间与上销预偏孔一致，缺局部特征坐标读回，待负责人确认允许区域） |
| 12 | `approved_circlip_hinge_pin_groove_fit` | `trial_circlip-7` (`door_L_04_circlip_top`) | `trial_door_hinge_pin-5` (`door_L_05_hinge_pin`) | L04上卡簧 ↔ L05铰链销 | 0.9284 | `[-400.00, 1136.90, -8.01 .. -399.29, 1137.70, -5.35]` | 门 L04 上端卡簧（外侧卡爪）与门 L05 铰链销下伸段（相对销轴原点 dY=-27.00 mm）；相对卡簧原点 d=(-2.64, +0.00, +1.05) mm；相对铰链销原点 d=(-2.64, -27.00, +0.32) mm | 缺少铰链销卡槽轴向 Y 位置与槽底直径读回、缺少卡簧内爪局部轮廓几何，仅含零件外包络 boxMm | 待验证（位于铰链销轴 dY=-27.00 mm 处，缺卡槽轴向/槽径实测读回，待负责人确认允许区域） |
| 13 | `approved_circlip_hinge_pin_groove_fit` | `trial_circlip-3` (`door_L_02_circlip_top`) | `trial_door_hinge_pin-3` (`door_L_03_hinge_pin`) | L02上卡簧 ↔ L03铰链销 | 0.9284 | `[-400.00, 565.90, -8.01 .. -399.29, 566.70, -5.35]` | 门 L02 上端卡簧（外侧卡爪）与门 L03 铰链销下伸段（相对销轴原点 dY=-27.00 mm）；相对卡簧原点 d=(-2.64, +0.00, +1.05) mm；相对铰链销原点 d=(-2.64, -27.00, +0.32) mm | 缺少铰链销卡槽轴向 Y 位置与槽底直径读回、缺少卡簧内爪局部轮廓几何，仅含零件外包络 boxMm | 待验证（位于铰链销轴 dY=-27.00 mm 处，缺卡槽轴向/槽径实测读回，待负责人确认允许区域） |
| 14 | `approved_circlip_hinge_pin_groove_fit` | `trial_circlip-7` (`door_L_04_circlip_top`) | `trial_door_hinge_pin-5` (`door_L_05_hinge_pin`) | L04上卡簧 ↔ L05铰链销 | 0.9284 | `[-394.79, 1136.90, -7.79 .. -394.00, 1137.70, -5.16]` | 门 L04 上端卡簧（内侧卡爪）与门 L05 铰链销下伸段（相对销轴原点 dY=-27.00 mm）；相对卡簧原点 d=(+2.60, +0.00, +1.25) mm；相对铰链销原点 d=(+2.60, -27.00, +0.53) mm | 缺少铰链销卡槽轴向 Y 位置与槽底直径读回、缺少卡簧内爪局部轮廓几何，仅含零件外包络 boxMm | 待验证（位于铰链销轴 dY=-27.00 mm 处，缺卡槽轴向/槽径实测读回，待负责人确认允许区域） |
| 15 | `approved_circlip_hinge_pin_groove_fit` | `trial_circlip-9` (`door_L_05_circlip_top`) | `trial_door_hinge_pin-6` (`door_L_06_hinge_pin`) | L05上卡簧 ↔ L06铰链销 | 0.9284 | `[-400.00, 1422.40, -8.01 .. -399.29, 1423.20, -5.35]` | 门 L05 上端卡簧（外侧卡爪）与门 L06 铰链销下伸段（相对销轴原点 dY=-27.00 mm）；相对卡簧原点 d=(-2.64, +0.00, +1.05) mm；相对铰链销原点 d=(-2.64, -27.00, +0.32) mm | 缺少铰链销卡槽轴向 Y 位置与槽底直径读回、缺少卡簧内爪局部轮廓几何，仅含零件外包络 boxMm | 待验证（位于铰链销轴 dY=-27.00 mm 处，缺卡槽轴向/槽径实测读回，待负责人确认允许区域） |
| 16 | `approved_circlip_hinge_pin_groove_fit` | `trial_circlip-13` (`door_R_01_circlip_top`) | `trial_door_hinge_pin-8` (`door_R_02_hinge_pin`) | R01上卡簧 ↔ R02铰链销 | 0.9284 | `[399.21, 337.50, -7.79 .. 400.00, 338.30, -5.16]` | 门 R01 上端卡簧（外侧卡爪）与门 R02 铰链销下伸段（相对销轴原点 dY=-27.00 mm）；相对卡簧原点 d=(+2.60, +0.00, +1.25) mm；相对铰链销原点 d=(+2.60, -27.00, +0.53) mm | 缺少铰链销卡槽轴向 Y 位置与槽底直径读回、缺少卡簧内爪局部轮廓几何，仅含零件外包络 boxMm | 待验证（位于铰链销轴 dY=-27.00 mm 处，缺卡槽轴向/槽径实测读回，待负责人确认允许区域） |
| 17 | `approved_circlip_hinge_pin_groove_fit` | `trial_circlip-9` (`door_L_05_circlip_top`) | `trial_door_hinge_pin-6` (`door_L_06_hinge_pin`) | L05上卡簧 ↔ L06铰链销 | 0.9284 | `[-394.79, 1422.40, -7.79 .. -394.00, 1423.20, -5.16]` | 门 L05 上端卡簧（内侧卡爪）与门 L06 铰链销下伸段（相对销轴原点 dY=-27.00 mm）；相对卡簧原点 d=(+2.60, +0.00, +1.25) mm；相对铰链销原点 d=(+2.60, -27.00, +0.53) mm | 缺少铰链销卡槽轴向 Y 位置与槽底直径读回、缺少卡簧内爪局部轮廓几何，仅含零件外包络 boxMm | 待验证（位于铰链销轴 dY=-27.00 mm 处，缺卡槽轴向/槽径实测读回，待负责人确认允许区域） |
| 18 | `approved_circlip_hinge_pin_groove_fit` | `trial_circlip-5` (`door_L_03_circlip_top`) | `trial_door_hinge_pin-4` (`door_L_04_hinge_pin`) | L03上卡簧 ↔ L04铰链销 | 0.9284 | `[-400.00, 851.40, -8.01 .. -399.29, 852.20, -5.35]` | 门 L03 上端卡簧（外侧卡爪）与门 L04 铰链销下伸段（相对销轴原点 dY=-27.00 mm）；相对卡簧原点 d=(-2.64, +0.00, +1.05) mm；相对铰链销原点 d=(-2.64, -27.00, +0.32) mm | 缺少铰链销卡槽轴向 Y 位置与槽底直径读回、缺少卡簧内爪局部轮廓几何，仅含零件外包络 boxMm | 待验证（位于铰链销轴 dY=-27.00 mm 处，缺卡槽轴向/槽径实测读回，待负责人确认允许区域） |
| 19 | `approved_circlip_hinge_pin_groove_fit` | `trial_circlip-3` (`door_L_02_circlip_top`) | `trial_door_hinge_pin-3` (`door_L_03_hinge_pin`) | L02上卡簧 ↔ L03铰链销 | 0.9284 | `[-394.79, 565.90, -7.79 .. -394.00, 566.70, -5.16]` | 门 L02 上端卡簧（内侧卡爪）与门 L03 铰链销下伸段（相对销轴原点 dY=-27.00 mm）；相对卡簧原点 d=(+2.60, +0.00, +1.25) mm；相对铰链销原点 d=(+2.60, -27.00, +0.53) mm | 缺少铰链销卡槽轴向 Y 位置与槽底直径读回、缺少卡簧内爪局部轮廓几何，仅含零件外包络 boxMm | 待验证（位于铰链销轴 dY=-27.00 mm 处，缺卡槽轴向/槽径实测读回，待负责人确认允许区域） |
| 20 | `approved_circlip_hinge_pin_groove_fit` | `trial_circlip-5` (`door_L_03_circlip_top`) | `trial_door_hinge_pin-4` (`door_L_04_hinge_pin`) | L03上卡簧 ↔ L04铰链销 | 0.9284 | `[-394.79, 851.40, -7.79 .. -394.00, 852.20, -5.16]` | 门 L03 上端卡簧（内侧卡爪）与门 L04 铰链销下伸段（相对销轴原点 dY=-27.00 mm）；相对卡簧原点 d=(+2.60, +0.00, +1.25) mm；相对铰链销原点 d=(+2.60, -27.00, +0.53) mm | 缺少铰链销卡槽轴向 Y 位置与槽底直径读回、缺少卡簧内爪局部轮廓几何，仅含零件外包络 boxMm | 待验证（位于铰链销轴 dY=-27.00 mm 处，缺卡槽轴向/槽径实测读回，待负责人确认允许区域） |
| 21 | `approved_circlip_hinge_pin_groove_fit` | `trial_circlip-17` (`door_R_03_circlip_top`) | `trial_door_hinge_pin-10` (`door_R_04_hinge_pin`) | R03上卡簧 ↔ R04铰链销 | 0.9284 | `[394.00, 1022.70, -8.01 .. 394.71, 1023.50, -5.35]` | 门 R03 上端卡簧（内侧卡爪）与门 R04 铰链销下伸段（相对销轴原点 dY=-27.00 mm）；相对卡簧原点 d=(-2.64, -0.00, +1.05) mm；相对铰链销原点 d=(-2.64, -27.00, +0.32) mm | 缺少铰链销卡槽轴向 Y 位置与槽底直径读回、缺少卡簧内爪局部轮廓几何，仅含零件外包络 boxMm | 待验证（位于铰链销轴 dY=-27.00 mm 处，缺卡槽轴向/槽径实测读回，待负责人确认允许区域） |
| 22 | `approved_circlip_hinge_pin_groove_fit` | `trial_circlip-17` (`door_R_03_circlip_top`) | `trial_door_hinge_pin-10` (`door_R_04_hinge_pin`) | R03上卡簧 ↔ R04铰链销 | 0.9284 | `[399.21, 1022.70, -7.79 .. 400.00, 1023.50, -5.16]` | 门 R03 上端卡簧（外侧卡爪）与门 R04 铰链销下伸段（相对销轴原点 dY=-27.00 mm）；相对卡簧原点 d=(+2.60, -0.00, +1.25) mm；相对铰链销原点 d=(+2.60, -27.00, +0.53) mm | 缺少铰链销卡槽轴向 Y 位置与槽底直径读回、缺少卡簧内爪局部轮廓几何，仅含零件外包络 boxMm | 待验证（位于铰链销轴 dY=-27.00 mm 处，缺卡槽轴向/槽径实测读回，待负责人确认允许区域） |
| 23 | `approved_circlip_hinge_pin_groove_fit` | `trial_circlip-19` (`door_R_04_circlip_top`) | `trial_door_hinge_pin-11` (`door_R_05_hinge_pin`) | R04上卡簧 ↔ R05铰链销 | 0.9284 | `[394.00, 1365.30, -8.01 .. 394.71, 1366.10, -5.35]` | 门 R04 上端卡簧（内侧卡爪）与门 R05 铰链销下伸段（相对销轴原点 dY=-27.00 mm）；相对卡簧原点 d=(-2.64, +0.00, +1.05) mm；相对铰链销原点 d=(-2.64, -27.00, +0.32) mm | 缺少铰链销卡槽轴向 Y 位置与槽底直径读回、缺少卡簧内爪局部轮廓几何，仅含零件外包络 boxMm | 待验证（位于铰链销轴 dY=-27.00 mm 处，缺卡槽轴向/槽径实测读回，待负责人确认允许区域） |
| 24 | `approved_circlip_hinge_pin_groove_fit` | `trial_circlip-19` (`door_R_04_circlip_top`) | `trial_door_hinge_pin-11` (`door_R_05_hinge_pin`) | R04上卡簧 ↔ R05铰链销 | 0.9284 | `[399.21, 1365.30, -7.79 .. 400.00, 1366.10, -5.16]` | 门 R04 上端卡簧（外侧卡爪）与门 R05 铰链销下伸段（相对销轴原点 dY=-27.00 mm）；相对卡簧原点 d=(+2.60, +0.00, +1.25) mm；相对铰链销原点 d=(+2.60, -27.00, +0.53) mm | 缺少铰链销卡槽轴向 Y 位置与槽底直径读回、缺少卡簧内爪局部轮廓几何，仅含零件外包络 boxMm | 待验证（位于铰链销轴 dY=-27.00 mm 处，缺卡槽轴向/槽径实测读回，待负责人确认允许区域） |
| 25 | `approved_circlip_hinge_pin_groove_fit` | `trial_circlip-15` (`door_R_02_circlip_top`) | `trial_door_hinge_pin-9` (`door_R_03_hinge_pin`) | R02上卡簧 ↔ R03铰链销 | 0.9284 | `[394.00, 680.10, -8.01 .. 394.71, 680.90, -5.35]` | 门 R02 上端卡簧（内侧卡爪）与门 R03 铰链销下伸段（相对销轴原点 dY=-27.00 mm）；相对卡簧原点 d=(-2.64, +0.00, +1.05) mm；相对铰链销原点 d=(-2.64, -27.00, +0.32) mm | 缺少铰链销卡槽轴向 Y 位置与槽底直径读回、缺少卡簧内爪局部轮廓几何，仅含零件外包络 boxMm | 待验证（位于铰链销轴 dY=-27.00 mm 处，缺卡槽轴向/槽径实测读回，待负责人确认允许区域） |
| 26 | `approved_circlip_hinge_pin_groove_fit` | `trial_circlip-13` (`door_R_01_circlip_top`) | `trial_door_hinge_pin-8` (`door_R_02_hinge_pin`) | R01上卡簧 ↔ R02铰链销 | 0.9284 | `[394.00, 337.50, -8.01 .. 394.71, 338.30, -5.35]` | 门 R01 上端卡簧（内侧卡爪）与门 R02 铰链销下伸段（相对销轴原点 dY=-27.00 mm）；相对卡簧原点 d=(-2.64, +0.00, +1.05) mm；相对铰链销原点 d=(-2.64, -27.00, +0.32) mm | 缺少铰链销卡槽轴向 Y 位置与槽底直径读回、缺少卡簧内爪局部轮廓几何，仅含零件外包络 boxMm | 待验证（位于铰链销轴 dY=-27.00 mm 处，缺卡槽轴向/槽径实测读回，待负责人确认允许区域） |
| 27 | `approved_circlip_hinge_pin_groove_fit` | `trial_circlip-15` (`door_R_02_circlip_top`) | `trial_door_hinge_pin-9` (`door_R_03_hinge_pin`) | R02上卡簧 ↔ R03铰链销 | 0.9284 | `[399.21, 680.10, -7.79 .. 400.00, 680.90, -5.16]` | 门 R02 上端卡簧（外侧卡爪）与门 R03 铰链销下伸段（相对销轴原点 dY=-27.00 mm）；相对卡簧原点 d=(+2.60, +0.00, +1.25) mm；相对铰链销原点 d=(+2.60, -27.00, +0.53) mm | 缺少铰链销卡槽轴向 Y 位置与槽底直径读回、缺少卡簧内爪局部轮廓几何，仅含零件外包络 boxMm | 待验证（位于铰链销轴 dY=-27.00 mm 处，缺卡槽轴向/槽径实测读回，待负责人确认允许区域） |
| 28 | `approved_circlip_hinge_pin_groove_fit` | `trial_circlip-1` (`door_L_01_circlip_top`) | `trial_door_hinge_pin-2` (`door_L_02_hinge_pin`) | L01上卡簧 ↔ L02铰链销 | 0.9284 | `[-400.00, 280.40, -8.01 .. -399.29, 281.20, -5.35]` | 门 L01 上端卡簧（外侧卡爪）与门 L02 铰链销下伸段（相对销轴原点 dY=-27.00 mm）；相对卡簧原点 d=(-2.64, -0.00, +1.05) mm；相对铰链销原点 d=(-2.64, -27.00, +0.32) mm | 缺少铰链销卡槽轴向 Y 位置与槽底直径读回、缺少卡簧内爪局部轮廓几何，仅含零件外包络 boxMm | 待验证（位于铰链销轴 dY=-27.00 mm 处，缺卡槽轴向/槽径实测读回，待负责人确认允许区域） |
| 29 | `approved_circlip_hinge_pin_groove_fit` | `trial_circlip-1` (`door_L_01_circlip_top`) | `trial_door_hinge_pin-2` (`door_L_02_hinge_pin`) | L01上卡簧 ↔ L02铰链销 | 0.9284 | `[-394.79, 280.40, -7.79 .. -394.00, 281.20, -5.16]` | 门 L01 上端卡簧（内侧卡爪）与门 L02 铰链销下伸段（相对销轴原点 dY=-27.00 mm）；相对卡簧原点 d=(+2.60, -0.00, +1.25) mm；相对铰链销原点 d=(+2.60, -27.00, +0.53) mm | 缺少铰链销卡槽轴向 Y 位置与槽底直径读回、缺少卡簧内爪局部轮廓几何，仅含零件外包络 boxMm | 待验证（位于铰链销轴 dY=-27.00 mm 处，缺卡槽轴向/槽径实测读回，待负责人确认允许区域） |
| 30 | `approved_circlip_hinge_pin_groove_fit` | `trial_circlip-7` (`door_L_04_circlip_top`) | `trial_door_hinge_pin-5` (`door_L_05_hinge_pin`) | L04上卡簧 ↔ L05铰链销 | 0.6739 | `[-397.81, 1136.90, -10.00 .. -395.97, 1137.70, -9.35]` | 门 L04 上端卡簧（背侧卡爪）与门 L05 铰链销下伸段（相对销轴原点 dY=-27.00 mm）；相对卡簧原点 d=(+0.11, +0.00, -1.95) mm；相对铰链销原点 d=(+0.11, -27.00, -2.68) mm | 缺少铰链销卡槽轴向 Y 位置与槽底直径读回、缺少卡簧内爪局部轮廓几何，仅含零件外包络 boxMm | 待验证（位于铰链销轴 dY=-27.00 mm 处，缺卡槽轴向/槽径实测读回，待负责人确认允许区域） |
| 31 | `approved_circlip_hinge_pin_groove_fit` | `trial_circlip-9` (`door_L_05_circlip_top`) | `trial_door_hinge_pin-6` (`door_L_06_hinge_pin`) | L05上卡簧 ↔ L06铰链销 | 0.6739 | `[-397.81, 1422.40, -10.00 .. -395.97, 1423.20, -9.35]` | 门 L05 上端卡簧（背侧卡爪）与门 L06 铰链销下伸段（相对销轴原点 dY=-27.00 mm）；相对卡簧原点 d=(+0.11, +0.00, -1.95) mm；相对铰链销原点 d=(+0.11, -27.00, -2.68) mm | 缺少铰链销卡槽轴向 Y 位置与槽底直径读回、缺少卡簧内爪局部轮廓几何，仅含零件外包络 boxMm | 待验证（位于铰链销轴 dY=-27.00 mm 处，缺卡槽轴向/槽径实测读回，待负责人确认允许区域） |
| 32 | `approved_circlip_hinge_pin_groove_fit` | `trial_circlip-5` (`door_L_03_circlip_top`) | `trial_door_hinge_pin-4` (`door_L_04_hinge_pin`) | L03上卡簧 ↔ L04铰链销 | 0.6739 | `[-397.81, 851.40, -10.00 .. -395.97, 852.20, -9.35]` | 门 L03 上端卡簧（背侧卡爪）与门 L04 铰链销下伸段（相对销轴原点 dY=-27.00 mm）；相对卡簧原点 d=(+0.11, +0.00, -1.95) mm；相对铰链销原点 d=(+0.11, -27.00, -2.68) mm | 缺少铰链销卡槽轴向 Y 位置与槽底直径读回、缺少卡簧内爪局部轮廓几何，仅含零件外包络 boxMm | 待验证（位于铰链销轴 dY=-27.00 mm 处，缺卡槽轴向/槽径实测读回，待负责人确认允许区域） |
| 33 | `approved_circlip_hinge_pin_groove_fit` | `trial_circlip-17` (`door_R_03_circlip_top`) | `trial_door_hinge_pin-10` (`door_R_04_hinge_pin`) | R03上卡簧 ↔ R04铰链销 | 0.6739 | `[396.19, 1022.70, -10.00 .. 398.03, 1023.50, -9.35]` | 门 R03 上端卡簧（背侧卡爪）与门 R04 铰链销下伸段（相对销轴原点 dY=-27.00 mm）；相对卡簧原点 d=(+0.11, -0.00, -1.95) mm；相对铰链销原点 d=(+0.11, -27.00, -2.68) mm | 缺少铰链销卡槽轴向 Y 位置与槽底直径读回、缺少卡簧内爪局部轮廓几何，仅含零件外包络 boxMm | 待验证（位于铰链销轴 dY=-27.00 mm 处，缺卡槽轴向/槽径实测读回，待负责人确认允许区域） |
| 34 | `approved_circlip_hinge_pin_groove_fit` | `trial_circlip-19` (`door_R_04_circlip_top`) | `trial_door_hinge_pin-11` (`door_R_05_hinge_pin`) | R04上卡簧 ↔ R05铰链销 | 0.6739 | `[396.19, 1365.30, -10.00 .. 398.03, 1366.10, -9.35]` | 门 R04 上端卡簧（背侧卡爪）与门 R05 铰链销下伸段（相对销轴原点 dY=-27.00 mm）；相对卡簧原点 d=(+0.11, +0.00, -1.95) mm；相对铰链销原点 d=(+0.11, -27.00, -2.68) mm | 缺少铰链销卡槽轴向 Y 位置与槽底直径读回、缺少卡簧内爪局部轮廓几何，仅含零件外包络 boxMm | 待验证（位于铰链销轴 dY=-27.00 mm 处，缺卡槽轴向/槽径实测读回，待负责人确认允许区域） |
| 35 | `approved_circlip_hinge_pin_groove_fit` | `trial_circlip-15` (`door_R_02_circlip_top`) | `trial_door_hinge_pin-9` (`door_R_03_hinge_pin`) | R02上卡簧 ↔ R03铰链销 | 0.6739 | `[396.19, 680.10, -10.00 .. 398.03, 680.90, -9.35]` | 门 R02 上端卡簧（背侧卡爪）与门 R03 铰链销下伸段（相对销轴原点 dY=-27.00 mm）；相对卡簧原点 d=(+0.11, +0.00, -1.95) mm；相对铰链销原点 d=(+0.11, -27.00, -2.68) mm | 缺少铰链销卡槽轴向 Y 位置与槽底直径读回、缺少卡簧内爪局部轮廓几何，仅含零件外包络 boxMm | 待验证（位于铰链销轴 dY=-27.00 mm 处，缺卡槽轴向/槽径实测读回，待负责人确认允许区域） |
| 36 | `approved_circlip_hinge_pin_groove_fit` | `trial_circlip-1` (`door_L_01_circlip_top`) | `trial_door_hinge_pin-2` (`door_L_02_hinge_pin`) | L01上卡簧 ↔ L02铰链销 | 0.6739 | `[-397.81, 280.40, -10.00 .. -395.97, 281.20, -9.35]` | 门 L01 上端卡簧（背侧卡爪）与门 L02 铰链销下伸段（相对销轴原点 dY=-27.00 mm）；相对卡簧原点 d=(+0.11, -0.00, -1.95) mm；相对铰链销原点 d=(+0.11, -27.00, -2.68) mm | 缺少铰链销卡槽轴向 Y 位置与槽底直径读回、缺少卡簧内爪局部轮廓几何，仅含零件外包络 boxMm | 待验证（位于铰链销轴 dY=-27.00 mm 处，缺卡槽轴向/槽径实测读回，待负责人确认允许区域） |
| 37 | `approved_circlip_hinge_pin_groove_fit` | `trial_circlip-3` (`door_L_02_circlip_top`) | `trial_door_hinge_pin-3` (`door_L_03_hinge_pin`) | L02上卡簧 ↔ L03铰链销 | 0.6739 | `[-397.81, 565.90, -10.00 .. -395.97, 566.70, -9.35]` | 门 L02 上端卡簧（背侧卡爪）与门 L03 铰链销下伸段（相对销轴原点 dY=-27.00 mm）；相对卡簧原点 d=(+0.11, +0.00, -1.95) mm；相对铰链销原点 d=(+0.11, -27.00, -2.68) mm | 缺少铰链销卡槽轴向 Y 位置与槽底直径读回、缺少卡簧内爪局部轮廓几何，仅含零件外包络 boxMm | 待验证（位于铰链销轴 dY=-27.00 mm 处，缺卡槽轴向/槽径实测读回，待负责人确认允许区域） |
| 38 | `approved_circlip_hinge_pin_groove_fit` | `trial_circlip-13` (`door_R_01_circlip_top`) | `trial_door_hinge_pin-8` (`door_R_02_hinge_pin`) | R01上卡簧 ↔ R02铰链销 | 0.6739 | `[396.19, 337.50, -10.00 .. 398.03, 338.30, -9.35]` | 门 R01 上端卡簧（背侧卡爪）与门 R02 铰链销下伸段（相对销轴原点 dY=-27.00 mm）；相对卡簧原点 d=(+0.11, +0.00, -1.95) mm；相对铰链销原点 d=(+0.11, -27.00, -2.68) mm | 缺少铰链销卡槽轴向 Y 位置与槽底直径读回、缺少卡簧内爪局部轮廓几何，仅含零件外包络 boxMm | 待验证（位于铰链销轴 dY=-27.00 mm 处，缺卡槽轴向/槽径实测读回，待负责人确认允许区域） |

---

## 二、DXF 验收逐件对应整改与离线测试结果

- **所用提交**：`e6f6adaf81babf9f8d5e06371993b2376f189359`（改动文件：`workers/master_v2/variable_trial/portal_pipeline.mjs`、`workers/master_v2/variable_trial/portal_pipeline.test.mjs`）。
- **证据文件**：`FE4372` 交付 ZIP 内 `DXF_展开图/`、`reports/assembly-evidence.json`、`reports/flat-dxf-audit-report.json`，以及 `dxfRunDir/native-result.json`（`2026-11-18T06-40-38-452Z-T3-envelope-dxf-2c8f690a`）。
- **代码改动摘要**：
  1. 在 `auditFlatDxfDirectory` 中建立由**装机清单（`installedRelPaths`）+ 原生导出记录（`expectedParts`）**构成的规范化相对路径应交清单；保留原有未安装件过滤（`excludedUninstalledFiles`）。
  2. 彻底移除 `!expected || ...` 静默放过漏洞：找不到对应导出记录或预期 SHA-256 非 64 位十六进制时，`recordMatched=false`、`shaMatches=false`，直接判失败并记入 `unexpectedDxfFiles`。
  3. 将批次通过条件从“数量相等”改为严格 1 对 1 双向集合一致（`identityPass`），明确区分并报错：`missingExportRecords`（装机件缺导出记录）、`missingDxfFiles`（应交钣金缺 DXF 文件）、`unexpectedDxfFiles`（混入未登记/非应交 DXF）、`duplicateMappings`（重复零件或 DXF 路径映射）、`pathIdentityMismatches`（记录的 `flatPath` 与零件身份不一致）、`shaMismatches`（SHA-256 不符）、`EXPECTED_SHEET_METAL_MANIFEST_EMPTY`（空应交清单）。
  4. 保留原有全部 `dxf_check.py` 几何检查（`$INSUNITS=4`、`SPLINE=0`、开链 `=0`、重复线 `=0`、切割线相交 `=0`、零长线 `=0`、不支持图元 `=0`）。
  5. 在 `packageVariableTrialDeliverable` 中增加二次防漂移校验：仅打包审计通过的 DXF 集合，并在打包落盘后复核 `dxfInventory` 与审计结果 1 对 1 路径及 SHA-256 一致。
- **离线测试结果（`portal_pipeline.test.mjs`，`7 / 7 PASS`）**：
  1. 正常 1 对 1 应交集合 + 未安装件过滤 → `PASS`（`identityPass=true, excludedUninstalledFiles=['structure\uninstalled_seed.flat.dxf']`）。
  2. **总数不变反例（漏导 `上盖壳体底板` 并混入几何合格的 `trial_circlip.flat.dxf`）** → **`FAIL`**（`dxfFileCount=2 == sheetMetalCount=2`，但被准确拦截：`missingDxfFiles=1`, `unexpectedDxfFiles=1`, 混入文件 `geometryPassed=true` 但 `recordMatched=false, shaMatches=false, auditPassed=false`）。
  3. **重复映射反例**（同路径大小写/斜杠重复） → **`FAIL`**（`duplicateMappings >= 1`, `auditPassed=false`）。
  4. **SHA-256 不符反例**（几何与路径正确但摘要被改） → **`FAIL`**（`shaMismatches=1`, `shaMatches=false, auditPassed=false`）。
  5. **装机件缺导出记录 / 空应交清单反例** → **`FAIL`**（`missingExportRecords=1` / `EXPECTED_SHEET_METAL_MANIFEST_EMPTY`）。
  6. **记录 `flatPath` 指向错零件 + 几何缺陷（重复同心圆）保留校验** → **`FAIL`**（`pathIdentityMismatches=1`, `geometryPassed=false`）。
  7. **已有样机 `FE4372` 真包 34 份 DXF 离线解包实检** → **`PASS`**（`34 / 34` 逐件对应，`missingExportRecords=0`, `missingDxfFiles=0`, `unexpectedDxfFiles=0`, `duplicateMappings=0`, `pathIdentityMismatches=0`, `shaMismatches=0`, 几何 `34 / 34` 通过）。
- **仍缺输入**：当前实测覆盖 `860×1800×450 mm (L6-R5)` 单一规格；其余尺寸与门格组合待获准实跑时按同一提交分别验证。

### 2.1 样机 `FE4372` 34 份应交钣金 DXF 逐件 1 对 1 核对清单

| # | 装机零件相对路径 | DXF 相对路径 | 展开尺寸 (mm) | 导出记录与 ZIP 实算 SHA-256（前 16 位） | SHA 一致 | 几何+身份通过 |
|---:|---|---|---|---|---|---|
| 01 | `parts\L_W367_H278p5_panel.SLDPRT` | `parts\L_W367_H278p5_panel.flat.dxf` | 403.4 × 314.9 | `5E093082D5F807E6...` == `5E093082D5F807E6...` | ✅ | ✅ |
| 02 | `parts\L_W367_H278p5_stiffener.SLDPRT` | `parts\L_W367_H278p5_stiffener.flat.dxf` | 112.4 × 268 | `734AA1FEFFD9A374...` == `734AA1FEFFD9A374...` | ✅ | ✅ |
| 03 | `parts\R_W367_H335p6_panel.SLDPRT` | `parts\R_W367_H335p6_panel.flat.dxf` | 403.4 × 372 | `3024412FD55F632B...` == `3024412FD55F632B...` | ✅ | ✅ |
| 04 | `parts\R_W367_H335p6_stiffener.SLDPRT` | `parts\R_W367_H335p6_stiffener.flat.dxf` | 112.4 × 325.1 | `7D3C438B76750D5B...` == `7D3C438B76750D5B...` | ✅ | ✅ |
| 05 | `parts\trial_hinge_latch_plate.SLDPRT` | `parts\trial_hinge_latch_plate.flat.dxf` | 39.8 × 18 | `A09D803A52048A1B...` == `A09D803A52048A1B...` | ✅ | ✅ |
| 06 | `parts\trial_u_hook_pad.SLDPRT` | `parts\trial_u_hook_pad.flat.dxf` | 24.8 × 87 | `37CCCB63309A09E5...` == `37CCCB63309A09E5...` | ✅ | ✅ |
| 07 | `structure\底座底板.sldprt` | `structure\底座底板.flat.dxf` | 420.7 × 828 | `7D112CC8D6E5628C...` == `7D112CC8D6E5628C...` | ✅ | ✅ |
| 08 | `structure\底座加强筋.sldprt` | `structure\底座加强筋.flat.dxf` | 113.4 × 839.4 | `2BE3B4612E9F5C97...` == `2BE3B4612E9F5C97...` | ✅ | ✅ |
| 09 | `structure\底座外框.sldprt` | `structure\底座外框.flat.dxf` | 455.4 × 920 | `7DCD48A481DEDA20...` == `7DCD48A481DEDA20...` | ✅ | ✅ |
| 10 | `structure\门框 横隔板.sldprt` | `structure\门框 横隔板.flat.dxf` | 49.98 × 375 | `77F039A3EBC0AD27...` == `77F039A3EBC0AD27...` | ✅ | ✅ |
| 11 | `structure\门框 横隔板R.SLDPRT` | `structure\门框 横隔板R.flat.dxf` | 375 × 50.03 | `7C344E924740461F...` == `7C344E924740461F...` | ✅ | ✅ |
| 12 | `structure\门框 上.sldprt` | `structure\门框 上.flat.dxf` | 857.2 × 152.4 | `99D7587CDAB88979...` == `99D7587CDAB88979...` | ✅ | ✅ |
| 13 | `structure\门框 竖隔板L.sldprt` | `structure\门框 竖隔板L.flat.dxf` | 49.3 × 1717 | `20C6C494A0E3029C...` == `20C6C494A0E3029C...` | ✅ | ✅ |
| 14 | `structure\门框 竖隔板R.SLDPRT` | `structure\门框 竖隔板R.flat.dxf` | 49.43 × 1717 | `B530CAA83119CF26...` == `B530CAA83119CF26...` | ✅ | ✅ |
| 15 | `structure\门框 下.sldprt` | `structure\门框 下.flat.dxf` | 857.2 × 122.4 | `01598A610F81A777...` == `01598A610F81A777...` | ✅ | ✅ |
| 16 | `structure\门框 右.sldprt` | `structure\门框 右.flat.dxf` | 102.9 × 1800 | `EDDBA569B28D1A4B...` == `EDDBA569B28D1A4B...` | ✅ | ✅ |
| 17 | `structure\门框 左.sldprt` | `structure\门框 左.flat.dxf` | 1800 × 100.9 | `3BBB29B07A76DF1D...` == `3BBB29B07A76DF1D...` | ✅ | ✅ |
| 18 | `structure\上盖壳体底板.sldprt` | `structure\上盖壳体底板.flat.dxf` | 509.4 × 885.2 | `2AE85EA3F46286DE...` == `2AE85EA3F46286DE...` | ✅ | ✅ |
| 19 | `structure\上盖壳体后侧板.sldprt` | `structure\上盖壳体后侧板.flat.dxf` | 182.8 × 860 | `A02527D66CF4E30C...` == `A02527D66CF4E30C...` | ✅ | ✅ |
| 20 | `structure\上盖壳体前侧板.sldprt` | `structure\上盖壳体前侧板.flat.dxf` | 860 × 109.46 | `E24F8BBE01C63B92...` == `E24F8BBE01C63B92...` | ✅ | ✅ |
| 21 | `structure\上盖壳体右侧板.SLDPRT` | `structure\上盖壳体右侧板.flat.dxf` | 180.8 × 402 | `C71104B79E917F57...` == `C71104B79E917F57...` | ✅ | ✅ |
| 22 | `structure\上盖壳体左侧板.sldprt` | `structure\上盖壳体左侧板.flat.dxf` | 402 × 180.8 | `5E5729F1026A3FC8...` == `5E5729F1026A3FC8...` | ✅ | ✅ |
| 23 | `structure\锁控维护条_源钣金.SLDPRT` | `structure\锁控维护条_源钣金.flat.dxf` | 110.4 × 1754.4 | `41CC0611A675EBD4...` == `41CC0611A675EBD4...` | ✅ | ✅ |
| 24 | `structure\维护门锁支架_原生.SLDPRT` | `structure\维护门锁支架_原生.flat.dxf` | 60.6 × 73.2 | `269F288323D81250...` == `269F288323D81250...` | ✅ | ✅ |
| 25 | `structure\箱体侧板加强筋1.sldprt` | `structure\箱体侧板加强筋1.flat.dxf` | 50 × 1688.8 | `998B103FF7DC1A19...` == `998B103FF7DC1A19...` | ✅ | ✅ |
| 26 | `structure\箱体侧板加强筋2.sldprt` | `structure\箱体侧板加强筋2.flat.dxf` | 1690.4 × 74 | `068E6BB2CE018D34...` == `068E6BB2CE018D34...` | ✅ | ✅ |
| 27 | `structure\箱体横层板加强筋.SLDPRT` | `structure\箱体横层板加强筋.flat.dxf` | 71.4 × 359 | `CF394BDDDEF003AB...` == `CF394BDDDEF003AB...` | ✅ | ✅ |
| 28 | `structure\箱体横层板L.sldprt` | `structure\箱体横层板L.flat.dxf` | 474.4 × 410.4 | `7A4397DB04FCC494...` == `7A4397DB04FCC494...` | ✅ | ✅ |
| 29 | `structure\箱体横层板R.SLDPRT` | `structure\箱体横层板R.flat.dxf` | 410.4 × 474.4 | `AA9CD854C2EAA048...` == `AA9CD854C2EAA048...` | ✅ | ✅ |
| 30 | `structure\箱体竖隔板加强件.sldprt` | `structure\箱体竖隔板加强件.flat.dxf` | 341.2 × 164.6 | `4179C1C744C03E26...` == `4179C1C744C03E26...` | ✅ | ✅ |
| 31 | `structure\箱体竖隔板L.sldprt` | `structure\箱体竖隔板L.flat.dxf` | 483.7 × 1739.6 | `51A4CFD505704B0C...` == `51A4CFD505704B0C...` | ✅ | ✅ |
| 32 | `structure\箱体竖隔板R_parametric.SLDPRT` | `structure\箱体竖隔板R_parametric.flat.dxf` | 483.71 × 1739.61 | `1B26461DA7E70F74...` == `1B26461DA7E70F74...` | ✅ | ✅ |
| 33 | `structure\箱体右侧板.sldprt` | `structure\箱体右侧板.flat.dxf` | 904.2 × 1750.8 | `3006DC3C98AB72CE...` == `3006DC3C98AB72CE...` | ✅ | ✅ |
| 34 | `structure\箱体左侧板.sldprt` | `structure\箱体左侧板.flat.dxf` | 912.2 × 1750.8 | `54AECC35D2A138BE...` == `54AECC35D2A138BE...` | ✅ | ✅ |

---

## 三、按实际配置与展开配置核对材料与重量（覆盖清单与整改）

- **所用提交**：历史勘误核对来自 `5e1a28e:docs/archives/16029-variable-trial-20261113/CORRECTIONS.md`；代码与离线测试整改位于 `e6f6adaf81babf9f8d5e06371993b2376f189359`（`TrialAssembly.cs`、`tests/TrialAssembly.OfflineTests.cs`）。
- **证据文件**：`FE4372` 交付 ZIP 内 `reports/assembly-evidence.json`（`materialCheck`、`massProperty`、`events[inspection=structural_part_material_repaired]`、`events[inspection=assembly_native_feature_health]`）、`reports/delivery-manifest.json`（`bomSummary`），以及 `dxfRunDir/native-result.json`（`deletedConfigs`）。
- **已有报告配置覆盖审计结论**：
  - 样机总装记录了整柜级 `massProperty`（总重 `83.4694 kg`、总体积 `10774.81 cm³`），但 `materialCheck` 仅保存了按文件路径去重后的 `uniquePartCount=42, missingMaterialCount=0`，**未保存装配实例实际引用的 `ReferencedConfiguration` 名称、逐配置材料库/材料名、逐件独立质量与密度读回**。
  - **34 个钣金件的展开配置全部待补读回**：从 `dxfRunDir/native-result.json` 的 `deletedConfigs` 可确认，全部 34 个装机钣金件均存在衍生展开配置（33 个为 `DefaultSM-FLAT-PATTERN`，`parts\trial_u_hook_pad.SLDPRT` 为 `SM-FLAT-PATTERN` + `DefaultSM-FLAT-PATTERN`），但现有报告**零覆盖**上述展开配置的材料与密度读回，需后续获准实跑时按新代码补采。
  - **需交工艺负责人确认材料来源的 9 个零件**：
    1. **6 个原模型无材料、经程序回退补赋 `AISI 1020 钢，冷轧`（`solidworks materials`，密度 `7870 kg/m³`，`fallbackApplied=true`）的零件**：`parts\R_W367_H335p6_panel.SLDPRT`（配置 `Default`）、`structure\箱体竖隔板R_parametric.SLDPRT`（配置 `Default`）、`parts\trial_circlip.SLDPRT`（配置 `默认`）、`parts\trial_mechanical_lock_tongue.SLDPRT`（配置 `默认`）、`parts\trial_top_latch_left.SLDPRT`（配置 `默认`）、`parts\trial_top_latch_right.SLDPRT`（配置 `默认`）。注：左门板为 `冷轧钢板` 而右门板为回退补赋 `AISI 1020 钢，冷轧`，左右门板材料库不一致；且回退补赋仅写入折叠配置，未写入 `DefaultSM-FLAT-PATTERN`。
    2. **3 个使用占位材料名的非钣金件**：`parts\trial_plastic_bushing.SLDPRT`（`外购件`）、`structure\调整脚 M12X60(模型).SLDPRT`（`外购件`）、`structure\螺母M12.SLDPRT`（`标准件`），现有证据缺少材料库与实际密度/质量核对。
    3. 另：`parts\trial_door_hinge_pin.SLDPRT` 特征树材料为 `不锈钢201`，缺少材料库与独立密度/质量读回，一并列入待补清单。
- **代码整改与离线测试结果**（`TrialAssembly.cs` & `tests/TrialAssembly.OfflineTests.cs`）：
  - 新增 `TrialAssembly.FullCabinetAssembly.EvaluateConfigurationMaterials` 与 `PartConfigMaterialObservation`，改造 `FullReopenAndMeasure`：不再按 `compPath` 去重，改为按 `(partPath, comp.ReferencedConfiguration)` 逐装配引用配置读取材料库/材料名及可用的独立 `MassProperty2`（质量/密度），并对钣金件遍历读取全部 `SM-FLAT-PATTERN` 展开配置的材料属性。
  - 离线测试 `TestConfigurationAwareMaterials`（16 项断言全部通过）：覆盖同文件同时引用两配置（第二配置缺材料 → 拒批）、折叠态有材料（`7870 kg/m³`）但展开态缺材料（`1000 kg/m³`）→ 拒批、钣金件缺少展开配置读回 → 拒批、引用配置与展开配置材料齐全 → 通过。

### 3.1 样机 `FE4372` 42 个唯一装机零件配置与材料覆盖清单

| # | 零件相对路径 | 装机数 | 钣金 | 已有报告记录的配置 | 已有材料库 | 已有材料名 | 已有密度 (kg/m³) | 独立质量 | 展开配置覆盖状态 | 材料来源确认结论 |
|---:|---|---:|---|---|---|---|---:|---|---|---|
| 01 | `parts\L_W367_H278p5_panel.SLDPRT` | 6 | 是 | 未单独记录（仅文件级默认/激活态通过） | 待补读回 | `冷轧钢板` | 待补读回 | 待补读回 | 待补读回（DXF阶段曾见展开配置 ['DefaultSM-FLAT-PATTERN']，现有报告未记录展开配置材料与密度） | 原模型自带材料（材料库、引用配置名、独立质量/密度待补读回） |
| 02 | `parts\L_W367_H278p5_stiffener.SLDPRT` | 6 | 是 | 未单独记录（仅文件级默认/激活态通过） | 待补读回 | `冷轧钢板` | 待补读回 | 待补读回 | 待补读回（DXF阶段曾见展开配置 ['DefaultSM-FLAT-PATTERN']，现有报告未记录展开配置材料与密度） | 原模型自带材料（材料库、引用配置名、独立质量/密度待补读回） |
| 03 | `parts\R_W367_H335p6_panel.SLDPRT` | 5 | 是 | Default | solidworks materials | `AISI 1020 钢，冷轧` | 7870 | 待补读回 | 待补读回（DXF阶段曾见展开配置 ['DefaultSM-FLAT-PATTERN']，现有报告未记录展开配置材料与密度） | 待工艺负责人确认（原模型无材料，程序回退补赋 AISI 1020 钢，冷轧） |
| 04 | `parts\R_W367_H335p6_stiffener.SLDPRT` | 5 | 是 | 未单独记录（仅文件级默认/激活态通过） | 待补读回 | `冷轧钢板` | 待补读回 | 待补读回 | 待补读回（DXF阶段曾见展开配置 ['DefaultSM-FLAT-PATTERN']，现有报告未记录展开配置材料与密度） | 原模型自带材料（材料库、引用配置名、独立质量/密度待补读回） |
| 05 | `parts\trial_circlip.SLDPRT` | 22 | 否 | 默认 | solidworks materials | `AISI 1020 钢，冷轧` | 7870 | 待补读回 | 不适用（非钣金件） | 待工艺负责人确认（原模型无材料，程序回退补赋 AISI 1020 钢，冷轧） |
| 06 | `parts\trial_door_hinge_pin.SLDPRT` | 11 | 否 | 未单独记录（仅文件级默认/激活态通过） | 待补读回 | `不锈钢201` | 待补读回 | 待补读回 | 不适用（非钣金件） | 原模型自带材料（材料库、引用配置名、独立质量/密度待补读回） |
| 07 | `parts\trial_hinge_latch_plate.SLDPRT` | 11 | 是 | 未单独记录（仅文件级默认/激活态通过） | 待补读回 | `冷轧钢板` | 待补读回 | 待补读回 | 待补读回（DXF阶段曾见展开配置 ['DefaultSM-FLAT-PATTERN']，现有报告未记录展开配置材料与密度） | 原模型自带材料（材料库、引用配置名、独立质量/密度待补读回） |
| 08 | `parts\trial_mechanical_lock_tongue.SLDPRT` | 11 | 否 | 默认 | solidworks materials | `AISI 1020 钢，冷轧` | 7870 | 待补读回 | 不适用（非钣金件） | 待工艺负责人确认（原模型无材料，程序回退补赋 AISI 1020 钢，冷轧） |
| 09 | `parts\trial_plastic_bushing.SLDPRT` | 22 | 否 | 未单独记录（仅文件级默认/激活态通过） | 待补读回 | `外购件` | 待补读回 | 待补读回 | 不适用（非钣金件） | 待工艺负责人确认（占位材料名“外购件”，缺少独立材料库/密度与质量读回） |
| 10 | `parts\trial_top_latch_left.SLDPRT` | 6 | 否 | 默认 | solidworks materials | `AISI 1020 钢，冷轧` | 7870 | 待补读回 | 不适用（非钣金件） | 待工艺负责人确认（原模型无材料，程序回退补赋 AISI 1020 钢，冷轧） |
| 11 | `parts\trial_top_latch_right.SLDPRT` | 5 | 否 | 默认 | solidworks materials | `AISI 1020 钢，冷轧` | 7870 | 待补读回 | 不适用（非钣金件） | 待工艺负责人确认（原模型无材料，程序回退补赋 AISI 1020 钢，冷轧） |
| 12 | `parts\trial_u_hook_pad.SLDPRT` | 11 | 是 | 未单独记录（仅文件级默认/激活态通过） | 待补读回 | `冷轧钢板` | 待补读回 | 待补读回 | 待补读回（DXF阶段曾见展开配置 ['SM-FLAT-PATTERN', 'DefaultSM-FLAT-PATTERN']，现有报告未记录展开配置材料与密度） | 原模型自带材料（材料库、引用配置名、独立质量/密度待补读回） |
| 13 | `structure\上盖壳体前侧板.sldprt` | 1 | 是 | 未单独记录（仅文件级默认/激活态通过） | 待补读回 | `冷轧钢板` | 待补读回 | 待补读回 | 待补读回（DXF阶段曾见展开配置 ['DefaultSM-FLAT-PATTERN']，现有报告未记录展开配置材料与密度） | 原模型自带材料（材料库、引用配置名、独立质量/密度待补读回） |
| 14 | `structure\上盖壳体右侧板.SLDPRT` | 1 | 是 | 未单独记录（仅文件级默认/激活态通过） | 待补读回 | `冷轧钢板` | 待补读回 | 待补读回 | 待补读回（DXF阶段曾见展开配置 ['DefaultSM-FLAT-PATTERN']，现有报告未记录展开配置材料与密度） | 原模型自带材料（材料库、引用配置名、独立质量/密度待补读回） |
| 15 | `structure\上盖壳体后侧板.sldprt` | 1 | 是 | 未单独记录（仅文件级默认/激活态通过） | 待补读回 | `冷轧钢板` | 待补读回 | 待补读回 | 待补读回（DXF阶段曾见展开配置 ['DefaultSM-FLAT-PATTERN']，现有报告未记录展开配置材料与密度） | 原模型自带材料（材料库、引用配置名、独立质量/密度待补读回） |
| 16 | `structure\上盖壳体左侧板.sldprt` | 1 | 是 | 未单独记录（仅文件级默认/激活态通过） | 待补读回 | `冷轧钢板` | 待补读回 | 待补读回 | 待补读回（DXF阶段曾见展开配置 ['DefaultSM-FLAT-PATTERN']，现有报告未记录展开配置材料与密度） | 原模型自带材料（材料库、引用配置名、独立质量/密度待补读回） |
| 17 | `structure\上盖壳体底板.sldprt` | 1 | 是 | 未单独记录（仅文件级默认/激活态通过） | 待补读回 | `冷轧钢板` | 待补读回 | 待补读回 | 待补读回（DXF阶段曾见展开配置 ['DefaultSM-FLAT-PATTERN']，现有报告未记录展开配置材料与密度） | 原模型自带材料（材料库、引用配置名、独立质量/密度待补读回） |
| 18 | `structure\底座加强筋.sldprt` | 2 | 是 | 未单独记录（仅文件级默认/激活态通过） | 待补读回 | `冷轧钢板` | 待补读回 | 待补读回 | 待补读回（DXF阶段曾见展开配置 ['DefaultSM-FLAT-PATTERN']，现有报告未记录展开配置材料与密度） | 原模型自带材料（材料库、引用配置名、独立质量/密度待补读回） |
| 19 | `structure\底座外框.sldprt` | 1 | 是 | 未单独记录（仅文件级默认/激活态通过） | 待补读回 | `冷轧钢板` | 待补读回 | 待补读回 | 待补读回（DXF阶段曾见展开配置 ['DefaultSM-FLAT-PATTERN']，现有报告未记录展开配置材料与密度） | 原模型自带材料（材料库、引用配置名、独立质量/密度待补读回） |
| 20 | `structure\底座底板.sldprt` | 1 | 是 | 未单独记录（仅文件级默认/激活态通过） | 待补读回 | `冷轧钢板` | 待补读回 | 待补读回 | 待补读回（DXF阶段曾见展开配置 ['DefaultSM-FLAT-PATTERN']，现有报告未记录展开配置材料与密度） | 原模型自带材料（材料库、引用配置名、独立质量/密度待补读回） |
| 21 | `structure\箱体侧板加强筋1.sldprt` | 10 | 是 | 未单独记录（仅文件级默认/激活态通过） | 待补读回 | `冷轧钢板` | 待补读回 | 待补读回 | 待补读回（DXF阶段曾见展开配置 ['DefaultSM-FLAT-PATTERN']，现有报告未记录展开配置材料与密度） | 原模型自带材料（材料库、引用配置名、独立质量/密度待补读回） |
| 22 | `structure\箱体侧板加强筋2.sldprt` | 2 | 是 | 未单独记录（仅文件级默认/激活态通过） | 待补读回 | `冷轧钢板` | 待补读回 | 待补读回 | 待补读回（DXF阶段曾见展开配置 ['DefaultSM-FLAT-PATTERN']，现有报告未记录展开配置材料与密度） | 原模型自带材料（材料库、引用配置名、独立质量/密度待补读回） |
| 23 | `structure\箱体右侧板.sldprt` | 1 | 是 | 未单独记录（仅文件级默认/激活态通过） | 待补读回 | `冷轧钢板` | 待补读回 | 待补读回 | 待补读回（DXF阶段曾见展开配置 ['DefaultSM-FLAT-PATTERN']，现有报告未记录展开配置材料与密度） | 原模型自带材料（材料库、引用配置名、独立质量/密度待补读回） |
| 24 | `structure\箱体左侧板.sldprt` | 1 | 是 | 未单独记录（仅文件级默认/激活态通过） | 待补读回 | `冷轧钢板` | 待补读回 | 待补读回 | 待补读回（DXF阶段曾见展开配置 ['DefaultSM-FLAT-PATTERN']，现有报告未记录展开配置材料与密度） | 原模型自带材料（材料库、引用配置名、独立质量/密度待补读回） |
| 25 | `structure\箱体横层板L.sldprt` | 5 | 是 | 未单独记录（仅文件级默认/激活态通过） | 待补读回 | `冷轧钢板` | 待补读回 | 待补读回 | 待补读回（DXF阶段曾见展开配置 ['DefaultSM-FLAT-PATTERN']，现有报告未记录展开配置材料与密度） | 原模型自带材料（材料库、引用配置名、独立质量/密度待补读回） |
| 26 | `structure\箱体横层板R.SLDPRT` | 4 | 是 | 未单独记录（仅文件级默认/激活态通过） | 待补读回 | `冷轧钢板` | 待补读回 | 待补读回 | 待补读回（DXF阶段曾见展开配置 ['DefaultSM-FLAT-PATTERN']，现有报告未记录展开配置材料与密度） | 原模型自带材料（材料库、引用配置名、独立质量/密度待补读回） |
| 27 | `structure\箱体横层板加强筋.SLDPRT` | 18 | 是 | 未单独记录（仅文件级默认/激活态通过） | 待补读回 | `冷轧钢板` | 待补读回 | 待补读回 | 待补读回（DXF阶段曾见展开配置 ['DefaultSM-FLAT-PATTERN']，现有报告未记录展开配置材料与密度） | 原模型自带材料（材料库、引用配置名、独立质量/密度待补读回） |
| 28 | `structure\箱体竖隔板L.sldprt` | 1 | 是 | 未单独记录（仅文件级默认/激活态通过） | 待补读回 | `冷轧钢板` | 待补读回 | 待补读回 | 待补读回（DXF阶段曾见展开配置 ['DefaultSM-FLAT-PATTERN']，现有报告未记录展开配置材料与密度） | 原模型自带材料（材料库、引用配置名、独立质量/密度待补读回） |
| 29 | `structure\箱体竖隔板R_parametric.SLDPRT` | 1 | 是 | Default | solidworks materials | `AISI 1020 钢，冷轧` | 7870 | 待补读回 | 待补读回（DXF阶段曾见展开配置 ['DefaultSM-FLAT-PATTERN']，现有报告未记录展开配置材料与密度） | 待工艺负责人确认（原模型无材料，程序回退补赋 AISI 1020 钢，冷轧） |
| 30 | `structure\箱体竖隔板加强件.sldprt` | 2 | 是 | 未单独记录（仅文件级默认/激活态通过） | 待补读回 | `冷轧钢板` | 待补读回 | 待补读回 | 待补读回（DXF阶段曾见展开配置 ['DefaultSM-FLAT-PATTERN']，现有报告未记录展开配置材料与密度） | 原模型自带材料（材料库、引用配置名、独立质量/密度待补读回） |
| 31 | `structure\维护门锁支架_原生.SLDPRT` | 1 | 是 | 未单独记录（仅文件级默认/激活态通过） | 待补读回 | `冷轧钢板` | 待补读回 | 待补读回 | 待补读回（DXF阶段曾见展开配置 ['DefaultSM-FLAT-PATTERN']，现有报告未记录展开配置材料与密度） | 原模型自带材料（材料库、引用配置名、独立质量/密度待补读回） |
| 32 | `structure\螺母M12.SLDPRT` | 4 | 否 | 未单独记录（仅文件级默认/激活态通过） | 待补读回 | `标准件` | 待补读回 | 待补读回 | 不适用（非钣金件） | 待工艺负责人确认（占位材料名“标准件”，缺少独立材料库/密度与质量读回） |
| 33 | `structure\调整脚 M12X60(模型).SLDPRT` | 4 | 否 | 未单独记录（仅文件级默认/激活态通过） | 待补读回 | `外购件` | 待补读回 | 待补读回 | 不适用（非钣金件） | 待工艺负责人确认（占位材料名“外购件”，缺少独立材料库/密度与质量读回） |
| 34 | `structure\锁控维护条_源钣金.SLDPRT` | 1 | 是 | 未单独记录（仅文件级默认/激活态通过） | 待补读回 | `冷轧钢板` | 待补读回 | 待补读回 | 待补读回（DXF阶段曾见展开配置 ['DefaultSM-FLAT-PATTERN']，现有报告未记录展开配置材料与密度） | 原模型自带材料（材料库、引用配置名、独立质量/密度待补读回） |
| 35 | `structure\门框 上.sldprt` | 1 | 是 | 未单独记录（仅文件级默认/激活态通过） | 待补读回 | `冷轧钢板` | 待补读回 | 待补读回 | 待补读回（DXF阶段曾见展开配置 ['DefaultSM-FLAT-PATTERN']，现有报告未记录展开配置材料与密度） | 原模型自带材料（材料库、引用配置名、独立质量/密度待补读回） |
| 36 | `structure\门框 下.sldprt` | 1 | 是 | 未单独记录（仅文件级默认/激活态通过） | 待补读回 | `冷轧钢板` | 待补读回 | 待补读回 | 待补读回（DXF阶段曾见展开配置 ['DefaultSM-FLAT-PATTERN']，现有报告未记录展开配置材料与密度） | 原模型自带材料（材料库、引用配置名、独立质量/密度待补读回） |
| 37 | `structure\门框 右.sldprt` | 1 | 是 | 未单独记录（仅文件级默认/激活态通过） | 待补读回 | `冷轧钢板` | 待补读回 | 待补读回 | 待补读回（DXF阶段曾见展开配置 ['DefaultSM-FLAT-PATTERN']，现有报告未记录展开配置材料与密度） | 原模型自带材料（材料库、引用配置名、独立质量/密度待补读回） |
| 38 | `structure\门框 左.sldprt` | 1 | 是 | 未单独记录（仅文件级默认/激活态通过） | 待补读回 | `冷轧钢板` | 待补读回 | 待补读回 | 待补读回（DXF阶段曾见展开配置 ['DefaultSM-FLAT-PATTERN']，现有报告未记录展开配置材料与密度） | 原模型自带材料（材料库、引用配置名、独立质量/密度待补读回） |
| 39 | `structure\门框 横隔板.sldprt` | 5 | 是 | 未单独记录（仅文件级默认/激活态通过） | 待补读回 | `冷轧钢板` | 待补读回 | 待补读回 | 待补读回（DXF阶段曾见展开配置 ['DefaultSM-FLAT-PATTERN']，现有报告未记录展开配置材料与密度） | 原模型自带材料（材料库、引用配置名、独立质量/密度待补读回） |
| 40 | `structure\门框 横隔板R.SLDPRT` | 4 | 是 | 未单独记录（仅文件级默认/激活态通过） | 待补读回 | `冷轧钢板` | 待补读回 | 待补读回 | 待补读回（DXF阶段曾见展开配置 ['DefaultSM-FLAT-PATTERN']，现有报告未记录展开配置材料与密度） | 原模型自带材料（材料库、引用配置名、独立质量/密度待补读回） |
| 41 | `structure\门框 竖隔板L.sldprt` | 1 | 是 | 未单独记录（仅文件级默认/激活态通过） | 待补读回 | `冷轧钢板` | 待补读回 | 待补读回 | 待补读回（DXF阶段曾见展开配置 ['DefaultSM-FLAT-PATTERN']，现有报告未记录展开配置材料与密度） | 原模型自带材料（材料库、引用配置名、独立质量/密度待补读回） |
| 42 | `structure\门框 竖隔板R.SLDPRT` | 1 | 是 | 未单独记录（仅文件级默认/激活态通过） | 待补读回 | `冷轧钢板` | 待补读回 | 待补读回 | 待补读回（DXF阶段曾见展开配置 ['DefaultSM-FLAT-PATTERN']，现有报告未记录展开配置材料与密度） | 原模型自带材料（材料库、引用配置名、独立质量/密度待补读回） |

---

## 四、仍缺输入与下一步动作（待审核与批复后执行）

1. **结构/工艺负责人确认项**：
   - 确认两类允许干涉区域（27 处上卡簧 ↔ 相邻门铰链销 `dY=-27.00 mm` 槽位允许区；11 处门板上销孔 `0.50 mm` 预偏 ↔ 上轴套允许区），确认后在 `FullClassifyInterference` 启用 `requireConfirmedPositionZones = true`。
   - 确认 6 个回退补赋 `AISI 1020 钢，冷轧` 零件（尤其是右门板与 `箱体竖隔板R_parametric`）及 3 个 `外购件`/`标准件` 的目标材料库与标准密度口径，以及钣金 `SM-FLAT-PATTERN` 展开配置是否应同步继承折叠态材料。
2. **获准实跑原生 CAD 后的闭环补证项**：
   - 固定同一提交与编译工具链，完整重跑 3D → DXF → 2D 工程图，消除当前 `FE4372` 中 3D/DXF 阶段与 2D 阶段 `toolchain.json` 的代码版本差。
   - 采回逐零件、逐引用配置、逐 `SM-FLAT-PATTERN` 展开配置的独立质量/密度与材料库读回，并补齐铰链销卡槽/上销预偏孔局部特征基准坐标读回。
   - 补做 `2D_工程图/*.SLDDRW` 迁移解压后的原生参考重开验证（注：3 份 PDF 的 180 DPI 渲染版面已在上一轮人工复核，代码侧自动化版面与迁移引用验证待补）。
   - 如拟向 `main` 发起正式 PR，先基于最新 `origin/main` 核对差异，拆出仅包含本功能及必要依赖的干净分支，避免带入 11 月 13 日归档提交 `5e1a28e` 的 675 个历史文件。
