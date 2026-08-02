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

/// <summary>Formats canonical Controller status labels without duplicating runtime interpretation across editor views.</summary>
internal static class ControllerStatusText
{
    /// <summary>Formats the Director's staged Effect and Transition as one concise future-intent row.</summary>
    internal static string FormatDirectorNext(DirectorStatus status)
    {
        return $"{FormatCatalogChoice(status.NextEffectIndex, status.NextEffectName)} · " +
            FormatCatalogChoice(status.NextTransitionIndex, status.NextTransitionName);
    }

    /// <summary>Formats the Held Effect independently from Hold Selected.</summary>
    internal static string FormatHeldEffect(DirectorStatus status)
    {
        return status.HeldEffectIndex < 0
            ? "Random"
            : FormatCatalogChoice(status.HeldEffectIndex, status.HeldEffectName);
    }

    /// <summary>Formats the Switcher's current Effect or active Transition without implying future intent.</summary>
    internal static string FormatSwitcherActive(SwitcherStatus status)
    {
        if (!status.Ready)
        {
            return "Unavailable";
        }

        return status.CurrentTransitionIndex >= 0
            ? $"{FormatCatalogChoice(status.CurrentTransitionIndex, status.CurrentTransitionName)} · {status.TransitionProgress:P0}"
            : FormatCatalogChoice(status.CurrentEffectIndex, status.CurrentEffectName);
    }

    /// <summary>Formats a catalog identity while retaining its stable runtime index.</summary>
    internal static string FormatCatalogChoice(int index, string name)
    {
        if (index < 0)
        {
            return "Unavailable";
        }

        return string.IsNullOrWhiteSpace(name) ? $"#{index}" : $"{name} (#{index})";
    }
}
