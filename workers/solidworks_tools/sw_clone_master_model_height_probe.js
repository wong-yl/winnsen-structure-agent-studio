var templatePath = String(WScript.Arguments(0));
var outPartPath = String(WScript.Arguments(1));
var outStepPath = String(WScript.Arguments(2));
var outJsonPath = String(WScript.Arguments(3));

var dimSpecs = [];
var manualSuppressFeatureNames = [];
for (var ai = 4; ai < WScript.Arguments.length;) {
  var token = String(WScript.Arguments(ai));
  if (token === "--suppress-feature") {
    if (ai + 1 < WScript.Arguments.length) {
      manualSuppressFeatureNames.push(String(WScript.Arguments(ai + 1)));
    }
    ai += 2;
    continue;
  }
  if (ai + 2 >= WScript.Arguments.length) break;
  dimSpecs.push({
    feature: token,
    dim: String(WScript.Arguments(ai + 1)),
    value_mm: parseFloat(WScript.Arguments(ai + 2))
  });
  ai += 3;
}

var fso = new ActiveXObject("Scripting.FileSystemObject");

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
  if (!path || fso.FolderExists(path)) return;
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
    var vb = new VBArray(v).toArray();
    for (var i = 0; i < vb.length; i++) out.push(vb[i]);
    return out;
  } catch (e0) {}
  try {
    for (var j = 0; j < v.length; j++) out.push(v[j]);
  } catch (e1) {}
  return out;
}

function mm(v) {
  if (v === null || v === undefined || v === "") return null;
  return Math.round(parseFloat(v) * 1000000) / 1000;
}

function boxFromMeters(vals) {
  if (!vals || vals.length < 6) return null;
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

function getPartBox(doc) {
  var raw = safe(function () { return doc.GetPartBox(true); }, null);
  if (!raw) raw = safe(function () { return doc.GetPartBox(false); }, null);
  return boxFromMeters(toArray(raw));
}

function setDimension(model, targetFeature, targetDim, newValueMm) {
  var feat = safe(function () { return model.FirstFeature(); }, null);
  var guard = 0;
  while (feat && guard < 5000) {
    guard++;
    var fname = safe(function () { return String(feat.Name); }, "");
    if (fname === targetFeature) {
      var dd = safe(function () { return feat.GetFirstDisplayDimension(); }, null);
      var dguard = 0;
      while (dd && dguard < 500) {
        dguard++;
        var dim = safe(function () { return dd.GetDimension2(0); }, null);
        if (dim && safe(function () { return String(dim.Name); }, "") === targetDim) {
          var before = mm(safe(function () { return dim.SystemValue; }, null));
          dim.SystemValue = newValueMm / 1000.0;
          return { ok: true, before_mm: before, after_requested_mm: newValueMm };
        }
        dd = safe(function () { return feat.GetNextDisplayDimension(dd); }, null);
      }
    }
    feat = safe(function () { return feat.GetNextFeature(); }, null);
  }
  return { ok: false, before_mm: null, after_requested_mm: newValueMm };
}

function getDimensions(model) {
  var rows = [];
  var feat = safe(function () { return model.FirstFeature(); }, null);
  var guard = 0;
  while (feat && guard < 5000) {
    guard++;
    var fname = safe(function () { return String(feat.Name); }, "");
    var ftype = safe(function () { return String(feat.GetTypeName2()); }, "");
    var dd = safe(function () { return feat.GetFirstDisplayDimension(); }, null);
    var dguard = 0;
    while (dd && dguard < 500) {
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

function collectOutputBlockerFeatureNames(model) {
  var names = [];
  var feat = safe(function () { return model.FirstFeature(); }, null);
  var guard = 0;
  while (feat && guard < 5000) {
    guard++;
    var type = safe(function () { return String(feat.GetTypeName2()); }, "");
    if (type === "CreateAssemFeat" || type === "DeleteBody") names.push(safe(function () { return String(feat.Name); }, ""));
    feat = safe(function () { return feat.GetNextFeature(); }, null);
  }
  return names;
}

function suppressFeatures(model, names) {
  var rows = [];
  for (var i = 0; i < names.length; i++) {
    var name = names[i];
    var feat = safe(function () { return model.FeatureByName(name); }, null);
    safe(function () { model.ClearSelection2(true); return true; }, false);
    var selected = feat ? safe(function () { return feat.Select2(false, 0); }, false) : false;
    var suppressed = selected ? safe(function () { return model.EditSuppress2(); }, false) : false;
    rows.push({ feature: name, selected: selected, edit_suppress_ok: suppressed });
  }
  safe(function () { model.ClearSelection2(true); return true; }, false);
  return rows;
}

function bodyRows(doc) {
  var part = doc;
  var bodies = toArray(safe(function () { return part.GetBodies2(0, false); }, null));
  if (!bodies.length) bodies = toArray(safe(function () { return part.GetBodies2(0, true); }, null));
  var rows = [];
  for (var i = 0; i < bodies.length; i++) {
    var body = bodies[i];
    var vals = toArray(safe(function () { return body.GetBodyBox(); }, null));
    rows.push({
      index: i,
      name: safe(function () { return String(body.Name); }, ""),
      visible: safe(function () { return body.Visible; }, null),
      bbox_mm: boxFromMeters(vals)
    });
  }
  return rows;
}

function featureSuppressed(feat) {
  var direct = safe(function () { return feat.IsSuppressed(); }, null);
  if (direct !== null && direct !== undefined) return !!direct;
  var raw = safe(function () { return feat.IsSuppressed2(0, null); }, null);
  var values = toArray(raw);
  if (values.length) return !!values[0];
  if (raw !== null && raw !== undefined) return !!raw;
  return null;
}

function featureRows(model) {
  var rows = [];
  var feat = safe(function () { return model.FirstFeature(); }, null);
  var guard = 0;
  while (feat && guard < 5000) {
    guard++;
    rows.push({
      name: safe(function () { return String(feat.Name); }, ""),
      type: safe(function () { return String(feat.GetTypeName2()); }, ""),
      is_suppressed: featureSuppressed(feat)
    });
    feat = safe(function () { return feat.GetNextFeature(); }, null);
  }
  return rows;
}

var result = {
  templatePath: templatePath,
  outPartPath: outPartPath,
  outStepPath: outStepPath,
  outJsonPath: outJsonPath,
  dimSpecs: dimSpecs,
  manualSuppressFeatureNames: manualSuppressFeatureNames,
  copied: false,
  opened: false,
  outputBlockerFeatures: [],
  suppressedOutputBlockerFeatures: [],
  manuallySuppressedFeatures: [],
  dimensionResults: [],
  rebuilt: false,
  savedPart: false,
  savedStep: false,
  partBoxMm: null,
  features: [],
  bodies: [],
  dimensions: []
};

if (!fso.FileExists(templatePath)) {
  result.error = "template missing";
  writeUtf8(outJsonPath, stringify(result));
  WScript.Echo(outJsonPath);
  WScript.Quit(2);
}

ensureFolder(fso.GetParentFolderName(outPartPath));
ensureFolder(fso.GetParentFolderName(outStepPath));
ensureFolder(fso.GetParentFolderName(outJsonPath));
if (fso.FileExists(outPartPath)) fso.DeleteFile(outPartPath, true);
if (fso.FileExists(outStepPath)) fso.DeleteFile(outStepPath, true);
fso.CopyFile(templatePath, outPartPath, true);
result.copied = true;

var sw = new ActiveXObject("SldWorks.Application");
safe(function () { sw.Visible = true; }, null);
var errors = 0;
var warnings = 0;
var doc = safe(function () { return sw.OpenDoc6(outPartPath, 1, 1, "", errors, warnings); }, null);
if (!doc) doc = safe(function () { return sw.OpenDoc(outPartPath, 1); }, null);
result.opened = !!doc;
result.openErrors = errors;
result.openWarnings = warnings;

if (!doc) {
  result.error = "open copied master failed";
  writeUtf8(outJsonPath, stringify(result));
  WScript.Echo(outJsonPath);
  WScript.Quit(3);
}

result.outputBlockerFeatures = collectOutputBlockerFeatureNames(doc);
result.suppressedOutputBlockerFeatures = suppressFeatures(doc, result.outputBlockerFeatures);
result.manuallySuppressedFeatures = suppressFeatures(doc, manualSuppressFeatureNames);

for (var di = 0; di < dimSpecs.length; di++) {
  var spec = dimSpecs[di];
  var set = setDimension(doc, spec.feature, spec.dim, spec.value_mm);
  result.dimensionResults.push({
    feature: spec.feature,
    dim: spec.dim,
    requested_mm: spec.value_mm,
    ok: set.ok,
    before_mm: set.before_mm
  });
}

result.rebuilt = safe(function () { return doc.ForceRebuild3(false); }, false);
result.savedPart = safe(function () { return doc.SaveAs(outPartPath); }, false);
result.partBoxMm = getPartBox(doc);
result.features = featureRows(doc);
result.bodies = bodyRows(doc);
result.dimensions = getDimensions(doc);
result.savedStep = safe(function () { return doc.SaveAs(outStepPath); }, false);
safe(function () { sw.CloseDoc(doc.GetTitle()); return true; }, false);

writeUtf8(outJsonPath, stringify(result));
WScript.Echo(outJsonPath);
WScript.Quit(result.rebuilt && result.savedPart ? 0 : 4);
