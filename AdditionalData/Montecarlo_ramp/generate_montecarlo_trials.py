#!/usr/bin/env python3
"""
Generate Monte Carlo trials for on-site construction duration under the
Integrated Edge-Ramp (Adaptive) logistics model.

This script produces 'montecarlo_edge_ramp_trials.csv' with per-trial results
for two scenarios:
  - baseline:      base headway = 4 minutes per active ramp
  - conservative:  base headway = 6 minutes per active ramp

Headway jitter and occasional stop downtime (MTBF/repair) are included.
Corner-turn/speed effects are assumed already encoded in headway.
"""

import argparse
import numpy as np
import pandas as pd
from datetime import datetime

# --- Model constants (as in manuscript tables/text) ---
PHASES = [
    {"name": "Phase 1 (16 ramps; 12 up)", "blocks": 317_000,  "up_ramps": 12},
    {"name": "Phase 2 (8 ramps; 6 up)",   "blocks": 315_000,  "up_ramps": 6},
    {"name": "Phase 3a (4 ramps; 3 up)",  "blocks": 1_677_000,"up_ramps": 3},
    {"name": "Phase 3b (2 ramps; 2 up)",  "blocks": 3_300,    "up_ramps": 2},
    {"name": "Phase 3c (1 ramp; 1 up)",   "blocks": 170,      "up_ramps": 1},
]

WORKING_MIN_PER_YEAR = 10*60*6*52  # 187,200 minutes/year

def run_trials(n_trials:int,
               base_headway_min:float,
               headway_abs_jitter_min:float=1.0,
               mtbf_hours:float=6.0,
               repair_min_low:float=5.0,
               repair_min_high:float=12.0,
               seed:int|None=None) -> pd.DataFrame:
    """
    Run Monte Carlo trials.
    - base_headway_min: nominal headway per active ramp (minutes)
    - headway_abs_jitter_min: draw headway uniformly in [base - jitter, base + jitter]
    - mtbf_hours: mean time between failures (hours) per ramp system
    - repair_min_low/high: repair duration (minutes), drawn uniformly per trial
    Returns a DataFrame with per-trial values and total years.
    """
    rng = np.random.default_rng(seed)

    # Headway per trial (minutes)
    headway = rng.uniform(base_headway_min - headway_abs_jitter_min,
                          base_headway_min + headway_abs_jitter_min,
                          size=n_trials)

    # Simple availability factor from MTBF and repair times
    repair_times = rng.uniform(repair_min_low, repair_min_high, size=n_trials)
    mtbf_min = mtbf_hours * 60.0
    availability = mtbf_min / (mtbf_min + repair_times)  # ~0.977 on average

    total_minutes = np.zeros(n_trials, dtype=float)
    for ph in PHASES:
        minutes_phase = (ph["blocks"] / ph["up_ramps"]) * headway
        # Adjust for downtime
        minutes_phase = minutes_phase / availability
        total_minutes += minutes_phase

    years = total_minutes / WORKING_MIN_PER_YEAR

    return pd.DataFrame({
        "trial": np.arange(1, n_trials + 1, dtype=int),
        "base_headway_min": base_headway_min,
        "headway_sampled_min": headway,
        "mtbf_min": mtbf_min,
        "repair_time_min": repair_times,
        "availability": availability,
        "total_minutes": total_minutes,
        "years": years
    })

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--trials", type=int, default=10000,
                        help="Trials per scenario (default 10000).")
    parser.add_argument("--seed", type=int, default=2025,
                        help="Random seed base (default 2025).")
    parser.add_argument("--outfile", type=str, default="montecarlo_edge_ramp_trials.csv",
                        help="Output CSV filename.")
    parser.add_argument("--headway_jitter", type=float, default=1.0,
                        help="Absolute headway jitter in minutes (±). Default ±1.0.")
    args = parser.parse_args()

    # Two scenarios: 4-min baseline and 6-min conservative
    base_df = run_trials(n_trials=args.trials, base_headway_min=4.0,
                         headway_abs_jitter_min=args.headway_jitter, seed=args.seed + 1)
    base_df.insert(0, "scenario", "4-min (baseline)")

    cons_df = run_trials(n_trials=args.trials, base_headway_min=6.0,
                         headway_abs_jitter_min=args.headway_jitter, seed=args.seed + 2)
    cons_df.insert(0, "scenario", "6-min (conservative)")

    out = pd.concat([base_df, cons_df], ignore_index=True)
    out.to_csv(args.outfile, index=False)

    # Quick summaries
    def summarize(x):
        qs = np.percentile(x, [2.5, 50, 97.5])
        return qs[1], qs[0], qs[2]

    b50, blo, bhi = summarize(base_df["years"].values)
    c50, clo, chi = summarize(cons_df["years"].values)

    print("Created:", args.outfile)
    print("Trials per scenario:", args.trials)
    print(f"[Baseline 4-min]      median={b50:.2f}  95%[{blo:.2f}, {bhi:.2f}] years")
    print(f"[Conservative 6-min]  median={c50:.2f}  95%[{clo:.2f}, {chi:.2f}] years")
    print("Timestamp:", datetime.utcnow().isoformat(), "UTC")

if __name__ == "__main__":
    main()