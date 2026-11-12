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
Description: Python script for generating phase-level Monte Carlo capacity trials for the Khafre IER configurations. On-site durations require a subsequent calculation using the Khafre block counts by phase and the working calendar.


khafre_edge_ramp_montecarlo_median_95ci.csv
Figure/Table: Fig. 10 (Main Article); Results — Monte Carlo traffic and schedule robustness
Description: Monte Carlo summary table reporting median on-site durations and the 2.5th and 97.5th simulation percentiles for Khafre IER scenarios. These percentile ranges describe simulated duration variability; they are not confidence intervals for the median.

khafre_montecarlo_boxplot_custom_labels.png
Figure: Fig. 10 (Main Article)
Description: Monte Carlo on-site duration boxplot associated with the manuscript version of Fig. 10. The figure reports median, interquartile range and 95% percentile intervals for the adaptive IER scenarios compared with the approximate 25-year reference horizon.

khafre_montecarlo_capacity_summary.csv
Section/Table: Results — Monte Carlo traffic and schedule robustness
Description: Summary of realized Monte Carlo capacities by phase or scenario for the Khafre adaptive IER sequence.

khafre_montecarlo_capacity_trials.csv
Section: Results — Monte Carlo traffic and schedule robustness
Description: Trial-level Monte Carlo capacity outputs by phase for Khafre, before aggregation into summary statistics. Capacity is expressed in blocks per hour. Queue and blocking columns, when present, must be interpreted according to the generating script; NaN denotes an unmodelled quantity, not a zero value.

khafre_montecarlo_edge_ramp_trials_summary_mu015_mu030_theta6_7_8.csv
Section/Table: Results — sensitivity to friction and ramp inclination; Monte Carlo traffic and schedule robustness
Description: Summary of Monte Carlo edge-ramp trials for Khafre across μ = 0.15–0.30 and θr = 6°, 7° and 8°. Used to evaluate robustness of on-site duration and phase capacity under friction/slope variation.

khafre_on_site_duration_montecarlo_samples_for_boxplot.csv
Figure: Fig. 10 (Main Article)
Description: Monte Carlo on-site duration samples used to generate the Khafre boxplot figure. This is the direct input table for create_khafre_montecarlo_duration_boxplot.py.

montecarlo_edge_ramp_summary_khafre.csv
Section/Table: Results — Monte Carlo traffic and schedule robustness
Description: Aggregated on-site duration statistics for the Khafre edge-ramp model: sample count, median, mean, standard deviation and the 2.5th and 97.5th simulation percentiles. Durations and standard deviations are expressed in years; variance, when included, is expressed in years squared.

montecarlo_edge_ramp_trials_mu015_mu030_theta6_7_8_khafre.zip
Section: Methods — Monte Carlo traffic and duration uncertainty
Description: Compressed archive of Khafre Monte Carlo edge-ramp trial outputs across μ = 0.15–0.30 and θr = 6°, 7° and 8°. This archive preserves the trial-level results underlying the summarized CSV outputs.

results_headway_khafre.zip
Section: Methods — Monte Carlo duration uncertainty; Results — Monte Carlo traffic and schedule robustness
Description: Compressed archive of the Khafre headway calculations for the 4-min baseline, 6-min conservative and 8-min slow scenarios, with 10,000 trials per scenario and random seed 42. 
