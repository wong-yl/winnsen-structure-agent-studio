# DXF 钣金单件解析卡

> 输出级别：工程参考。不能作为正式生产展开图。

- 来源文件：`C:\Users\Administrator\Desktop\参数化模板素材\16029 寄存柜(标准组合式 1917×1000×550)\10.备份\2.钣金展开图20190423\储物柜门板1╱12展开图.dxf`
- 角色猜测：`door_panel`
- 文件名板厚线索：`None` mm
- 原始 bbox：X `473.4` mm × Y `181.9` mm
- 规则 bbox：X `473.4` mm × Y `181.9` mm
- 规则 bbox 来源：`raw_curve_bbox`
- 质量状态：`needs_layout_filter`
- 空间统计：`{"model": 71, "paper": 1}`
- 实体数量：`{"ARC": 2, "CIRCLE": 4, "LINE": 65, "VIEWPORT": 1}`
- 圆孔/圆实体数量：`4`
- 圆弧数量：`2`
- 闭合轮廓：`0`，开放端点：`0`

## 质量警告

- DXF contains VIEWPORT; bbox may describe drawing/layout space instead of true flat manufacturing extents.
- DXF has paper-space entities; filter model-space geometry before deriving manufacturing rules.
- Outline appears fragmented into LINE/ARC entities; closed-loop reconstruction is required before automatic unfold output.
- No closed curve loop was detected; geometry can be used for reference statistics but not for automatic unfold output yet.
- Sheet thickness was not found in the file name; confirm material/thickness before bend deduction.

## 生产释放前缺项

- material grade
- sheet thickness
- bend radius
- K-factor or bend deduction table
- datum and tolerance rules
- formal drawing title block/version

## 下一步

把 bbox、孔径、折弯线和角色猜测与 SolidWorks/BOM/工程图进行交叉验证，通过后再写入生成器规则。
