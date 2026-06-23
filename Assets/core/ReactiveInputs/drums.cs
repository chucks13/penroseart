
using System;
using UnityEngine;
using System.Collections;
using Random = UnityEngine.Random;

/// <summary>
/// Drum-hit and ring overlay system driven by local test keys, OSC-style messages,
/// and UDP trigger packets.
/// </summary>
/// <remarks>
/// This is not the global beat clock; <see cref="BeatManager"/> owns beat timing.
/// The drums class is a separate visual overlay that draws expanding hit/ring
/// shapes on top of the current Penrose buffer after the active effect or
/// transition has rendered.
/// </remarks>
[Serializable]
public class drums
{
    /// <summary>Active Penrose model used for tile positions.</summary>
    protected Penrose penrose;

    /// <summary>Controller singleton that owns this overlay.</summary>
    protected Controller controller;

    /// <summary>Cached tile metadata used to locate overlay centers.</summary>
    protected Penrose.TileData[] tiles;

    /// <summary>Runtime settings placeholder for future drum controls.</summary>
    private Settings setting;

    /// <summary>UDP listener for simple OpenPixel-style drum trigger packets.</summary>
    private UDPReceive listenerOpenPixel;

    /// <summary>Current radius of each filled drum hit overlay.</summary>
    private float[] hits;

    /// <summary>Current lifetime of each ring overlay.</summary>
    private float[] rings;

    /// <summary>Per-hit decay speed used to accelerate shrinking hit circles.</summary>
    private float[] speed;

    /// <summary>Local overlay clock, randomized on startup like effects.</summary>
    public float effectTime;

    /// <summary>Frame delta used by hit/ring decay.</summary>
    public float effectDelta;

    /// <summary>
    /// Five fixed overlay centers as x/y pairs: right-lower, right-upper,
    /// center, left-upper, left-lower.
    /// </summary>
    public float[] points = { 10f, -5f, 10f, 5f, 0f, 0f, -10f, 5f, -10f, -5f };

    /// <summary>Display colors for the five drum hit pads.</summary>
    private Color[] colors = { Color.green, Color.yellow, Color.cyan, new Color(0xff, 0xa5, 0x00), Color.red };//};

    /// <summary>Initial hit diameter used by UDP trigger packets.</summary>
    public float diameter = 8;

    /// <summary>Acceleration factor for shrinking hit overlays.</summary>
    public float shrink = 128f;
    [HideInInspector]

    /// <summary>Debug label for this overlay.</summary>
    public string DebugText() => "drums";

    /// <summary>
    /// Binds Controller/Penrose state, starts the UDP trigger listener on port
    /// 8500, and allocates five hit/ring state slots.
    /// </summary>
    public void Init()
    {
        controller = Controller.Instance;
        penrose = controller.penrose;
        tiles = penrose.Tiles;
        setting = new Settings();
        listenerOpenPixel = new UDPReceive(8500, handleOpenPixel);
        hits = new float[5];
        rings = new float[5];
        speed = new float[5];
    }

    /// <summary>
    /// Reserved activation hook. Controller currently creates the drum overlay
    /// once and does not use an effect-style OnStart lifecycle for it.
    /// </summary>
    public void OnStart()
    {
    }

    /// <summary>Reserved deactivation hook; not called by Controller.</summary>
    public void OnEnd() { }

    /// <summary>
    /// Draws active hit and ring overlays into the destination buffer in-place.
    /// </summary>
    /// <remarks>
    /// Filled hits paint a colored annulus with a black center. Rings paint a
    /// moving annulus with a color derived from the ring index. After drawing,
    /// hit radii shrink and ring lifetimes decay using <see cref="effectDelta"/>.
    /// </remarks>
    public void Draw(Color[] destBuffer)
    {

        for (int i = 0; i < destBuffer.Length; i++)
        {

            float x = tiles[i].center.x;
            float y = tiles[i].center.y;
            int k = 0;
            for (int j = 0; j < 5; j++)
            {
                float dx = points[k++] - x;
                float dy = points[k++] - y;
                dx = dx * dx;
                dy = dy * dy;
                float r2 = dx + dy;       // radius squared
                float v = hits[j];      // hit radius
                float v2 = v / 2;
                if (r2 < (v * v))
                {
                    if (r2 < (v2 * v2))
                        destBuffer[i] = Color.black;
                    else
                        destBuffer[i] = colors[j];
                }
                if (rings[j] > 0)
                {
                    v = 5f - rings[j];      // ring progress
                    v *= 10;
                    v2 = v / 2;
                    int c2 = j + 1;
                    int r = ((c2 & 1) != 0) ? 255 : 0;
                    int g = ((c2 & 2) != 0) ? 255 : 0;
                    int b = ((c2 & 4) != 0) ? 255 : 0;
                    Color ring = new Color32((byte)r, (byte)g, (byte)b, 0);
                    if (r2 < (v * v))
                    {
                        if (r2 > (v2 * v2))
                            destBuffer[i] = ring;
                    }
                }
            }
        }
        for (int j = 0; j < 5; j++)
        {
            if (hits[j] > 0)
            {
                // decay accelerates
                speed[j] += shrink * effectDelta * effectDelta;
                hits[j] -= speed[j];
            }
            if (hits[j] < 0)
            {
                hits[j] = 0;
                speed[j] = 0;
            }
            if (rings[j] > 0)
            {
                rings[j] -= 12f * effectDelta; // Equivalent to 0.2 per frame at 60fps
                if (rings[j] < 0)
                    rings[j] = 0f;

            }


        }

    }

    /// <summary>Advances the overlay clock and frame delta.</summary>
    public void Update()
    {
        UpdateTime();
    }

    /// <summary>Seeds the overlay clock with a random phase.</summary>
    public void RandomizeTime()
    {
        effectTime = Random.Range(0f, 14400f);
    }

    /// <summary>Advances the overlay clock from Unity frame time.</summary>
    public void UpdateTime()
    {
        effectDelta = Time.deltaTime;
        effectTime += effectDelta;
    }

    /// <summary>
    /// Runtime controls for the drum overlay colors, fade, and hit/ring behavior.
    /// Currently a placeholder for future settings.
    /// </summary>
    public class Settings
    {


    }

    /// <summary>
    /// Starts or refreshes one of the five filled hit overlays.
    /// </summary>
    /// <param name="i">Zero-based hit index.</param>
    /// <param name="p">Strength multiplier. Current code maps 1.0 to radius 5.</param>
    public void hit(int i, float p)
    {
        if ((i >= 0) && (i < 5))
        {
            hits[i] = p * 5f;
            speed[i] = 0f;
        }
    }

    /// <summary>
    /// Starts or refreshes one of the five ring overlays.
    /// </summary>
    /// <param name="i">One-based ring index from OSC/control surfaces.</param>
    /// <param name="p">Strength argument retained for protocol shape; currently unused.</param>
    public void ring(int i, float p)
    {
        if ((i > 0) && (i < 6))
        {
            rings[i - 1] = 5f;
        }
    }

    // current drum packet byte packet[9] = {0, 2, 0, 5, 0, 0, 0, 0, 0};

    /// <summary>
    /// Handles UDP trigger packets. Bytes 4-8 correspond to the five pads;
    /// values greater than 20 trigger the matching filled hit overlay.
    /// </summary>
    void handleOpenPixel(byte[] data)
    {
        for (int i = 0; i < 5; i++)
        {
            if (data[i + 4] > 20)
                hits[i] = diameter;
        }
    }

    /// <summary>
    /// Handles OSC page-3 drum controls and appends any replies to <paramref name="oms"/>.
    /// </summary>
    private void OSCpage3(OscMessage om, ArrayList oms)
    {
        if (om.address == "/disk")      // test the drums
        {
            ring(om.GetInt(0), 1f);
        }

        if (om.address.StartsWith("/3/toggle"))      // test the drums
        {
            if (om.GetInt(0) == 1)
            {
                int pad = int.Parse(om.address.Substring(9));
                hit(pad - 1, 1f);

            }
        }
        if (om.address.StartsWith("/3/rotary"))
        {

        }
        if (om.address == "/ping")
        {

        }
    }

    /// <summary>
    /// OSC entry point called by Controller after it handles the shared/root OSC controls.
    /// </summary>
    public void OSCHandler(OscMessage om, ArrayList oms)
    {
        OSCpage3(om, oms);
    }

}

// starburst, 0,8,11,16,20
/*
 *    20             16
 *            0
 *        11      8
 *        
 *        loops:            *47  (big loop)
 *        0,1
 *        2,
 *        3,4,5,6,7
 *       
 *        26,48        24,49
 *        
 */
