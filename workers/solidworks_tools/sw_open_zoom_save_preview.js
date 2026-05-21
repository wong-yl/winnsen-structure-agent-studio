var fso = new ActiveXObject("Scripting.FileSystemObject");

var inPath = String(WScript.Arguments(0));
var previewPath = String(WScript.Arguments(1));
var shouldClose = WScript.Arguments.length > 2 ? String(WScript.Arguments(2)).toLowerCase() !== "keepopen" : true;

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

function ensureFolder(path) {
  if (fso.FolderExists(path)) return;
  var parent = fso.GetParentFolderName(path);
  if (parent && !fso.FolderExists(parent)) ensureFolder(parent);
  fso.CreateFolder(path);
}

function docTypeFor(path) {
  var ext = String(fso.GetExtensionName(path)).toLowerCase();
  if (ext === "sldasm") return 2;
  if (ext === "slddrw") return 3;
  return 1;
}

var result = {
  input_path: inPath,
  preview_path: previewPath,
  input_exists: fso.FileExists(inPath),
  opened: false,
  zoomed: false,
  preview_saved: false,
  closed: false
};

ensureFolder(fso.GetParentFolderName(previewPath));
var sw = new ActiveXObject("SldWorks.Application");
safe(function () { sw.Visible = true; }, null);
var doc = safe(function () { return sw.OpenDoc(inPath, docTypeFor(inPath)); }, null);
result.opened = !!doc;

if (doc) {
  safe(function () { sw.ActivateDoc(doc.GetTitle()); return true; }, false);
  safe(function () { doc.ShowNamedView2("*Isometric", 7); return true; }, false);
  result.zoomed = safe(function () { return doc.ViewZoomtofit2(); }, false);
  result.preview_saved = safe(function () { return doc.SaveAs(previewPath); }, false);
  if (shouldClose) {
    result.closed = safe(function () { sw.CloseDoc(doc.GetTitle()); return true; }, false);
  }
}

writeUtf8(previewPath + ".json", stringify(result));
WScript.Echo(previewPath + ".json");
WScript.Quit(result.opened ? 0 : 2);
