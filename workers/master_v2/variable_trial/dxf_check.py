"""Read-only ASCII DXF geometry audit for the isolated variable trial.

Uses only Python's standard library. No CAD, file repair, layer guessing, or
manufacturing release is performed. JSON reports are new files in trial outputs.
"""
import argparse
import collections
import hashlib
import json
import math
import os
from pathlib import Path
import sys


TRIAL_ROOT = Path(r"D:\Winnsen_Structure_Agent_Studio\data\master_v2\variable_trial_20261112")
TAU = 2 * math.pi
UNIT_MM = {1: ("inch", 25.4), 2: ("foot", 304.8), 4: ("millimetre", 1.0),
           5: ("centimetre", 10.0), 6: ("metre", 1000.0), 7: ("kilometre", 1e6),
           8: ("microinch", 0.0000254), 9: ("mil", 0.0254), 10: ("yard", 914.4),
           13: ("micrometre", 0.001), 14: ("decimetre", 100.0)}


class AuditError(ValueError):
    pass


def require(condition, message):
    if not condition:
        raise AuditError(message)


def pairs_from_bytes(data):
    require(not data.startswith(b"AutoCAD Binary DXF"), "BINARY_DXF_UNSUPPORTED")
    text = data.decode("utf-8-sig", errors="replace")
    lines = text.splitlines()
    while lines and not lines[-1].strip():
        lines.pop()
    require(len(lines) % 2 == 0, "MALFORMED_GROUP_PAIR_COUNT")
    pairs = []
    for i in range(0, len(lines), 2):
        try:
            code = int(lines[i].strip())
        except ValueError as error:
            raise AuditError("INVALID_GROUP_CODE_AT_LINE_%d" % (i + 1)) from error
        pairs.append((code, lines[i + 1].strip()))
    require(pairs and pairs[-1] == (0, "EOF"), "DXF_EOF_REQUIRED")
    return pairs


def first(tags, code, default=None):
    return next((value for key, value in tags if key == code), default)


def num(tags, code, default=0.0):
    try:
        value = float(first(tags, code, default))
    except (ValueError, TypeError) as error:
        raise AuditError("INVALID_NUMERIC_GROUP_%s" % code) from error
    require(math.isfinite(value), "NONFINITE_NUMERIC_GROUP_%s" % code)
    return value


def section_records(pairs):
    sections = collections.defaultdict(list)
    current = None
    i = 0
    while i < len(pairs):
        code, value = pairs[i]
        if (code, value) == (0, "SECTION"):
            require(current is None and i + 1 < len(pairs) and pairs[i + 1][0] == 2,
                    "MALFORMED_SECTION_HEADER")
            current = pairs[i + 1][1]
            i += 2
            continue
        if (code, value) == (0, "ENDSEC"):
            require(current is not None, "UNMATCHED_ENDSEC")
            current = None
        elif current is not None:
            sections[current].append((code, value))
        i += 1
    require(current is None, "UNCLOSED_SECTION")
    return sections


def records(tags):
    result = []
    for code, value in tags:
        if code == 0:
            result.append([value, []])
        else:
            require(bool(result), "ENTITY_TAG_WITHOUT_ENTITY")
            result[-1][1].append((code, value))
    return result


def units_of(header):
    for i, pair in enumerate(header):
        if pair == (9, "$INSUNITS"):
            require(i + 1 < len(header) and header[i + 1][0] == 70,
                    "MALFORMED_INSUNITS")
            try:
                return int(header[i + 1][1])
            except ValueError as error:
                raise AuditError("INVALID_INSUNITS") from error
    return None


def point(tags, xcode=10, factor=1.0):
    require(first(tags, xcode) is not None and first(tags, xcode + 10) is not None,
            "MISSING_PLANAR_COORDINATES")
    require(abs(num(tags, xcode + 20)) < 1e-9, "NONPLANAR_ENTITY_Z_UNSUPPORTED")
    return (num(tags, xcode) * factor, num(tags, xcode + 10) * factor)


def planar(tags):
    require(abs(num(tags, 210)) < 1e-9 and abs(num(tags, 220)) < 1e-9
            and abs(num(tags, 230, 1) - 1) < 1e-9, "NONSTANDARD_EXTRUSION_UNSUPPORTED")
    require(abs(num(tags, 39)) < 1e-9, "ENTITY_THICKNESS_UNSUPPORTED")


def distance(a, b):
    return math.hypot(a[0] - b[0], a[1] - b[1])


def line(a, b, layer, entity_id):
    return {"kind": "LINE", "a": a, "b": b, "layer": layer, "entity": entity_id}


def arc(center, radius, start, sweep, layer, entity_id):
    require(radius > 0 and math.isfinite(radius), "NONPOSITIVE_CIRCLE_RADIUS")
    return {"kind": "ARC", "c": center, "r": radius, "start": start % TAU,
            "sweep": sweep, "layer": layer, "entity": entity_id}


def ends(primitive):
    if primitive["kind"] == "LINE":
        return primitive["a"], primitive["b"]
    c, r = primitive["c"], primitive["r"]
    angles = [primitive["start"], primitive["start"] + primitive["sweep"]]
    return tuple((c[0] + r * math.cos(t), c[1] + r * math.sin(t)) for t in angles)


def poly_edge(a, b, bulge, layer, entity_id):
    if abs(bulge) < 1e-12:
        return line(a, b, layer, entity_id)
    chord = distance(a, b)
    require(chord > 1e-12, "BULGE_WITH_COINCIDENT_VERTICES")
    dx, dy = b[0] - a[0], b[1] - a[1]
    offset = chord * (1 - bulge * bulge) / (4 * bulge)
    center = ((a[0] + b[0]) / 2 - dy / chord * offset,
              (a[1] + b[1]) / 2 + dx / chord * offset)
    radius = chord * (1 + bulge * bulge) / (4 * abs(bulge))
    return arc(center, radius, math.atan2(a[1] - center[1], a[0] - center[0]),
               4 * math.atan(bulge), layer, entity_id)


def geometry(entity_records, factor):
    primitives, circles, counts, used = [], [], collections.Counter(), collections.Counter()
    unsupported = []
    i = 0
    while i < len(entity_records):
        kind, tags = entity_records[i]
        entity_id = i + 1
        counts[kind] += 1
        layer = first(tags, 8, "0")
        used[layer] += 1
        if kind not in {"LINE", "ARC", "CIRCLE", "LWPOLYLINE", "POLYLINE"}:
            unsupported.append({"entity": entity_id, "type": kind, "layer": layer})
            i += 1
            continue
        planar(tags)
        if kind == "LINE":
            primitives.append(line(point(tags, 10, factor), point(tags, 11, factor), layer, entity_id))
        elif kind in {"ARC", "CIRCLE"}:
            center, radius = point(tags, 10, factor), num(tags, 40) * factor
            require(radius > 0, "NONPOSITIVE_CIRCLE_RADIUS")
            if kind == "CIRCLE":
                circles.append({"c": center, "r": radius, "layer": layer, "entity": entity_id})
            else:
                require(first(tags, 50) is not None and first(tags, 51) is not None,
                        "ARC_ANGLES_REQUIRED")
                start, stop = math.radians(num(tags, 50)), math.radians(num(tags, 51))
                primitives.append(arc(center, radius, start, (stop - start) % TAU, layer, entity_id))
        else:
            flags = int(num(tags, 70))
            require((flags & ~(1 | 128)) == 0, "POLYLINE_3D_MESH_FIT_OR_OTHER_FLAGS_UNSUPPORTED")
            vertices = []
            if kind == "LWPOLYLINE":
                require(abs(num(tags, 38)) < 1e-9, "NONPLANAR_POLYLINE_ELEVATION")
                require(abs(num(tags, 43)) < 1e-12, "POLYLINE_WIDTH_UNSUPPORTED")
                vertex = None
                for code, value in tags:
                    if code == 10:
                        if vertex is not None:
                            vertices.append(vertex)
                        vertex = {10: value}
                    elif code in {20, 40, 41, 42}:
                        require(vertex is not None, "POLYLINE_VERTEX_TAG_ORDER")
                        vertex[code] = value
                if vertex is not None:
                    vertices.append(vertex)
                require(int(num(tags, 90)) == len(vertices), "POLYLINE_VERTEX_COUNT_MISMATCH")
                vertices = [list(v.items()) for v in vertices]
            else:
                require(abs(num(tags, 40)) < 1e-12 and abs(num(tags, 41)) < 1e-12,
                        "POLYLINE_WIDTH_UNSUPPORTED")
                require(abs(num(tags, 30)) < 1e-9, "NONPLANAR_POLYLINE_ELEVATION")
                i += 1
                while i < len(entity_records) and entity_records[i][0] == "VERTEX":
                    vertex_tags = entity_records[i][1]
                    planar(vertex_tags)
                    require(int(num(vertex_tags, 70)) == 0, "POLYLINE_VERTEX_FLAGS_UNSUPPORTED")
                    vertices.append(vertex_tags)
                    i += 1
                require(i < len(entity_records) and entity_records[i][0] == "SEQEND",
                        "POLYLINE_SEQEND_REQUIRED")
            require(len(vertices) >= 2, "POLYLINE_TWO_VERTICES_REQUIRED")
            for vertex in vertices:
                require(abs(num(vertex, 40)) < 1e-12 and abs(num(vertex, 41)) < 1e-12,
                        "POLYLINE_VERTEX_WIDTH_UNSUPPORTED")
            pairs = len(vertices) if flags & 1 else len(vertices) - 1
            for k in range(pairs):
                primitives.append(poly_edge(point(vertices[k], 10, factor),
                                            point(vertices[(k + 1) % len(vertices)], 10, factor),
                                            num(vertices[k], 42), layer, entity_id))
        i += 1
    return primitives, circles, counts, used, unsupported


def angle_on(primitive, angle):
    if primitive["sweep"] >= 0:
        return (angle - primitive["start"]) % TAU <= primitive["sweep"] + 1e-12
    return (primitive["start"] - angle) % TAU <= -primitive["sweep"] + 1e-12


def bounds(primitives, circles):
    points = []
    for primitive in primitives:
        points.extend(ends(primitive))
        if primitive["kind"] == "ARC":
            for angle in [0, math.pi / 2, math.pi, 3 * math.pi / 2]:
                if angle_on(primitive, angle):
                    points.append((primitive["c"][0] + primitive["r"] * math.cos(angle),
                                   primitive["c"][1] + primitive["r"] * math.sin(angle)))
    for circle in circles:
        points.extend([(circle["c"][0] - circle["r"], circle["c"][1] - circle["r"]),
                       (circle["c"][0] + circle["r"], circle["c"][1] + circle["r"])])
    if not points:
        return None
    low = [min(p[axis] for p in points) for axis in [0, 1]]
    high = [max(p[axis] for p in points) for axis in [0, 1]]
    return {"min": low, "max": high, "width": high[0] - low[0], "height": high[1] - low[1]}


def sample(primitive, reverse=False):
    if primitive["kind"] == "LINE":
        result = list(ends(primitive))
    else:
        # Polygon sampling affects nesting/area only; extents and circle dimensions are analytic.
        count = max(2, int(math.ceil(abs(primitive["sweep"]) / (math.pi / 180))))
        result = [(primitive["c"][0] + primitive["r"] * math.cos(primitive["start"] + primitive["sweep"] * k / count),
                   primitive["c"][1] + primitive["r"] * math.sin(primitive["start"] + primitive["sweep"] * k / count))
                  for k in range(count + 1)]
    return list(reversed(result)) if reverse else result


def area(polygon):
    return abs(sum(a[0] * b[1] - b[0] * a[1] for a, b in zip(polygon, polygon[1:] + polygon[:1]))) / 2


def inside(point_value, polygon):
    x, y = point_value
    result = False
    for a, b in zip(polygon, polygon[1:] + polygon[:1]):
        if (a[1] > y) != (b[1] > y) and x < (b[0] - a[0]) * (y - a[1]) / (b[1] - a[1]) + a[0]:
            result = not result
    return result


def topology(primitives, circles, tolerance):
    nodes, cells, edges = [], collections.defaultdict(list), []

    def node(point_value):
        cell = (math.floor(point_value[0] / tolerance), math.floor(point_value[1] / tolerance))
        matches = []
        for dx in [-1, 0, 1]:
            for dy in [-1, 0, 1]:
                matches.extend(n for n in cells[(cell[0] + dx, cell[1] + dy)]
                               if distance(point_value, nodes[n]) <= tolerance)
        require(len(matches) <= 1, "AMBIGUOUS_ENDPOINT_CLUSTER_WITHIN_TOLERANCE")
        if matches:
            return matches[0]
        index = len(nodes)
        nodes.append(point_value)
        cells[cell].append(index)
        return index

    adjacency = collections.defaultdict(list)
    for primitive in primitives:
        a, b = ends(primitive)
        na, nb = node(a), node(b)
        index = len(edges)
        edges.append((na, nb, primitive))
        adjacency[na].append(index)
        adjacency[nb].append(index)
    open_nodes = [{"node": n, "point": nodes[n], "degree": len(links)}
                  for n, links in adjacency.items() if len(links) == 1]
    branches = [{"node": n, "point": nodes[n], "degree": len(links)}
                for n, links in adjacency.items() if len(links) > 2]
    components, visited, contours = [], set(), []
    for start in range(len(edges)):
        if start in visited:
            continue
        pending, component = [start], set()
        while pending:
            index = pending.pop()
            if index in component:
                continue
            component.add(index)
            a, b, unused = edges[index]
            pending.extend(adjacency[a] + adjacency[b])
        visited.update(component)
        component_nodes = {n for index in component for n in edges[index][:2]}
        closed = all(len(adjacency[n]) == 2 for n in component_nodes)
        row = {"segmentCount": len(component), "nodeCount": len(component_nodes), "closed": closed,
               "entities": sorted({edges[index][2]["entity"] for index in component})}
        if closed:
            start_node = edges[start][0]
            current_node, current_edge, used_edges, polygon = start_node, start, set(), []
            while current_edge not in used_edges:
                used_edges.add(current_edge)
                a, b, primitive = edges[current_edge]
                reverse = a != current_node
                polygon.extend(sample(primitive, reverse)[:-1])
                current_node = a if reverse else b
                next_edges = [index for index in adjacency[current_node] if index not in used_edges]
                if not next_edges:
                    break
                current_edge = next_edges[0]
            require(current_node == start_node and used_edges == component, "CLOSED_CHAIN_TRAVERSAL_FAILED")
            row["area"] = area(polygon)
            arc_parts = [edges[index][2] for index in component]
            if arc_parts and all(p["kind"] == "ARC" for p in arc_parts):
                center, radius = arc_parts[0]["c"], arc_parts[0]["r"]
                if (all(distance(p["c"], center) <= tolerance and abs(p["r"] - radius) <= tolerance
                        for p in arc_parts)
                        and abs(sum(abs(p["sweep"]) for p in arc_parts) - TAU) * radius <= tolerance):
                    row["roundCircle"] = {"center": center, "radius": radius,
                                          "source": "closed native circular-arc chain"}
            contours.append({"polygon": polygon, "row": row})
        components.append(row)
    for circle in circles:
        polygon = [(circle["c"][0] + circle["r"] * math.cos(TAU * k / 360),
                    circle["c"][1] + circle["r"] * math.sin(TAU * k / 360)) for k in range(360)]
        row = {"segmentCount": 1, "nodeCount": 0, "closed": True,
               "entities": [circle["entity"]], "circle": True, "area": math.pi * circle["r"] ** 2,
               "roundCircle": {"center": circle["c"], "radius": circle["r"], "source": "native CIRCLE"}}
        contours.append({"polygon": polygon, "row": row})
    for contour in contours:
        probe = contour["polygon"][0]
        depth = sum(inside(probe, other["polygon"]) for other in contours
                    if other is not contour and other["row"]["area"] > contour["row"]["area"])
        contour["row"]["nestingDepth"] = depth
        contour["row"]["hole"] = bool(depth % 2)
    return {"closedContourCount": len(contours),
            "outerContourCount": sum(not c["row"]["hole"] for c in contours),
            "holeContourCount": sum(c["row"]["hole"] for c in contours),
            "closedContours": [c["row"] for c in contours], "components": components,
            "openChainCount": sum(not row["closed"] for row in components),
            "openNodes": open_nodes, "branchNodes": branches,
            "nestingMethod": "even-odd containment of analytic circles and arcs sampled at <=1 degree; area/nesting are approximate"}


def duplicates(primitives, circles, tolerance):
    # Geometric comparisons also catch reversed LINEs and clockwise bulge arcs.
    rows = []
    all_items = primitives + [dict(circle, kind="CIRCLE") for circle in circles]
    for i, a in enumerate(all_items):
        for b in all_items[:i]:
            if a["kind"] != b["kind"]:
                continue
            equal = False
            if a["kind"] == "LINE":
                equal = ((distance(a["a"], b["a"]) <= tolerance and distance(a["b"], b["b"]) <= tolerance)
                         or (distance(a["a"], b["b"]) <= tolerance and distance(a["b"], b["a"]) <= tolerance))
            elif distance(a["c"], b["c"]) <= tolerance and abs(a["r"] - b["r"]) <= tolerance:
                if a["kind"] == "CIRCLE":
                    equal = True
                else:
                    ae, be = ends(a), ends(b)
                    amid = sample(a)[len(sample(a)) // 2]
                    bmid = sample(b)[len(sample(b)) // 2]
                    equal = (abs(abs(a["sweep"]) - abs(b["sweep"])) * max(a["r"], b["r"]) <= tolerance
                             and min(distance(ae[0], be[0]) + distance(ae[1], be[1]),
                                     distance(ae[0], be[1]) + distance(ae[1], be[0])) <= 2 * tolerance
                             and distance(amid, bmid) <= tolerance)
            if equal:
                rows.append({"type": a["kind"], "entities": [b["entity"], a["entity"]],
                             "layers": [b["layer"], a["layer"]]})
    return rows


def cut_intersections(primitives, circles, tolerance):
    """Analytic planar intersection audit; ordinary shared chain ends are allowed."""
    shapes = primitives + [dict(c, kind="CIRCLE") for c in circles]
    rows = []

    def cross(a, b):
        return a[0] * b[1] - a[1] * b[0]

    def sub(a, b):
        return a[0] - b[0], a[1] - b[1]

    def on_shape(p, shape):
        if shape["kind"] == "LINE":
            return abs(distance(p, shape["a"]) + distance(p, shape["b"]) - distance(shape["a"], shape["b"])) <= tolerance
        if shape["kind"] == "CIRCLE":
            return True
        angle = math.atan2(p[1] - shape["c"][1], p[0] - shape["c"][0])
        return angle_on(shape, angle)

    def at_endpoint(p, shape):
        return shape["kind"] != "CIRCLE" and any(distance(p, end) <= tolerance for end in ends(shape))

    def angular_intervals(shape):
        if shape["kind"] == "CIRCLE":
            return [(0, TAU)]
        length = abs(shape["sweep"])
        start = (shape["start"] if shape["sweep"] >= 0 else shape["start"] + shape["sweep"]) % TAU
        return [(start, min(start + length, TAU))] + ([(0, start + length - TAU)] if start + length > TAU else [])

    for i, a in enumerate(shapes):
        for b in shapes[:i]:
            hits, overlap = [], False
            if a["kind"] == "LINE" and b["kind"] == "LINE":
                r, s, offset = sub(a["b"], a["a"]), sub(b["b"], b["a"]), sub(b["a"], a["a"])
                denominator = cross(r, s)
                if abs(denominator) <= 1e-12:
                    length = distance(a["a"], a["b"])
                    if length > 1e-12 and abs(cross(offset, r)) / length <= tolerance:
                        projected = [sum((p[k] - a["a"][k]) * r[k] for k in [0, 1]) / length
                                     for p in [b["a"], b["b"]]]
                        overlap = min(length, max(projected)) - max(0, min(projected)) > tolerance
                else:
                    t, u = cross(offset, s) / denominator, cross(offset, r) / denominator
                    if -1e-10 <= t <= 1 + 1e-10 and -1e-10 <= u <= 1 + 1e-10:
                        hits.append((a["a"][0] + t * r[0], a["a"][1] + t * r[1]))
            elif a["kind"] == "LINE" or b["kind"] == "LINE":
                linear, circular = (a, b) if a["kind"] == "LINE" else (b, a)
                d, offset = sub(linear["b"], linear["a"]), sub(linear["a"], circular["c"])
                aa = sum(v * v for v in d)
                bb = 2 * sum(offset[k] * d[k] for k in [0, 1])
                cc = sum(v * v for v in offset) - circular["r"] ** 2
                discriminant = bb * bb - 4 * aa * cc
                if aa > 1e-20 and discriminant >= -1e-12:
                    for t in {(-bb - math.sqrt(max(0, discriminant))) / (2 * aa),
                              (-bb + math.sqrt(max(0, discriminant))) / (2 * aa)}:
                        if -1e-10 <= t <= 1 + 1e-10:
                            p = (linear["a"][0] + t * d[0], linear["a"][1] + t * d[1])
                            if on_shape(p, circular):
                                hits.append(p)
            else:
                separation = distance(a["c"], b["c"])
                if separation <= tolerance and abs(a["r"] - b["r"]) <= tolerance:
                    overlap = any(min(x[1], y[1]) - max(x[0], y[0]) > tolerance / a["r"]
                                  for x in angular_intervals(a) for y in angular_intervals(b))
                elif separation > 1e-12 and separation <= a["r"] + b["r"] + 1e-10 and separation >= abs(a["r"] - b["r"]) - 1e-10:
                    along = (a["r"] ** 2 - b["r"] ** 2 + separation ** 2) / (2 * separation)
                    altitude_sq = a["r"] ** 2 - along ** 2
                    if altitude_sq >= -1e-10:
                        direction = ((b["c"][0] - a["c"][0]) / separation, (b["c"][1] - a["c"][1]) / separation)
                        base = (a["c"][0] + along * direction[0], a["c"][1] + along * direction[1])
                        for sign in [-1, 1]:
                            p = (base[0] - sign * math.sqrt(max(0, altitude_sq)) * direction[1],
                                 base[1] + sign * math.sqrt(max(0, altitude_sq)) * direction[0])
                            if on_shape(p, a) and on_shape(p, b):
                                hits.append(p)
            invalid_hits = [p for p in hits if not (at_endpoint(p, a) and at_endpoint(p, b))]
            if overlap or invalid_hits:
                rows.append({"entities": [b["entity"], a["entity"]], "overlap": overlap, "points": invalid_hits})
    return rows


def audit(data, expected_width=None, expected_height=None, expected_bends=None,
          tolerance_mm=0.01, dimension_tolerance_mm=0.05):
    require(tolerance_mm > 0 and dimension_tolerance_mm > 0, "POSITIVE_TOLERANCE_REQUIRED")
    sections = section_records(pairs_from_bytes(data))
    require("ENTITIES" in sections, "ENTITIES_SECTION_REQUIRED")
    insunits = units_of(sections.get("HEADER", []))
    known_units = insunits in UNIT_MM
    unit_name, factor = UNIT_MM.get(insunits, ("unverified drawing unit", 1.0))
    primitives, circles, counts, used, unsupported = geometry(records(sections["ENTITIES"]), factor)
    declared_layers = []
    if sections.get("TABLES"):
        declared_layers = [first(tags, 2) for kind, tags in records(sections["TABLES"]) if kind == "LAYER"]
    errors, warnings = [], []
    if not known_units:
        errors.append("UNVERIFIED_UNITS: missing, unitless, or unsupported $INSUNITS")
    for layer in used:
        if layer not in {"CUT", "BEND", "MARK"}:
            errors.append("UNMAPPED_OR_UNVERIFIED_DIRECTION_LAYER: " + layer)
    if unsupported:
        errors.append("UNSUPPORTED_ENTITIES_PRESENT")
    cut = [p for p in primitives if p["layer"] == "CUT"]
    cut_circles = [c for c in circles if c["layer"] == "CUT"]
    bend = [p for p in primitives if p["layer"] == "BEND"]
    if any(p["kind"] != "LINE" for p in bend) or any(c["layer"] == "BEND" for c in circles):
        errors.append("BEND_REQUIRES_LINEAR_GEOMETRY")
    zero = [{"entity": p["entity"], "layer": p["layer"], "type": p["kind"]}
            for p in primitives if (distance(*ends(p)) if p["kind"] == "LINE"
                                   else abs(p["sweep"]) * p["r"]) <= 1e-8]
    duplicate_rows = duplicates(primitives, circles, tolerance_mm)
    intersection_rows = cut_intersections(cut, cut_circles, tolerance_mm)
    if zero:
        errors.append("ZERO_LENGTH_SEGMENTS_PRESENT")
    if duplicate_rows:
        errors.append("DUPLICATE_GEOMETRY_PRESENT")
    if intersection_rows:
        errors.append("CUT_INTERSECTIONS_OR_OVERLAPS_PRESENT")
    topology_report = topology(cut, cut_circles, tolerance_mm)
    if not cut and not cut_circles:
        errors.append("CUT_GEOMETRY_REQUIRED")
    if topology_report["openChainCount"] or topology_report["branchNodes"]:
        errors.append("CUT_OPEN_CHAINS_OR_BRANCHES")
    if topology_report["closedContourCount"] == 0:
        errors.append("CLOSED_CUT_CONTOUR_REQUIRED")
    cut_bounds = bounds(cut, cut_circles)
    measurements = {"units": "mm" if known_units else "unverified drawing units",
                    "cutExtents": cut_bounds, "allExtents": bounds(primitives, circles)}
    expected_checks = {}
    for name, expected, actual in [
            ("width", expected_width, None if cut_bounds is None else cut_bounds["width"]),
            ("height", expected_height, None if cut_bounds is None else cut_bounds["height"])]:
        if expected is not None:
            passed = known_units and actual is not None and abs(actual - expected) <= dimension_tolerance_mm
            expected_checks[name] = {"expectedMm": expected, "actualMm": actual if known_units else None,
                                     "toleranceMm": dimension_tolerance_mm, "pass": passed}
            if not passed:
                errors.append("EXPECTED_%s_MISMATCH_OR_UNVERIFIED" % name.upper())
    if expected_bends is not None:
        passed = len(bend) == expected_bends and all(p["kind"] == "LINE" for p in bend)
        expected_checks["bends"] = {"expected": expected_bends, "actual": len(bend), "pass": passed}
        if not passed:
            errors.append("EXPECTED_BEND_COUNT_MISMATCH")
    round_holes = []
    hole_contours = [c for c in topology_report["closedContours"] if c["hole"] and c.get("roundCircle")]
    for i, contour in enumerate(sorted(hole_contours,
                                      key=lambda c: (c["roundCircle"]["center"][1], c["roundCircle"]["center"][0]))):
        circle = contour["roundCircle"]
        origin = cut_bounds["min"] if cut_bounds else [0, 0]
        round_holes.append({"id": "H%d" % (i + 1), "entities": contour["entities"],
                            "center": circle["center"], "xFromCutMin": circle["center"][0] - origin[0],
                            "yFromCutMin": circle["center"][1] - origin[1], "diameter": circle["radius"] * 2,
                            "source": circle["source"], "units": measurements["units"]})
    warnings.extend(["Bend direction is UNKNOWN; canonical BEND has no up/down claim.",
                     "Layer provenance requires TrialOutput native API/option differential evidence; this audit checks the supplied geometry.",
                     "Endpoint tolerance and approximate contour nesting require dimensional review; manufacturing clearances and process allowances are not certified."])
    return {"success": not errors, "auditPassed": not errors, "manufacturingAccepted": False,
            "sha256": hashlib.sha256(data).hexdigest().upper(),
            "units": {"insunits": insunits, "name": unit_name, "verified": known_units,
                      "factorToMm": factor if known_units else None},
            "entityTypeCounts": dict(counts), "layers": {"declared": declared_layers, "used": dict(used)},
            "cuts": {"entityCount": sum(used[layer] for layer in used if layer == "CUT"),
                     "lineSegmentCount": sum(p["kind"] == "LINE" for p in cut),
                     "arcSegmentCount": sum(p["kind"] == "ARC" for p in cut), "circleCount": len(cut_circles),
                     "primitives": [dict(p) for p in cut] + [dict(c, kind="CIRCLE") for c in cut_circles]},
            "bends": {"segmentCount": len(bend), "direction": "UNKNOWN", "lines": [
                {"entity": p["entity"], "start": ends(p)[0], "end": ends(p)[1]} for p in bend]},
            "marks": {"entityCount": used.get("MARK", 0)}, "measurements": measurements,
            "roundHoleCount": len(round_holes), "roundHoles": round_holes,
            "closedContours": topology_report, "duplicateSegments": duplicate_rows,
            "cutIntersections": intersection_rows,
            "zeroLengthSegments": zero, "unsupportedEntities": unsupported, "expectedChecks": expected_checks,
            "endpointTolerance": tolerance_mm, "errors": errors, "warnings": warnings}


def new_report_path(path):
    full = Path(os.path.abspath(path))
    root = Path(os.path.abspath(TRIAL_ROOT))
    require(os.path.commonpath([full, root]) == str(root) and full != root,
            "REPORT_OUTSIDE_TRIAL_ROOT")
    require(any(parent.name.lower() == "outputs" for parent in full.parents
                if parent == root or root in parent.parents), "REPORT_MUST_BE_IN_TRIAL_OUTPUTS")
    require(not full.exists(), "PRESERVE_EXISTING_REPORT")
    for ancestor in [full] + list(full.parents):
        if ancestor.exists():
            attributes = getattr(ancestor.stat(follow_symlinks=False), "st_file_attributes", 0)
            require(not ancestor.is_symlink() and not attributes & 0x400, "REPORT_REPARSE_PATH")
    return full


def rigid_point(point_value, matrix, translation):
    return [sum(matrix[i][k] * point_value[k] for k in range(2)) + translation[i] for i in range(2)]


def cut_coordinate_mapping(report, evidence):
    """Try eight unit-scale axis mappings; require every native CUT primitive exactly once."""
    result = {"verified": False, "errors": [], "candidateCount": 0, "candidates": [],
              "candidateDiagnostics": [], "scale": 1, "dxfGeometryChanged": False,
              "basis": "Complete native top-Z CUT LINE/ARC/CIRCLE multiset; BEND is excluded",
              "manufacturingOrientationVerified": False}
    tolerance = 0.01
    try:
        native = evidence["nativeCutGeometry"]
        require(native["schema"] == "native-top-z-cut-v1" and native["complete"] is True
                and native["errors"] == [] and native["units"] == "mm", "NATIVE_CUT_SNAPSHOT_INCOMPLETE")
        require(report["auditPassed"] and report["units"]["verified"] and report["units"]["insunits"] == 4,
                "CUT_MAPPING_REQUIRES_AUDITED_MM_DXF")
        require(evidence.get("sourceIdentityVerified") is True
                and os.path.normcase(os.path.abspath(native["modelPath"])) == os.path.normcase(os.path.abspath(evidence["modelPath"]))
                and native["configuration"] == evidence["exportConfiguration"]
                and len(evidence["modelSha256"]) == 64
                and evidence["modelSha256"] == evidence["exportModelSha256"], "NATIVE_CUT_SOURCE_IDENTITY_MISMATCH")
        require(evidence.get("modelHand") in {"L", "R"}
                and evidence["modelHand"] == evidence.get("drawingHand") == evidence.get("exportModelHand") == native.get("modelHand"),
                "NATIVE_CUT_HAND_IDENTITY_MISMATCH")
        top, top_tolerance = native["topZMm"], native["topToleranceMm"]
        require(math.isfinite(top) and 0 < top_tolerance <= 0.0001 + 1e-12, "NATIVE_CUT_TOP_Z_FILTER_REQUIRED")

        def xy(value):
            require(len(value) == 3 and all(math.isfinite(v) for v in value)
                    and abs(value[2] - top) <= top_tolerance, "NATIVE_CUT_POINT_OFF_TOP_Z")
            return list(value[:2])

        source, target = [], []
        for item in native["primitives"]:
            kind, length = item["type"], item["lengthMm"]
            require(kind in {"LINE", "ARC", "CIRCLE"} and math.isfinite(length) and length > 0,
                    "NATIVE_CUT_PRIMITIVE_UNSUPPORTED")
            p = {"kind": kind, "length": length}
            if kind in {"LINE", "ARC"}:
                p["a"], p["b"] = xy(item["startMm"]), xy(item["endMm"])
            if kind == "LINE":
                require(abs(distance(p["a"], p["b"]) - length) <= tolerance, "NATIVE_CUT_LINE_LENGTH_MISMATCH")
            else:
                p["c"], p["r"] = xy(item["centerMm"]), item["radiusMm"]
                require(math.isfinite(p["r"]) and p["r"] > 0, "NATIVE_CUT_RADIUS_REQUIRED")
                if kind == "CIRCLE":
                    require(abs(length - TAU * p["r"]) <= tolerance, "NATIVE_CUT_CIRCLE_LENGTH_MISMATCH")
                else:
                    p["mid"] = xy(item["midpointMm"])
                    require(0 < length < TAU * p["r"]
                            and all(abs(distance(v, p["c"]) - p["r"]) <= tolerance for v in [p["a"], p["b"], p["mid"]]),
                            "NATIVE_CUT_ARC_GEOMETRY_INVALID")
                    angle = math.atan2(p["a"][1] - p["c"][1], p["a"][0] - p["c"][0])
                    sweep = length / p["r"]
                    valid_senses = []
                    for sign in [-1, 1]:
                        expected = [(p["c"][0] + p["r"] * math.cos(angle + sign * sweep * fraction),
                                     p["c"][1] + p["r"] * math.sin(angle + sign * sweep * fraction)) for fraction in [0.5, 1]]
                        valid_senses.append(distance(expected[0], p["mid"]) <= tolerance and distance(expected[1], p["b"]) <= tolerance)
                    require(sum(valid_senses) == 1, "NATIVE_CUT_ARC_TRIM_UNVERIFIED")
            source.append(p)
        require(bool(source) and len(source) == native["boundaryEdgeCount"], "NATIVE_CUT_BOUNDARY_COUNT_MISMATCH")
        for item in report["cuts"]["primitives"]:
            kind = item["kind"]
            require(kind in {"LINE", "ARC", "CIRCLE"}, "DXF_CUT_PRIMITIVE_UNSUPPORTED")
            p = {"kind": kind}
            if kind in {"LINE", "ARC"}:
                p["a"], p["b"] = ends(item)
            if kind == "LINE":
                p["length"] = distance(p["a"], p["b"])
            else:
                p["c"], p["r"] = item["c"], item["r"]
                p["length"] = (TAU if kind == "CIRCLE" else abs(item["sweep"])) * p["r"]
                if kind == "ARC":
                    mid_angle = item["start"] + item["sweep"] / 2
                    p["mid"] = [p["c"][0] + p["r"] * math.cos(mid_angle), p["c"][1] + p["r"] * math.sin(mid_angle)]
            target.append(p)
        require(len(source) == len(target), "CUT_PRIMITIVE_MULTISET_COUNT_MISMATCH")

        def anchors(p):
            return [p[key] for key in ["a", "b", "c", "mid"] if key in p]

        def centroid(values):
            points = [point_value for p in values for point_value in anchors(p)]
            return [sum(v[i] for v in points) / len(points) for i in range(2)]

        def same(a, b):
            if a["kind"] != b["kind"] or abs(a["length"] - b["length"]) > tolerance:
                return False
            if a["kind"] in {"ARC", "CIRCLE"} and (distance(a["c"], b["c"]) > tolerance or abs(a["r"] - b["r"]) > tolerance):
                return False
            if a["kind"] in {"LINE", "ARC"} and min(max(distance(a["a"], b["a"]), distance(a["b"], b["b"])),
                                                      max(distance(a["a"], b["b"]), distance(a["b"], b["a"]))) > tolerance:
                return False
            return a["kind"] != "ARC" or distance(a["mid"], b["mid"]) <= tolerance

        source_center, target_center = centroid(source), centroid(target)
        for swap in [False, True]:
            for sx in [-1, 1]:
                for sy in [-1, 1]:
                    matrix = [[0, sx], [sy, 0]] if swap else [[sx, 0], [0, sy]]
                    rotated_center = rigid_point(source_center, matrix, [0, 0])
                    translation = [target_center[i] - rotated_center[i] for i in range(2)]
                    transformed = [dict(p, **{key: rigid_point(p[key], matrix, translation) for key in ["a", "b", "c", "mid"] if key in p}) for p in source]
                    matches = [[i for i, target_item in enumerate(target) if same(p, target_item)] for p in transformed]
                    matched = all(len(row) == 1 for row in matches) and len({row[0] for row in matches if row}) == len(target)
                    result["candidateDiagnostics"].append({"matrix": matrix, "uniquePrimitiveMatches": sum(len(row) == 1 for row in matches), "fullMultisetMatched": matched})
                    if matched:
                        determinant = matrix[0][0] * matrix[1][1] - matrix[0][1] * matrix[1][0]
                        result["candidates"].append({"matrix": matrix, "translationMm": translation, "determinant": determinant,
                                                     "mirrored": determinant < 0, "matchedPrimitiveCount": len(source),
                                                     "dxfPrimitiveIndices": [row[0] for row in matches]})
        result["candidateCount"] = len(result["candidates"])
        require(result["candidateCount"] > 0, "NO_FULL_CUT_RIGID_COORDINATE_MAPPING")
        require(result["candidateCount"] == 1, "AMBIGUOUS_FULL_CUT_COORDINATE_MAPPING")
        result["verified"] = True
        result["mapping"] = result["candidates"][0]
        result["primitiveCount"] = len(source)
        result["toleranceMm"] = tolerance
    except (AuditError, KeyError, TypeError, ValueError, IndexError) as error:
        result["errors"].append(str(error))
    return result


def native_bend_comparison(report, evidence):
    """Bind native view-relative directions through verified coordinates; never change DXF."""
    problems, rows = [], []
    expected_hash = report["sha256"]
    if evidence.get("dxfSha256") != expected_hash:
        problems.append("NATIVE_EVIDENCE_DXF_HASH_MISMATCH")
    if not report["units"]["verified"] or report["units"]["insunits"] != 4:
        problems.append("NATIVE_COMPARISON_REQUIRES_DXF_MM")
    api_alignment = evidence.get("apiAlignmentSpecified", True)
    use_cut_mapping = api_alignment is False and "nativeCutGeometry" in evidence
    cut_mapping = cut_coordinate_mapping(report, evidence) if use_cut_mapping else None
    if use_cut_mapping:
        alignment = None
        valid_basis = (evidence.get("nativeExportMethod") == "IPartDoc.ExportFlatPatternView direct file"
                       and evidence.get("alignment") is None and cut_mapping["verified"])
        problems.extend(cut_mapping["errors"])
        if not valid_basis and not cut_mapping["errors"]:
            problems.append("NATIVE_CUT_MAPPING_API_BASIS_UNVERIFIED")
    elif api_alignment is False:
        alignment = evidence.get("comparisonAlignment")
        valid_basis = (evidence.get("nativeExportMethod") == "IPartDoc.ExportFlatPatternView direct file"
                       and evidence.get("alignment") is None
                       and evidence.get("comparisonXFlipped") is False
                       and evidence.get("comparisonYFlipped") is False)
    else:
        alignment = evidence.get("alignment")
        valid_basis = (api_alignment is True and evidence.get("xFlipped") is False
                       and evidence.get("yFlipped") is False)
    if not use_cut_mapping and (not isinstance(alignment, list) or len(alignment) != 12 or not valid_basis):
        problems.append("RECORDED_UNFLIPPED_EXPORT_ALIGNMENT_REQUIRED")
        alignment = None
    if alignment is not None and alignment != [0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1]:
        problems.append("UNSUPPORTED_EXPORT_ALIGNMENT_NO_FITTING_ALLOWED")

    def transform(point_value, matrix):
        require(len(point_value) == 3 and len(matrix) == 16 and matrix[12] > 0,
                "INVALID_NATIVE_TRANSFORM")
        result = [matrix[12] * sum(point_value[k] * matrix[i + 3 * k] for k in range(3))
                  + matrix[i + 9] for i in range(3)]
        require(all(math.isfinite(v) for v in result), "NONFINITE_NATIVE_TRANSFORM")
        return result

    def gap(a, b):
        require(len(a) == len(b) and all(math.isfinite(v) for v in list(a) + list(b)),
                "FINITE_MATCHING_POINT_DIMENSIONS_REQUIRED")
        return math.sqrt(sum((x - y) ** 2 for x, y in zip(a, b)))

    def export_point(point_value):
        if use_cut_mapping:
            mapping = cut_mapping["mapping"]
            return rigid_point([v * 1000 for v in point_value[:2]], mapping["matrix"], mapping["translationMm"])
        return [sum((point_value[k] - alignment[k]) * alignment[3 + 3 * axis + k]
                    for k in range(3)) * 1000 for axis in [0, 1]]

    native_lines = evidence.get("lines", [])
    dxf_lines = report["bends"]["lines"]
    candidates = []
    for item in native_lines:
        row = {"id": item.get("id"), "positionVerified": False, "directionVerified": False,
               "direction": "UNKNOWN", "candidateDxfIndices": []}
        rows.append(row)
        try:
            require(valid_basis and (use_cut_mapping or alignment is not None), "VERIFIED_COORDINATE_BASIS_REQUIRED")
            require(item.get("modelEntityIdentityVerified") is True, "MODEL_ENTITY_IDENTITY_UNVERIFIED")
            a, b = item["startSketchMetres"], item["endSketchMetres"]
            ma, mb = transform(a, item["sketchToModelTransform"]), transform(b, item["sketchToModelTransform"])
            require(gap(transform(ma, item["modelToSketchTransform"]), a) <= 1e-7
                    and gap(transform(mb, item["modelToSketchTransform"]), b) <= 1e-7,
                    "NATIVE_TRANSFORM_ROUNDTRIP_FAILED")
            require(abs(gap(ma, mb) * 1000 - item["lengthMm"]) <= 0.01,
                    "NATIVE_LENGTH_TRANSFORM_MISMATCH")
            require(gap(ma, item["startModelMetres"]) <= 1e-7
                    and gap(mb, item["endModelMetres"]) <= 1e-7, "NATIVE_MODEL_POINT_MISMATCH")
            va, vb = transform(ma, item["modelToViewTransform"]), transform(mb, item["modelToViewTransform"])
            require(gap([v * 1000 for v in va], item["startSheetMm"]) <= 0.01
                    and gap([v * 1000 for v in vb], item["endSheetMm"]) <= 0.01,
                    "NATIVE_VIEW_POINT_MISMATCH")
            da, db = export_point(ma), export_point(mb)
            if not use_cut_mapping:
                require(gap(da, item["startDxfMm"]) <= 0.01 and gap(db, item["endDxfMm"]) <= 0.01,
                        "NATIVE_EXPORT_POINT_MISMATCH")
            matches = [index for index, line in enumerate(dxf_lines)
                       if min(max(distance(da, line["start"]), distance(db, line["end"])),
                              max(distance(da, line["end"]), distance(db, line["start"]))) <= 0.01]
            row["candidateDxfIndices"] = matches
            row["startDxfMm"] = da
            row["endDxfMm"] = db
            direction = item.get("directionEnum")
            official = {1: ("swUpDirection", "UP"), 2: ("swDownDirection", "DOWN")}.get(direction)
            require(official is not None and item.get("directionEnumName") == official[0]
                    and item.get("direction") == official[1], "OFFICIAL_NATIVE_DIRECTION_UNVERIFIED")
            row["nativeDirection"] = official[1]
            row["directionBasis"] = "Official native flat-view direction associated by position; not a manufacturing-side direction"
            candidates.extend(matches)
        except (AuditError, KeyError, TypeError, ValueError, IndexError) as error:
            row["unverifiedReason"] = str(error)
    usage = collections.Counter(candidates)
    for row in rows:
        matches = row["candidateDxfIndices"]
        verified = not problems and "unverifiedReason" not in row and len(matches) == 1 and usage[matches[0]] == 1
        row["positionVerified"] = verified
        row["directionVerified"] = verified
        if verified:
            row["direction"] = row["nativeDirection"]
    verified = (not problems and bool(rows) and len(rows) == len(dxf_lines)
                and all(row["directionVerified"] for row in rows)
                and {row["candidateDxfIndices"][0] for row in rows} == set(range(len(dxf_lines))))
    return {"positionsAndDirectionsVerified": verified, "nativeLineCount": len(rows),
            "dxfLineCount": len(dxf_lines), "lines": rows, "errors": problems,
            "method": "Recomputed sketch->model->unique complete CUT coordinate mapping->DXF endpoint comparison" if use_cut_mapping else "Recomputed sketch->model->literal recorded comparison basis and actual DXF endpoint matching, 0.01 mm; no fitting",
            "apiAlignmentSpecified": api_alignment, "basisConfirmedByEndpointComparison": verified,
            "cutCoordinateMapping": cut_mapping, "coordinateMappingVerified": bool(cut_mapping and cut_mapping["verified"]),
            "nativeViewDirectionAssociationVerified": verified, "manufacturingOrientationVerified": False,
            "directionConvention": "Official native flat-view swBendLineDirection_e; manufacturing orientation unverified",
            "dxfGeometryChanged": False, "countOnlyAcceptance": False}


def find_native_bends(document, dxf_hash, dxf_path=None):
    found = []

    def visit(value, depth=0):
        require(depth < 60, "NATIVE_EVIDENCE_NESTING_LIMIT")
        if isinstance(value, dict):
            path_match = dxf_path is None or os.path.normcase(os.path.abspath(value.get("dxfPath") or "")) == os.path.normcase(os.path.abspath(dxf_path))
            if value.get("dxfSha256") == dxf_hash and path_match and isinstance(value.get("lines"), list):
                found.append(value)
            else:
                for child in value.values():
                    visit(child, depth + 1)
        elif isinstance(value, list):
            for child in value:
                visit(child, depth + 1)

    visit(document)
    require(len(found) == 1, "ONE_NATIVE_BEND_EVIDENCE_FOR_DXF_HASH_REQUIRED")
    return found[0]


def dxf_fixture(entities, units=4):
    tags = [(0, "SECTION"), (2, "HEADER")]
    if units is not None:
        tags.extend([(9, "$INSUNITS"), (70, units)])
    tags.extend([(0, "ENDSEC"), (0, "SECTION"), (2, "ENTITIES")])
    for kind, layer, values in entities:
        tags.extend([(0, kind), (8, layer)] + values)
    tags.extend([(0, "ENDSEC"), (0, "EOF")])
    return ("\n".join(str(item) for pair in tags for item in pair) + "\n").encode("ascii")


def self_test():
    square = [("LINE", "CUT", [(10, a[0]), (20, a[1]), (11, b[0]), (21, b[1])])
              for a, b in [((0, 0), (10, 0)), ((10, 0), (10, 20)),
                           ((10, 20), (0, 20)), ((0, 20), (0, 0))]]
    hole = ("CIRCLE", "CUT", [(10, 5), (20, 10), (40, 1)])
    bend = ("LINE", "BEND", [(10, 2), (20, 0), (11, 2), (21, 20)])
    checks = []

    def check(name, condition):
        require(condition, "SELF_TEST_FAILED: " + name)
        checks.append(name)

    base = audit(dxf_fixture(square + [hole, bend]), 10, 20, 1)
    check("closed_square_hole_bend_expected_dimensions", base["auditPassed"])
    check("round_hole_coordinates", base["roundHoles"][0]["xFromCutMin"] == 5
          and base["roundHoles"][0]["diameter"] == 2)
    check("nested_hole_classification", base["closedContours"]["holeContourCount"] == 1)
    duplicate = audit(dxf_fixture(square + [square[0]]))
    check("duplicate_rejected", not duplicate["auditPassed"] and bool(duplicate["duplicateSegments"]))
    opened = audit(dxf_fixture(square[:-1]))
    check("open_chain_rejected", not opened["auditPassed"] and opened["closedContours"]["openChainCount"] == 1)
    arc_shape = [("ARC", "CUT", [(10, 0), (20, 0), (40, 5), (50, 0), (51, 180)]),
                 ("LINE", "CUT", [(10, -5), (20, 0), (11, 5), (21, 0)])]
    arc_report = audit(dxf_fixture(arc_shape), 10, 5)
    check("arc_extrema_and_closed_contour", arc_report["auditPassed"])
    bulge = [("LWPOLYLINE", "CUT", [(90, 2), (70, 1), (10, -5), (20, 0),
                                        (42, 1), (10, 5), (20, 0)])]
    bulge_report = audit(dxf_fixture(bulge), 10, 5)
    check("bulge_arc_and_closed_contour", bulge_report["auditPassed"])
    arc_hole = [("ARC", "CUT", [(10, 5), (20, 10), (40, 1), (50, 0), (51, 180)]),
                ("ARC", "CUT", [(10, 5), (20, 10), (40, 1), (50, 180), (51, 360)])]
    arc_hole_report = audit(dxf_fixture(square + arc_hole))
    check("closed_arc_chain_round_hole", arc_hole_report["auditPassed"]
          and arc_hole_report["roundHoleCount"] == 1
          and arc_hole_report["roundHoles"][0]["diameter"] == 2)
    circular_outer = audit(dxf_fixture([("CIRCLE", "CUT", hole[2])]))
    check("circular_outer_is_not_hole", circular_outer["auditPassed"] and circular_outer["roundHoleCount"] == 0)
    unknown = audit(dxf_fixture(square, None))
    check("unknown_units_rejected", not unknown["auditPassed"] and not unknown["units"]["verified"])
    directed = audit(dxf_fixture(square + [("LINE", "BEND_UP", bend[2])]))
    check("unproven_bend_direction_rejected", not directed["auditPassed"])
    unsupported = audit(dxf_fixture(square + [("SPLINE", "CUT", [])]))
    check("unsupported_entity_rejected", not unsupported["auditPassed"] and bool(unsupported["unsupportedEntities"]))
    mismatch = audit(dxf_fixture(square + [bend]), 11, 21, 12)
    check("expected_dimensions_and_bend_count_rejected", not mismatch["auditPassed"]
          and len(mismatch["expectedChecks"]) == 3 and not any(v["pass"] for v in mismatch["expectedChecks"].values()))
    inch_square = audit(dxf_fixture(square, 1), 254, 508)
    check("known_unit_conversion", inch_square["auditPassed"])
    zero = audit(dxf_fixture(square + [("LINE", "MARK", [(10, 3), (20, 2), (11, 3), (21, 2)])]))
    check("zero_length_rejected", not zero["auditPassed"] and bool(zero["zeroLengthSegments"]))
    extruded = square + [("CIRCLE", "CUT", hole[2] + [(230, -1)])]
    try:
        audit(dxf_fixture(extruded))
        check("nonstandard_extrusion_rejected", False)
    except AuditError as error:
        check("nonstandard_extrusion_rejected", "EXTRUSION" in str(error))
    bow_tie = [("LINE", "CUT", [(10, a[0]), (20, a[1]), (11, b[0]), (21, b[1])])
               for a, b in [((0, 0), (10, 10)), ((10, 10), (0, 10)),
                            ((0, 10), (10, 0)), ((10, 0), (0, 0))]]
    crossing = audit(dxf_fixture(bow_tie))
    check("self_crossing_cut_rejected", not crossing["auditPassed"] and bool(crossing["cutIntersections"]))
    overlapping = audit(dxf_fixture(square + [("LINE", "CUT", [(10, 2), (20, 0), (11, 8), (21, 0)])]))
    check("partial_collinear_overlap_rejected", not overlapping["auditPassed"] and bool(overlapping["cutIntersections"]))
    polyline = [("POLYLINE", "CUT", [(70, 1)]),
                ("VERTEX", "CUT", [(10, 0), (20, 0)]),
                ("VERTEX", "CUT", [(10, 10), (20, 0)]),
                ("VERTEX", "CUT", [(10, 10), (20, 20)]),
                ("VERTEX", "CUT", [(10, 0), (20, 20)]), ("SEQEND", "CUT", [])]
    check("legacy_polyline_closed_contour", audit(dxf_fixture(polyline), 10, 20)["auditPassed"])
    try:
        audit(b"AutoCAD Binary DXF\r\n\x1a\x00")
        check("binary_dxf_rejected", False)
    except AuditError as error:
        check("binary_dxf_rejected", "BINARY_DXF" in str(error))
    identity = [1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0]
    native_line = {"id": "B1", "directionEnum": 1, "directionEnumName": "swUpDirection", "direction": "UP",
                   "startSketchMetres": [0.002, 0, 0], "endSketchMetres": [0.002, 0.020, 0],
                   "startModelMetres": [0.002, 0, 0], "endModelMetres": [0.002, 0.020, 0],
                   "modelToSketchTransform": identity, "sketchToModelTransform": identity,
                   "modelToViewTransform": identity, "startSheetMm": [2, 0, 0], "endSheetMm": [2, 20, 0],
                   "lengthMm": 20, "startDxfMm": [2, 0], "endDxfMm": [2, 20], "modelEntityIdentityVerified": True}
    evidence = {"dxfSha256": base["sha256"], "alignment": [0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1],
                "xFlipped": False, "yFlipped": False, "lines": [native_line]}
    check("native_fixed_basis_endpoint_direction_match", native_bend_comparison(base, evidence)["positionsAndDirectionsVerified"])
    shifted = dict(base, bends=dict(base["bends"], lines=[{"entity": 6, "start": [3, 0], "end": [3, 20]}]))
    check("same_count_wrong_bend_position_rejected", not native_bend_comparison(shifted, evidence)["positionsAndDirectionsVerified"])
    reversed_report = dict(base, bends=dict(base["bends"], lines=[{"entity": 6, "start": [2, 20], "end": [2, 0]}]))
    check("native_endpoint_order_does_not_invent_direction", native_bend_comparison(reversed_report, evidence)["positionsAndDirectionsVerified"])
    check("native_unknown_direction_rejected", not native_bend_comparison(base, dict(evidence, lines=[dict(native_line, directionEnum=0)]))["positionsAndDirectionsVerified"])
    check("native_wrong_dxf_hash_rejected", not native_bend_comparison(base, dict(evidence, dxfSha256="wrong"))["positionsAndDirectionsVerified"])
    check("native_owner_unverified_rejected", not native_bend_comparison(base, dict(evidence, lines=[dict(native_line, modelEntityIdentityVerified=False)]))["positionsAndDirectionsVerified"])
    check("native_duplicate_correspondence_rejected", not native_bend_comparison(base, dict(evidence, lines=[native_line, native_line]))["positionsAndDirectionsVerified"])
    legacy_evidence = dict(evidence, nativeExportMethod="IPartDoc.ExportFlatPatternView direct file",
                           apiAlignmentSpecified=False, alignment=None, xFlipped=None, yFlipped=None,
                           comparisonAlignment=evidence["alignment"], comparisonXFlipped=False,
                           comparisonYFlipped=False)
    check("legacy_literal_basis_actual_endpoint_match", native_bend_comparison(base, legacy_evidence)["positionsAndDirectionsVerified"])
    check("legacy_same_count_shifted_bends_rejected", not native_bend_comparison(shifted, legacy_evidence)["positionsAndDirectionsVerified"])
    check("legacy_false_api_alignment_claim_rejected", not native_bend_comparison(base, dict(legacy_evidence, alignment=evidence["alignment"]))["positionsAndDirectionsVerified"])
    check("legacy_unknown_export_method_rejected", not native_bend_comparison(base, dict(legacy_evidence, nativeExportMethod="unknown"))["positionsAndDirectionsVerified"])

    def native_snapshot(value):
        primitives = []
        for p in value["cuts"]["primitives"]:
            item = {"type": p["kind"]}
            if p["kind"] in {"LINE", "ARC"}:
                a, b = ends(p)
                item.update(startMm=list(a) + [0.4], endMm=list(b) + [0.4])
            if p["kind"] == "LINE":
                item["lengthMm"] = distance(a, b)
            else:
                item.update(centerMm=list(p["c"]) + [0.4], radiusMm=p["r"])
                item["lengthMm"] = (TAU if p["kind"] == "CIRCLE" else abs(p["sweep"])) * p["r"]
                if p["kind"] == "ARC":
                    t = p["start"] + p["sweep"] / 2
                    item["midpointMm"] = [p["c"][0] + p["r"] * math.cos(t), p["c"][1] + p["r"] * math.sin(t), 0.4]
            primitives.append(item)
        return {"schema": "native-top-z-cut-v1", "complete": True, "errors": [], "units": "mm",
                "modelPath": r"D:\trial\door_L.SLDPRT", "configuration": "DefaultSM-FLAT-PATTERN", "modelHand": "L",
                "topZMm": 0.4, "topToleranceMm": 0.0001, "boundaryEdgeCount": len(primitives), "primitives": primitives}

    def source_evidence(source_report, target_report):
        return dict(legacy_evidence, dxfSha256=target_report["sha256"], nativeCutGeometry=native_snapshot(source_report),
                    modelPath=r"D:\trial\door_L.SLDPRT", exportConfiguration="DefaultSM-FLAT-PATTERN",
                    modelHand="L", drawingHand="L", exportModelHand="L", sourceIdentityVerified=True,
                    modelSha256="A" * 64, exportModelSha256="A" * 64)

    def mapped_fixture(entities, matrix, translation):
        transformed = []
        determinant = matrix[0][0] * matrix[1][1] - matrix[0][1] * matrix[1][0]
        for kind, layer, tags in entities:
            original = dict(tags)
            def p(xcode):
                return rigid_point([original[xcode], original[xcode + 10]], matrix, translation)
            a = p(10)
            values = [(10, a[0]), (20, a[1])]
            if kind == "LINE":
                b = p(11)
                values.extend([(11, b[0]), (21, b[1])])
            else:
                values.append((40, original[40]))
                if kind == "ARC":
                    angles = []
                    for angle in [original[50], original[51]]:
                        rotated = rigid_point([math.cos(math.radians(angle)), math.sin(math.radians(angle))], matrix, [0, 0])
                        angles.append(math.degrees(math.atan2(rotated[1], rotated[0])) % 360)
                    if determinant < 0:
                        angles.reverse()
                    values.extend([(50, angles[0]), (51, angles[1])])
            transformed.append((kind, layer, values))
        return dxf_fixture(transformed)

    asymmetric_entities = square + [("CIRCLE", "CUT", [(10, 3), (20, 7), (40, 1)]), bend]
    asymmetric = audit(dxf_fixture(asymmetric_entities))
    translated = audit(mapped_fixture(asymmetric_entities, [[1, 0], [0, 1]], [139.7, 107.95]))
    full_evidence = source_evidence(asymmetric, translated)
    full_comparison = native_bend_comparison(translated, full_evidence)
    check("full_cut_unique_translation_binds_native_bend", full_comparison["positionsAndDirectionsVerified"]
          and full_comparison["cutCoordinateMapping"]["candidateCount"] == 1
          and not full_comparison["manufacturingOrientationVerified"])
    mirrored = audit(mapped_fixture(asymmetric_entities, [[0, -1], [-1, 0]], [50, 70]))
    mirror_comparison = native_bend_comparison(mirrored, source_evidence(asymmetric, mirrored))
    check("full_cut_unique_axis_swap_mirror_binds_view_direction", mirror_comparison["positionsAndDirectionsVerified"]
          and mirror_comparison["cutCoordinateMapping"]["mapping"]["mirrored"]
          and mirror_comparison["lines"][0]["direction"] == "UP"
          and not mirror_comparison["manufacturingOrientationVerified"])
    wrong_hand = dict(full_evidence, drawingHand="R")
    check("wrong_hand_metadata_rejected_despite_geometry_match", not native_bend_comparison(translated, wrong_hand)["positionsAndDirectionsVerified"])
    moved_hole = audit(mapped_fixture(square + [("CIRCLE", "CUT", [(10, 3.2), (20, 7), (40, 1)]), bend], [[1, 0], [0, 1]], [139.7, 107.95]))
    check("same_bbox_and_count_moved_hole_rejected", not native_bend_comparison(moved_hole, source_evidence(asymmetric, moved_hole))["positionsAndDirectionsVerified"])
    symmetric_mapping = cut_coordinate_mapping(base, source_evidence(base, base))
    check("symmetric_full_cut_multiple_mappings_rejected", not symmetric_mapping["verified"] and symmetric_mapping["candidateCount"] > 1)
    scaled = audit(mapped_fixture([(kind, layer, [(code, value * 1.1 if code in {10, 20, 11, 21, 40} else value) for code, value in tags])
                                  for kind, layer, tags in asymmetric_entities], [[1, 0], [0, 1]], [0, 0]))
    check("nonunit_scale_rejected", not cut_coordinate_mapping(scaled, source_evidence(asymmetric, scaled))["verified"])
    shifted_bend = dict(translated, bends=dict(translated["bends"], lines=[{"entity": 6, "start": [142.7, 107.95], "end": [142.7, 127.95]}]))
    check("correct_cut_wrong_bend_positions_rejected", not native_bend_comparison(shifted_bend, full_evidence)["positionsAndDirectionsVerified"])
    check("cut_source_hash_mismatch_rejected", not native_bend_comparison(translated, dict(full_evidence, exportModelSha256="B" * 64))["positionsAndDirectionsVerified"])
    incomplete = dict(full_evidence, nativeCutGeometry=dict(full_evidence["nativeCutGeometry"], complete=False))
    check("incomplete_native_cut_no_literal_fallback", not native_bend_comparison(translated, incomplete)["positionsAndDirectionsVerified"])
    off_plane = json.loads(json.dumps(full_evidence))
    off_plane["nativeCutGeometry"]["primitives"][0]["startMm"][2] += 0.001
    check("native_cut_off_top_z_rejected", not cut_coordinate_mapping(translated, off_plane)["verified"])
    reordered = dict(full_evidence, nativeCutGeometry=dict(full_evidence["nativeCutGeometry"], primitives=list(reversed(full_evidence["nativeCutGeometry"]["primitives"]))))
    check("complete_cut_multiset_is_order_independent", native_bend_comparison(translated, reordered)["positionsAndDirectionsVerified"])
    arc_asymmetric = audit(dxf_fixture(arc_shape + [("CIRCLE", "CUT", [(10, 1), (20, 2), (40, 0.4)])]))
    arc_evidence = source_evidence(arc_asymmetric, arc_asymmetric)
    check("native_arc_midpoint_and_trim_full_cut_match", cut_coordinate_mapping(arc_asymmetric, arc_evidence)["verified"])
    wrong_arc = json.loads(json.dumps(arc_evidence))
    next(p for p in wrong_arc["nativeCutGeometry"]["primitives"] if p["type"] == "ARC")["midpointMm"][1] *= -1
    check("wrong_arc_trim_rejected", not cut_coordinate_mapping(arc_asymmetric, wrong_arc)["verified"])
    return {"selfTestPassed": True, "count": len(checks), "checks": checks,
            "cadRun": False, "filesWritten": False}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("path", nargs="?")
    parser.add_argument("--out")
    parser.add_argument("--expect-width", type=float)
    parser.add_argument("--expect-height", type=float)
    parser.add_argument("--expect-bends", type=int)
    parser.add_argument("--native-bends", help="Drawing result or native-result JSON containing nativeBends evidence")
    parser.add_argument("--require-bend-directions", action="store_true")
    parser.add_argument("--tolerance-mm", type=float, default=0.01)
    parser.add_argument("--dimension-tolerance-mm", type=float, default=0.05)
    parser.add_argument("--self-test", action="store_true")
    args = parser.parse_args()
    try:
        if args.self_test:
            require(args.path is None and args.out is None and args.native_bends is None, "SELF_TEST_IS_MEMORY_ONLY")
            report = self_test()
        else:
            require(args.path is not None, "DXF_PATH_REQUIRED")
            source = Path(args.path)
            data = source.read_bytes()
            report = audit(data, args.expect_width, args.expect_height, args.expect_bends,
                           args.tolerance_mm, args.dimension_tolerance_mm)
            report["path"] = str(source.absolute())
            if args.native_bends:
                document = json.loads(Path(args.native_bends).read_text(encoding="utf-8-sig"))
                evidence = find_native_bends(document, report["sha256"], source.absolute())
                comparison = native_bend_comparison(report, evidence)
                report["nativeBendComparison"] = comparison
                report["bends"]["direction"] = "PER_LINE_NATIVE" if comparison["positionsAndDirectionsVerified"] else "UNKNOWN"
                if not comparison["positionsAndDirectionsVerified"]:
                    report["errors"].append("NATIVE_BEND_POSITIONS_OR_DIRECTIONS_UNVERIFIED")
                    report["success"] = report["auditPassed"] = False
            elif args.require_bend_directions:
                report["errors"].append("NATIVE_BEND_DIRECTION_EVIDENCE_REQUIRED")
                report["success"] = report["auditPassed"] = False
        rendered = json.dumps(report, ensure_ascii=False, indent=2, allow_nan=False)
        if args.out:
            target = new_report_path(args.out)
            target.parent.mkdir(parents=True, exist_ok=True)
            with target.open("x", encoding="utf-8", newline="\n") as output:
                output.write(rendered + "\n")
        print(rendered)
        return 0 if report.get("selfTestPassed", report.get("auditPassed", False)) else 2
    except (AuditError, OSError, ValueError) as error:
        print(json.dumps({"success": False, "auditPassed": False, "manufacturingAccepted": False,
                          "errors": [type(error).__name__ + ": " + str(error)]}, ensure_ascii=False, indent=2))
        return 2


if __name__ == "__main__":
    sys.exit(main())
