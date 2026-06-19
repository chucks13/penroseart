using System;

/// <summary>Broad visual family for an A-to-B transition.</summary>
public enum TransitionShape
{
    Blend,
    ChannelBlend,
    DirectionalWipe,
    IndexWipe,
    Dissolve,
    Iris,
    Noise
}

/// <summary>How forcefully a transition reads as a musical move.</summary>
public enum TransitionIntensity
{
    Subtle,
    Medium,
    High
}

/// <summary>
/// Timing and musical-use contract that a Transition advertises to the Director.
/// </summary>
/// <remarks>
/// A transition moves from A to B over <see cref="DurationBeats"/> in Synced Mode.
/// The Director schedules the transition so the <see cref="RunwayBeats"/> lead-in reaches
/// the <see cref="ImpactPoint"/> on the key musical beat; completion can happen on that same
/// beat or after it depending on <see cref="TailBeats"/>. Standalone Mode uses
/// <see cref="DefaultDurationSeconds"/> for the same A-to-B motion.
/// </remarks>
public readonly struct TransitionRepertoire
{
    /// <summary>Default transition contract: a four-beat blend with a four-second Standalone duration.</summary>
    public static TransitionRepertoire Default { get; } = FromRunwayAndTail(
        Repertoire.None,
        runwayBeats: 4,
        tailBeats: 0,
        TransitionShape.Blend,
        TransitionIntensity.Subtle,
        defaultDurationSeconds: 4f);

    /// <summary>Musical situations this transition is suited for, such as Drop or Energy changes.</summary>
    public readonly Repertoire Tags;

    /// <summary>Beats from transition start to the Impact Point.</summary>
    public readonly int RunwayBeats;

    /// <summary>Beats from the Impact Point to full B/completion.</summary>
    public readonly int TailBeats;

    /// <summary>Broad visual family for this transition.</summary>
    public readonly TransitionShape Shape;

    /// <summary>How forcefully this transition reads as a musical move.</summary>
    public readonly TransitionIntensity Intensity;

    /// <summary>Default Standalone Mode A-to-B transition duration in seconds.</summary>
    public readonly float DefaultDurationSeconds;

    /// <summary>Total A-to-B transition duration in beats.</summary>
    public int DurationBeats => RunwayBeats + TailBeats;

    /// <summary>Normalized A-to-B progress where the key musical beat should land.</summary>
    public float ImpactPoint => RunwayBeats / (float)DurationBeats;

    /// <summary>Whether this transition intentionally continues after its Impact Point.</summary>
    public bool HasTail => TailBeats > 0;

    public TransitionRepertoire(
        Repertoire tags,
        int runwayBeats,
        int tailBeats,
        TransitionShape shape,
        TransitionIntensity intensity,
        float defaultDurationSeconds)
    {
        if (runwayBeats <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(runwayBeats), runwayBeats, "Runway must be at least one beat.");
        }

        if (tailBeats < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tailBeats), tailBeats, "Tail cannot be negative.");
        }


        Tags = tags;
        RunwayBeats = runwayBeats;
        TailBeats = tailBeats;
        Shape = shape;
        Intensity = intensity;
        DefaultDurationSeconds = defaultDurationSeconds;
    }

    /// <summary>
    /// Creates a transition contract from the musical language the Director uses to schedule it.
    /// </summary>
    public static TransitionRepertoire FromRunwayAndTail(
        Repertoire tags,
        int runwayBeats,
        int tailBeats,
        TransitionShape shape,
        TransitionIntensity intensity,
        float defaultDurationSeconds)
    {
        return new TransitionRepertoire(tags, runwayBeats, tailBeats, shape, intensity, defaultDurationSeconds);
    }
}
