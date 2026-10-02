# 16029 参数化执行进度（2026-09-06）

继续依据 `16029_execution_handoff_20260905.md`，目标仍为1000原始结构、V35认可质量的结构工程辅助模型。原计划执行中，未完成高度/深度和门户自动生成闭环，不宣告项目完成。

## 可复用的最新实际模型

887W × 1917H × 550D、两列六门 L642-R246：

- attempt：`data/parametric_attempts/PARAM-20260906050855850-6D6F04`
- 原生包：`package/16029_887W_1917H_550D.SLDASM`，包内87个CAD文件。
- ZIP：`16029_887W_1917H_550D_L642-R246_engineering_candidate.zip`，28,199,618 bytes；SHA256 `BF68D617E6CD0E4EA58E13F93B9459B4847C4B35EE43FD1DBCA966478B736D8B`。
- `evidence/verify-package.json`：实测外宽887.000 mm，六门、六锁舌，六个锁舌与各自孔中心对齐，重建特征问题0，包内引用通过。
- `evidence/physical-audit.json`：42个钣金件均实际取消FlatPattern抑制并重建，展开错误0；16处静态接触。
- 同方法只读V35副本：`data/parametric_attempts/PARAM-20260905130307482-41DE53/evidence/physical-audit.json`，16处相同的门板/插销板微接触及销/挡圈接触。比较命令 `node tools/verify_16029_parametric_physical_quality.mjs <V35证据> <887证据> 6` 通过；无新增接触类型、数量或体积超限。
- 门板与加强筋是真实原生参数驱动；上插销板使用V35已认可的固定零件几何，依新门宽/门高平移安装，不重新调用STEP/FreeCAD生成。
- 部分门装配打开时仍有NeedsRegen(32)，重建后特征无错误；不得称其无打开警告。ZIP解压迁移重开尚需最后验证；当前仍为工程辅助候选。

## 已完成的关键修正

913W × 1917H × 550D、两列四门等高已通过当前静态工程辅助验收：

- 最终attempt：`data/parametric_attempts/PARAM-20260906115210111-B86FBD`，原生根装配`package/16029_913W_1917H_550D.SLDASM`，58个CAD文件。
- `verify-package.json`和`verify-relocated.json`成功，实测913.000 mm、4门4锁舌、特征问题0、引用均在对应包内；ZIP解压字节一致、只读重开文件哈希不变。
- `native_model.zip` SHA256 `B5E0C090295C02394E7D22F2992C4E6BB9BAA14B487D60F3A69A2B5E0AA2B859`。
- `physical-quality.json`：pass=true，33个钣金件实际展开错误0，10处接触只含V35既有三类，数量按4门缩放且体积无超限。
- 该结果仍不代表完整项目完成或生产发布；839W十四门、H/D和门户闭环待完成。

1. `runNativeWorkerFromCheckpoint` catch只在本次已原子取得且仍持有同一workerId/leaseId时修改状态。新增回归先失败后通过，worker14/14，task store18/18。未改R22计划、收据、工具链。
2. 运行时统一参数：`tools/lib/locker_16029_parametric_contract.mjs`。连续W公式、各列门高/门数/门序、实际孔中心；H/D只计算，尚未开放原生运行。
3. `workers/native_model_requests/parametric_v1/`编译复用原生操作。旧V37宽度源码、固定888源码与exe不写；适配器核对原始源码SHA后生成编译副本。
4. 原始门板草图12的两个旧E盘装配点引用在副本内解除。右件MirrorPart2(BreakLink=true)返回完整本地原生特征树而非MirrorPartFeatureData，已以左右228顶点实际镜像等价、单实体/钣金/展开/外部引用验证。
5. 修复组件自动重命名、重复五金打开、先重建保存、迁移校验使用已移动的旧路径等原型错误。不是降低几何门禁。
6. 根据V35实测矩阵纠正右门加强筋及五金方向、上插销板几何，消除28,337 mm³等真实穿透。
7. 真正边界位置：门间隙中心不等于横档/层板实体中心。层板中心=门间隙中心−1 mm，横档中心=门间隙中心−6 mm；装配源中心分别1705/1700。此规则已用V35实际包围盒回归验证，消除横档与门板穿透。
8. 右侧板草图19旧衣杆固定支架外部约束解除，孔几何保留，消除整柜重建的51/1错误。
9. 底座脚孔实际驱动是`草图_调整脚Ø15_X335_V33`内两个圆心，而不是已抑制的移动面2/3。现按±(W/2−35)驱动并验证圆柱孔轴，消除新增4处脚座穿透。失败的移动面尝试已回滚。

## 当前正在执行

913W × 1917H × 550D、两列四门等高，从V35新隔离副本完整运行：

- request：`data/parametric_913_four_request.json`
- 已验证的锁处理前续跑源：`data/parametric_attempts/PARAM-20260906054751992-99FD64`。尺寸、派生件、底座孔、8个柜体装配及24个变换、908 mm门模块、门五金/接口/脚孔修正均已完成；不要重生成这些阶段。
- 2026-09-06 18:23锁驱动证据：`PARAM-20260906102244445-A2C187/evidence/locks.json`。直接改草图块位置会在重建后复位；正确驱动为`D2@草图6 = 76.25 + 1706 - 顶部门中心Y`。本例381.25 mm驱动出1401 mm锁槽，配套圆孔通过原有约束自动到1341 mm；915 mm阵列生成486/1401锁槽和426/1341圆孔，22边轮廓指纹完整、特征错误/警告0。
- 上述attempt在右隔板镜像阶段失败并已回滚；不能当作锁阶段通过。`PARAM-20260906102657763-586D28`和`PARAM-20260906105629934-225E84`证明新MirrorPart2只有MirrorStock而无SheetMetal/FlatPattern；后者已排除窗口激活影响。`PARAM-20260906105953542-3C8230`尝试BreakAllExternalFileReferences2无效（关联计数仍4），此试验分支已移除，遗留的所属SW PID23728按会话启动ticks核对后终止。
- V35右隔板旧镜像虽然能列出引用，但状态0在实际interop枚举中是Broken；换路径、配置和ModifyDefinition都不更新实体，旧链复用分支已移除。正确方式是新建右列本地参数源`箱体竖隔板R参数源.SLDPRT`，MirrorPart2(false, ImportSolids)保留关联，再按V35实际固定面及钣金定义调用InsertBends2。读取值R0.7366、厚0.8、K0.333333、BendAllowance0.0014、自动释放true、比率0.5。
- `PARAM-20260906111643077-ABDE82`的locks、locks-verify、root、verify、package均通过。左右668条边双向镜像对照未匹配0，913 mm外宽、4门4锁舌及孔中心正确。但verify-package发现右隔板焊接MateGroup错误，不能放行。
- 只读配合诊断`PARAM-20260906113931881-3D5123/evidence/probe-partition-mates.json`证明距离1、同心1–4的右隔板面引用失效（51）；加强筋变换尚在原位。按真实平面/圆柱轴线匹配唯一对应面，保留原配合类型、0.2 mm距离、方向和锁转设置，以AddMate5重建后删除失效配合。AddMate5的NoError枚举值为1，不能与Save的0混用。
- `PARAM-20260906114703127-3F2BE2/evidence/repair-partition-mates.json`已通过5处修复，全部子配合错误0、全部组件变换不变。verify和package已通过，当前verify-package；最新日志`data/parametric_attempts/913-four-20260906194703.stdout.log`及`.stderr.log`。续跑入口`data/parametric_913_continue.mjs`复用ABDE82源；常规入口也已加入repair-partition-mates阶段。结果需现场核对，不得同时启动第二个SolidWorks任务。
- 原生入口：`node tools/run_16029_parametric_model.mjs <request.json>`。
- 重编译：`node workers/native_model_requests/parametric_v1/build.mjs unique-doors`。每个新attempt把exe和interop复制进自己的`tools/`，正在运行的任务不受后续编译影响。
- 若一个阶段失败，查看`evidence/<phase>.json`、`.process.json`、`.rollback.json`以及会话PID证据。修具体根因，只复用有收据和哈希绑定的已完成阶段，不重跑完整基线。

## 仍必须完成

- 913四门的实际锁拓扑、总装、独立结构对照；十四门与不同门序复验。
- ZIP解压新位置重开，完整对应目录的引用和哈希检查。
- H/D驱动与接口联动。只读主模型已发现高`D2@草图1`、深`D1@凸台-拉伸1`；仍需所有关联钣金/孔/装配的真实验证，不能只改这两个值就宣告支持。
- 门户输入接入真正运行与经验证的新包下载，保持模型就绪与任务登记的区分。
- 减少已用毕的诊断代码和临时入口，并使最终源代码/证据可审查。无需清理或删除历史模型。

2026-09-06 20:38 已开始839W十四门全链运行，request `data/parametric_839_fourteen_request.json`，每列7门，门宽356.5 mm、门高254.428571428571 mm。日志`data/parametric_attempts/839-fourteen-20260906203835.stdout.log`及`.stderr.log`。启动PID21044仅为当时记录，续跑时以日志和会话证据核对当前状态。

839第一次attempt `PARAM-20260906123835319-60F748`已通过dimensions、21个derived、base-hole、8个assemblies和door-254p428571（左右228顶点反射通过）。repair-door-placements启动时执行进程被中断，只剩SW24456（2026-09-06 20:50:16.371024，-Embedding）及子monitor22844；按精确启动时间/进程身份核对后终止。门收据绑定的88个CAD文件全部未变，未完成阶段不计通过。当前恢复attempt `PARAM-20260906125701574-4551C4`，日志`839-resume-20260906205701.stdout.log`及`.stderr.log`；只续跑五金、锁、总装及验收。

新增`NativeParametricEnvelope.cs`为待合并的诊断spike，只测单一H或D变化及关联件响应，不开放交付；编译标签envelope-probe。准备请求`data/parametric_height_probe_request.json`（2017H/550D）、`data/parametric_depth_probe_request.json`（1917H/600D），须在当前CAD空闲后以`phases:['clone','probe-envelope']`及`buildTag:'envelope-probe'`启动。入口已拒绝该诊断混入package或resume，3个负例通过，尚未CAD运行。

2026-09-07续跑实况：高度诊断`PARAM-20260906130141243-FB6337`已完成测量（不是验收通过）。主模型实际升到2017；大多数关联件上移/伸长100 mm，底座保持原位。上盖壳体底板变为5实体、7错误/6警告；右竖隔板因Broken旧镜像仍停在1858.2，需分别修复。深度600仍未实跑。

839锁拓扑前最佳源仍为`PARAM-20260906125701574-4551C4`（五金、接口、脚孔已通过）。顶部目标1727.785714：圆孔1667.785714正确，锁槽仅13/22边匹配，上半9边在Y1730.75截断。`PARAM-20260907001245074-FEAD3A/evidence/locks.json`保留完整seedGeometry，不能改容差放行。当前入口`data/parametric_839_continue.mjs`只续锁及后续阶段；最新诊断attempt `PARAM-20260907143407566-59C7F5`，日志`839-locks-20260907223407.stdout.log`及`.stderr.log`，新增截断邻面所属特征记录。

22:38修复方向已具体化：截断边所属为`拉伸-薄壁4`，草图7位于Y1803.75，其补料边界Y1730.75覆盖上移后的种子孔。保留补料与钣金，不修改原结构；选取请求中不高于原1706种子基准的最高门中心作为种子，其他行用同一原生二实例阵列分别向上/向下。当前839种子1466.357143，顶部1727.785714由最后的向上阵列生成。attempt `PARAM-20260907143839895-894CFD`，日志`839-locks-20260907223839.stdout.log`及`.stderr.log`，待实际22边孔形及后续验收，不得预判通过。

22:54实际进展：仅换种子和GeometryPattern=true仍会被补料覆盖。新增将向上阵列ReorderFeature到`拉伸-薄壁4`之后（保留原生折弯处理）后，`PARAM-20260907145146239-6A867E`左列七孔完整通过，右件496顶点反射通过；当前等待锁阶段收尾与独立重开。日志`839-locks-20260907225146.stdout.log`及`.stderr.log`。后续阶段已在同一执行器内顺序安排，不要重复生成柜体/门模块。

23:10更新：上述6A867E已通过locks/locks-verify、5条配合修复、root、verify、package、verify-package、verify-relocated；14门14锁舌、实测839 mm、特征问题0。33件钣金展开无错误，但物理质量FAIL：64接触=40处V35按门数折算既有接触+24处横隔板插舌/竖框穿透（每端约5.184 mm³）。根因是竖框标准槽阵列固定152.5间距。

23:38最终十四门候选已通过：`PARAM-20260907151732299-D42ADE`，`package/16029_839W_1917H_550D.SLDASM`、58 CAD，ZIP SHA256 `AEAD5340476C8F69D2EA0A150A69176CC055F5AD7753C62C0F48FBF0FB876EB6`。门框槽修复后40接触均为V35三类、无新增/超限，33件实际展开无错误，包重开/解压重开通过。只读`validate16029ParametricPublication`已核对4个ancestor的完成收据及最终ZIP/库存/物理对照，返回ok=true。仍为静态工程辅助，不是整项目完成。

深度600诊断已启动：入口`data/parametric_depth_probe.mjs`，日志`depth-probe-20260907233759.stdout.log`及`.stderr.log`，buildTag=envelope-probe。只做clone/probe-envelope，不发布模型。当前持续执行应从此日志接上。

深度诊断已完成`PARAM-20260907153759191-BE1AD2`：全部测量件healthy=true，主模型/底座/上盖/层板/侧板实际加深50 mm；前门框及前加强件保持前基准，右竖隔板Broken旧镜像未加深，须由新镜像路径处理。D4@草图130仍426 mm。未做600D整柜装配与物理验收，不表示深度支持已开放。

高度详细诊断`PARAM-20260907154054053-927D6D`确认唯一异常件为上盖壳体底板：草图2 code51、凸台-拉伸1 code1、切除-拉伸2 code51、草图7 code51、切除-拉伸4 code1。当前只测主模型→上盖模型→底板的focused诊断`PARAM-20260907154434960-0F7891`，记录草图约束和坐标，不再重跑其他已测关联件。

门户进展：`locker_16029_native_generator.mjs`现在可受理显式parametricRequest（cabinet+L/R doors），保留完整门序并纳入fingerprint；task store条件性纳入不可变请求身份，历史任务digest不变；HTTP提交落盘保存该对象。generator14/14、store19/19、隔离portal7/7通过。UI及实际worker消费/ready发布/下载接线仍未完成。

新增只读交付检查`tools/lib/locker_16029_parametric_publication.mjs`验证阶段收据/源码exe/上游resume链、请求计算、包库存及解压库存hash、锁舌位置、ZIP SHA、固定V35基准及重新计算物理对照、源hash。`tools/verify_16029_parametric_publication.mjs`已接受完整913和839，拒绝不同参数/缺迁移/物理失败/诊断/越界路径。尚需篡改文件负例及接入worker，未改线上任务ready状态。

2026-09-08高度修复续进：`PARAM-20260907235418183-F67107`在原高度副本中删除草图2的3条、草图7的7条外部约束，保留原尺寸；原高度特征/实体/包围盒检查通过，升高2017后原来的草图51及凸台1错误消失，仅切除-拉伸2 code51、实体仍5。其AutoSelect=true试验`PARAM-20260907235742864-150A76`无效，试验修改已从代码移除。当前`PARAM-20260907235935193-1CBE24`按rollback逐特征记录上盖底板实体变化，定位最先分裂的位置。入口仍是focused topCoverOnly诊断，不应把success=true当H支持通过。

高度底板修复已实测成功：`PARAM-20260908000213446-F7E5B2`。真正上游问题是切除-拉伸5的草图8仍在旧Y坐标切掉底板前部，导致后续凸台悬空。基线内解除草图2/7/8的3/7/4条外部约束且原几何不变；草图8通过SketchModifyTranslate沿局部Y平移H−1917。2017H时底板仍1实体66特征、0错误/0警告、X/Z不变、Y整体+100。

当前H全链：request `data/parametric_887_2017_four_request.json`（887W2017H550D四门）。runner/native范围实验开放1817–2017H，门户仍仅1917；新增envelope阶段复用上述修复并严格检查关联件目标Y边界，右竖隔板留给后续新镜像重建；层板加强筋装配Y同步H差值，root层板/横档源Y扣除H差值；锁槽种子datum以1706+H差值计算；最终包增加实测顶部高度/深度门禁。变H发布要求envelope收据和实测H/D。

第一次H全链`PARAM-20260908001621443-15DCA4`通过尺寸/21派生件/base-hole，但assemblies期间执行进程退出且无收据。所属SW18940（2026-09-08 08:22:50）按精确身份核对终止，backups/assemblies恢复后75文件与base-hole收据一致。现在采用Win32_Process.Create独立后台node（隐藏窗口），入口`data/parametric_height_resume.mjs`，固定日志`data/parametric_attempts/height2017-durable.log`，当前attempt `PARAM-20260908003150335-6E98B8`，PID25660（当时记录）；从base-hole继续，避免会话切换中断子进程。以日志/进程/收据现场核对，不得同时启第二CAD。

门户接线新增：只读inspect/open参数化publication已接publicGenerationRequest及同一鉴权下载路由，ZIP以打开fd再hash确认。隔离HTTP下载913真实ZIP200且hash匹配，篡改task中的archiveSHA返回409；7/7 portal测试通过。UI对受理范围内的新规格自动发送parametricRequest（暂未提供自定义门序UI）。taskstore新增claimTask精确原子领取参数化taskId，旧claimNext排除此类任务；20/20 store测试通过。子代理portal_worker_bridge拥有两个新worker文件，尚待其交付；不要修改它的文件或运行真实portal worker抢CAD。

2026-09-08后续：子代理未交付文件且已不在运行列表，根代理已接管并落地`run_16029_parametric_portal_worker.mjs`和验证脚本。明确taskId→原子claimTask→planning/building→调用真实runner→publication验证→complete；heartbeat/取消向内嵌task传递，只有自有lease才失败收尾。无CAD注入测试通过真实913验收ready、验收拒绝failed、取消cancelled、旧任务拒绝、foreign lease不修改。尚未真实门户任务调用CAD和服务部署。

H整柜`PARAM-20260908003435764-19EE1F`通过envelope、958mm门模块和locks-verify；上部同心孔按H差值重新匹配后，`PARAM-20260908011100666-FE545F`通过root/frame-slots/verify/package/verify-package/verify-relocated，实测887W2017H550D、4门4锁、特征问题0、33件展开无错。但14接触包含4处中央加强件/隔板凸台接触。整体平移两加强件的试验`PARAM-20260908011747701-D77640`只消除2处且上件被配合保持915，不能放行。

13:26实际孔轴诊断`PARAM-20260908052622065-AED720`：加强件与主模型安装孔Y674.8/1589.8固定未变，新增穿透来自隔板凸台-拉伸3/阵列4的Ø4定位凸台随H上移（Y683.2/1598.2）。修复改为保留加强件原变换0/915，以隔板L及R参数源`D5@草图19=360+(H-1917)`保持原安装组Y298.2/583.2/1213.2/1498.2，真实圆柱轴线逐项验证。此替代了错误的统一平移加强件方案；当前使用同一`data/parametric_height_resume.mjs`和`height2017-durable.log`执行（WMI独立隐藏后台）。读取日志最新taskId继续。

变高度完整验收已通过：`PARAM-20260908052928978-57BA55`为887W2017H550D四门，33件展开0错误、10处V35既有接触、包实测W/H/D及迁移重开通过，publication验收ok=true，ZIP SHA256 `10020A2E382BD82FE72B0572B96CD83B2B19C3CFEA142A4FBB316F770C440BF0`。门户已增加1817–2017柜高输入与完整parametricRequest，未验证组合仍必须执行全验收；显式旧固定入口非1917H拒绝，不再静默重写。generator14/14、portal7/7（含生成页面JS语法、真实ZIP下载及错误SHA409）通过。

当前深度全链为853W1917H600D四门：request `data/parametric_853_600_four_request.json`，WMI入口`data/parametric_depth_full.mjs`、固定日志`data/parametric_attempts/depth600-durable.log`，attempt `PARAM-20260908053453512-9E55FD`，PID19452（当时记录）。已通过尺寸/derived/base-hole/assemblies，当前envelope逐件目标Z门禁。runner/native已实验允许550–600D，但暂不同时变H/D；门户仍550D参数化受理，待600D最终验收后开放。新增repair-depth-placements代码（后加强筋/螺母/脚向后随D移动），尚未编译入当前已锁定exe、未实跑，需根据完整物理结果决定接入；别预判安装变化通过。

原生清单/JS复制统一排除`~$`临时锁文件，修复恢复后75件误算80件的问题。SetPlacement新增实际变换回读，不把SetTransformAndSolve2返回true当移动成功。height brace修复已经幂等。当前没有活动子代理；新worker两个文件已由根代理接管。--once读取下一条created参数化任务并原子claimTask，旧任务不抢；真实门户任务→CAD→download全链尚未运行，后台常驻队列尚未部署。

600D全链首次`PARAM-20260908053453512-9E55FD`已通过W853/H1917/D600实测、4门4锁、33展开、迁移重开，仅新增1处底座后加强筋/底板接触1.12 mm³。直接移后加强筋被同心配合保持旧位置，回读门禁拦截。`PARAM-20260908060048705-D4F8C8`配合诊断显示同心7将后筋绑定到后敲落孔（Z−500.5）。正确修复为草图9两个后敲落孔块由500.5到550.5、前孔74.5不变，保留原生块/孔形；重建后筋Z−625，再核对两后螺母/脚Z−550.5。

当前`PARAM-20260908060301076-B6694B`修复阶段已成功、孔深度中心实测74.5/74.5/550.5/550.5，五个后组件位置回读通过，verify通过，正在package及后续物理验收。`data/parametric_depth_full.mjs`现复用9E55FD，仅clone/repair-depth-placements/verify/package/verify-package/verify-relocated/physical-audit；日志仍depth600-durable.log。常规runner已加入depth-placement阶段；原来拟移动侧板/层板加强筋的未验证草案已删，只保留必要底座支撑组。

600D最终已通过：`PARAM-20260908060301076-B6694B`实测853W1917H600D四门、33展开0错误、10处V35既有接触、迁移重开及publication核验通过；ZIP SHA256 `1877753C37EF904475E1693B76E57ED9B5A8300BF2DD6E0789B364F33CDE3AF6`。

组合W/H/D门户真实运行：`data/parametric_portal_runtime.mjs`调用worker，隔离门户store为`data/parametric_portal_acceptance`，请求877W1967H575D四门。首任务`PARAMETRIC-PORTAL-877-1967-575-20260908`确实调用CAD但主模型切除-拉伸60 code51，门户正确failed/modelReady=false。诊断`PARAM-20260908061822795-237886`证实草图137固定350孔距在877W外孔恰好相切横档边缘。原始1000主模型只读副本`PARAM-20260908062030841-2AE8EA`：孔距350，外/内孔中心与横档边界间距32.5/58.5。

2026-09-15修复width_adapter新增原生驱动D3@草图137=(W−300)/2，1000仍350，877变288.5，保留原边距。executor在740/877/887/913/1000的计划与边距回归通过。新任务`PARAMETRIC-PORTAL-877-1967-575-20260915-R2`已在同一隔离store创建，当前attempt `PARAM-20260915055137210-5624DF`；WMI隐藏独立worker PID20172（当时记录），固定日志`data/parametric_attempts/portal-combined-durable.log`，已进入dimensions。沙箱现为workspace-write，WMI启动曾拒绝访问，经require_escalated自动审核允许后启动；后续CAD启动需同样按已有授权执行，不能假设普通沙箱可访问COM。

门框定向探测`PARAM-20260907151036264-434B65/evidence/probe-frame.json`：侧框切除-拉伸2/阵列(线性)2、中央框阵列(线性)1生成高3.6、深1.8的边缘槽，中心旧网格181..1706。新增`repair-frame-slots`按真实doorGapCenterYmm补同规格原生切槽，保留原V35通用网格。`data/parametric_839_continue.mjs`现复用6A867E，只执行clone、框槽修复和包验收。CreateCornerRectangle返回null，已改AddToDB下四条原生草图线；最新运行日志`839-frame-20260907231732.stdout.log`及`.stderr.log`，结果尚待核对。未放宽干涉门禁。

完整旧主线验证脚本会重写历史exe/manifest，因此未运行。相关JS/C#编译及上述真实CAD门禁已分别执行；未修改的历史检查不重复跑。最后的源保护检查366个源CAD均未变，R22 plan/assemblies receipt/audit三项SHA与交接一致。

2026-09-15 15:20 接续：R2全链实际通过package但verify-package发现底座焊接两处配合code51。重建时使用CLOSEST会翻转后加强筋，保留旧alignment又使同心配合求解失败；正确修复是读取原实际EntityParams轴向点积，重合取aligned、同心取anti-aligned，并要求所有组件变换不变。`PARAM-20260915064443750-89C4C1`修复和全部包验收通过，随后由真实门户worker调用CAD续跑的R3最终`PARAM-20260915070745350-8EF71C`自动ready。58CAD，877W1967H575D，4门4锁，33展开零错误，10处V35已知范围接触，ZIP SHA `FB4E36646A921AC44B6AEA3A729A5ABB49C285778300DF7C4D1A7145907DCD25`。

门户深度受理扩为550–600，变高1817–2017；每个任务全验收后下载。generator14/14、portal HTTP7/7通过；publication新增隔离evidence文件篡改拒绝测试并通过，未篡改实际候选CAD，完整CAD文件篡改负例尚未单独构造。Edge浏览器实际填入877/1967/575/4、提交后保存完整参数且未就绪；导入真实R3任务结果的隔离门户鉴权下载与真实ZIP SHA一致，pageErrors=[]，截图已人工视觉检查。证据`output/playwright/16029-1789456389463`。浏览器CLI安装不完整，使用桌面bundle自带Playwright，无新增依赖。

常驻worker新增--watch并接入现有门户watchdog；唯一daemon锁、串行领取、CAD忙时不领取、终止信号等行为已验证，失败/取消/外部lease测试仍通过。原watchdog前台阻塞server改为既有隐藏launcher，监听检测改用.NET TCP listeners；避免Get-NetTCPConnection在该后台上下文漏检反复拉起。现门户HTTP5180返回200，server PID10896，worker PID25208，watchdog PID8440（均仅当时记录，续接须核实）。启动脚本`tools/start_16029_parametric_portal_worker.mjs`，日志`data/review_portal_runtime/parametric-worker.stdout.log`与stderr。

最后无人工阶段覆盖的完整默认队列验收正在运行：主store任务`PARAMETRIC-AUTO-889-1967-575-20260915`，889W1967H575D四门，由已部署常驻worker自行领取；新attempt `PARAM-20260915071831594-70961E`，从V35源副本执行默认全链，未指定resumeSource或phases。以worker日志和当前attempt收据继续，不可并行启动第二个CAD。当前编译exe SHA `E300DC36C696B5E44490DEA8C843921B737558EBB343387E4196AF5E4E5DF383`。整体尚待该默认全链最终验收和最终代码/文档收口，不能提前称全部完成。
