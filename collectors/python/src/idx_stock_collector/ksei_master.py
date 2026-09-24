"""Strict, local-only inspection of one KSEI master securities ZIP snapshot."""

from __future__ import annotations

import csv
import json
from argparse import ArgumentParser
from dataclasses import dataclass
from datetime import datetime
from io import StringIO
from pathlib import Path
from zipfile import ZipFile

PARSER_VERSION = "ksei-master-inspect-1"

EXPECTED_COLUMNS = (
    "Date", "Code", "Description", "Type", "Isin Code", "Issuer", "Status",
    "Stock Exchange", "Listing Date", "Currency", "Form", "Eff. Date Isin",
    "Maturity Date", "Expire Date", "Exercise Price", "Interest",
    "Interest Type", "Interest Freq", "Nominal Value", "Num. of Sec",
    "Originated Amt", "Current Amt", "Total Scripless", "Local (%)",
    "Foreign (%)", "Total (%)", "Sector", "Closing Price",
)


@dataclass(frozen=True)
class SecurityCandidate:
    snapshot_date: str
    code: str
    isin: str
    issuer: str
    security_type: str
    listing_date: str | None


@dataclass(frozen=True)
class Inspection:
    total_records: int
    equity_candidates: tuple[SecurityCandidate, ...]
    unsupported_types: dict[str, int]
    malformed_lines: tuple[int, ...]
    missing_equity_listing_dates: int


def _parse_date(value: str, *, required: bool) -> str | None:
    if not value:
        if required:
            raise ValueError("required date is missing")
        return None
    return datetime.strptime(value, "%d-%b-%Y").date().isoformat()


def inspect_text(text: str) -> Inspection:
    reader = csv.reader(StringIO(text), delimiter="|")
    header = next(reader, None)
    if tuple(header or ()) != EXPECTED_COLUMNS:
        raise ValueError("KSEI master schema drift: unexpected header")

    equity: list[SecurityCandidate] = []
    unsupported: dict[str, int] = {}
    malformed: list[int] = []
    seen_isin: set[str] = set()
    seen_code: set[str] = set()
    total = 0
    missing_dates = 0

    for line_number, row in enumerate(reader, start=2):
        if len(row) != len(EXPECTED_COLUMNS):
            malformed.append(line_number)
            continue
        total += 1
        values = dict(zip(EXPECTED_COLUMNS, row, strict=True))
        kind = values["Type"].strip()
        if kind != "EQUITY":
            unsupported[kind or "<MISSING>"] = unsupported.get(kind or "<MISSING>", 0) + 1
            continue

        code = values["Code"].strip().upper()
        isin = values["Isin Code"].strip().upper()
        issuer = values["Issuer"].strip()
        if not code or not isin or not issuer:
            raise ValueError(f"missing equity identity field at line {line_number}")
        if isin in seen_isin or code in seen_code:
            raise ValueError(f"duplicate equity code or ISIN at line {line_number}")
        seen_isin.add(isin)
        seen_code.add(code)

        snapshot_date = _parse_date(values["Date"].strip(), required=True)
        listing_date = _parse_date(values["Listing Date"].strip(), required=False)
        if listing_date is None:
            missing_dates += 1
        equity.append(SecurityCandidate(
            snapshot_date=snapshot_date or "",
            code=code,
            isin=isin,
            issuer=issuer,
            security_type=kind,
            listing_date=listing_date,
        ))

    return Inspection(total, tuple(equity), unsupported, tuple(malformed), missing_dates)


def inspect_zip(path: Path) -> Inspection:
    with ZipFile(path) as archive:
        members = archive.infolist()
        if len(members) != 1 or members[0].is_dir() or members[0].file_size > 10_000_000:
            raise ValueError("expected one bounded KSEI master text file")
        with archive.open(members[0]) as member:
            payload = member.read(10_000_001)
        if len(payload) > 10_000_000:
            raise ValueError("KSEI master text file exceeds 10 MB")
    return inspect_text(payload.decode("utf-8-sig"))


def main() -> None:
    parser = ArgumentParser(description=__doc__)
    parser.add_argument("zip_path", type=Path, help="An already archived KSEI master ZIP")
    args = parser.parse_args()
    result = inspect_zip(args.zip_path)
    print(json.dumps({
        "parser_version": PARSER_VERSION,
        "total_records": result.total_records,
        "equity_candidates": len(result.equity_candidates),
        "missing_equity_listing_dates": result.missing_equity_listing_dates,
        "malformed_lines": result.malformed_lines,
        "unsupported_types": result.unsupported_types,
        "canonical_ingestion_allowed": False,
    }, sort_keys=True))


if __name__ == "__main__":
    main()
