var fso = new ActiveXObject("Scripting.FileSystemObject");

var sourcePath = WScript.Arguments.length > 0 ? String(WScript.Arguments(0)) : "";
var outPath = WScript.Arguments.length > 1 ? String(WScript.Arguments(1)) : "";

function safe(fn, fallback) {
  try { return fn(); } catch (e) { return fallback; }
}

function toArray(v) {
  var out = [];
  if (v === null || v === undefined) return out;
  try {
    var a = new VBArray(v);
    for (var i = a.lbound(); i <= a.ubound(); i++) out.push(a.getItem(i));
    return out;
  } catch (e0) {}
  try {
    for (var j = 0; j < v.length; j++) out.push(v[j]);
  } catch (e1) {}
  return out;
}

function escJson(s) {
  return String(s).replace(/\\/g, "\\\\").replace(/"/g, "\\\"").replace(/\r/g, "\\r").replace(/\n/g, "\\n").replace(/\t/g, "\\t");
}

function stringify(v) {
  if (v === null || v === undefined) return "null";
  var t = typeof v;
  if (t === "number") return isFinite(v) ? String(v) : "null";
  if (t === "boolean") return v ? "true" : "false";
  if (t === "string") return "\"" + escJson(v) + "\"";
  if (v instanceof Array) {
    var items = [];
    for (var i = 0; i < v.length; i++) items.push(stringify(v[i]));
    return "[" + items.join(",") + "]";
  }
  var rows = [];
  for (var k in v) {
    if (v.hasOwnProperty(k)) rows.push("\"" + escJson(k) + "\":" + stringify(v[k]));
  }
  return "{" + rows.join(",") + "}";
}

function writeUtf8(path, text) {
  var stream = new ActiveXObject("ADODB.Stream");
  stream.Type = 2;
  stream.Charset = "utf-8";
  stream.Open();
  stream.WriteText(text);
  stream.SaveToFile(path, 2);
  stream.Close();
}

function transformInfo(comp) {
  var direct = safe(function () { return comp.Transform2; }, null);
  var call = safe(function () { return comp.Transform2(); }, null);
  var totalTrue = safe(function () { return comp.GetTotalTransform(true); }, null);
  var totalFalse = safe(function () { return comp.GetTotalTransform(false); }, null);
  return {
    direct: toArray(direct ? safe(function () { return direct.ArrayData; }, null) : null).length,
    call: toArray(call ? safe(function () { return call.ArrayData; }, null) : null).length,
    total_true: toArray(totalTrue ? safe(function () { return totalTrue.ArrayData; }, null) : null).length,
    total_false: toArray(totalFalse ? safe(function () { return totalFalse.ArrayData; }, null) : null).length
  };
}

function boxInfo(comp) {
  return {
    true_false: toArray(safe(function () { return comp.GetBox(true, false); }, null)).length,
    false_false: toArray(safe(function () { return comp.GetBox(false, false); }, null)).length,
    false_one_arg: toArray(safe(function () { return comp.GetBox(false); }, null)).length
  };
}

function componentInfo(comp) {
  if (!comp) return null;
  return {
    name: safe(function () { return String(comp.Name2); }, ""),
    path: safe(function () { return String(comp.GetPathName()); }, ""),
    referenced_configuration: safe(function () { return String(comp.ReferencedConfiguration); }, ""),
    suppressed: safe(function () { return Boolean(comp.IsSuppressed()); }, null),
    suppression_code: safe(function () { return comp.GetSuppression(); }, ""),
    hidden: safe(function () { return Boolean(comp.IsHidden(true)); }, null),
    lightweight: safe(function () { return Boolean(comp.IsLightWeight()); }, null),
    children_count: toArray(safe(function () { return comp.GetChildren(); }, null)).length,
    transform_lengths: transformInfo(comp),
    box_lengths: boxInfo(comp)
  };
}

if (!sourcePath || !fso.FileExists(sourcePath)) {
  WScript.Echo("ERROR: source not found: " + sourcePath);
  WScript.Quit(2);
}

var sw = new ActiveXObject("SldWorks.Application");
sw.Visible = true;
var errors = 0;
var warnings = 0;
var doc = safe(function () { return sw.OpenDoc6(sourcePath, 2, 1, "", errors, warnings); }, null);
if (!doc) doc = safe(function () { return sw.OpenDoc(sourcePath, 2); }, null);
if (!doc) {
  WScript.Echo("ERROR: OpenDoc failed: " + sourcePath);
  WScript.Quit(4);
}

safe(function () { doc.ResolveAllLightWeightComponents(false); return true; }, false);
safe(function () { doc.ResolveAllLightWeightComponents(true); return true; }, false);
safe(function () { doc.EditRebuild3(); return true; }, false);
safe(function () { doc.ForceRebuild3(false); return true; }, false);

var cfg = safe(function () { return doc.ConfigurationManager.ActiveConfiguration; }, null);
var rootTrue = cfg ? safe(function () { return cfg.GetRootComponent3(true); }, null) : null;
var rootFalse = cfg ? safe(function () { return cfg.GetRootComponent3(false); }, null) : null;
var flatFalse = toArray(safe(function () { return doc.GetComponents(false); }, null));
var flatTrue = toArray(safe(function () { return doc.GetComponents(true); }, null));
var first = flatFalse.length ? flatFalse[0] : (flatTrue.length ? flatTrue[0] : null);

var result = {
  source_path: sourcePath,
  solidworks_revision: safe(function () { return String(sw.RevisionNumber()); }, ""),
  document_title: safe(function () { return String(doc.GetTitle()); }, ""),
  open_errors_code: errors,
  open_warnings_code: warnings,
  active_configuration: cfg ? safe(function () { return String(cfg.Name); }, "") : "",
  root_true_exists: rootTrue !== null,
  root_false_exists: rootFalse !== null,
  root_true: componentInfo(rootTrue),
  root_false: componentInfo(rootFalse),
  flat_false_count: flatFalse.length,
  flat_true_count: flatTrue.length,
  first_flat_component: componentInfo(first)
};

var text = stringify(result);
if (outPath) writeUtf8(outPath, text);
WScript.Echo(text);
safe(function () { sw.CloseDoc(doc.GetTitle()); }, null);
