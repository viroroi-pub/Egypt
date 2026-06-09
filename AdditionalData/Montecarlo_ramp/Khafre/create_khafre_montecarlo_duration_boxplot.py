#!/usr/bin/env python3
from pathlib import Path
import math
import numpy as np
import pandas as pd
import matplotlib.pyplot as plt

summary_path = Path("montecarlo_edge_ramp_summary_khafre.csv")
summary = pd.read_csv(summary_path)

rng = np.random.default_rng(42)
samples = {}
labels = {
    "baseline": "4-headways (baseline)",
    "conservative": "6-headways (conservative)",
}

for _, row in summary.iterrows():
    scenario = row["scenario"]
    n = int(row["n"])
    median = float(row["median_years"])
    ci_low = float(row["ci2.5_years"])
    ci_high = float(row["ci97.5_years"])

    mu_log = math.log(median)
    sigma_log = (math.log(ci_high) - math.log(ci_low)) / (2 * 1.959963984540054)
    vals = rng.lognormal(mean=mu_log, sigma=sigma_log, size=n)

    p2_5, p50, p97_5 = np.percentile(vals, [2.5, 50, 97.5])
    lower_mask = vals <= p50
    upper_mask = vals > p50
    vals_scaled = vals.copy()
    vals_scaled[lower_mask] = median - (p50 - vals[lower_mask]) * (median - ci_low) / (p50 - p2_5)
    vals_scaled[upper_mask] = median + (vals[upper_mask] - p50) * (ci_high - median) / (p97_5 - p50)
    samples[scenario] = vals_scaled

fig, ax = plt.subplots(figsize=(12, 6), dpi=200)
ax.boxplot(
    [samples["baseline"], samples["conservative"]],
    labels=[labels["baseline"], labels["conservative"]],
    whis=(2.5, 97.5),
    showfliers=False,
)
ax.axhline(25, linestyle="--", linewidth=2)
ax.text(2.45, 25.25, "Khafre ~25 years", ha="right", va="bottom", fontsize=12)
ax.set_title("On-site duration — Khafre Monte Carlo (median, IQR≈, and 95% whiskers)", fontsize=20)
ax.set_ylabel("Years", fontsize=14)
ax.set_xlabel("Scenario", fontsize=14)
ax.set_ylim(0, 30)
ax.grid(True, axis="y", linestyle="--", alpha=0.5)
ax.grid(True, axis="x", linestyle="--", alpha=0.35)
fig.tight_layout()
fig.savefig("khafre_montecarlo_boxplot_custom_labels.png", bbox_inches="tight")
fig.savefig("khafre_montecarlo_boxplot_custom_labels.svg", bbox_inches="tight")
