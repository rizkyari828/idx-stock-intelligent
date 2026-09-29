"""Fixed-universe, manual Free-plan collector. No retries or calendar inference."""
from __future__ import annotations

import argparse
from datetime import date, datetime, timedelta, timezone
from decimal import Decimal
import fcntl
import hashlib
import json
import os
import subprocess
from pathlib import Path
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid
from zoneinfo import ZoneInfo

from .contract import archive_payload
from .eodhd_experimental import parse_daily, PARSER_VERSION


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *args, **kwargs):
        return None


def window(start: str, end: str, today: date) -> tuple[date, date]:
    first, last = date.fromisoformat(start), date.fromisoformat(end)
    # Conservative subset of the approximately one-year Free entitlement.
    if first > last or first < today - timedelta(days=330) or last >= today:
        raise ValueError("Use completed dates within the last 330 days.")
    return first, last


def remaining(usage: dict, ceiling: int, quota_day: date | None = None) -> int:
    if str(usage.get("subscriptionType", "")).casefold() != "free" or usage.get("dailyRateLimit") != 20:
        raise ValueError("Expected verified Free entitlement (20 units/day).")
    used = usage.get("apiRequests")
    if type(used) is not int or used < 0 or not 0 < ceiling <= 16:
        raise ValueError("Invalid quota accounting.")
    if quota_day is not None and used:
        accounted_day = date.fromisoformat(usage["apiRequestsDate"])
        if accounted_day > quota_day:
            raise ValueError("Future quota accounting date.")
        # Provider resets lazily at the first charged request after midnight GMT.
        if accounted_day < quota_day:
            used = 0
    return max(0, ceiling - used)


def normalized(payload: bytes, start: date, end: date) -> list[dict]:
    rows = parse_daily(payload)
    if any(not start <= date.fromisoformat(row["date"]) <= end for row in rows):
        raise ValueError("Provider returned dates outside request window.")
    return [{key: str(value) if isinstance(value, Decimal) else value for key, value in row.items()} for row in rows]


def covers(manifest: dict, start: date, end: date) -> bool:
    params = manifest["request_parameters"]
    return date.fromisoformat(params["from"]) <= start and date.fromisoformat(params["to"]) >= end


def read_seed(manifest_path: Path, raw_root: Path, start: date, end: date) -> dict:
    manifest = json.loads(manifest_path.read_text())
    entry={"status":"AVAILABLE","manifest":manifest,"raw_root":str(raw_root)}
    return dict(entry,**read_seed_entry(entry,start,end),request_window_covered=covers(manifest,start,end))


def configuration(path: str) -> dict:
    config = json.loads(Path(path).read_text())
    if config.get("universe_mode","Pilot") != "Pilot":
        raise ValueError("FullIdx is not enabled.")
    fixed = json.loads(Path(__file__).resolve().parents[4].joinpath("pilot/universe.json").read_text())
    if (config.get("benchmark"), config.get("daily_unit_ceiling"), config.get("instruments")) != (
            fixed["benchmark"], fixed["daily_unit_ceiling"], fixed["instruments"]):
        raise ValueError("Live universe is fixed to Pilot; FullIdx is not enabled.")
    return config


def local_state() -> dict:
    """Read canonical state through the .NET owner, using local Docker IPC only."""
    try:
        result = subprocess.run(["dotnet", "run", "--project", "src/IdxStockIntelligence.Worker",
                                 "--no-build", "--no-restore", "--", "pilot-state"],
                                env={k:v for k,v in os.environ.items() if k!="EODHD_API_TOKEN"},
                                capture_output=True, text=True, timeout=90)
        state = json.loads(result.stdout)
        if result.returncode == 0 and state["status"] == "KNOWN":
            return state
    except (OSError, ValueError, KeyError, subprocess.TimeoutExpired):
        pass
    return {"status":"UNKNOWN", "revisions":[]}


def reusable_entries(root: Path, start: date, end: date, state: dict, refresh: bool = False) -> dict:
    """Only clean, completed canonical sessions with intact evidence can skip fetch."""
    if refresh or start != end or state.get("status") != "KNOWN":
        return {}
    sessions = json.loads(Path("pilot/sessions.json").read_text())
    current = {(r["instrument_id"],r["date"]):r for r in state["revisions"]}
    previous = {}
    for path in sorted(root.glob("*.operation.json"), key=lambda p:p.stat().st_mtime):
        try:
            operation = json.loads(path.read_text())
            batch = json.loads(Path(operation["batch_path"]).read_text())
            if batch.get("from") != start.isoformat() or batch.get("to") != end.isoformat():
                continue
            # A later degraded/failed attempt must not revive older clean evidence.
            for entry in batch["entries"]:
                previous.pop(entry["symbol"],None)
            worker = operation["worker"]
            if (operation["status"] != "SUCCEEDED" or worker["run_status"] != "SUCCEEDED"
                    or worker.get("database") != state.get("database") or worker.get("warnings")
                    or worker["market_date"] != start.isoformat() or worker["session_evidence"] != sessions):
                continue
            recorded = {(r["instrument_id"],r["date"]):r for r in worker["canonical_state"]}
            for entry in batch["entries"]:
                key = (entry["instrument_id"],start.isoformat())
                if (entry["status"] != "AVAILABLE" or not covers(entry["manifest"],start,end)
                        or entry["manifest"].get("parser_version") != PARSER_VERSION
                        or entry["manifest"].get("source_id") != "eodhd"
                        or entry["manifest"].get("requested_uri") != "https://eodhd.com/api/eod/"+entry["symbol"]
                        or key not in recorded or current.get(key) != recorded[key]):
                    continue
                verified = read_seed_entry(entry,start,end)
                fetched = datetime.fromisoformat(entry["manifest"]["fetched_at_utc"].replace("Z","+00:00"))
                canonical = datetime.fromisoformat(current[key]["retrieved_at"].replace("Z","+00:00"))
                proof = next(p for p in sessions if p["date"] == start.isoformat())
                known = datetime.fromisoformat(proof["known_at"].replace("Z","+00:00"))
                if fetched < canonical or fetched < known or len(verified["rows"]) != 1:
                    continue
                previous[entry["symbol"]] = dict(entry, **verified, request_window_covered=True)
        except (ValueError, OSError, KeyError, TypeError, StopIteration):
            continue  # Uncertain evidence requires fetch, never implies a canonical session.
    return previous


def dry_run(args) -> dict:
    config = configuration(args.universe)
    start,end = window(args.start,args.end,datetime.now(ZoneInfo("Asia/Jakarta")).date())
    state = local_state()
    cached = reusable_entries(Path("data/collector-output/pilot"),start,end,state,getattr(args,"refresh",False))
    sessions = json.loads(Path("pilot/sessions.json").read_text())
    symbols = [i["symbol"] for i in config["instruments"]]
    requested = [s for s in symbols if s not in cached]
    return {"mode":"DRY_RUN", "universe_mode":"Pilot", "symbols_that_would_be_requested":requested,
            "cached_symbols":list(cached), "benchmark":config["benchmark"], "from":str(start),"to":str(end),
            "maximum_eod_units":len(requested), "account_ceiling":config["daily_unit_ceiling"],
            "quota":"not queried in offline mode", "canonical_state":state["status"],
            "canonical_bars":len(state["revisions"]),
            "canonical_session_dates":sorted({r["date"] for r in state["revisions"]}),
            "session_evidence":[p for p in sessions if start<=date.fromisoformat(p["date"])<=end],
            "skipped_actions":["provider/account requests","archival","ingestion","ledger writes"],
            "provider_requests":0}


def collect(args) -> Path:
    config = configuration(args.universe)
    instruments = config["instruments"]
    symbols = [item["symbol"] for item in instruments]
    if len(symbols) != 11 or len(set(symbols)) != 11 or config["benchmark"] != "JKSE.INDX":
        raise ValueError("Expected fixed 10-equity + IHSG panel.")
    today = datetime.now(ZoneInfo("Asia/Jakarta")).date()
    start, end = window(args.start, args.end, today)
    output = Path("data/collector-output/pilot")
    raw_root = Path("data/raw/pilot")
    output.mkdir(parents=True, exist_ok=True)
    run = {"run_id": str(uuid.uuid4()), "started_at": datetime.now(timezone.utc).isoformat(),
           "from": start.isoformat(), "to": end.isoformat(), "entries": [], "requests": [], "reserved_units": 0,
           "mode": "OFFLINE_REPLAY" if args.offline else "DAILY" if start==end and not args.resume and not args.seed else "BOOTSTRAP"}
    target = output / (run["run_id"] + ".json")
    token = os.environ.get("EODHD_API_TOKEN", "")
    if not token and not args.offline:
        raise ValueError("EODHD_API_TOKEN is absent.")
    opener = urllib.request.build_opener(NoRedirect)

    def save():
        temporary = target.with_suffix(".tmp")
        serialized = json.dumps(run, indent=2)
        if token and token in serialized:
            raise ValueError("Credential-bearing metadata rejected.")
        temporary.write_text(serialized)
        temporary.replace(target)

    def fetch(path: str, params: dict, cost: int):
        uri = "https://eodhd.com/api/" + path
        request = {"uri": uri, "parameters": params, "reserved_units": cost,
                   "started_at": datetime.now(timezone.utc).isoformat()}
        run["requests"].append(request)
        run["reserved_units"] += cost
        save()  # Reserve before network; uncertain failures still consume local budget.
        started = time.monotonic()
        try:
            url = uri + "?" + urllib.parse.urlencode(dict(params, api_token=token))
            try:
                response = opener.open(urllib.request.Request(url, headers={"Accept": "application/json", "Accept-Encoding": "identity"}), timeout=30)
            except urllib.error.HTTPError as error:
                response = error
            with response:
                payload = response.read(2 * 1024 * 1024 + 1)
                request.update(http_status=response.status, bytes=len(payload))
            if len(payload) > 2 * 1024 * 1024 or token.encode() in payload:
                raise ValueError("Unsafe response.")
            manifest = None
            if cost:
                manifest = archive_payload(payload, raw_root, source_id="eodhd", requested_uri=uri,
                                           request_parameters=params, parser_version=PARSER_VERSION, extension=".json")
                request["manifest"] = manifest
            if request["http_status"] != 200:
                raise ValueError("Non-success HTTP status.")
            return payload, manifest
        except Exception:
            request["error"] = "SOURCE_ERROR"  # Never stringify an exception containing a credential URL.
            raise ValueError("Provider request failed; no retry.") from None
        finally:
            request["elapsed_seconds"] = round(time.monotonic() - started, 3)
            save()

    state = local_state() if not args.offline else {"status":"UNKNOWN"}
    if getattr(args,"ingest",False) and not args.offline and state["status"] != "KNOWN":
        run.update(status="FAILED",error_code="CANONICAL_PREFLIGHT_FAILED",completed_at=datetime.now(timezone.utc).isoformat())
        run["entries"]=[{"instrument_id":i["id"],"symbol":i["symbol"],"status":"SOURCE_ERROR",
                         "reason":"CANONICAL_PREFLIGHT_FAILED","rows":[]} for i in instruments]
        save()
        return target
    previous = reusable_entries(output,start,end,state,getattr(args,"refresh",False)) if not args.offline else {}
    if args.resume:
        prior = json.loads(Path(args.resume).read_text())
        if (prior["from"], prior["to"]) != (run["from"], run["to"]):
            raise ValueError("Resume window differs.")
        for entry in prior["entries"]:
            if entry["status"]=="AVAILABLE":
                verified=read_seed_entry(entry,start,end)
                previous[entry["symbol"]]=dict(entry,**verified)
    seeds = {}
    for seed in args.seed:
        symbol, manifest, root = seed.split("=", 2)
        if symbol not in symbols:
            raise ValueError("Seed outside fixed panel.")
        seeds[symbol] = read_seed(Path(manifest), Path(root), start, end)
    halted = args.offline or all(symbol in previous for symbol in symbols)
    allowance = 0
    if not halted:
        try:
            usage = json.loads(fetch("user", {"fmt": "json"}, 0)[0])
            allowance = remaining(usage, config["daily_unit_ceiling"], datetime.now(timezone.utc).date())
            run["usage_before"] = {k: usage.get(k) for k in ("subscriptionType", "dailyRateLimit", "apiRequests", "apiRequestsDate")}
        except (ValueError,KeyError):
            run["account_error"]="ACCOUNT_CHECK_FAILED"
            halted=True
    # Resume initial history across quota resets; a new daily run supplies no resume.
    ordered = sorted(instruments, key=lambda item: item["symbol"] not in ("LPIN.JK", "BBRI.JK", "RAJA.JK"))
    for instrument in ordered:
        symbol = instrument["symbol"]
        entry = {"instrument_id": instrument["id"], "symbol": symbol}
        if symbol in previous and covers(previous[symbol]["manifest"], start, end):
            entry.update(previous[symbol])
            entry["cached"] = True
        elif symbol in seeds:
            entry.update(seeds[symbol])
        elif halted or run["reserved_units"] >= allowance:
            if symbol in previous:
                entry.update(previous[symbol], cached=True, request_window_covered=False, reason="PARTIAL_WINDOW_QUOTA_DEFERRED")
            else:
                entry.update(status="SOURCE_ERROR", reason="OFFLINE_OR_QUOTA_DEFERRED", rows=[])
        else:
            try:
                # Account check is zero-unit; never rely on bonus quota or purchase it.
                usage = json.loads(fetch("user", {"fmt": "json"}, 0)[0])
                if remaining(usage, config["daily_unit_ceiling"], datetime.now(timezone.utc).date()) < 1:
                    halted = True
                    entry.update(status="SOURCE_ERROR", reason="QUOTA_DEFERRED", rows=[])
                else:
                    payload, manifest = fetch("eod/" + symbol, {"from": run["from"], "to": run["to"], "period": "d", "fmt": "json"}, 1)
                    entry.update(manifest=manifest, raw_root=str(raw_root))
                    entry.update(status="AVAILABLE", rows=normalized(payload, start, end), request_window_covered=True)
                    run["requests"][-1]["payload_valid"] = True
            except ValueError:
                entry.update(status="SOURCE_ERROR", reason="REQUEST_OR_SCHEMA_ERROR", rows=[])
                halted = True
        run["entries"].append(entry)
        save()
    if not args.offline and any(r["reserved_units"] for r in run["requests"]):
        try:
            usage = json.loads(fetch("user", {"fmt": "json"}, 0)[0])
            run["usage_after"] = {k: usage.get(k) for k in ("subscriptionType", "dailyRateLimit", "apiRequests", "apiRequestsDate")}
        except ValueError:
            run["usage_after"] = "UNKNOWN"
    run["completed_at"] = datetime.now(timezone.utc).isoformat()
    run["status"] = "FAILED" if run.get("account_error") else "SUCCEEDED" if all(e["status"]=="AVAILABLE" and covers(e["manifest"],start,end) for e in run["entries"]) else "DEGRADED"
    save()
    return target


def read_seed_entry(entry: dict, start: date, end: date) -> dict:
    """Verify a resumed artifact, not just yesterday's normalized batch text."""
    manifest=entry["manifest"]
    root=Path(entry["raw_root"])
    artifact=manifest["artifact"]
    path=(root/artifact["relative_uri"]).resolve()
    if not path.is_relative_to(root.resolve()):
        raise ValueError("Artifact path escapes root.")
    payload=path.read_bytes()
    if hashlib.sha256(payload).hexdigest()!=artifact["content_sha256"] or len(payload)!=artifact["byte_length"]:
        raise ValueError("Resume evidence failed verification.")
    rows=parse_daily(payload)
    return {"rows":[{k:str(v) if isinstance(v,Decimal) else v for k,v in row.items()}
                    for row in rows if start<=date.fromisoformat(row["date"])<=end],"cached":True}


def ingest_and_summarize(batch_path: Path) -> Path:
    batch=json.loads(batch_path.read_text())
    started=datetime.now(timezone.utc).isoformat()
    operation_id=str(uuid.uuid4())
    output=batch_path.parent/(operation_id+".operation.json")
    before=batch.get("usage_before")
    after=batch.get("usage_after")
    quota_delta=None
    if isinstance(before,dict) and isinstance(after,dict) and type(after.get("apiRequests")) is int and type(before.get("apiRequests")) is int:
        quota_delta=max(0,after["apiRequests"]-(before["apiRequests"] if before.get("apiRequestsDate")==after.get("apiRequestsDate") else 0))
    report={"operation_id":operation_id,"run_id":batch["run_id"],"started_at":batch["started_at"],"worker_started_at":started,"batch_path":str(batch_path),
            "requested_instruments":[e["symbol"] for e in batch["entries"]],"request_count":len(batch["requests"]),
            "reserved_units":batch["reserved_units"],"usage_before":batch.get("usage_before"),"usage_after":batch.get("usage_after"),
            "quota_consumed_account_delta":quota_delta,"request_bytes":sum(r.get("bytes",0) for r in batch["requests"]),
            "request_ledger":batch["requests"],
            "fetch_outcomes":[{k:e.get(k) for k in ("symbol","status","reason","cached","request_window_covered","manifest")} for e in batch["entries"]]}
    try:
        if batch["status"]=="FAILED":
            raise ValueError("Account check failed.")
        # Worker needs PostgreSQL configuration, not the provider credential.
        child_env={k:v for k,v in os.environ.items() if k!="EODHD_API_TOKEN"}
        result=subprocess.run(["dotnet","run","--project","src/IdxStockIntelligence.Worker","--no-restore","--","pilot",str(batch_path)],
                              env=child_env,capture_output=True,text=True,timeout=120)
        lines=[line for line in result.stdout.splitlines() if line.startswith('{"summary_path":')]
        if not lines:
            raise ValueError("Worker produced no summary.")
        path=Path(json.loads(lines[-1])["summary_path"])
        report.update(worker_summary_path=str(path),worker=json.loads(path.read_text()),worker_exit_code=result.returncode)
        report["status"]=report["worker"]["run_status"]
        expected_code = {"SUCCEEDED":0,"DEGRADED":2,"FAILED":1}.get(report["status"])
        if result.returncode != expected_code:
            raise ValueError("Worker exit code disagrees with summary.")
    except (OSError,ValueError,KeyError,TypeError,subprocess.TimeoutExpired):
        report.update(status="FAILED",error_code="WORKER_EXECUTION_ERROR",worker_exit_code=1)
    report["completed_at"]=datetime.now(timezone.utc).isoformat()
    worker=report.get("worker",{})
    report["ledger"]={"run_id":batch["run_id"],"session_date":batch.get("from") if batch.get("from")==batch.get("to") else None,
        "started_at":batch["started_at"],"completed_at":report["completed_at"],"mode":batch.get("mode","UNKNOWN"),
        "requested_symbols":[e["symbol"] for e in batch["entries"]],
        "successful_fetches":sum(r.get("payload_valid") is True for r in batch["requests"] if r["reserved_units"]),
        "failed_fetches":sum(r.get("payload_valid") is not True for r in batch["requests"] if r["reserved_units"]),
        "quota_units":batch["reserved_units"],"raw_artifacts":[e["manifest"]["artifact"]["content_sha256"] for e in batch["entries"] if "manifest" in e],
        "canonical_bars_added":worker.get("canonical_additions"),"revisions_added":worker.get("revisions_added"),
        "rejected_evidence":worker.get("rejected_evidence"),"feature_status":worker.get("features","UNKNOWN"),
        "warnings":worker.get("warnings",[report.get("error_code","UNKNOWN")]),
        "final_status":"SUCCESS" if report["status"]=="SUCCEEDED" else report["status"],
        "soak_eligible":report["status"]=="SUCCEEDED" and worker.get("soak_eligible",False)}
    report["soak"]=soak_progress(batch_path.parent,report.get("worker_summary_path") if report["status"]!="SUCCEEDED" else None)
    output.write_text(json.dumps(report,indent=2))
    return output


def soak_progress(root: Path, rejected_summary: str | None = None) -> dict:
    config=json.loads(Path("pilot/soak.json").read_text())
    dates=set()
    rejected_summaries={Path(rejected_summary).resolve()} if rejected_summary else set()
    for path in root.glob("*.operation.json"):
        try:
            operation=json.loads(path.read_text())
            if operation.get("status") != "SUCCEEDED" and operation.get("worker_summary_path"):
                rejected_summaries.add(Path(operation["worker_summary_path"]).resolve())
        except (ValueError,OSError,TypeError):
            continue
    for path in root.glob("*.summary.json"):
        if path.resolve() in rejected_summaries:
            continue
        try:
            summary=json.loads(path.read_text())
            day=summary.get("market_date")
            if summary.get("soak_eligible") is True and summary.get("run_status")=="SUCCEEDED" and day>config["after_market_date"]:
                dates.add(day)
        except (ValueError,OSError,TypeError):
            continue  # Partial report cannot prove a completed run.
    return {"completed_unique_sessions":len(dates),"required":config["required_completed_runs"],
            "market_dates":sorted(dates),"gate_complete":len(dates)>=config["required_completed_runs"]}


def soak_report(root: Path) -> dict:
    ledger=[]
    bootstrap=[]
    for path in root.glob("*.operation.json"):
        operation=json.loads(path.read_text())
        if "ledger" in operation:
            ledger.append(operation["ledger"])
        batch=json.loads(Path(operation["batch_path"]).read_text())
        if batch.get("mode")=="BOOTSTRAP":
            complete=False
            try:
                first,last=date.fromisoformat(batch["from"]),date.fromisoformat(batch["to"])
                symbols={i["symbol"] for i in configuration("pilot/universe.json")["instruments"]}
                complete=(batch["status"]=="SUCCEEDED" and len(batch["entries"])==len(symbols)
                          and {e["symbol"] for e in batch["entries"]}==symbols
                          and all(e["status"]=="AVAILABLE" and covers(e["manifest"],first,last)
                                  and e["manifest"].get("source_id")=="eodhd"
                                  and e["manifest"].get("requested_uri")=="https://eodhd.com/api/eod/"+e["symbol"]
                                  and isinstance(e["manifest"].get("parser_version"),str)
                                  and e["manifest"]["parser_version"].strip() for e in batch["entries"]))
                if complete:
                    for entry in batch["entries"]:
                        read_seed_entry(entry,first,last)
            except (ValueError,OSError,KeyError,TypeError):
                complete=False
            bootstrap.append((batch["started_at"],complete))
    ledger.sort(key=lambda row:(row["started_at"],row["run_id"],row["completed_at"]))
    for number,row in enumerate(ledger,1):
        row["run_number"]=number
    progress=soak_progress(root)
    bootstrap_complete=bool(bootstrap and max(bootstrap)[1])
    remaining=[]
    if not bootstrap_complete:
        remaining.append("bootstrap request coverage incomplete")
    if any(i["listing_evidence"]["status"]=="UNKNOWN" for i in configuration("pilot/universe.json")["instruments"]):
        remaining.append("listing boundaries incomplete")
    if not progress["gate_complete"]:
        remaining.append("10-run soak incomplete")
    return {"title":"Phase 0 prospective soak","soak":progress,"last_run":ledger[-1] if ledger else None,
            "bootstrap_request_coverage":"COMPLETE" if bootstrap_complete else "INCOMPLETE_OR_UNKNOWN",
            "quota":"not queried in offline mode","universe":"10 equities + JKSE.INDX",
            "universe_mode":"Pilot","FullIdx":"NOT ENABLED","ledger":ledger,
            "remaining_gates":remaining}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--from", dest="start")
    parser.add_argument("--to", dest="end")
    parser.add_argument("--universe", default="pilot/universe.json")
    parser.add_argument("--resume")
    parser.add_argument("--seed", action="append", default=[], help="SYMBOL=manifest.json=raw-root")
    parser.add_argument("--offline", action="store_true")
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--soak-report", action="store_true")
    parser.add_argument("--refresh", action="store_true", help="Bypass automatic canonical reuse; never automatic retry.")
    parser.add_argument("--ingest", action="store_true", help="Run validation, ingestion, features and durable operation report after collection.")
    try:
        args = parser.parse_args()
    except SystemExit as error:
        raise SystemExit(1 if error.code else 0) from None
    try:
        if args.soak_report:
            print(json.dumps(soak_report(Path("data/collector-output/pilot")),indent=2))
            return
        if not args.start or not args.end:
            raise ValueError("Explicit --from and --to required.")
        if args.refresh and (args.resume or args.seed):
            raise ValueError("Refresh cannot reuse resume/seed evidence.")
        if args.dry_run:
            if args.resume or args.seed:
                raise ValueError("Dry-run currently plans automatic reuse only; omit resume/seed.")
            print(json.dumps(dry_run(args),indent=2))
            return
    except (ValueError,OSError,KeyError):
        raise SystemExit("STOP: invalid local planning configuration.") from None
    Path("data/collector-output/pilot").mkdir(parents=True, exist_ok=True)
    # ponytail: one local collector; use account-wide coordination if multiple machines collect.
    with Path("data/collector-output/pilot/collector.lock").open("a") as lock:
        try:
            fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
            path=collect(args)
            if args.ingest:
                path=ingest_and_summarize(path)
                report=json.loads(path.read_text())
                print(json.dumps({"report":str(path),"status":report["status"],"quota_units":report["reserved_units"],
                    "canonical_bars_added":report.get("worker",{}).get("canonical_additions"),"soak":report["soak"]}))
                status=json.loads(path.read_text())["status"]
                raise SystemExit(0 if status=="SUCCEEDED" else 2 if status=="DEGRADED" else 1)
            print(path)
            status=json.loads(path.read_text())["status"]
            raise SystemExit(0 if status=="SUCCEEDED" else 2 if status=="DEGRADED" else 1)
        except (ValueError, OSError, KeyError, json.JSONDecodeError):
            raise SystemExit("STOP: invalid configuration, evidence, entitlement or request. No automatic retry.") from None


if __name__ == "__main__":
    main()
