import contextlib
import io
import os
import subprocess
import tempfile
import unittest
from pathlib import Path

from scripts.check_backend_coverage import run


class CoverageGateTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        self.previous = Path.cwd()
        os.chdir(self.root)
        subprocess.run(["git", "init", "-q"], check=True)
        subprocess.run(["git", "config", "user.email", "coverage@example.test"], check=True)
        subprocess.run(["git", "config", "user.name", "Coverage Test"], check=True)
        source = self.root / "src/BuildingBlocks/LoanSystem.BuildingBlocks.Domain/Rule.cs"
        source.parent.mkdir(parents=True)
        source.write_text("public class Rule { public bool Initial => true; }\n")
        subprocess.run(["git", "add", "."], check=True)
        subprocess.run(["git", "commit", "-qm", "base"], check=True)
        self.base = subprocess.check_output(["git", "rev-parse", "HEAD"], text=True).strip()
        source.write_text("public class Rule { public bool Changed => true; }\n")
        subprocess.run(["git", "add", "."], check=True)
        subprocess.run(["git", "commit", "-qm", "change"], check=True)

    def tearDown(self):
        os.chdir(self.previous)
        self.temporary.cleanup()

    def report(self, hits):
        report = self.root / "coverage.xml"
        report.write_text(f'''<coverage><packages><package><classes><class filename="LoanSystem.BuildingBlocks.Domain/Rule.cs"><lines><line number="1" hits="{hits}"/></lines></class></classes></package></packages></coverage>''')
        return str(report)

    def test_passes_when_changed_business_line_is_covered(self):
        self.assertEqual(0, run([self.report(1)], self.base))

    def test_fails_when_changed_business_line_is_uncovered(self):
        with contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(1, run([self.report(0)], self.base))

    def test_fails_without_reports(self):
        with contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(1, run([], self.base))


if __name__ == "__main__":
    unittest.main()
