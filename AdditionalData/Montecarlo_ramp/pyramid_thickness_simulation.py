import pandas as pd
import numpy as np
import os

# --- Analysis Parameters ---
NUM_SIMULATIONS = 2000
NUM_COURSES = 203
RANDOM_SEED = 42  # Seed for reproducibility
# Parameters calibrated from real archaeological data
MEAN_THICKNESS = 0.722      # Mean in meters
STD_DEV_THICKNESS = 0.235   # Standard deviation in meters
MIN_THICKNESS = 0.495       # Minimum observed thickness
MAX_THICKNESS = 1.500       # Maximum observed thickness
OUTPUT_FILENAME = 'pyramid_course_simulations_corrected.csv'

# Set the seed for NumPy's random number generator
np.random.seed(RANDOM_SEED)

# --- Main Generation Function ---
def generate_realistic_simulations():
    """
    Generates and saves 2000 realistic simulations of the pyramid courses.
    """
    all_simulations_data = []
    
    print(f"Starting the generation of {NUM_SIMULATIONS} simulations...")

    for i in range(NUM_SIMULATIONS):
        sim_id = i + 1
        
        # Generate values from a normal distribution
        # The loop ensures that values stay within the observed limits
        simulated_courses = []
        while len(simulated_courses) < NUM_COURSES:
            # Generate a batch of numbers for efficiency
            batch_size = NUM_COURSES - len(simulated_courses)
            random_values = np.random.normal(loc=MEAN_THICKNESS, scale=STD_DEV_THICKNESS, size=batch_size)
            
            # Filter values to be within the [MIN, MAX] range
            valid_values = random_values[(random_values >= MIN_THICKNESS) & (random_values <= MAX_THICKNESS)]
            simulated_courses.extend(valid_values)

        # Ensure we have exactly the required number of courses
        simulated_courses = simulated_courses[:NUM_COURSES]

        # Calculate the cumulative elevation
        cumulative_elevation = np.cumsum(simulated_courses)

        # Prepare the data for this simulation
        for j in range(NUM_COURSES):
            all_simulations_data.append({
                'simulation_id': sim_id,
                'course_no': j + 1,
                'simulated_thickness_m': round(simulated_courses[j], 4),
                'simulated_cumulative_elevation_m': round(cumulative_elevation[j], 4)
            })
            
        if sim_id % 100 == 0:
            print(f"  ... {sim_id}/{NUM_SIMULATIONS} simulations completed.")

    # Create a pandas DataFrame and save it to CSV
    df = pd.DataFrame(all_simulations_data)
    df.to_csv(OUTPUT_FILENAME, index=False)
    
    print(f"\nSuccess! The file '{OUTPUT_FILENAME}' has been generated with {len(df)} rows.")
    print(f"It contains {NUM_SIMULATIONS} simulations of {NUM_COURSES} courses each.")

# --- Execute the script ---
if __name__ == "__main__":
    # Check if the file already exists to avoid regenerating it
    if not os.path.exists(OUTPUT_FILENAME):
        generate_realistic_simulations()
    else:
        print(f"File '{OUTPUT_FILENAME}' already exists. Skipping generation.")

