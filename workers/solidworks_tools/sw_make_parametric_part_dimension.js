var fso = new ActiveXObject("Scripting.FileSystemObject");

var templatePath = String(WScript.Arguments(0));
var outPartPath = String(WScript.Arguments(1));
var outStepPath = String(WScript.Arguments(2));
var featureName = String(WScript.Arguments(3));
var dimName = String(WScript.Arguments(4));
var valueMm = parseFloat(WScript.Arguments(5));
var outJsonPath = String(WScript.Arguments(6));

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
      type: safe(function () { return String(feat.GetTypeName2()); }, "")
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

function setDimension(model, targetFeature, targetDim, newValueMm) {
  var feat = safe(function () { return model.FirstFeature(); }, null);
  var guard = 0;
  while (feat && guard < 3000) {
    guard++;
    var fname = safe(function () { return String(feat.Name); }, "");
    if (fname === targetFeature) {
      var dd = safe(function () { return feat.GetFirstDisplayDimension(); }, null);
      var dguard = 0;
      while (dd && dguard < 300) {
        dguard++;
        var dim = safe(function () { return dd.GetDimension2(0); }, null);
        if (dim) {
          var name = safe(function () { return String(dim.Name); }, "");
          if (name === targetDim) {
            dim.SystemValue = newValueMm / 1000.0;
            return true;
          }
        }
        dd = safe(function () { return feat.GetNextDisplayDimension(dd); }, null);
      }
    }
    feat = safe(function () { return feat.GetNextFeature(); }, null);
  }
  return false;
}

var result = {
  templatePath: templatePath,
  outPartPath: outPartPath,
  outStepPath: outStepPath,
  featureName: featureName,
  dimName: dimName,
  requestedValueMm: valueMm,
  copied: false,
  opened: false,
  dimensionSet: false,
  rebuilt: false,
  partSaved: false,
  stepSaved: false,
  features: [],
  dimensions: []
};

if (!fso.FileExists(templatePath)) {
  result.error = "template missing";
  writeUtf8(outJsonPath, stringify(result));
  WScript.Echo(outJsonPath);
  WScript.Quit(2);
}

if (fso.FileExists(outPartPath)) fso.DeleteFile(outPartPath, true);
fso.CopyFile(templatePath, outPartPath, true);
result.copied = true;

var sw = new ActiveXObject("SldWorks.Application");
safe(function () { sw.Visible = true; }, null);
var doc = safe(function () { return sw.OpenDoc(outPartPath, 1); }, null);
if (!doc) {
  result.error = "open copied part failed";
  writeUtf8(outJsonPath, stringify(result));
  WScript.Echo(outJsonPath);
  WScript.Quit(3);
}
result.opened = true;

result.dimensionSet = setDimension(doc, featureName, dimName, valueMm);
result.rebuilt = safe(function () { return doc.ForceRebuild3(false); }, false);
result.partSaved = safe(function () { return doc.SaveAs(outPartPath); }, false);
result.features = getFeatures(doc);
result.dimensions = getDimensions(doc);
result.stepSaved = safe(function () { return doc.SaveAs(outStepPath); }, false);
safe(function () { sw.CloseDoc(doc.GetTitle()); }, null);

writeUtf8(outJsonPath, stringify(result));
WScript.Echo(outJsonPath);
