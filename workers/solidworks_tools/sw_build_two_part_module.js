var fso = new ActiveXObject("Scripting.FileSystemObject");

var outAsmPath = String(WScript.Arguments(0));
var outJsonPath = String(WScript.Arguments(1));
var partAPath = String(WScript.Arguments(2));
var partAName = String(WScript.Arguments(3));
var partBPath = String(WScript.Arguments(4));
var partBName = String(WScript.Arguments(5));
var partATxMm = WScript.Arguments.length > 6 ? Number(WScript.Arguments(6)) : 0;
var partATyMm = WScript.Arguments.length > 7 ? Number(WScript.Arguments(7)) : 0;
var partATzMm = WScript.Arguments.length > 8 ? Number(WScript.Arguments(8)) : 0;
var partBTxMm = WScript.Arguments.length > 9 ? Number(WScript.Arguments(9)) : 0;
var partBTyMm = WScript.Arguments.length > 10 ? Number(WScript.Arguments(10)) : 0;
var partBTzMm = WScript.Arguments.length > 11 ? Number(WScript.Arguments(11)) : 0;

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

function collectReferences(doc) {
  var rows = [];
  var feat = safe(function () { return doc.FirstFeature(); }, null);
  var guard = 0;
  while (feat && guard < 1000) {
    guard++;
    var type = safe(function () { return String(feat.GetTypeName2()); }, "");
    if (type === "Reference") {
      var comp = safe(function () { return feat.GetSpecificFeature2(); }, null);
      rows.push({
        feature_name: safe(function () { return String(feat.Name); }, ""),
        component_name: comp ? safe(function () { return String(comp.Name2); }, "") : "",
        path: comp ? safe(function () { return String(comp.GetPathName()); }, "") : "",
        suppressed: comp ? safe(function () { return comp.IsSuppressed(); }, null) : null
      });
    }
    feat = safe(function () { return feat.GetNextFeature(); }, null);
  }
  return rows;
}

function addComponent(asm, path, name, x, y, z) {
  var comp = safe(function () { return asm.AddComponent5(path, 0, "", false, "", x, y, z); }, null);
  if (!comp) comp = safe(function () { return asm.AddComponent(path, x, y, z); }, null);
  if (comp) safe(function () { comp.Name2 = name; return true; }, false);
  return comp;
}

function docTypeFor(path) {
  var ext = String(fso.GetExtensionName(path)).toLowerCase();
  if (ext === "sldasm") return 2;
  if (ext === "slddrw") return 3;
  return 1;
}

var result = {
  outAsmPath: outAsmPath,
  partAPath: partAPath,
  partBPath: partBPath,
  partATxMm: partATxMm,
  partATyMm: partATyMm,
  partATzMm: partATzMm,
  partBTxMm: partBTxMm,
  partBTyMm: partBTyMm,
  partBTzMm: partBTzMm,
  partAExists: fso.FileExists(partAPath),
  partBExists: fso.FileExists(partBPath),
  newAssembly: false,
  partAAdded: false,
  partBAdded: false,
  rebuilt: false,
  saved: false,
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

var docA = safe(function () { return sw.OpenDoc(partAPath, docTypeFor(partAPath)); }, null);
var docB = safe(function () { return sw.OpenDoc(partBPath, docTypeFor(partBPath)); }, null);
result.partAOpened = !!docA;
result.partBOpened = !!docB;

var compA = result.partAOpened ? addComponent(asm, partAPath, partAName, partATxMm / 1000, partATyMm / 1000, partATzMm / 1000) : null;
var compB = result.partBOpened ? addComponent(asm, partBPath, partBName, partBTxMm / 1000, partBTyMm / 1000, partBTzMm / 1000) : null;
result.partAAdded = !!compA;
result.partBAdded = !!compB;

result.rebuilt = safe(function () { return asm.ForceRebuild3(false); }, false);
result.references = collectReferences(asm);
result.saved = safe(function () { return asm.SaveAs(outAsmPath); }, false);
safe(function () { sw.ActivateDoc(asm.GetTitle()); return true; }, false);
safe(function () { asm.ShowNamedView2("*Isometric", 7); return true; }, false);
safe(function () { asm.ViewZoomtofit2(); return true; }, false);

writeUtf8(outJsonPath, stringify(result));
WScript.Echo(outJsonPath);
