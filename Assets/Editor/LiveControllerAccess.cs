using UnityEditor;
using UnityEngine;

/// <summary>
/// No-spawn access to the live <see cref="Controller"/> from editor UI.
/// </summary>
/// <remarks>
/// <see cref="Controller.Instance"/> is a singleton accessor that can create a Controller GameObject when no
/// instance exists. Editor tooling that merely wants to observe or steer Play Mode must cross this seam instead:
/// it first checks Play Mode and <see cref="Singleton{T}.HasInstance"/>, then reads the instance only when one is
/// already present.
/// </remarks>
internal static class LiveControllerAccess
{
    /// <summary>Returns the existing live Controller without creating one; false outside Play Mode or before startup.</summary>
    public static bool TryGet(out Controller liveController)
    {
        liveController = null;
        if (!Application.isPlaying || !Controller.HasInstance)
        {
            return false;
        }

        liveController = Controller.Instance;
        return liveController != null;
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
