using UnityEngine;
using Random = UnityEngine.Random;


/// <summary>
/// Renders a direct tile-space tunnel from radius, density, and time.
/// </summary>
/// <remarks>
/// FILL: the tunnel rushes (scroll accelerates) and zooms (radial bands tighten) as the Fill builds,
/// both eased off a smoothed <see cref="BeatManager.Fill"/> progress so they ramp in and release cleanly.
/// DROP: on the first Grid downbeat inside a Drop the tunnel slams once — a hard reverse warp (scroll lunges
/// inward, the opposite of the Fill's forward build) plus a deep zoom — then decays over ~2 bars.
/// </remarks>
public class Tunnel : EffectBase
{
    /// <summary>The tunnel intensifies its rush/zoom motion for a Fill, and slams a reverse warp for a Drop.</summary>
    public override Repertoire Repertoire => Repertoire.HandlesFill | Repertoire.HandlesDrop;


    private float density;
    private float speed;
    private float mix;

    /// <summary>Extra scroll-rate multiple at full Fill: the color scroll rushes this much faster at the build's peak. Tune on the readout.</summary>
    private const float FillRush = 5f;

    /// <summary>Extra ring-compression multiple at full Fill: the radial bands tighten (zoom in) this much at the build's peak. Tune on the readout.</summary>
    private const float FillZoom = 3f;

    /// <summary>Fast attack rate (per second) so even a one-beat Fill slams to full drama instead of barely ramping.</summary>
    private const float FillAttack = 22f;

    /// <summary>Slower release rate (per second) so the rush/zoom tail lingers past the Fill instead of snapping back.</summary>
    private const float FillRelease = 5f;

    /// <summary>Smoothed Fill amount (0..1) driving rush and zoom; fast-attack, slow-release toward Fill.progress so it slams in and eases out without popping.</summary>
    private float fillEnv;

    /// <summary>Integrated extra scroll phase from the Fill rush, kept in [0,1). Integrating the rate avoids the phase jump that scaling absolute effectTime would cause.</summary>
    private float fillScroll;

    /// <summary>Floor of the beat brightness pulse: higher = shallower pulse (less beat effect on brightness). 1 = no pulse. Tune on the readout.</summary>
    private const float BeatBrightnessFloor = 0.75f;

    /// <summary>Beats per bar used to derive the Drop decay length from BPM.</summary>
    private const int BeatsPerBar = 4;

    /// <summary>Drop decay length in bars: the warp slam eases from full to nothing over this many bars.</summary>
    private const float DropBars = 2f;

    /// <summary>Drop decay length used when no BPM is available (≈2 bars at 120 BPM).</summary>
    private const float DropFallbackSeconds = 4f;

    /// <summary>Reverse scroll-rate multiple at the Drop's peak: the tunnel warps inward this much faster than its base scroll. Bigger than <see cref="FillRush"/> so the Drop out-slams a Fill. Tune on the readout.</summary>
    private const float DropRush = 10f;

    /// <summary>Extra ring-compression multiple at the Drop's peak, stacked on any Fill zoom. Tune on the readout.</summary>
    private const float DropZoom = 6f;

    /// <summary>Drop slam amount (1 at the downbeat, SmoothStep-eased to 0 over <see cref="DropBars"/>); drives the reverse warp and zoom punch.</summary>
    private float dropEnv;

    /// <summary>Seconds elapsed into the current Drop slam.</summary>
    private float dropElapsed;

    /// <summary>Length of the current Drop slam in seconds (BPM-derived, or <see cref="DropFallbackSeconds"/>).</summary>
    private float dropSeconds;

    /// <summary>Integrated reverse scroll phase from the Drop warp, kept in [0,1). Like <see cref="fillScroll"/> but pulls the phase the other way.</summary>
    private float dropScroll;

    /// <summary>True once the current Drop has already slammed, so it fires only once per Drop (on the first Grid downbeat inside it). Cleared when the Drop ends.</summary>
    private bool dropFlashed;

    /// <summary>
    /// Initializes per-activation random state before this effect starts drawing.
    /// </summary>
    public override void OnStart()
    {
        base.OnStart();
        Reroll();
        fillEnv = 0f;
        fillScroll = 0f;
        dropEnv = 0f;
        dropElapsed = 0f;
        dropSeconds = DropFallbackSeconds;
        dropScroll = 0f;
        dropFlashed = false;
        buffer.Clear();
    }

    /// <summary>
    /// Re-rolls the per-activation look: band density, scroll speed, radial mix, and rhythmic variant. Called
    /// once at activation and again on each new Grid, so the tunnel takes a fresh form in step with the music.
    /// </summary>
    private void Reroll()
    {
        density = Random.Range(0.0004f, 0.003f);
        speed = Random.Range(0.1f, 1f);
        mix = Random.Range(0.01f, 0.2f);
        beatVariant = beatManager.GetRandomVariantChill();
    }

    /// <summary>
    /// On each new Grid the tunnel takes a fresh form. If that Grid downbeat lands inside a Drop and this Drop
    /// has not slammed yet, it also fires the one-shot warp slam — i.e. beat one of the Grid during the Drop.
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

    /// <summary>Arms the Drop slam: full envelope now, eased to nothing over <see cref="DropBars"/> bars of the current tempo.</summary>
    private void TriggerDrop()
    {
        dropSeconds = beatManager.Bpm is { } bpm && bpm > 0f
            ? (60f / bpm) * BeatsPerBar * DropBars
            : DropFallbackSeconds;
        dropElapsed = 0f;
        dropEnv = 1f;
    }

    /// <summary>
    /// Reserved deactivation hook. Controller does not currently call this.
    /// </summary>
    public override void OnEnd() { }

    /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
    public override string DebugText()
    {
        return $"Density: {density}\n" +
        $"Speed: {speed}\n" +
        $"Mix: {mix}\n" +
        (fillEnv > 0.01f ? $"FILL {fillEnv:0.00}\n" : "") +
        (dropEnv > 0.01f ? $"DROP {dropEnv:0.00}\n" : "");
    }
    public override void Init()
    {
        base.Init();
    }

    /// <summary>
    /// Renders one frame of radial tunnel bands directly into the tile buffer.
    /// </summary>
    public override void Draw()
    {
        // Beat pulse scales tunnel brightness without changing the tunnel phase.
        float beatBrightness = beatManager.GetBeatBrightness(beatVariant, 1.0f, BeatBrightnessFloor, beatEnable);

        // Fill build: ease a smoothed envelope toward Fill.progress, then drive rush (extra integrated scroll
        // rate) and zoom (radial band compression). Integrating the rush keeps it from jumping the phase the
        // way scaling absolute effectTime would. Fast attack slams it to full even on a one-beat fill; slower
        // release lets the rush/zoom tail off past the fill instead of snapping back.
        PhraseEventInfo? fill = beatManager.Fill;
        float fillTarget = fill is { inProgress: true } ? Mathf.Clamp01(fill.Value.progress ?? 0f) : 0f;
        float fillRate = fillTarget > fillEnv ? FillAttack : FillRelease;
        fillEnv = Mathf.Lerp(fillEnv, fillTarget, 1f - Mathf.Exp(-fillRate * effectDelta));
        fillScroll = Mathf.Repeat(fillScroll + (speed * FillRush * fillEnv * effectDelta), 1f);

        // Drop slam: fired on the first Grid downbeat inside a Drop (see OnNewGrid), it snaps to full and decays
        // over ~2 bars. It warps the scroll the OTHER way (inward, the inverse of the Fill's forward rush) and
        // punches the zoom deeper. Re-arm the once-per-Drop guard whenever no Drop is in progress.
        if (!(beatManager.Drop is { inProgress: true }))
        {
            dropFlashed = false;
        }
        if (dropEnv > 0f)
        {
            dropElapsed += effectDelta;
            float u = dropSeconds > 0f ? Mathf.Clamp01(dropElapsed / dropSeconds) : 1f;
            dropEnv = 1f - Mathf.SmoothStep(0f, 1f, u);
        }
        dropScroll = Mathf.Repeat(dropScroll - (speed * DropRush * dropEnv * effectDelta), 1f);

        float zoom = 1f + (FillZoom * fillEnv) + (DropZoom * dropEnv);

        for (int i = 0; i < Penrose.Total; i++)
        {
            float x = Mathf.Abs(tiles[i].center.x * 0.03f);
            float y = Mathf.Abs(tiles[i].center.y * 0.03f);
            float distance = Mathf.Sqrt((x * x) + (y * y));
            float phase = (i * density + (effectTime * speed) + fillScroll + dropScroll + (distance * mix * zoom)) % 1f;
            buffer[i] = Color.HSVToRGB(phase, 1f, 1f) * beatBrightness;
        }
    }
}