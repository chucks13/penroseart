using UnityEngine;

/// <summary>
/// Creates two or three child effects and additively mixes their buffers.
/// </summary>
public class RandomEffectsMixer : MixerBase
{
    /// <summary>RandomEffectsMixer's shifting mix accents Fills and suits Mid/High-energy sections.</summary>
    public override Repertoire Repertoire =>
        Repertoire.HandlesFill | Repertoire.EnergyMid | Repertoire.EnergyHigh;


    private EffectBase[] effects;
    private int total;
    private float percent;

    /// <summary>
    /// Returns text for the Controller debug display while this effect is active.
    /// </summary>
    public override string DebugText()
    {
        var debugText = string.Empty;
        for (var i = 0; i < total; i++)
        {
            debugText += (i < total - 1) ? $"{effects[i].Name}, " : $"{effects[i].Name}";
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

    /// <summary>Creates and starts independent child Effects for this activation.</summary>
    public override void OnStart()
    {
        effects = new EffectBase[Random.Range(2, 4)];
        total = effects.Length;

        var debugText = string.Empty;
        for (var i = 0; i < total; i++)
        {
            effects[i] = GetRandomEffect();
            effects[i].RandomizeTime();
            effects[i].Init();
            effects[i].OnStart();
            // Passive: allow children to use the beat independently
            debugText += (i < total - 1) ? $"{effects[i].Name}, " : $"{effects[i].Name}";
        }

        controller.debugText.text = debugText;
        percent = (1f / total) * 2f;

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

        for (int i = 0; i < total; i++)
        {
            effects[i].UpdateTime();
            effects[i].Draw();
        }

        for (int i = 0; i < buffer.Length; i++)
        {

            float r = 0f, g = 0f, b = 0f;
            for (int j = 0; j < total; j++)
            {
                r += effects[j].buffer[i].r * percent;
                g += effects[j].buffer[i].g * percent;
                b += effects[j].buffer[i].b * percent;
            }

            buffer[i] = new Color(r, g, b, 1f);
        }
    }

}
