// Shared test helper for writing real /rave/onair OSC lanes into bundles.

#nullable enable

using RaveSystem.Osc;

/// <summary>
/// Test helper: writes <c>/rave/onair/*</c> lanes into an OSC bundle with the wire contract's
/// type tags, so wire-in tests build real packets instead of hand-assembled snapshots. Shared by
/// the ingestion round-trip and Data Surface suites (one spelling of each lane's shape).
/// </summary>
internal static class OnAirOscWriter
{
    /// <summary>Writes a single-int lane (beat, bar, beat_in_bar, beat_avg_ms, track_id, ...).</summary>
    internal static void WriteInt(ref OscBundleWriter bundle, string address, int value)
    {
        var element = bundle.BeginElement();
        var writer = new OscWriter(element);
        writer.WriteAddress(address);
        writer.WriteInt32(value);
        bundle.EndElement(writer.Finish());
    }

    /// <summary>Writes a four-int lane (beats_count_ms, on_beats).</summary>
    internal static void WriteFourInts(ref OscBundleWriter bundle, string address, int first, int second, int third, int fourth)
    {
        var element = bundle.BeginElement();
        var writer = new OscWriter(element);
        writer.WriteAddress(address);
        writer.WriteInt32(first);
        writer.WriteInt32(second);
        writer.WriteInt32(third);
        writer.WriteInt32(fourth);
        bundle.EndElement(writer.Finish());
    }

    /// <summary>Writes a single-float lane (bpm, beat_pulse).</summary>
    internal static void WriteFloat(ref OscBundleWriter bundle, string address, float value)
    {
        var element = bundle.BeginElement();
        var writer = new OscWriter(element);
        writer.WriteAddress(address);
        writer.WriteFloat32(value);
        bundle.EndElement(writer.Finish());
    }

    /// <summary>Writes the three-float levels lane (low, mid, high).</summary>
    internal static void WriteThreeFloats(ref OscBundleWriter bundle, string address, float low, float mid, float high)
    {
        var element = bundle.BeginElement();
        var writer = new OscWriter(element);
        writer.WriteAddress(address);
        writer.WriteFloat32(low);
        writer.WriteFloat32(mid);
        writer.WriteFloat32(high);
        bundle.EndElement(writer.Finish());
    }

    /// <summary>Writes the <c>fff</c> <c>/rave/onair/levels</c> lane: low/mid/high band energy (-1 sentinels when unavailable).</summary>
    internal static void WriteLevels(ref OscBundleWriter bundle, float low, float mid, float high)
    {
        WriteThreeFloats(ref bundle, "/rave/onair/levels", low, mid, high);
    }

    /// <summary>Writes a single-string lane (players_live, track).</summary>
    internal static void WriteString(ref OscBundleWriter bundle, string address, string value)
    {
        var element = bundle.BeginElement();
        var writer = new OscWriter(element);
        writer.WriteAddress(address);
        writer.WriteString(value);
        bundle.EndElement(writer.Finish());
    }

    /// <summary>Writes a <c>siii</c> phrase_state lane: label, countBeats, lengthBeats, irregular tri-state.</summary>
    internal static void WritePhraseState(
        ref OscBundleWriter bundle,
        string address,
        string label,
        int countBeats,
        int lengthBeats,
        int irregular)
    {
        var element = bundle.BeginElement();
        var writer = new OscWriter(element);
        writer.WriteAddress(address);
        writer.WriteString(label);
        writer.WriteInt32(countBeats);
        writer.WriteInt32(lengthBeats);
        writer.WriteInt32(irregular);
        bundle.EndElement(writer.Finish());
    }

    /// <summary>Writes a <c>sii</c> labeled-countdown lane shared by next_phrase_state/energy_state/next_energy_state.</summary>
    internal static void WriteLabeledCountdown(
        ref OscBundleWriter bundle,
        string address,
        string label,
        int countBeats,
        int lengthBeats)
    {
        var element = bundle.BeginElement();
        var writer = new OscWriter(element);
        writer.WriteAddress(address);
        writer.WriteString(label);
        writer.WriteInt32(countBeats);
        writer.WriteInt32(lengthBeats);
        bundle.EndElement(writer.Finish());
    }

    /// <summary>Writes an <c>iifiii</c> loop_state lane: active/set tri-states, lengthBeats (float), lengthMs, size fraction.</summary>
    internal static void WriteLoopState(
        ref OscBundleWriter bundle,
        string address,
        int active,
        int set,
        float lengthBeats,
        int lengthMs,
        int sizeNumerator,
        int sizeDenominator)
    {
        var element = bundle.BeginElement();
        var writer = new OscWriter(element);
        writer.WriteAddress(address);
        writer.WriteInt32(active);
        writer.WriteInt32(set);
        writer.WriteFloat32(lengthBeats);
        writer.WriteInt32(lengthMs);
        writer.WriteInt32(sizeNumerator);
        writer.WriteInt32(sizeDenominator);
        bundle.EndElement(writer.Finish());
    }

    /// <summary>Writes an <c>iis</c> timing_grid lane: beat, bar, grid-confidence state string.</summary>
    internal static void WriteTimingGrid(ref OscBundleWriter bundle, string address, int beat, int bar, string state)
    {
        var element = bundle.BeginElement();
        var writer = new OscWriter(element);
        writer.WriteAddress(address);
        writer.WriteInt32(beat);
        writer.WriteInt32(bar);
        writer.WriteString(state);
        bundle.EndElement(writer.Finish());
    }

    /// <summary>Writes an <c>iiii</c> countdown-state lane shared by drop_state/fill_state.</summary>
    internal static void WriteCountdownState(ref OscBundleWriter bundle, string address, int active, int countBeats, int lengthBeats, int remaining)
    {
        var element = bundle.BeginElement();
        var writer = new OscWriter(element);
        writer.WriteAddress(address);
        writer.WriteInt32(active);
        writer.WriteInt32(countBeats);
        writer.WriteInt32(lengthBeats);
        writer.WriteInt32(remaining);
        bundle.EndElement(writer.Finish());
    }
}
