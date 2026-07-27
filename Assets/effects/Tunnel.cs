using UnityEngine;
using Random = UnityEngine.Random;


/// <summary>
/// Renders a direct tile-space tunnel from radius, density, and time.
/// </summary>
/// <remarks>
/// FILL: the tunnel rushes (scroll accelerates) and zooms (radial bands tighten) as the Fill builds,
/// both driven by <see cref="BeatManager.Fill"/> Build.
/// DROP: <see cref="BeatManager.Drop"/> Decay drives a hard reverse warp plus a deep zoom over two bars.
/// </remarks>
public class Tunnel : EffectBase
{
    /// <summary>The tunnel intensifies its rush/zoom motion for a Fill, and slams a reverse warp for a Drop;
    /// its driving motion suits Mid/High-energy sections.</summary>
    public override Repertoire Repertoire =>
        Repertoire.HandlesFill | Repertoire.HandlesDrop | Repertoire.EnergyMid | Repertoire.EnergyHigh;


    private float density;
    private float speed;
    private float mix;

    /// <summary>Scales tile center coordinates into the tunnel's radial-distance space; smaller = wider, more spread-out rings.</summary>
    private const float CenterScale = 0.03f;

    /// <summary>Extra scroll-rate multiple at full Fill: the color scroll rushes this much faster at the build's peak. Tune on the readout.</summary>
    private const float FillRush = 5f;

    /// <summary>Extra ring-compression multiple at full Fill: the radial bands tighten (zoom in) this much at the build's peak. Tune on the readout.</summary>
    private const float FillZoom = 3f;

    /// <summary>Fill Build amount driving rush and zoom.</summary>
    private float fillEnv;

    /// <summary>Integrated extra scroll phase from the Fill rush, kept in [0,1). Integrating the rate avoids the phase jump that scaling absolute effectTime would cause.</summary>
    private float fillScroll;

    /// <summary>Floor of the beat brightness pulse: higher = shallower pulse (less beat effect on brightness). 1 = no pulse. Tune on the readout.</summary>
    private const float BeatBrightnessFloor = 0.75f;

    /// <summary>Drop decay length in bars: the warp slam falls linearly from full to nothing over this many bars.</summary>
    private const int DropBars = 2;

    /// <summary>Reverse scroll-rate multiple at the Drop's peak: the tunnel warps inward this much faster than its base scroll. Bigger than <see cref="FillRush"/> so the Drop out-slams a Fill. Tune on the readout.</summary>
    private const float DropRush = 10f;

    /// <summary>Extra ring-compression multiple at the Drop's peak, stacked on any Fill zoom. Tune on the readout.</summary>
    private const float DropZoom = 6f;

    /// <summary>Drop Decay amount driving the reverse warp and zoom punch.</summary>
    private float dropEnv;

    /// <summary>Integrated reverse scroll phase from the Drop warp, kept in [0,1). Like <see cref="fillScroll"/> but pulls the phase the other way.</summary>
    private float dropScroll;

    /// <summary>
    /// Initializes per-activation random state before this effect starts drawing.
    /// </summary>
    public override void OnStart()
    {
        Reroll();
        fillEnv = 0f;
        fillScroll = 0f;
        dropEnv = 0f;
        dropScroll = 0f;
        buffer.Clear();
    }

    /// <summary>
    /// Re-rolls the per-activation look: band density, scroll speed, radial mix, and Waveform. Called
    /// once at activation and again on each new Grid, so the tunnel takes a fresh form in step with the music.
    /// </summary>
    private void Reroll()
    {
        density = Random.Range(0.0004f, 0.003f);
        speed = Random.Range(0.1f, 1f);
        mix = Random.Range(0.01f, 0.2f);
        waveform = waveforms.Random(Energy.Low, Energy.Mid);
    }

    /// <summary>
    /// On each new Grid the tunnel takes a fresh form.
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

    /// <summary>
    /// Reads Fill Build and integrates an extra scroll rate from that value. Integrating the rush preserves tunnel phase;
    /// scaling absolute effectTime would make the bands jump when a Fill starts or ends.
    /// </summary>
    private void UpdateFillEnvelope()
    {
        fillEnv = beatManager.Fill.In.Build();
        fillScroll = Mathf.Repeat(fillScroll + (speed * FillRush * fillEnv * effectDelta), 1f);
    }

    /// <summary>
    /// Reads the two-bar Drop Decay and integrates reverse scroll. The reverse phase is intentionally the inverse of
    /// the Fill rush, so the Drop reads as an inward warp instead of a stronger version of the build.
    /// </summary>
    private void UpdateDropSlam()
    {
        dropEnv = beatManager.Drop.In.Decay(DropBars * 4);
        dropScroll = Mathf.Repeat(dropScroll - (speed * DropRush * dropEnv * effectDelta), 1f);
    }

    /// <summary>
    /// Renders one frame of radial tunnel bands directly into the tile buffer.
    /// </summary>
    public override void Draw()
    {
        // Beat pulse scales tunnel brightness without changing the tunnel phase.
        float beatBrightness = waveform.Lerp(BeatBrightnessFloor, 1f);
        UpdateFillEnvelope();
        UpdateDropSlam();

        float zoom = 1f + (FillZoom * fillEnv) + (DropZoom * dropEnv);

        for (int i = 0; i < Penrose.Total; i++)
        {
            float x = Mathf.Abs(tiles[i].center.x * CenterScale);
            float y = Mathf.Abs(tiles[i].center.y * CenterScale);
            float distance = Mathf.Sqrt((x * x) + (y * y));
            float phase = (i * density + (effectTime * speed) + fillScroll + dropScroll + (distance * mix * zoom)) % 1f;
            buffer[i] = Color.HSVToRGB(phase, 1f, 1f) * beatBrightness;
        }
    }
}
