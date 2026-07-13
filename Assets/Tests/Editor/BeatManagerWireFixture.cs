// Test-only wire ingress fixture for incrementally authored RaveSystem snapshots.

#nullable enable

using System;
using System.Runtime.CompilerServices;
using PenroseArt.RaveOsc;

/// <summary>
/// Owns mutable transport builders for tests while feeding every resulting snapshot through the
/// same deep-copy ingress as the live OSC adapter.
/// </summary>
internal static class BeatManagerWireFixture
{
    /// <summary>Per-manager staging snapshots; production BeatManager state never leaves the hub.</summary>
    private static readonly ConditionalWeakTable<BeatManager, RaveOnAirSnapshot> Snapshots = new();

    /// <summary>Applies a staging mutation and immediately feeds its deep-copied result into the hub.</summary>
    internal static void Feed(BeatManager beatManager, Action<RaveOnAirSnapshot> mutate)
    {
        var snapshot = Snapshots.GetOrCreateValue(beatManager);
        mutate(snapshot);
        beatManager.FeedWireSnapshot(snapshot);
    }
}
