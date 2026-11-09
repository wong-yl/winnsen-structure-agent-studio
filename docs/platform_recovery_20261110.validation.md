# 上传前验证记录

日期：2026-11-10。执行目录为独立发布工作树，不修改运行中的原工作区。检查只覆盖本次源码快照，真实 CAD 验收沿用修复阶段记录。

| 检查 | 命令与结果 |
|---|---|
| 后板宽度几何 | `node workers/native_model_requests/parametric_v1/repairs/readonly-open-20261109-r4-rearwidth1/test_rear_width.mjs`：92通过，0失败，源码与夹具未变，实际 DoorPlacement SHA256仍为 `4337E70001B449355F28FBD2572B8CDA2F422CA71484F66699B08640C6E2F96C` |
| 双段运行衔接 | `node tools/verify_16029_parametric_runtime.test.mjs`：162项，161通过、0失败、1跳过；跳过的是本机真实前段记录回放，历史任务没有上传。完整本机记录在修复阶段已通过 |
| 续跑阶段筛选 | `node tools/verify_16029_parametric_resume.test.mjs`：2通过，0失败 |
| 构建身份 | `node tools/verify_16029_parametric_build.test.mjs`：通过，绑定十份源码、已接受程序、manifest、包及物理覆盖，拒绝身份漂移 |
| 超时与取消 | `node tools/verify_16029_parametric_process.test.mjs`：通过，没有启动 CAD |
| 网页接单后台 | `node tools/verify_16029_parametric_portal_worker.mjs`：通过，覆盖取消、租约和检查点，不启动 CAD |
| 物理覆盖 | `node tools/verify_16029_parametric_physical_quality.test.mjs`：通过，覆盖缺失展开、重复身份、实体/检查对缺口和异常接触拒绝 |
| 交付绑定 | `node tools/verify_16029_parametric_publication.mjs`：通过 |
| 任务状态 | `node tools/verify_16029_native_task_store.mjs`：通过 |
| 原生流程合同 | 下列十一份 fixture 检查全部通过；仅使用隔离测试数据，不运行模型生成 |
| 原始源码适配器 | `node workers/native_model_requests/parametric_v1/verify_adapters.mjs`：通过，源字节与漂移拒绝检查一致 |
| 文件范围拦截回归 | `./tools/test_check_project_file_scope.ps1`：31路径通过 |
| 新增/修改JS语法 | 53份 `.mjs` 的 `node --check`：全部通过 |
| 静态导入 | 检查101个相对导入：缺失0；门户1个绝对D盘导入已归档并记录恢复路径，不能据此称独立运行包 |
| 网页 | `npm ci`、`npm run build`、`npm run lint`：通过 |
| API（后台接口） | `python -m unittest discover -s tests -v`：28项，27通过、0失败、1跳过；跳过是当前Windows账号无创建符号链接的权限，未为此提升权限 |
| 依赖安全 | `npm audit --json`：未通过，12项（2高、10中、0严重），详细范围见下文 |

原生流程十一项：`verify_16029_native_generator`、`verify_16029_native_recipe_registry --fixture-only`、`verify_16029_native_execution_authorization`、`verify_16029_native_assembly_contract --fixture-only`、`verify_16029_native_stage_contract`、`verify_16029_native_toolchain_manifests`、`verify_16029_native_worker`、`verify_16029_native_stage_executor --fixture-only`、`verify_16029_native_publication`、`verify_16029_parametric_input_boundaries`、`verify_16029_native_portal_session`。命令统一为 `node tools/<名称>.mjs` 后加所列参数；session 检查使用临时端口、虚构账号和隔离数据，没有操作真实账号或生成队列。

依赖审计高项为 `vite`、间接依赖 `zod`，中项主要为 ESLint/TypeScript ESLint 的 `minimatch` 依赖链。审计数量包含依赖链传播，不等于12个独立漏洞。当前锁文件被原样保存；未执行自动强制升级。此分支是可追溯源码备份，不能宣传为全部安全问题已清零。

最终125份暂存文件已通过 `git diff --cached --check`、项目文件范围守卫；120份来源清单与Git blob（将上传的文件内容）逐份匹配，未含账号、密钥或模型/二进制生成物。独立只读核对未发现新阻塞。上传后还会通过GitHub API读取分支SHA和tree进行核对；最终提交号保存在本机上传回执，避免将回执写进它自身所标识的提交。

未在本次上传中重新启动SOLIDWORKS、生成任何新模型或修改用户已打开的模型；未重跑会写历史门禁的室外 `verify_runtime.mjs`/`verify_candidate_r24.py`。未验证：缺整段尺寸、新工艺、门全行程、完整图纸及最终生产签核。
