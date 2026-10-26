"""AI-infra facade acceptance: no model, no network, no sibling mutation."""
import os
from pathlib import Path
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parent.parent
ADAPTER = ROOT / "scripts/ai-infra"

STUBS = """
#!/bin/sh
printf '%s' "$0" > "$CAPTURE"
for arg in "$@"; do printf '|%s' "$arg" >> "$CAPTURE"; done
printf '\\n' >> "$CAPTURE"
exit 0
"""


class AiInfraFacadeAcceptance(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.repo = Path(self.tmp.name) / "ai-local-infra"
        (self.repo / "scripts").mkdir(parents=True)
        for name in ("route", "orchestrate", "trace-report", "record-span"):
            script = self.repo / "scripts" / name
            script.write_text(STUBS, encoding="utf-8")
            script.chmod(0o755)
        self.capture = Path(self.tmp.name) / "capture"

    def run_adapter(self, *args, lai=None, unset_lai=False, extra_env=None):
        env = {k: v for k, v in os.environ.items() if k not in ("LAI_REPO", "AI_INFRA_ALLOW_MODEL_LAUNCH")}
        env["CAPTURE"] = str(self.capture)
        env.update(extra_env or {})
        if lai is not None:
            env["LAI_REPO"] = lai
        if not unset_lai and lai is None:
            env["LAI_REPO"] = str(self.repo)
        return subprocess.run([str(ADAPTER), *args], capture_output=True, text=True, env=env, timeout=30)

    def captured(self):
        return self.capture.read_text(encoding="utf-8").strip() if self.capture.exists() else ""

    def test_missing_setting_fails_closed_with_guidance(self):
        result = self.run_adapter("check", unset_lai=True)
        self.assertEqual(2, result.returncode)
        self.assertIn("LAI_REPO is unset", result.stderr)

    def test_relative_path_is_rejected(self):
        result = self.run_adapter("check", lai="ai-local-infra")
        self.assertEqual(2, result.returncode)
        self.assertIn("absolute path", result.stderr)

    def test_missing_checkout_is_rejected(self):
        result = self.run_adapter("check", lai=str(Path(self.tmp.name) / "absent"))
        self.assertEqual(2, result.returncode)
        self.assertIn("not a directory", result.stderr)

    def test_check_reports_scripts_and_never_calls_them(self):
        result = self.run_adapter("check")
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertIn("script: route ok", result.stdout)
        self.assertEqual("", self.captured(), "check must not execute sibling scripts")

    def test_route_passes_arguments_through_unchanged(self):
        result = self.run_adapter("route", "--task", "frozen contract work", "--json")
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual(str(self.repo / "scripts/route") + "|--task|frozen contract work|--json", self.captured())

    def test_plan_always_uses_the_plan_subcommand(self):
        result = self.run_adapter("plan", "--objective", "x", "--read-only")
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual(str(self.repo / "scripts/orchestrate") + "|plan|--objective|x|--read-only", self.captured())

    def test_model_launching_surfaces_are_not_reachable(self):
        for command in ("run", "agent-run", "orchestrate", "bootstrap"):
            result = self.run_adapter(command)
            self.assertEqual(64, result.returncode, command)
        self.assertEqual("", self.captured(), "refused commands must never reach the sibling")

    def test_dispatch_is_dry_run_without_the_human_flag(self):
        result = self.run_adapter("dispatch", "--objective", "goal", "--read-only")
        self.assertEqual(0, result.returncode, result.stderr)
        captured = self.captured()
        self.assertIn("|run|--objective|goal|--workspace|", captured)
        self.assertIn("|--max-ai-calls|1|--backend|auto|--state-dir|", captured)
        self.assertIn("|--read-only|--dry-run", captured)
        self.assertNotIn("--confirm", captured)

    def test_dispatch_fake_backend_confirms_without_a_model(self):
        result = self.run_adapter("dispatch", "--objective", "goal", "--backend", "fake")
        self.assertEqual(0, result.returncode, result.stderr)
        captured = self.captured()
        self.assertIn("|--backend|fake|", captured)
        self.assertIn("|--confirm", captured)
        self.assertNotIn("--dry-run", captured)
        self.assertIn("no model is launched", result.stderr)

    def test_dispatch_state_dir_defaults_inside_the_repository(self):
        result = self.run_adapter("dispatch", "--objective", "goal", "--backend", "fake")
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertIn("|--state-dir|" + str(ROOT) + "/logs/ai-infra-state", self.captured())

    def test_dispatch_forwards_execution_requirements(self):
        requirement = Path(self.tmp.name) / "requirements.json"
        requirement.write_text('{"T1": {"role": "teacher-cheap"}}', encoding="utf-8")
        result = self.run_adapter("dispatch", "--objective", "goal", "--backend", "fake",
                                  "--execution-requirements", str(requirement))
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertIn("|--execution-requirements|" + str(requirement), self.captured())

    def test_dispatch_rejects_a_missing_requirements_file(self):
        result = self.run_adapter("dispatch", "--objective", "goal",
                                  "--execution-requirements", str(Path(self.tmp.name) / "absent.json"))
        self.assertEqual(2, result.returncode)
        self.assertIn("requirements file not found", result.stderr)
        self.assertEqual("", self.captured())

    def test_dispatch_rejects_an_unknown_backend(self):
        result = self.run_adapter("dispatch", "--objective", "goal", "--backend", "shell")
        self.assertEqual(2, result.returncode)
        self.assertIn("unknown backend", result.stderr)
        self.assertEqual("", self.captured())

    def test_dispatch_confirms_only_with_the_human_flag(self):
        result = self.run_adapter("dispatch", "--objective", "goal",
                                  extra_env={"AI_INFRA_ALLOW_MODEL_LAUNCH": "1"})
        self.assertEqual(0, result.returncode, result.stderr)
        captured = self.captured()
        self.assertIn("|--confirm", captured)
        self.assertNotIn("--dry-run", captured)
        self.assertIn("CONFIRMED", result.stderr)

    def test_dispatch_rejects_unbounded_or_missing_objective(self):
        for args in (("dispatch",), ("dispatch", "--objective", "x", "--max-ai-calls", "0"),
                     ("dispatch", "--objective", "x", "--max-ai-calls", "many"),
                     ("dispatch", "--objective", "x", "--confirm-now")):
            result = self.run_adapter(*args)
            self.assertEqual(2, result.returncode, args)
        self.assertEqual("", self.captured())

    def test_span_requires_the_sibling_script(self):
        (self.repo / "scripts/record-span").unlink()
        result = self.run_adapter("span", "--trace-id", "t")
        self.assertEqual(2, result.returncode)
        self.assertIn("record-span", result.stderr)


if __name__ == "__main__":
    unittest.main()
