"""Offline date selection for the unattended daily pilot.

This module answers one question without touching the network: which completed
Jakarta trading day should a single unattended run collect? It never proves a
session and never substitutes a calendar for independent session evidence; the
collector still applies its own eligibility rules. Dates are only proposed from
the cited exchange-holiday fixture, weekends, and an explicit override.
"""
from __future__ import annotations

import argparse
from datetime import date, datetime, timedelta, timezone
import json
import os
from pathlib import Path
import sys
from zoneinfo import ZoneInfo

DEFAULT_HOLIDAYS = Path("pilot/holidays-2026.json")
DEFAULT_JOURNAL = Path("data/collector-output/pilot/auto-journal.json")


class CalendarError(ValueError):
    """The requested date cannot be selected without guessing or fabricating."""


def holidays_path() -> Path:
    return Path(os.environ.get("IDX_PILOT_HOLIDAYS", DEFAULT_HOLIDAYS))


def journal_path() -> Path:
    return Path(os.environ.get("IDX_PILOT_AUTO_JOURNAL", DEFAULT_JOURNAL))


def load_holidays(path: str | Path | None = None) -> tuple[int, dict[date, dict]]:
    """Load the cited exchange-holiday fixture; every entry must carry a source."""
    document = json.loads(Path(path or holidays_path()).read_text())
    entries = document["holidays"] if isinstance(document, dict) else document
    year = document.get("year") if isinstance(document, dict) else None
    if type(year) is not int:
        raise CalendarError("Holiday fixture must declare its covered year.")
    holidays: dict[date, dict] = {}
    for entry in entries:
        day = date.fromisoformat(entry["date"])
        if day.year != year:
            raise CalendarError(f"{day} is outside the fixture year {year}.")
        if day.weekday() >= 5:
            raise CalendarError(f"{day} is a weekend; weekends are not holidays.")
        if not str(entry.get("reference", "")).startswith("https://") or not entry.get("known_at"):
            raise CalendarError(f"{day} lacks a retained source URL and known_at.")
        if day in holidays:
            raise CalendarError(f"{day} is listed twice.")
        holidays[day] = entry
    return year, dict(sorted(holidays.items()))


def latest_trading_day(today: date, holidays: dict[date, dict], year: int,
                       max_lookback: int = 400) -> date:
    """Most recent weekday strictly before today that is not a listed holiday."""
    candidate = today - timedelta(days=1)
    for _ in range(max_lookback):
        if candidate.year != year:
            raise CalendarError(
                f"Holiday coverage for {year} does not include {candidate}; update the fixture.")
        if candidate.weekday() < 5 and candidate not in holidays:
            return candidate
        candidate -= timedelta(days=1)
    raise CalendarError("No eligible trading day inside the lookback window.")


def require_trading_day(day: date, holidays: dict[date, dict], year: int) -> None:
    """Reject an explicit override that is a weekend or a cited exchange holiday."""
    if day.year != year:
        raise CalendarError(f"Holiday coverage for {year} does not include {day}; update the fixture.")
    if day.weekday() >= 5:
        raise CalendarError(f"{day} is a weekend, not a trading day.")
    if day in holidays:
        raise CalendarError(f"{day} is a listed exchange holiday: {holidays[day].get('name', 'unnamed')}.")


def prior_attempt(journal: dict | None, target: date) -> dict | None:
    """Return the retained attempt for this date, if any, without judging it."""
    if isinstance(journal, dict) and journal.get("date") == target.isoformat():
        return {key: journal.get(key) for key in ("date", "status", "exit_code", "recorded_at")}
    return None


def build_plan(today: date, holidays: dict[date, dict], year: int, *,
               explicit: date | None = None, journal: dict | None = None,
               mode: str = "PLAN") -> dict:
    """Deterministic plan; a repeated automated date is refused unless overridden."""
    if explicit is not None:
        require_trading_day(explicit, holidays, year)
        target, selection = explicit, "EXPLICIT_OVERRIDE"
    else:
        target, selection = latest_trading_day(today, holidays, year), "LATEST_TRADING_DAY"
    prior = prior_attempt(journal, target)
    refused = prior is not None and explicit is None
    return {
        "mode": mode,
        "date": target.isoformat(),
        "selection": selection,
        "calendar_year": year,
        "jakarta_today": today.isoformat(),
        "holiday_count": len(holidays),
        "provider_requests": 0,
        "note": "Planning reads local fixtures only; the collector still proves the session.",
        "prior_attempt": prior,
        "refused": refused,
        "refusal_reason": "AUTOMATED_DATE_ALREADY_ATTEMPTED" if refused else None,
        "override_command": (f"bash scripts/pilot-auto.sh --date {target.isoformat()}"
                             if refused else None),
    }


def read_journal(path: str | Path | None = None) -> dict | None:
    try:
        value = json.loads(Path(path or journal_path()).read_text())
    except (OSError, ValueError):
        return None
    return value if isinstance(value, dict) else None


def write_journal(day: date, status: str, exit_code: int | None = None,
                  path: str | Path | None = None) -> Path:
    target = Path(path or journal_path())
    target.parent.mkdir(parents=True, exist_ok=True)
    record = {"date": day.isoformat(), "status": status, "exit_code": exit_code,
              "recorded_at": datetime.now(timezone.utc).isoformat()}
    temporary = target.with_suffix(target.suffix + ".tmp")
    temporary.write_text(json.dumps(record, indent=2))
    temporary.replace(target)
    return target


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)

    plan = commands.add_parser("plan", help="Print the selected date without provider calls.")
    plan.add_argument("--today", help="Jakarta date override, testing only (YYYY-MM-DD).")
    plan.add_argument("--date", help="Explicit trading-day override (YYYY-MM-DD).")
    plan.add_argument("--holidays")
    plan.add_argument("--journal")
    plan.add_argument("--mode", default="PLAN", choices=("PLAN", "DRY_RUN", "RUN"))

    record = commands.add_parser("record", help="Record one attempt in the local journal.")
    record.add_argument("--date", required=True)
    record.add_argument("--status", required=True, choices=("STARTED", "FINISHED"))
    record.add_argument("--exit-code", type=int)
    record.add_argument("--journal")

    args = parser.parse_args(argv)
    try:
        if args.command == "plan":
            year, holidays = load_holidays(args.holidays)
            today = (date.fromisoformat(args.today) if args.today
                     else datetime.now(timezone.utc).astimezone(ZoneInfo("Asia/Jakarta")).date())
            explicit = date.fromisoformat(args.date) if args.date else None
            journal = read_journal(args.journal)
            print(json.dumps(build_plan(today, holidays, year, explicit=explicit,
                                        journal=journal, mode=args.mode), indent=2))
            return 0
        target = date.fromisoformat(args.date)
        path = write_journal(target, args.status, args.exit_code, args.journal)
        print(json.dumps({"journal": str(path), "date": target.isoformat(),
                          "status": args.status, "exit_code": args.exit_code}))
        return 0
    except (CalendarError, ValueError, KeyError, OSError) as error:
        print(f"STOP: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
