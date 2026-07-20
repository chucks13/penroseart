using UnityEngine;

/// <summary>
/// Combines two child effects using a Perlin noise mask and colored border band.
/// </summary>
public class NoiseMixer : MixerBase
{
    /// <summary>NoiseMixer's blended noise suits Low/Mid-energy sections.</summary>
    public override Repertoire Repertoire =>
        Repertoire.HandlesDrop | Repertoire.EnergyLow | Repertoire.EnergyMid;


    private EffectBase[] effects;
    private Color border;
    private int distortionMode; // 0: time, 1: width

    /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
    public override string DebugText()
    {
        var debugText = string.Empty;
        for (var i = 0; i < 2; i++)
        {
            debugText += (i < 2 - 1) ? $"{effects[i].Name}, " : $"{effects[i].Name}";
        }

        return debugText;
    }

    /// <summary>
    /// Performs one-time setup after reflection creates this effect instance.
    /// </summary>
    public override void Init()
    {
        base.Init();
    }

    /// <summary>
    /// Initializes per-activation random state before this effect starts drawing.
    /// </summary>
    public override void OnStart()
    {
        waveform = waveforms.Random();
        effects = new EffectBase[2];

        var debugText = string.Empty;
        for (var i = 0; i < 2; i++)
        {
            effects[i] = GetRandomEffect();
            effects[i].RandomizeTime();
            effects[i].Init();
            effects[i].OnStart();
            // NoiseMixer owns the rhythmic shape of the composite, so child Waveforms are suppressed.
            effects[i].waveform = waveforms.None;
            debugText += (i < 2 - 1) ? $"{effects[i].Name}, " : $"{effects[i].Name}";
            border = Color.HSVToRGB(Random.value, 1, 1);
        }
        distortionMode = Random.Range(0, 2);

        controller.debugText.text = debugText;
    }

    /// <summary>
    /// Reserved deactivation hook. Controller does not currently call this.
    /// </summary>
    public override void OnEnd() { }

    /// <summary>
    /// Renders one frame into this effect's 900-color buffer.
    /// </summary>
    public override void Draw()
    {
        for (int i = 0; i < 2; i++)
        {
            effects[i].UpdateTime();
            // Reassert suppression after UpdateTime because the child may acquire a new Waveform on a Grid wrap.
            effects[i].waveform = waveforms.None;
            effects[i].Draw();
        }
        float rhythm = waveform.Envelope;

        // we are going to hack the local time with our own local delta
        effectTime -= effectDelta;            // remove the currelt delta
        // calculate our drop modified time delta
        float localDelta = effectDelta;
        var beatsTilDrop = (float?)beatManager.Drop.BeatsUntil ?? 8f;
        if (beatsTilDrop < 8)                   // slow down leading to drop
        {
            localDelta *= ((float)beatsTilDrop) / 8f;
        }

        if (beatManager.Drop.Active)
        {
            float rampDown = localDelta * beatManager.Drop.Decay(8).Remap(1f, 0f, 5f, localDelta);
            if (rampDown > localDelta)
                localDelta = rampDown;
        }
        // change the effect time by this updated delta
        effectTime += localDelta;


        float sampleTime = effectTime + (0.5f * rhythm);
        float width = 0.1f;
        if (distortionMode == 1)
            width = waveform.Lerp(0.1f, 0.25f);

        for (int i = 0; i < buffer.Length; i++)
        {
            float scale = 0.07f;
            float x = tiles[i].center.x * scale;
            float y = tiles[i].center.y * scale;
            float z = (distortionMode == 0) ? sampleTime : effectTime; // use local mixer time

            float n = Perlin.Noise(x, y, z);
            if (n > width)
                buffer[i] = effects[0].buffer[i];
            else if (n > -width)
                buffer[i] = border;
            else
                buffer[i] = effects[1].buffer[i];
        }
    }

}
