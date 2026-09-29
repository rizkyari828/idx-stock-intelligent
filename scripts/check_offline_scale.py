"""Opt-in synthetic scale check. Owns and drops only unique disposable databases."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import time
import uuid

ROOT = Path(__file__).resolve().parents[1]
os.chdir(ROOT)
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--equities", type=int, default=900, choices=(100, 250, 500, 900))
args = parser.parse_args()
env = {k: v for k, v in os.environ.items() if k != "EODHD_API_TOKEN"}
name = "idx_scale_" + uuid.uuid4().hex
restored = name + "_restore"
work = Path("data/collector-output/scale") / name
work.mkdir(parents=True)
report_path = work / "result.json"


def sql(text, database="idx_stock_intelligence"):
    result = subprocess.run(["docker", "compose", "exec", "-T", "postgres", "psql", "-X", "-q", "-A", "-t",
                             "-v", "ON_ERROR_STOP=1", "-U", "idx_stock", "-d", database],
                            input=text, text=True, capture_output=True, env=env, timeout=60)
    if result.returncode:
        raise RuntimeError("Local scale SQL failed; details suppressed.")
    return result.stdout.strip()


def signature(database):
    # Includes values, provenance and knowledge clocks; prints no normal market rows.
    value = sql("SELECT coalesce(jsonb_agg(to_jsonb(r) ORDER BY instrument_id,session_date,revision_number),'[]') "
                "FROM daily_bar_revision r;", database)
    return hashlib.sha256(value.encode()).hexdigest()


before = signature("idx_stock_intelligence")
created = []
try:
    for database in (name, restored):
        sql(f"CREATE DATABASE {database};")
        created.append(database)
    env["IDX_PILOT_DATABASE"] = name
    subprocess.run(["dotnet", "run", "--project", "scripts/OfflineScale", "--no-restore", "--",
                    str(args.equities), str(report_path)], check=True, env=env, timeout=1800)
    report = json.loads(report_path.read_text())
    timer = time.perf_counter()
    with (work / "dump.sql").open("wb") as target:
        subprocess.run(["docker", "compose", "exec", "-T", "postgres", "pg_dump", "-U", "idx_stock", name],
                       stdout=target, check=True, env=env, timeout=180)
    report["timings"]["backup"] = time.perf_counter() - timer
    timer = time.perf_counter()
    with (work / "dump.sql").open("rb") as source:
        subprocess.run(["docker", "compose", "exec", "-T", "postgres", "psql", "-X", "-q",
                        "-v", "ON_ERROR_STOP=1", "-U", "idx_stock", "-d", restored],
                       stdin=source, stdout=subprocess.DEVNULL, check=True, env=env, timeout=180)
    report["timings"]["restore"] = time.perf_counter() - timer
    assert signature(name) == signature(restored), "Restore changed revisions/provenance/chronology."
    env["IDX_PILOT_DATABASE"] = restored
    result = subprocess.run(["dotnet", "run", "--project", "scripts/OfflineScale", "--no-build", "--no-restore", "--",
                             str(args.equities), str(report_path), "verify-restore"],
                            capture_output=True, text=True, check=True, env=env, timeout=180)
    restored_result = json.loads(result.stdout)
    report["timings"].update(restored_result["timings"])
    report["restore"] = "PASS: exact revisions and independently recomputed prior/latest features"
    assert signature("idx_stock_intelligence") == before, "Normal database changed."
    report["normal_database_unchanged"] = True
    report_path.write_text(json.dumps(report, indent=2) + "\n")
    print(json.dumps({"status": "PASS", "report": str(report_path), "provider_requests": 0}), flush=True)
finally:
    for database in reversed(created):
        sql(f"DROP DATABASE {database};")
