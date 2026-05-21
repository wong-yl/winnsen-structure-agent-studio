var fso = new ActiveXObject("Scripting.FileSystemObject");

var outAsmPath = String(WScript.Arguments(0));
var outJsonPath = String(WScript.Arguments(1));
var doorHeightMm = parseFloat(WScript.Arguments(2));
var panelPath = String(WScript.Arguments(3));
var stiffenerPath = String(WScript.Arguments(4));
var latchPlatePath = String(WScript.Arguments(5));
var hookPadPath = String(WScript.Arguments(6));

function esc(s) {
  return String(s).replace(/\\/g, "\\\\").replace(/"/g, "\\\"").replace(/\r/g, "\\r").replace(/\n/g, "\\n");
}

function stringify(v) {
  if (v === null || v === undefined) return "null";
  var t = typeof v;
  if (t === "number") return isFinite(v) ? String(v) : "null";
  if (t === "boolean") return v ? "true" : "false";
  if (t === "string") return "\"" + esc(v) + "\"";
  if (v instanceof Array) {
    var a = [];
    for (var i = 0; i < v.length; i++) a.push(stringify(v[i]));
    return "[" + a.join(",") + "]";
  }
  var rows = [];
  for (var k in v) if (v.hasOwnProperty(k)) rows.push("\"" + esc(k) + "\":" + stringify(v[k]));
  return "{" + rows.join(",") + "}";
}

function writeUtf8(path, text) {
  var s = new ActiveXObject("ADODB.Stream");
  s.Type = 2;
  s.Charset = "utf-8";
  s.Open();
  s.WriteText(text);
  s.SaveToFile(path, 2);
  s.Close();
}

function ensureFolder(path) {
  if (fso.FolderExists(path)) return;
  var parent = fso.GetParentFolderName(path);
  if (parent && !fso.FolderExists(parent)) ensureFolder(parent);
  fso.CreateFolder(path);
}

function safe(fn, fallback) {
  try { return fn(); } catch (e) { return fallback; }
}

function transform(mathUtil, r, txMm, tyMm, tzMm) {
  var data = [
    r[0], r[1], r[2],
    r[3], r[4], r[5],
    r[6], r[7], r[8],
    txMm / 1000.0, tyMm / 1000.0, tzMm / 1000.0,
    1, 0, 0, 0
  ];
  return mathUtil.CreateTransform(data);
}

function addComponent(asm, mathUtil, item) {
  var row = {
    role: item.role,
    path: item.path,
    exists: fso.FileExists(item.path),
    opened: false,
    added: false,
    transformed: false,
    tx_mm: item.tx,
    ty_mm: item.ty,
    tz_mm: item.tz,
    rotation: item.r.join(";"),
    error: ""
  };
  if (!row.exists) {
    row.error = "missing source";
    return row;
  }
  var docType = item.path.toLowerCase().indexOf(".sldasm") >= 0 ? 2 : 1;
  var doc = safe(function () { return sw.OpenDoc(item.path, docType); }, null);
  row.opened = !!doc;
  if (!doc) {
    row.error = "open failed";
    return row;
  }
  safe(function () { sw.ActivateDoc(asm.GetTitle()); return true; }, false);
  var comp = safe(function () {
    return asm.AddComponent5(item.path, 0, "", false, "", item.tx / 1000.0, item.ty / 1000.0, item.tz / 1000.0);
  }, null);
  if (!comp) comp = safe(function () { return asm.AddComponent(item.path, item.tx / 1000.0, item.ty / 1000.0, item.tz / 1000.0); }, null);
  row.added = !!comp;
  if (!comp) {
    row.error = "add failed";
    return row;
  }
  safe(function () { comp.Name2 = item.role; return true; }, false);
  safe(function () { sw.ActivateDoc(asm.GetTitle()); return true; }, false);
  var xf = safe(function () { return transform(mathUtil, item.r, item.tx, item.ty, item.tz); }, null);
  if (!xf) {
    row.error = "transform create failed";
    return row;
  }
  row.transformed = safe(function () { return comp.SetTransformAndSolve2(xf); }, false);
  if (!row.transformed) {
    row.transformed = safe(function () { return comp.SetTransformAndSolve3(xf, true); }, false);
  }
  if (!row.transformed) {
    row.transformed = safe(function () { comp.Transform2 = xf; return true; }, false);
  }
  if (!row.transformed) row.error = "transform apply failed";
  return row;
}

function collectReferences(doc) {
  var rows = [];
  var feat = safe(function () { return doc.FirstFeature(); }, null);
  var guard = 0;
  while (feat && guard < 2000) {
    guard++;
    var type = safe(function () { return String(feat.GetTypeName2()); }, "");
    if (type === "Reference") {
      var comp = safe(function () { return feat.GetSpecificFeature2(); }, null);
      rows.push({
        feature_name: safe(function () { return String(feat.Name); }, ""),
        component_name: comp ? safe(function () { return String(comp.Name2); }, "") : "",
        path: comp ? safe(function () { return String(comp.GetPathName()); }, "")
          : "",
        suppressed: comp ? safe(function () { return comp.IsSuppressed(); }, null) : null
      });
    }
    feat = safe(function () { return feat.GetNextFeature(); }, null);
  }
  return rows;
}

var identity = [1, 0, 0, 0, 1, 0, 0, 0, 1];
var rotateX90 = [1, 0, 0, 0, 0, 1, 0, -1, 0];
var latchTopTy = doorHeightMm / 2.0 - 0.8;
var latchBottomTy = -doorHeightMm / 2.0 + 28.8;

var placements = [
  { role: "door_panel_rule_part", path: panelPath, r: identity, tx: 0, ty: 0, tz: 0 },
  { role: "door_stiffener_rule_part", path: stiffenerPath, r: identity, tx: 0, ty: 0, tz: -14.8 },
  { role: "latch_plate_top", path: latchPlatePath, r: rotateX90, tx: -208.5, ty: latchTopTy, tz: -14.0 },
  { role: "latch_plate_bottom", path: latchPlatePath, r: rotateX90, tx: -208.5, ty: latchBottomTy, tz: -14.0 },
  { role: "u_hook_pad", path: hookPadPath, r: identity, tx: 206.3, ty: 0, tz: -1.6 }
];

var result = {
  outAsmPath: outAsmPath,
  doorHeightMm: doorHeightMm,
  newAssembly: false,
  rebuilt: false,
  saved: false,
  placements: [],
  references: []
};

ensureFolder(fso.GetParentFolderName(outAsmPath));
if (fso.FileExists(outAsmPath)) fso.DeleteFile(outAsmPath, true);

var sw = new ActiveXObject("SldWorks.Application");
safe(function () { sw.Visible = true; }, null);
safe(function () { sw.CloseAllDocuments(true); return true; }, false);

var asm = safe(function () { return sw.NewAssembly(); }, null);
if (!asm) {
  result.error = "NewAssembly failed";
  writeUtf8(outJsonPath, stringify(result));
  WScript.Echo(outJsonPath);
  WScript.Quit(2);
}
result.newAssembly = true;
var mathUtil = sw.GetMathUtility();

for (var i = 0; i < placements.length; i++) {
  result.placements.push(addComponent(asm, mathUtil, placements[i]));
}

result.rebuilt = safe(function () { return asm.ForceRebuild3(false); }, false);
result.references = collectReferences(asm);
result.saved = safe(function () { return asm.SaveAs(outAsmPath); }, false);
safe(function () { sw.ActivateDoc(asm.GetTitle()); return true; }, false);
safe(function () { asm.ShowNamedView2("*Isometric", 7); return true; }, false);
safe(function () { asm.ViewZoomtofit2(); return true; }, false);
safe(function () { sw.CloseDoc(asm.GetTitle()); }, null);

writeUtf8(outJsonPath, stringify(result));
WScript.Echo(outJsonPath);
