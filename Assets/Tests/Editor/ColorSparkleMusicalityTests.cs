// Verifies the ColorSparkle musicality seams that carry state across frames.
using NUnit.Framework;
using UnityEngine;

/// <summary>Verifies ColorSparkle's Waveform turn, activation Palette transitions, and glint state.</summary>
public sealed class ColorSparkleMusicalityTests
{
    /// <summary>
    /// A qualifying multi-frame On Beat gate fires once on one absolute beat and can fire again
    /// after the Data Surface advances to the next beat.
    /// </summary>
    [Test]
    public void ColorSparkleGlintGateFiresOncePerBeat()
    {
        var effect = new ColorSparkle();

        Assert.That(effect.TryBeginGlintBeat(42, true, 0.75f, 0.5f), Is.True);
        Assert.That(effect.TryBeginGlintBeat(42, true, 0.75f, 0.5f), Is.False);
        Assert.That(effect.TryBeginGlintBeat(43, true, 0.75f, 0.5f), Is.True);
    }

    /// <summary>
    /// A sparkle's birth coordinate is home at both Waveform troughs and halfway around its
    /// Palette on the intervening beat, while its stored fade level controls luminance alone.
    /// </summary>
    [Test]
    public void ColorSparklePaletteStateTurnsHomeToHalfTurnToHome()
    {
        var sparkle = ColorSparkle.SparkleState.Palette(0.8f);

        Assert.That(sparkle.TurnedCoordinate(0f), Is.EqualTo(0.8f).Within(0.0001f));
        Assert.That(sparkle.TurnedCoordinate(0.5f), Is.EqualTo(0.3f).Within(0.0001f));
        Assert.That(sparkle.TurnedCoordinate(1f), Is.EqualTo(0.8f).Within(0.0001f));

        Color faded = sparkle.Advance(Color.red, Color.black, 0.5f);
        Assert.That(faded.r, Is.EqualTo(0.5f).Within(0.0001f));
        Assert.That(faded.g, Is.Zero.Within(0.0001f));
        Assert.That(faded.b, Is.Zero.Within(0.0001f));
    }

    /// <summary>
    /// A confetti activation creates the requested number of full-saturation, full-value colors
    /// ordered by hue so adjacent blended Palette reads stay saturated.
    /// </summary>
    [Test]
    public void ColorSparkleConfettiPaletteUsesSortedFullHsvColors()
    {
        Random.State priorRandomState = Random.state;
        try
        {
            Random.InitState(12345);
            const int paletteSize = 8;
            GPalette palette = ColorSparkle.CreateConfettiPalette(paletteSize);

            Assert.That(palette.length, Is.EqualTo(paletteSize));
            float previousHue = -1f;
            foreach (Color color in palette.values)
            {
                Color.RGBToHSV(color, out float hue, out float saturation, out float value);
                Assert.That(hue, Is.GreaterThanOrEqualTo(previousHue));
                Assert.That(saturation, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(value, Is.EqualTo(1f).Within(0.0001f));
                previousHue = hue;
            }
        }
        finally
        {
            Random.state = priorRandomState;
        }
    }

    /// <summary>
    /// A generated Palette can be the initial AnimPalette endpoint, then a supplied replacement
    /// starts the same transition contract used by the shared Palette.
    /// </summary>
    [Test]
    public void ColorSparkleGeneratedPaletteUsesAnimPaletteTransition()
    {
        var current = new GPalette(new[] { Color.red, Color.green });
        var next = new GPalette(new[] { Color.blue, Color.white });
        var palette = new AnimPalette(current);

        palette.Change(next);

        Assert.That(palette.CurrentPalette, Is.SameAs(current));
        Assert.That(palette.NextPalette, Is.SameAs(next));
        Assert.That(palette.IsTransitioning, Is.True);
        Assert.That(palette.TransitionProgress, Is.Zero.Within(0.0001f));
        Assert.That(palette.Revision, Is.EqualTo(1));
        Assert.That(palette.ReadCyclic(0f), Is.EqualTo(Color.red));
    }

    /// <summary>
    /// An active glint advances by its own seconds duration and keeps its birth hue even while the
    /// sparkle field is halfway through its Waveform turn.
    /// </summary>
    [Test]
    public void ColorSparkleGlintFadeUsesItsOwnClockAndSkipsTheTurn()
    {
        var effect = new ColorSparkle { buffer = new[] { Color.black } };
        effect.ResetSparklesAndGlints();
        effect.StartGlint(0, Color.white, 2f);

        effect.FadeFieldAndGlints(Color.black, 0f, 0.5f, true, 0.5f);

        Assert.That(effect.buffer[0].r, Is.EqualTo(0.75f).Within(0.0001f));
        Assert.That(effect.buffer[0].g, Is.EqualTo(0.75f).Within(0.0001f));
        Assert.That(effect.buffer[0].b, Is.EqualTo(0.75f).Within(0.0001f));
    }

    /// <summary>
    /// A fading glint rejects a field sparkle on its Tile, then releases the Tile when its own fade
    /// clock finishes.
    /// </summary>
    [Test]
    public void ColorSparkleGlintProtectsItsTileUntilFadeFinishes()
    {
        var effect = new ColorSparkle { buffer = new[] { Color.black } };
        effect.ResetSparklesAndGlints();
        effect.StartGlint(0, Color.red, 1f);

        Assert.That(
            effect.TryStartSparkle(0, ColorSparkle.SparkleState.Fixed(Color.green), 0.5f),
            Is.False);
        Assert.That(effect.buffer[0], Is.EqualTo(Color.red));

        effect.FadeFieldAndGlints(Color.black, 0f, 1f, true, 0.5f);

        Assert.That(
            effect.TryStartSparkle(0, ColorSparkle.SparkleState.Fixed(Color.green), 0.5f),
            Is.True);
        Assert.That(effect.buffer[0], Is.EqualTo(Color.green));
    }
}
