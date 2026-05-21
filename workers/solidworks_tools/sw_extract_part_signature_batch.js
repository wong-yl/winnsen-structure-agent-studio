var outJsonPath = String(WScript.Arguments(0));
var partPaths = [];
for (var ai = 1; ai < WScript.Arguments.length; ai++) partPaths.push(String(WScript.Arguments(ai)));

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

function toArray(v) {
  var out = [];
  if (v === null || v === undefined) return out;
  try {
    var a = new VBArray(v);
    for (var i = a.lbound(); i <= a.ubound(); i++) out.push(a.getItem(i));
    return out;
  } catch (e) {}
  try {
    for (var j = 0; j < v.length; j++) out.push(v[j]);
  } catch (e2) {}
  return out;
}

function mm(v) {
  if (v === null || v === undefined || v === "") return null;
  return Math.round(parseFloat(v) * 1000000) / 1000;
}

function getFeatures(model) {
  var rows = [];
  var feat = safe(function () { return model.FirstFeature(); }, null);
  var guard = 0;
  while (feat && guard < 3000) {
    guard++;
    rows.push({
      name: safe(function () { return String(feat.Name); }, ""),
      type: safe(function () { return String(feat.GetTypeName2()); }, ""),
      suppressed: safe(function () { return feat.IsSuppressed(); }, null)
    });
    feat = safe(function () { return feat.GetNextFeature(); }, null);
  }
  return rows;
}

function getDimensions(model) {
  var rows = [];
  var feat = safe(function () { return model.FirstFeature(); }, null);
  var guard = 0;
  while (feat && guard < 3000) {
    guard++;
    var fname = safe(function () { return String(feat.Name); }, "");
    var ftype = safe(function () { return String(feat.GetTypeName2()); }, "");
    var dd = safe(function () { return feat.GetFirstDisplayDimension(); }, null);
    var dguard = 0;
    while (dd && dguard < 300) {
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

function getBoxData(doc) {
  var raw = safe(function () { return doc.GetPartBox(true); }, null);
  if (!raw) raw = safe(function () { return doc.GetPartBox(false); }, null);
  if (!raw) raw = safe(function () { return doc.IGetPartBox(true); }, null);
  var vals = toArray(raw);
  if (vals.length < 6) return null;
  return {
    x_min_mm: mm(vals[0]),
    y_min_mm: mm(vals[1]),
    z_min_mm: mm(vals[2]),
    x_max_mm: mm(vals[3]),
    y_max_mm: mm(vals[4]),
    z_max_mm: mm(vals[5]),
    x_len_mm: mm(vals[3] - vals[0]),
    y_len_mm: mm(vals[4] - vals[1]),
    z_len_mm: mm(vals[5] - vals[2])
  };
}

function hasType(features, typeName) {
  for (var i = 0; i < features.length; i++) if (features[i].type === typeName) return true;
  return false;
}

var fso = new ActiveXObject("Scripting.FileSystemObject");
var sw = new ActiveXObject("SldWorks.Application");
safe(function () { sw.Visible = true; }, null);

function timestampText() {
  var d = new Date();
  function pad(n) { return n < 10 ? "0" + n : String(n); }
  return d.getFullYear() + "-" + pad(d.getMonth() + 1) + "-" + pad(d.getDate()) +
    " " + pad(d.getHours()) + ":" + pad(d.getMinutes()) + ":" + pad(d.getSeconds());
}

var result = {
  generatedAt: timestampText(),
  count: partPaths.length,
  parts: []
};

for (var pi = 0; pi < partPaths.length; pi++) {
  var path = partPaths[pi];
  var item = {
    path: path,
    exists: fso.FileExists(path),
    opened: false,
    feature_count: 0,
    dimension_count: 0,
    has_sheet_metal: false,
    has_flat_pattern: false,
    bbox_mm: null,
    features: [],
    dimensions: []
  };
  if (item.exists) {
    var doc = safe(function () { return sw.OpenDoc(path, 1); }, null);
    if (!doc) doc = safe(function () { return sw.OpenDoc6(path, 1, 1, "", 0, 0); }, null);
    item.opened = !!doc;
    if (doc) {
      item.title = safe(function () { return String(doc.GetTitle()); }, "");
      item.type = safe(function () { return doc.GetType(); }, null);
      item.bbox_mm = getBoxData(doc);
      item.features = getFeatures(doc);
      item.dimensions = getDimensions(doc);
      item.feature_count = item.features.length;
      item.dimension_count = item.dimensions.length;
      item.has_sheet_metal = hasType(item.features, "SheetMetal");
      item.has_flat_pattern = hasType(item.features, "FlatPattern");
      safe(function () { sw.CloseDoc(doc.GetTitle()); }, null);
    } else {
      item.error = "open_failed";
    }
  } else {
    item.error = "missing_file";
  }
  result.parts.push(item);
}

writeUtf8(outJsonPath, stringify(result));
WScript.Echo(outJsonPath);
