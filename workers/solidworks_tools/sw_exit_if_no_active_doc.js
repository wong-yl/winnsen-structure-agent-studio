function safe(fn, fallback) {
  try { return fn(); } catch (e) { return fallback; }
}

var sw = new ActiveXObject("SldWorks.Application");
var doc = safe(function () { return sw.ActiveDoc; }, null);

if (doc) {
  WScript.Echo("kept_open_active_doc=" + safe(function () { return doc.GetTitle(); }, ""));
  WScript.Quit(0);
}

safe(function () { sw.ExitApp(); }, null);
WScript.Echo("closed_empty_solidworks_session");
