var fso = new ActiveXObject("Scripting.FileSystemObject");

function safe(fn, fallback) {
  try { return fn(); } catch (e) { return fallback; }
}

function toArray(v) {
  var out = [];
  if (v === null || v === undefined) return out;
  try {
    var a = new VBArray(v);
    for (var i = a.lbound(); i <= a.ubound(); i++) out.push(a.getItem(i));
    return out;
  } catch (e0) {}
  try {
    for (var j = 0; j < v.length; j++) out.push(v[j]);
    return out;
  } catch (e1) {}
  return out;
}

function mm(v) {
  if (v === null || v === undefined || v === "") return "";
  var n = parseFloat(v);
  if (isNaN(n)) return "";
  return Math.round(n * 1000000.0) / 1000.0;
}

function escCsv(s) {
  s = String(s === null || s === undefined ? "" : s);
  if (/[",\r\n]/.test(s)) return "\"" + s.replace(/"/g, "\"\"") + "\"";
  return s;
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

if (WScript.Arguments.length < 2) {
  WScript.Echo("usage: cscript sw_measure_part_bboxes.js out.csv part1.SLDPRT [part2.SLDPRT...]");
  WScript.Quit(2);
}

var outCsv = String(WScript.Arguments(0));
var shell = new ActiveXObject("WScript.Shell");
var maxPartsText = String(shell.Environment("PROCESS")("WINNSEN_SW_MEASURE_MAX_PARTS") || "1");
var maxParts = parseInt(maxPartsText, 10);
if (isNaN(maxParts) || maxParts < 1) maxParts = 1;
if ((WScript.Arguments.length - 1) > maxParts) {
  WScript.Echo("ERROR: refused to open " + (WScript.Arguments.length - 1) + " parts; limit is " + maxParts + ". Set WINNSEN_SW_MEASURE_MAX_PARTS only when intentionally batching.");
  WScript.Quit(4);
}

var sw = new ActiveXObject("SldWorks.Application");
sw.Visible = true;
var asm = sw.NewAssembly();
if (!asm) {
  WScript.Echo("ERROR: NewAssembly failed");
  WScript.Quit(3);
}
var asmTitle = safe(function () { return String(asm.GetTitle()); }, "");
var openedTitles = [];

var rows = [];
for (var i = 1; i < WScript.Arguments.length; i++) {
  var path = String(WScript.Arguments(i));
  var row = {
    path: path,
    name: fso.GetFileName(path),
    exists: fso.FileExists(path),
    opened: false,
    added: false,
    minX: "", minY: "", minZ: "",
    maxX: "", maxY: "", maxZ: "",
    sizeX: "", sizeY: "", sizeZ: "",
    error: ""
  };
  if (!row.exists) {
    row.error = "missing";
    rows.push(row);
    continue;
  }
  var doc = safe(function () { return sw.OpenDoc(path, 1); }, null);
  if (!doc) {
    row.error = "OpenDoc failed";
    rows.push(row);
    continue;
  }
  row.opened = true;
  var docTitle = safe(function () { return String(doc.GetTitle()); }, "");
  if (docTitle) openedTitles.push(docTitle);
  safe(function () { sw.ActivateDoc(asm.GetTitle()); return true; }, false);
  var comp = safe(function () {
    return asm.AddComponent5(path, 0, "", false, "", 0, 0, 0);
  }, null);
  if (!comp) {
    row.error = "AddComponent5 failed";
    rows.push(row);
    safe(function () { sw.CloseDoc(doc.GetTitle()); return true; }, false);
    continue;
  }
  row.added = true;
  var raw = safe(function () { return comp.GetBox(true, false); }, null);
  if (!raw) raw = safe(function () { return comp.GetBox(false, false); }, null);
  var vals = toArray(raw);
  if (vals.length >= 6) {
    row.minX = mm(vals[0]);
    row.minY = mm(vals[1]);
    row.minZ = mm(vals[2]);
    row.maxX = mm(vals[3]);
    row.maxY = mm(vals[4]);
    row.maxZ = mm(vals[5]);
    row.sizeX = mm(vals[3] - vals[0]);
    row.sizeY = mm(vals[4] - vals[1]);
    row.sizeZ = mm(vals[5] - vals[2]);
  } else {
    row.error = "no bbox";
  }
  rows.push(row);
  if (docTitle) safe(function () { sw.CloseDoc(docTitle); return true; }, false);
}

var csv = "name,path,exists,opened,added,minX,minY,minZ,maxX,maxY,maxZ,sizeX,sizeY,sizeZ,error\r\n";
for (var r = 0; r < rows.length; r++) {
  var x = rows[r];
  var fields = [
    x.name, x.path, x.exists, x.opened, x.added,
    x.minX, x.minY, x.minZ, x.maxX, x.maxY, x.maxZ,
    x.sizeX, x.sizeY, x.sizeZ, x.error
  ];
  var escaped = [];
  for (var c = 0; c < fields.length; c++) escaped.push(escCsv(fields[c]));
  csv += escaped.join(",") + "\r\n";
}
writeUtf8(outCsv, csv);
for (var t = 0; t < openedTitles.length; t++) {
  safe(function () { sw.CloseDoc(openedTitles[t]); return true; }, false);
}
if (asmTitle) safe(function () { sw.CloseDoc(asmTitle); return true; }, false);
WScript.Echo(outCsv);
