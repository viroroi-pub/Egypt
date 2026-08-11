import pandas as pd
import numpy as np
import matplotlib.pyplot as plt
from pathlib import Path

# ============================================================
# Khafre IER correspondence analysis at 7.5°: 2,000 Monte Carlo trials
# ============================================================
#
# Null model:
# Randomly permute the 205 observed course heights (Height_m),
# preserving:
#   - the exact number of courses,
#   - the empirical distribution of course heights,
#   - the total reconstructed height,
# while disrupting their observed vertical order.
#
# For each randomized realization, observable elevation intervals
# are recalculated at the same course boundaries and compared with
# the fixed IER turning elevations using a ±0.70 m tolerance.
#
# Empirical p-values use the standard add-one correction:
# p = (exceedances + 1) / (N + 1)
# ============================================================

# -----------------------------
# User settings
# -----------------------------
INPUT_FILE = Path(
    "Khafre_courses_height_model_v4_N205_normal0p7_thin0p5_star1p2_real.xlsx"
)
SHEET_NAME = "Model"
OUTPUT_DIR = Path("khafre_monte_carlo_2000_7p5_only")

N_SIMULATIONS = 2000
SEED = 42
TOLERANCE_M = 0.70

OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

# -----------------------------
# Load course-height model
# -----------------------------
df = pd.read_excel(INPUT_FILE, sheet_name=SHEET_NAME)

# Keep only valid course rows
df = df[pd.to_numeric(df["Course"], errors="coerce").notna()].copy()
df["Course"] = df["Course"].astype(int)

# The reconstructed model contains 205 courses
df = df.iloc[:205].copy()

heights = df["Height_m"].to_numpy(dtype=float)
cumulative = df["Cumulative_m"].to_numpy(dtype=float)
total_height = float(cumulative[-1])

# -----------------------------
# Predeclared observables
# Elevation intervals in metres
# -----------------------------
observables = {
    "O1": (25.0, 31.0),
    "O2": (73.0, 74.0),
    "O3": (77.0, 78.0),
    "O4": (77.0, 80.0),
    "O5": (78.0, 79.0),
    "O6": (99.0, 100.0),
    "O7": (107.0, 109.0),
    "O8": (107.0, 108.0),
    "O9": (104.0, 105.0),
    "O10": (105.0, 106.0),
    "O11": (120.0, 121.0),
}

# -----------------------------
# IER turn heights from Table S2.1
# Only the 7.5° reference configuration is evaluated.
# -----------------------------
turns = {
    7.50: [25.9, 46.9, 64.4, 78.4, 90.3, 100.1, 107.8, 114.1, 119.7, 123.9, 127.4],
}

rng = np.random.default_rng(SEED)

# -----------------------------
# Helper functions
# -----------------------------
def boundary_course_index(z: float) -> int:
    """
    Return the index of the first reconstructed course boundary
    whose cumulative elevation is equal to or above z.
    """
    return int(np.searchsorted(cumulative, z, side="left"))


def count_matches(turn_list, observable_intervals):
    """
    Count observable IDs intersected by at least one predicted turn
    after expanding each observable interval by ±TOLERANCE_M.

    Each observable is counted only once per inclination.
    """
    matched = set()

    for observable_id, (lower, upper) in observable_intervals.items():
        for turn_height in turn_list:
            if (lower - TOLERANCE_M) <= turn_height <= (upper + TOLERANCE_M):
                matched.add(observable_id)
                break

    return len(matched), matched


# Map each observable elevation interval to its original course boundaries.
observable_course_bounds = {
    observable_id: (
        boundary_course_index(lower),
        boundary_course_index(upper),
    )
    for observable_id, (lower, upper) in observables.items()
}

# -----------------------------
# Observed correspondence counts
# -----------------------------
observed_counts = {}
observed_sets = {}

for angle, turn_list in turns.items():
    count, matched = count_matches(turn_list, observables)
    observed_counts[angle] = count
    observed_sets[angle] = sorted(matched)

# -----------------------------
# Monte Carlo simulations
# -----------------------------
simulated_counts = {
    angle: np.zeros(N_SIMULATIONS, dtype=int)
    for angle in turns
}

for simulation_index in range(N_SIMULATIONS):
    # Randomly reorder the empirical course heights
    randomized_heights = rng.permutation(heights)

    # Recalculate cumulative elevations
    randomized_cumulative = np.cumsum(randomized_heights)

    # Recalculate each observable interval at the same course boundaries
    randomized_observables = {}

    for observable_id, (lower_index, upper_index) in observable_course_bounds.items():
        lower = float(randomized_cumulative[lower_index])
        upper = float(randomized_cumulative[upper_index])

        if lower > upper:
            lower, upper = upper, lower

        randomized_observables[observable_id] = (lower, upper)

    for angle, turn_list in turns.items():
        count, _ = count_matches(turn_list, randomized_observables)
        simulated_counts[angle][simulation_index] = count

# -----------------------------
# Summary statistics and p-values
# -----------------------------
summary_rows = []

for angle in sorted(turns):
    simulated = simulated_counts[angle]
    observed = observed_counts[angle]

    exceedances = int(np.sum(simulated >= observed))
    empirical_p = (exceedances + 1) / (N_SIMULATIONS + 1)

    summary_rows.append({
        "Ramp_angle_deg": angle,
        "Observed_matches": observed,
        "Observed_observables": "/".join(observed_sets[angle]),
        "Null_mean": simulated.mean(),
        "Null_median": np.median(simulated),
        "Null_P2.5": np.percentile(simulated, 2.5),
        "Null_P97.5": np.percentile(simulated, 97.5),
        "Null_max": simulated.max(),
        "Simulations_ge_observed": exceedances,
        "Empirical_p_ge_observed": empirical_p,
    })

summary = pd.DataFrame(summary_rows)

# Reference configuration: 7.5°
reference_angle = 7.50
reference_observed = observed_counts[reference_angle]
reference_exceedances = int(
    np.sum(simulated_counts[reference_angle] >= reference_observed)
)
reference_p = (
    reference_exceedances + 1
) / (N_SIMULATIONS + 1)

global_summary = pd.DataFrame([{
    "N_simulations": N_SIMULATIONS,
    "Seed": SEED,
    "Tolerance_m": TOLERANCE_M,
    "Courses": len(heights),
    "Total_height_m": total_height,
    "Reference_angle_deg": reference_angle,
    "Reference_observed_matches": reference_observed,
    "Reference_simulations_ge_observed": reference_exceedances,
    "Reference_empirical_p": reference_p,
    "Null_model": (
        "Random permutation of observed Height_m values; "
        "course count, empirical height distribution, and total height preserved."
    ),
}])

# -----------------------------
# Distribution tables
# -----------------------------
distribution_rows = []

for angle in sorted(turns):
    values, frequencies = np.unique(
        simulated_counts[angle],
        return_counts=True,
    )

    for value, frequency in zip(values, frequencies):
        distribution_rows.append({
            "Ramp_angle_deg": angle,
            "Match_count": int(value),
            "Frequency": int(frequency),
            "Percentage": 100.0 * frequency / N_SIMULATIONS,
        })

distribution = pd.DataFrame(distribution_rows)

# Course-boundary mapping used for the observables
mapping_rows = []

for observable_id, (lower, upper) in observables.items():
    lower_index, upper_index = observable_course_bounds[observable_id]

    mapping_rows.append({
        "Observable": observable_id,
        "Observed_lower_m": lower,
        "Observed_upper_m": upper,
        "Lower_boundary_course": int(df.iloc[lower_index]["Course"]),
        "Upper_boundary_course": int(df.iloc[upper_index]["Course"]),
        "Model_cumulative_at_lower_course_m": float(cumulative[lower_index]),
        "Model_cumulative_at_upper_course_m": float(cumulative[upper_index]),
    })

mapping = pd.DataFrame(mapping_rows)

# Raw simulation counts
raw_counts = pd.DataFrame({
    f"{angle:.2f}deg": simulated_counts[angle]
    for angle in sorted(turns)
})

# -----------------------------
# Save tabular outputs
# -----------------------------
summary.to_csv(
    OUTPUT_DIR / "MonteCarlo_summary_by_angle.csv",
    index=False,
)
global_summary.to_csv(
    OUTPUT_DIR / "MonteCarlo_global_summary.csv",
    index=False,
)
distribution.to_csv(
    OUTPUT_DIR / "MonteCarlo_match_distributions.csv",
    index=False,
)
mapping.to_csv(
    OUTPUT_DIR / "Observable_course_mapping.csv",
    index=False,
)
raw_counts.to_csv(
    OUTPUT_DIR / "MonteCarlo_raw_counts_2000.csv",
    index=False,
)

# -----------------------------
# Plot: null distribution at 7.5°
# -----------------------------
plt.figure(figsize=(8, 5))

reference_simulated = simulated_counts[reference_angle]
bins = np.arange(
    reference_simulated.min() - 0.5,
    reference_simulated.max() + 1.5,
    1,
)

plt.hist(reference_simulated, bins=bins, edgecolor="black")
plt.axvline(
    reference_observed,
    linestyle="--",
    linewidth=2,
    label=f"Observed = {reference_observed}",
)

plt.xlabel("Number of matched observables")
plt.ylabel("Monte Carlo simulations")
plt.title("Null distribution of IER–observable correspondences at 7.5°")
plt.legend()
plt.tight_layout()

plt.savefig(
    OUTPUT_DIR / "MonteCarlo_7p5deg_null_distribution.png",
    dpi=300,
)
plt.close()

# -----------------------------
# Console output
# -----------------------------
print(global_summary.to_string(index=False))
print("\nResult for 7.5°:")
print(summary.to_string(index=False))
print(f"\nOutputs saved in: {OUTPUT_DIR.resolve()}")
