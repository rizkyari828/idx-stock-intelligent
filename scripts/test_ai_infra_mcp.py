"""Protocol acceptance for the stdio MCP server: no model, no network."""
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parent.parent
SERVER = ROOT / "scripts/ai_infra_mcp.py"

STUB = """#!/bin/sh
printf '%s' "$0" > "$CAPTURE"
for arg in "$@"; do printf '|%s' "$arg" >> "$CAPTURE"; done
printf '\\n' >> "$CAPTURE"
exit 0
"""


class AiInfraMcpAcceptance(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.repo = Path(self.tmp.name) / "ai-local-infra"
        (self.repo / "scripts").mkdir(parents=True)
        for name in ("route", "orchestrate", "trace-report", "record-span"):
            script = self.repo / "scripts" / name
            script.write_text(STUB, encoding="utf-8")
            script.chmod(0o755)
        self.capture = Path(self.tmp.name) / "capture"

    def speak(self, messages, lai=True):
        env = {k: v for k, v in os.environ.items() if k != "LAI_REPO"}
        env["CAPTURE"] = str(self.capture)
        if lai:
            env["LAI_REPO"] = str(self.repo)
        payload = "".join(json.dumps(message) + "\n" for message in messages)
        completed = subprocess.run(
            ["python3", str(SERVER)], input=payload, capture_output=True, text=True, env=env, timeout=60)
        self.assertEqual(0, completed.returncode, completed.stderr)
        return [json.loads(line) for line in completed.stdout.splitlines() if line.strip()]

    def captured(self):
        return self.capture.read_text(encoding="utf-8").strip() if self.capture.exists() else ""

    def test_initialize_negotiates_a_supported_protocol(self):
        replies = self.speak([{"jsonrpc": "2.0", "id": 1, "method": "initialize",
                               "params": {"protocolVersion": "2024-11-05", "capabilities": {},
                                          "clientInfo": {"name": "test", "version": "0"}}}])
        result = replies[0]["result"]
        self.assertEqual("2024-11-05", result["protocolVersion"])
        self.assertEqual("ai-infra", result["serverInfo"]["name"])
        self.assertIn("tools", result["capabilities"])

    def test_newer_dated_revision_is_echoed_not_downgraded(self):
        replies = self.speak([{"jsonrpc": "2.0", "id": 10, "method": "initialize",
                               "params": {"protocolVersion": "2026-07-28", "capabilities": {},
                                          "clientInfo": {"name": "test", "version": "0"}}}])
        self.assertEqual("2026-07-28", replies[0]["result"]["protocolVersion"])

    def test_non_dated_revision_falls_back_to_a_supported_one(self):
        replies = self.speak([{"jsonrpc": "2.0", "id": 11, "method": "initialize",
                               "params": {"protocolVersion": "banana", "capabilities": {},
                                          "clientInfo": {"name": "test", "version": "0"}}}])
        self.assertEqual("2025-06-18", replies[0]["result"]["protocolVersion"])

    def test_notifications_get_no_reply(self):
        replies = self.speak([
            {"jsonrpc": "2.0", "method": "notifications/initialized"},
            {"jsonrpc": "2.0", "id": 2, "method": "ping"},
        ])
        self.assertEqual(1, len(replies))
        self.assertEqual(2, replies[0]["id"])

    def test_tools_list_exposes_only_read_only_surfaces(self):
        replies = self.speak([{"jsonrpc": "2.0", "id": 3, "method": "tools/list"}])
        tools = replies[0]["result"]["tools"]
        names = {tool["name"] for tool in tools}
        self.assertEqual({"ai_infra_check", "ai_infra_route", "ai_infra_plan", "ai_infra_report", "ai_infra_span"}, names)
        for forbidden in ("ai_infra_run", "ai_infra_agent_run", "ai_infra_bootstrap"):
            self.assertNotIn(forbidden, names)
        for tool in tools:
            self.assertEqual("object", tool["inputSchema"]["type"])

    def test_check_reports_sibling_scripts(self):
        replies = self.speak([{"jsonrpc": "2.0", "id": 4, "method": "tools/call",
                               "params": {"name": "ai_infra_check", "arguments": {}}}])
        result = replies[0]["result"]
        self.assertFalse(result["isError"], result)
        self.assertIn("script: route ok", result["content"][0]["text"])

    def test_route_forwards_task_and_json_flag(self):
        replies = self.speak([{"jsonrpc": "2.0", "id": 5, "method": "tools/call",
                               "params": {"name": "ai_infra_route", "arguments": {"task": "bounded frozen work"}}}])
        self.assertFalse(replies[0]["result"]["isError"], replies[0])
        self.assertEqual(str(self.repo / "scripts/route") + "|--task|bounded frozen work|--json", self.captured())

    def test_plan_defaults_to_read_only(self):
        replies = self.speak([{"jsonrpc": "2.0", "id": 6, "method": "tools/call",
                               "params": {"name": "ai_infra_plan", "arguments": {"objective": "goal"}}}])
        self.assertFalse(replies[0]["result"]["isError"], replies[0])
        self.assertEqual(str(self.repo / "scripts/orchestrate") + "|plan|--objective|goal|--json|--read-only", self.captured())

    def test_missing_lai_repo_returns_tool_error_not_crash(self):
        replies = self.speak([{"jsonrpc": "2.0", "id": 7, "method": "tools/call",
                               "params": {"name": "ai_infra_check", "arguments": {}}}], lai=False)
        result = replies[0]["result"]
        self.assertTrue(result["isError"])
        self.assertIn("LAI_REPO is unset", result["content"][0]["text"])

    def test_unknown_tool_is_a_protocol_error(self):
        replies = self.speak([{"jsonrpc": "2.0", "id": 8, "method": "tools/call",
                               "params": {"name": "ai_infra_run", "arguments": {}}}])
        self.assertEqual(-32602, replies[0]["error"]["code"])
        self.assertEqual("", self.captured(), "unknown tool must never reach the sibling")

    def test_malformed_line_does_not_kill_the_server(self):
        env = {k: v for k, v in os.environ.items() if k != "LAI_REPO"}
        payload = "not json\n" + json.dumps({"jsonrpc": "2.0", "id": 9, "method": "ping"}) + "\n"
        completed = subprocess.run(["python3", str(SERVER)], input=payload, capture_output=True, text=True, env=env, timeout=60)
        self.assertEqual(0, completed.returncode, completed.stderr)
        replies = [json.loads(line) for line in completed.stdout.splitlines() if line.strip()]
        self.assertEqual(-32700, replies[0]["error"]["code"])
        self.assertEqual(9, replies[1]["id"])


if __name__ == "__main__":
    unittest.main()
