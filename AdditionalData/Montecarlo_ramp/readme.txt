params.txt — Configuration file listing key parameters used in the Monte Carlo edge-ramp simulations (μ ranges, θ values, number of trials, seed). Supports Supplementary Section S9 (Methods / Monte Carlo setup).

pyramid_thickness_simulation.py / .ipynb — Python / Jupyter scripts reproducing the course-thickness alignment tests described in Supplement S11. Generate the simulated course-by-course series compared with McKenzie’s [30] empirical data and output the alignment figures (used in Fig. 10, Table S11.1).

requirements.txt — List of Python libraries and versions required to execute all notebooks (NumPy, Pandas, Matplotlib, SciPy etc.). Enables full reproducibility of the pipeline (as declared in Methods → Reproducibility and Zenodo README).

edge_ramp_montecarlo_generator.ipynb — Master Jupyter notebook for generating Monte Carlo runs over friction (μ) and ramp angle (θr) ranges. Implements the discrete-event loop producing synthetic timelines and queue statistics. Underlies the bootstrap and confidence-interval results in Table 5 and Figure 8 (Main Article).

edge_ramp_montecarlo.py / .ipynb — Core simulation modules used by the generator. They compute phase-by-phase throughput and on-site duration for each random trial. Corresponds to Supplementary S9 (“Monte Carlo model and stochastic headway testing”).

edge_ramp_montecarlo_byphase_Sadjusted_ALL.zip — Compressed archive of all by-phase output CSVs (16→8→4→2→1 ramp phases) used to build the percentile plots and confidence bands in Figure 8 and Supplement Fig. S9.1.

montecarlo_edge_ramp_trials_mu015_mu030_theta6_7_8.zip — Dataset of full Monte Carlo trials sweeping μ = 0.15–0.30 and θr = 6°–8°. Source data for the variability bands and duration ranges (Results → Fig. 8 and Table S9.2).

montecarlo_boxplot_custom_labels.png — Box-and-whisker summary image showing the median, IQR, and P2.5–P97.5 for all ramp configurations; corresponds to Figure 8 (Main Article) and Supplement Fig. S9.2.

edge_ramp_montecarlo_median_95ci.csv — Summary table of Monte Carlo medians and 95 % confidence intervals for each configuration (1-, 4-, adaptive ramps). Values reported in Results → Figure 8 and Supplement Table S9.2.

median_capacity_per_phase.png — Graph of median capacity per ramp phase (16, 8, 4, 2, 1 channels). Supports Supplementary Section S9 (capacity analysis) and accompanies the discussion of Fig. S9.3.

edge_ramp_montecarlo_summary_by_phase.csv — CSV summary giving per-phase mean, median, P10–P90 capacities and durations; used to generate median_capacity_per_phase.png and cited in S9 (Method details).

edge_ramp_queue_summary.csv — CSV of queue-length and waiting-time statistics from the discrete-event model. Supports the Results paragraph on queuing metrics (λ, μ, ρ) and Supplement S9 (“Queue behaviour”).

montecarlo_edge_ramp_summary.csv — Consolidated on-site duration results for all simulations (single, four, adaptive ramps) used to create Table 5 and Figure 7 (Main Article).

montecarlo_edge_ramp_trials.csv — Raw trial-by-trial outputs (headway, phase duration, total years, random seed). Fundamental dataset archived on Zenodo for full reproducibility (referenced in Methods → Reproducibility and Supplement S9).

edge_ramp_montecarlo_10000x5.csv — Expanded run (10 000 × 5 replicates) used for the bootstrap distributions; provides input to compute 95 % CIs shown in Fig. 8 and Supplement Fig. S9.1.

--------------------------------------------------
Field descriptions for Monte Carlo by-phase CSV files
(e.g., edge_ramp_montecarlo_mu0p15_theta6deg_10000_by_phase.csv)
--------------------------------------------------

Each CSV contains Monte Carlo outputs generated phase by phase. 
The total number of rows corresponds to N trials per phase multiplied by the number of construction phases (5 phases: 1, 2, 3a, 3b, 3c), i.e., 10,000 × 5 = 50,000 rows.

Identification and scenario parameters
- trial_id: Sequential identifier of the Monte Carlo trial (1–10,000). The same trial_id is reused
  across all phases.
- mu:  Kinetic friction coefficient assumed for the hauling process.
- theta_deg: Ramp slope angle in degrees.

Phase and configuration metadata
- phase_id: Construction phase identifier (1, 2, 3a, 3b, 3c).
- phase_label: Human-readable label of the phase.
- ramp_configuration: Textual description of the active ramp layout in the phase.
- ramps_up: Number of ascending ramps available for block transport.
- ramps_down: Number of descending or return ramps (if applicable).
- turns_per_block: Number of corner turns required per block in the given phase.

Stochastic input variables (Monte Carlo)
- speed_factor: Random multiplicative hauling-speed factor (uniform ±25% around baseline).
- headway_arrival_min: Arrival headway in minutes between successive blocks per ramp
  (baseline 4 min ±1 min, bounded).
- corner_delay_per_turn_min: Stochastic delay per corner turn (minutes), sampled from a lognormal distribution;
  set to zero for phases without turns.

Service and flow metrics (per ramp)
- service_time_min: Total service time per block (minutes), including hauling time scaled by S(mu,theta)
  and corner delays.
- arrival_rate_blocks_per_min:  Arrival rate λ = 1 / headway_arrival_min.
- service_rate_blocks_per_min:  Service rate μ_s = 1 / service_time_min.
- utilization:  Traffic intensity ρ = λ / μ_s (capped to avoid numerical divergence).

Capacity metrics
- per_ramp_capacity_blocks_per_hour:  Effective throughput per ramp (blocks per hour), accounting for saturation
  and blocking effects.
- total_capacity_blocks_per_hour:  Total phase capacity, computed as per-ramp capacity multiplied by ramps_up.

Queueing and congestion indicators
- mean_corner_queue_length:  Mean queue length (Lq) estimated from the queueing approximation.
- waiting_probability:  Probability that an arriving block must wait (approximated by ρ).
- blocking_probability:  Phenomenological probability of blocking near saturation (up to 30%).

Metadata
- settings:  Text description of the Monte Carlo assumptions (speed variation, headway jitter,
  corner-delay model).
- S_mu_theta:
  Dimensionless scaling factor applied to hauling time:
  S(mu,theta) = (sin(theta) + mu cos(theta)) /
                (sin(7°) + 0.20 cos(7°)).
--------------------------------------------------

--------------------------------------------------
Field descriptions for global Monte Carlo trial CSV
(e.g., montecarlo_edge_ramp_trials_mu0p15_theta7_10000.csv)
--------------------------------------------------

This CSV contains Monte Carlo simulation outputs for a single construction scenario (mu = 0.15, theta = 7°). 
Each row corresponds to one complete Monte Carlo trial, yielding a total of 10,000 trials (10,000 rows).

Unlike the by-phase CSV files, this dataset reports global on-site construction
duration per trial and does not include phase-level breakdowns.

Identification and scenario parameters
- trial:  Sequential identifier of the Monte Carlo trial (1–10,000).
- scenario:  Scenario label encoding friction coefficient and ramp slope
  (here: mu0p15_theta7).
- friction_mu:  Kinetic friction coefficient assumed for the hauling process.
- theta_r_deg:  Ramp slope angle in degrees.

Monte Carlo control parameters
- headway_mean_min:  Mean arrival headway between successive blocks (minutes).
- headway_multiplier:  Random multiplicative factor applied to the nominal headway.
- headway_fluctuation_pct:  Percentage variability applied to headway values.
- stop_mtbf_h:  Mean time between random stop events (hours).
- stop_repair_min_low:  Lower bound of repair duration following a stop event (minutes).
- stop_repair_min_high:  Upper bound of repair duration following a stop event (minutes).
- stop_factor:  Multiplicative slowdown factor associated with stop events.

Primary Monte Carlo output
- duration_years:  Total simulated on-site construction duration (working years)
  for the given trial.

This file is used to compute median construction time and uncertainty ranges
(P10–P90 and P2.5–P97.5) reported in the main text and figures.
--------------------------------------------------

