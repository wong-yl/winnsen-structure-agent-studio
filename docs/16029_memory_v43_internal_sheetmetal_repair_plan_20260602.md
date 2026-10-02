# 16029 v43 内部钣金修正计划 - 2026-06-02

## 当前结论

- 用户已确认：`740W / L642-R246 / v43` 这一套柜门方向与大小现在是对的。
- 当前缺口：柜体内部钣金结构仍不够接近工程标准，不能因为模型能打开、能截图、能 Pack-and-Go 就标为工程就绪。
- 今天目标：沿用 v43 柜门路线，结合 `1000W` 金标准和工程师反馈截图，完成内部钣金结构修正并在 SolidWorks 2020 中打开复查。

## 边界

- `1000W` 是金标准/源结构参考，用来约束柜体内部钣金、侧板焊接、层板/前框、竖隔板加强、孔位和模块拆分。
- `740W / L642-R246 / v43` 是已验证模板种子和当前正确柜门路线，不是结构金标准。
- 生成模型不再包含电器板、电控锁、电控锁钩等电器件；但锁相关孔位、定位孔、安装界面和基准必须保留。
- SolidWorks 2020 是工程师评审与交付主线；FreeCAD 只能作内部证据或参数辅助。
- 不提交生成模型、日志、截图、zip、历史试错包。

## 必须回看的证据

- 当前正确门路线：
  - `workers/generated_models/SW-NATIVE-16029-740W-1917H-550D-L642-R246-ORDINARY-20260528/v43_full/candidate_16029_740W_L642_R246_gold_hybrid_v43_hidden_lock_body_restored_tongue.SLDASM`
- 1000W 结构规则与证据：
  - `data/locker_16029_gold_sheetmetal_rules.md`
  - `data/locker_16029_gold_sheetmetal_rules.json`
  - `data/locker_16029_gold_sheetmetal_evidence.md`
  - `data/locker_16029_verified_rule_packet.md`
  - `data/solidworks_16029_role_rules.md`
  - `data/locker_16029_structural_rule_audit.md`
  - `workers/analysis/SW2020-GOLD-STANDARD-U16029-PROBE-20260526/summary.json`
- 工程师反馈截图与归纳：
  - `workers/maintenance/16029_r3_feedback_triage_20260527.md`
  - `data/review_feedback/20260527T002004-ys8315-eed99a/01_17798411588814.png`
  - `data/review_feedback/20260527T002423-dengy123456-3fc0ff/01_17798414497188.png`
  - `data/review_feedback/20260527T002428-ys8315-7d6ea5/01_17798413961303.png`
  - `data/review_feedback/20260527T002716-dengy123456-01332a/01_17798415241528.png`
  - `data/review_feedback/20260527T002907-dengy123456-8f4cd6/01_17798416972854.png`

## 今天实施顺序

1. 冻结 v43 柜门路线：门大小、门序、左右方向、2/4/6 模板门复用不作为今天改动目标，只做回归复核。
2. 对照 `1000W` 金标准抽取内部钣金模块：
   - 左右侧板焊接/加强关系；
   - 层板与前框横梁的数量、位置和定位接口；
   - 中间竖隔板、内侧竖隔板加强板；
   - 底座、顶框、调节脚安装基准；
   - 锁侧孔位和定位孔基准，不生成电控锁实体。
3. 按工程师反馈修正三类硬问题：
   - 锁/钩/定位孔：定位孔与锁钩/锁侧安装界面必须同一基准，不能漂移；
   - 层板/前框：按门行边界生成，补定位脚和配套缺口，去掉无效长槽；
   - 竖隔板加强：补左/右两块内侧竖隔板加强板，并进入组件树和证据门禁。
4. 更新规则与门禁：
   - 组件角色必须能识别柜体、侧板焊接、层板/前框焊接、门模块、竖隔板加强、调节脚、孔位基准；
   - 禁止电器板、电控锁实体、电控锁钩进入生成包；
   - 层板/前框数量和位置必须按 `row boundary` 检查；
   - 外侧顶/侧/背面不得出现不该贯穿的孔。
5. 生成修正版 `740W / L642-R246 / v43-internal-sheetmetal` SW2020 包，打开 SolidWorks 2020 复查。
6. 截图必须包含：
   - 正面整柜；
   - 等轴测；
   - 中缝和锁侧孔位；
   - 内部层板/前框；
   - 内侧竖隔板加强；
   - 右侧板和底部调节脚。
7. 代码改动后运行：
   - `tools\verify_16029_current_mainline.ps1 -SkipWebBuild`

## 今天完成判定

- 有一个新的 SW2020 可打开修正版整柜包。
- 截图放到桌面新文件夹，能直接对比 v43 原始门路线和修正后的内部钣金。
- 明确说明每个工程师反馈项的状态：已修、仍需工程验证、或暂时不在今天范围。
- 不把结果标成 engineer-ready，除非已经按 1000W 结构金标准和 SolidWorks 2020 视觉复核确认过内部钣金质量。
