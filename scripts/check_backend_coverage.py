#!/usr/bin/env python3
"""Enforce TASK-00 coverage thresholds from merged Cobertura reports and the Git diff."""
from __future__ import annotations

import argparse
import glob
import re
import subprocess
import xml.etree.ElementTree as ET
from collections import defaultdict
from pathlib import PurePosixPath


def canonical_path(raw_name: str) -> str:
    parts = PurePosixPath(raw_name.replace("\\", "/")).parts
    project_index = next((index for index, part in enumerate(parts) if part.startswith("LoanSystem.")), None)
    if project_index is not None:
        return "/".join(parts[project_index:])
    if "src" in parts:
        return "/".join(parts[parts.index("src") + 1 :])
    return "/".join(parts)


def is_business_path(path: str) -> bool:
    return (
        path.startswith("LoanSystem.BuildingBlocks.Domain/")
        or path.startswith("LoanSystem.BuildingBlocks.Application/")
        or "/Domain/" in f"/{path}"
        or "/Application/" in f"/{path}"
    ) and not path.endswith("NamespaceMarker.cs")


def merge_coverage(reports: list[str]) -> dict[tuple[str, int], int]:
    lines: dict[tuple[str, int], int] = defaultdict(int)
    for report in reports:
        root = ET.parse(report).getroot()
        for source_class in root.findall(".//class"):
            path = canonical_path(source_class.attrib["filename"])
            if "/Migrations/" in f"/{path}" or path.endswith("NamespaceMarker.cs"):
                continue
            for line in source_class.findall("./lines/line"):
                key = (path, int(line.attrib["number"]))
                lines[key] = max(lines[key], int(line.attrib["hits"]))
    return lines


def changed_lines(base_ref: str) -> set[tuple[str, int]]:
    command = ["git", "diff", "--unified=0", "--diff-filter=ACMR", f"{base_ref}...HEAD", "--", "src/**/*.cs"]
    output = subprocess.run(command, check=True, capture_output=True, text=True).stdout
    current_path: str | None = None
    changed: set[tuple[str, int]] = set()
    for row in output.splitlines():
        if row.startswith("+++ b/"):
            current_path = canonical_path(row[6:])
            continue
        match = re.match(r"@@ -\d+(?:,\d+)? \+(\d+)(?:,(\d+))? @@", row)
        if current_path is None or match is None:
            continue
        start = int(match.group(1))
        count = int(match.group(2) or "1")
        changed.update((current_path, number) for number in range(start, start + count))
    return changed


def rate(selected: dict[tuple[str, int], int]) -> float:
    return 100.0 * sum(hit > 0 for hit in selected.values()) / len(selected)


def run(reports: list[str], base_ref: str) -> int:
    if not reports:
        print("Backend coverage gate failed: no Cobertura reports were found.")
        return 1
    lines = merge_coverage(reports)
    if not lines:
        print("Backend coverage gate failed: reports contained no production lines.")
        return 1

    domain_application = {key: hit for key, hit in lines.items() if is_business_path(key[0])}
    if not domain_application:
        print("Backend coverage gate failed: no Domain/Application production lines were measured.")
        return 1

    changed = changed_lines(base_ref)
    changed_business = {key: hit for key, hit in lines.items() if key in changed and is_business_path(key[0])}
    checks = [("Domain/Application", rate(domain_application), 95.0)]
    if changed_business:
        checks.append(("Changed backend business", rate(changed_business), 90.0))
    else:
        print("Changed backend business line coverage: not applicable (the diff has no changed coverable business lines).")

    for label, actual, threshold in checks:
        print(f"{label} line coverage: {actual:.2f}% (required: {threshold:.2f}%)")
    failures = [f"{label} {actual:.2f}% < {threshold:.2f}%" for label, actual, threshold in checks if actual < threshold]
    if failures:
        print("Backend coverage gate failed: " + "; ".join(failures))
        return 1
    return 0


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("patterns", nargs="*", default=["TestResults/**/coverage.cobertura.xml"])
    parser.add_argument("--base-ref", default="origin/main")
    args = parser.parse_args()
    reports = sorted({path for pattern in args.patterns for path in glob.glob(pattern, recursive=True)})
    return run(reports, args.base_ref)


if __name__ == "__main__":
    raise SystemExit(main())
