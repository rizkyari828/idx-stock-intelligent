from datetime import datetime, timezone
import hashlib
from pathlib import Path
import sys
import tempfile
import unittest

COLLECTOR_ROOT = Path(__file__).parents[1]
sys.path.insert(0, str(COLLECTOR_ROOT / "src"))

from idx_stock_collector.contract import archive_payload  # noqa: E402


class CollectorContractTests(unittest.TestCase):
    def test_archive_is_content_addressed_and_idempotent(self) -> None:
        payload = b"fixture-only"
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            first = archive_payload(
                payload,
                root,
                source_id="fixture",
                requested_uri="fixture://local/sample",
                parser_version="test-1",
                fetched_at=datetime(2026, 1, 2, tzinfo=timezone.utc),
                extension=".json",
            )
            second = archive_payload(
                payload,
                root,
                source_id="fixture",
                requested_uri="fixture://local/sample",
                parser_version="test-1",
                fetched_at=datetime(2026, 1, 2, tzinfo=timezone.utc),
                extension=".json",
            )

            expected_hash = hashlib.sha256(payload).hexdigest()
            self.assertEqual(expected_hash, first["artifact"]["content_sha256"])
            self.assertEqual(first["artifact"], second["artifact"])
            self.assertEqual(1, len(list(root.rglob("*.json"))))
            self.assertEqual(1, first["schema_version"])

    def test_required_provenance_is_rejected_when_blank(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            with self.assertRaises(ValueError):
                archive_payload(
                    b"payload",
                    Path(directory),
                    source_id=" ",
                    requested_uri="fixture://local/sample",
                    parser_version="test-1",
                )

    def test_naive_fetch_timestamp_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            with self.assertRaises(ValueError):
                archive_payload(
                    b"payload",
                    Path(directory),
                    source_id="fixture",
                    requested_uri="fixture://local/sample",
                    parser_version="test-1",
                    fetched_at=datetime(2026, 1, 2),
                )


if __name__ == "__main__":
    unittest.main()
