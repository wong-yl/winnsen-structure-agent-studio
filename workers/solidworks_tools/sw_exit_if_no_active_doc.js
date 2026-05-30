function safe(fn, fallback) {
  try { return fn(); } catch (e) { return fallback; }
}

function createSolidWorks() {
  var progIds = ["SldWorks.Application.28", "SldWorks.Application"];
  for (var i = 0; i < progIds.length; i++) {
    try { return { app: GetObject("", progIds[i]), progId: progIds[i] }; } catch (e) {}
  }
  return { app: null, progId: "" };
}

var created = createSolidWorks();
var sw = created.app;
var result = {
  prog_id_used: created.progId,
  connected: !!sw,
  has_active_doc: false,
  exited: false
};

if (sw) {
  var doc = safe(function () { return sw.ActiveDoc; }, null);
  result.has_active_doc = !!doc;
  if (!doc) {
    result.exited = safe(function () { sw.ExitApp(); return true; }, false);
  }
}

function esc(s) {
  return String(s).replace(/\\/g, "\\\\").replace(/"/g, "\\\"");
}

WScript.Echo("{\"prog_id_used\":\"" + esc(result.prog_id_used) + "\",\"connected\":" + (result.connected ? "true" : "false") + ",\"has_active_doc\":" + (result.has_active_doc ? "true" : "false") + ",\"exited\":" + (result.exited ? "true" : "false") + "}");
