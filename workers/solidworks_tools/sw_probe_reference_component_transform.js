var asmPath = String(WScript.Arguments(0));

function safe(label, fn) {
  try {
    var v = fn();
    var text = "";
    try { text = String(v); } catch (e0) { text = v ? "[object]" : ""; }
    WScript.Echo(label + "=" + text);
    return v;
  } catch (e) {
    WScript.Echo(label + " ERROR " + e.message);
    return null;
  }
}

function toArray(v) {
  var out = [];
  if (v === null || v === undefined) return out;
  try {
    return (new VBArray(v)).toArray();
  } catch (e00) {}
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

function transformText(xf) {
  if (!xf) return "";
  var raw = safe("xf.ArrayData", function () { return xf.ArrayData; });
  var data = toArray(raw);
  if (!data.length) {
    data = toArray(safe("xf.IArrayData", function () { return xf.IArrayData; }));
  }
  WScript.Echo("xf.array_len=" + data.length);
  return data.join(";");
}

var sw = new ActiveXObject("SldWorks.Application");
sw.Visible = true;
var doc = safe("OpenDoc", function () { return sw.OpenDoc(asmPath, 2); });
if (!doc) WScript.Quit(2);
safe("title", function () { return doc.GetTitle(); });
safe("resolve", function () { return doc.ResolveAllLightWeightComponents(false); });

var feat = safe("FirstFeature", function () { return doc.FirstFeature(); });
var idx = 0;
while (feat && idx < 200) {
  var name = safe("feat.Name", function () { return feat.Name; });
  var type = safe("feat.Type", function () { return feat.GetTypeName2(); });
  if (type === "Reference") {
    WScript.Echo("--- reference " + idx + " " + name + " ---");
    var spec = safe("specific", function () { return feat.GetSpecificFeature2(); });
    if (spec) {
      safe("spec.Name2", function () { return spec.Name2; });
      safe("spec.GetPathName", function () { return spec.GetPathName(); });
      safe("spec.Transform2", function () { return transformText(spec.Transform2); });
      safe("spec.GetTotalTransform", function () { return transformText(spec.GetTotalTransform(true)); });
      safe("spec.IsSuppressed", function () { return spec.IsSuppressed(); });
    }
  }
  feat = safe("next", function () { return feat.GetNextFeature(); });
  idx++;
}

safe("CloseDoc", function () { return sw.CloseDoc(doc.GetTitle()); });
