# Penrose Waveform Designer

This folder contains a standalone browser sketchpad for designing and visualizing waveform ideas. Its JSON file belongs only to the browser tool; the Penrose runtime does not consume it.

## Files in this Folder

- `index.html`: The main HTML file for the waveform designer UI.
- `generator.js`: The JavaScript logic that handles waveform generation, UI interaction, and data management.
- `penrose_waveforms.json`: This file stores all your defined waveforms. It is loaded by `generator.js` on startup and should be updated with your exported changes.

## How to Use the Waveform Designer

1.  **Launch the Designer**: Open `index.html` in a web browser.
    *   **Note**: Due to browser security restrictions, loading local JSON files via `fetch` might not work directly when opening `index.html` as a `file://` URL. It's recommended to use a local web server (e.g., Python's `http.server`, Node.js `serve`, or the "Live Server" extension in VS Code) to serve the `waveforms` folder.

2.  **Waveform List**:
   *   The dropdown menu (`waveList`) at the top displays all currently loaded waveforms. Select one to view and edit its properties.

3.  **Waveform Properties**:
   *   **Name**: A descriptive name for your waveform.
   *   **Energy**: An integer value representing the energy level associated with the waveform.
   *   **Wave Sequence (1,2,4,8)**: A string of numbers (1, 2, 4, or 8) defining the frequency pattern of the waveform.
   *   **Amplitude Sequence (0-8)**: A string of numbers (0-8) defining the amplitude pattern.

4.  **Actions**:
   *   **+ Add New**: Creates a new waveform with default values and selects it for editing.
   *   **Delete**: Removes the currently selected waveform from the list.

5.  **Saving Your Work (Exporting to JSON)**:
   *   All changes to waveform properties (Name, Energy, Sequences) are automatically saved to the internal list when you type.
   *   To persist these changes to the browser tool's `penrose_waveforms.json` file, click the **Export JSON** button.
   *   This will download a file named `penrose_waveforms.json` to your browser's downloads folder.
   *   **Crucial Step**: **Move this downloaded `penrose_waveforms.json` file into this `waveforms/` folder, replacing the existing one.** This ensures your changes are saved and can be committed to version control.

## Penrose runtime

The runtime Pool is [`Assets/StreamingAssets/penrose_waveforms.txt`](../Assets/StreamingAssets/penrose_waveforms.txt), loaded and decoded by [`WaveformPool`](../Assets/core/Rhythm/WaveformPool.cs). The JSON in this directory is not a runtime input; transfer a waveform into the Pool through the Waveform Pool editor or its canonical text format.
