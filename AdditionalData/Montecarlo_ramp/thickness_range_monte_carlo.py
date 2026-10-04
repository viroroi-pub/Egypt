# Monte Carlo analysis of course-thickness transitions and turning elevations
#
# Parameters can be changed from the command line. Example:
#
# python thickness_range_monte_carlo.py \
#   --turnings 27.25 49.51 67.68 82.51 94.62 104.51 112.58 119.17 124.55 128.94 \
#   --simulations 2000 --courses 203 --seed 42
#
# A significant upward thickness-range transition is identified when:
# 1) mean(first two courses after) >= mean_ratio * mean(two courses before)
# 2) min(first two courses after) >= range_ratio * max(two courses before)
# 3) first course after >= step_ratio * immediately preceding course
# 4) first > second > third course after the boundary
#
# A turning is counted as a match when its elevation lies within the
# match_window interval immediately below a detected transition elevation.

from pathlib import Path
import argparse
import numpy as np
import pandas as pd

DEFAULT_TURNINGS = [
    27.25, 49.51, 67.68, 82.51, 94.62,
    104.51, 112.58, 119.17, 124.55, 128.94
]

def parse_args():
    p = argparse.ArgumentParser(
        description="Monte Carlo analysis of course-thickness transitions and turning elevations."
    )
    p.add_argument("--turnings", nargs="+", type=float, default=DEFAULT_TURNINGS,
                   help="Turning elevations in metres.")
    p.add_argument("--simulations", type=int, default=2000)
    p.add_argument("--courses", type=int, default=203)
    p.add_argument("--seed", type=int, default=42)
    p.add_argument("--mean", type=float, default=0.722, dest="mean_m")
    p.add_argument("--sd", type=float, default=0.235, dest="sd_m")
    p.add_argument("--lower", type=float, default=0.495, dest="low_m")
    p.add_argument("--upper", type=float, default=1.50, dest="high_m")
    p.add_argument("--mean-ratio", type=float, default=1.12)
    p.add_argument("--range-ratio", type=float, default=1.06)
    p.add_argument("--step-ratio", type=float, default=1.18)
    p.add_argument("--match-window", type=float, default=1.50)
    p.add_argument("--threshold-matches", type=int, default=5,
                   help="Score threshold used for the upper-tail frequency.")
    p.add_argument("--output-prefix", default="random_heights")
    return p.parse_args()

def draw_truncated_normal(rng, n, mean_m, sd_m, low_m, high_m):
    values = np.empty(n, dtype=float)
    filled = 0
    while filled < n:
        batch = rng.normal(mean_m, sd_m, size=max(64, 2 * (n - filled)))
        batch = batch[(batch >= low_m) & (batch <= high_m)]
        take = min(len(batch), n - filled)
        if take:
            values[filled:filled + take] = batch[:take]
            filled += take
    return values

def detect_transitions(thickness, mean_ratio, range_ratio, step_ratio):
    h = np.asarray(thickness, dtype=float)
    cumulative = np.cumsum(h)
    elevations = []
    for i in range(2, len(h) - 2):
        before = h[i - 2:i]
        after2 = h[i:i + 2]
        if (after2.mean() >= mean_ratio * before.mean()
            and after2.min() >= range_ratio * before.max()
            and h[i] >= step_ratio * h[i - 1]
            and h[i] > h[i + 1] > h[i + 2]):
            elevations.append(cumulative[i - 1])
    return np.asarray(elevations, dtype=float)

def score_turnings(transitions, turnings, match_window):
    transitions = np.asarray(transitions, dtype=float)
    turnings = np.asarray(turnings, dtype=float)
    if transitions.size == 0:
        return 0
    return int(sum(
        np.any((turn >= transitions - match_window) & (turn <= transitions))
        for turn in turnings
    ))

def run_analysis(turnings=DEFAULT_TURNINGS, simulations=2000, courses=203, seed=42,
                 mean_m=0.722, sd_m=0.235, low_m=0.495, high_m=1.50,
                 mean_ratio=1.12, range_ratio=1.06, step_ratio=1.18,
                 match_window=1.50, threshold_matches=5):
    rng = np.random.default_rng(seed)
    scores = np.empty(simulations, dtype=int)
    n_transitions = np.empty(simulations, dtype=int)

    for s in range(simulations):
        h = draw_truncated_normal(rng, courses, mean_m, sd_m, low_m, high_m)
        transitions = detect_transitions(h, mean_ratio, range_ratio, step_ratio)
        scores[s] = score_turnings(transitions, turnings, match_window)
        n_transitions[s] = len(transitions)

    values, counts = np.unique(scores, return_counts=True)
    distribution = pd.DataFrame({
        "matches": values,
        "simulations": counts,
        "frequency": counts / simulations
    })

    n_tail = int(np.sum(scores >= threshold_matches))
    summary = pd.DataFrame({
        "metric": [
            "seed", "simulations", "courses_per_simulation", "number_of_turnings",
            "normal_mean_m", "normal_sd_m", "lower_truncation_m",
            "upper_truncation_m", "mean_ratio_threshold", "range_ratio_threshold",
            "step_ratio_threshold", "match_window_m", "score_threshold",
            "mean_detected_transitions", "max_matches",
            "simulations_at_or_above_threshold", "frequency_at_or_above_threshold",
            "monte_carlo_p_plus"
        ],
        "value": [
            seed, simulations, courses, len(turnings), mean_m, sd_m, low_m,
            high_m, mean_ratio, range_ratio, step_ratio, match_window,
            threshold_matches, n_transitions.mean(), scores.max(), n_tail,
            n_tail / simulations, (n_tail + 1) / (simulations + 1)
        ]
    })

    raw = pd.DataFrame({
        "simulation": np.arange(1, simulations + 1),
        "matches": scores,
        "detected_transitions": n_transitions
    })
    return distribution, summary, raw

def main():
    a = parse_args()
    distribution, summary, raw = run_analysis(
        turnings=a.turnings, simulations=a.simulations, courses=a.courses, seed=a.seed,
        mean_m=a.mean_m, sd_m=a.sd_m, low_m=a.low_m, high_m=a.high_m,
        mean_ratio=a.mean_ratio, range_ratio=a.range_ratio, step_ratio=a.step_ratio,
        match_window=a.match_window, threshold_matches=a.threshold_matches
    )
    out = Path.cwd()
    distribution.to_csv(out / f"{a.output_prefix}_distribution.csv", index=False)
    summary.to_csv(out / f"{a.output_prefix}_summary.csv", index=False)
    raw.to_csv(out / f"{a.output_prefix}_all_simulations.csv", index=False)

    print("Turning elevations (m):", a.turnings)
    print()
    print(distribution.to_string(index=False))
    print()
    print(summary.to_string(index=False))

if __name__ == "__main__":
    main()
