README — Khafre course-height line detector

Purpose
This small HTML tool is used to estimate course-height intervals on images of the Pyramid of Khafre. It detects horizontal blue guide lines drawn over a pyramid image, orders them from bottom to top, converts pixel gaps into real-height intervals using the original Khafre height entered by the user, and exports the resulting values as a C# List<float> for use in the IER course-height model.

How it works
1. Open compute_heights.html in a web browser.
2. Upload one of the prepared Khafre line-overlay images.
3. Set the merge threshold in pixels if needed.
4. Keep the total real height as 143.5 m unless a different calibration is required.
5. Click Analyze Image.
6. The tool returns:
   - detected line intervals from bottom to top;
   - gap values in pixels;
   - converted real-height gaps;
   - a C# List<float> containing the estimated course-height sequence.

Files

1. compute_heights.html
Main browser-based line-detection tool. It scans the uploaded image for blue horizontal guide lines, merges nearby detections, sorts the detected lines from bottom to top, converts pixel intervals into real-height values, and produces a C# list output.

2. Khafre-Courses_lines.jpg
Reference image of the Pyramid of Khafre with manually or semi-manually drawn course/line markers. Used as the visual input for extracting approximate course-height intervals.

3. Khafre-lines-1.png
Line-overlay input image used for one detection pass. It contains blue guide lines over part of the pyramid profile.

4. Khafre-lines-2.png
Alternative or additional line-overlay input image used to refine or cross-check detected course-height intervals.

5. Khafre-lines-3.png
Processed line-overlay image used for a further detection pass or calibration check.

6. Khafre-lines-4.png
Additional processed line-overlay image used to inspect line detection, especially where the pyramid outline or course visibility is weaker.

7. Khafre-lines-5.png
Final or consolidated line-overlay image used to review the complete set of detected course-height markers.

Notes
- The tool is intended for semi-automatic visual extraction, not for metrically controlled TLS/SfM survey.
- Results should be interpreted as approximate modelling inputs for the Khafre course-height reconstruction.
- Detection quality depends on the clarity and continuity of the blue guide lines.
- The merge threshold can be adjusted to avoid counting multiple nearby pixels as separate course lines.
- The extracted values support the qualitative course-height model and should be verified by future high-resolution metrological survey.

