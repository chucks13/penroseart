using UnityEditor;
using UnityEngine;

/// <summary>
/// No-spawn access to the live <see cref="Controller"/> from editor UI.
/// </summary>
/// <remarks>
/// Runtime code can use <see cref="Controller.Instance"/> when a live scene Controller is required.
/// Editor tooling that merely wants to observe or steer Play Mode should cross this seam instead so it can
/// report "not running yet" without throwing when the scene Controller has not registered.
/// </remarks>
internal static class LiveControllerAccess
{
    /// <summary>Returns the existing live Controller without creating one; false outside Play Mode or before startup.</summary>
    public static bool TryGet(out Controller liveController)
    {
        liveController = null;
        return Application.isPlaying && Controller.TryGetInstance(out liveController);
    }

    /// <summary>Requests a repaint on Play Mode updates so editor windows can animate live state.</summary>
    public static void RepaintDuringPlayMode(EditorWindow window)
    {
        if (Application.isPlaying)
        {
            window.Repaint();
        }
    }
}
