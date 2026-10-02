# topcover_reference_brep_v1

固定模式 `left-side-760-and-1000-sharp-flat-detailed-v7` 的 SolidWorks 2020 只读参考捕获器。它仅为结构工程辅助采集 760 V37 与 1000 金标准左侧板的 folded、`SplitBody → SheetMetal` 转换前锐角母体和 manufacturing flat B-rep；不会创建、复制、改名或修改参考零件。

输入必须已由调用方放入一个新的 attempt：

```text
<attempt>/capture_inputs/v37_left_side.sldprt
<attempt>/capture_inputs/gold_left_side.sldprt
<attempt>/evidence/private/
```

两个输入均要求单硬链接、无 reparse point、显式 SHA-256 匹配；输出仅允许为：

```text
<attempt>/evidence/private/topcover_left_reference_brep_capture_v7.json
```

左侧参考件保留 2 个历史外部引用；捕获器只记录并核对该数量，不把它误当成新原生件的无外链标准。新生成件仍必须为 0 外部引用。

构建和静态验证不启动 CAD：

```powershell
./Build-TopCoverRightReferenceBrep.ps1
```

运行包内含固定 SHA-256 的 SolidWorks 2020 interop。每个输入使用独立受控只读会话：先采集 folded snapshot；再对唯一 `SheetMetal` 特征调用 `IAccessSelections2` 进入只读回滚状态，采集转换前锐角母体，并 `ReleaseSelectionAccess` 恢复后复核 folded 签名；最后按原流程临时解除 `FlatPattern` 压缩，采集 manufacturing flat，再恢复并复核 folded 签名。整个过程不保存 CAD。证据采用 `FileMode.CreateNew`、`Flush(true)` 和回读校验。锐角母体、flat 或 folded 的采集结果都只是原生生成器的几何证据，不替代结构工程师评审。

这不是结构验收或整柜结论。必须由结构工程师在后续对比与评审中使用。
