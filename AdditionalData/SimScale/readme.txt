Piramide4RampsTotal_03_smooth.zip
→ Compressed geometry and mesh model used in SimScale. Contains the full pyramid with integrated edge-ramp geometry (3 × 6 mask) prepared for FEA runs. Input for both coarse and fine simulations referenced in Table 4 (“Two-level mesh convergence”).

SimScale.txt
→ Raw output summary from the coarse-mesh (baseline) FEA run: node count, reaction forces, mean/p95 σvM, and apex-cap displacement. Values feed Table 4 (fineness 3 columns).

SimScale_tot_fine.txt
→ Raw output summary from the fine-mesh FEA run. Used together with SimScale.txt to compute Δ% differences and confirm convergence (fineness 5 columns in Table 4).

simscale-results.zip
→ Archive of coarse-mesh results (reaction files, stress/displacement .vtk exports, and Code_Aster logs). Source data for Table 4 and Fig. 9 (low-resolution run).

simscale-results_fine.zip
→ Archive of fine-mesh results (full .vtk and .rmed outputs). Provides the high-resolution stress and settlement fields used for the comparative FEA table (Table 4) and for Fig. 9 (stress/settlement map).

SimScaleScreenShot.png
→ Screenshot from SimScale of the baseline (coarse) stress-field visualization; supports Fig. 9 illustration.

SimScaleScreenShotFine.png
→ Screenshot from SimScale of the refined (fine) stress-field visualization; supports Fig. 9 and shows mesh refinement and stress distribution consistency.