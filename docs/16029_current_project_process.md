# 16029 当前项目总进程

更新时间：`2026-06-03`

## 当前主线

- 当前工程复核包：`16029 / 740W / L642-R246 / v43 / v43-int-v18-lockfix`
- CAD 主线：`SolidWorks 2020`
- 结构参考：`1000W x 1917H x 550D` 是 gold/source reference，用于约束内部钣金结构，不是当前下载交付包。
- 柜门边界：沿用已确认的 `740W / L642-R246 / v43` 柜门路线，不再回到历史 direct assembly 逐零件乱装配路线。
- 生成排除：电器板、电控锁、电控锁钩不进入生成包；锁侧孔位、定位孔、安装界面和 datum 必须保留。

## 当前交付资产

- 最终请求：`v43-int-v18-lockfix`
- 主装配：
  - `D:\Winnsen_Structure_Agent_Studio\workers\generated_models\review_generation_requests\v43-int-v18-lockfix\sw2020_full_740W_parametric_template\pack_and_go\candidate_16029_740W_L642_R246_v43_internal_sheetmetal_flat_full.SLDASM`
- ZIP：
  - `D:\Winnsen_Structure_Agent_Studio\workers\generation_logs\review_generation_v43-int-v18-lockfix_solidworks2020_full_assembly.zip`
- 交付 manifest：
  - `data/locker_16029_v43_internal_sheetmetal_delivery.json`
- 交付记录：
  - `workers/maintenance/16029_v43_internal_sheetmetal_delivery_handoff_20260603.md`
- 截图目录：
  - `C:\Users\Administrator\Desktop\16029_v43_internal_steps_20260603\step_v18_lock_tongue_restored`

## 当前判定

- Gold/source structure gate：`PASS`
- Structure feedback：`clean`
- 锁舌：`6` 个机械锁舌已恢复。
- 电器/电控锁残留：`0`
- 后背接缝：按侧板钣金 back flange 居中，不是贴 box。
- 用户已目视确认：锁舌位置、后背居中接缝、内部层板/前框配合。

当前包可以进入 `SolidWorks 2020` 工程复核和交付整理，但不是生产图纸释放包。

## 平台入口

- FastAPI `/api/review-downloads` 首位展示 `16029-v43-internal-sheetmetal-lockfix-zip`。
- 独立审核页 `tools/serve_16029_review_downloads.mjs` 当前轮次为 `16029-v43-internal-sheetmetal-review-20260603`。
- 默认生成提示词和参数已切到 `740W / L642-R246 / v43`，并明确排除电器板、电控锁、电控锁钩。
- 同路线旧请求如 `v43-int-v16-*`、`v43-int-v17-*`、`v43-int-v15-*` 不作为默认当前任务展示；保留直链和历史证据，不删除。
- 800W LMS/SML/DUAL 包只保留为历史参考，不再是当前工程主线。

## 验证入口

每次代码/平台规则修改后运行：

```powershell
tools\verify_16029_current_mainline.ps1 -SkipWebBuild
```

该 gate 现在覆盖：

- API 编译和审核页语法检查；
- 1000W gold/source sheet-metal evidence 和 rules；
- v43 delivery manifest gate；
- 锁舌恢复规则和 gate；
- 当前 handoff scope gate；
- 首提交范围和 staged scope guard。

## 不允许混入

- 生成模型、日志、截图、zip；
- v15/v16/v17 试错包；
- 1200W、2117H、W537 历史试错线；
- SolidWorks 2025 当前主线说法；
- FreeCAD 作为工程师交付主线；
- 仅凭截图或能打开就标成生产 release。

## 一句话结论

16029 当前已收敛到 `740W / L642-R246 / v43` 内部钣金 v18 包：柜门路线冻结，内部钣金 gate 通过，锁舌恢复，电器件排除。下一阶段如果要生产释放，需要单独做图纸、DXF/展开图、BOM、材料厚度、公差、供应商工艺和样机签核。
