# 16029 Git 清理边界

更新时间：2026-05-28
原则：不清空、不回滚、不整仓上传；先分类，再只提交当前主线需要的最小集合。

## 一、当前 git 状态判断

当前仓库的主要问题已经从“大量生成物混在一起”收敛为“少量当前主线文件 + 一批 tracked 历史改动需要人工判断”。

项目代码管理总原则见 `docs/project_code_management_policy.md`。后续清理、提交候选、历史证据隔离都按该文件执行。

2026-05-28 第二轮清理后：

- Git 视图剩余 58 项
- Modified 36 项
- Untracked 22 项
- `data` 中只保留当前 route manifest，gate/validation/review 输出已通过 `.gitignore` 排除
- `workers` 中未跟踪项只保留当前 gold-variable 候选脚本，旧宽高/门序/反馈轮次脚本已从当前视图排除
- 自动分类脚本 `tools/check_16029_first_commit_scope.ps1` 当前结果：33 个 include、10 个 exclude、15 个 needs_decision、0 个 uncategorized
- 待判断审计脚本 `tools/audit_16029_needs_decision.ps1` 当前结果：PASS，15 个 defer，0 个 unclassified
- 总验证脚本 `tools/verify_16029_current_mainline.ps1` 当前结果：PASS，7 checks，0 failed
- `tools/stage_16029_first_commit.ps1` 默认 dry-run，只在明确批准后用 `-Apply` 暂存第一批 include 文件
- `tools/guard_16029_staged_scope.ps1` 用于提交前检查暂存区是否混入 exclude 或 needs_decision 文件

已经从当前视图排除的噪音：

- 多轮 16029 宽度/高度/门序试错数据
- 多个 handoff zip 和解压目录
- NESTED_LEGACY 备份包
- tmp JSON、tmp PNG、临时截图
- review 用户、下载和反馈数据
- R3/R4/R5/R6/R7 反馈修复脚本
- 编译输出目录和旧 CAD 探针

## 二、可以进入提交候选的内容

这类文件可以进入下一轮人工确认，但不是现在直接全提交：

- 当前项目主线说明文档
- 当前 16029 route manifest
- 当前 handoff scope gate 脚本
- 当前工程师审核登录系统入口和界面文案
- 当前 API 中只暴露 800W gold-variable 的接口调整
- SolidWorks 2020 默认路径和软件主线配置
- 当前 LMS/SML/DUAL 打包脚本
- SolidWorks 2020 打开截图验证脚本，前提是后续能跑通

## 三、暂不提交的内容

这类内容可能有用，但现在不要直接提交：

- 当前 LMS/SML/DUAL zip 包
- 当前 LMS/SML/DUAL 解压目录
- 当前 gate 输出 CSV/JSON/MD
- review 登录用户 JSON
- review feedback 目录
- 新生成的截图证据
- 编译后的 exe 和 dll
- 任何还没有通过 SolidWorks 2020 截图 gate 的模型包

原因：这些文件很多是生成产物或本机审核状态，应该先确认是否需要纳入版本管理，不能直接把整个 dirty worktree 上传。

## 四、必须排除当前提交的内容

这些内容属于历史试错、废线或临时产物，不进入当前主线提交：

- `16029_WIDTH_CANDIDATE*`
- `16029_HEIGHT_CANDIDATE*`
- `16029_800W_*RULE_REVIEW*`
- `16029_10_12_14*`
- `*_NESTED_LEGACY_*`
- `workers/tmp_*`
- R3/R4/R5/R6/R7 反馈修复试错目录
- 1200W、1000W、2117H、W537 相关证据
- FreeCAD-only 截图或仅用于内部历史的 FCStd 证据

这些不是删除建议，只是当前不进提交、不进工程师界面、不作为当前交付依据。

## 五、当前提交建议

第一批只适合提交整理边界和防呆逻辑，不适合提交模型包：

- `docs/16029_current_project_process.md`
- `docs/16029_git_cleanup_boundary.md`
- `data/locker_16029_project_route_manifest.md`
- 当前审核界面中去掉错误路线和繁杂信息的最小改动
- 当前 scope gate 中要求 SolidWorks 2020 截图证据的最小改动

第二批现在已经具备 SolidWorks 2020 证据，可以进入提交候选审查：

- 打包脚本
- 验证脚本
- 当前审核系统下载目录
- 当前 LMS/SML/DUAL 包

## 六、第二轮清理后的剩余提交候选

明确保留在当前视图里的 untracked 文件：

- `data/locker_16029_project_route_manifest.json`
- `data/locker_16029_project_route_manifest.md`
- `docs/16029_current_project_process.md`
- `docs/16029_git_cleanup_boundary.md`
- `tools/serve_16029_review_downloads.mjs`
- `tools/start_16029_review_portal.mjs`
- `tools/run_16029_review_portal_watchdog.ps1`
- `tools/render_16029_project_flow_pdf.mjs`
- `tools/audit_16029_needs_decision.ps1`
- `workers/maintenance/generate_16029_800w_gold_variable_model_freecad.py`
- `workers/maintenance/finalize_16029_800w_gold_variable_handoff.py`
- `workers/maintenance/validate_16029_current_handoff_scope.ps1`
- `workers/maintenance/build_16029_dimension_contract.py`
- `workers/maintenance/validate_16029_dimension_contract.py`
- `workers/maintenance/build_16029_variable_door_stack_contract.py`
- `workers/maintenance/build_16029_800w_lms_contract.py`

Tracked 但需要人工判断的 worker 改动：

- `workers/solidworks_tools/StepOpenProbe.cs`
- `workers/solidworks_tools/build_step_open_probe.ps1`
- SolidWorks 2020 相关构建脚本和模块生成器
- `workers/solidworks_tools/bin/*` 编译产物

其中 `StepOpenProbe.cs` 和 `build_step_open_probe.ps1` 是当前 2020 打开截图证据链需要的源码；`bin/*` 是编译结果，不建议作为第一批提交内容，除非决定继续把二进制工具纳入仓库。

## 七、下一步执行顺序

1. 固定当前主线文档。
2. 固定 git 分类边界。
3. 对工程师审核界面做减法，只保留当前三包。
4. 跑当前 scope gate，要求 PASS。
5. 用 SolidWorks 2020 对 LMS/SML 打开截图。
6. gate PASS 后，只把当前主线脚本、界面/API 改动和路由清单作为提交候选；模型包继续作为外部分发物，不直接塞进 Git。

## 八、当前一句话结论

现在不要上传 13 万行混杂改动；当前可提交候选应只覆盖 16029 当前主线、SolidWorks 2020 防呆、工程师界面/API 和审核路由清单。模型包、截图、gate 输出和历史试错继续留在本机证据区，不进 Git。
