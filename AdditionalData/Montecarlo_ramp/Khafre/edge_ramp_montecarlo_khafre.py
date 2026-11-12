"""Monte Carlo dispatch-capacity model for Khafre (G2).

Requested and nominal minimum headways share a uniform perturbation.
The sampled minimum implies a variable speed, referenced to 0.15 m/s.
Availability is included once. Corner delays are diagnostic; queue and
blocking metrics are unspecified (NaN).
"""

import math
from pathlib import Path
import numpy as np
import pandas as pd

PHASES_KHAFRE = [
    {"phase_id": "1",  "phase_label": "Phase 1",  "ramp_configuration": "16 straight edge-ramps", "ramps_up": 12, "ramps_down": 4, "turns_per_block": 0, "base_service_time_min": 3.5},
    {"phase_id": "2",  "phase_label": "Phase 2",  "ramp_configuration": "8 straight edge-ramps",  "ramps_up": 6,  "ramps_down": 2, "turns_per_block": 0, "base_service_time_min": 3.6},
    {"phase_id": "3a", "phase_label": "Phase 3a", "ramp_configuration": "4 helical edge-ramps",  "ramps_up": 3,  "ramps_down": 1, "turns_per_block": 1, "base_service_time_min": 3.8},
    {"phase_id": "3b", "phase_label": "Phase 3b", "ramp_configuration": "2 helical edge-ramps",  "ramps_up": 2,  "ramps_down": 0, "turns_per_block": 1, "base_service_time_min": 3.9},
    {"phase_id": "3c", "phase_label": "Phase 3c", "ramp_configuration": "1 helical edge-ramp",   "ramps_up": 1,  "ramps_down": 0, "turns_per_block": 2, "base_service_time_min": 4.0},
]


def s_mu_theta(mu: float, theta_deg: float, mu0: float = 0.20, theta0_deg: float = 7.5) -> float:
    """Hauling-force scale relative to a reference friction/slope pair."""
    th = math.radians(theta_deg)
    th0 = math.radians(theta0_deg)
    return (math.sin(th) + mu * math.cos(th)) / (math.sin(th0) + mu0 * math.cos(th0))


def run_trials(
    mu: float = 0.30,
    theta_deg: float = 7.5,
    N: int = 10000,
    seed: int = 42,
    theta0_deg: float = 7.5,
    phases=PHASES_KHAFRE,
    nominal_headway_min: float = 4.0,
    headway_abs_jitter_min: float = 1.0,
    minimum_headway_abs_jitter_min: float = 1.0,
    mtbf_hours: float = 6.0,
    repair_min_low: float = 5.0,
    repair_min_high: float = 12.0,
) -> pd.DataFrame:
    if isinstance(N, bool) or not isinstance(N, int) or N < 1:
        raise ValueError("N must be a positive integer")
    parameters = (mu, theta_deg, theta0_deg, nominal_headway_min,
                  headway_abs_jitter_min, minimum_headway_abs_jitter_min,
                  mtbf_hours, repair_min_low, repair_min_high)
    if not all(math.isfinite(x) for x in parameters):
        raise ValueError("Parameters must be finite")
    if mu < 0 or not 0 < theta_deg < 90 or not 0 < theta0_deg < 90:
        raise ValueError("Invalid friction or slope")
    if not 0 <= headway_abs_jitter_min < nominal_headway_min or not phases:
        raise ValueError("Invalid headway jitter or empty phases")
    if mtbf_hours <= 0 or not 0 <= repair_min_low <= repair_min_high:
        raise ValueError("Invalid MTBF or repair times")

    th = math.radians(theta_deg)
    force = 2267.96 * 9.81 * (math.sin(th) + mu * math.cos(th))
    pullers = math.ceil(force / 300.0)
    team_length = 1.5 * math.ceil(pullers / 2) - 1.0
    safety_distance = team_length + 3.0 + 15.0
    minimum_headway = safety_distance / 0.15 / 60.0
    if not 0 <= minimum_headway_abs_jitter_min < minimum_headway:
        raise ValueError("Invalid minimum-headway jitter")

    rng = np.random.default_rng(seed)
    variation = rng.uniform(-1.0, 1.0, size=N)
    requested_headway = nominal_headway_min + headway_abs_jitter_min * variation
    sampled_minimum = minimum_headway + minimum_headway_abs_jitter_min * variation
    repair_times = rng.uniform(repair_min_low, repair_min_high, size=N)
    availability = mtbf_hours * 60.0 / (mtbf_hours * 60.0 + repair_times)
    headway = np.maximum(requested_headway, sampled_minimum) / availability
    speed_factor = minimum_headway / sampled_minimum

    frames = []
    for ph in phases:

        mu_ln = math.log(2.8)
        sigma_ln = 0.35
        corner_delay = rng.lognormal(mean=mu_ln, sigma=sigma_ln, size=N)
        heavy = rng.random(N) < 0.10
        corner_delay[heavy] *= rng.uniform(1.1, 1.4, size=heavy.sum())

        service_time = sampled_minimum / availability

        arrival_rate = 1.0 / headway
        service_rate = 1.0 / service_time

        utilization = arrival_rate / service_rate
        Lq = np.full(N, np.nan)
        waiting_probability = np.full(N, np.nan)
        blocking_probability = np.full(N, np.nan)

        per_ramp_capacity_bph = 60.0 * np.minimum(arrival_rate, service_rate)
        total_capacity_bph = per_ramp_capacity_bph * ph["ramps_up"]

        df = pd.DataFrame({
            "trial_id": np.arange(1, N + 1, dtype=int),
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
            "waiting_probability": waiting_probability,
            "blocking_probability": blocking_probability,
            "mc_settings": f"Khafre/G2; mu={mu}; theta={theta_deg}; nominal_headway_min={nominal_headway_min}; jitter_min={headway_abs_jitter_min}; minimum_jitter_min={minimum_headway_abs_jitter_min}; common uniform perturbation; nominal_minimum_min={minimum_headway}; sampled minimum implies variable speed; mtbf_hours={mtbf_hours}; repair_min={repair_min_low}-{repair_min_high}; availability included; seed={seed}; corner delays diagnostic; queue/blocking not modelled",
        })
        frames.append(df)

    return pd.concat(frames, ignore_index=True)


def save_trials_csv(mu: float = 0.30, theta_deg: float = 7.5, N: int = 10000,
                    out_csv: str = "khafre_montecarlo_capacity_trials.csv",
                    seed: int = 42, theta0_deg: float = 7.5,
                    nominal_headway_min: float = 4.0,
                    headway_abs_jitter_min: float = 1.0,
                    minimum_headway_abs_jitter_min: float = 1.0,
                    mtbf_hours: float = 6.0, repair_min_low: float = 5.0,
                    repair_min_high: float = 12.0) -> str:
    df = run_trials(mu=mu, theta_deg=theta_deg, N=N, seed=seed, theta0_deg=theta0_deg,
                    nominal_headway_min=nominal_headway_min,
                    headway_abs_jitter_min=headway_abs_jitter_min,
                    minimum_headway_abs_jitter_min=minimum_headway_abs_jitter_min,
                    mtbf_hours=mtbf_hours, repair_min_low=repair_min_low,
                    repair_min_high=repair_min_high)
    Path(out_csv).parent.mkdir(parents=True, exist_ok=True)
    df.to_csv(out_csv, index=False)
    return out_csv


if __name__ == "__main__":
    print(save_trials_csv())
