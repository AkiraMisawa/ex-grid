"""Summarize recorded normal-GC observations; never treat them as live-object sizes."""

import json
import statistics
import sys

MIB = 1024 * 1024
for filename in sys.argv[1:]:
    result = json.load(open(filename))
    samples = result["memory"]
    first, last = samples[0], samples[-1]
    compactions = []
    capacity_growth = []
    for before, after in zip(samples, samples[1:]):
        if after["compacted"]:
            compactions.append({
                "update": after["update"],
                "intervalAllocatedMiB": (after["allocatedBytes"] - before["allocatedBytes"]) / MIB,
                "managedMiB": after["managedBytes"] / MIB,
                "wasmMiB": after["wasmHeapBytes"] / MIB,
                "retainedReports": after["roots"]["distinctRetainedReports"],
            })
        if after["wasmHeapBytes"] != before["wasmHeapBytes"]:
            capacity_growth.append({"update": after["update"], "wasmMiB": after["wasmHeapBytes"] / MIB})
    summary = {
        "file": filename,
        "completed": result["completed"],
        "errors": result["errors"],
        "initialManagedMiB": first["managedBytes"] / MIB,
        "finalManagedMiB": last["managedBytes"] / MIB,
        "maxObservedManagedMiB": max(s["managedBytes"] for s in samples) / MIB,
        "initialWasmMiB": first["wasmHeapBytes"] / MIB,
        "finalWasmMiB": last["wasmHeapBytes"] / MIB,
        "averageAllocatedMiBPerUpdate": (last["allocatedBytes"] - first["allocatedBytes"]) / MIB / result["completed"],
        "maxActionAddresses": max(s["roots"]["actionAddresses"] for s in samples),
        "maxOperationReports": max(s["roots"]["heldReports"] for s in samples),
        "maxDistinctRetainedReports": max(s["roots"]["distinctRetainedReports"] for s in samples),
        "finalRoots": last["roots"],
        "capacityGrowth": capacity_growth,
        "compactions": compactions,
        "medianCompactionIntervalAllocatedMiB": statistics.median(c["intervalAllocatedMiB"] for c in compactions) if compactions else None,
    }
    print(json.dumps(summary, indent=2))
