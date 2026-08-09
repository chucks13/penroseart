// Builds paste-ready Effect defaults blocks from saved Settings assets without writing source files.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Reads an Effect's authored defaults block, substitutes saved numeric Settings values, and copies
/// the unchanged source block shape to the system clipboard.
/// </summary>
internal static class EffectDefaultsClipboardUtility
{
    /// <summary>Matches one complete single-line constant declaration inside an authored defaults block.</summary>
    private static readonly Regex ConstantDeclarationPattern = new Regex(
        @"^[ \t]*(?:(?:public|private|protected|internal)\s+)?const\s+" +
        @"(?<type>[A-Za-z_][A-Za-z0-9_.]*)\s+" +
        @"(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?<value>[^;\r\n]+?)\s*;",
        RegexOptions.Multiline | RegexOptions.CultureInvariant);

    /// <summary>Matches a signed decimal numeric literal while retaining its optional float suffix.</summary>
    private static readonly Regex NumericLiteralPattern = new Regex(
        @"^\s*(?<literal>[+-]?(?:(?:\d+(?:\.\d*)?)|(?:\.\d+))(?:[eE][+-]?\d+)?[fF]?)\s*$",
        RegexOptions.CultureInvariant);

    /// <summary>Matches C# identifier tokens in a defaults initializer expression.</summary>
    private static readonly Regex IdentifierPattern = new Regex(
        @"\b[A-Za-z_][A-Za-z0-9_]*\b",
        RegexOptions.CultureInvariant);

    /// <summary>Copies a paste-ready Standalone Defaults block for the selected Effect.</summary>
    internal static void CopyStandaloneDefaultsUpdate(Type effectType, SerializedProperty settingsProperty)
    {
        CopyDefaultsUpdate(
            effectType,
            settingsProperty,
            "StandaloneDefaults",
            "Standalone",
            "Standalone Defaults");
    }

    /// <summary>Copies a paste-ready Sync Defaults block for the selected Effect.</summary>
    internal static void CopySyncDefaultsUpdate(Type effectType, SerializedProperty settingsProperty)
    {
        CopyDefaultsUpdate(effectType, settingsProperty, "SyncDefaults", "Sync", "Sync Defaults");
    }

    /// <summary>
    /// Maps saved Settings fields through the Effect's defaults initializer and replaces only mapped
    /// numeric literal spans in the original authored block.
    /// </summary>
    private static void CopyDefaultsUpdate(
        Type effectType,
        SerializedProperty settingsProperty,
        string defaultsPropertyName,
        string constantPrefix,
        string defaultsLabel)
    {
        var sourcePath = SourcePathFor(effectType);
        if (string.IsNullOrEmpty(sourcePath))
        {
            LogSummary(effectType, defaultsLabel, 0, 0, new List<string> { "source file" }, null);
            return;
        }

        var source = File.ReadAllText(sourcePath);
        if (!TryFindInitializer(source, defaultsPropertyName, out var initializerIndex, out var initializerBody))
        {
            LogSummary(
                effectType,
                defaultsLabel,
                0,
                0,
                new List<string> { defaultsPropertyName + " initializer" },
                null);
            return;
        }

        if (!TryFindDefaultsBlock(source, defaultsLabel, initializerIndex, out var defaultsBlock))
        {
            LogSummary(
                effectType,
                defaultsLabel,
                0,
                0,
                new List<string> { defaultsLabel + " const block" },
                null);
            return;
        }

        var assignments = ParseAssignments(initializerBody);
        var declarations = new Dictionary<string, Match>(StringComparer.Ordinal);
        foreach (Match declaration in ConstantDeclarationPattern.Matches(defaultsBlock))
        {
            declarations[declaration.Groups["name"].Value] = declaration;
        }

        var replacements = new Dictionary<int, string>();
        var replacementLengths = new Dictionary<int, int>();
        var skipped = new List<string>();
        var stretchedRails = new List<string>();
        var updatedCount = 0;
        var matchedCount = 0;

        foreach (var settingsField in DirectChildrenOf(settingsProperty))
        {
            if (!assignments.TryGetValue(settingsField.name, out var expression))
            {
                skipped.Add(settingsField.name);
                continue;
            }

            var constantNames = ConstantNamesIn(expression, declarations, constantPrefix);
            if (!TryReadNumericValues(settingsField, out var values, out var railsDiffer))
            {
                skipped.Add(settingsField.name);
                continue;
            }

            if (railsDiffer)
            {
                stretchedRails.Add(settingsField.name);
            }

            if (constantNames.Count != values.Count)
            {
                skipped.Add(settingsField.name);
                continue;
            }

            var fieldReplacements = new List<KeyValuePair<int, string>>();
            var fieldReplacementLengths = new Dictionary<int, int>();
            var fieldMatchedCount = 0;
            var fieldUpdatedCount = 0;
            var fieldCanUpdate = true;

            for (var i = 0; i < constantNames.Count; i++)
            {
                var constantName = constantNames[i];
                if (!declarations.TryGetValue(constantName, out var declaration) ||
                    !TryCreateLiteral(
                        declaration,
                        values[i],
                        out var literalIndex,
                        out var literalLength,
                        out var replacement,
                        out var alreadyMatched))
                {
                    skipped.Add(settingsField.name + " (" + constantName + ")");
                    fieldCanUpdate = false;
                    break;
                }

                if (alreadyMatched)
                {
                    fieldMatchedCount++;
                }
                else
                {
                    fieldUpdatedCount++;
                    fieldReplacements.Add(new KeyValuePair<int, string>(literalIndex, replacement));
                    fieldReplacementLengths[literalIndex] = literalLength;
                }
            }

            if (!fieldCanUpdate)
            {
                continue;
            }

            foreach (var replacement in fieldReplacements)
            {
                replacements[replacement.Key] = replacement.Value;
                replacementLengths[replacement.Key] = fieldReplacementLengths[replacement.Key];
            }

            matchedCount += fieldMatchedCount;
            updatedCount += fieldUpdatedCount;
        }

        var replacementIndexes = new List<int>(replacements.Keys);
        replacementIndexes.Sort((left, right) => right.CompareTo(left));
        var updatedBlock = new StringBuilder(defaultsBlock);
        foreach (var replacementIndex in replacementIndexes)
        {
            updatedBlock.Remove(replacementIndex, replacementLengths[replacementIndex]);
            updatedBlock.Insert(replacementIndex, replacements[replacementIndex]);
        }

        EditorGUIUtility.systemCopyBuffer = updatedBlock.ToString();
        LogSummary(
            effectType,
            defaultsLabel,
            updatedCount,
            matchedCount,
            skipped,
            stretchedRails,
            sourcePath);
    }

    /// <summary>Locates an Effect source file by convention, then by its imported <see cref="MonoScript"/>.</summary>
    private static string SourcePathFor(Type effectType)
    {
        var conventionalPath = "Assets/effects/" + effectType.Name + ".cs";
        if (File.Exists(conventionalPath))
        {
            return conventionalPath;
        }

        foreach (var guid in AssetDatabase.FindAssets(effectType.Name + " t:MonoScript", new[] { "Assets" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            if (script != null && script.GetClass() == effectType)
            {
                return path;
            }
        }

        return string.Empty;
    }

    /// <summary>Finds the object-initializer body of the named defaults property.</summary>
    private static bool TryFindInitializer(
        string source,
        string propertyName,
        out int initializerIndex,
        out string initializerBody)
    {
        var pattern = @"\b" + Regex.Escape(propertyName) +
            @"\s*=>\s*new\s+[A-Za-z_][A-Za-z0-9_.<>]*\s*(?:\(\s*\))?\s*\{";
        var match = Regex.Match(source, pattern, RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            initializerIndex = -1;
            initializerBody = string.Empty;
            return false;
        }

        initializerIndex = match.Index;
        var bodyStart = match.Index + match.Length;
        var depth = 1;
        for (var index = bodyStart; index < source.Length; index++)
        {
            if (source[index] == '{')
            {
                depth++;
            }
            else if (source[index] == '}' && --depth == 0)
            {
                initializerBody = source.Substring(bodyStart, index - bodyStart);
                return true;
            }
        }

        initializerBody = string.Empty;
        return false;
    }

    /// <summary>Extracts the authored defaults block from its marker through its final constant line.</summary>
    private static bool TryFindDefaultsBlock(
        string source,
        string defaultsLabel,
        int initializerIndex,
        out string defaultsBlock)
    {
        var markerPattern = new Regex(
            @"^[ \t]*// " + Regex.Escape(defaultsLabel) + @"[ \t]*\r?$",
            RegexOptions.Multiline | RegexOptions.CultureInvariant);
        var marker = markerPattern.Match(source);
        if (!marker.Success || marker.Index >= initializerIndex)
        {
            defaultsBlock = string.Empty;
            return false;
        }

        var searchEnd = initializerIndex;
        var anyDefaultsMarkerPattern = new Regex(
            @"^[ \t]*// (?:Standalone|Sync) Defaults[ \t]*\r?$",
            RegexOptions.Multiline | RegexOptions.CultureInvariant);
        var nextMarker = anyDefaultsMarkerPattern.Match(source, marker.Index + marker.Length);
        if (nextMarker.Success && nextMarker.Index < searchEnd)
        {
            searchEnd = nextMarker.Index;
        }

        var region = source.Substring(marker.Index, searchEnd - marker.Index);
        var declarations = ConstantDeclarationPattern.Matches(region);
        if (declarations.Count == 0)
        {
            defaultsBlock = string.Empty;
            return false;
        }

        var lastDeclaration = declarations[declarations.Count - 1];
        var declarationEnd = lastDeclaration.Index + lastDeclaration.Length;
        var lineEnd = region.IndexOf('\n', declarationEnd);
        var blockEnd = lineEnd < 0 ? region.Length : lineEnd + 1;

        defaultsBlock = region.Substring(0, blockEnd);
        return true;
    }

    /// <summary>Splits top-level object-initializer assignments without splitting constructor arguments.</summary>
    private static Dictionary<string, string> ParseAssignments(string initializerBody)
    {
        var assignments = new Dictionary<string, string>(StringComparer.Ordinal);
        var assignmentStart = 0;
        var parenthesisDepth = 0;
        var bracketDepth = 0;
        var braceDepth = 0;

        for (var index = 0; index < initializerBody.Length; index++)
        {
            switch (initializerBody[index])
            {
                case '(':
                    parenthesisDepth++;
                    break;
                case ')':
                    parenthesisDepth--;
                    break;
                case '[':
                    bracketDepth++;
                    break;
                case ']':
                    bracketDepth--;
                    break;
                case '{':
                    braceDepth++;
                    break;
                case '}':
                    braceDepth--;
                    break;
                case ',' when parenthesisDepth == 0 && bracketDepth == 0 && braceDepth == 0:
                    AddAssignment(initializerBody.Substring(assignmentStart, index - assignmentStart), assignments);
                    assignmentStart = index + 1;
                    break;
            }
        }

        AddAssignment(initializerBody.Substring(assignmentStart), assignments);
        return assignments;
    }

    /// <summary>Adds one parsed field/expression pair when the initializer segment is an assignment.</summary>
    private static void AddAssignment(string segment, IDictionary<string, string> assignments)
    {
        var equalsIndex = segment.IndexOf('=');
        if (equalsIndex < 0)
        {
            return;
        }

        var fieldName = segment.Substring(0, equalsIndex).Trim();
        var expression = segment.Substring(equalsIndex + 1).Trim();
        if (IdentifierPattern.Match(fieldName).Value == fieldName)
        {
            assignments[fieldName] = expression;
        }
    }

    /// <summary>Returns copies of the direct serialized fields held by a Settings object.</summary>
    private static List<SerializedProperty> DirectChildrenOf(SerializedProperty parent)
    {
        var children = new List<SerializedProperty>();
        var iterator = parent.Copy();
        var end = iterator.GetEndProperty();
        var enterChildren = true;
        while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, end))
        {
            enterChildren = false;
            if (iterator.depth == parent.depth + 1)
            {
                children.Add(iterator.Copy());
            }
        }

        return children;
    }

    /// <summary>Returns the initializer's referenced constants in expression order.</summary>
    private static List<string> ConstantNamesIn(
        string expression,
        IReadOnlyDictionary<string, Match> declarations,
        string expectedPrefix)
    {
        var names = new List<string>();
        foreach (Match identifier in IdentifierPattern.Matches(expression))
        {
            var name = identifier.Value;
            if (declarations.ContainsKey(name) || name.StartsWith(expectedPrefix, StringComparison.Ordinal))
            {
                names.Add(name);
            }
        }

        return names;
    }

    /// <summary>
    /// Reads one numeric scalar or the two authored endpoints of a supported range and reports whether
    /// its saved rails differ from the endpoint-seeded rails that defaults would create.
    /// </summary>
    private static bool TryReadNumericValues(
        SerializedProperty settingsField,
        out List<object> values,
        out bool railsDiffer)
    {
        values = new List<object>();
        railsDiffer = false;

        if (settingsField.propertyType == SerializedPropertyType.Integer)
        {
            values.Add(settingsField.intValue);
            return true;
        }

        if (settingsField.propertyType == SerializedPropertyType.Float)
        {
            values.Add(settingsField.floatValue);
            return true;
        }

        if (settingsField.type == nameof(FloatRange))
        {
            var minimum = settingsField.FindPropertyRelative(nameof(FloatRange.Min));
            var maximum = settingsField.FindPropertyRelative(nameof(FloatRange.Max));
            var lowRail = settingsField.FindPropertyRelative(nameof(FloatRange.LowRail));
            var highRail = settingsField.FindPropertyRelative(nameof(FloatRange.HighRail));
            values.Add(minimum.floatValue);
            values.Add(maximum.floatValue);
            railsDiffer = lowRail.floatValue != minimum.floatValue || highRail.floatValue != maximum.floatValue;
            return true;
        }

        if (settingsField.type == nameof(IntRange))
        {
            var minimum = settingsField.FindPropertyRelative(nameof(IntRange.MinInclusive));
            var maximum = settingsField.FindPropertyRelative(nameof(IntRange.MaxExclusive));
            var lowRail = settingsField.FindPropertyRelative(nameof(IntRange.LowRail));
            var highRail = settingsField.FindPropertyRelative(nameof(IntRange.HighRail));
            values.Add(minimum.intValue);
            values.Add(maximum.intValue);
            railsDiffer = lowRail.intValue != minimum.intValue || highRail.intValue != maximum.intValue;
            return true;
        }

        return false;
    }

    /// <summary>Formats one mapped value in the declaration's existing literal style.</summary>
    private static bool TryCreateLiteral(
        Match declaration,
        object value,
        out int literalIndex,
        out int literalLength,
        out string replacement,
        out bool alreadyMatched)
    {
        var numericLiteral = NumericLiteralPattern.Match(declaration.Groups["value"].Value);
        if (!numericLiteral.Success)
        {
            literalIndex = -1;
            literalLength = 0;
            replacement = string.Empty;
            alreadyMatched = false;
            return false;
        }

        var original = numericLiteral.Groups["literal"].Value;
        literalIndex = declaration.Groups["value"].Index + numericLiteral.Groups["literal"].Index;
        literalLength = numericLiteral.Groups["literal"].Length;
        var typeName = declaration.Groups["type"].Value;

        if (typeName == "int" && value is int integerValue)
        {
            if (!int.TryParse(original, NumberStyles.Integer, CultureInfo.InvariantCulture, out var oldValue))
            {
                replacement = string.Empty;
                alreadyMatched = false;
                return false;
            }

            alreadyMatched = oldValue == integerValue;
            replacement = alreadyMatched
                ? original
                : integerValue.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        if (typeName == "float" && value is float floatValue)
        {
            var suffixLength = original.EndsWith("f", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            var originalNumber = suffixLength == 0
                ? original
                : original.Substring(0, original.Length - suffixLength);
            if (!float.TryParse(
                    originalNumber,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var oldValue))
            {
                replacement = string.Empty;
                alreadyMatched = false;
                return false;
            }

            alreadyMatched = oldValue.Equals(floatValue);
            replacement = alreadyMatched ? original : FormatFloatLiteral(floatValue, original);
            return true;
        }

        replacement = string.Empty;
        alreadyMatched = false;
        return false;
    }

    /// <summary>Formats a float round-trip value while retaining decimal, exponent, sign, and suffix conventions.</summary>
    private static string FormatFloatLiteral(float value, string original)
    {
        var suffix = original.EndsWith("f", StringComparison.OrdinalIgnoreCase)
            ? original.Substring(original.Length - 1)
            : string.Empty;
        var originalNumber = suffix.Length == 0
            ? original
            : original.Substring(0, original.Length - suffix.Length);
        var exponentIndex = originalNumber.IndexOfAny(new[] { 'e', 'E' });
        string formatted;

        if (exponentIndex >= 0)
        {
            var decimalPoint = originalNumber.IndexOf('.');
            var minimumDecimals = decimalPoint >= 0 ? exponentIndex - decimalPoint - 1 : 0;
            formatted = value.ToString("E8", CultureInfo.InvariantCulture);
            for (var decimals = minimumDecimals; decimals <= 8; decimals++)
            {
                var candidate = value.ToString("E" + decimals, CultureInfo.InvariantCulture);
                if (float.TryParse(candidate, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) &&
                    parsed.Equals(value))
                {
                    formatted = candidate;
                    break;
                }
            }

            if (originalNumber[exponentIndex] == 'e')
            {
                formatted = formatted.Replace('E', 'e');
            }
        }
        else
        {
            formatted = value.ToString("R", CultureInfo.InvariantCulture);
            var originalDecimalPoint = originalNumber.IndexOf('.');
            if (originalDecimalPoint >= 0 && formatted.IndexOfAny(new[] { '.', 'e', 'E' }) < 0)
            {
                formatted += ".0";
            }

            var formattedDecimalPoint = formatted.IndexOf('.');
            if (originalDecimalPoint >= 0 && formattedDecimalPoint >= 0)
            {
                var minimumDecimals = originalNumber.Length - originalDecimalPoint - 1;
                var formattedDecimals = formatted.Length - formattedDecimalPoint - 1;
                if (formattedDecimals < minimumDecimals)
                {
                    formatted += new string('0', minimumDecimals - formattedDecimals);
                }
            }

            if (originalNumber.StartsWith(".", StringComparison.Ordinal) &&
                formatted.StartsWith("0.", StringComparison.Ordinal))
            {
                formatted = formatted.Substring(1);
            }
            else if (originalNumber.StartsWith("-.", StringComparison.Ordinal) &&
                formatted.StartsWith("-0.", StringComparison.Ordinal))
            {
                formatted = "-" + formatted.Substring(2);
            }
        }

        if (originalNumber.StartsWith("+", StringComparison.Ordinal) && value >= 0f &&
            !formatted.StartsWith("+", StringComparison.Ordinal))
        {
            formatted = "+" + formatted;
        }

        if (suffix.Length == 0 && formatted.IndexOfAny(new[] { '.', 'e', 'E' }) >= 0)
        {
            suffix = "f";
        }

        return formatted + suffix;
    }

    /// <summary>Writes the required one-line copy summary, including only material asset-only rail differences.</summary>
    private static void LogSummary(
        Type effectType,
        string defaultsLabel,
        int updatedCount,
        int matchedCount,
        IReadOnlyList<string> skipped,
        IReadOnlyList<string> stretchedRails,
        string sourcePath = null)
    {
        var copiedFrom = string.IsNullOrEmpty(sourcePath) ? string.Empty : " copied from " + sourcePath + ";";
        var skippedText = skipped == null || skipped.Count == 0 ? "none" : string.Join(", ", skipped);
        var railText = stretchedRails == null || stretchedRails.Count == 0
            ? string.Empty
            : "; asset-only rails differ from endpoint-seeded defaults: " +
                string.Join(", ", stretchedRails);
        Debug.Log(
            "Copy Defaults Update — " + effectType.Name + " " + defaultsLabel + ":" + copiedFrom + " " +
            updatedCount + " values updated, " + matchedCount +
            " already matched; skipped/unmatched: " + skippedText + railText + ".");
    }
}
