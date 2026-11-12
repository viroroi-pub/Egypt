#!/usr/bin/env python3
from pathlib import Path
import numpy as np
import pandas as pd
import matplotlib.pyplot as plt

trials_path = Path("khafre_on_site_duration_trials.csv")
trials = pd.read_csv(trials_path)

samples = {}
labels = {
    "baseline": "4 min (baseline)",
    "conservative": "6 min (conservative)",
}

for scenario in labels:
    vals = trials.loc[trials["scenario"] == scenario, "duration_years"].to_numpy(dtype=float)
    if len(vals) == 0 or not np.isfinite(vals).all() or (vals <= 0).any():
        raise ValueError(f"Invalid or missing duration trials: {scenario}")
    samples[scenario] = vals

fig, ax = plt.subplots(figsize=(12, 6), dpi=200)
ax.boxplot(
    [samples["baseline"], samples["conservative"]],
    tick_labels=[labels["baseline"], labels["conservative"]],
    whis=(2.5, 97.5),
    showfliers=False,
)
ax.axhline(25, linestyle="--", linewidth=2)
ax.text(2.45, 25.25, "Khafre ~25 years", ha="right", va="bottom", fontsize=12)
ax.set_title("On-site duration — Khafre Monte Carlo (median, IQR, and 95% whiskers)", fontsize=20)
ax.set_ylabel("Years", fontsize=14)
ax.set_xlabel("Scenario", fontsize=14)
ax.set_ylim(0, 30)
ax.grid(True, axis="y", linestyle="--", alpha=0.5)
ax.grid(True, axis="x", linestyle="--", alpha=0.35)
fig.tight_layout()
fig.savefig("khafre_montecarlo_boxplot_custom_labels.png", bbox_inches="tight")
fig.savefig("khafre_montecarlo_boxplot_custom_labels.svg", bbox_inches="tight")
