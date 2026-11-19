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
MEASURED = ("Method", "Mean", "Error", "StdDev", "Ratio", "RatioSD", "Allocated")
HEADING = "## Benchmark\n"


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


def version_at(sha):
    try:
        text = subprocess.check_output(
            ["git", "show", f"{sha}:src/MapLean/MapLean.csproj"],
            cwd=ROOT,
            text=True,
            stderr=subprocess.DEVNULL,
        )
    except subprocess.CalledProcessError:
        return None
    match = re.search(r"<Version>\s*([^<]+?)\s*</Version>", text)
    return None if match is None else match.group(1)


def commit():
    return subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip()


def cells(line):
    return [part.strip() for part in line.strip().strip("|").split("|")]


def parse_table(text, columns):
    table = [line for line in text.splitlines() if line.startswith("|")]
    if len(table) < 3:
        raise SystemExit("table is missing")
    header = cells(table[0])
    missing = [name for name in columns if name not in header]
    if missing:
        raise SystemExit(f"table is missing {missing}")
    index = {name: header.index(name) for name in header}
    rows = []
    for line in table[2:]:
        row = cells(line)
        rows.append({name: row[index[name]] for name in columns})
    return rows


def host_fence(text):
    start = text.find("```")
    end = text.find("```", start + 3)
    if start < 0 or end < 0:
        raise SystemExit("artifact has no host header fence")
    return text[start : end + 3].strip("\n")


def number(cell):
    match = re.search(r"[-+]?\d+(?:\.\d+)?", cell.replace(",", ""))
    if match is None:
        raise SystemExit(f"no number in {cell}")
    return float(match.group(0))


def separated(left, right):
    gap = abs(number(left["Mean"]) - number(right["Mean"]))
    return gap > number(left["Error"]) + number(right["Error"])


def clearly_different(manual, lean):
    if not separated(manual, lean):
        return False
    return abs(number(lean["Ratio"]) - 1) > 2 * number(lean["RatioSD"])


def speed_sentence(rows):
    by = {row["Method"]: row for row in rows}
    manual = by["Manual"]
    lean = by["MapLean"]
    auto = by["AutoMapper"]
    if not clearly_different(manual, lean):
        against_manual = "the same speed as the hand-written map"
    elif number(lean["Mean"]) < number(manual["Mean"]):
        against_manual = "faster than the hand-written map"
    else:
        against_manual = "slower than the hand-written map"
    factor = number(auto["Mean"]) / number(lean["Mean"])
    nearest = round(factor)
    if nearest > 0 and abs(factor - nearest) / nearest <= 0.15:
        against_auto = f"about {nearest:g}x faster than AutoMapper"
    else:
        against_auto = f"{factor:.2f} times the speed of AutoMapper"
    sentence = f"MapLean is {against_manual} and {against_auto}."
    if manual["Allocated"] != lean["Allocated"]:
        sentence += f" Manual allocates {manual['Allocated']} and MapLean allocates {lean['Allocated']}."
    return sentence


def benchmark_block(host, rows, version, sha):
    lines = [
        "## Benchmark",
        "",
        f"Version {version}. Commit {sha}.",
        "",
        speed_sentence(rows),
        "",
        host,
        "",
        "| " + " | ".join(COLUMNS) + " |",
        "| --- | ---: | ---: | ---: | ---: |",
    ]
    for row in rows:
        lines.append("| " + " | ".join(row[name] for name in COLUMNS) + " |")
    lines.append("")
    return "\n".join(lines)


def slice_bounds(text):
    if text.startswith(HEADING):
        start = 0
    else:
        found = text.find("\n" + HEADING)
        if found < 0:
            raise SystemExit("readme has no benchmark section")
        start = found + 1
    nxt = text.find("\n## ", start + len(HEADING))
    end = len(text) if nxt < 0 else nxt + 1
    return start, end


def benchmark_slice(text):
    start, end = slice_bounds(text)
    return text[start:end].strip("\n")


def parse_benchmark(text):
    version = re.search(r"^Version (\S+)\. Commit (\S+)\.$", text, re.M)
    if version is None:
        raise SystemExit("readme has no version line")
    sentence = re.search(r"^Version \S+\. Commit \S+\.\n\n(.+)\n\n```", text, re.M)
    if sentence is None:
        raise SystemExit("readme has no speed sentence")
    host = host_fence(text)
    visible = [tuple(row[name] for name in COLUMNS) for row in parse_table(text, COLUMNS)]
    return version.group(1), version.group(2), sentence.group(1), host, visible


def differences(readme_text, artifact_text, version):
    block = benchmark_slice(readme_text)
    got_version, got_sha, got_sentence, got_host, got_rows = parse_benchmark(block)
    host = host_fence(artifact_text)
    rows = parse_table(artifact_text, MEASURED)
    visible = [tuple(row[name] for name in COLUMNS) for row in rows]
    problems = []
    if got_version != version:
        problems.append(f"version {got_version} diverges from {version}")
    cited = version_at(got_sha)
    if cited != got_version:
        problems.append(f"version {got_version} is not the version at {got_sha}")
    if got_host != host:
        problems.append("host header diverges from the artifact")
    if got_rows != visible:
        problems.append(f"table diverges from the artifact: {got_rows} != {visible}")
    expected = speed_sentence(rows)
    if got_sentence != expected:
        problems.append(f"speed sentence diverges: {got_sentence!r} != {expected!r}")
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
        host = host_fence(artifact_text)
        rows = parse_table(artifact_text, MEASURED)
        block = benchmark_block(host, rows, version, commit())
        readme = args.readme.read_text()
        start, end = slice_bounds(readme)
        updated = readme[:start] + block + "\n" + readme[end:]
        args.readme.write_text(updated)
        return
    problems = differences(args.readme.read_text(), artifact_text, version)
    if problems:
        print("\n".join(problems))
        return 1
    print("ok")
    return 0


if __name__ == "__main__":
    sys.exit(main())
