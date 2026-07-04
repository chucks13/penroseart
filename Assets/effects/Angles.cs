using Random = UnityEngine.Random;
using System;
using UnityEngine;

/// <summary>
/// Renders a palette hue sweep based on each tile's stored geometric angle.
/// </summary>
/// <remarks>
/// FILL: a soft-edged wavefront sweeps across the tiles, ordered by each tile's hue distance from the
/// wall's own mean hue (closest first), collapsing their hue toward that mean. The front position is
/// driven by <see cref="BeatManager.Fill"/> progress (so it always finishes by the Fill's end regardless of
/// how many beats the Fill lasts), eased with a fast attack/slow release envelope, given a light pre-Fill
/// primer from <see cref="BeatManager.Fill"/> anticipation, and kicked forward once per beat so the sweep
/// visibly lurches on the beat instead of gliding smoothly.
///
/// DROP: on the first Grid downbeat inside a Drop the whole wall slams to near-black, then reignites as a
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
/// On every new Grid the sweep speed, rhythmic variant, and shading light direction re-roll, so the look
/// changes character every 16 beats even outside a Fill/Drop.
/// </remarks>
public class Angles : EffectBase
{
    /// <summary>
    /// The wall's hue pattern collapses toward its own mean hue for a Fill, and blacks out then reignites through
    /// its ten orientation families for a Drop. Its shading depth is subtle at Low energy and pronounced at High,
    /// so it advertises as a Mid/High-energy Performer.
    /// </summary>
    public override Repertoire Repertoire =>
        Repertoire.HandlesFill | Repertoire.HandlesDrop | Repertoire.EnergyMid | Repertoire.EnergyHigh;

    private float speed;

    /// <summary>Width, in normalized rank space (0..1), of the collapsing wavefront's soft edge. Smaller = a crisper traveling edge; larger = a blurrier gradient.</summary>
    private const float FrontSoftness = 0.12f;

    /// <summary>Fast attack rate (per second) so even a one-beat Fill slams the wavefront envelope toward full drama instead of barely ramping.</summary>
    private const float FillAttack = 22f;

    /// <summary>Slower release rate (per second) so the wavefront eases back to normal instead of snapping if a Fill ends without a Drop.</summary>
    private const float FillRelease = 5f;

    /// <summary>How far a single on-beat kick punches the wavefront envelope forward, on top of the smoothed track toward Fill progress, so the collapse advances in visible per-beat surges instead of a silky ramp. Tune on the readout.</summary>
    private const float BeatKick = 0.15f;

    /// <summary>Exponent shaping the long (32-beat) pre-Fill anticipation window so the wavefront primer stays negligible until the last few beats before a Fill actually starts.</summary>
    private const float AnticipationCurvePower = 5f;

    /// <summary>Maximum wavefront envelope contributed by the pre-Fill anticipation primer, reached only right at the Fill's start.</summary>
    private const float AnticipationPrimerCap = 0.18f;

    /// <summary>Drop length in beats: the whole blackout-and-reignite cascade plays over this many beats of the current tempo, kept short so the event reads within a 2-4 beat window.</summary>
    private const float DropBeats = 3f;

    /// <summary>Drop length used when no BPM is available (3 beats at 120 BPM).</summary>
    private const float DropFallbackSeconds = 1.5f;

    /// <summary>Number of orientation (tileangle) classes the wall reignites through, one per 18° pentagrid direction. Verified against the 900-tile data: exactly 10 classes of 62-119 tiles each.</summary>
    private const int OrientationClasses = 10;

    /// <summary>Brightness the wall drops to during the blackout — a floor, not literal off, so it reads as intentional impact rather than a crash. Tune on the readout.</summary>
    private const float DarkFloor = 0.04f;

    /// <summary>Fraction of the Drop window held fully black before the cascade begins — the punctuation of the impact. Tune on the readout.</summary>
    private const float BlackHold = 0.12f;

    /// <summary>Reignition snap width (in cascade-progress units) for each class: smaller = harder staccato pop as each orientation family lights; larger = a softer roll. Tune on the readout.</summary>
    private const float ClassSnapWidth = 0.08f;

    /// <summary>Minimum sweep speed re-rolled on activation and each new Grid.</summary>
    private const float MinSpeed = 0.15f;

    /// <summary>Maximum sweep speed re-rolled on activation and each new Grid.</summary>
    private const float MaxSpeed = 0.4f;

    /// <summary>Directional-shading depth at Low energy: the dimmest orientation drops this far below full (so its floor is 1 - this). Kept shallow so calm sections stay subtle. Tune on the readout.</summary>
    private const float ShadeDepthLow = 0.12f;

    /// <summary>Directional-shading depth at High energy: deeper contrast so intense sections read the ten families more strongly, without ever going as dark as the Drop. Tune on the readout.</summary>
    private const float ShadeDepthHigh = 0.4f;

    /// <summary>Energy level assumed in Standalone (no beat clock, so <see cref="BeatManager.Energy"/> is null): 0.5 = Mid, a steady moderate shading depth. Tune on the readout.</summary>
    private const float StandaloneEnergy = 0.5f;

    /// <summary>Smoothing rate (per second) easing the shading depth between energy tiers, so a Low/Mid/High change ramps over ~0.5s instead of snapping. Tune on the readout.</summary>
    private const float EnergySmoothing = 2f;

    /// <summary>Smoothed wavefront position (0..1) driving the hue collapse; fast-attack, slow-release toward Fill progress (or the anticipation primer beforehand), plus per-beat kicks.</summary>
    private float fillEnv;

    /// <summary>True once the current on-beat gate has already kicked the wavefront, so each beat kicks only once. Cleared when the gate closes.</summary>
    private bool beatKicked;

    /// <summary>True while a Drop cascade is playing out (from the blackout through the last class reigniting). Off means the wall renders at normal shaded brightness.</summary>
    private bool dropActive;

    /// <summary>Seconds elapsed into the current Drop cascade.</summary>
    private float dropElapsed;

    /// <summary>Length of the current Drop cascade in seconds (BPM-derived, or <see cref="DropFallbackSeconds"/>).</summary>
    private float dropSeconds;

    /// <summary>Latest cascade progress (0 at blackout, 1 when all classes are lit), also shown on the debug readout.</summary>
    private float dropProgress;

    /// <summary>True once the current Drop has already fired, so it fires only once per Drop (on the first Grid downbeat inside it). Cleared when the Drop ends.</summary>
    private bool dropFlashed;

    /// <summary>Each tile's raw angle-hue (pre-sweep, pre-beat), cached once since <see cref="Penrose.TileData.tileangle"/> never changes.</summary>
    private float[] rawHue;

    /// <summary>Per tile, the shortest signed hue delta (in [-0.5, 0.5)) from <see cref="rawHue"/> toward <see cref="meanHue"/>, cached once.</summary>
    private float[] hueDelta;

    /// <summary>Per tile, the cascade-progress point (0..~0.9) at which its orientation class reignites during a Drop — its class index / <see cref="OrientationClasses"/>. Cached once.</summary>
    private float[] classReveal;

    /// <summary>Per tile, the wavefront envelope value at which this tile's collapse begins, cached once from its rank (ascending hue-distance from the mean).</summary>
    private float[] frontStart;

    /// <summary>Per tile, its folded orientation in [0,1) (tileangle mod 180° / 180°), cached once. Drives the directional shading; wraps smoothly so same-facing tiles (0° ≡ 180°) shade identically.</summary>
    private float[] orient01;

    /// <summary>Circular mean of every tile's raw angle-hue: the color the Fill wavefront collapses toward.</summary>
    private float meanHue;

    /// <summary>Direction (radians) the shading gradient is "lit" from; re-rolled each Grid so the bright/shadowed sides of the orientation field shift.</summary>
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
        (dropActive ? $"\nDROP {dropProgress:0.00}" : "");

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
        frontStart = new float[total];
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
            frontStart[order[rank]] = normalizedRank * (1f - FrontSoftness);
        }
    }

    /// <summary>
    /// Initializes per-activation random state before this effect starts drawing.
    /// </summary>
    public override void OnStart()
    {
        base.OnStart();
        Reroll();
        fillEnv = 0f;
        beatKicked = false;
        dropActive = false;
        dropElapsed = 0f;
        dropSeconds = DropFallbackSeconds;
        dropProgress = 0f;
        dropFlashed = false;
        smoothedEnergy = StandaloneEnergy;
        controller.debugText.text = "Angles";
        buffer.Clear();
    }

    /// <summary>
    /// Re-rolls the per-activation look: sweep speed, rhythmic variant, and shading light direction. Called
    /// once at activation and again on each new Grid, so the look takes a fresh character every 16 beats.
    /// </summary>
    private void Reroll()
    {
        speed = Random.Range(MinSpeed, MaxSpeed);
        beatVariant = beatManager.GetRandomVariantChill();
        lightPhase = Random.Range(0f, Mathf.PI * 2f);
    }

    /// <summary>
    /// On each new Grid the sweep takes a fresh speed/variant. If that Grid downbeat lands inside a Drop and
    /// this Drop has not fired yet, also starts the one-shot blackout-and-reignite cascade — i.e. beat one
    /// of the Grid during the Drop.
    /// </summary>
    protected override void OnNewGrid()
    {
        Reroll();
        if (beatManager.Drop is { inProgress: true } && !dropFlashed)
        {
            TriggerDrop();
            dropFlashed = true;
        }
    }

    /// <summary>Starts the Drop cascade: blacks out now and reignites through all orientation classes over <see cref="DropBeats"/> beats of the current tempo.</summary>
    private void TriggerDrop()
    {
        dropSeconds = beatManager.Bpm is { } bpm && bpm > 0f
            ? 60f / bpm * DropBeats
            : DropFallbackSeconds;
        dropElapsed = 0f;
        dropProgress = 0f;
        dropActive = true;
    }

    /// <summary>
    /// Reserved deactivation hook. Controller does not currently call this.
    /// </summary>
    public override void OnEnd() { }

    /// <summary>
    /// Exponentially eases a value toward a target at a frame-rate-independent rate.
    /// </summary>
    private static float SmoothToward(float current, float target, float rate, float deltaTime) =>
        Mathf.Lerp(current, target, 1f - Mathf.Exp(-rate * deltaTime));

    /// <summary>
    /// Updates the Fill wavefront from Fill progress, anticipation, and once-per-beat kicks.
    /// </summary>
    private void UpdateFillEnvelope()
    {
        PhraseEventInfo? fill = beatManager.Fill;
        bool filling = fill is { inProgress: true };
        float anticipation = !filling ? Mathf.Clamp01(fill?.anticipation ?? 0f) : 0f;
        float primer = Mathf.Pow(anticipation, AnticipationCurvePower) * AnticipationPrimerCap;
        float fillTarget = filling ? Mathf.Clamp01(fill.Value.progress ?? 0f) : primer;
        float fillRate = fillTarget > fillEnv ? FillAttack : FillRelease;
        fillEnv = SmoothToward(fillEnv, fillTarget, fillRate, effectDelta);

        if (filling && beatManager.IsBeatTriggered(beatVariant))
        {
            if (!beatKicked)
            {
                fillEnv = Mathf.Min(1f, fillEnv + BeatKick);
                beatKicked = true;
            }
        }
        else
        {
            beatKicked = false;
        }
    }

    /// <summary>
    /// Advances the active Drop cascade and returns the current orientation-class front position.
    /// </summary>
    private float UpdateDropCascade()
    {
        if (beatManager.Drop is not { inProgress: true })
        {
            dropFlashed = false;
        }

        if (!dropActive)
        {
            return 1f;
        }

        dropElapsed += effectDelta;
        dropProgress = dropSeconds > 0f ? Mathf.Clamp01(dropElapsed / dropSeconds) : 1f;
        float cascade = Mathf.InverseLerp(BlackHold, 1f, dropProgress);
        if (dropProgress >= 1f)
        {
            dropActive = false;
        }

        return cascade;
    }

    /// <summary>
    /// Updates musical-energy smoothing and returns the current directional-shading depth.
    /// </summary>
    private float UpdateShadeDepth()
    {
        float energyTarget = beatManager.Energy is { } energy ? energy.normalized : StandaloneEnergy;
        smoothedEnergy = SmoothToward(smoothedEnergy, energyTarget, EnergySmoothing, effectDelta);
        return Mathf.Lerp(ShadeDepthLow, ShadeDepthHigh, smoothedEnergy);
    }

    /// <summary>
    /// Renders one frame into this effect's 900-color buffer.
    /// </summary>
    public override void Draw()
    {
        // Beat pulse narrows/widens the angle-to-hue mapping without changing the underlying tile-angle pattern.
        float beatAngle = beatManager.GetBeatBrightness(beatVariant, 1.0f, 0.9f, beatEnable);
        UpdateFillEnvelope();
        float cascade = UpdateDropCascade();
        float shadeDepth = UpdateShadeDepth();

        for (int i = 0; i < buffer.Length; i++)
        {
            float collapse = Mathf.InverseLerp(frontStart[i], frontStart[i] + FrontSoftness, fillEnv);
            float angle = rawHue[i] + (hueDelta[i] * collapse) + (effectTime * speed);

            // Directional shading: same-facing tiles (0° ≡ 180°) shade identically, giving the angle
            // families brightness definition on top of hue.
            float align = 0.5f + (0.5f * Mathf.Cos((orient01[i] * Mathf.PI * 2f) - lightPhase));
            float shade = Mathf.Lerp(1f - shadeDepth, 1f, align);

            // Drop reignition: outside a Drop every tile sits at its shaded brightness; during a Drop,
            // each orientation class snaps up as the cascade reaches its reveal point.
            float lit = dropActive
                ? Mathf.Clamp01((cascade - classReveal[i]) / ClassSnapWidth)
                : 1f;
            float value = Mathf.Lerp(DarkFloor, shade, lit);

            buffer[i] = Color.HSVToRGB(Mathf.Repeat(angle + beatAngle, 1f), 1f, value);
        }
    }
}
