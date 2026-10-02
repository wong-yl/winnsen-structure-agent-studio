# 16029 原生参数化生成交付记录

目标：保留1000原始结构规则，以V35结构工程辅助模型为质量对照，用同一套规则生成新尺寸原生SOLIDWORKS模型。此记录不替代生产设计审核或开门运动检查。

## 已接通的使用流程

本机门户：`http://127.0.0.1:5180`。在新规格表单输入宽、高、深及两列门数后提交，后台自动领取参数化任务，生成原生零件和装配，执行验收，通过后显示下载。生成过程中不提供未验证ZIP。

当前自动受理范围：700–1200 mm宽、1700–2200 mm高、250–650 mm深（2026-10-01更新）。页面支持左右列独立设置门数、逐门成品高度，总门数自动汇总为2–34门，允许左右不对称；每扇门高至少100 mm。默认保留六门L642-R246，另有四门等高预设。范围表示允许请求生成，各连续组合仍须各自验收；下表为实际完成的样本，不是规格白名单。

参数化接口的`columns[].doors[]`支持`heightUnits`比例或`heightMm`实际门高，按从下到上填写。门户每列独立选择“按高度 / 按比例”：按高度固定已知门高，自动门补齐余量，多扇自动门均分；按比例按门格节距（门高+7 mm）换算。每列门高加每门7 mm间隙须等于柜高减87 mm，分配精度为0.001 mm。预览、确认及提交采用同一组已计算的`heightMm`，无需手算。修改柜高保留固定门高并重算自动门；修改门数或“均分本列”仅重排对应列。输入无效、空间不足或全部固定后不闭合时阻止提交。只有尺寸和门序均一致才复用V35/V36/V37现成模型。

2026-10-02自动配高验收：`output/door-allocation-20261002/final-acceptance.json`，20组功能及37组布局检查通过，包含比例、混合固定/自动、精度余数、最小门高、左右独立编辑和HTTP入队一致性；在隔离数据内完成，未启动CAD。新不对称订单仍须逐单原生模型验收。

独立门序界面验收：`output/door-layout-editor-20261001/browser-1790841885616/result.json`，7项行为、6项布局检查通过，包含左3/右4共7门的逐门高度确认、真实HTTP入队保存、奇数门数、差额与最小门高校验、预设和确认失效、34门及手机布局，浏览器异常0。全门户回归：`output/playwright/16029-ui-1790841885611/result.json`，8项行为和29项布局检查通过；原有门户HTTP7项通过。均在隔离数据目录完成，没有启动CAD或改写生产任务。新不对称门序仍需逐单原生模型验收。

## 实际模型证据

| W × H × D / 门数 | 最终attempt | 验收状态 |
|---|---|---|
| 700 × 1700 × 250 / 4 | `PARAM-20261001033730211-E9FFB6` | 33展开零错误、10对应接触、原11槽网格、迁移重开、publication通过 |
| 1200 × 2200 × 650 / 6 | `PARAM-20261001014907247-86E569` | 33展开零错误、16对应接触、原11槽网格、迁移重开、publication通过 |
| 700 × 2200 × 250 / 6 | `PARAM-20261001034352876-9D7AA6` | 43展开零错误、16对应接触、原11槽网格、迁移重开、publication通过 |
| 1200 × 1700 × 650 / 6 | `PARAM-20261001035118622-A51CFB` | 43展开零错误、16对应接触、原11槽网格、迁移重开、publication通过 |
| 850 × 1950 × 380 / 6 | `PARAM-20261001013715691-79D112` | 43展开零错误、16对应接触、原11槽网格、迁移重开、publication通过 |
| 850 × 1950 × 449.9 / 6 | `PARAM-20261001020612126-8AAA93` | 43展开零错误、16对应接触、原11槽网格、迁移重开、publication通过 |
| 850 × 1950 × 450 / 6 | `PARAM-20261001031029744-4FF861` | 43展开零错误、16对应接触、原孔深度联动、迁移重开、publication通过 |
| 913 × 1917 × 550 / 4 | `PARAM-20260906115210111-B86FBD` | 33展开零错误、10处V35范围内接触、迁移重开、publication通过 |
| 839 × 1917 × 550 / 14 | `PARAM-20260907151732299-D42ADE` | 33展开零错误、40处V35按门数对应接触、迁移重开、publication通过 |
| 887 × 2017 × 550 / 4 | `PARAM-20260908052928978-57BA55` | 实测变高、33展开零错误、10处对应接触、迁移重开、publication通过 |
| 853 × 1917 × 600 / 4 | `PARAM-20260908060301076-B6694B` | 实测变深、后支撑联动、33展开零错误、10处对应接触、迁移重开、publication通过 |
| 877 × 1967 × 575 / 4 | `PARAM-20260915070745350-8EF71C` | 实测组合变化、33展开零错误、10处对应接触、迁移重开、真实门户worker自动ready |
| 889 × 1967 × 575 / 4 | `PARAM-20260915071831594-70961E` | 默认常驻队列完成；58 CAD、33展开零错误、10处对应接触、迁移重开、publication通过 |

每个attempt位于`data/parametric_attempts/`，模型在其`package/`，完整可迁移包为`native_model.zip`。877组合模型ZIP SHA256：`FB4E36646A921AC44B6AEA3A729A5ABB49C285778300DF7C4D1A7145907DCD25`。

2026-10-01七点联合门禁及500/549.9 mm完整包络补测通过后，默认`bin-unique-doors`已提升至EXE SHA `707F745DAA480FDCD3B80B68832F0A89FB6AC04208C1343D06F79530E2DEAA32`，保留旧执行器备份。门户范围patch已应用，15项生成器和7项门户接口检查通过；隔离浏览器实际核验两步提交、pending无下载、越界拒绝、普通登记、worker自动ready与真实鉴权ZIP下载，pageErrors=0。浏览器worker复用79D112已验收publication，不重复CAD建模；静态V35/V36/V37本次仅核对路由。常驻服务重启后5180/status.json为ok、auth enabled、worker idle。证据：`data/parametric_attempts/range-final-promotion-20261001.json`、`range-portal-runtime-acceptance-20261001.json`及`output/playwright/16029-range-1790827255543-127dec/result.json`。333个历史当前CAD哈希保持；制造公差、开门运动和实际净距尚未实测。

887 × 1917 × 550六门L642-R246历史生成样本`PARAM-20260906050855850-6D6F04`已完成尺寸、锁孔、干涉和展开检查，但该具体ZIP缺迁移重开收据，publication仍拒绝发布，不列为完整交付包。

## 后台与检查入口

现有`tools/run_16029_review_portal_watchdog.ps1`守护门户和参数化worker。worker采用唯一实例锁、串行领取，已有SOLIDWORKS会话时等待，不接管用户CAD进程。

```powershell
# 单次检查下一条排队任务
node tools/run_16029_parametric_portal_worker.mjs --once

# 常驻执行；正常使用时由门户watchdog负责启动
node tools/run_16029_parametric_portal_worker.mjs --watch

# 验证一个实际产物，不修改CAD
node --input-type=module -e 'import {validate16029ParametricPublication} from "./tools/lib/locker_16029_parametric_publication.mjs"; console.log(validate16029ParametricPublication("data/parametric_attempts/PARAM-20260915070745350-8EF71C"));'
```

日志：`data/review_portal_runtime/parametric-worker.stdout.log`及`parametric-worker.stderr.log`。阶段证据、授权、固定执行器和收据保存在各attempt中；不要修改它们或用手工状态改写代替验收。失败任务不会提供下载。

页面和HTTP验证：`output/playwright/16029-1789456389463/result.json`，包含真实Edge表单提交、原生任务参数保存、已完成R3模型鉴权下载SHA及零页面错误；同目录有已查看的截图。

本轮相关检查通过：参数入口14项、门户HTTP7项、参数契约、执行器原始源保护及宽度公式、实体质量拒绝用例、publication收据篡改拒绝、worker取消/外部租约/CAD忙时排队/单实例。未运行会重写R22旧工具链的总验证脚本。R22计划、装配收据和审计SHA已单独核对不变；本轮记录的365个受保护源CAD哈希均未变。

当前未完成项：887六门历史样本ZIP迁移检查。不能据此宣称所有连续参数均已通过；开门运动/生产发布审核仍不在本交付范围。
