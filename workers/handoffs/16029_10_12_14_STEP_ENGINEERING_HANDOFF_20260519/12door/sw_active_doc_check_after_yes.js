var sw = new ActiveXObject("SldWorks.Application");
WScript.Echo("GetDocumentCount=" + sw.GetDocumentCount());
var doc = null;
try { doc = sw.ActiveDoc; } catch(e) { doc = null; }
if (doc) {
  WScript.Echo("Title=" + doc.GetTitle());
  WScript.Echo("Path=" + doc.GetPathName());
  WScript.Echo("Type=" + doc.GetType());
} else {
  WScript.Echo("NoActiveDoc");
}
