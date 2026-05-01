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

### Developer Tools (Nova Technique)
For debugging, the controller supports a "Nova" testing override:
- **Force Effect**: A toggle in the Inspector (or the **Escape** key during runtime) that enables/disables the override.
- **Force Effect Name**: A string field. When the override is active, the controller searches for any effect whose name contains this string.
- **Behavior**: This bypasses the deck randomization logic for the top-level effect selection.

Effect Subclasses:
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
