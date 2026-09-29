"""Offline failure/reproduction check in an owned, disposable PostgreSQL database."""
from copy import deepcopy
from datetime import datetime, timezone
import json
import hashlib
import os
from pathlib import Path
import subprocess
import sys
import uuid

ROOT=Path(__file__).resolve().parents[1]
os.chdir(ROOT)
sys.path.insert(0,str(ROOT/"collectors/python/src"))
from idx_stock_collector.contract import archive_payload
from idx_stock_collector.eodhd_experimental import PARSER_VERSION
from idx_stock_collector.pilot import normalized

batch_path=Path(sys.argv[1] if len(sys.argv)>1 else "data/collector-output/pilot/1f7cc972-68ee-41cd-9bce-eded7efda103.json")
database="idx_pilot_check_"+uuid.uuid4().hex
work=Path("data/collector-output/pilot")/database
work.mkdir(parents=True)
env={k:v for k,v in os.environ.items() if k!="EODHD_API_TOKEN"}
env["IDX_PILOT_DATABASE"]=database
snapshot=subprocess.check_output(["git","show","3024e03:pilot/sessions.json"],text=True)
(work/"sessions.json").write_text(snapshot)
env["IDX_PILOT_SESSIONS"]=str(work/"sessions.json")
report={"database":database,"provider_requests":0,"cases":{}}


def sql(statement, db=database):
    result=subprocess.run(["docker","compose","exec","-T","postgres","psql","-X","-q","-A","-t","-v","ON_ERROR_STOP=1",
                           "-U","idx_stock","-d",db],input=statement,text=True,capture_output=True,env=env,timeout=60)
    if result.returncode:
        raise RuntimeError("Local database check failed; values suppressed.")
    return result.stdout.strip()


def invoke(batch):
    batch=deepcopy(batch)
    batch["run_id"]=str(uuid.uuid4())
    batch["started_at"]=datetime.now(timezone.utc).isoformat()
    batch["mode"]="OFFLINE_REPLAY"
    path=work/(batch["run_id"]+".json")
    path.write_text(json.dumps(batch))
    result=subprocess.run(["dotnet","run","--project","src/IdxStockIntelligence.Worker","--no-build","--no-restore","--","pilot",str(path)],
                          capture_output=True,text=True,env=env,timeout=120)
    messages=[line for line in result.stdout.splitlines() if line.startswith('{"summary_path":')]
    assert messages,"Worker must leave an explicit report."
    summary=json.loads(Path(json.loads(messages[-1])["summary_path"]).read_text())
    assert not summary["soak_eligible"],"Offline fixtures must never count as prospective runs."
    return result.returncode,summary


def prices(batch, symbol, duplicate=False, malformed=False):
    entry=next(e for e in batch["entries"] if e["symbol"]==symbol)
    payload=(Path(entry["raw_root"])/entry["manifest"]["artifact"]["relative_uri"]).read_bytes()
    rows=json.loads(payload)
    if duplicate:
        rows.append(rows[-1])
    else:
        rows[-1]["close"]+=1
        rows[-1]["high"]=max(rows[-1]["high"],rows[-1]["close"])
    payload=b"not-json" if malformed else json.dumps(rows).encode()
    raw=Path("data/raw/pilot-hardening")/database
    entry["manifest"]=archive_payload(payload,raw,source_id="eodhd",requested_uri=entry["manifest"]["requested_uri"],
        request_parameters=entry["manifest"]["request_parameters"],parser_version=PARSER_VERSION,extension=".json")
    entry["raw_root"]=str(raw)
    if not duplicate and not malformed:
        from datetime import date
        entry["rows"]=normalized(payload,date.fromisoformat(batch["from"]),date.fromisoformat(batch["to"]))


def count():
    return int(sql("SELECT count(*) FROM daily_bar_revision;"))


def signature(db, reference_instruments=None):
    selected=" WHERE session_date BETWEEN '2026-09-23' AND '2026-09-25' AND revision_number=1" if db=="idx_stock_intelligence" else ""
    if reference_instruments is not None:
        selected+=" AND instrument_id IN ("+",".join("'"+str(uuid.UUID(i))+"'::uuid" for i in reference_instruments)+")"
    return json.loads(sql("SELECT coalesce(jsonb_agg(to_jsonb(s) ORDER BY instrument_id,session_date,revision_number),'[]') FROM (SELECT instrument_id,session_date,revision_number,raw_artifact_id,canonical_content_sha256,open,high,low,close,volume,adjusted_close,retrieved_at,volume_unit,volume_basis,market_segment FROM daily_bar_revision"+selected+") s;",db))


def verify_provenance_and_order():
    records=json.loads(sql("""SELECT jsonb_agg(to_jsonb(s) ORDER BY instrument_id,session_date,revision_number)
        FROM (SELECT r.instrument_id,r.session_date,r.revision_number,r.known_at,r.retrieved_at,
              r.session_reference,r.session_known_at,a.local_uri,a.content_sha256,a.byte_length
              FROM daily_bar_revision r JOIN raw_artifact a USING(raw_artifact_id)) s;"""))
    grouped={}
    for record in records:
        payload=Path(record["local_uri"]).read_bytes()
        assert hashlib.sha256(payload).hexdigest()==record["content_sha256"]
        assert len(payload)==record["byte_length"]
        assert record["session_reference"].startswith("https://")
        known=datetime.fromisoformat(record["known_at"])
        assert datetime.fromisoformat(record["retrieved_at"])<=known
        assert datetime.fromisoformat(record["session_known_at"])<=known
        grouped.setdefault((record["instrument_id"],record["session_date"]),[]).append(record)
    for revisions in grouped.values():
        assert [r["revision_number"] for r in revisions]==list(range(1,len(revisions)+1))
        assert [r["known_at"] for r in revisions]==sorted(r["known_at"] for r in revisions)


created=False
try:
    base=json.loads(batch_path.read_text())
    reference=signature("idx_stock_intelligence",[e["instrument_id"] for e in base["entries"] if e["status"]=="AVAILABLE"])
    sql("CREATE DATABASE "+database+";","idx_stock_intelligence")
    created=True
    code, restored=invoke(base)
    assert code==2 and restored["run_status"]=="DEGRADED"
    assert count()==21 and signature(database)==reference
    verify_provenance_and_order()
    report["cases"]["restore_canonical_and_provenance"]="PASS"
    previous=json.loads(Path("data/collector-output/pilot/1f7cc972-68ee-41cd-9bce-eded7efda103.summary.json").read_text())
    assert restored["features"]==previous["features"]
    report["cases"]["restore_current_warmup_features"]="PASS"
    _, rerun=invoke(base)
    assert rerun["revisions_added"]==0 and count()==21
    report["cases"]["identical_replay"]="PASS"
    # Metadata corrections append; identical imports must not change earlier knowledge.
    boundary=json.loads(Path("pilot/instrument-boundaries.json").read_text())[0]
    boundary.update(instrument_id=str(uuid.uuid4()),symbol="SYNTHETIC",issuer_name="Synthetic issuer",
                    listed_from="2001-01-02",retrieved_at="2026-01-01T00:00:00Z",known_at="2026-01-01T00:00:00Z")
    boundary_path=work/"boundaries.json"
    def import_boundary(record):
        boundary_path.write_text(json.dumps([record]))
        return subprocess.run(["dotnet","run","--project","src/IdxStockIntelligence.Worker","--no-build","--no-restore","--",
            "import-boundaries",str(boundary_path)],capture_output=True,text=True,env=env,timeout=120).returncode
    assert import_boundary(boundary)==0 and import_boundary(boundary)==0
    predicate="instrument_id='"+boundary["instrument_id"]+"'::uuid"
    assert sql("SELECT count(*) FROM instrument_listing_evidence WHERE "+predicate+";")=="1"
    changed=deepcopy(boundary);changed["listed_from"]="2001-01-03"
    assert import_boundary(changed)==1
    changed.update(retrieved_at="2026-01-02T00:00:00Z",known_at="2026-01-02T00:00:00Z")
    assert import_boundary(changed)==0
    assert sql("SELECT count(*) FROM instrument_listing_evidence WHERE "+predicate+";")=="2"
    for cutoff,expected_date in (("2026-01-01","2001-01-02"),("2026-01-02","2001-01-03")):
        assert sql("SELECT evidence->>'listed_from' FROM instrument_listing_evidence WHERE "+predicate+
                   " AND known_at<='"+cutoff+"' ORDER BY known_at DESC LIMIT 1;")==expected_date
    assert count()==21
    report["cases"]["boundary_import_idempotence_conflict_and_asof_correction"]="PASS"
    statuses=[o["Status"] for o in restored["observations"]]
    assert "SESSION_UNCONFIRMED" in statuses and "CLOSED" in statuses
    assert sql("SELECT count(*) FROM daily_bar_revision WHERE session_date IN ('2026-08-17','2026-08-25');")=="0"
    report["cases"]["unconfirmed_and_holiday_padding"]="PASS"
    failed=deepcopy(base)
    entry=next(e for e in failed["entries"] if e["symbol"]=="BBCA.JK")
    entry.update(status="SOURCE_ERROR",rows=[])
    entry.pop("manifest")
    _, outcome=invoke(failed)
    assert outcome["run_status"]=="DEGRADED" and count()==21
    assert any(o["Status"]=="SOURCE_ERROR" and o["symbol"]=="BBCA.JK" for o in outcome["observations"])
    report["cases"]["provider_failure_no_zero_bar"]="PASS"
    for label, mutate in (
        ("missing_instrument",lambda b:b["entries"].pop()),
        ("duplicate_provider_row",lambda b:prices(b,"BBCA.JK",duplicate=True)),
        ("malformed_payload",lambda b:prices(b,"BBCA.JK",malformed=True))):
        fixture=deepcopy(base);mutate(fixture)
        code,outcome=invoke(fixture)
        assert code==1 and outcome["run_status"]=="FAILED" and count()==21
        report["cases"][label]="PASS"
    correction=deepcopy(base);prices(correction,"BBCA.JK")
    _,outcome=invoke(correction)
    assert outcome["corrections"]==1 and count()==22
    _,outcome=invoke(correction)
    assert outcome["corrections"]==0 and count()==22
    _,outcome=invoke(base)
    assert outcome["stale_evidence_ignored"]==1 and count()==22
    report["cases"]["changed_row_and_stale_archive_replay"]="PASS"
    assert sql("SELECT bool_and(first_seen_at<=known_at AND retrieved_at<=known_at) FROM pilot_revision_evidence;")=="t"
    report["cases"]["first_seen_and_revision_chronology"]="PASS"
    transaction=deepcopy(base);prices(transaction,"BBRI.JK");prices(transaction,"ANTM.JK")
    target=next(e["instrument_id"] for e in transaction["entries"] if e["symbol"]=="ANTM.JK")
    sql("CREATE FUNCTION controlled_failure() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.instrument_id='"+target+"'::uuid THEN RAISE EXCEPTION 'controlled transaction failure'; END IF; RETURN NEW; END $$; CREATE TRIGGER controlled_failure BEFORE INSERT ON daily_bar_revision FOR EACH ROW EXECUTE FUNCTION controlled_failure();")
    before=count();code,outcome=invoke(transaction)
    assert code==1 and outcome["run_status"]=="FAILED" and count()==before
    sql("DROP TRIGGER controlled_failure ON daily_bar_revision;")
    _,outcome=invoke(transaction)
    assert outcome["corrections"]==2 and count()==before+2
    _,outcome=invoke(transaction)
    assert outcome["revisions_added"]==0
    report["cases"]["transaction_rollback_and_safe_rerun"]="PASS"
    verify_provenance_and_order()
    expected=signature(database)
    # Recreate the same correction sequence in a second clean database, entirely offline.
    second=database+"_r"
    sql("CREATE DATABASE "+second+";","idx_stock_intelligence")
    try:
        env["IDX_PILOT_DATABASE"]=second
        invoke(base)
        invoke(correction)
        invoke(transaction)
        assert signature(second)==expected
        report["cases"]["clean_restore_revision_sequence_and_raw_provenance"]="PASS"
    finally:
        env["IDX_PILOT_DATABASE"]=database
        sql("DROP DATABASE "+second+";","idx_stock_intelligence")
    report["status"]="PASS"
    report["restored_rows"]=21
    report["limitation"]="Warmup features reproduced; original canonical knowledge times require a database backup, not re-ingestion."
finally:
    if created:
        sql("DROP DATABASE "+database+";","idx_stock_intelligence")
    (work/"result.json").write_text(json.dumps(report,indent=2))
print(json.dumps({"status":report.get("status","FAILED"),"checks":len(report["cases"]),"provider_requests":0,"report":str(work/"result.json")}))
