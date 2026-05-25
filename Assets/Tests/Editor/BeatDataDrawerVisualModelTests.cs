#nullable enable

using System;
using System.Reflection;
using NUnit.Framework;

public sealed class BeatDataDrawerVisualModelTests
{
    [Test]
    public void BuildBeatDotGlyphsUsesRaveSystemFilledDotsForMusicalBeatPosition()
    {
        var beatData = new BeatData
        {
            active = true,
            beatInBar = 3,
            onBeats = new[] { false, false, true, false },
        };

        var glyphs = InvokeStringArrayHelper("BuildBeatDotGlyphs", beatData);

        Assert.That(glyphs, Is.EqualTo(new[] { "●", "●", "●", "○" }));
    }

    [Test]
    public void BuildBeatDotGlyphsUsesPlaceholderWhenBeatDataIsUnavailable()
    {
        var beatData = new BeatData
        {
            active = false,
            beatInBar = -1,
            onBeats = new[] { true, true, true, true },
        };

        var glyphs = InvokeStringArrayHelper("BuildBeatDotGlyphs", beatData);

        Assert.That(glyphs, Is.EqualTo(new[] { "○", "○", "○", "○" }));
    }

    [Test]
    public void GetClampedEighthPulseUsesStrongerOnBeatOrOffBeatPulse()
    {
        var beatData = new BeatData
        {
            beatPulse = 0.25f,
            offBeatPulse = 1.25f,
        };

        var pulse = InvokeFloatHelper("GetClampedEighthPulse", beatData);

        Assert.That(pulse, Is.EqualTo(1f));
    }

    [Test]
    public void BuildCountdownChipLabelsUsesExplicitBeatNames()
    {
        var labels = InvokeStringArrayHelper("BuildCountdownChipLabels");

        Assert.That(labels, Is.EqualTo(new[] { "NEXT BEAT", "ON BEAT", "NEXT OFF BEAT", "OFF BEAT" }));
    }

    private static string[] InvokeStringArrayHelper(string name, BeatData beatData)
    {
        var method = GetHelper(name, typeof(BeatData));
        return (string[])method.Invoke(null, new object[] { beatData });
    }

    private static string[] InvokeStringArrayHelper(string name)
    {
        var method = GetHelper(name);
        return (string[])method.Invoke(null, Array.Empty<object>());
    }

    private static float InvokeFloatHelper(string name, BeatData beatData)
    {
        var method = GetHelper(name, typeof(BeatData));
        return (float)method.Invoke(null, new object[] { beatData });
    }

    private static MethodInfo GetHelper(string name, params Type[] parameterTypes)
    {
        var method = typeof(BeatDataDrawer).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static, null, parameterTypes, null);
        Assert.That(method, Is.Not.Null, $"BeatDataDrawer should expose non-public static helper '{name}' for its visual model.");
        return method!;
    }
}
