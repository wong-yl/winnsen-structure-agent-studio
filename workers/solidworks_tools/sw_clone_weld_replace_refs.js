var fso = new ActiveXObject("Scripting.FileSystemObject");

var templateAsmPath = String(WScript.Arguments(0));
var outAsmPath = String(WScript.Arguments(1));
var oldPanelPath = String(WScript.Arguments(2));
var newPanelPath = String(WScript.Arguments(3));
var oldStiffenerPath = String(WScript.Arguments(4));
var newStiffenerPath = String(WScript.Arguments(5));
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

var result = {
  templateAsmPath: templateAsmPath,
  outAsmPath: outAsmPath,
  oldPanelPath: oldPanelPath,
  newPanelPath: newPanelPath,
  oldStiffenerPath: oldStiffenerPath,
  newStiffenerPath: newStiffenerPath,
  copiedAssembly: false,
  replacePanel: false,
  replaceStiffener: false,
  opened: false,
  rebuilt: false,
  saved: false,
  references: []
};

if (!fso.FileExists(templateAsmPath)) {
  result.error = "template assembly missing";
  writeUtf8(outJsonPath, stringify(result));
  WScript.Echo(outJsonPath);
  WScript.Quit(2);
}
if (!fso.FileExists(newPanelPath) || !fso.FileExists(newStiffenerPath)) {
  result.error = "new replacement part missing";
  writeUtf8(outJsonPath, stringify(result));
  WScript.Echo(outJsonPath);
  WScript.Quit(3);
}

ensureFolder(fso.GetParentFolderName(outAsmPath));
if (fso.FileExists(outAsmPath)) fso.DeleteFile(outAsmPath, true);
fso.CopyFile(templateAsmPath, outAsmPath, true);
result.copiedAssembly = true;

var sw = new ActiveXObject("SldWorks.Application");
safe(function () { sw.Visible = true; }, null);
safe(function () { sw.CloseAllDocuments(true); return true; }, false);

result.replacePanel = safe(function () {
  return sw.ReplaceReferencedDocument(outAsmPath, oldPanelPath, newPanelPath);
}, false);
result.replaceStiffener = safe(function () {
  return sw.ReplaceReferencedDocument(outAsmPath, oldStiffenerPath, newStiffenerPath);
}, false);

var doc = safe(function () { return sw.OpenDoc(outAsmPath, 2); }, null);
if (!doc) doc = safe(function () { return sw.OpenDoc6(outAsmPath, 2, 1, "", 0, 0); }, null);
result.opened = !!doc;

if (doc) {
  result.rebuilt = safe(function () { return doc.ForceRebuild3(false); }, false);
  result.references = collectReferences(doc);
  result.saved = safe(function () { return doc.SaveAs(outAsmPath); }, false);
  safe(function () { sw.CloseDoc(doc.GetTitle()); }, null);
}

writeUtf8(outJsonPath, stringify(result));
WScript.Echo(outJsonPath);
