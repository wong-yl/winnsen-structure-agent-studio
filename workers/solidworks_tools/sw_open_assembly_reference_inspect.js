var fso = new ActiveXObject("Scripting.FileSystemObject");

var asmPath = String(WScript.Arguments(0));
var outJsonPath = String(WScript.Arguments(1));
var closeAfter = WScript.Arguments.length > 2 ? String(WScript.Arguments(2)).toLowerCase() !== "keepopen" : true;

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

function toArray(v) {
  var out = [];
  if (v === null || v === undefined) return out;
  try {
    var e = new Enumerator(v);
    for (; !e.atEnd(); e.moveNext()) out.push(Number(e.item()));
    return out;
  } catch (ignored) {}
  try {
    for (var i = 0; i < v.length; i++) out.push(Number(v[i]));
  } catch (ignored2) {}
  return out;
}

function readTransform(comp) {
  var xf = safe(function () { return comp.Transform2; }, null);
  var data = xf ? safe(function () { return xf.ArrayData; }, null) : null;
  return toArray(data);
}

function readBox(comp) {
  var box = safe(function () { return comp.GetBox(false, false); }, null);
  if (!box) box = safe(function () { return comp.GetBox(false); }, null);
  return toArray(box);
}

function readComponent(comp, featureName, source) {
  if (!comp) return { feature_name: featureName, source: source, error: "null_component" };
  return {
    feature_name: featureName,
    source: source,
    component_name: safe(function () { return String(comp.Name2); }, ""),
    path: safe(function () { return String(comp.GetPathName()); }, ""),
    suppressed: safe(function () { return comp.IsSuppressed(); }, null),
    transform: readTransform(comp),
    box: readBox(comp)
  };
}

function collectFeatureReferences(doc) {
  var rows = [];
  var feat = safe(function () { return doc.FirstFeature(); }, null);
  var guard = 0;
  while (feat && guard < 5000) {
    guard++;
    var type = safe(function () { return String(feat.GetTypeName2()); }, "");
    if (type === "Reference") {
      var comp = safe(function () { return feat.GetSpecificFeature2(); }, null);
      rows.push(readComponent(comp, safe(function () { return String(feat.Name); }, ""), "feature"));
    }
    feat = safe(function () { return feat.GetNextFeature(); }, null);
  }
  return rows;
}

function collectGetComponents(doc) {
  var rows = [];
  var comps = safe(function () { return doc.GetComponents(false); }, null);
  if (!comps) return rows;
  try {
    var e = new Enumerator(comps);
    for (; !e.atEnd(); e.moveNext()) rows.push(readComponent(e.item(), "", "GetComponents(false)"));
    return rows;
  } catch (ignored) {}
  try {
    for (var i = 0; i < comps.length; i++) rows.push(readComponent(comps[i], "", "GetComponents(false)"));
  } catch (ignored2) {}
  return rows;
}

ensureFolder(fso.GetParentFolderName(outJsonPath));

var result = {
  assembly_path: asmPath,
  out_json_path: outJsonPath,
  exists: fso.FileExists(asmPath),
  opened: false,
  references: [],
  components: []
};

var sw = new ActiveXObject("SldWorks.Application");
safe(function () { sw.Visible = true; }, null);
var errors = 0;
var warnings = 0;
var doc = safe(function () { return sw.OpenDoc6(asmPath, 2, 1, "", errors, warnings); }, null);
if (!doc) doc = safe(function () { return sw.OpenDoc(asmPath, 2); }, null);

result.opened = !!doc;
result.open_errors = errors;
result.open_warnings = warnings;
if (doc) {
  result.title = safe(function () { return String(doc.GetTitle()); }, "");
  result.path = safe(function () { return String(doc.GetPathName()); }, "");
  result.rebuilt = safe(function () { return doc.ForceRebuild3(false); }, false);
  result.references = collectFeatureReferences(doc);
  result.components = collectGetComponents(doc);
  result.reference_count = result.references.length;
  result.component_count = result.components.length;
  if (closeAfter) safe(function () { sw.CloseDoc(doc.GetTitle()); return true; }, false);
}

writeUtf8(outJsonPath, stringify(result));
WScript.Echo(outJsonPath);
WScript.Quit(result.opened ? 0 : 2);
