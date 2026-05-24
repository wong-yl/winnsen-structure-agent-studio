var inPath = WScript.Arguments(0);
var outPath = WScript.Arguments(1);
var sw = new ActiveXObject("SldWorks.Application");
try { sw.Visible = true; } catch (e) {}
var docType = inPath.toLowerCase().indexOf(".sldasm") >= 0 ? 2 : 1;
var doc = null;
var errors = 0;
var warnings = 0;
try { doc = sw.OpenDoc6(inPath, docType, 1, "", errors, warnings); } catch (e1) { doc = null; }
if (!doc) {
  try { doc = sw.OpenDoc(inPath, docType); } catch (e2) { doc = null; }
}
if (!doc) {
  WScript.Echo("OPEN_FAILED " + inPath);
  WScript.Quit(2);
}
var ok = false;
try { ok = doc.SaveAs(outPath); } catch (e3) { WScript.Echo("SAVE_EXCEPTION " + e3.message); }
WScript.Echo((ok ? "SAVE_OK " : "SAVE_FAIL ") + outPath);
try { sw.CloseDoc(doc.GetTitle()); } catch (e4) {}
