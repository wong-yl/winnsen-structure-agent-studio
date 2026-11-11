# 本次源码上传检查

2026-11-11，Asia/Shanghai。执行目录为`codex/platform-recovery-20261110`上传工作树。本页的程序检查与本机真实CAD证据各有范围，不能相互替代。

| 项目 | 实际结果 |
|---|---|
| 上传字节 | 39份来源文件逐份大小和SHA256与实际运行项目一致，共3,520,646 bytes；Git暂存blob逐字节相同，新增候选目录沿用固定输入的`-text`规则，防止换行转换改变身份；清单见相邻sources JSON |
| 构建身份检查 | `node tools/verify_16029_parametric_build.test.mjs`退出码0；固定十源、默认保护、验收绑定、身份漂移拒绝通过，没有编译CAD程序 |
| 本次针对检查 | `node --test --test-name-pattern 'profile migrations\|a failed right-owner\|resume skips\|ordinary and explicitly\|distinct door' tools/verify_16029_parametric_runtime.test.mjs tools/verify_16029_parametric_resume.test.mjs`：8项通过、0失败 |
| 来源完整测试 | 实际项目在相同运行库、测试源码上此前完整运行661项、661通过、0失败。后续仅调整runner的检查点优先级，三项续跑测试和语法复查通过；本次上传工作树的8项检查覆盖该续跑行为与新增修复阶段 |
| 语法 | 选入的8份`.mjs`均`node --check`通过；新增JSON解析通过，`git diff --check`通过 |
| 上传范围 | 仅源码、运行版本选择、固定输入换行保护规则及三份文档；无CAD/EXE/DLL/压缩包/原始账户、任务或授权记录 |
| 凭据模式 | 39份来源文件的高置信GitHub令牌、私钥正文及API密钥模式扫描0命中 |
| 远端前置 | 已确认远端同名分支为本地父提交`55427f36fad825e7ba8543deb300c6a0d3f70680`，按正常提交和推送更新，不改写远端历史 |

真实CAD、HTTP下载和正式平台切换证据概要见[当前结果](indoor_recovery_verified_20261111.md)。完整模型和凭证保留在本机，不为使GitHub检查看起来通过而补造数据。未重新生成CAD或重复已通过的完整测试；未触碰无关室外柜和图纸代码。

此上传不等于合并`main`或生产放行；本次没有声称远端CI已经通过。新程序从零单次生成回归及其他尺寸，由后续实际提交分别验证。
