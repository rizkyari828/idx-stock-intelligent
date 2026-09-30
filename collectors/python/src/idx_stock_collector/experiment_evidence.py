"""Sanitized, noncanonical evidence for explicitly authorized EOD experiments.

This module has no production archive, ingestion, database, feature or soak imports.
It never performs a provider request. Call it only with an EOD response already received.
"""

from datetime import date, datetime, timezone
from hashlib import sha256
import json
from pathlib import Path
import re

from .eodhd_experimental import parse_daily

REPOSITORY_ROOT = Path(__file__).resolve().parents[4]
EXPERIMENT_NAMESPACE = Path("data/collector-output/experiments")
SYMBOL = re.compile(r"(?:[A-Z]{4}\.JK|JKSE\.INDX)\Z")
EXPERIMENT_ID = re.compile(r"[a-z0-9]+(?:-[a-z0-9]+)*\Z")


def evidence_record(symbol: str, requested_date: date, retrieved_at: datetime,
                    http_status: int | None, payload: bytes, failure_class: str | None = None) -> dict:
    """Allowlist fields only; never retain response text or provider metadata."""
    if (not isinstance(symbol, str) or not SYMBOL.fullmatch(symbol) or type(requested_date) is not date
            or type(retrieved_at) is not datetime or retrieved_at.tzinfo is None
            or retrieved_at.utcoffset() is None
            or type(payload) is not bytes or (http_status is not None
            and (type(http_status) is not int or not 100 <= http_status <= 599))
            or failure_class not in (None, "TIMEOUT", "NETWORK_ERROR")):
        raise ValueError("Invalid experiment evidence input")
    if (http_status is None) == (failure_class is None):
        raise ValueError("Exactly one HTTP outcome is required")

    try:
        body = json.loads(payload)
    except (ValueError, UnicodeDecodeError):
        body = None
    if isinstance(body, dict) and any(key in body for key in
                                      ("apiRequests", "subscriptionType", "dailyRateLimit", "extraLimit")):
        raise ValueError("Account responses cannot be experiment evidence")

    record = {
        "scope": "NONCANONICAL", "purpose": "EXPERIMENT_ONLY",
        "symbol": symbol, "requested_date": requested_date.isoformat(),
        "retrieved_at": retrieved_at.astimezone(timezone.utc).isoformat(),
        "http_outcome": f"HTTP_{http_status}" if http_status is not None else failure_class,
        "row_count": len(body) if isinstance(body, list) else 0,
        "payload_valid": False, "response_bytes": len(payload),
        "content_hash": sha256(payload).hexdigest(),
        "warning_error_classification": None,
        "open": None, "high": None, "low": None, "close": None,
        "adjusted_close": None, "volume": None,
    }
    if http_status != 200:
        record["warning_error_classification"] = failure_class or "HTTP_ERROR"
    elif isinstance(body, (list, dict)) and (body if isinstance(body, list) else [body]):
        first = body[0] if isinstance(body, list) else body
        if isinstance(first, dict) and any(key.casefold() in ("warning", "error", "message") for key in first):
            record["warning_error_classification"] = "PROVIDER_WARNING_OR_ERROR"
    if http_status == 200 and record["warning_error_classification"] is None:
        try:
            rows = parse_daily(payload)
            if len(rows) != 1 or rows[0]["date"] != requested_date.isoformat():
                raise ValueError("Unexpected experiment date or row count")
            row = rows[0]
            record.update({key: str(row[key]) for key in
                           ("open", "high", "low", "close", "adjusted_close")})
            record["volume"] = row["volume"]
            record["payload_valid"] = True
        except (ValueError, TypeError):
            record["warning_error_classification"] = "MALFORMED_OR_UNEXPECTED_EOD"
    return record


def write_evidence(experiment_id: str, symbol: str, requested_date: date,
                   retrieved_at: datetime, http_status: int | None, payload: bytes,
                   failure_class: str | None = None,
                   repository_root: Path = REPOSITORY_ROOT) -> Path:
    """Write one immutable observation under the Git-ignored experiment namespace."""
    if not isinstance(experiment_id, str) or not EXPERIMENT_ID.fullmatch(experiment_id):
        raise ValueError("Invalid experiment ID")
    record = evidence_record(symbol, requested_date, retrieved_at, http_status, payload, failure_class)
    repository = Path(repository_root).resolve()
    namespace = repository / EXPERIMENT_NAMESPACE
    if namespace.resolve() != namespace:
        raise ValueError("Experiment namespace must not be redirected")
    root = namespace / experiment_id
    root.mkdir(parents=True, exist_ok=True)
    stamp = retrieved_at.astimezone(timezone.utc)
    name = f"{requested_date}-{symbol}-{stamp.strftime('%Y%m%dT%H%M%S%fZ')}-{record['content_hash'][:12]}.json"
    target = root / name
    if not target.resolve().is_relative_to(root.resolve()):
        raise ValueError("Experiment evidence path escaped namespace")
    serialized = json.dumps(record, sort_keys=True, indent=2) + "\n"
    try:
        with target.open("x", encoding="utf-8") as output:
            output.write(serialized)
    except FileExistsError:
        if target.read_text(encoding="utf-8") != serialized:
            raise ValueError("Existing experiment evidence differs")
    return target
