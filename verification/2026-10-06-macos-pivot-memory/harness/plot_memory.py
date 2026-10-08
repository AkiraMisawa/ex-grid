"""Plot the two controlled, forced-GC interventions from retained JSON evidence."""
import argparse
import json
from pathlib import Path

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.ticker import MaxNLocator

parser = argparse.ArgumentParser()
parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
parser.add_argument("--output", type=Path)
args = parser.parse_args()
output = args.output or args.root / "memory-retention.png"

fig, axes = plt.subplots(1, 2, figsize=(11, 4.9), gridspec_kw={"width_ratios": [1.35, 1]})
colours = ["#b34131", "#177a70"]
for name, label, colour in zip(
    ["gc-only", "drop-paints-gc"],
    ["Original retention + forced GC", "Clear paint history + forced GC (diagnostic)"],
    colours,
):
    data = json.loads((args.root / "raw" / name / "result.json").read_text())
    samples = data["memory"]
    assert all(row["forcedGc"] for row in samples)
    x = [row["update"] for row in samples]
    axes[0].plot(x, [row["managedBytes"] / 2**20 for row in samples], "o-", color=colour,
                 label=label, linewidth=2, markersize=4)
    axes[1].plot(x, [row["tracked"]["Report"]["alive"] for row in samples], "o-",
                 color=colour, linewidth=2, markersize=4)
    if name == "gc-only":
        assert data["completed"] == 6 and data["errors"]
        axes[0].annotate("Update 7: OOM", xy=(x[-1], samples[-1]["managedBytes"] / 2**20),
                         xytext=(7.1, 1250), fontsize=10, color=colour,
                         arrowprops={"arrowstyle": "->", "color": colour})
    else:
        assert data["completed"] >= 12 and not data["errors"]

axes[0].set(ylabel="Managed memory after full GC (MiB)", ylim=(0, 1400))
axes[1].set(ylabel="Surviving Report generations", ylim=(0, 8))
for ax in axes:
    ax.set(xlabel="Completed updates (0 = initial report)", xlim=(-0.3, 12.5))
    ax.xaxis.set_major_locator(MaxNLocator(integer=True))
    ax.yaxis.set_major_locator(MaxNLocator(integer=True))
    ax.grid(axis="y", alpha=0.18)
    ax.spines[["top", "right"]].set_visible(False)
    ax.tick_params(labelsize=9)

fig.suptitle("Old Report generations remain reachable through paint history", x=0.065,
             ha="left", fontsize=15, fontweight="bold", y=0.99)
fig.text(0.065, 0.925, "401,001 report rows  |  1,000 changes per update  |  published WebAssembly",
         fontsize=10, color="#454b52")
handles, labels = axes[0].get_legend_handles_labels()
fig.legend(handles, labels, loc="upper left", bbox_to_anchor=(0.056, 0.885), ncol=2,
           frameon=False, fontsize=9)
fig.text(0.065, 0.045,
         "Clearing paint history disables write-refusal history: this is a causal experiment, not a product fix.\n"
         "Both runs force GC after each frame. Source: raw/gc-only and raw/drop-paints-gc.",
         fontsize=8.5, color="#454b52", va="bottom")
fig.subplots_adjust(left=0.08, right=0.98, top=0.76, bottom=0.23, wspace=0.3)
output.parent.mkdir(parents=True, exist_ok=True)
fig.savefig(output, dpi=180, facecolor="white")
print(output)
