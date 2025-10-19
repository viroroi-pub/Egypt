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