"""Deterministic H+1 planning and account gates; no provider or production writes."""
from copy import deepcopy
from datetime import datetime, timezone
import io
import json
import os
from pathlib import Path
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

from idx_stock_collector.pilot import collect, main
from idx_stock_collector.pilot_daily import daily_plan


class DailyTests(unittest.TestCase):
    def setUp(self):
        self.original = Path.cwd()
        self.temporary = tempfile.TemporaryDirectory()
        config = json.loads((Path(__file__).resolve().parents[3] / "pilot/universe.json").read_text())
        os.chdir(self.temporary.name)
        Path("pilot").mkdir()
        Path("pilot/universe.json").write_text(json.dumps(config))
        Path("pilot/collection-policy.json").write_text('{"SAFE_EOD_CUTOFF":"19:00"}')
        Path("pilot/soak.json").write_text('{"after_market_date":"2026-09-28","required_completed_runs":10}')
        self.root = Path("data/collector-output/pilot")
        self.root.mkdir(parents=True)
        revisions = [{"instrument_id":i["id"],"date":"2026-09-30","content_sha256":"fixture",
                      "retrieved_at":"2026-10-01T01:00:00Z"} for i in config["instruments"]]
        self.state = {"status":"KNOWN","database":"idx_stock_intelligence","revisions":revisions}
        self.operation = {"status":"SUCCEEDED","worker":{"run_status":"SUCCEEDED",
            "database":"idx_stock_intelligence","market_date":"2026-09-30","canonical_state":deepcopy(revisions),
            "warnings":["PILOT_SEMANTICS_DEGRADED"]},
            "ledger":{"mode":"DAILY","final_status":"SUCCESS","session_date":"2026-09-30"}}
        (self.root / "fixture.operation.json").write_text(json.dumps(self.operation))
        (self.root / "fixture.summary.json").write_text(json.dumps({"run_status":"SUCCEEDED",
            "soak_eligible":True,"market_date":"2026-09-30"}))
        Path("pilot/sessions.json").write_text('[]')
        self.network = patch("urllib.request.OpenerDirector.open",side_effect=AssertionError("Provider I/O forbidden"))
        self.network.start()

    def tearDown(self):
        self.network.stop()
        os.chdir(self.original)
        self.temporary.cleanup()

    def proof(self, day, status="ObservedTrading"):
        return {"date":day,"status":status,"reference":"https://reference.example/session",
                "completed_at":day+"T16:30:00+07:00","known_at":day+"T17:00:00+07:00"}

    def plan(self, now="2026-10-02T00:00:00Z", proofs=(), state=None):
        Path("pilot/sessions.json").write_text(json.dumps(proofs))
        with patch("idx_stock_collector.pilot_daily.local_state",return_value=self.state if state is None else state):
            return daily_plan(datetime.fromisoformat(now))

    def test_proven_oct1_selected_after_successful_sep30(self):
        plan = self.plan(proofs=[self.proof("2026-10-01")])
        self.assertEqual("ELIGIBLE",plan["decision"])
        self.assertEqual("2026-10-01",plan["next_candidate_session"])
        self.assertEqual("CONFIRMED",plan["session_proof"])

    def test_missing_incomplete_future_or_provider_proof_blocks(self):
        valid = self.proof("2026-10-01")
        for proofs in ([],[dict(valid,completed_at=None)],[dict(valid,reference="https://eodhd.com/row")],
                       [dict(valid,known_at="2026-10-03T00:00:00Z")],[valid,valid],
                       [dict(valid,completed_at="2026-10-01T18:00:00+07:00")]):
            with self.subTest(proofs=proofs):
                self.assertEqual("WAITING_FOR_SESSION_PROOF",self.plan(proofs=proofs)["decision"])

    def test_hplus1_no_new_session_and_no_redundant_fetch(self):
        plan = self.plan(now="2026-10-01T14:00:00Z",proofs=[self.proof("2026-10-01")])
        self.assertEqual("NO_NEW_SESSION",plan["decision"])
        self.assertEqual([],plan["pending_sessions"])
        for dry in (True,False):
            with patch("sys.argv",["pilot","--daily"]+(["--dry-run"] if dry else [])), \
                 patch("idx_stock_collector.pilot_daily.daily_plan",return_value=plan), \
                 patch("idx_stock_collector.pilot.collect",side_effect=AssertionError("No redundant collection")), \
                 patch("sys.stdout",new_callable=io.StringIO):
                with self.assertRaises(SystemExit) as exit:
                    main()
                self.assertEqual(0,exit.exception.code)

    def test_database_unavailable_or_wrong_identity_blocks_before_collection(self):
        for state in ({"status":"UNKNOWN","revisions":[]},dict(self.state,database="replacement"),
                      dict(self.state,revisions=self.state["revisions"][:-1])):
            plan = self.plan(state=state)
            self.assertEqual("BLOCKED",plan["decision"])
            with patch("sys.argv",["pilot","--daily"]), \
                 patch("idx_stock_collector.pilot_daily.daily_plan",return_value=plan), \
                 patch("idx_stock_collector.pilot.collect",side_effect=AssertionError("Provider path forbidden")), \
                 patch("sys.stdout",new_callable=io.StringIO):
                with self.assertRaises(SystemExit) as exit:
                    main()
                self.assertEqual(1,exit.exception.code)

    def test_catchup_oldest_first_and_missing_dates_never_skipped(self):
        proofs=[self.proof(d) for d in ("2026-10-01","2026-10-02","2026-10-05")]
        plan=self.plan("2026-10-05T00:00:00Z",proofs)
        self.assertEqual("2026-10-01",plan["next_candidate_session"])
        self.assertEqual(["2026-10-01","2026-10-02"],plan["pending_sessions"])
        plan=self.plan("2026-10-06T00:00:00Z",[self.proof("2026-10-02")])
        self.assertEqual("WAITING_FOR_SESSION_PROOF",plan["decision"])
        self.assertEqual("2026-10-01",plan["next_candidate_session"])
        self.assertEqual(["2026-10-02"],plan["pending_sessions"])

    def test_only_proven_closures_are_skipped(self):
        proofs=[self.proof("2026-10-01","AnnouncedClosed"),self.proof("2026-10-02")]
        self.assertEqual("2026-10-02",self.plan("2026-10-03T00:00:00Z",proofs)["next_candidate_session"])
        self.assertEqual("NO_NEW_SESSION",self.plan(proofs=proofs[:1])["decision"])

    def test_unproven_weekend_is_skipped_but_weekday_and_explicit_weekend_open_are_not(self):
        for revision in self.state["revisions"]:
            revision["date"] = "2026-10-02"
        self.operation["worker"].update(market_date="2026-10-02", canonical_state=deepcopy(self.state["revisions"]))
        self.operation["ledger"]["session_date"] = "2026-10-02"
        (self.root / "fixture.operation.json").write_text(json.dumps(self.operation))
        plan = self.plan("2026-10-06T00:00:00Z")
        self.assertEqual("2026-10-05", plan["next_candidate_session"])
        self.assertEqual("WAITING_FOR_SESSION_PROOF", plan["decision"])
        opened = self.plan("2026-10-06T00:00:00Z", [self.proof("2026-10-03")])
        self.assertEqual("2026-10-03", opened["next_candidate_session"])
        self.assertEqual("ELIGIBLE", opened["decision"])

    def test_new_successful_session_advances_baseline_without_refetch(self):
        for revision in self.state["revisions"]:
            revision["date"]="2026-10-01"
        worker=self.operation["worker"]
        worker.update(market_date="2026-10-01",canonical_state=deepcopy(self.state["revisions"]))
        self.operation["ledger"]["session_date"]="2026-10-01"
        (self.root/"fixture.operation.json").write_text(json.dumps(self.operation))
        for now in ("2026-10-02T00:00:00Z","2026-10-01T14:00:00Z"):
            plan=self.plan(now,proofs=[self.proof("2026-10-01")])
            self.assertEqual("2026-10-01",plan["latest_successful_canonical_session"])
            self.assertEqual("NO_NEW_SESSION",plan["decision"])
            self.assertEqual([],plan["pending_sessions"])

    def test_jakarta_midnight_and_aware_clock(self):
        proof=[self.proof("2026-10-01")]
        self.assertEqual("NO_NEW_SESSION",self.plan("2026-10-01T16:59:59Z",proof)["decision"])
        plan=self.plan("2026-10-01T17:00:00Z",proof)
        self.assertEqual("2026-10-02",plan["current_jakarta_date"])
        self.assertEqual("ELIGIBLE",plan["decision"])
        with self.assertRaises(ValueError):
            daily_plan(datetime(2026,10,2))

    def test_dryrun_no_network_no_writes_and_authoritative_soak(self):
        plan=self.plan(proofs=[self.proof("2026-10-01")])
        before={str(p):p.read_bytes() for p in Path('.').rglob('*') if p.is_file()}
        with patch("sys.argv",["pilot","--daily","--dry-run"]), \
             patch("idx_stock_collector.pilot_daily.daily_plan",return_value=plan), \
             patch("sys.stdout",new_callable=io.StringIO) as output:
            with self.assertRaises(SystemExit) as exit:
                main()
        self.assertEqual(0,exit.exception.code)
        result=json.loads(output.getvalue())
        self.assertEqual(0,result["provider_requests"])
        self.assertEqual("NOT ENABLED",result["FullIdx"])
        self.assertEqual(1,result["soak"]["completed_unique_sessions"])
        after={str(p):p.read_bytes() for p in Path('.').rglob('*') if p.is_file()}
        self.assertEqual(before,after)

    def test_degraded_canonical_ahead_or_missing_success_report_is_data_blocked(self):
        changed=deepcopy(self.state)
        changed["revisions"].append(dict(changed["revisions"][0],date="2026-10-01"))
        self.assertEqual("BLOCKED",self.plan(state=changed)["decision"])
        self.operation["status"]="DEGRADED"
        (self.root / "fixture.operation.json").write_text(json.dumps(self.operation))
        self.assertEqual("BLOCKED",self.plan()["decision"])

    def test_daily_reuses_pipeline_and_propagates_exit_codes(self):
        plan=self.plan(proofs=[self.proof("2026-10-01")])
        for status,code in (("SUCCEEDED",0),("DEGRADED",2),("FAILED",1)):
            path=self.root/"result.operation.json"
            path.write_text(json.dumps({"status":status,"reserved_units":11,"soak":plan["soak"]}))
            with patch("sys.argv",["pilot","--daily","--ingest"]), \
                 patch("idx_stock_collector.pilot_daily.daily_plan",return_value=plan), \
                 patch("idx_stock_collector.pilot.collect",return_value=path) as collector, \
                 patch("idx_stock_collector.pilot.ingest_and_summarize",return_value=path) as ingest, \
                 patch("sys.stdout",new_callable=io.StringIO):
                with self.assertRaises(SystemExit) as exit:
                    main()
            self.assertEqual(code,exit.exception.code)
            self.assertEqual(1,collector.call_count)
            self.assertEqual(1,ingest.call_count)
            args=collector.call_args.args[0]
            self.assertEqual(("2026-10-01","2026-10-01"),(args.start,args.end))
            self.assertTrue(args.daily)

    def test_strict_account_stale_unknown_extra_or_insufficient_allowance_never_fetches(self):
        Path("pilot/sessions.json").write_text(json.dumps([self.proof("2026-10-01")]))
        normal={"subscriptionType":"free","apiRequestsDate":"2026-10-02","apiRequests":1,
                "dailyRateLimit":20,"extraLimit":464}
        args=SimpleNamespace(universe="pilot/universe.json",start="2026-10-01",end="2026-10-01",
            daily=True,offline=False,ingest=True,refresh=False,resume=None,seed=[])
        for account in (dict(normal,apiRequestsDate="2026-10-01"),dict(normal,extraLimit=None),dict(normal,apiRequests=6)):
            def only_account(request, timeout):
                self.assertEqual(30,timeout)
                self.assertIn('/api/user?',request.full_url)
                response=io.BytesIO(json.dumps(account).encode());response.status=200
                return response
            with patch.dict(os.environ,{"EODHD_API_TOKEN":"fixture-only-sentinel"}), \
                 patch("idx_stock_collector.pilot.local_state",return_value=self.state), \
                 patch("idx_stock_collector.pilot.datetime") as clock, \
                 patch("urllib.request.OpenerDirector.open",side_effect=only_account) as requests:
                clock.now.return_value=datetime(2026,10,2,tzinfo=timezone.utc)
                clock.fromisoformat.side_effect=datetime.fromisoformat
                batch=json.loads(collect(args).read_text())
            self.assertEqual("FAILED",batch["status"])
            self.assertEqual(0,batch["reserved_units"])
            self.assertEqual(1,requests.call_count)

    def test_daily_disallows_fullidx_refresh_and_explicit_dates(self):
        for extra in (["--universe","fullidx.json"],["--refresh"],["--from","2026-10-01"],["--offline"],["--soak-report"]):
            with patch("sys.argv",["pilot","--daily","--dry-run"]+extra):
                with self.assertRaises(SystemExit):
                    main()
