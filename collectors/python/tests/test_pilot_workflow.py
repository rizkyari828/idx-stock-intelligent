"""Offline orchestration checks; any attempted provider I/O fails the test."""
from copy import deepcopy
from datetime import date, datetime, timezone
import io
import json
import os
from pathlib import Path
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

from idx_stock_collector.contract import archive_payload
from idx_stock_collector.eodhd_experimental import PARSER_VERSION
from idx_stock_collector.pilot import (collect, configuration, dry_run, ingest_and_summarize,
                                      main, reusable_entries, soak_progress, soak_report, window)


class WorkflowTests(unittest.TestCase):
    def setUp(self):
        self.original = Path.cwd()
        self.temporary = tempfile.TemporaryDirectory()
        os.chdir(self.temporary.name)
        Path("pilot").mkdir()
        self.config = json.loads((Path(__file__).resolve().parents[3] / "pilot/universe.json").read_text())
        Path("pilot/universe.json").write_text(json.dumps(self.config))
        Path("pilot/collection-policy.json").write_text(json.dumps({"SAFE_EOD_CUTOFF":"19:00"}))
        Path("pilot/soak.json").write_text(json.dumps({"after_market_date":"2026-09-28","required_completed_runs":10}))
        self.proof = {"date":"2026-09-29","status":"ObservedTrading","reference":"https://example.org/close",
                      "known_at":"2026-09-29T10:00:00+00:00"}
        Path("pilot/sessions.json").write_text(json.dumps([self.proof]))
        self.root = Path("data/collector-output/pilot")
        self.root.mkdir(parents=True)

    def tearDown(self):
        os.chdir(self.original)
        self.temporary.cleanup()

    def evidence(self):
        instrument = self.config["instruments"][0]
        raw = Path("data/raw/fixture")
        payload = b'[{"date":"2026-09-29","open":100,"high":101,"low":99,"close":100,"adjusted_close":100,"volume":200}]'
        manifest = archive_payload(payload,raw,source_id="eodhd",requested_uri="https://eodhd.com/api/eod/"+instrument["symbol"],
                                   request_parameters={"from":"2026-09-29","to":"2026-09-29"},
                                   parser_version=PARSER_VERSION,extension=".json")
        manifest["fetched_at_utc"]="2026-09-29T11:00:00+00:00"
        entry = {"symbol":instrument["symbol"],"instrument_id":instrument["id"],"status":"AVAILABLE",
                 "manifest":manifest,"raw_root":str(raw),"rows":[]}
        batch=self.root/"batch.json"
        batch.write_text(json.dumps({"from":"2026-09-29","to":"2026-09-29","entries":[entry]}))
        revision={"instrument_id":instrument["id"],"date":"2026-09-29","content_sha256":"canonical-fixture",
                  "retrieved_at":"2026-09-29T11:00:00+00:00"}
        state={"status":"KNOWN","database":"idx_stock_intelligence","revisions":[revision]}
        worker={"database":state["database"],"run_status":"SUCCEEDED","warnings":[],"market_date":"2026-09-29",
                "session_evidence":[self.proof],"canonical_state":[revision]}
        operation={"status":"SUCCEEDED","batch_path":str(batch),"worker":worker}
        path=self.root/"test.operation.json"
        path.write_text(json.dumps(operation))
        return entry,state,operation,path

    def test_completed_canonical_raw_reuse_and_refresh(self):
        entry,state,_,_=self.evidence()
        day=date(2026,9,29)
        cached=reusable_entries(self.root,day,day,state)
        self.assertEqual([entry["symbol"]],list(cached))
        self.assertEqual(1,len(cached[entry["symbol"]]["rows"]))
        self.assertFalse(reusable_entries(self.root,day,day,state,refresh=True))
        self.assertFalse(reusable_entries(self.root,day,date(2026,9,30),state))
        changed=deepcopy(state)
        changed["revisions"][0]["content_sha256"]="new-correction"
        self.assertFalse(reusable_entries(self.root,day,day,changed))
        self.assertFalse(reusable_entries(self.root,day,day,{"status":"UNKNOWN"}))

    def test_all_completed_entries_skip_even_account_requests(self):
        entry,state,operation,path=self.evidence()
        entries=[]
        revisions=[]
        for instrument in self.config["instruments"]:
            copied=deepcopy(entry)
            copied.update(symbol=instrument["symbol"],instrument_id=instrument["id"])
            copied["manifest"]["requested_uri"]="https://eodhd.com/api/eod/"+instrument["symbol"]
            entries.append(copied)
            revision=deepcopy(state["revisions"][0])
            revision["instrument_id"]=instrument["id"]
            revisions.append(revision)
        state["revisions"]=revisions
        operation["worker"]["canonical_state"]=revisions
        path.write_text(json.dumps(operation))
        Path(operation["batch_path"]).write_text(json.dumps({"from":"2026-09-29","to":"2026-09-29","entries":entries}))
        args=SimpleNamespace(universe="pilot/universe.json",start="2026-09-29",end="2026-09-29",
                             refresh=False,resume=None,seed=[],offline=False)
        with patch.dict(os.environ,{"EODHD_API_TOKEN":"fixture-only-sentinel"}), \
             patch("idx_stock_collector.pilot.local_state",return_value=state), \
             patch("idx_stock_collector.pilot.datetime") as clock, \
             patch("urllib.request.OpenerDirector.open",side_effect=AssertionError("Provider I/O forbidden")):
            clock.now.return_value=datetime(2026,9,30,tzinfo=timezone.utc)
            clock.fromisoformat.side_effect=datetime.fromisoformat
            batch=json.loads(collect(args).read_text())
        self.assertEqual("SUCCEEDED",batch["status"])
        self.assertEqual([],batch["requests"])
        self.assertEqual(0,batch["reserved_units"])
        self.assertTrue(all(e["cached"] for e in batch["entries"]))

    def test_degraded_stale_or_incomplete_evidence_requires_fetch(self):
        entry,state,operation,path=self.evidence()
        day=date(2026,9,29)
        for change in (
            lambda o:o.update(status="DEGRADED"),
            lambda o:o["worker"].update(warnings=["PILOT_SEMANTICS_DEGRADED"]),
            lambda o:o["worker"].update(session_evidence=[]),
            lambda o:o["worker"].update(canonical_state=[]),
        ):
            altered=deepcopy(operation);change(altered);path.write_text(json.dumps(altered))
            self.assertFalse(reusable_entries(self.root,day,day,state))
        path.write_text(json.dumps(operation))
        later=deepcopy(operation)
        later["status"]="DEGRADED"
        later_path=self.root/"later.operation.json"
        later_path.write_text(json.dumps(later))
        self.assertFalse(reusable_entries(self.root,day,day,state))
        later_path.unlink()
        batch=json.loads(Path(operation["batch_path"]).read_text())
        batch["entries"][0]["manifest"]["fetched_at_utc"]="2026-09-29T09:00:00+00:00"
        Path(operation["batch_path"]).write_text(json.dumps(batch))
        self.assertFalse(reusable_entries(self.root,day,day,state))
        batch["entries"][0]=entry
        Path(operation["batch_path"]).write_text(json.dumps(batch))
        (Path(entry["raw_root"])/entry["manifest"]["artifact"]["relative_uri"]).write_bytes(b"corrupt")
        self.assertFalse(reusable_entries(self.root,day,day,state))

    def test_dry_run_is_zero_network_and_dates_fail_closed(self):
        args=SimpleNamespace(universe="pilot/universe.json",start="2026-09-29",end="2026-09-29",refresh=False)
        with patch("idx_stock_collector.pilot.local_state",return_value={"status":"KNOWN","revisions":[]}), \
             patch("idx_stock_collector.pilot.datetime") as clock, \
             patch("urllib.request.OpenerDirector.open",side_effect=AssertionError("Provider I/O forbidden")):
            clock.now.return_value=datetime(2026,9,30,tzinfo=timezone.utc)
            clock.fromisoformat.side_effect=datetime.fromisoformat
            plan=dry_run(args)
            self.assertEqual(11,plan["maximum_eod_units"])
            self.assertEqual(0,plan["provider_requests"])
            self.assertEqual("JKSE.INDX",plan["benchmark"])
        for start,end in (("2026-09-30","2026-09-29"),("2026-10-01","2026-10-01")):
            with self.assertRaises(ValueError):
                window(start,end,date(2026,9,30))
        altered=deepcopy(self.config)
        altered["instruments"][0]["symbol"]="OTHER.JK"
        Path("altered.json").write_text(json.dumps(altered))
        with self.assertRaises(ValueError):
            configuration("altered.json")

    def test_worker_nonzero_exit_cannot_be_hidden_by_summary(self):
        batch=self.root/"batch.json"
        batch.write_text(json.dumps({"run_id":"fixture","started_at":"2026-09-30T00:00:00Z","status":"SUCCEEDED",
            "mode":"DAILY","from":"2026-09-29","to":"2026-09-29","entries":[],"requests":[],"reserved_units":0}))
        summary=self.root/"worker.summary.json"
        summary.write_text(json.dumps({"run_status":"SUCCEEDED","soak_eligible":True,"market_date":"2026-09-29"}))
        result=SimpleNamespace(returncode=1,stdout=json.dumps({"summary_path":str(summary)}))
        with patch("subprocess.run",return_value=result):
            path=ingest_and_summarize(batch)
        report=json.loads(path.read_text())
        self.assertEqual("FAILED",report["status"])
        self.assertEqual("FAILED",report["ledger"]["final_status"])
        self.assertEqual(0,report["ledger"]["quota_units"])
        self.assertFalse(report["ledger"]["soak_eligible"])
        self.assertEqual(0,report["soak"]["completed_unique_sessions"])
        self.assertEqual(0,soak_progress(self.root)["completed_unique_sessions"])

    def test_unavailable_canonical_database_fails_before_provider_request(self):
        args=SimpleNamespace(universe="pilot/universe.json",start="2026-09-29",end="2026-09-29",
                             refresh=False,resume=None,seed=[],offline=False,ingest=True)
        with patch.dict(os.environ,{"EODHD_API_TOKEN":"fixture-only-sentinel"}), \
             patch("idx_stock_collector.pilot.local_state",return_value={"status":"UNKNOWN","revisions":[]}), \
             patch("idx_stock_collector.pilot.datetime") as clock, \
             patch("urllib.request.OpenerDirector.open",side_effect=AssertionError("Provider I/O forbidden")):
            clock.now.return_value=datetime(2026,9,30,tzinfo=timezone.utc)
            clock.fromisoformat.side_effect=datetime.fromisoformat
            batch=json.loads(collect(args).read_text())
        self.assertEqual("FAILED",batch["status"])
        self.assertEqual([],batch["requests"])
        self.assertEqual("CANONICAL_PREFLIGHT_FAILED",batch["error_code"])

    def test_ledger_no_fabricated_soak_and_same_date_counts_once(self):
        self.assertEqual(0,soak_report(self.root)["soak"]["completed_unique_sessions"])
        self.assertIsNone(soak_report(self.root)["last_run"])
        for name,status,eligible,day in (
            ("success","SUCCEEDED",True,"2026-09-29"),
            ("duplicate","SUCCEEDED",True,"2026-09-29"),
            ("failed","FAILED",False,"2026-09-30"),
            ("degraded","DEGRADED",True,"2026-10-01"),
            ("fixture","SUCCEEDED",False,"2026-10-02"),
            ("past","SUCCEEDED",True,"2026-09-25"),
        ):
            (self.root/(name+".summary.json")).write_text(json.dumps({"run_status":status,"soak_eligible":eligible,"market_date":day}))
        self.assertEqual(1,soak_progress(self.root)["completed_unique_sessions"])
        with patch("sys.argv",["pilot","--soak-report"]),patch("sys.stdout",new_callable=io.StringIO) as output, \
             patch("urllib.request.OpenerDirector.open",side_effect=AssertionError("Provider I/O forbidden")):
            main()
            self.assertIn("not queried in offline mode",output.getvalue())

    def test_bootstrap_report_requires_complete_verified_raw_evidence(self):
        entry,_,operation,path=self.evidence()
        entries=[]
        for instrument in self.config["instruments"]:
            copied=deepcopy(entry)
            copied.update(symbol=instrument["symbol"],instrument_id=instrument["id"])
            copied["manifest"]["requested_uri"]="https://eodhd.com/api/eod/"+instrument["symbol"]
            copied["manifest"]["parser_version"]="legacy-archive-parser"
            entries.append(copied)
        batch=Path(operation["batch_path"])
        batch.write_text(json.dumps({"mode":"BOOTSTRAP","started_at":"2026-09-30T00:00:00Z",
            "status":"SUCCEEDED","from":"2026-09-29","to":"2026-09-29","entries":entries}))
        report=soak_report(self.root)
        self.assertEqual("COMPLETE",report["bootstrap_request_coverage"])
        self.assertEqual(0,report["soak"]["completed_unique_sessions"])
        self.assertNotIn("bootstrap request coverage incomplete",report["remaining_gates"])
        (Path(entry["raw_root"])/entry["manifest"]["artifact"]["relative_uri"]).write_bytes(b"corrupt")
        self.assertEqual("INCOMPLETE_OR_UNKNOWN",soak_report(self.root)["bootstrap_request_coverage"])


if __name__ == "__main__":
    unittest.main()
