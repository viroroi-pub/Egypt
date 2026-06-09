#!/usr/bin/env python3
"""
Generate the Khafre/G2 equivalent of montecarlo_edge_ramp_summary.csv.

Method:
- Read the Khufu/G1 Monte Carlo duration summary.
- Preserve the same relative Monte Carlo uncertainty envelope.
- Scale all time statistics by the deterministic adaptive-duration ratio:
    baseline:     Khafre 11.84 y / Khufu 13.67 y
    conservative: Khafre 17.76 y / Khufu 20.51 y

This reproduces the same output structure:
scenario,n,median_years,ci2.5_years,ci97.5_years,mean_years,std_years
"""

import csv
from pathlib import Path

INPUT_CSV = Path('/mnt/data/montecarlo_edge_ramp_summary.csv')
OUTPUT_CSV = Path('/mnt/data/montecarlo_edge_ramp_summary_khafre.csv')

# Deterministic adaptive durations used as scaling anchors.
# Khufu values are the G1 deterministic adaptive schedule at 4 and 6 min.
# Khafre values are the G2 deterministic adaptive schedule at 4 and 6 min.
SCALE = {
    'baseline': 11.84 / 13.67,
    'conservative': 17.76 / 20.51,
}

TIME_COLUMNS = ['median_years', 'ci2.5_years', 'ci97.5_years', 'mean_years', 'std_years']

with INPUT_CSV.open(newline='', encoding='utf-8-sig') as f:
    rows = list(csv.DictReader(f))
    fieldnames = rows[0].keys()

out_rows = []
for row in rows:
    scenario = row['scenario']
    if scenario not in SCALE:
        raise ValueError(f'No scale factor defined for scenario: {scenario}')
    scale = SCALE[scenario]
    out = dict(row)
    for col in TIME_COLUMNS:
        out[col] = repr(float(row[col]) * scale)
    out_rows.append(out)

with OUTPUT_CSV.open('w', newline='', encoding='utf-8') as f:
    writer = csv.DictWriter(f, fieldnames=fieldnames)
    writer.writeheader()
    writer.writerows(out_rows)

print(f'Saved: {OUTPUT_CSV}')
for r in out_rows:
    print(r)
