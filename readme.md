Effect life cycle:
Each effect has only one instance. effect.Init() is called when it is created. 
    It is never destroyed.
Each time an effect starts, effect.OnStart() is called.
Each frame the effect is displayer, effect.Draw() is called.

Animation cycle:
1) Select an effect randomly
2) Play the new for 10 seconds
3) Select a different effect, and a transition.
4) Transition from the current effect to the new one, over 2 seconds
5) Go back to step 2.

Selection process:
For randomly selected things like effects and transitions, there is a collection
of these like a deck of cards. a card is drawn form the top half of the deck
to be used, then the card is placed at the bottom of the deck. This insures
that all effects are used, in a random order, but recent effects take longer
to repeate.

### Beat System & Synchronization
The system includes a `BeatManager` that provides a global clock for rhythmic visuals.
*Note: The current version is a debug simulation that generates beats internally and serves as a placeholder for external OSC data.*
- **Pulse**: Effects use `beatManager.GetBeatBrightness()` to pulse in sync with the BPM. It uses a high-power curve ($x^4$) to ensure the pulse feels like a sharp rhythmic "kick" rather than a slow fade, maintaining high average brightness.
- **Personality**: Each effect instance picks a `beatVariant` in `OnStart` to determine its specific rhythm (e.g., only pulsing on the '1').
- **Variants**: 
    - `0`: Every Beat, `1`: Beats 1&3, `2`: Beats 2&4, `3`: Measure Start
    - `4`: 8th Notes, `5`: 16th Notes, `6`: Syncopated (1 and 4)
- **Bypass**: If `BeatManager.active` is false, all effects return to 100% brightness and standard motion.
- **Distortion Modes**: Some effects (like `Noise`) can randomly choose how to react to the beat:
    - **Brightness**: Rhythmic gain pulsing (default floor of 0.85).
    - **Color**: Post-palette hue rotation (e.g., shifting 90 degrees on the beat).
    - **Time**: Warping `effectTime` to cause motion "surges" or "kicks."

### Developer Tools (Nova Technique)
For debugging, the controller supports a "Nova" testing override:
- **Force Effect**: A toggle in the Inspector (or the **Escape** key during runtime) that enables/disables the override.
- **Force Effect Name**: A string field. When the override is active, the controller searches for any effect whose name contains this string.
- **Behavior**: This bypasses the deck randomization logic for the top-level effect selection.

### Palette System (GPalette)
The project uses a global palette animation system (implemented as `AnimPalette`) to ensure visual harmony across different effects.
- **Sampling**: Effects sample colors using `APalette.read(position, interpolate)` where position is typically a normalized value (0.0 to 1.0).
- **Animation**: The `Controller` updates the palette state every frame, enabling smooth color cycling and rotation.
- **Triggers**: Pressing **Return** reloads the palette definitions at runtime. The palette also shifts automatically during effect transitions to maintain variety.

### Effect Subclasses:
Most effects are **Generative**, meaning they create visual data from scratch using math or noise functions. However, structural effects (Mixers and Wrappers) inherit from **`MixerBase`** and use child effects:

### ScreenEffects
Some generative effects inherit from **`ScreenEffect`** instead of directly from `EffectBase`. 
- **Purpose**: Acts as a "Geometric Lens" for designers. It allows the creation of effects using a standard 2D coordinate system (like Fractals or Fluids) while mapping them to the irregular Penrose grid.
- **Operation**: It provides a `screenBuffer` and a conversion engine. It uses `static` mapping data to ensure that the expensive interpolation weights are only calculated once during the application's lifetime.
- **Note**: Located in the `/core` folder, it is an architectural helper. Subclasses (like `Julia.cs`) look and behave like standard generative effects to the player and the `Controller`.

### Mixers
Mixers are used to combine multiple visual streams. 
- **Behavior**: Inherit from `MixerBase`. They typically manage multiple child effects simultaneously.
- **Interaction**: They call `Draw()` on multiple children and use a blending algorithm (like Additive or Screen) to merge them into a single output.

### Wrappers (Filters)
Wrappers act as post-processors for a single visual source (e.g., `Mirror.cs`).
- **Behavior**: Inherit from `MixerBase`. They encapsulate exactly one `sourceEffect`. 
- **Interaction**: They call `sourceEffect.Draw()` first to populate the buffer, then execute their own logic to transform that data (e.g., duplicating pixels across symmetry lines or shifting colors). They inherit from `MixerBase` to signal this dependency.
