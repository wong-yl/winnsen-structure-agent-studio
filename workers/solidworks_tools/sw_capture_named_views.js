var fso = new ActiveXObject("Scripting.FileSystemObject");

var inPath = String(WScript.Arguments(0));
var outDir = String(WScript.Arguments(1));
var resultPath = String(WScript.Arguments(2));
var shouldClose = WScript.Arguments.length > 3 ? String(WScript.Arguments(3)).toLowerCase() !== "keepopen" : true;

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
  if (!path || fso.FolderExists(path)) return;
  var parent = fso.GetParentFolderName(path);
  if (parent && !fso.FolderExists(parent)) ensureFolder(parent);
  fso.CreateFolder(path);
}

function createSolidWorks() {
  var progIds = ["SldWorks.Application.28", "SldWorks.Application"];
  for (var i = 0; i < progIds.length; i++) {
    try { return { app: new ActiveXObject(progIds[i]), progId: progIds[i] }; } catch (e) {}
  }
  return { app: null, progId: "" };
}

function docTypeFor(path) {
  var ext = String(fso.GetExtensionName(path)).toLowerCase();
  if (ext === "sldasm") return 2;
  if (ext === "slddrw") return 3;
  return 1;
}

function openModel(sw, path, docType) {
  var errors = 0;
  var warnings = 0;
  var attempts = [];
  var doc = safe(function () { return sw.OpenDoc6(path, docType, 1, "", errors, warnings); }, null);
  attempts.push({ method: "OpenDoc6_silent", opened: !!doc });
  if (!doc) {
    doc = safe(function () { return sw.OpenDoc6(path, docType, 0, "", errors, warnings); }, null);
    attempts.push({ method: "OpenDoc6_interactive", opened: !!doc });
  }
  if (!doc) {
    doc = safe(function () { return sw.OpenDoc(path, docType); }, null);
    attempts.push({ method: "OpenDoc_legacy", opened: !!doc });
  }
  return { doc: doc, errors: errors, warnings: warnings, attempts: attempts };
}

ensureFolder(outDir);
ensureFolder(fso.GetParentFolderName(resultPath));

var views = [
  { label: "isometric", name: "*Isometric", id: 7 },
  { label: "front", name: "*Front", id: 1 },
  { label: "back", name: "*Back", id: 2 },
  { label: "top", name: "*Top", id: 5 },
  { label: "right", name: "*Right", id: 4 },
  { label: "left", name: "*Left", id: 3 }
];

var result = {
  input_path: inPath,
  out_dir: outDir,
  input_exists: fso.FileExists(inPath),
  prog_id_used: "",
  opened: false,
  open_errors: 0,
  open_warnings: 0,
  open_attempts: [],
  views: [],
  closed: false,
  error: ""
};

var created = createSolidWorks();
result.prog_id_used = created.progId;
var sw = created.app;

if (!sw) {
  result.error = "SolidWorks unavailable";
} else {
  safe(function () { sw.Visible = true; return true; }, false);
  safe(function () { sw.CloseAllDocuments(true); return true; }, false);
  var opened = openModel(sw, inPath, docTypeFor(inPath));
  var doc = opened.doc;
  result.opened = !!doc;
  result.open_errors = opened.errors;
  result.open_warnings = opened.warnings;
  result.open_attempts = opened.attempts;
  if (doc) {
    safe(function () { sw.ActivateDoc(doc.GetTitle()); return true; }, false);
    for (var i = 0; i < views.length; i++) {
      var v = views[i];
      var path = outDir + "\\" + v.label + ".png";
      var shown = safe(function () { doc.ShowNamedView2(v.name, v.id); return true; }, false);
      var zoomed = safe(function () { return !!doc.ViewZoomtofit2(); }, false);
      var saved = safe(function () { return !!doc.SaveAs(path); }, false);
      result.views.push({ label: v.label, named_view: v.name, path: path, shown: shown, zoomed: zoomed, saved: saved });
    }
    if (shouldClose) result.closed = safe(function () { sw.CloseDoc(doc.GetTitle()); return true; }, false);
  }
}

writeUtf8(resultPath, stringify(result));
WScript.Echo(resultPath);
WScript.Quit(result.opened ? 0 : 2);
