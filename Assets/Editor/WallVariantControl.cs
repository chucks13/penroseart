using UnityEngine;

/// <summary>
/// Editor adapter for reading and writing the live wall-wide Waveform variant lock.
/// </summary>
/// <remarks>
/// The BeatManager dashboard needs three live facts together: the wall's <see cref="BeatManager.activeVariant"/>,
/// the on-screen effect's current variant, and the <see cref="BeatManager"/> to write when the dropdown changes.
/// This module owns that live Controller crossing through <see cref="LiveControllerAccess"/> so drawers and windows
/// do not duplicate the no-spawn singleton guard.
/// </remarks>
internal static class WallVariantControl
{
    /// <summary>
    /// Returns the on-screen effect variant for a dashboard owned by <paramref name="owner"/>, or <c>-1</c>
    /// when Play Mode has no safe live Controller context.
    /// </summary>
    public static int ResolveOnScreenVariant(Object owner)
    {
        if (!Application.isPlaying)
        {
            return -1;
        }

        if (owner is Controller ownerController)
        {
            return ownerController.CurrentBeatVariant;
        }

        return TryGetState(out var state) ? state.CurrentVariant : -1;
    }

    /// <summary>Reads the live wall variant state without creating a Controller.</summary>
    public static bool TryGetState(out WallVariantState state)
    {
        state = WallVariantState.None;
        if (!LiveControllerAccess.TryGet(out var controller))
        {
            return false;
        }

        var beatManager = controller.beatManager;
        state = new WallVariantState(beatManager, beatManager.activeVariant, controller.CurrentBeatVariant);
        return true;
    }

    /// <summary>
    /// Applies a dropdown row to the live wall: row 0 releases Auto, rows 1.. lock the parallel Pool index.
    /// </summary>
    public static void ApplySelection(BeatManager beatManager, int dropdownIndex)
    {
        if (dropdownIndex <= 0)
        {
            beatManager.ReleaseToAuto();
            return;
        }

        beatManager.LockVariant(dropdownIndex - 1);
        if (LiveControllerAccess.TryGet(out var controller))
        {
            controller.CurrentBeatVariant = beatManager.activeVariant; // immediate: retarget the effect on screen
        }
    }
}

internal readonly struct WallVariantState
{
    public static readonly WallVariantState None = new WallVariantState(null, -1, -1);

    public readonly BeatManager BeatManager;
    public readonly int ActiveVariant;
    public readonly int CurrentVariant;

    public WallVariantState(BeatManager beatManager, int activeVariant, int currentVariant)
    {
        BeatManager = beatManager;
        ActiveVariant = activeVariant;
        CurrentVariant = currentVariant;
    }
}
