// Assembly-level attributes for the core runtime (compiled into Unity's predefined Assembly-CSharp).

using System.Runtime.CompilerServices;

// The edit-mode tests under Assets/Tests/Editor compile into Assembly-CSharp-Editor and exercise
// internal machinery — the BeatManager wire-snapshot test seam and the Data Surface edge rules.
// Internals stay internal: they are test seams and hub bookkeeping, never a consumer offering.
[assembly: InternalsVisibleTo("Assembly-CSharp-Editor")]
