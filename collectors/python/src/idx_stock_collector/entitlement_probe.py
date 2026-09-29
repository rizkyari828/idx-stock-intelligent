"""Isolated quota experiment: no archive, normalization, database or soak imports."""
import argparse
from datetime import date, datetime, timezone
from decimal import Decimal
import json
import os
from pathlib import Path
import urllib.parse
import urllib.request
from zoneinfo import ZoneInfo

VERIFIED = "EXTRA CALL BUFFER VERIFIED"
NOT_VERIFIED = "EXTRA CALL BUFFER NOT VERIFIED"
NOT_ELIGIBLE = "EXTRA CALL BUFFER TEST NOT ELIGIBLE"
SYMBOLS = ("BBCA.JK", "ANTM.JK", "GOTO.JK")


def plan(used, limit, extra, probes=3, cap=10, approved=False):
    if (any(type(v) is not int or v < 0 for v in (used, limit, extra, probes, cap))
            or limit != 20 or probes < 1 or cap not in (10, 12)
            or (cap == 12 and approved is not True)):
        raise ValueError("Unknown counters or unapproved limits.")
    ordinary = max(0, limit - used)
    if extra < probes or ordinary + probes > cap:
        raise ValueError("Insufficient extra balance or request cap.")
    return {"ordinary_remaining": ordinary, "extra_probes": probes,
            "total_billable_requests": ordinary + probes, "hard_cap": cap,
            "expected_extra_after": extra - probes}


def counters(payload, today):
    account = json.loads(payload)
    if (not isinstance(account, dict) or str(account.get("subscriptionType", "")).casefold() != "free"
            or account.get("apiRequestsDate") != today.isoformat()):
        raise ValueError("Free current-day counters required.")
    state = {"daily_used": account.get("apiRequests"), "daily_limit": account.get("dailyRateLimit"),
             "extra_balance": account.get("extraLimit"), "counter_date": account.get("apiRequestsDate")}
    if (any(type(state[k]) is not int or state[k] < 0 for k in ("daily_used", "daily_limit", "extra_balance"))
            or state["daily_limit"] != 20):
        raise ValueError("Unknown Free counters.")
    return state  # Account identity, token and all other fields are discarded.


def valid_eod(payload, requested_date):
    """Transient shape/bounds check, deliberately produces no normalized records."""
    try:
        rows = json.loads(payload, parse_float=Decimal)
        if not isinstance(rows, list) or len(rows) != 1 or not isinstance(rows[0], dict):
            return False
        row = rows[0]
        if row.get("date") != requested_date.isoformat() or type(row.get("volume")) is not int or row["volume"] < 0:
            return False
        prices = [row[k] for k in ("open", "high", "low", "close", "adjusted_close")]
        if any(type(p) not in (int, Decimal) or not Decimal(p).is_finite() or p <= 0 for p in prices):
            return False
        opening, high, low, close, _ = prices
        return low <= min(opening, close) <= max(opening, close) <= high
    except (ValueError, KeyError, TypeError):
        return False


def reconcile(before, after, planned, attempts):
    if (after is None or len(attempts) != planned["total_billable_requests"]
            or not all(a["payload_valid"] for a in attempts)
            or before["counter_date"] != after["counter_date"]
            or before["daily_limit"] != after["daily_limit"]
            or before["extra_balance"] - after["extra_balance"] != planned["extra_probes"]):
        return NOT_VERIFIED
    # Providers may cap the ordinary counter or include overflow in the usage counter.
    expected = {max(before["daily_used"], before["daily_limit"]),
                before["daily_used"] + planned["total_billable_requests"]}
    return VERIFIED if after["daily_used"] in expected else NOT_VERIFIED


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *args, **kwargs):
        return None


def transport(token):
    opener = urllib.request.build_opener(NoRedirect)

    def request(path, parameters):
        # Only this fixed whitelist can reach the network. No retries or redirects.
        if path != "user" and path not in tuple("eod/" + s for s in SYMBOLS):
            raise ValueError("Unsupported endpoint.")
        url = "https://eodhd.com/api/" + path + "?" + urllib.parse.urlencode(dict(parameters, api_token=token))
        with opener.open(url, timeout=30) as response:
            payload = response.read(2 * 1024 * 1024 + 1)
            if response.status != 200 or len(payload) > 2 * 1024 * 1024:
                raise ValueError("Request rejected.")
            return payload
    return request


def execute(request, requested_date, probes=3, cap=10, approved=False, clock=lambda: datetime.now(timezone.utc)):
    before = after = planned = None
    attempts = []
    try:
        today = clock().date()
        before = counters(request("user", {"fmt": "json"}), today)
        planned = plan(before["daily_used"], before["daily_limit"], before["extra_balance"], probes, cap, approved)
    except Exception:
        return NOT_ELIGIBLE, {"reason": "PREFLIGHT_FAILED", "billable_attempts": 0}
    try:
        for number in range(planned["total_billable_requests"]):
            if clock().date() != today:
                break  # Never cross a quota reset and reinterpret the counters.
            symbol = SYMBOLS[number % len(SYMBOLS)]
            attempt = {"request_number": number + 1, "symbol": symbol, "http_success": False,
                       "payload_valid": False, "units_expected": 1}
            attempts.append(attempt)  # A failed/uncertain attempt still consumes the hard cap.
            try:
                payload = request("eod/" + symbol, {"from": requested_date.isoformat(), "to": requested_date.isoformat(),
                                                  "period": "d", "fmt": "json"})
                attempt.update(http_success=True, payload_valid=valid_eod(payload, requested_date))
                del payload
            except Exception:
                break  # Never print credential-bearing exception text.
            if not attempt["payload_valid"]:
                break
        after = counters(request("user", {"fmt": "json"}), clock().date())
        verdict = reconcile(before, after, planned, attempts)
    except Exception:
        verdict = NOT_VERIFIED
    return verdict, {"before": before, "after": after, "plan": planned,
                     "billable_attempts": len(attempts), "attempts": attempts}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--extra-probes", type=int, default=3)
    parser.add_argument("--hard-cap", type=int, choices=(10, 12), default=10)
    parser.add_argument("--approve-cap-12", action="store_true")
    parser.add_argument("--daily-used", type=int)
    parser.add_argument("--daily-limit", type=int)
    parser.add_argument("--extra-balance", type=int)
    parser.add_argument("--date", type=date.fromisoformat)
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()
    verdict, details = NOT_ELIGIBLE, {"reason": "INVALID_CONFIGURATION"}
    try:
        if args.dry_run:
            details = dict(plan(args.daily_used, args.daily_limit, args.extra_balance,
                                args.extra_probes, args.hard_cap, args.approve_cap_12),
                           mode="DRY_RUN", provider_requests=0, reason="NO_TEST_EXECUTED")
        else:
            if any(v is not None for v in (args.daily_used, args.daily_limit, args.extra_balance)):
                raise ValueError("Live counters cannot be overridden.")
            now = datetime.now(timezone.utc)
            today = now.astimezone(ZoneInfo("Asia/Jakarta")).date()
            day = args.date
            if day is None:
                sessions = json.loads(Path("pilot/sessions.json").read_text())
                day = max(date.fromisoformat(p["date"]) for p in sessions if p["status"] == "ObservedTrading"
                          and datetime.fromisoformat(p["known_at"]) <= now and date.fromisoformat(p["date"]) < today)
            if not 0 < (today - day).days <= 330:
                raise ValueError("Use a tiny prior-date Free window.")
            # Validate cap/probe options before even a zero-unit account request.
            plan(20, 20, args.extra_probes, args.extra_probes, args.hard_cap, args.approve_cap_12)
            token = os.environ.get("EODHD_API_TOKEN", "")
            if not token:
                raise ValueError("Provider token absent.")
            verdict, details = execute(transport(token), day, args.extra_probes, args.hard_cap, args.approve_cap_12)
    except Exception:
        pass  # No exception bodies, account identity, URLs or market values in output.
    print(json.dumps(details))
    print(verdict)
    raise SystemExit(0 if verdict == VERIFIED or args.dry_run and "total_billable_requests" in details else 2)


if __name__ == "__main__":
    main()
