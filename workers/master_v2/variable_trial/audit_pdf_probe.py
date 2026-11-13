"""Diagnostic-only, offline PDF probe audit; may later join the formal audit.

Read a terminated run without starting CAD. Write only a new analysis directory
inside TRIAL_ROOT, outside runs/CAD inputs. Text extraction is not visual review
or content/production acceptance. Existing pdfplumber/pypdfium2 are required.
"""

import argparse
import hashlib
import importlib.metadata
import io
import json
import os
import re
import stat
import sys
from datetime import datetime, timezone
from pathlib import Path

import pdfplumber
import pypdfium2


TRIAL_ROOT = Path(r"D:\Winnsen_Structure_Agent_Studio\data\master_v2\variable_trial_20261112")
RUNS_ROOT = TRIAL_ROOT / "runs"
CJK = re.compile(r"[\u3400-\u4dbf\u4e00-\u9fff\uf900-\ufaff\U00020000-\U000323af]")
COUNTS = ("chars", "lines", "curves", "rects")


class AuditError(ValueError):
    pass


def require(condition, message):
    if not condition:
        raise AuditError(message)


def sha256(data):
    return hashlib.sha256(data).hexdigest().upper()


def checked_path(value):
    path = Path(value)
    require(path.is_absolute() and ".." not in path.parts, "ABSOLUTE_PATH_WITHOUT_PARENT_TRAVERSAL_REQUIRED")
    devices = {"CON", "PRN", "AUX", "NUL"} | {f"{p}{n}" for p in ("COM", "LPT") for n in range(1, 10)}
    for part in path.parts[1:]:
        require(part == part.rstrip(" .") and not any(c in part for c in '<>:"|?*'), "AMBIGUOUS_WINDOWS_PATH")
        require(part.split(".")[0].upper() not in devices, "WINDOWS_DEVICE_PATH")
    for item in reversed([path, *path.parents]):
        try:
            info = item.lstat()
        except FileNotFoundError:
            continue
        require(not stat.S_ISLNK(info.st_mode) and not (getattr(info, "st_file_attributes", 0) & 0x400),
                f"REPARSE_PATH: {item}")
    return path


def within(path, root):
    return path != root and root in path.parents


def read_input(path, role, fingerprints, allow_missing=False):
    checked_path(path)
    try:
        with path.open("rb") as stream:
            require(stat.S_ISREG(os.fstat(stream.fileno()).st_mode), f"REGULAR_FILE_REQUIRED: {path}")
            data = stream.read()
    except FileNotFoundError:
        require(allow_missing, f"INPUT_MISSING: {path}")
        data = None
    fingerprints.append({"role": role, "path": str(path), "exists": data is not None,
                         "length": len(data) if data is not None else None,
                         "sha256": sha256(data) if data is not None else None})
    return data


def read_json(path, role, fingerprints):
    data = read_input(path, role, fingerprints)
    try:
        result = json.loads(data.decode("utf-8-sig"))
    except (UnicodeError, json.JSONDecodeError) as error:
        raise AuditError(f"INVALID_JSON: {path}: {error}") from error
    require(isinstance(result, dict), f"JSON_OBJECT_REQUIRED: {path}")
    return result


def verify_inputs(fingerprints):
    checks = []
    for recorded in fingerprints:
        try:
            current = []
            read_input(Path(recorded["path"]), recorded["role"], current, allow_missing=True)
            observed = current[0]
            unchanged = all(recorded[key] == observed[key] for key in ("exists", "length", "sha256"))
            checks.append({"path": recorded["path"], "unchanged": unchanged,
                           "observedExists": observed["exists"], "observedSha256": observed["sha256"]})
        except (AuditError, OSError) as error:
            checks.append({"path": recorded["path"], "unchanged": False, "error": str(error)})
    return {"unchanged": all(item["unchanged"] for item in checks), "files": checks}


def preflight(run, target, fingerprints):
    checked_path(TRIAL_ROOT)
    require(run.parent == RUNS_ROOT and run.is_dir(), "DIRECT_TRIAL_RUN_REQUIRED")
    require(within(target, TRIAL_ROOT), "OUTPUT_MUST_BE_INSIDE_TRIAL_ROOT")
    relative = target.relative_to(TRIAL_ROOT)
    require(not any(part.lower() in ("runs", "cad", "pdf-probe") for part in relative.parts),
            "OUTPUT_MUST_BE_OUTSIDE_RUNS_AND_CAD_INPUTS")
    require(not any(Path(part).suffix.lower() in (".pdf", ".sldprt", ".sldasm", ".slddrw") for part in relative.parts),
            "OUTPUT_CANNOT_BE_A_CAD_OR_PDF_PATH")
    require(not target.exists(), "NEW_OUTPUT_DIRECTORY_REQUIRED")
    supervision = read_json(run / "supervision.json", "supervision", fingerprints)
    require(supervision.get("state") == "cad_exited" and supervision.get("nativeEnded") is True
            and supervision.get("cad") == [], "RUN_SUPERVISION_NOT_TERMINATED")
    native = read_json(run / "native-result.json", "native-result", fingerprints)
    events = native.get("events")
    require(isinstance(events, list) and events and isinstance(events[-1], dict), "NATIVE_END_EVENT_MISSING")
    ended = events[-1]
    require(ended.get("inspection") == "finished" and ended.get("cadExited") is True, "NATIVE_RUN_NOT_EXITED")
    probe_root = run / "cad" / "pdf-probe"
    probe_path = probe_root / "probe-result.json"
    probe = read_json(probe_path, "probe-result", fingerprints)
    attempts = probe.get("attempts")
    require(isinstance(attempts, list), "PROBE_ATTEMPTS_ARRAY_REQUIRED")
    prepared, arms, paths = [], set(), set()
    for index, attempt in enumerate(attempts, 1):
        require(isinstance(attempt, dict), f"INVALID_ATTEMPT: {index}")
        arm = attempt.get("arm")
        require(isinstance(arm, str) and arm.strip() and arm not in arms, f"INVALID_OR_DUPLICATE_ARM: {index}")
        require(isinstance(attempt.get("path"), str), f"PDF_PATH_REQUIRED: {arm}")
        pdf = checked_path(attempt["path"])
        require(within(pdf, probe_root) and pdf.suffix.lower() == ".pdf" and pdf not in paths,
                f"UNIQUE_PDF_INSIDE_RUN_PROBE_REQUIRED: {arm}")
        receipt_path = pdf.with_suffix(".json")
        receipt = read_json(receipt_path, f"receipt:{arm}", fingerprints)
        require(attempt == receipt, f"PROBE_RECEIPT_MISMATCH: {arm}")
        require(isinstance(receipt.get("exists"), bool), f"RECEIPT_PDF_EXISTENCE_REQUIRED: {arm}")
        data = read_input(pdf, f"pdf:{arm}", fingerprints, allow_missing=not receipt["exists"])
        require((data is not None) == receipt["exists"], f"PDF_EXISTENCE_MISMATCH: {arm}")
        if data is not None:
            recorded_sha = receipt.get("sha256")
            require(isinstance(recorded_sha, str) and re.fullmatch(r"[0-9A-Fa-f]{64}", recorded_sha),
                    f"RECEIPT_SHA256_REQUIRED: {arm}")
            require(sha256(data) == recorded_sha.upper(), f"PDF_SHA256_MISMATCH: {arm}")
            require(type(receipt.get("length")) is int and len(data) == receipt["length"],
                    f"PDF_LENGTH_MISMATCH: {arm}")
        else:
            require(not receipt.get("sha256") and receipt.get("length") in (None, 0),
                    f"MISSING_PDF_HAS_RECORDED_BYTES: {arm}")
        arms.add(arm)
        paths.add(pdf)
        prepared.append({"arm": arm, "receipt": receipt, "receiptPath": receipt_path,
                         "pdf": pdf, "data": data})
    return supervision, ended, probe, prepared


def extract_pdf(data):
    result = {"status": "complete", "pageCount": None, "pages": [], "errors": [],
              "counts": {key: 0 for key in COUNTS}, "statisticsComplete": True, "fontnames": []}
    fonts = set()
    try:
        with pdfplumber.open(io.BytesIO(data)) as document:
            result["pageCount"] = len(document.pages)
            for index, page in enumerate(document.pages, 1):
                row = {"page": index, "widthPt": float(page.width), "heightPt": float(page.height),
                       "text": None, "textExtractionStatus": "complete", "errors": []}
                try:
                    row["counts"] = {key: len(getattr(page, key)) for key in COUNTS}
                    row["fontnames"] = sorted({c["fontname"] for c in page.chars if isinstance(c.get("fontname"), str)})
                    fonts.update(row["fontnames"])
                    for key in COUNTS:
                        result["counts"][key] += row["counts"][key]
                except Exception as error:
                    row["counts"] = None
                    row["fontnames"] = []
                    row["errors"].append(f"STATISTICS: {type(error).__name__}: {error}")
                    result["statisticsComplete"] = False
                try:
                    row["text"] = page.extract_text() or ""
                    row["hasReplacementGlyph"] = "\ufffd" in row["text"] or "(cid:" in row["text"]
                except Exception as error:
                    row["textExtractionStatus"] = "failed"
                    row["errors"].append(f"TEXT_EXTRACTION: {type(error).__name__}: {error}")
                if row["errors"]:
                    result["status"] = "partial"
                result["pages"].append(row)
    except Exception as error:
        result["status"] = "failed"
        result["statisticsComplete"] = False
        result["errors"].append(f"{type(error).__name__}: {error}")
    result["fontnames"] = sorted(fonts)
    return result


def output_bytes(path, data, target):
    checked_path(path)
    require(within(path, target), "OUTPUT_FILE_OUTSIDE_NEW_DIRECTORY")
    with path.open("xb") as stream:
        stream.write(data)


def render_pdf(data, folder, target, dpi):
    result = {"status": "complete", "pageCount": None, "pages": [], "errors": [], "dpi": dpi}
    document = None
    try:
        document = pypdfium2.PdfDocument(data)
        result["pageCount"] = len(document)
        for index in range(len(document)):
            page = bitmap = image = None
            try:
                page = document[index]
                bitmap = page.render(scale=dpi / 72)
                image = bitmap.to_pil().convert("RGB")
                buffer = io.BytesIO()
                image.save(buffer, format="PNG")
                png = buffer.getvalue()
                path = folder / f"page-{index + 1:03d}.png"
                output_bytes(path, png, target)
                result["pages"].append({"page": index + 1, "png": str(path.relative_to(target)),
                                        "pngSha256": sha256(png), "pixelSha256": sha256(image.tobytes()),
                                        "widthPx": image.width, "heightPx": image.height, "mode": image.mode})
            except (AuditError, OSError):
                raise
            except Exception as error:
                result["status"] = "partial"
                result["errors"].append({"page": index + 1, "error": f"{type(error).__name__}: {error}"})
            finally:
                for resource in (image, bitmap, page):
                    if resource is not None:
                        resource.close()
    except (AuditError, OSError):
        raise
    except Exception as error:
        result["status"] = "failed"
        result["errors"].append({"error": f"{type(error).__name__}: {error}"})
    finally:
        if document is not None:
            document.close()
    result["allPagesRendered"] = (result["status"] == "complete" and result["pageCount"] is not None
                                  and result["pageCount"] > 0 and len(result["pages"]) == result["pageCount"])
    return result


def no_whitespace(text):
    return "".join(text.split())


def match_notes(notes, extraction, scope):
    pages = [(page["page"], page["text"]) for page in extraction["pages"] if isinstance(page.get("text"), str)]
    complete = (extraction["pageCount"] is not None and len(pages) == extraction["pageCount"]
                and extraction["status"] != "failed")
    rows = []
    for index, note in enumerate(notes):
        if not isinstance(note, dict):
            continue
        key = "requestedText" if scope == "requested" and "requestedText" in note else "text"
        text = note.get(key)
        if not isinstance(text, str):
            continue
        nonblank = bool(text.strip())
        language = "empty" if not nonblank else "cjk" if CJK.search(text) else "ascii" if text.isascii() else "other"
        exact = [number for number, value in pages if nonblank and text in value]
        compact = no_whitespace(text)
        normalized = [number for number, value in pages if nonblank and compact in no_whitespace(value)]
        prefix = "noteRewrite.notes" if scope == "requested" else "before.notes"
        row = {"index": index, "noteIndex": note.get("noteIndex", index), "source": f"{prefix}[{index}].{key}",
               "text": text, "textSha256": sha256(text.encode("utf-8")), "language": language,
               "nonblank": nonblank, "exactPages": exact, "ignoringWhitespacePages": normalized,
               "exactFullTextMatched": bool(exact) if nonblank else None,
               "ignoringWhitespaceFullTextMatched": bool(normalized) if nonblank else None,
               "negativeMatchConclusive": complete if nonblank else None,
               "requestedTypeface": note.get("requestedTypeface"),
               "nativeTypeface": note.get("actualTypeface", note.get("typeface"))}
        if scope == "requested":
            row["sourceSemantics"] = "explicit_request" if key == "requestedText" else "legacy_recorded_note_text"
            row["nativeReadback"] = {key: note[key] for key in
                                     ("actualText", "textMatched", "readbackMatched", "actualFormat", "requestedFormat")
                                     if key in note}
        rows.append(row)
    metrics = {}
    for group in ("all", "cjk", "ascii", "other"):
        selected = [row for row in rows if row["nonblank"] and (group == "all" or row["language"] == group)]
        exact = sum(row["exactFullTextMatched"] for row in selected)
        normalized = sum(row["ignoringWhitespaceFullTextMatched"] for row in selected)
        metrics[group] = {"total": len(selected), "exactMatched": exact, "ignoringWhitespaceMatched": normalized,
                          "exactRate": exact / len(selected) if selected else None,
                          "ignoringWhitespaceRate": normalized / len(selected) if selected else None}
    return {"scope": scope, "targetTextRecorded": bool(rows), "extractionComplete": complete,
            "metrics": metrics, "notes": rows}


def analyze_attempt(prepared, index, target, dpi):
    receipt, data = prepared["receipt"], prepared["data"]
    row = {"index": index, "arm": prepared["arm"], "pdf": str(prepared["pdf"]),
           "receipt": str(prepared["receiptPath"]), "pdfSha256": sha256(data) if data is not None else None,
           "pdfLength": len(data) if data is not None else None, "receiptMatchesProbeAttempt": True,
           "pdfMatchesReceipt": True, "api": {key: receipt.get(key) for key in ("returned", "errors", "warnings", "nativeApiPass")},
           "contentAcceptance": False, "visualReview": "pending", "production_release": False}
    if data is None:
        extraction = {"status": "pdf_missing", "pageCount": None, "pages": [], "errors": [],
                      "counts": {key: 0 for key in COUNTS}, "statisticsComplete": False, "fontnames": []}
        rendering = {"status": "pdf_missing", "pageCount": None, "pages": [], "errors": [],
                     "dpi": dpi, "allPagesRendered": False}
    else:
        folder = target / f"attempt-{index:03d}"
        checked_path(folder)
        folder.mkdir(exist_ok=False)
        extraction = extract_pdf(data)
        rendering = render_pdf(data, folder, target, dpi)
    rewrite = receipt.get("noteRewrite")
    requested = rewrite.get("notes", []) if isinstance(rewrite, dict) else []
    before = receipt.get("before")
    native_notes = before.get("notes", []) if isinstance(before, dict) else []
    require(isinstance(requested, list) and isinstance(native_notes, list), f"NOTE_ARRAY_REQUIRED: {prepared['arm']}")
    row.update({"extraction": extraction, "rendering": rendering,
                "parserRendererPageCountMatches": (extraction["pageCount"] == rendering["pageCount"]
                                                    if extraction["pageCount"] is not None and rendering["pageCount"] is not None else None),
                "requestedNotes": match_notes(requested, extraction, "requested"),
                "nativeBeforeNotes": match_notes(native_notes, extraction, "native_readback")})
    return row


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("run", help="Absolute path to a terminated direct child of TRIAL_ROOT/runs")
    parser.add_argument("--out", required=True, help="New directory inside TRIAL_ROOT, outside runs/CAD inputs")
    parser.add_argument("--dpi", type=int, default=120, help="PNG render resolution, 72..300 (default: 120)")
    args = parser.parse_args()
    target = None
    try:
        require(72 <= args.dpi <= 300, "DPI_MUST_BE_72_TO_300")
        run, target = checked_path(args.run), checked_path(args.out)
        fingerprints = []
        source = checked_path(Path(__file__).absolute())
        read_input(source, "audit-script", fingerprints)
        supervision, ended, probe, prepared = preflight(run, target, fingerprints)
        target.mkdir(parents=True, exist_ok=False)
        checked_path(target)
        attempts = [analyze_attempt(item, index, target, args.dpi) for index, item in enumerate(prepared, 1)]
        integrity = verify_inputs(fingerprints)
        report = {"schema": "16029.variable-trial.pdf-probe-diagnostic-analysis.v1",
                  "purpose": "diagnostic_only; candidate for later formal-audit integration",
                  "createdAtUtc": datetime.now(timezone.utc).isoformat(), "run": str(run), "output": str(target),
                  "script": fingerprints[0], "inputFingerprints": fingerprints, "inputIntegrity": integrity,
                  "tools": {name: importlib.metadata.version(name) for name in ("pdfplumber", "pypdfium2", "Pillow")},
                  "runEndEvidence": {"state": supervision["state"], "nativeEnded": True,
                                     "inspection": ended["inspection"], "cadExited": True, "nativeSuccess": ended.get("success")},
                  "probe": {key: probe.get(key) for key in
                            ("schema", "mode", "completed", "expectedAttemptCount", "actualAttemptCount", "failure")},
                  "attemptCount": len(attempts), "attempts": attempts, "analysisCompleted": True,
                  "contentAcceptance": False, "visualReview": "pending", "production_release": False,
                  "limitations": ["Text extraction failures or unmatched strings do not establish visual failure.",
                                  "Exact and whitespace-insensitive matches require a complete target string on one page.",
                                  "Native readback notes are reported separately; old receipts may not record requested text.",
                                  "Empty target groups have null rates; matches do not establish layout or unique occurrences.",
                                  "Pixel SHA256 covers raw row-major RGB bytes; dimensions and mode are recorded separately.",
                                  "Terminal supervision/native records are checked; no CAD process or shared lock is operated."]}
        output_bytes(target / "analysis.json", (json.dumps(report, ensure_ascii=False, indent=2) + "\n").encode("utf-8"), target)
        print(json.dumps({"analysis": str(target / "analysis.json"), "scriptSha256": fingerprints[0]["sha256"],
                          "attemptCount": len(attempts), "inputsUnchanged": integrity["unchanged"],
                          "contentAcceptance": False, "visualReview": "pending", "production_release": False}))
        return 0 if integrity["unchanged"] else 2
    except (AuditError, OSError, UnicodeError) as error:
        print(json.dumps({"error": f"{type(error).__name__}: {error}", "output": str(target) if target else None,
                          "contentAcceptance": False, "visualReview": "pending", "production_release": False}), file=sys.stderr)
        return 2


if __name__ == "__main__":
    sys.exit(main())
