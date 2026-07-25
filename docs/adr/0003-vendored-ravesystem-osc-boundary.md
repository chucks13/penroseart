# Vendored RaveSystem.Osc boundary

Status: accepted

PenroseArt carries a Unity-compatible copy of the generic `RaveSystem.Osc` library under `Assets/OSC/*.cs`. That code is a compatibility port of a reusable OSC implementation, not the place for Penrose-specific runtime policy. Penrose/Rave application behavior lives beside it in `Assets/OSC/Rave/` and in core consumers such as `Assets/core/IO/RaveOscReceiver.cs` and `Assets/core/Rhythm/BeatManager.cs`. The boundary is easy to blur during urgent live-installation debugging — the generic dispatcher, bundle reader, and timetag code sit close to the symptom — but changing them for Penrose policy mutates an imported library and makes future upstream comparisons harder.

## `Assets/OSC/*.cs` is a vendored generic library
The top-level C# files directly under `Assets/OSC/` are treated as imported `RaveSystem.Osc` code adapted only for Unity's C#/.NET profile. They keep copyright/origin headers:

```csharp
// Copyright © 2026 Hunter Luisi. All rights reserved.
// Origin: RaveSystem.Osc; adapted for PenroseArt's Unity runtime.
```

Those files may be edited when the task is explicitly about the generic OSC implementation itself: wire format, OSC type support, address validation/matching, bundle reading/writing, dispatch semantics, UDP transport, Unity compatibility, or keeping the port aligned with upstream `RaveSystem.Osc`.

## Penrose policy does not go into the generic OSC files
Penrose-specific behavior must not be solved by special-casing `Assets/OSC/*.cs`. Examples of Penrose policy that belongs outside the vendored library:

- Rave on-air source selection and liveness.
- Simulation/fallback behavior.
- Whether `/rave/onair/*` payload values are usable.
- Treating Rave live telemetry bundle timetags as metadata instead of delivery scheduling.
- Clock-skew tolerance for the installation's live DJ telemetry stream.

For example, OSC bundle timetag scheduling is valid generic OSC dispatcher behavior. If Penrose needs live `/rave/onair/*` packets to apply on local receive time regardless of sender clock skew, that rule belongs in `Assets/OSC/Rave/RaveOscPacketParser.cs` or `Assets/core/IO/RaveOscReceiver.cs`, not in `Assets/OSC/OscDispatcher.cs`.

## Adapter code carries Penrose ownership, not imported-origin headers
Files under `Assets/OSC/Rave/` are PenroseArt's Rave OSC adapter. They should carry Penrose copyright headers but not the `Origin: RaveSystem.Osc` header, because they are application integration code rather than copied generic library code.

OSC tests under `Assets/OSC/Tests/Editor/` also carry copyright headers. Tests that assert generic OSC behavior should stay generic; tests for `/rave/onair/*` live telemetry behavior should target the adapter layer.

## Modify the vendored library only with an explicit boundary check
Before changing `Assets/OSC/*.cs`, agents should verify and state which category the change is in:

1. **Generic OSC/library change** — allowed in `Assets/OSC/*.cs`, ideally compared against upstream RaveSystem and covered by generic OSC tests.
2. **Unity compatibility port change** — allowed in `Assets/OSC/*.cs` when it preserves the generic API/behavior while making Unity compile/run it.
3. **Penrose/Rave application policy** — not allowed in `Assets/OSC/*.cs`; implement in `Assets/OSC/Rave/` or core runtime consumers instead.

If the category is not clear, stop and ask before editing the vendored library.

## Consequences

- Existing behavior changes already made inside `Assets/OSC/*.cs` for Penrose source-selection policy should be audited and either justified as generic OSC changes or moved back out to the adapter layer.
