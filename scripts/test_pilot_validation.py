"""Read-only retained-evidence replay in an owned DB; never promotes a soak run."""
from copy import deepcopy
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
import uuid

import test_product_slice as product
import test_screener_evidence as evidence


class PilotIndexValidationAcceptance(unittest.TestCase):
    sql = classmethod(product.ProductAcceptance.sql.__func__)

    def test_retained_zero_volume_index_is_price_available_but_replay_never_counts(self):
        before, files_before = evidence.fingerprints(self.sql), evidence.files()
        database = "idx_pilot_index_" + uuid.uuid4().hex
        self.sql("CREATE DATABASE " + database, "idx_stock_intelligence")
        try:
            for migration in sorted((product.ROOT / "src/IdxStockIntelligence.Infrastructure/Migrations").glob("*.sql")):
                self.sql(migration.read_text(), database)
            env = {k: v for k, v in os.environ.items() if k != "EODHD_API_TOKEN"}
            env["IDX_PILOT_DATABASE"] = database
            batch = json.loads((product.ROOT / "data/collector-output/pilot/479c0acc-c2ba-4934-a473-7ac7978781c7.json").read_text())
            with tempfile.TemporaryDirectory(prefix="idx-pilot-index-") as directory:
                for mode in ("DAILY", "OFFLINE_REPLAY"):
                    with self.subTest(mode=mode):
                        replay = deepcopy(batch)
                        replay.update(run_id=str(uuid.uuid4()), mode=mode)
                        path = Path(directory) / "batch.json"
                        path.write_text(json.dumps(replay))
                        result = subprocess.run(["dotnet", "run", "--project", "src/IdxStockIntelligence.Worker",
                                                 "--no-build", "--no-restore", "--", "pilot", str(path)],
                                                cwd=product.ROOT, env=env, text=True, capture_output=True, timeout=120)
                        message = next(line for line in result.stdout.splitlines() if line.startswith('{"summary_path":'))
                        summary_path = product.ROOT / json.loads(message)["summary_path"]
                        try:
                            summary = json.loads(summary_path.read_text())
                        finally:
                            summary_path.unlink()
                        self.assertEqual(0, result.returncode, result.stdout)
                        self.assertEqual("SUCCEEDED", summary["run_status"])
                        self.assertEqual(11, summary["accepted_rows"])
                        self.assertEqual(0, summary["rejected_evidence"])
                        self.assertFalse(summary["soak_eligible"], "Owned/archival replay cannot qualify prospectively")
                        index = next(o for o in summary["observations"] if o["symbol"] == "JKSE.INDX")
                        self.assertEqual(("AVAILABLE", "NOT_APPLICABLE"), (index["Status"], index["instrument_state"]))
                        self.assertEqual("0", self.sql("SELECT volume FROM daily_bar_revision WHERE instrument_id='76237e96-232f-5085-9b13-dcb7104222bc'", database))
        finally:
            self.sql("DROP DATABASE " + database + " WITH (FORCE)", "idx_stock_intelligence")
            self.assertEqual(before, evidence.fingerprints(self.sql))
            self.assertEqual(files_before, evidence.files())
