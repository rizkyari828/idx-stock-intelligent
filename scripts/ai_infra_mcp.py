#!/usr/bin/env python3
"""Minimal stdio MCP server exposing the read-only ai-local-infra facade.

Transport: newline-delimited JSON-RPC 2.0 over stdin/stdout (MCP stdio).
Dependencies: standard library only.

Boundaries inherited from scripts/ai-infra: no model launch, no network, no edits
to the sibling repository. The facade stays the single place that validates
LAI_REPO and refuses mutating surfaces; this server only maps typed tool arguments
to facade argv lists (never a shell string).
"""
from __future__ import annotations

import json
import os
import re
import subprocess
import sys
import time

PROTOCOL_VERSIONS = ("2024-11-05", "2025-03-26", "2025-06-18")
# Clients may request a newer dated revision; the tool surface is unchanged, so the
# server echoes any well-formed revision not older than 2024-11-05 instead of
# forcing a downgrade that some clients treat as a failed handshake.
DATED_REVISION = r"^20[0-9]{2}-[0-9]{2}-[0-9]{2}$"
SERVER_INFO = {"name": "ai-infra", "version": "0.1.0"}
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FACADE = os.path.join(ROOT, "scripts", "ai-infra")
CALL_TIMEOUT_SECONDS = 60
MAX_OUTPUT_BYTES = 65536

TOOLS = [
    {
        "name": "ai_infra_check",
        "description": "Verify LAI_REPO and the sibling local-ai-infra scripts. Read-only, offline.",
        "inputSchema": {"type": "object", "properties": {}, "additionalProperties": False},
    },
    {
        "name": "ai_infra_route",
        "description": "Cheapest-sufficient tier/effort/model recommendation for one task. Read-only, offline.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "task": {"type": "string", "minLength": 1, "maxLength": 2000,
                         "description": "Task description to classify. Not echoed back by the sibling."},
            },
            "required": ["task"],
            "additionalProperties": False,
        },
    },
    {
        "name": "ai_infra_plan",
        "description": "Offline task-graph plan for one objective; persists nothing and calls no model.",
        "inputSchema": {
            "type": "object",
            "properties": {
                "objective": {"type": "string", "minLength": 1, "maxLength": 4000},
                "read_only": {"type": "boolean", "default": True,
                              "description": "Reject mutating nodes before execution. Defaults to true."},
            },
            "required": ["objective"],
            "additionalProperties": False,
        },
    },
    {
        "name": "ai_infra_report",
        "description": "Trace report rebuilt from recorded spans. Read-only, offline.",
        "inputSchema": {"type": "object", "properties": {}, "additionalProperties": False},
    },
    {
        "name": "ai_infra_span",
        "description": "Append one sanitized span to the sibling's telemetry store (the only writing tool).",
        "inputSchema": {
            "type": "object",
            "properties": {
                "trace_id": {"type": "string", "minLength": 1, "maxLength": 128},
                "stage": {"type": "string", "minLength": 1, "maxLength": 64},
                "duration_ms": {"type": "integer", "minimum": 0},
                "model": {"type": "string", "maxLength": 128},
                "provider": {"type": "string", "maxLength": 64},
                "role": {"type": "string", "enum": ["student", "teacher-cheap", "teacher-premium"]},
                "attempt": {"type": "integer", "minimum": 1, "maximum": 100},
            },
            "required": ["trace_id", "stage", "duration_ms"],
            "additionalProperties": False,
        },
    },
]


def _build_argv(name: str, args: dict) -> list[str]:
    if name == "ai_infra_check":
        return [FACADE, "check"]
    if name == "ai_infra_route":
        return [FACADE, "route", "--task", str(args["task"]), "--json"]
    if name == "ai_infra_plan":
        argv = [FACADE, "plan", "--objective", str(args["objective"]), "--json"]
        if args.get("read_only", True):
            argv.append("--read-only")
        return argv
    if name == "ai_infra_report":
        return [FACADE, "report", "--json"]
    if name == "ai_infra_span":
        argv = [
            FACADE, "span",
            "--trace-id", str(args["trace_id"]),
            "--stage", str(args["stage"]),
            "--duration-ms", str(int(args["duration_ms"])),
            "--attempt", str(int(args.get("attempt", 1))),
        ]
        if args.get("model"):
            argv += ["--model", str(args["model"])]
        if args.get("provider"):
            argv += ["--provider", str(args["provider"])]
        if args.get("role"):
            argv += ["--role", str(args["role"])]
        return argv
    raise ValueError(f"unknown tool: {name}")


def _truncate(text: str) -> str:
    raw = text.encode("utf-8", "replace")
    if len(raw) <= MAX_OUTPUT_BYTES:
        return text
    return raw[:MAX_OUTPUT_BYTES].decode("utf-8", "ignore") + "\n[truncated by ai-infra MCP server]"


def call_tool(name: str, args: dict) -> dict:
    if name not in {tool["name"] for tool in TOOLS}:
        raise ValueError(f"unknown tool: {name}")
    if not os.access(FACADE, os.X_OK):
        return {"content": [{"type": "text", "text": f"facade not executable: {FACADE}"}], "isError": True}
    argv = _build_argv(name, args)
    started = time.monotonic()
    try:
        completed = subprocess.run(
            argv, capture_output=True, text=True, timeout=CALL_TIMEOUT_SECONDS,
            stdin=subprocess.DEVNULL, cwd=ROOT,
            env={k: v for k, v in os.environ.items()},
        )
    except subprocess.TimeoutExpired:
        return {"content": [{"type": "text", "text": f"{name} timed out after {CALL_TIMEOUT_SECONDS}s"}], "isError": True}
    elapsed = int((time.monotonic() - started) * 1000)
    body = completed.stdout.strip() or completed.stderr.strip() or "(no output)"
    if completed.returncode != 0:
        return {"content": [{"type": "text", "text": f"exit {completed.returncode}: {_truncate(body)}"}], "isError": True}
    return {"content": [{"type": "text", "text": _truncate(body)}], "isError": False, "_elapsedMs": elapsed}


def _response(request_id, result=None, error=None) -> dict:
    payload = {"jsonrpc": "2.0", "id": request_id}
    if error is not None:
        payload["error"] = error
    else:
        payload["result"] = result
    return payload


def handle(message: dict, out):
    method = message.get("method")
    request_id = message.get("id")
    is_notification = request_id is None
    params = message.get("params") or {}

    if method == "initialize":
        requested = params.get("protocolVersion")
        if requested in PROTOCOL_VERSIONS:
            version = requested
        elif isinstance(requested, str) and re.match(DATED_REVISION, requested) and requested >= "2024-11-05":
            version = requested
        else:
            version = PROTOCOL_VERSIONS[-1]
        result = {"protocolVersion": version, "capabilities": {"tools": {"listChanged": False}},
                  "serverInfo": SERVER_INFO}
        if not is_notification:
            out(_response(request_id, result))
        return

    if method in ("notifications/initialized", "initialized"):
        return

    if method == "ping":
        if not is_notification:
            out(_response(request_id, {}))
        return

    if method == "tools/list":
        if not is_notification:
            out(_response(request_id, {"tools": TOOLS}))
        return

    if method == "tools/call":
        name = params.get("name")
        arguments = params.get("arguments") or {}
        if not isinstance(arguments, dict):
            out(_response(request_id, error={"code": -32602, "message": "arguments must be an object"}))
            return
        try:
            result = call_tool(str(name), arguments)
        except ValueError as error:
            out(_response(request_id, error={"code": -32602, "message": str(error)}))
            return
        result.pop("_elapsedMs", None)
        out(_response(request_id, result))
        return

    if not is_notification:
        out(_response(request_id, error={"code": -32601, "message": f"unknown method: {method}"}))


def main() -> int:
    def out(payload: dict) -> None:
        sys.stdout.write(json.dumps(payload, separators=(",", ":")) + "\n")
        sys.stdout.flush()

    for line in sys.stdin:
        line = line.strip()
        if not line:
            continue
        try:
            message = json.loads(line)
        except json.JSONDecodeError:
            out(_response(None, error={"code": -32700, "message": "parse error"}))
            continue
        if not isinstance(message, dict):
            out(_response(None, error={"code": -32600, "message": "invalid request"}))
            continue
        try:
            handle(message, out)
        except Exception as error:  # never crash the transport on one bad request
            if message.get("id") is not None:
                out(_response(message["id"], error={"code": -32603, "message": f"internal error: {type(error).__name__}"}))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
