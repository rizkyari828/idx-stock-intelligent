"""Archive raw bytes and describe them without interpreting provider semantics."""

from __future__ import annotations

from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
from typing import Any

SCHEMA_VERSION = 1


def archive_payload(
    payload: bytes,
    output_root: Path,
    *,
    source_id: str,
    requested_uri: str,
    parser_version: str,
    request_parameters: dict[str, Any] | None = None,
    fetched_at: datetime | None = None,
    extension: str = ".bin",
) -> dict[str, Any]:
    """Store immutable raw bytes and return a JSON-serializable manifest."""
    if not source_id.strip():
        raise ValueError("source_id is required")
    if not requested_uri.strip():
        raise ValueError("requested_uri is required")
    if not parser_version.strip():
        raise ValueError("parser_version is required")
    if not extension.startswith(".") or not extension[1:].isalnum() or len(extension) > 16:
        raise ValueError("extension must be short and alphanumeric")

    observed_at = fetched_at or datetime.now(timezone.utc)
    if observed_at.tzinfo is None:
        raise ValueError("fetched_at must be timezone-aware")

    digest = hashlib.sha256(payload).hexdigest()
    relative_path = Path(digest[:2]) / f"{digest}{extension.lower()}"
    artifact_path = output_root / relative_path
    artifact_path.parent.mkdir(parents=True, exist_ok=True)

    try:
        descriptor = os.open(artifact_path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    except FileExistsError:
        if artifact_path.read_bytes() != payload:
            raise RuntimeError("content-address collision or corrupted artifact") from None
    else:
        with os.fdopen(descriptor, "wb") as artifact:
            artifact.write(payload)
            artifact.flush()
            os.fsync(artifact.fileno())

    return {
        "schema_version": SCHEMA_VERSION,
        "source_id": source_id,
        "requested_uri": requested_uri,
        "request_parameters": request_parameters or {},
        "fetched_at_utc": observed_at.astimezone(timezone.utc).isoformat(),
        "artifact": {
            "relative_uri": relative_path.as_posix(),
            "content_sha256": digest,
            "byte_length": len(payload),
        },
        "parser_version": parser_version,
    }


def serialize_manifest(manifest: dict[str, Any]) -> str:
    return json.dumps(manifest, sort_keys=True, separators=(",", ":"))
