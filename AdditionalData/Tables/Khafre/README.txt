compute_edge_ramp_queue_summary_khafre.py
Section: Results — Monte Carlo traffic and schedule robustness / deterministic queue summary
Description: Python script used to compute queue/capacity summaries for the Khafre IER ramp configurations. It produces or supports edge_ramp_queue_summary_khafre.csv.

compute_khafre_montecarlo_edge_ramp_trials_mu015_mu030_theta6_7_8.py
Section: Methods — Monte Carlo traffic and duration uncertainty; Results — Monte Carlo traffic and schedule robustness
Description: Python script used to generate Monte Carlo edge-ramp trials for Khafre across the tested friction range μ = 0.15–0.30 and ramp inclinations θr = 6°, 7° and 8°. It supports the trial archive and summary table for the μ–θ sensitivity grid.

compute_montecarlo_edge_ramp_summary_khafre.py
Section: Results — Monte Carlo traffic and schedule robustness
Description: Python script used to aggregate Khafre Monte Carlo trial outputs into summary statistics, including medians and percentile intervals.

create_khafre_montecarlo_duration_boxplot.py
Figure: Fig. 10 (Main Article)
Description: Python plotting script used to generate the Monte Carlo on-site duration boxplot for Khafre from the corresponding duration sample table.

edge_ramp_montecarlo_khafre.py
Section: Methods — Monte Carlo traffic and duration uncertainty
Description: Core Python script for the Khafre Monte Carlo traffic/duration model. It implements the stochastic framework adapted from the published Khufu IER model to the Khafre-specific geometry and schedule assumptions.


edge_ramp_queue_summary_khafre.csv
Section/Table: Results — deterministic or semi-deterministic queue/capacity summary
Description: Queue and capacity summary for the Khafre IER ramp configurations. This file documents the deterministic traffic/capacity baseline used before Monte Carlo aggregation.

Khafre_courses_height_model_v4_N205_normal0p7_thin0p5_star1p2_nonstarAdjusted.xlsx
Figure/Table: Fig. 7 (Main Article); Table 1 (Main Article); Supplementary Section S3/S6
Description: Khafre course-height model with N = 205 courses. This version uses adjusted non-star course-height values for the qualitative course-height reconstruction used in the comparison with predicted IER turn elevations.

Khafre_courses_height_model_v4_N205_normal0p7_thin0p5_star1p2_real.xlsx
Figure/Table: Fig. 7 (Main Article); Supplementary Section S6
Description: Khafre course-height model with N = 205 courses preserving the reconstructed/real course-height values used as the basis for the visual course-height profile and related checks.

khafre_edge_ramp_montecarlo_median_95ci.csv
Figure/Table: Fig. 10 (Main Article); Results — Monte Carlo traffic and schedule robustness
Description: Monte Carlo summary table reporting median values and 95% percentile intervals for Khafre IER on-site duration scenarios.

khafre_montecarlo_boxplot_custom_labels.png
Figure: Fig. 10 (Main Article)
Description: Final Monte Carlo on-site duration boxplot for Khafre. The figure reports median, interquartile range and 95% percentile intervals for the adaptive IER scenarios compared with the approximate 25-year reference horizon.

khafre_montecarlo_capacity_summary.csv
Section/Table: Results — Monte Carlo traffic and schedule robustness
Description: Summary of realized Monte Carlo capacities by phase or scenario for the Khafre adaptive IER sequence.

khafre_montecarlo_capacity_trials.csv
Section: Results — Monte Carlo traffic and schedule robustness
Description: Trial-level Monte Carlo capacity outputs for Khafre. This file records stochastic variation across trials before aggregation into summary statistics.

khafre_montecarlo_edge_ramp_trials_summary_mu015_mu030_theta6_7_8.csv
Section/Table: Results — sensitivity to friction and ramp inclination; Monte Carlo traffic and schedule robustness
Description: Summary of Monte Carlo edge-ramp trials for Khafre across μ = 0.15–0.30 and θr = 6°, 7° and 8°. Used to evaluate robustness of on-site duration and phase capacity under friction/slope variation.

khafre_on_site_duration_montecarlo_samples_for_boxplot.csv
Figure: Fig. 10 (Main Article)
Description: Monte Carlo on-site duration samples used to generate the Khafre boxplot figure. This is the direct input table for create_khafre_montecarlo_duration_boxplot.py.

montecarlo_edge_ramp_summary_khafre.csv
Section/Table: Results — Monte Carlo traffic and schedule robustness
Description: Aggregated Monte Carlo summary for the Khafre edge-ramp model. This file consolidates duration/capacity indicators used in the manuscript discussion of stochastic robustness.

montecarlo_edge_ramp_trials_mu015_mu030_theta6_7_8_khafre.zip
Section: Methods — Monte Carlo traffic and duration uncertainty
Description: Compressed archive of Khafre Monte Carlo edge-ramp trial outputs across μ = 0.15–0.30 and θr = 6°, 7° and 8°. This archive preserves the trial-level results underlying the summarized CSV outputs.

Predicted turns per ramp inclination.xlsx
Table: Supplementary Table S2.1
Description: Spreadsheet containing the predicted cumulative heights of IER turns for the Pyramid of Khafre as a function of ramp inclination. This is the main turn-height input used for the comparison with architectural observables.

Predicted turns per ramp inclination heighs.xlsx
Table: Supplementary Table S2.1
Description: Working spreadsheet containing predicted IER turn elevations/heights by ramp inclination. 

pyramid_kefren_new.zip
Table: Table 3
Description: new results with the algorith of the safety distance changed. Across 6-7º ramp slope, 0.30 friction, and all ramp configuration.