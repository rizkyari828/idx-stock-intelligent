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


def collect(args) -> Path:
    config = json.loads(Path(args.universe).read_text())
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

    previous = {}
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
    halted = args.offline
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
            except ValueError:
                entry.update(status="SOURCE_ERROR", reason="REQUEST_OR_SCHEMA_ERROR", rows=[])
                halted = True
        run["entries"].append(entry)
        save()
    if not args.offline:
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
    except (OSError,ValueError,subprocess.TimeoutExpired):
        report.update(status="FAILED",error_code="WORKER_EXECUTION_ERROR",worker_exit_code=1)
    report["completed_at"]=datetime.now(timezone.utc).isoformat()
    report["soak"]=soak_progress(batch_path.parent)
    output.write_text(json.dumps(report,indent=2))
    return output


def soak_progress(root: Path) -> dict:
    config=json.loads(Path("pilot/soak.json").read_text())
    dates=set()
    for path in root.glob("*.summary.json"):
        try:
            summary=json.loads(path.read_text())
            day=summary.get("market_date")
            if summary.get("soak_eligible") is True and summary.get("run_status")=="SUCCEEDED" and day>config["after_market_date"]:
                dates.add(day)
        except (ValueError,OSError,TypeError):
            continue  # Partial report cannot prove a completed run.
    return {"completed_unique_sessions":len(dates),"required":config["required_completed_runs"],
            "market_dates":sorted(dates),"gate_complete":len(dates)>=config["required_completed_runs"]}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--from", dest="start", required=True)
    parser.add_argument("--to", dest="end", required=True)
    parser.add_argument("--universe", default="pilot/universe.json")
    parser.add_argument("--resume")
    parser.add_argument("--seed", action="append", default=[], help="SYMBOL=manifest.json=raw-root")
    parser.add_argument("--offline", action="store_true")
    parser.add_argument("--ingest", action="store_true", help="Run validation, ingestion, features and durable operation report after collection.")
    args = parser.parse_args()
    Path("data/collector-output/pilot").mkdir(parents=True, exist_ok=True)
    # ponytail: one local collector; use account-wide coordination if multiple machines collect.
    with Path("data/collector-output/pilot/collector.lock").open("a") as lock:
        fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        try:
            path=collect(args)
            if args.ingest:
                path=ingest_and_summarize(path)
                print(path)
                status=json.loads(path.read_text())["status"]
                raise SystemExit(0 if status=="SUCCEEDED" else 2 if status=="DEGRADED" else 1)
            print(path)
            status=json.loads(path.read_text())["status"]
            raise SystemExit(0 if status=="SUCCEEDED" else 2 if status=="DEGRADED" else 1)
        except (ValueError, OSError, KeyError, json.JSONDecodeError):
            raise SystemExit("STOP: invalid configuration, evidence, entitlement or request. No automatic retry.") from None


if __name__ == "__main__":
    main()
