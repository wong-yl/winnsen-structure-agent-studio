"""Outdoor-specific dimensions; door editing follows the indoor portal contract."""
import math

INVESTIGATION_RANGES = {'height': (1700, 2200), 'width': (600, 1200), 'depth': (450, 700)}
GAP_MM = 6
PITCH_BUDGET_OFFSET_MM = 115
MIN_DOOR_HEIGHT_MM = 170
MIN_BOTTOM_DOOR_HEIGHT_MM = 170
MAX_TOTAL_DOORS = 22
MAX_COLUMN_DOORS = 11


def finite_mm(value):
    if isinstance(value, bool) or not isinstance(value, (int, float)) or not math.isfinite(value):
        raise ValueError('尺寸必须是有限数值')
    if abs(value * 1000 - round(value * 1000)) > 1e-6:
        raise ValueError('尺寸最多保留三位小数')
    return round(value, 3)


def validate_specification(spec, ranges=INVESTIGATION_RANGES):
    result = {key: finite_mm(spec[key]) for key in ('height', 'width', 'depth')}
    for key, value in result.items():
        if not ranges[key][0] <= value <= ranges[key][1]:
            raise ValueError(f'{key}超出本阶段范围')
    columns = spec.get('columns')
    if not isinstance(columns, list) or len(columns) != 2:
        raise ValueError('必须填写左右两列门板布局')
    resolved = []
    for index, column in enumerate(columns):
        side = ('L', 'R')[index]
        if column.get('side') != side:
            raise ValueError('两列须按L、R顺序提交')
        doors = column.get('doors')
        if not isinstance(doors, list) or not 1 <= len(doors) <= min(MAX_COLUMN_DOORS, (round(result['height'] * 1000) - PITCH_BUDGET_OFFSET_MM * 1000) // ((MIN_DOOR_HEIGHT_MM + GAP_MM) * 1000)):
            raise ValueError('每列门数须为整数，且不超过当前柜高可容纳的170 mm门数')
        heights = [finite_mm(door['heightMm']) for door in doors]
        if any(height < MIN_DOOR_HEIGHT_MM for height in heights):
            raise ValueError('每扇门高至少170 mm')
        used = sum(round(height * 1000) + GAP_MM * 1000 for height in heights)
        if used != round((result['height'] - PITCH_BUDGET_OFFSET_MM) * 1000):
            raise ValueError(f'{side}列门高与6 mm门缝未闭合')
        resolved.append({'side': side, 'doors': [{'heightMm': height} for height in heights]})
    count = sum(len(column['doors']) for column in resolved)
    if count > MAX_TOTAL_DOORS or ('doors' in spec and spec['doors'] != count):
        raise ValueError('总门数不符或超过22门')
    return {**result, 'doors': count, 'columns': resolved}


def door_rows(spec):
    rows = []
    for column in spec['columns']:
        offset = 0
        for index, door in enumerate(column['doors']):
            height = door['heightMm']
            rows.append({'side': column['side'], 'index': index + 1,
                         'height_mm': height, 'bottom_mm': 23 + offset,
                         'center_mm': 23 + offset + height / 2,
                         'boundary_mm': offset + height + GAP_MM,
                         'bottom_door': index == 0,
                         'top_door': index == len(column['doors']) - 1})
            offset += height + GAP_MM
    return rows
