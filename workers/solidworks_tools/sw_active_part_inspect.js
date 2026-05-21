var outJsonPath = WScript.Arguments(0);

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

function mm(v) {
  if (v === null || v === undefined) return null;
  return Math.round(v * 1000000) / 1000;
}

function getFeatures(model) {
  var rows = [];
  var feat = safe(function () { return model.FirstFeature(); }, null);
  var guard = 0;
  while (feat && guard < 2000) {
    guard++;
    rows.push({
      name: safe(function () { return String(feat.Name); }, ""),
      type: safe(function () { return String(feat.GetTypeName2()); }, "")
    });
    feat = safe(function () { return feat.GetNextFeature(); }, null);
  }
  return rows;
}

function hasFeature(features, namePattern, typePattern) {
  for (var i = 0; i < features.length; i++) {
    if (namePattern && namePattern.test(features[i].name)) return true;
    if (typePattern && typePattern.test(features[i].type)) return true;
  }
  return false;
}

function getDimensions(model) {
  var rows = [];
  var feat = safe(function () { return model.FirstFeature(); }, null);
  var guard = 0;
  while (feat && guard < 2000) {
    guard++;
    var fname = safe(function () { return String(feat.Name); }, "");
    var ftype = safe(function () { return String(feat.GetTypeName2()); }, "");
    var dd = safe(function () { return feat.GetFirstDisplayDimension(); }, null);
    var dguard = 0;
    while (dd && dguard < 200) {
      dguard++;
      var dim = safe(function () { return dd.GetDimension2(0); }, null);
      if (dim) {
        rows.push({
          feature: fname,
          feature_type: ftype,
          name: safe(function () { return String(dim.Name); }, ""),
          full_name: safe(function () { return String(dim.FullName); }, ""),
          value_mm: mm(safe(function () { return dim.SystemValue; }, null))
        });
      }
      dd = safe(function () { return feat.GetNextDisplayDimension(dd); }, null);
    }
    feat = safe(function () { return feat.GetNextFeature(); }, null);
  }
  return rows;
}

var sw = new ActiveXObject("SldWorks.Application");
var doc = safe(function () { return sw.ActiveDoc; }, null);
var result = { active: false };

if (doc) {
  var features = getFeatures(doc);
  result = {
    active: true,
    title: safe(function () { return String(doc.GetTitle()); }, ""),
    path: safe(function () { return String(doc.GetPathName()); }, ""),
    type: safe(function () { return doc.GetType(); }, null),
    dimensions: getDimensions(doc),
    has_sheet_metal: hasFeature(features, /^Sheet-Metal$/, /^SheetMetal$/),
    has_flat_pattern: hasFeature(features, /Flat-Pattern|FlatPattern/, /FlatPattern/),
    feature_count: features.length
  };
}

writeUtf8(outJsonPath, stringify(result));
WScript.Echo(outJsonPath);
