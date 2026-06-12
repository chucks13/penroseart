using UnityEditor;
using UnityEngine;

/// <summary>
/// Default Controller inspector that repaints every editor frame during Play Mode.
/// </summary>
/// <remarks>
/// Controller.beatManager rewrites BeatData every frame at runtime, but a default inspector only repaints
/// on the editor's idle tick, so live dashboards such as <see cref="BeatManagerDrawer"/> animate in coarse steps.
/// Requesting constant repaints while playing lets the beat/offbeat pulse meters, the flashing current-beat dot,
/// and the millisecond countdowns animate smoothly. The drawing itself is unchanged: the base Editor still renders
/// the standard default inspector, and constant repaint is dropped the moment Play Mode ends. The shortcut into the
/// Waveform Pool editor lives in the <see cref="BeatManagerDrawer"/> panel, right beside the live dashboard.
/// </remarks>
[CustomEditor(typeof(Controller))]
public sealed class ControllerEditor : Editor
{
    /// <summary>Repaints continuously only while playing, where BeatData changes every frame.</summary>
    public override bool RequiresConstantRepaint()
    {
        return Application.isPlaying;
    }
}
