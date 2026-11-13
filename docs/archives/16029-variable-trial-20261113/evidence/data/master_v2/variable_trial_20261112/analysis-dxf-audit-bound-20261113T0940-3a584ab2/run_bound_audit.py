from pathlib import Path
import hashlib,json,runpy,sys,importlib.metadata
root=Path(r"D:\Winnsen_Structure_Agent_Studio")
source=root/"workers/master_v2/variable_trial"
run=root/"data/master_v2/variable_trial_20261112/runs/2026-11-13T08-27-26-311Z-T2-outputs-dxf-9f7e1153"
out=root/"data/master_v2/variable_trial_20261112/analysis-dxf-audit-bound-20261113T0940-3a584ab2/output"
files={name:(source/name).read_bytes() for name in ["audit_outputs.py","dxf_check.py"]}
versions={name:importlib.metadata.version(name) for name in ["pdfplumber","pypdfium2"]}
sys.path.insert(0,str(source));sys.argv=[str(source/"audit_outputs.py"),str(run),"--out",str(out)]
runpy.run_path(str(source/"audit_outputs.py"),run_name="__main__")
records=[]
archive=out/"auditor-sources";archive.mkdir(exist_ok=False)
for name,data in files.items():
 assert (source/name).read_bytes()==data,"AUDITOR_CHANGED_DURING_RUN"
 target=archive/name;target.write_bytes(data)
 records.append({"path":str(source/name),"archivePath":str(target),"sha256":hashlib.sha256(data).hexdigest().upper()})
evidence={"schema":"16029-dxf-auditor-evidence/v1","source":str(run),"outputAuditSha256":hashlib.sha256((out/"output-audit.json").read_bytes()).hexdigest().upper(),"sources":records,"libraries":versions,"sourceBytesStableDuringAudit":True,"drawingAndPdfActionsExecuted":False,"production_release":False}
(out/"auditor-toolchain.json").write_text(json.dumps(evidence,ensure_ascii=False,indent=2),encoding="utf-8")
print("BOUND_AUDIT",out)
