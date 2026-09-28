"""Inspect EODHD daily rows without asserting exchange-session or volume semantics."""

from datetime import date
from decimal import Decimal
import json

PARSER_VERSION = "eodhd-experimental-1"
PRICE_FIELDS = ("open", "high", "low", "close", "adjusted_close")


def parse_daily(payload: bytes) -> list[dict]:
    """Validate and sort provider rows; archive original bytes separately first.

    Zero-volume rows remain provider evidence, not confirmed tradable sessions.
    No missing dates are inserted and neither prices nor volume are adjusted here.
    """
    rows = json.loads(payload, parse_float=Decimal)
    if not isinstance(rows, list):
        raise ValueError("expected a daily row array")
    result = []
    seen = set()
    for row in rows:
        if not isinstance(row, dict) or not {"date", "volume", *PRICE_FIELDS} <= row.keys():
            raise ValueError("missing required daily fields")
        session = row["date"]
        if not isinstance(session, str) or date.fromisoformat(session).isoformat() != session:
            raise ValueError("expected YYYY-MM-DD provider date")
        if session in seen:
            raise ValueError("duplicate provider date")
        seen.add(session)
        prices = {}
        for field in PRICE_FIELDS:
            value = row[field]
            if type(value) not in (int, Decimal):
                raise ValueError("price must be a JSON number")
            price = Decimal(value)
            if not price.is_finite() or price <= 0:
                raise ValueError("price must be finite and positive")
            prices[field] = price
        if not prices["low"] <= min(prices["open"], prices["close"]) <= max(prices["open"], prices["close"]) <= prices["high"]:
            raise ValueError("invalid OHLC bounds")
        if type(row["volume"]) is not int or row["volume"] < 0:
            raise ValueError("volume must be a nonnegative JSON integer")
        result.append(dict(date=session, **prices, volume=row["volume"]))
    return sorted(result, key=lambda row: row["date"])
