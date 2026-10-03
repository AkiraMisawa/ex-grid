"""Transcribe the October 3 Excel observations, preserving input kinds and numeric bits.

Run with function names as arguments. Existing, explicit ADR refusals are retained on a rerun.
This imports the first completed calculation; lifecycle cases need separate behavioral tests.
"""
import json
from pathlib import Path
import struct
import sys

ROOT = Path(__file__).resolve().parents[3]
EVIDENCE = ROOT / "verification/2026-10-03-windows-functions"
DESTINATION = ROOT / "tests/ExSheet.Engine.Tests/ExcelCases"


def read(name):
    return json.loads((EVIDENCE / name).read_text(encoding="utf-8-sig"))


def convert(source, result):
    values, cells = {}, {}
    for address, value in source["cells"].items():
        if isinstance(value, str) and value.startswith("="):
            cells[address] = value
        else:
            values[address] = value
    cells["Z1"] = source["formula"]
    case = {"id": source["id"].replace(".", ""), "source": "observed",
            "description": source["question"], "culture": "en-GB",
            "observation": "verification/2026-10-03-windows-functions/" + source["id"],
            "values": values, "cells": cells, "check": "Z1"}
    if result["status"] == "refused-entry":
        case["expect"] = {"refusedEntry": True}
    else:
        assert result["status"] == "observed"
        cell = result["states"][0]["cells"][0]
        if cell["kind"] == "number":
            value = float(cell["roundTrip"])
            assert struct.pack(">d", value).hex().upper() == cell["bits"]
            case["excelBits"] = cell["bits"]
        else:
            value = cell["text"] if cell["kind"] == "error" else cell["value2"]
        case["expect"] = {"value2": value}
        if cell["kind"] == "text":
            case["expect"]["kind"] = "text"
    return case


def main(functions):
    collected = {function: [] for function in functions}
    for inputs, outputs in [("cases.json", "results.json"),
                            ("followup-cases.json", "followup-results.json"),
                            ("additional-cases.json", "additional-results.json")]:
        results = {case["id"]: case for case in read(outputs)["cases"]}
        for source in read(inputs)["cases"]:
            if source["function"] in collected:
                collected[source["function"]].append(convert(source, results[source["id"]]))
    for function, cases in collected.items():
        assert cases, function
        area = function.lower().replace(".", "-")
        target = DESTINATION / (area + ".json")
        previous = {c["id"]: c for c in json.loads(target.read_text())["cases"]} if target.exists() else {}
        for case in cases:
            old = previous.get(case["id"], {})
            if "engineDiffersByDecision" in old:
                case["excelExpect"] = case["expect"]
                case["expect"] = old["expect"]
                case["engineDiffersByDecision"] = old["engineDiffersByDecision"]
        target.write_text(json.dumps({"area": area,
            "about": f"{function}: Windows Excel 16.0.20430.20092, en-GB; October 3 observations.",
            "cases": cases}, ensure_ascii=False, indent=2) + "\n")
        print(f"{area}: {len(cases)} observed cases")


if __name__ == "__main__":
    if not sys.argv[1:]:
        raise SystemExit("Pass the function names to import.")
    main([name.upper() for name in sys.argv[1:]])
