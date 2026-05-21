var outJsonPath = String(WScript.Arguments(0));
var expectedAsmPath = WScript.Arguments.length > 1 ? String(WScript.Arguments(1)).toLowerCase() : "";

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

function safe(fn, fallback) {
  try { return fn(); } catch (e) { return fallback; }
}

function collectReferences(doc) {
  var rows = [];
  var feat = safe(function () { return doc.FirstFeature(); }, null);
  var guard = 0;
  while (feat && guard < 3000) {
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

var sw = new ActiveXObject("SldWorks.Application");
var doc = safe(function () { return sw.ActiveDoc; }, null);
var result = { active: false, expectedAsmPath: expectedAsmPath, pathMatches: false, references: [] };

if (doc) {
  var path = safe(function () { return String(doc.GetPathName()); }, "");
  result.active = true;
  result.title = safe(function () { return String(doc.GetTitle()); }, "");
  result.path = path;
  result.type = safe(function () { return doc.GetType(); }, null);
  result.pathMatches = expectedAsmPath ? path.toLowerCase() === expectedAsmPath : true;
  result.reference_count = 0;
  result.references = collectReferences(doc);
  result.reference_count = result.references.length;
  result.rebuilt = safe(function () { return doc.ForceRebuild3(false); }, false);
}

writeUtf8(outJsonPath, stringify(result));
WScript.Echo(outJsonPath);
