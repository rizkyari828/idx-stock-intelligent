"""Synthetic clocks/evidence only; provider I/O is forbidden."""
from datetime import date, datetime, time, timezone
import json
import os
from pathlib import Path
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

from idx_stock_collector.pilot import collection_eligibility, collect, dry_run, safe_cutoff


class CompletedSessionTests(unittest.TestCase):
    day=date(2026,9,29)
    proof={"date":"2026-09-29","status":"ObservedTrading","reference":"https://reference.example/closing-report",
           "known_at":"2026-09-29T10:00:00Z","completed_at":"2026-09-29T09:15:00Z"}

    def test_clock_proof_cutoff_and_timezone_matrix(self):
        def clock(text):
            return datetime.fromisoformat(text)
        cases=[
            (date(2026,9,29),"2026-09-30T00:00:00+00:00",[self.proof],time(19),"ELIGIBLE_PRIOR_COMPLETED_SESSION"),
            (self.day,"2026-09-29T11:59:59+00:00",[self.proof],time(19),"SAFE_EOD_CUTOFF_NOT_REACHED"),
            (self.day,"2026-09-29T12:00:00+00:00",[self.proof],time(19),"ELIGIBLE_SAME_DAY_COMPLETED_SESSION"),
            (self.day,"2026-09-29T12:30:00+00:00",[],time(19),"SESSION_PROOF_REQUIRED"),
            (self.day,"2026-09-29T12:30:00+00:00",[dict(self.proof,status="AnnouncedClosed")],time(19),"KNOWN_CLOSED"),
            (self.day,"2026-09-29T12:30:00+00:00",[self.proof],time(19),"ELIGIBLE_SAME_DAY_COMPLETED_SESSION"),
            (date(2026,9,30),"2026-09-29T12:30:00+00:00",[],time(19),"FUTURE_DATE"),
            (self.day,"2026-09-28T18:00:00+00:00",[self.proof],time(19),"SAFE_EOD_CUTOFF_NOT_REACHED"),
            (self.day,"2026-09-29T18:00:00+00:00",[self.proof],time(19),"ELIGIBLE_PRIOR_COMPLETED_SESSION"),
            (self.day,"2026-09-29T12:30:00+00:00",[self.proof],time(20),"SAFE_EOD_CUTOFF_NOT_REACHED"),
            (self.day,"2026-09-29T13:00:00+00:00",[self.proof],time(20),"ELIGIBLE_SAME_DAY_COMPLETED_SESSION"),
            (self.day,"2026-09-29T12:30:00+00:00",[{k:v for k,v in self.proof.items() if k!="completed_at"}],time(19),"COMPLETED_SESSION_EVIDENCE_REQUIRED"),
            (self.day,"2026-09-29T12:30:00+00:00",[dict(self.proof,known_at="2026-09-29T13:00:00Z")],time(19),"INDEPENDENT_ALREADY_KNOWN_PROOF_REQUIRED"),
            (self.day,"2026-09-29T12:30:00+00:00",[dict(self.proof,reference="https://eodhd.com/row")],time(19),"INDEPENDENT_ALREADY_KNOWN_PROOF_REQUIRED"),
            (self.day,"2026-09-29T12:30:00+00:00",[dict(self.proof,completed_at="2026-09-29T11:00:00Z")],time(19),"COMPLETED_SESSION_EVIDENCE_REQUIRED"),
            (self.day,"2026-09-29T12:30:00+00:00",[self.proof,self.proof],time(19),"CONFLICTING_SESSION_PROOF"),
        ]
        for day,now,proofs,cutoff,reason in cases:
            with self.subTest(day=day,now=now,reason=reason):
                result=collection_eligibility(day,clock(now),proofs,cutoff)
                self.assertEqual(reason,result["reason"])
                self.assertEqual(reason.startswith("ELIGIBLE_"),result["eligible"])
        with self.assertRaises(ValueError):
            collection_eligibility(self.day,datetime(2026,9,29),[],time(19))

    def test_shared_calendar_policy_fixture(self):
        fixture = Path(__file__).resolve().parents[3] / "tests/fixtures/calendar-policy.json"
        clock = datetime(2026, 10, 6, 12, tzinfo=timezone.utc)
        for row in json.loads(fixture.read_text()):
            proofs = [] if row["status"] is None else [dict(date=row["date"], status=row["status"],
                reference="https://reference.example/synthetic", known_at="2026-10-05T12:00:00Z")]
            with self.subTest(row=row):
                result = collection_eligibility(date.fromisoformat(row["date"]), clock, proofs, time(19))
                self.assertEqual(row["reason"], result["reason"])
                if row["reason"] == "CLOSED_BY_CALENDAR":
                    self.assertEqual("KNOWN_CLOSED", result["session_proof"])

    def test_dry_run_reports_gate_and_live_rejects_before_any_provider_io(self):
        root=Path(__file__).resolve().parents[3]
        original=Path.cwd()
        with tempfile.TemporaryDirectory() as directory:
            try:
                os.chdir(directory)
                Path("pilot").mkdir()
                Path("pilot/universe.json").write_text(root.joinpath("pilot/universe.json").read_text())
                Path("pilot/sessions.json").write_text(json.dumps([self.proof]))
                policy=Path("pilot/collection-policy.json")
                policy.write_text(json.dumps({"SAFE_EOD_CUTOFF":"19:00"}))
                args=SimpleNamespace(universe="pilot/universe.json",start=str(self.day),end=str(self.day),
                    refresh=False,resume=None,seed=[],offline=False)
                with patch("idx_stock_collector.pilot.datetime") as clock, \
                     patch("idx_stock_collector.pilot.local_state",return_value={"status":"KNOWN","revisions":[]}), \
                     patch("urllib.request.build_opener",side_effect=AssertionError("Provider I/O forbidden")):
                    clock.fromisoformat.side_effect=datetime.fromisoformat
                    clock.now.return_value=datetime(2026,9,29,11,59,tzinfo=timezone.utc)
                    plan=dry_run(args)
                    self.assertEqual("NOT_ELIGIBLE",plan["collection"])
                    self.assertEqual("SAFE_EOD_CUTOFF_NOT_REACHED",plan["collection_eligibility"][0]["reason"])
                    self.assertEqual(0,plan["provider_requests"])
                    self.assertEqual(0,plan["maximum_eod_units"])
                    with self.assertRaises(ValueError):
                        collect(args)
                    self.assertFalse(Path("data/collector-output/pilot").exists())
                    clock.now.return_value=datetime(2026,9,29,12,tzinfo=timezone.utc)
                    self.assertEqual("ELIGIBLE",dry_run(args)["collection"])
                    policy.write_text(json.dumps({"SAFE_EOD_CUTOFF":"20:00"}))
                    self.assertEqual("NOT_ELIGIBLE",dry_run(args)["collection"])
                    for invalid in ("7pm","24:00","19:00:00",None):
                        policy.write_text(json.dumps({"SAFE_EOD_CUTOFF":invalid}))
                        with self.assertRaises(ValueError):
                            safe_cutoff()
            finally:
                os.chdir(original)
