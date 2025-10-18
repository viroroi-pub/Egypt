
import math
import numpy as np
import pandas as pd

PHASES = [
    {"phase_id": "1",  "phase_label": "Phase 1",  "ramp_configuration": "16 straight edge-ramps", "ramps_up": 12, "ramps_down": 4, "turns_per_block": 0, "base_service_time_min": 3.5},
    {"phase_id": "2",  "phase_label": "Phase 2",  "ramp_configuration": "8 straight edge-ramps",  "ramps_up": 6,  "ramps_down": 2, "turns_per_block": 0, "base_service_time_min": 3.6},
    {"phase_id": "3a", "phase_label": "Phase 3a", "ramp_configuration": "4 helical edge-ramps",  "ramps_up": 3,  "ramps_down": 1, "turns_per_block": 1, "base_service_time_min": 3.8},
    {"phase_id": "3b", "phase_label": "Phase 3b", "ramp_configuration": "2 helical edge-ramps",  "ramps_up": 2,  "ramps_down": 0, "turns_per_block": 1, "base_service_time_min": 3.9},
    {"phase_id": "3c", "phase_label": "Phase 3c", "ramp_configuration": "1 helical edge-ramp",   "ramps_up": 1,  "ramps_down": 0, "turns_per_block": 2, "base_service_time_min": 4.0},
]

def s_mu_theta(mu, theta_deg, mu0=0.20, theta0_deg=7.0):
    th = math.radians(theta_deg); th0 = math.radians(theta0_deg)
    return (math.sin(th) + mu*math.cos(th)) / (math.sin(th0) + mu0*math.cos(th0))

def run_trials(mu: float, theta_deg: float, N: int = 10000, seed: int = 20250905):
    rng = np.random.default_rng(seed)
    scale = s_mu_theta(mu, theta_deg)

    frames = []
    for ph in PHASES:
        headway = 4.0 + rng.uniform(-1.0, 1.0, size=N)
        headway = np.clip(headway, 2.0, 8.0)
        speed_factor = rng.uniform(0.75, 1.25, size=N)

        mu_ln = math.log(2.8)
        sigma_ln = 0.35
        corner_delay = rng.lognormal(mean=mu_ln, sigma=sigma_ln, size=N)
        heavy = rng.random(N) < 0.10
        corner_delay[heavy] *= rng.uniform(1.1, 1.4, size=heavy.sum())

        hauling = (ph["base_service_time_min"] / speed_factor) * scale
        turning = ph["turns_per_block"] * corner_delay
        service_time = hauling + turning

        arrival_rate = 1.0 / headway
        service_rate = 1.0 / service_time

        rho = np.clip(arrival_rate / service_rate, 1e-6, 0.995)
        Wq = rho / (2.0 * service_rate * (1.0 - rho))
        Lq = arrival_rate * Wq

        waiting_probability = rho
        blocking_probability = np.clip((rho - 0.85) / 0.15, 0.0, 1.0) * 0.30

        per_ramp_capacity_bph = 60.0 * np.minimum(arrival_rate, service_rate) * (1.0 - blocking_probability)
        total_capacity_bph = per_ramp_capacity_bph * ph["ramps_up"]

        df = pd.DataFrame({
            "trial_id": np.arange(1, N+1, dtype=int),
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
            "corner_delay_per_turn_min": corner_delay if ph["turns_per_block"]>0 else np.zeros_like(corner_delay),
            "service_time_min": service_time,
            "arrival_rate_blocks_per_min": arrival_rate,
            "service_rate_blocks_per_min": service_rate,
            "utilization": rho,
            "per_ramp_capacity_blocks_per_hour": per_ramp_capacity_bph,
            "total_capacity_blocks_per_hour": total_capacity_bph,
            "mean_corner_queue_length": Lq,
            "waiting_probability": waiting_probability,
            "blocking_probability": blocking_probability,
            "settings": "±25% speed; ±1 min headway; lognormal corner delays (median 2.8 min, σ=0.35); hauling term scaled by S(mu,θ)",
            "S_mu_theta": scale,
        })
        frames.append(df)

    return pd.concat(frames, ignore_index=True)

def save_trials_csv(mu: float, theta_deg: float, N: int, out_csv: str, seed: int = 20250905) -> str:
    df = run_trials(mu=mu, theta_deg=theta_deg, N=N, seed=seed)
    df.to_csv(out_csv, index=False)
    return out_csv
