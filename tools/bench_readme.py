#!/usr/bin/env python3

import argparse
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ARTIFACT = ROOT / "bench/results/MapLean.Bench.MapBench-report-github.md"
README = ROOT / "README.md"
COLUMNS = ("Method", "Mean", "Error", "Ratio", "Allocated")


def version_field():
    found = []
    for path in ROOT.rglob("*.csproj"):
        if any(part in {"bin", "obj"} for part in path.parts):
            continue
        for match in re.finditer(r"<Version>\s*([^<]+?)\s*</Version>", path.read_text()):
            found.append((path, match.group(1)))
    if len(found) != 1:
        raise SystemExit(f"expected one Version field, found {len(found)}: {found}")
    return found[0][1]


def commit():
    return subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()


def cells(line):
    return [part.strip() for part in line.strip().strip("|").split("|")]


def parse_artifact(text):
    start = text.find("```")
    end = text.find("```", start + 3)
    if start < 0 or end < 0:
        raise SystemExit("artifact has no host header fence")
    host = text[start : end + 3].strip("\n")
    table = [line for line in text[end + 3 :].splitlines() if line.startswith("|")]
    if len(table) < 3:
        raise SystemExit("artifact table is missing")
    header = cells(table[0])
    index = {name: header.index(name) for name in COLUMNS}
    rows = []
    for line in table[2:]:
        row = cells(line)
        rows.append(tuple(row[index[name]] for name in COLUMNS))
    return host, rows


def section(host, rows, version, sha):
    lines = [
        "# MapLean",
        "",
        "## Benchmark",
        "",
        f"Version {version}. Commit {sha}.",
        "",
        host,
        "",
        "| " + " | ".join(COLUMNS) + " |",
        "| --- | ---: | ---: | ---: | ---: |",
    ]
    for row in rows:
        lines.append("| " + " | ".join(row) + " |")
    lines.append("")
    return "\n".join(lines)


def parse_readme(text):
    version = re.search(r"^Version (\S+)\. Commit (\S+)\.$", text, re.M)
    if version is None:
        raise SystemExit("readme has no version line")
    start = text.find("```")
    end = text.find("```", start + 3)
    if start < 0 or end < 0:
        raise SystemExit("readme has no host header")
    host = text[start : end + 3].strip("\n")
    table = [line for line in text[end + 3 :].splitlines() if line.startswith("|")]
    if len(table) < 3:
        raise SystemExit("readme table is missing")
    header = cells(table[0])
    if tuple(header) != COLUMNS:
        raise SystemExit(f"readme columns {header} are not {COLUMNS}")
    rows = [tuple(cells(line)) for line in table[2:]]
    return version.group(1), host, rows


def differences(readme_text, artifact_text, version):
    got_version, got_host, got_rows = parse_readme(readme_text)
    host, rows = parse_artifact(artifact_text)
    problems = []
    if got_version != version:
        problems.append(f"version {got_version} diverges from {version}")
    if got_host != host:
        problems.append("host header diverges from the artifact")
    if got_rows != rows:
        problems.append(f"table diverges from the artifact: {got_rows} != {rows}")
    return problems


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true")
    parser.add_argument("--write", action="store_true")
    parser.add_argument("--readme", type=Path, default=README)
    parser.add_argument("--artifact", type=Path, default=ARTIFACT)
    args = parser.parse_args()
    version = version_field()
    artifact_text = args.artifact.read_text()
    if args.write:
        host, rows = parse_artifact(artifact_text)
        args.readme.write_text(section(host, rows, version, commit()))
        return
    problems = differences(args.readme.read_text(), artifact_text, version)
    if problems:
        print("\n".join(problems))
        return 1
    print("ok")
    return 0


if __name__ == "__main__":
    sys.exit(main())
