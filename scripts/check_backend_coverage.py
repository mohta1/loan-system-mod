#!/usr/bin/env python3
"""Enforce TASK-00 coverage thresholds from one or more Cobertura reports."""
from __future__ import annotations

import glob
import sys
import xml.etree.ElementTree as ET
from collections import defaultdict
from pathlib import PurePosixPath

patterns = sys.argv[1:] or ["TestResults/**/coverage.cobertura.xml"]
reports = sorted({path for pattern in patterns for path in glob.glob(pattern, recursive=True)})
if not reports:
    raise SystemExit("Backend coverage gate failed: no Cobertura reports were found.")

# A solution test run produces one report per test assembly. Merge line hits by taking
# the greatest observed hit count for the same production source line.
lines: dict[tuple[str, int], int] = defaultdict(int)
for report in reports:
    root = ET.parse(report).getroot()
    for source_class in root.findall(".//class"):
        raw_name = source_class.attrib["filename"].replace("\\", "/")
        parts = PurePosixPath(raw_name).parts
        project_index = next((index for index, part in enumerate(parts) if part.startswith("LoanSystem.")), None)
        if project_index is not None:
            raw_name = "/".join(parts[project_index:])
        elif "src" in parts:
            raw_name = "/".join(parts[parts.index("src") + 1:])
        if "/Migrations/" in f"/{raw_name}" or raw_name.endswith("NamespaceMarker.cs"):
            continue
        for line in source_class.findall("./lines/line"):
            key = (raw_name, int(line.attrib["number"]))
            lines[key] = max(lines[key], int(line.attrib["hits"]))

if not lines:
    raise SystemExit("Backend coverage gate failed: reports contained no production lines.")

def rate(selected: dict[tuple[str, int], int]) -> float:
    return 100.0 * sum(hit > 0 for hit in selected.values()) / len(selected)

def select(predicate):
    return {key: hit for key, hit in lines.items() if predicate(key[0])}

domain_application = select(lambda name: any(token in f"/{name}" for token in (
    "/LoanSystem.BuildingBlocks.Domain/", "/LoanSystem.BuildingBlocks.Application/", "/Domain/", "/Application/")))
if not domain_application:
    raise SystemExit("Backend coverage gate failed: no Domain/Application production lines were measured.")

business = domain_application
checks = [
    ("Domain/Application", rate(domain_application), 95.0),
    ("changed backend business code", rate(business), 90.0),
]
for label, actual, threshold in checks:
    print(f"{label} line coverage: {actual:.2f}% (required: {threshold:.2f}%)")

failures = [f"{label} {actual:.2f}% < {threshold:.2f}%" for label, actual, threshold in checks if actual < threshold]
if failures:
    raise SystemExit("Backend coverage gate failed: " + "; ".join(failures))
