# Helper Directory

`Assets/core/helpers/` contains small support utilities that do not currently justify their own domain folder.

Most runtime systems have moved into explicit `Assets/core/` subdirectories such as `Runtime/`, `Effects/`, `Transitions/`, `Switching/`, `Rhythm/`, `IO/`, `Hardware/`, `Blending/`, `ReactiveInputs/`, and `Reference/`. Keep new code near the subsystem it primarily supports; use this helper directory only for small cross-cutting utilities.

## Current helper files

- `Factory.cs` — reflection catalog builder and `[RuntimeCatalogIgnore]` attribute for effects, transitions, and blenders.
- `GPalette.cs` — shared palette parsing, sampling, and animated palette transitions.
- `Timer.cs` — lightweight countdown timer used by runtime switching paths.
- `Singleton.cs` — generic Unity `MonoBehaviour` singleton base used by `Controller`.
- `ExtensionMethods.cs` — shared numeric, vector, color, and buffer helpers.
- `SliderScript.cs` — scene UI slider binding for controller brightness.
- `TelnetServer.cs` — optional telnet command server compiled only when telnet support is enabled.

## Guidance

Do not add broad new systems here by default. Prefer the subsystem folders when ownership is clear; leave `helpers/` for small shared utilities whose purpose is genuinely cross-cutting.
