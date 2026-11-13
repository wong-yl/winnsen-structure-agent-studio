# 16029 门板上铰链孔验证入口

日期：2026-06-17

## 已新增验证入口

`workers/solidworks_tools/Build16029DoorPanelSheetMetalSmoke.cs` 新增两个仅用于候选规则验证的参数：

- `--edge-unfold-hybrid-hole-cuts`
- `--upper-hinge-cx-mm <value>`

默认不传这些参数时，正式门板生成行为不变。

Hybrid 策略：

- 锁侧两个 `2.0 mm` 孔：沿用原始 `Unfold/Cut/Fold` 选面。
- 铰链两个 `10.2 mm` 孔：使用 FlatPattern 映射后的 Z 向选面。

## 候选 A 非正式验证

候选 A 是“保持上铰链 Y 不变，向右移动上铰链 X”。它不推荐作为默认结构方案，因为会破坏上下铰链孔共线。

已测试上铰链 X：

| upper hinge source X mm | 结果 |
|---:|---|
| `-129.4` | hybrid 只到 `3/4` cut，flat/folded 保持 |
| `-128.4` | hybrid 只到 `3/4` cut，flat/folded 保持 |
| `-127.4` | hybrid 只到 `3/4` cut，flat/folded 保持 |
| `-125.0` | hybrid 只到 `3/4` cut，flat/folded 保持 |
| `-122.0` | 几何退化到 `269 x 260 x 0.8`，不可用 |
| `-120.0` | hybrid 只到 `3/4` cut，flat/folded 保持 |

原始 `Unfold/Cut/Fold` 上孔单独验证：

| upper hinge source X mm | 结果 |
|---:|---|
| `-129.4` | 选到 `y_plus_to_minus` 侧向面，画圆成功，cut 失败，几何退化 |
| `-125.0` | 选到 `y_plus_to_minus` 侧向面，画圆成功，cut 失败，flat/folded 保持 |
| `-120.0` | 选到 `y_plus_to_minus` 侧向面，画圆成功，cut 失败，flat/folded 保持 |

## 当前判断

验证入口已经具备，但候选 A 没有关闭 4/4 cut。继续随机移动 X 或扫 `FeatureCut3` 参数价值很低。

下一步仍应回到结构规则：

- 优先复核门板手性、上下孔镜像、金标展开中左上角 relief 的来源。
- 或调整左上角 corner relief / 法兰展开，使上铰链孔真正落入可选中的展开材料面。
- 或由工艺签字定义上铰链孔为后加工。

## 后续验证命令模板

结构规则确认后，用同一个入口验证：

```powershell
& workers\solidworks_tools\bin\Build16029DoorPanelSheetMetalSmoke.exe `
  C:\tmp\door_hybrid_confirmed_rule `
  C:\tmp\door_hybrid_confirmed_rule\result.json `
  --edge-flange `
  --edge-unfold-hybrid-hole-cuts `
  --upper-hinge-cx-mm <confirmed_x> `
  --cut-variant 1 `
  --edge-base-width-mm 269 `
  --edge-base-height-mm 260 `
  --edge-offset-mm 18.2 `
  --edge-position-offset-mm 18.2 `
  --edge-position-type 6 `
  --edge-bend-allowance-mm 2.0
```

通过条件：

- `holeCutCount = 4`
- folded `307 x 298 x 18.2 mm`
- flat DXF `343.4 x 334.4 mm`
- DXF 有 4 个孔实体
- default/strict gate 均 PASS
