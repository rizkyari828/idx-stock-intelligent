"""Personal (non-canonical) store: opt-in matrix, append-only revisions, no ingestion."""
from copy import deepcopy
from datetime import date, datetime, time, timezone
import hashlib
import io
import json
import os
from pathlib import Path
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

from idx_stock_collector.contract import archive_payload
from idx_stock_collector.pilot import (PERSONAL_SCHEMA_VERSION, collect, collection_eligibility,
    collection_plan, main, personal_capture_document, personal_row_digest, write_personal_capture)

REPO = Path(__file__).resolve().parents[3]
SESSION = "2026-10-26"
PAYLOAD = (b'[{"date":"2026-10-26","open":100.25,"high":101.5,"low":99.75,'
           b'"close":100.5,"adjusted_close":100.5,"volume":1234567}]')


def proof(day, status="ObservedTrading", reference="https://reference.example/session"):
    return {"date": day, "status": status, "reference": reference,
            "completed_at": day + "T16:30:00+07:00", "known_at": day + "T17:00:00+07:00"}


class PersonalStoreTests(unittest.TestCase):
    def setUp(self):
        self.original = Path.cwd()
        self.temporary = tempfile.TemporaryDirectory()
        os.chdir(self.temporary.name)
        Path("pilot").mkdir()
        Path("pilot/universe.json").write_text((REPO / "pilot/universe.json").read_text())
        Path("pilot/collection-policy.json").write_text('{"SAFE_EOD_CUTOFF":"19:00"}')
        Path("pilot/holidays-2026.json").write_text((REPO / "pilot/holidays-2026.json").read_text())
        Path("pilot/soak.json").write_text('{"after_market_date":"2026-09-28","required_completed_runs":10}')
        Path("pilot/sessions.json").write_text("[]")
        self.config = json.loads(Path("pilot/universe.json").read_text())
        self.network = patch("urllib.request.OpenerDirector.open",
                             side_effect=AssertionError("Provider I/O forbidden"))
        self.network.start()

    def tearDown(self):
        self.network.stop()
        os.chdir(self.original)
        self.temporary.cleanup()

    def cutoff(self):
        return time(19, 0)

    def capture_run(self, status="SUCCEEDED", session_proof="UNPROVEN_PERSONAL", close="100.5"):
        raw = PAYLOAD.replace(b"100.5", close.encode(), 1)
        entries = []
        for instrument in self.config["instruments"]:
            manifest = archive_payload(raw, Path("data/raw/pilot"), source_id="eodhd",
                requested_uri="https://eodhd.com/api/eod/" + instrument["symbol"],
                parser_version="fixture", extension=".json",
                request_parameters={"from": SESSION, "to": SESSION})
            entries.append({"instrument_id": instrument["id"], "symbol": instrument["symbol"],
                "status": "AVAILABLE", "manifest": manifest, "raw_root": "data/raw/pilot",
                "rows": [{"date": SESSION, "open": "100.25", "high": "101.5", "low": "99.75",
                          "close": close, "adjusted_close": close, "volume": 1234567}]})
        return {"from": SESSION, "to": SESSION, "status": status, "entries": entries,
                "collection_eligibility": [{"date": SESSION, "session_proof": session_proof,
                                            "eligible": True, "reason": "fixture"}]}

    def test_opt_in_matrix_keeps_every_other_refusal(self):
        now = datetime.fromisoformat("2026-10-27T02:00:00+00:00")  # 09:00 Jakarta
        def gate(day, when=now, sessions=(), allow=False):
            result = collection_eligibility(date.fromisoformat(day), when, list(sessions),
                                            self.cutoff(), allow)
            return result["session_proof"], result["reason"], result["eligible"]
        self.assertEqual(("UNKNOWN_SESSION", "SESSION_PROOF_REQUIRED", False), gate("2026-10-26"))
        self.assertEqual(("UNPROVEN_PERSONAL", "ELIGIBLE_UNPROVEN_PERSONAL", True),
                         gate("2026-10-26", allow=True))
        for day, reason in (("2026-10-28", "FUTURE_DATE"),
                            ("2026-10-27", "SAFE_EOD_CUTOFF_NOT_REACHED"),
                            ("2026-10-24", "CLOSED_BY_CALENDAR")):
            self.assertEqual(reason, gate(day, allow=True)[1])
            self.assertFalse(gate(day, allow=True)[2])
        after_cutoff = gate("2026-10-27", datetime.fromisoformat("2026-10-27T13:00:00+00:00"), allow=True)
        self.assertEqual("ELIGIBLE_UNPROVEN_PERSONAL", after_cutoff[1])
        self.assertEqual(("CONFLICTING_SESSION_PROOF", False),
                         gate("2026-10-26", sessions=[proof("2026-10-26"), proof("2026-10-26")], allow=True)[1:])
        self.assertEqual(("INDEPENDENT_ALREADY_KNOWN_PROOF_REQUIRED", False),
                         gate("2026-10-26", sessions=[proof("2026-10-26", reference="https://eodhd.com/row")],
                              allow=True)[1:])
        self.assertEqual(("KNOWN_CLOSED", False),
                         gate("2026-10-26", sessions=[proof("2026-10-26", status="AnnouncedClosed")],
                              allow=True)[1:])
        plan = collection_plan(date(2026, 10, 22), date(2026, 10, 24), now, allow_unproven=True)
        self.assertEqual(["2026-10-22", "2026-10-23"], [p["date"] for p in plan if p["eligible"]])
        self.assertEqual("CLOSED_BY_CALENDAR", plan[-1]["reason"])
        self.assertEqual(["SESSION_PROOF_REQUIRED"],
                         [p["reason"] for p in collection_plan(date(2026, 10, 23), date(2026, 10, 23), now)])

    def test_cited_holiday_is_refused_even_with_the_opt_in(self):
        now = datetime.fromisoformat("2026-10-27T02:00:00+00:00")
        with self.assertRaises(ValueError) as holiday:
            collection_eligibility(date(2026, 8, 17), now, [], self.cutoff(), True)
        self.assertIn("cited exchange holiday", str(holiday.exception))
        with self.assertRaises(ValueError) as uncovered:
            collection_eligibility(date(2027, 1, 4), datetime.fromisoformat("2027-02-01T02:00:00+00:00"),
                                   [], self.cutoff(), True)
        self.assertIn("does not include", str(uncovered.exception))
        self.assertFalse(collection_eligibility(date(2026, 8, 17), now, [], self.cutoff())["eligible"])
        args = SimpleNamespace(universe="pilot/universe.json", start="2026-08-17", end="2026-08-17",
                               resume=None, seed=[], offline=False, daily=True, personal=True,
                               ingest=True, refresh=False)
        with patch.dict(os.environ, {"EODHD_API_TOKEN": "fixture-only-sentinel"}), \
             patch("idx_stock_collector.pilot.datetime") as clock:
            clock.now.return_value = datetime(2026, 10, 27, tzinfo=timezone.utc)
            clock.fromisoformat.side_effect = datetime.fromisoformat
            with self.assertRaises(ValueError):
                collect(args)
        self.assertFalse(Path("data/personal").exists())

    def test_document_fields_identical_recapture_and_revision_naming(self):
        run = self.capture_run()
        document = personal_capture_document(run)
        self.assertEqual(PERSONAL_SCHEMA_VERSION, document["schema_version"])
        self.assertEqual(SESSION, document["session_date"])
        self.assertEqual("UNPROVEN_PERSONAL", document["session_proof"])
        self.assertEqual("eodhd", document["source_id"])
        self.assertEqual(11, len(document["rows"]))
        expected_artifact = hashlib.sha256(PAYLOAD).hexdigest()
        for row in document["rows"]:
            for field in ("open", "high", "low", "close", "adjusted_close"):
                self.assertIsInstance(row[field], str)
            self.assertIsInstance(row["volume"], int)
            self.assertEqual("100.25", row["open"])
            self.assertEqual(expected_artifact, row["artifact_sha256"])
            self.assertEqual("UNPROVEN_PERSONAL", row["quality_status"])
            self.assertEqual(personal_row_digest(SESSION, row), row["content_sha256"])
            self.assertNotEqual(row["artifact_sha256"], row["content_sha256"])
            self.assertTrue(row["fetched_at"].endswith("+00:00"))
        first = write_personal_capture(document)
        self.assertEqual(("WRITTEN", 1), (first["status"], first["revision"]))
        self.assertEqual(f"data/personal/bars/2026/{SESSION}.json", first["path"])
        self.assertEqual(json.dumps(document, indent=2).encode(), Path(first["path"]).read_bytes())
        self.assertEqual(dict(first, status="UNCHANGED"), write_personal_capture(document))
        path = Path(first["path"])
        original = path.read_bytes()
        changed = deepcopy(document)
        changed["rows"][0]["close"] = "101.5"
        second = write_personal_capture(changed)
        self.assertEqual(("REVISION", 2), (second["status"], second["revision"]))
        self.assertTrue(second["path"].endswith(f"/{SESSION}.r2.json"))
        self.assertEqual(original, path.read_bytes())
        self.assertEqual(dict(second, status="UNCHANGED"), write_personal_capture(changed))
        again = deepcopy(document)
        again["rows"][0]["volume"] = 7654321
        self.assertEqual(3, write_personal_capture(again)["revision"])
        self.assertEqual([f"{SESSION}.json", f"{SESSION}.r2.json", f"{SESSION}.r3.json"],
                         sorted(p.name for p in path.parent.glob(f"{SESSION}*.json")))
        self.assertEqual(original, path.read_bytes())
        self.assertIsNone(personal_capture_document(dict(run, status="DEGRADED")))
        with self.assertRaises(ValueError):
            personal_capture_document(dict(run, collection_eligibility=[
                {"date": SESSION, "session_proof": "UNKNOWN_SESSION", "eligible": False}]))

    def test_unproven_session_captures_and_canonical_ingestion_is_skipped(self):
        calls = []
        class Response(io.BytesIO):
            status = 200
        def open_request(request, timeout):
            calls.append(request.full_url)
            if "/api/user?" in request.full_url:
                return Response(json.dumps({"subscriptionType": "free", "dailyRateLimit": 20,
                                            "apiRequests": 0, "apiRequestsDate": "2026-10-27",
                                            "extraLimit": 464}).encode())
            return Response(PAYLOAD)
        plan = {"decision": "ELIGIBLE", "next_candidate_session": SESSION, "mode": "PERSONAL",
                "reason": "READY_UNPROVEN_PERSONAL", "session_proof": "UNPROVEN_PERSONAL",
                "provider_requests": 0}
        with patch("idx_stock_collector.pilot_daily.daily_plan", return_value=plan), \
             patch("idx_stock_collector.pilot.local_state", return_value={"status": "UNKNOWN", "revisions": []}), \
             patch("idx_stock_collector.pilot.datetime") as clock, \
             patch("urllib.request.build_opener") as opener, \
             patch("subprocess.run", side_effect=AssertionError("the worker must not run")), \
             patch("sys.argv", ["pilot", "--daily", "--personal", "--ingest"]), \
             patch.dict(os.environ, {"EODHD_API_TOKEN": "fixture-only-sentinel"}), \
             patch("sys.stdout", new_callable=io.StringIO) as output:
            clock.now.return_value = datetime(2026, 10, 27, tzinfo=timezone.utc)
            clock.fromisoformat.side_effect = datetime.fromisoformat
            opener.return_value.open.side_effect = open_request
            with self.assertRaises(SystemExit) as exit_status:
                main()
        self.assertEqual(0, exit_status.exception.code)
        printed = output.getvalue()
        self.assertIn("PERSONAL-CAPTURE: SUCCEEDED WRITTEN", printed)
        self.assertIn("CANONICAL-INGESTION: SKIPPED_UNPROVEN_SESSION", printed)
        self.assertIn("because the session is unproven", printed)
        self.assertIn("worker was not called", printed)
        self.assertEqual(11, sum("/api/eod/" in url for url in calls))
        capture = Path("data/personal/bars/2026") / f"{SESSION}.json"
        self.assertTrue(capture.is_file())
        document = json.loads(capture.read_text())
        self.assertEqual("UNPROVEN_PERSONAL", document["session_proof"])
        self.assertEqual(11, len(document["rows"]))
        self.assertEqual(sorted(i["symbol"] for i in self.config["instruments"]),
                         sorted(row["symbol"] for row in document["rows"]))
        batches = list(Path("data/collector-output/pilot").glob("*.json"))
        self.assertEqual(1, len(batches))
        batch = json.loads(batches[0].read_text())
        self.assertEqual("SUCCEEDED", batch["status"])
        self.assertEqual(("WRITTEN", capture.as_posix()),
                         (batch["personal_store"]["status"], batch["personal_store"]["path"]))
        self.assertEqual([], list(Path("data/collector-output/pilot").glob("*.operation.json")))

    def test_strict_collection_still_refuses_an_unproven_session(self):
        args = SimpleNamespace(universe="pilot/universe.json", start=SESSION, end=SESSION,
                               resume=None, seed=[], offline=False, daily=True, personal=False,
                               ingest=True, refresh=False)
        with patch("idx_stock_collector.pilot.datetime") as clock, \
             patch.dict(os.environ, {"EODHD_API_TOKEN": "fixture-only-sentinel"}), \
             patch("urllib.request.build_opener", side_effect=AssertionError("no provider request")):
            clock.now.return_value = datetime(2026, 10, 27, tzinfo=timezone.utc)
            clock.fromisoformat.side_effect = datetime.fromisoformat
            with self.assertRaises(ValueError) as raised:
                collect(args)
        self.assertIn("SESSION_PROOF_REQUIRED", str(raised.exception))
        self.assertFalse(Path("data/personal").exists())


if __name__ == "__main__":
    unittest.main()
