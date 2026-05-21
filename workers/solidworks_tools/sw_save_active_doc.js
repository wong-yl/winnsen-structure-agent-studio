var targetPath = WScript.Arguments.length > 0 ? String(WScript.Arguments(0)).toLowerCase() : "";

function safe(fn, fallback) {
  try { return fn(); } catch (e) { return fallback; }
}

var sw = new ActiveXObject("SldWorks.Application");
var doc = safe(function () { return sw.ActiveDoc; }, null);

if (!doc) {
  WScript.Echo("no_active_doc");
  WScript.Quit(0);
}

var activePath = safe(function () { return String(doc.GetPathName()).toLowerCase(); }, "");
var title = safe(function () { return String(doc.GetTitle()); }, "");

if (targetPath && activePath !== targetPath) {
  WScript.Echo("active_doc_mismatch=" + activePath);
  WScript.Quit(2);
}

var saved = safe(function () { return doc.Save(); }, false);
WScript.Echo("saved=" + title + "; result=" + saved);
