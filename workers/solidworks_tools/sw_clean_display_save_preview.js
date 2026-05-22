var fso = new ActiveXObject("Scripting.FileSystemObject");

var previewPath = WScript.Arguments.length > 0 ? String(WScript.Arguments(0)) : "";
var shouldClose = WScript.Arguments.length > 1 ? String(WScript.Arguments(1)).toLowerCase() === "close" : false;
var viewName = WScript.Arguments.length > 2 ? String(WScript.Arguments(2)) : "*Isometric";

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

function setToggle(sw, id, value) {
  return safe(function () { sw.SetUserPreferenceToggle(id, value); return true; }, false);
}

function setDocToggle(doc, id, value) {
  return safe(function () { return doc.SetUserPreferenceToggle(id, value); }, false);
}

function viewIdFor(name) {
  var n = String(name).toLowerCase();
  if (n === "*front") return 1;
  if (n === "*back") return 2;
  if (n === "*left") return 3;
  if (n === "*right") return 4;
  if (n === "*top") return 5;
  if (n === "*bottom") return 6;
  return 7;
}

var toggles = [
  { id: 198, name: "swViewDisplayHideAllTypes", value: true },
  { id: 196, name: "swDisplaySketches", value: false },
  { id: 195, name: "swDisplayCurves", value: false },
  { id: 5, name: "swDisplayPlanes", value: false },
  { id: 4, name: "swDisplayAxes", value: false },
  { id: 7, name: "swDisplayTemporaryAxes", value: false },
  { id: 6, name: "swDisplayOrigins", value: false },
  { id: 13, name: "swDisplayCoordSystems", value: false },
  { id: 19, name: "swDisplayReferencePoints", value: false },
  { id: 31, name: "swDisplayAnnotations", value: false },
  { id: 197, name: "swDisplayAllAnnotations", value: false }
];

var result = {
  active: false,
  title: "",
  path: "",
  preview_path: previewPath,
  view_name: viewName,
  toggles: [],
  doc_toggles: [],
  zoomed: false,
  preview_saved: false,
  closed: false
};

var sw = new ActiveXObject("SldWorks.Application");
safe(function () { sw.Visible = true; }, null);
var doc = safe(function () { return sw.ActiveDoc; }, null);

if (doc) {
  result.active = true;
  result.title = safe(function () { return String(doc.GetTitle()); }, "");
  result.path = safe(function () { return String(doc.GetPathName()); }, "");
  for (var i = 0; i < toggles.length; i++) {
    var toggle = toggles[i];
    result.toggles.push({ name: toggle.name, ok: setToggle(sw, toggle.id, toggle.value), value: toggle.value });
    result.doc_toggles.push({ name: toggle.name, ok: setDocToggle(doc, toggle.id, toggle.value), value: toggle.value });
  }
  safe(function () { doc.ShowNamedView2(viewName, viewIdFor(viewName)); return true; }, false);
  result.zoomed = safe(function () { return doc.ViewZoomtofit2(); }, false);
  safe(function () { doc.GraphicsRedraw2(); return true; }, false);
  if (previewPath) {
    ensureFolder(fso.GetParentFolderName(previewPath));
    result.preview_saved = safe(function () { return doc.SaveAs(previewPath); }, false);
  }
  if (shouldClose) {
    result.closed = safe(function () { sw.CloseDoc(result.title); return true; }, false);
  }
}

if (previewPath) {
  writeUtf8(previewPath + ".json", stringify(result));
  WScript.Echo(previewPath + ".json");
} else {
  WScript.Echo(stringify(result));
}
WScript.Quit(result.active ? 0 : 2);
