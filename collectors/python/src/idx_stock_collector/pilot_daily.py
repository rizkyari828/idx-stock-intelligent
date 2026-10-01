"""Read-only H+1 planning; collection and soak remain owned by the pilot pipeline."""
from datetime import date, datetime, timedelta, timezone
import json
from pathlib import Path
import os
from zoneinfo import ZoneInfo

from .pilot import configuration, collection_eligibility, local_state, safe_cutoff, soak_progress


def daily_plan(now: datetime | None = None) -> dict:
    now = now or datetime.now(timezone.utc)
    if now.tzinfo is None:
        raise ValueError("Timezone-aware planning clock required.")
    today = now.astimezone(ZoneInfo("Asia/Jakarta")).date()
    config = configuration("pilot/universe.json")
    ids = {i["id"] for i in config["instruments"]}
    if len(ids) != 11 or config["benchmark"] != "JKSE.INDX":
        raise ValueError("Expected fixed 10-equity + JKSE panel.")
    root = Path("data/collector-output/pilot")
    state = local_state()
    result = {
        "title": "Pilot Daily Plan", "current_jakarta_date": str(today), "policy": "H+1",
        "latest_successful_canonical_session": None, "next_candidate_session": None,
        "pending_sessions": [], "session_proof": "MISSING", "database": state["status"],
        "panel": "10 equities + JKSE.INDX", "universe_mode": "Pilot", "FullIdx": "NOT ENABLED",
        "maximum_provider_units": 11, "soak": soak_progress(root),
        "account": "not queried during planning", "provider_requests": 0,
        "decision": "BLOCKED", "reason": "DATA_BLOCKED",
    }
    current = {(r["instrument_id"], r["date"]): r for r in state.get("revisions", [])}
    if (state["status"] != "KNOWN" or state.get("database") != "idx_stock_intelligence"
            or {key[0] for key in current} != ids):
        return result

    completed = []
    for path in root.glob("*.operation.json"):
        try:
            operation = json.loads(path.read_text())
            worker, ledger = operation["worker"], operation["ledger"]
            day = ledger["session_date"]
            recorded = {(r["instrument_id"], r["date"]): r for r in worker["canonical_state"]}
            if (operation["status"] == worker["run_status"] == "SUCCEEDED"
                    and ledger["mode"] == "DAILY" and ledger["final_status"] == "SUCCESS"
                    and worker["database"] == state["database"] and worker["market_date"] == day
                    and all(recorded.get((i, day)) == current.get((i, day))
                            and (i, day) in recorded for i in ids)):
                completed.append(date.fromisoformat(day))
        except (ValueError, OSError, KeyError, TypeError):
            continue  # Partial/failed reports cannot establish completed canonical sessions.
    if not completed:
        return result
    latest = max(completed)
    result["latest_successful_canonical_session"] = str(latest)
    if latest > today or any(date.fromisoformat(d) > latest for _, d in current):
        result["reason"] = "CANONICAL_STATE_AHEAD_OF_SUCCESSFUL_LEDGER"
        return result

    sessions = json.loads(Path(os.environ.get("IDX_PILOT_SESSIONS", "pilot/sessions.json")).read_text())
    cutoff = safe_cutoff()

    def proof(day):
        gate = collection_eligibility(day, now, sessions, cutoff)
        if gate["eligible"]:
            row = next(p for p in sessions if p["date"] == str(day))
            completed_at = datetime.fromisoformat(row["completed_at"].replace("Z", "+00:00")) if row.get("completed_at") else None
            known_at = datetime.fromisoformat(row["known_at"].replace("Z", "+00:00"))
            gate["eligible"] = bool(completed_at and completed_at.tzinfo is not None
                and completed_at <= known_at <= now
                and completed_at.astimezone(ZoneInfo("Asia/Jakarta")).date() == day)
            if not gate["eligible"]:
                gate["reason"] = "COMPLETED_SESSION_EVIDENCE_REQUIRED"
        return gate

    result["pending_sessions"] = sorted({p["date"] for p in sessions
        if latest < date.fromisoformat(p["date"]) < today and proof(date.fromisoformat(p["date"]))["eligible"]})
    candidate = latest + timedelta(days=1)
    while candidate < today:
        gate = proof(candidate)
        if gate["session_proof"] == "KNOWN_CLOSED":
            candidate += timedelta(days=1)
            continue
        result["next_candidate_session"] = str(candidate)
        if gate["eligible"]:
            result.update(decision="ELIGIBLE", reason="READY", session_proof="CONFIRMED")
        else:
            result.update(decision="WAITING_FOR_SESSION_PROOF", reason=gate["reason"])
        return result
    result.update(decision="NO_NEW_SESSION", reason="NO_PENDING_PRIOR_JAKARTA_SESSION")
    return result
