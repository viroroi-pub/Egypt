#!/usr/bin/env python3
"""
edge_ramp_capacity_safety_headway.py

Monte Carlo capacity model for the Integrated Edge-Ramp (IER) phases.

This version computes ramp capacity from the effective safety headway,
not from mechanical work scaling. Friction affects capacity indirectly:
higher friction -> larger pulling crew -> longer occupied cell / safety
distance -> longer headway -> lower blocks/hour.

Baseline example:
    theta = 7 deg
    mu = 0.30
    team = 32 workers
    safety distance = 41 m
    effective headway = 4.56 min
"""

from __future__ import annotations

import argparse
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, List

import numpy as np
import pandas as pd
import matplotlib.pyplot as plt


@dataclass(frozen=True)
class Phase:
    phase: str
    up_ramps: int


# Adaptive IER phases: 16->8->4->2->1 system.
# "up_ramps" follows the operational allocation used in the model.
PHASES: List[Phase] = [
    Phase("1", 12),
    Phase("2", 6),
    Phase("3a", 3),
    Phase("3b", 2),
    Phase("3c", 1),
]


def crew_size(theta_deg: float, mu: float) -> int:
    """
    Estimate pullers per block.

    Values follow the article's Table 1 around 6–8° and μ = 0.1–0.8.
    For the current use case, theta=7, mu=0.30 -> 32 workers.

    The calculation uses the physical force model:
        F = m g (sin(theta) + mu cos(theta))
    and divides by an assumed sustained 300 N/puller, rounded up.
    """
    block_mass_kg = 2267.96
    g = 9.81
    force_per_worker_n = 300.0
    theta = np.deg2rad(theta_deg)
    force = block_mass_kg * g * (np.sin(theta) + mu * np.cos(theta))
    return int(np.ceil(force / force_per_worker_n))


def team_length_m(workers: int) -> float:
    """
    Convert crew size to team length.

    Calibrated to the published values:
      24 workers -> 17.0 m
      32 workers -> 23.0 m

    Assumes two-file organization and 1.5 m longitudinal spacing
    plus a small formation allowance.
    """
    return 1.5 * np.ceil(workers / 2.0) - 1.0


def safety_distance_m(workers: int, sledge_length_m: float = 3.0, buffer_m: float = 15.0) -> float:
    """
    Minimum occupied/safety distance:
        team length + sledge length + dynamic buffer
    """
    return team_length_m(workers) + sledge_length_m + buffer_m


def headway_from_safety_distance(distance_m: float, ramp_speed_mps: float = 0.15) -> float:
    """
    Convert minimum safety distance to headway in minutes:
        headway = distance / speed
    """
    return distance_m / ramp_speed_mps / 60.0


def run_monte_carlo(
    theta_deg: float = 7.0,
    mu: float = 0.30,
    n: int = 10_000,
    seed: int = 42,
    speed_sigma: float = 0.10,
    headway_sigma: float = 0.06,
) -> tuple[pd.DataFrame, pd.DataFrame, Dict[str, float]]:
    """
    Generate phase capacity distributions.

    Stochastic components:
      - ramp speed lognormal variation, centered on 0.15 m/s
      - headway operational variation, centered on the safety-derived headway

    Capacity per phase:
      total_capacity_blocks_h = up_ramps * 60 / effective_headway_min
    """
    rng = np.random.default_rng(seed)

    workers = crew_size(theta_deg, mu)
    length = team_length_m(workers)
    safety = safety_distance_m(workers)
    base_headway = headway_from_safety_distance(safety)

    # Variability centered around 1.0.
    speed_factor = rng.lognormal(mean=-0.5 * speed_sigma**2, sigma=speed_sigma, size=n)
    operational_factor = rng.lognormal(mean=-0.5 * headway_sigma**2, sigma=headway_sigma, size=n)

    # Higher speed lowers headway; operational factor adds real-world dispersion.
    effective_headway = base_headway * operational_factor / speed_factor

    trials = []
    for ph in PHASES:
        capacity = ph.up_ramps * 60.0 / effective_headway
        for i in range(n):
            trials.append(
                {
                    "trial": i,
                    "phase": ph.phase,
                    "up_ramps": ph.up_ramps,
                    "theta_deg": theta_deg,
                    "mu": mu,
                    "workers": workers,
                    "team_length_m": length,
                    "safety_distance_m": safety,
                    "base_headway_min": base_headway,
                    "effective_headway_min": effective_headway[i],
                    "total_capacity_blocks_per_hour": capacity[i],
                }
            )

    trials_df = pd.DataFrame(trials)

    summary_df = (
        trials_df.groupby("phase", sort=False)
        .agg(
            up_ramps=("up_ramps", "first"),
            median_capacity_blocks_per_hour=("total_capacity_blocks_per_hour", "median"),
            p10_capacity_blocks_per_hour=("total_capacity_blocks_per_hour", lambda x: np.percentile(x, 10)),
            p90_capacity_blocks_per_hour=("total_capacity_blocks_per_hour", lambda x: np.percentile(x, 90)),
            mean_capacity_blocks_per_hour=("total_capacity_blocks_per_hour", "mean"),
            median_headway_min=("effective_headway_min", "median"),
            p10_headway_min=("effective_headway_min", lambda x: np.percentile(x, 10)),
            p90_headway_min=("effective_headway_min", lambda x: np.percentile(x, 90)),
        )
        .reset_index()
    )

    metadata = {
        "theta_deg": theta_deg,
        "mu": mu,
        "workers": workers,
        "team_length_m": length,
        "safety_distance_m": safety,
        "base_headway_min": base_headway,
        "n": n,
        "seed": seed,
    }

    return summary_df, trials_df, metadata


def plot_summary(summary_df: pd.DataFrame, output_png: Path) -> None:
    phases = summary_df["phase"].astype(str).tolist()
    x = np.arange(len(phases))

    med = summary_df["median_capacity_blocks_per_hour"].to_numpy()
    p10 = summary_df["p10_capacity_blocks_per_hour"].to_numpy()
    p90 = summary_df["p90_capacity_blocks_per_hour"].to_numpy()
    yerr = np.vstack([med - p10, p90 - med])

    plt.figure(figsize=(16, 8))
    plt.errorbar(
        x,
        med,
        yerr=yerr,
        fmt="o",
        capsize=5,
        linewidth=2,
        markersize=7,
        color="#E69F00",
    )
    plt.xticks(x, phases)
    plt.xlabel("Phase", fontsize=16)
    plt.ylabel("Blocks per hour", fontsize=16)
    plt.title("Median Total Capacity per Phase (with P10–P90)", fontsize=22)
    plt.grid(True, linestyle="--", alpha=0.7)
    plt.tight_layout()
    plt.savefig(output_png, dpi=300)
    plt.close()


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--theta", type=float, default=7.0, help="Ramp angle in degrees.")
    parser.add_argument("--mu", type=float, default=0.30, help="Kinetic friction coefficient.")
    parser.add_argument("--n", type=int, default=10_000, help="Monte Carlo trials.")
    parser.add_argument("--seed", type=int, default=42, help="Random seed.")
    parser.add_argument("--outdir", type=Path, default=Path("."), help="Output directory.")
    args = parser.parse_args()

    args.outdir.mkdir(parents=True, exist_ok=True)

    summary, trials, meta = run_monte_carlo(
        theta_deg=args.theta,
        mu=args.mu,
        n=args.n,
        seed=args.seed,
    )

    suffix = f"mu{args.mu:.2f}_theta{args.theta:.1f}".replace(".", "p")
    summary_path = args.outdir / f"edge_ramp_montecarlo_summary_by_phase_{suffix}_safety_headway.csv"
    trials_path = args.outdir / f"edge_ramp_montecarlo_trials_by_phase_{suffix}_safety_headway.csv"
    png_path = args.outdir / f"median_total_capacity_per_phase_{suffix}_safety_headway.png"

    summary.to_csv(summary_path, index=False)
    trials.to_csv(trials_path, index=False)
    plot_summary(summary, png_path)

    print("Metadata:")
    for k, v in meta.items():
        print(f"  {k}: {v}")
    print("\nFiles written:")
    print(f"  {summary_path}")
    print(f"  {trials_path}")
    print(f"  {png_path}")


if __name__ == "__main__":
    main()
