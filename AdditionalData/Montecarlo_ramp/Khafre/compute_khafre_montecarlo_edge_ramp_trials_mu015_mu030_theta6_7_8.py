#!/usr/bin/env python3
"""
Generate Khafre/G2 Monte Carlo edge-ramp trial files for:
μ = 0.15 and 0.30; θr = 6°, 7°, and 8°; N = 10,000 trials.

This script keeps the Khufu Monte Carlo traffic/queuing structure:
- adaptive phases 16 → 8 → 4 → 2 → 1
- ±25% speed variation
- ±1 min headway perturbation
- lognormal corner delays, median 2.8 min, sigma 0.35
- realized capacity limited by min(arrival_rate, service_rate)
- Khafre reference scaling θ0 = 7.5°, μ0 = 0.20
"""
import math
import zipfile
from pathlib import Path
import numpy as np
import pandas as pd

PHASES = [
    {"phase_id": "1",  "phase_label": "Phase 1",  "ramp_configuration": "16 straight edge-ramps", "ramps_up": 12, "ramps_down": 4, "turns_per_block": 0, "base_service_time_min": 3.5},
    {"phase_id": "2",  "phase_label": "Phase 2",  "ramp_configuration": "8 straight edge-ramps",  "ramps_up": 6,  "ramps_down": 2, "turns_per_block": 0, "base_service_time_min": 3.6},
    {"phase_id": "3a", "phase_label": "Phase 3a", "ramp_configuration": "4 helical edge-ramps",  "ramps_up": 3,  "ramps_down": 1, "turns_per_block": 1, "base_service_time_min": 3.8},
    {"phase_id": "3b", "phase_label": "Phase 3b", "ramp_configuration": "2 helical edge-ramps",  "ramps_up": 2,  "ramps_down": 0, "turns_per_block": 1, "base_service_time_min": 3.9},
    {"phase_id": "3c", "phase_label": "Phase 3c", "ramp_configuration": "1 helical edge-ramp",   "ramps_up": 1,  "ramps_down": 0, "turns_per_block": 2, "base_service_time_min": 4.0},
]

def s_mu_theta(mu, theta_deg, mu0=0.20, theta0_deg=7.5):
    th = math.radians(theta_deg)
    th0 = math.radians(theta0_deg)
    return (math.sin(th) + mu * math.cos(th)) / (math.sin(th0) + mu0 * math.cos(th0))

def run_trials_khafre(mu, theta_deg, N=10000, seed=42):
    rng = np.random.default_rng(seed)
    scale = s_mu_theta(mu, theta_deg)
    frames = []
    for ph in PHASES:
        headway = np.clip(4.0 + rng.uniform(-1.0, 1.0, size=N), 2.0, 8.0)
        speed_factor = rng.uniform(0.75, 1.25, size=N)
        corner_delay = rng.lognormal(mean=math.log(2.8), sigma=0.35, size=N)
        heavy = rng.random(N) < 0.10
        corner_delay[heavy] *= rng.uniform(1.1, 1.4, size=heavy.sum())
        hauling = (ph["base_service_time_min"] / speed_factor) * scale
        service_time = hauling + ph["turns_per_block"] * corner_delay
        arrival_rate = 1.0 / headway
        service_rate = 1.0 / service_time
        utilization = np.clip(arrival_rate / service_rate, 1e-6, 0.995)
        Wq = utilization / (2.0 * service_rate * (1.0 - utilization))
        Lq = arrival_rate * Wq
        blocking_probability = np.clip((utilization - 0.85) / 0.15, 0.0, 1.0) * 0.30
        per_ramp_capacity_bph = 60.0 * np.minimum(arrival_rate, service_rate) * (1.0 - blocking_probability)
        total_capacity_bph = per_ramp_capacity_bph * ph["ramps_up"]
        frames.append(pd.DataFrame({
            "trial_id": np.arange(1, N + 1, dtype=int),
            "mu": mu,
            "theta_deg": theta_deg,
            "phase_id": ph["phase_id"],
            "phase_label": ph["phase_label"],
            "ramp_configuration": ph["ramp_configuration"],
            "ramps_up": ph["ramps_up"],
            "ramps_down": ph["ramps_down"],
            "turns_per_block": ph["turns_per_block"],
            "speed_factor": speed_factor,
            "headway_arrival_min": headway,
            "corner_delay_per_turn_min": corner_delay if ph["turns_per_block"] > 0 else np.zeros_like(corner_delay),
            "service_time_min": service_time,
            "arrival_rate_blocks_per_min": arrival_rate,
            "service_rate_blocks_per_min": service_rate,
            "utilization": utilization,
            "per_ramp_capacity_blocks_per_hour": per_ramp_capacity_bph,
            "total_capacity_blocks_per_hour": total_capacity_bph,
            "mean_corner_queue_length": Lq,
            "waiting_probability": utilization,
            "blocking_probability": blocking_probability,
            "mc_settings": "Khafre/G2; ±25% speed; ±1 min headway; lognormal corner delays (median 2.8 min, σ=0.35); hauling term scaled by S(mu,θ); θ0=7.5°",
            "S_mu_theta": scale,
        }))
    return pd.concat(frames, ignore_index=True)

def file_mu_label(mu):
    if abs(mu - 0.15) < 1e-12:
        return "mu0p15"
    if abs(mu - 0.30) < 1e-12:
        return "mu0p3"
    return f"mu{str(mu).replace('.', 'p')}"

def main():
    out_dir = Path("khafre_montecarlo_edge_ramp_trials_mu015_mu030_theta6_7_8")
    out_dir.mkdir(parents=True, exist_ok=True)
    metrics = [
        "total_capacity_blocks_per_hour",
        "per_ramp_capacity_blocks_per_hour",
        "utilization",
        "mean_corner_queue_length",
        "waiting_probability",
        "blocking_probability",
        "service_time_min",
        "headway_arrival_min",
    ]
    summary_rows = []
    scenario_files = []
    for mu in [0.30, 0.15]:
        for theta in [6, 7, 8]:
            df = run_trials_khafre(mu, theta, N=10000, seed=42)
            path = out_dir / f"montecarlo_edge_ramp_trials_{file_mu_label(mu)}_theta{theta}_10000.csv"
            df.to_csv(path, index=False)
            scenario_files.append(path)
            grouped = df.groupby(["mu", "theta_deg", "phase_id", "phase_label", "ramp_configuration", "ramps_up", "ramps_down"], sort=False)
            for keys, g in grouped:
                row = {
                    "mu": keys[0],
                    "theta_deg": keys[1],
                    "phase_id": keys[2],
                    "phase_label": keys[3],
                    "ramp_configuration": keys[4],
                    "ramps_up": keys[5],
                    "ramps_down": keys[6],
                    "n_trials": len(g),
                    "S_mu_theta": float(g["S_mu_theta"].iloc[0]),
                }
                for metric in metrics:
                    arr = g[metric].to_numpy(dtype=float)
                    row[f"{metric}_median"] = float(np.percentile(arr, 50))
                    row[f"{metric}_ci_lower"] = float(np.percentile(arr, 2.5))
                    row[f"{metric}_ci_upper"] = float(np.percentile(arr, 97.5))
                summary_rows.append(row)
    summary_path = out_dir / "montecarlo_edge_ramp_trials_summary_mu015_mu030_theta6_7_8.csv"
    pd.DataFrame(summary_rows).to_csv(summary_path, index=False)
    zip_path = Path("montecarlo_edge_ramp_trials_mu015_mu030_theta6_7_8_khafre.zip")
    with zipfile.ZipFile(zip_path, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=6) as zf:
        for p in scenario_files + [summary_path]:
            zf.write(p, arcname=p.name)
    print(f"Created {zip_path}")

if __name__ == "__main__":
    main()
