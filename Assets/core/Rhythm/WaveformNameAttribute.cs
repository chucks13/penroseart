// Marks a serialized string setting as a Waveform Pool entry name for pulldown editing.

using UnityEngine;

/// <summary>
/// Marks a serialized string field that holds a Waveform Pool entry name, so editor surfaces render
/// it as a pulldown over the current Pool instead of a free text box. Runtime acquisition reads the
/// stored name through <see cref="Waveforms.Named"/>.
/// </summary>
public sealed class WaveformNameAttribute : PropertyAttribute
{
}
