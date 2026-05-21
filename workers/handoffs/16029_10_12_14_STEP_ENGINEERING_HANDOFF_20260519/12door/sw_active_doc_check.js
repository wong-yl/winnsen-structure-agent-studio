var sw = new ActiveXObject("SldWorks.Application");
function safe(label, fn) {
  try { WScript.Echo(label + "=" + String(fn())); }
  catch (e) { WScript.Echo(label + " ERROR " + e.message); }
}
safe("GetDocumentCount", function(){ return sw.GetDocumentCount(); });
safe("ActiveDoc", function(){ return sw.ActiveDoc; });
var doc = null;
try { doc = sw.ActiveDoc; } catch(e) { doc = null; }
if (doc) {
  safe("Title", function(){ return doc.GetTitle(); });
  safe("Path", function(){ return doc.GetPathName(); });
  safe("Type", function(){ return doc.GetType(); });
} else {
  WScript.Echo("NoActiveDoc");
}
