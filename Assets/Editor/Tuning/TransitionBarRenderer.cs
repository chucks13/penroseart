// IMGUI drawing for the always-on live A-to-B transition strip. Thin: SwitcherStatus
// owns the facts; this file only gives the current wall state a stable visual register.
#nullable enable

using UnityEditor;
using UnityEngine;

/// <summary>
/// Draws the current Effect or active A-to-B Transition without shifting the Live workspace layout.
/// </summary>
internal static class TransitionBarRenderer
{
    /// <summary>Fixed strip height reserved in every Switcher state.</summary>
    public const float StripHeight = 42f;

    /// <summary>Inset from the strip edge to both text and rail.</summary>
    private const float ContentInset = 6f;

    /// <summary>Height of the Effect and Transition identity row.</summary>
    private const float TextRowHeight = 18f;

    /// <summary>Gap separating the identity row from the progress rail.</summary>
    private const float RowGap = 4f;

    /// <summary>Height of the always-present progress rail.</summary>
    private const float RailHeight = 8f;

    // The tracker reserves saturated magenta, cyan, orange, green, and yellow for plan
    // identities. This strip stays achromatic: dark graphite structure and a bright
    // silver fill read as live state without borrowing any tracker meaning.
    /// <summary>Graphite border and idle progress track — the strip's structure.</summary>
    private static readonly Color StructureColor = new(0.24f, 0.25f, 0.27f);

    /// <summary>Near-black neutral field behind both rows.</summary>
    private static readonly Color BackgroundColor = new(0.10f, 0.11f, 0.13f);

    /// <summary>Bright neutral progress fill and identity text.</summary>
    private static readonly Color LiveColor = new(0.89f, 0.91f, 0.93f);

    /// <summary>Dim neutral text for the idle on-air state.</summary>
    private static readonly Color IdleColor = new(0.55f, 0.57f, 0.60f);

    /// <summary>Cached left-aligned identity style.</summary>
    private static GUIStyle? leftStyle;

    /// <summary>Cached right-aligned state style; its colour is set per draw.</summary>
    private static GUIStyle? rightStyle;

    /// <summary>Draws the fixed-height live state strip for a Switcher status snapshot.</summary>
    /// <param name="status">The stage snapshot to show; a not-ready snapshot still reserves the full strip.</param>
    public static void Draw(SwitcherStatus status)
    {
        EnsureStyles();

        var strip = GUILayoutUtility.GetRect(0f, float.MaxValue, StripHeight, StripHeight);
        EditorGUI.DrawRect(strip, StructureColor);
        var interior = new Rect(strip.x + 1f, strip.y + 1f, strip.width - 2f, strip.height - 2f);
        EditorGUI.DrawRect(interior, BackgroundColor);

        var textRow = new Rect(
            interior.x + ContentInset,
            interior.y + 3f,
            interior.width - (2f * ContentInset),
            TextRowHeight);
        var leftText = new Rect(textRow.x, textRow.y, textRow.width * 0.6f, textRow.height);
        var rightText = new Rect(
            leftText.xMax,
            textRow.y,
            textRow.width - leftText.width,
            textRow.height);
        var rail = new Rect(
            textRow.x,
            textRow.yMax + RowGap,
            textRow.width,
            RailHeight);

        var transitioning = status.Ready && status.CurrentTransitionIndex >= 0;
        var leftLabel = status.Ready
            ? transitioning
                ? $"{NameOrDash(status.SourceEffectName)} → {NameOrDash(status.TargetEffectName)}"
                : NameOrDash(status.CurrentEffectName)
            : "—";
        var rightLabel = !status.Ready
            ? string.Empty
            : transitioning
                ? $"{NameOrDash(status.CurrentTransitionName)} · {Mathf.RoundToInt(status.TransitionProgress * 100f)}%"
                : "ON AIR";

        rightStyle!.normal.textColor = transitioning ? LiveColor : IdleColor;
        GUI.Label(leftText, leftLabel, leftStyle);
        GUI.Label(rightText, rightLabel, rightStyle);
        EditorGUI.DrawRect(rail, StructureColor);
        if (transitioning && status.TransitionProgress > 0f)
        {
            EditorGUI.DrawRect(
                new Rect(rail.x, rail.y, rail.width * status.TransitionProgress, rail.height),
                LiveColor);
        }
    }

    /// <summary>Degrades missing runtime names without changing the strip geometry.</summary>
    private static string NameOrDash(string? name)
    {
        return string.IsNullOrEmpty(name) ? "—" : name;
    }

    /// <summary>Creates the cached strip label styles once per Editor session.</summary>
    private static void EnsureStyles()
    {
        leftStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = LiveColor },
        };
        rightStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleRight,
        };
    }
}
