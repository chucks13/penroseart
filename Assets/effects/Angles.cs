using Random = UnityEngine.Random;
using System;
using UnityEngine;

/// <summary>
/// Renders a palette hue sweep based on each tile's stored geometric angle.
/// </summary>
/// <remarks>
/// FILL: a soft-edged wavefront sweeps across the tiles, ordered by each tile's hue distance from the
/// wall's own mean hue (closest first), collapsing their hue toward that mean. The front position is
/// driven by the Fill's <see cref="InSpan.Build"/> (so it always finishes by the Fill's end regardless of
/// how many beats the Fill lasts), given a light pre-Fill primer from <see cref="FillValues.BeatsUntil"/>,
/// and kicked forward once per Waveform hit so the sweep
/// visibly lurches on the beat instead of gliding smoothly.
///
/// DROP: from the Drop's onset the whole wall slams to near-black, then reignites as a
/// staccato cascade through the tiling's ten orientation classes — the multiples-of-18° directional
/// families of the underlying pentagrid. Because orientation drives hue here, each class is also a single
/// hue, so the smooth rainbow visibly reassembles itself one hue/direction at a time out of the darkness,
/// revealing the ten hidden families that compose it. The cascade completes over a few beats back to normal.
///
/// SHADING: a gentle directional brightness gradient keyed to each tile's orientation (as if the faceted
/// quasicrystal were lit from one direction) gives the ten families brightness definition, not just hue.
/// Its depth scales with musical <see cref="BeatManager.Energy"/> — subtle in low-energy sections, more
/// pronounced in high-energy ones. This is pure geometry plus a nullable Energy read, so it renders steady
/// at a fixed mid depth in Standalone (no beat clock) rather than going flat.
///
/// On every new Grid the sweep speed, held Waveform, and shading light direction re-roll, so the look
/// changes character every 16 beats even outside a Fill/Drop.
/// </remarks>
[EffectSyncSettings(typeof(AnglesSyncSettingsAsset))]
public class Angles : EffectBase
{
    // Standalone Defaults

    /// <summary>Minimum sweep speed re-rolled on activation and each new Grid.</summary>
    private const float StandaloneSpeedMin = 0.15f;

    /// <summary>Maximum sweep speed re-rolled on activation and each new Grid.</summary>
    private const float StandaloneSpeedMax = 0.4f;

    /// <summary>Directional-shading depth at Low energy: the dimmest orientation drops this far below full (so its floor is 1 - this). Kept shallow so calm sections stay subtle. Tune on the readout.</summary>
    private const float StandaloneShadeDepthLow = 0.12f;

    /// <summary>Directional-shading depth at High energy: deeper contrast so intense sections read the ten families more strongly, without ever going as dark as the Drop. Tune on the readout.</summary>
    private const float StandaloneShadeDepthHigh = 0.4f;

    /// <summary>Energy level assumed when <see cref="EnergyValues.Level"/> has no value: 0.5 = Mid, a steady moderate shading depth. Tune on the readout.</summary>
    private const float StandaloneEnergy = 0.5f;

    /// <summary>Smoothing rate (per second) easing the shading depth between energy tiers, so a Low/Mid/High change ramps over ~0.5s instead of snapping. Tune on the readout.</summary>
    private const float StandaloneEnergySmoothing = 2f;

    /// <summary>Fixed full-cycle Routine hue offset returned when no live clock can place the choreography.</summary>
    private const float StandaloneRhythmHueOffset = 1f;

    // Sync Defaults

    /// <summary>Width, in normalized rank space (0..1), of the collapsing wavefront's soft edge. Smaller = a crisper traveling edge; larger = a blurrier gradient.</summary>
    private const float SyncFrontSoftness = 0.12f;

    /// <summary>How far a Waveform hit punches the wavefront beyond the stock Fill Build, so the collapse advances in visible surges instead of a silky ramp. Tune on the readout.</summary>
    private const float SyncBeatKick = 0.15f;

    /// <summary>Exponent shaping the long (<see cref="SyncAnticipationBeats"/>-beat, 32 by default) pre-Fill anticipation window so the wavefront primer stays negligible until the last few beats before a Fill actually starts.</summary>
    private const float SyncAnticipationCurvePower = 5f;

    /// <summary>Maximum wavefront envelope contributed by the pre-Fill anticipation primer, reached only right at the Fill's start.</summary>
    private const float SyncAnticipationPrimerCap = 0.18f;

    /// <summary>Length of the pre-Fill anticipation window in beats: how far ahead of a Fill the wavefront primer starts creeping up. Paired with <see cref="SyncAnticipationCurvePower"/>, whose exponent is what keeps the primer negligible across a window this long — retune the two together.</summary>
    private const float SyncAnticipationBeats = 32f;

    /// <summary>Drop length in beats: the whole blackout-and-reignite cascade plays over this many beats of the current tempo, kept short so the event reads within a 2-4 beat window.</summary>
    private const int SyncDropBeats = 3;

    /// <summary>Brightness the wall drops to during the blackout — a floor, not literal off, so it reads as intentional impact rather than a crash. Tune on the readout.</summary>
    private const float SyncDarkFloor = 0.04f;

    /// <summary>Fraction of the Drop window held fully black before the cascade begins — the punctuation of the impact. Tune on the readout.</summary>
    private const float SyncBlackHold = 0.12f;

    /// <summary>Reignition snap width (in cascade-progress units) for each class: smaller = harder staccato pop as each orientation family lights; larger = a softer roll. Tune on the readout.</summary>
    private const float SyncClassSnapWidth = 0.08f;

    /// <summary>First energy pool sampled for the four-bar Routine choreography.</summary>
    private const Energy SyncRoutineEnergyOne = Energy.Mid;

    /// <summary>Second energy pool sampled for the four-bar Routine choreography.</summary>
    private const Energy SyncRoutineEnergyTwo = Energy.Low;

    /// <summary>Third energy pool sampled for the four-bar Routine choreography.</summary>
    private const Energy SyncRoutineEnergyThree = Energy.Mid;

    /// <summary>Fourth energy pool sampled for the four-bar Routine choreography.</summary>
    private const Energy SyncRoutineEnergyFour = Energy.Low;

    /// <summary>Directional-shading depth at Low energy: the dimmest orientation drops this far below full (so its floor is 1 - this). Kept shallow so calm sections stay subtle. Tune on the readout.</summary>
    private const float SyncShadeDepthLow = 0.12f;

    /// <summary>Directional-shading depth at High energy: deeper contrast so intense sections read the ten families more strongly, without ever going as dark as the Drop. Tune on the readout.</summary>
    private const float SyncShadeDepthHigh = 0.4f;

    /// <summary>Smoothing rate (per second) easing the shading depth between energy tiers, so a Low/Mid/High change ramps over ~0.5s instead of snapping. Tune on the readout.</summary>
    private const float SyncEnergySmoothing = 2f;

    /// <summary>Minimum hue rotation applied at the bottom of the live Routine envelope.</summary>
    private const float SyncRhythmHueOffsetMin = 0.8f;

    /// <summary>Maximum hue rotation applied at the top of the live Routine envelope.</summary>
    private const float SyncRhythmHueOffsetMax = 1f;

    // Runtime mechanism constants

    /// <summary>Number of orientation (tileangle) classes the wall reignites through, one per 18° pentagrid direction. Verified against the 900-tile data: exactly 10 classes of 62-119 tiles each.</summary>
    private const int OrientationClasses = 10;

    /// <summary>
    /// The wall's hue pattern collapses toward its own mean hue for a Fill, and blacks out then reignites through
    /// its ten orientation families for a Drop. Its shading depth is subtle at Low energy and pronounced at High,
    /// so it advertises as a Mid/High-energy Performer.
    /// </summary>
    public override Repertoire Repertoire =>
        Repertoire.HandlesFill | Repertoire.HandlesDrop | Repertoire.EnergyMid | Repertoire.EnergyHigh;

    /// <summary>Resolves a fresh immutable-by-convention copy of Angles' Standalone Defaults.</summary>
    public static AnglesStandaloneSettings StandaloneSettings => new AnglesStandaloneSettings
    {
        Speed = new FloatRange(StandaloneSpeedMin, StandaloneSpeedMax),
        ShadeDepthLow = StandaloneShadeDepthLow,
        ShadeDepthHigh = StandaloneShadeDepthHigh,
        Energy = StandaloneEnergy,
        EnergySmoothing = StandaloneEnergySmoothing,
        RhythmHueOffset = StandaloneRhythmHueOffset,
    };

    /// <summary>Resolves a fresh copy of Angles' file-local Sync Defaults.</summary>
    public static AnglesSyncSettings SyncDefaults => new AnglesSyncSettings
    {
        FrontSoftness = SyncFrontSoftness,
        BeatKick = SyncBeatKick,
        AnticipationCurvePower = SyncAnticipationCurvePower,
        AnticipationPrimerCap = SyncAnticipationPrimerCap,
        AnticipationBeats = SyncAnticipationBeats,
        DropBeats = SyncDropBeats,
        DarkFloor = SyncDarkFloor,
        BlackHold = SyncBlackHold,
        ClassSnapWidth = SyncClassSnapWidth,
        RoutineEnergyOne = SyncRoutineEnergyOne,
        RoutineEnergyTwo = SyncRoutineEnergyTwo,
        RoutineEnergyThree = SyncRoutineEnergyThree,
        RoutineEnergyFour = SyncRoutineEnergyFour,
        ShadeDepthLow = SyncShadeDepthLow,
        ShadeDepthHigh = SyncShadeDepthHigh,
        EnergySmoothing = SyncEnergySmoothing,
        RhythmHueOffsetMin = SyncRhythmHueOffsetMin,
        RhythmHueOffsetMax = SyncRhythmHueOffsetMax,
    };

    /// <summary>The Standalone Settings fixed for the current activation.</summary>
    private AnglesStandaloneSettings standaloneSettings = StandaloneSettings;

    /// <summary>The effective saved-or-default Sync Settings read by the current activation.</summary>
    private AnglesSyncSettings SyncSettings { get; set; } = SyncDefaults;

    /// <summary>Current sweep speed rolled for this activation or Grid.</summary>
    private float speed;

    /// <summary>Four-bar waveform choreography, one Waveform per bar drawn from the energy pools named by <see cref="AnglesSyncSettings.RoutineEnergyOne"/> through <see cref="AnglesSyncSettings.RoutineEnergyFour"/>.</summary>
    private Routine routine;

    /// <summary>Wavefront position (0..1) from the stock Fill Build, anticipation primer, and Waveform-hit kicks.</summary>
    private float fillEnv;

    /// <summary>Each tile's raw angle-hue (pre-sweep, pre-beat), cached once since <see cref="Penrose.TileData.tileangle"/> never changes.</summary>
    private float[] rawHue;

    /// <summary>Per tile, the shortest signed hue delta (in [-0.5, 0.5)) from <see cref="rawHue"/> toward <see cref="meanHue"/>, cached once.</summary>
    private float[] hueDelta;

    /// <summary>Per tile, the cascade-progress point (0..~0.9) at which its orientation class reignites during a Drop — its class index / <see cref="OrientationClasses"/>. Cached once.</summary>
    private float[] classReveal;

    /// <summary>Per tile, its normalized rank (0..1) by ascending hue-distance from <see cref="meanHue"/>, cached once. The tile with the closest hue ranks 0 and collapses first.</summary>
    /// <remarks>
    /// This holds the rank alone, not the wavefront envelope value at which the tile's collapse begins.
    /// <see cref="Draw"/> scales it by the live <see cref="AnglesSyncSettings.FrontSoftness"/> each frame to
    /// reach that start value, so a Play Mode edit of the soft-edge width moves the whole front coherently
    /// instead of only its trailing edge.
    /// </remarks>
    private float[] frontRank;

    /// <summary>Per tile, its folded orientation in [0,1) (tileangle mod 180° / 180°), cached once. Drives the directional shading; wraps smoothly so same-facing tiles (0° ≡ 180°) shade identically.</summary>
    private float[] orient01;

    /// <summary>Circular mean of every tile's raw angle-hue: the color the Fill wavefront collapses toward.</summary>
    private float meanHue;

    /// <summary>Direction (radians) the shading gradient is "lit" from; re-rolled each Grid so the bright/shadowed sides of the orientation field shift.</summary>
    /// <remarks>
    /// Its <c>0..2π</c> roll is deliberately not captured as a Standalone randomization range. A full turn is the
    /// complete angular domain of a direction, not an authored span — narrowing it would stop the light reaching
    /// some orientations at all, which is a different effect rather than a tuning of this one.
    /// </remarks>
    private float lightPhase;

    /// <summary>Energy level (0..1) smoothed frame-to-frame, driving shading depth. Seeded to <see cref="StandaloneEnergy"/> so the first frame and all of Standalone render at a steady mid depth.</summary>
    private float smoothedEnergy;

    /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
    public override string DebugText() =>
        "Angles" +
        $"\nEN {smoothedEnergy:0.00}" +
        (fillEnv > 0.01f ? $"\nFILL {fillEnv:0.00}" : "") +
        (beatManager.Drop.Active
            ? $"\nDROP {1f - beatManager.Drop.In.Decay(SyncSettings.DropBeats):0.00}"
            : "");

    /// <summary>
    /// Performs one-time setup after reflection creates this effect instance.
    /// </summary>
    public override void Init()
    {
        base.Init();
        PrecomputeTileFields();
    }

    /// <summary>
    /// Caches the static per-tile geometry used by Fill, Drop, and directional shading.
    /// </summary>
    private void PrecomputeTileFields()
    {
        int total = tiles.Length;
        rawHue = new float[total];
        hueDelta = new float[total];
        classReveal = new float[total];
        frontRank = new float[total];
        orient01 = new float[total];

        float sumX = 0f, sumY = 0f;
        for (int i = 0; i < total; i++)
        {
            rawHue[i] = tiles[i].tileangle / 180f;
            float radians = rawHue[i] * Mathf.PI * 2f;
            sumX += Mathf.Cos(radians);
            sumY += Mathf.Sin(radians);
        }
        meanHue = Mathf.Repeat(Mathf.Atan2(sumY, sumX) / (Mathf.PI * 2f), 1f);

        float[] distance = new float[total];
        int[] order = new int[total];
        for (int i = 0; i < total; i++)
        {
            hueDelta[i] = Mathf.Repeat(meanHue - rawHue[i] + 0.5f, 1f) - 0.5f;
            distance[i] = Mathf.Abs(hueDelta[i]);
            order[i] = i;

            // Folded orientation in [0,1): tileangle mod 180° normalized. Same field feeds both the Drop
            // cascade order (snapped to the nearest 18° class) and the continuous directional shading.
            float folded = Mathf.Repeat(tiles[i].tileangle, 180f) / 180f;
            orient01[i] = folded;
            int cls = Mathf.RoundToInt(folded * OrientationClasses) % OrientationClasses;
            classReveal[i] = cls / (float)OrientationClasses;
        }
        Array.Sort(order, (a, b) => distance[a].CompareTo(distance[b]));

        for (int rank = 0; rank < total; rank++)
        {
            float normalizedRank = total > 1 ? rank / (float)(total - 1) : 0f;
            frontRank[order[rank]] = normalizedRank;
        }
    }

    /// <summary>
    /// Initializes per-activation random state before this effect starts drawing.
    /// </summary>
    public override void OnStart()
    {
        standaloneSettings = StandaloneSettings;
        SyncSettings = EffectSyncSettingsProvider.Resolve(
            typeof(Angles),
            SyncDefaults);
        Reroll();
        fillEnv = 0f;
        smoothedEnergy = standaloneSettings.Energy;
        controller.debugText.text = "Angles";
        buffer.Clear();
    }

    /// <summary>
    /// Re-rolls the per-activation look: sweep speed, four-bar Waveform routine, and shading light direction. Called
    /// once at activation and again on each new Grid, so the look takes a fresh character every 16 beats.
    /// </summary>
    private void Reroll()
    {
        speed = Random.Range(standaloneSettings.Speed.Min, standaloneSettings.Speed.Max);
        routine = Routine.Of(
            waveforms.Random(SyncSettings.RoutineEnergyOne),
            waveforms.Random(SyncSettings.RoutineEnergyTwo),
            waveforms.Random(SyncSettings.RoutineEnergyThree),
            waveforms.Random(SyncSettings.RoutineEnergyFour));
        lightPhase = Random.Range(0f, Mathf.PI * 2f);
    }

    /// <summary>
    /// On each new Grid the sweep takes a fresh speed and four-bar Waveform routine.
    /// </summary>
    protected override void OnNewGrid()
    {
        Reroll();
    }

    /// <summary>
    /// Reserved deactivation hook. Controller does not currently call this.
    /// </summary>
    public override void OnEnd() { }

    /// <summary>
    /// Exponentially eases a value toward a target at a frame-rate-independent rate.
    /// </summary>
    private static float SmoothToward(float current, float target, float rate, float deltaTime) =>
        (1f - Mathf.Exp(-rate * deltaTime)).Lerp(current, target);

    /// <summary>
    /// Updates the Fill wavefront from the stock Build, next-Fill anticipation, and continuous Routine lift.
    /// </summary>
    private void UpdateFillEnvelope()
    {
        var fill = beatManager.Fill;
        bool filling = fill.Active;
        float anticipation = !filling && fill.BeatsUntil is { } next
            ? ((float)next).Remap(0f, SyncSettings.AnticipationBeats, 1f, 0f, clamp: true)
            : 0f;
        float primer = Mathf.Pow(anticipation, SyncSettings.AnticipationCurvePower) * SyncSettings.AnticipationPrimerCap;
        fillEnv = Mathf.Max(fill.In.Build(), primer);

        if (filling)
        {
            fillEnv = Mathf.Min(1f, fillEnv + (SyncSettings.BeatKick * routine.Envelope));
        }
    }

    /// <summary>
    /// Updates musical-energy smoothing and returns the current directional-shading depth.
    /// </summary>
    private float UpdateShadeDepth()
    {
        float energyTarget = beatManager.Energy.Level switch
        {
            Energy.Low => 0f,
            Energy.Mid => 0.5f,
            Energy.High => 1f,
            _ => standaloneSettings.Energy,
        };
        float energySmoothing = beatManager.IsSynced
            ? SyncSettings.EnergySmoothing
            : standaloneSettings.EnergySmoothing;
        smoothedEnergy = SmoothToward(smoothedEnergy, energyTarget, energySmoothing, effectDelta);
        float shadeDepthLow = beatManager.IsSynced
            ? SyncSettings.ShadeDepthLow
            : standaloneSettings.ShadeDepthLow;
        float shadeDepthHigh = beatManager.IsSynced
            ? SyncSettings.ShadeDepthHigh
            : standaloneSettings.ShadeDepthHigh;
        return smoothedEnergy.Lerp(shadeDepthLow, shadeDepthHigh);
    }

    /// <summary>
    /// Renders one frame into this effect's 900-color buffer.
    /// </summary>
    public override void Draw()
    {
        // The Routine rotates the full angle-to-hue pattern without changing the tiles' relative hues.
        float rhythmHueOffset = routine.Lerp(
            SyncSettings.RhythmHueOffsetMin,
            beatManager.IsSynced
                ? SyncSettings.RhythmHueOffsetMax
                : standaloneSettings.RhythmHueOffset);
        UpdateFillEnvelope();
        var drop = beatManager.Drop;
        bool inDrop = drop.Active;
        float cascade = inDrop
            ? (1f - drop.In.Decay(SyncSettings.DropBeats)).Remap(
                SyncSettings.BlackHold,
                1f,
                0f,
                1f,
                clamp: true)
            : 1f;
        float shadeDepth = UpdateShadeDepth();

        // Hoisted: the front's soft edge is uniform across the wall, so its rank scale is one
        // frame-wide value rather than 900 identical products.
        float frontSoftness = SyncSettings.FrontSoftness;
        float rankScale = 1f - frontSoftness;

        for (int i = 0; i < buffer.Length; i++)
        {
            float collapseStart = frontRank[i] * rankScale;
            float collapse = fillEnv.Remap(
                collapseStart,
                collapseStart + frontSoftness,
                0f,
                1f,
                clamp: true);
            float angle = rawHue[i] + (hueDelta[i] * collapse) + (effectTime * speed);

            // Directional shading: same-facing tiles (0° ≡ 180°) shade identically, giving the angle
            // families brightness definition on top of hue.
            float align = 0.5f + (0.5f * Mathf.Cos((orient01[i] * Mathf.PI * 2f) - lightPhase));
            float shade = align.Lerp(1f - shadeDepth, 1f);

            // Drop reignition: outside a Drop every tile sits at its shaded brightness; during a Drop,
            // each orientation class snaps up as the cascade reaches its reveal point.
            float lit = inDrop
                ? cascade.Remap(
                    classReveal[i],
                    classReveal[i] + SyncSettings.ClassSnapWidth,
                    0f,
                    1f,
                    clamp: true)
                : 1f;
            float value = lit.Lerp(SyncSettings.DarkFloor, shade);

            buffer[i] = Color.HSVToRGB(Mathf.Repeat(angle + rhythmHueOffset, 1f), 1f, value);
        }
    }
}

/// <summary>The resolved Standalone Settings that preserve Angles' authored no-music look.</summary>
public sealed class AnglesStandaloneSettings
{
    /// <summary>Per-activation and per-Grid sweep-speed range.</summary>
    public FloatRange Speed;

    /// <summary>Fixed Low-energy directional-shading depth outside live musical placement.</summary>
    public float ShadeDepthLow;

    /// <summary>Fixed High-energy directional-shading depth outside live musical placement.</summary>
    public float ShadeDepthHigh;

    /// <summary>Normalized fallback energy used when no track Energy value exists.</summary>
    public float Energy;

    /// <summary>Per-second shading-depth smoothing rate outside live musical placement.</summary>
    public float EnergySmoothing;

    /// <summary>Fixed Routine hue offset returned without live musical placement.</summary>
    public float RhythmHueOffset;
}

/// <summary>The saved-or-default musical-response settings used by Angles in Synced Mode.</summary>
[Serializable]
public sealed class AnglesSyncSettings
{
    /// <summary>Width of the Fill wavefront's soft edge in normalized rank space.</summary>
    [Range(0.0001f, 1f)] public float FrontSoftness;

    /// <summary>Extra Fill wavefront advance contributed by a Waveform hit.</summary>
    [Min(0f)] public float BeatKick;

    /// <summary>Exponent that keeps pre-Fill anticipation negligible until the event approaches.</summary>
    [Min(0.0001f)] public float AnticipationCurvePower;

    /// <summary>Maximum Fill wavefront contribution from pre-Fill anticipation.</summary>
    [Range(0f, 1f)] public float AnticipationPrimerCap;

    /// <summary>Length of the pre-Fill anticipation window in beats.</summary>
    [Min(0.0001f)] public float AnticipationBeats;

    /// <summary>Length of the Drop blackout-and-reignition cascade in beats.</summary>
    [Min(1)] public int DropBeats;

    /// <summary>Minimum brightness retained during the Drop blackout.</summary>
    [Range(0f, 1f)] public float DarkFloor;

    /// <summary>Fraction of the Drop window held at the blackout floor before reignition.</summary>
    [Range(0f, 1f)] public float BlackHold;

    /// <summary>Soft-edge width used as each orientation class reignites.</summary>
    [Range(0.0001f, 1f)] public float ClassSnapWidth;

    /// <summary>First energy pool sampled for the four-bar Routine choreography.</summary>
    public Energy RoutineEnergyOne;

    /// <summary>Second energy pool sampled for the four-bar Routine choreography.</summary>
    public Energy RoutineEnergyTwo;

    /// <summary>Third energy pool sampled for the four-bar Routine choreography.</summary>
    public Energy RoutineEnergyThree;

    /// <summary>Fourth energy pool sampled for the four-bar Routine choreography.</summary>
    public Energy RoutineEnergyFour;

    /// <summary>Directional-shading depth at Low track Energy.</summary>
    [Range(0f, 1f)] public float ShadeDepthLow;

    /// <summary>Directional-shading depth at High track Energy.</summary>
    [Range(0f, 1f)] public float ShadeDepthHigh;

    /// <summary>Per-second smoothing rate between track Energy shading targets.</summary>
    [Min(0f)] public float EnergySmoothing;

    /// <summary>Hue rotation applied at the bottom of the live Routine envelope.</summary>
    [Range(0f, 1f)] public float RhythmHueOffsetMin;

    /// <summary>Hue rotation applied at the top of the live Routine envelope.</summary>
    [Range(0f, 1f)] public float RhythmHueOffsetMax;

    /// <summary>Copies every Angles Sync Setting from another value.</summary>
    public void CopyFrom(AnglesSyncSettings source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        FrontSoftness = source.FrontSoftness;
        BeatKick = source.BeatKick;
        AnticipationCurvePower = source.AnticipationCurvePower;
        AnticipationPrimerCap = source.AnticipationPrimerCap;
        AnticipationBeats = source.AnticipationBeats;
        DropBeats = source.DropBeats;
        DarkFloor = source.DarkFloor;
        BlackHold = source.BlackHold;
        ClassSnapWidth = source.ClassSnapWidth;
        RoutineEnergyOne = source.RoutineEnergyOne;
        RoutineEnergyTwo = source.RoutineEnergyTwo;
        RoutineEnergyThree = source.RoutineEnergyThree;
        RoutineEnergyFour = source.RoutineEnergyFour;
        ShadeDepthLow = source.ShadeDepthLow;
        ShadeDepthHigh = source.ShadeDepthHigh;
        EnergySmoothing = source.EnergySmoothing;
        RhythmHueOffsetMin = source.RhythmHueOffsetMin;
        RhythmHueOffsetMax = source.RhythmHueOffsetMax;
    }
}
