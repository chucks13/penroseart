// Copyright © 2026 Hunter Luisi. All rights reserved.
// RaveSystem OSC client state for PenroseArt.

#nullable enable

namespace PenroseArt.RaveOsc {

/// <summary>
/// Latest known RaveSystem on-air OSC values decoded from UDP broadcasts.
/// </summary>
public struct RaveOscSnapshot {
    public bool HasBpm;
    public float Bpm;

    public bool HasBeat;
    public int Beat;

    public bool HasBar;
    public int Bar;

    public bool HasBeatInBar;
    public int BeatInBar;

    public bool HasNextBeatMs;
    public int NextBeatMs;

    public bool HasOnBeat;
    public bool OnBeat;

    public bool HasLow;
    public float Low;

    public bool HasMid;
    public float Mid;

    public bool HasHigh;
    public float High;

    public bool HasDropIn;
    public bool DropIn;

    public bool HasPhase;
    public string? Phase;

    public override string ToString() {
        var bpm = HasBpm ? Bpm.ToString("0.##") : "?";
        var beat = HasBeat ? Beat.ToString() : "?";
        var bar = HasBar ? Bar.ToString() : "?";
        var phrase = HasPhase ? Phase : "?";
        return $"Rave OSC bpm={bpm} beat={beat} bar={bar} phase={phrase}";
    }
}

}
