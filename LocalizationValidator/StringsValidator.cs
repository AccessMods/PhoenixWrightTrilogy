using System.Text.RegularExpressions;

namespace LocalizationValidator;

public enum IssueType
{
    Error,
    Warning,
}

public record ValidationIssue(IssueType Type, string Category, string Key, string Message);

public class ValidationResult
{
    public List<ValidationIssue> Issues { get; } = new();
    public int EnglishKeyCount { get; set; }
    public int TranslatedKeyCount { get; set; }

    public int ErrorCount => Issues.Count(i => i.Type == IssueType.Error);
    public int WarningCount => Issues.Count(i => i.Type == IssueType.Warning);
    public bool HasErrors => ErrorCount > 0;
}

public class StringsValidator
{
    private static readonly Regex PlaceholderRegex = new(@"\{(\d+)\}", RegexOptions.Compiled);

    public ValidationResult Validate(
        Dictionary<string, string> englishStrings,
        Dictionary<string, string> targetStrings
    )
    {
        var result = new ValidationResult
        {
            EnglishKeyCount = englishStrings.Count,
            TranslatedKeyCount = targetStrings.Count,
        };

        // Check for missing keys (in English but not in translation)
        foreach (var key in englishStrings.Keys)
        {
            if (!targetStrings.ContainsKey(key))
            {
                result.Issues.Add(
                    new ValidationIssue(
                        IssueType.Error,
                        "MISSING",
                        key,
                        "Key missing in translation"
                    )
                );
            }
        }

        // Check for extra keys (in translation but not in English)
        foreach (var key in targetStrings.Keys)
        {
            if (!englishStrings.ContainsKey(key))
            {
                result.Issues.Add(
                    new ValidationIssue(
                        IssueType.Warning,
                        "EXTRA",
                        key,
                        "Key not in English (remove or typo?)"
                    )
                );
            }
        }

        // Check placeholder consistency and empty values for keys that exist in both
        foreach (var kvp in englishStrings)
        {
            if (!targetStrings.TryGetValue(kvp.Key, out var targetValue))
                continue; // Already reported as missing

            // Check for empty values
            if (string.IsNullOrWhiteSpace(targetValue) && !string.IsNullOrWhiteSpace(kvp.Value))
            {
                result.Issues.Add(
                    new ValidationIssue(IssueType.Warning, "EMPTY", kvp.Key, "Value is empty")
                );
            }

            // Check placeholder consistency
            var englishPlaceholders = ExtractPlaceholders(kvp.Value);
            var targetPlaceholders = ExtractPlaceholders(targetValue);

            if (!englishPlaceholders.SetEquals(targetPlaceholders))
            {
                var expected =
                    englishPlaceholders.Count > 0
                        ? string.Join(", ", englishPlaceholders.OrderBy(p => p))
                        : "(none)";
                var found =
                    targetPlaceholders.Count > 0
                        ? string.Join(", ", targetPlaceholders.OrderBy(p => p))
                        : "(none)";

                result.Issues.Add(
                    new ValidationIssue(
                        IssueType.Error,
                        "PLACEHOLDER",
                        kvp.Key,
                        $"Expected {expected}, found {found}"
                    )
                );
            }
        }

        // Check plural form completeness
        CheckPluralForms(englishStrings, targetStrings, result);

        return result;
    }

    private HashSet<string> ExtractPlaceholders(string value)
    {
        var placeholders = new HashSet<string>();
        foreach (Match match in PlaceholderRegex.Matches(value))
        {
            placeholders.Add(match.Value);
        }
        return placeholders;
    }

    private void CheckPluralForms(
        Dictionary<string, string> englishStrings,
        Dictionary<string, string> targetStrings,
        ValidationResult result
    )
    {
        // Find all plural base keys in English (keys ending with .one or .other)
        var pluralBases = new HashSet<string>();

        foreach (var key in englishStrings.Keys)
        {
            if (key.EndsWith(".one"))
            {
                pluralBases.Add(key.Substring(0, key.Length - 4));
            }
            else if (key.EndsWith(".other"))
            {
                pluralBases.Add(key.Substring(0, key.Length - 6));
            }
        }

        // For each plural base, check that target has both forms if English has both
        foreach (var baseKey in pluralBases)
        {
            var oneKey = baseKey + ".one";
            var otherKey = baseKey + ".other";

            bool englishHasOne = englishStrings.ContainsKey(oneKey);
            bool englishHasOther = englishStrings.ContainsKey(otherKey);
            bool targetHasOne = targetStrings.ContainsKey(oneKey);
            bool targetHasOther = targetStrings.ContainsKey(otherKey);

            // If English has .one but target doesn't (and key isn't already reported as missing)
            if (englishHasOne && !targetHasOne)
            {
                // Already reported in missing keys check
            }

            // If English has .other but target doesn't
            if (englishHasOther && !targetHasOther)
            {
                // Already reported in missing keys check
            }

            // If English has both forms but target only has one, add a specific warning
            if (englishHasOne && englishHasOther)
            {
                if (targetHasOne && !targetHasOther)
                {
                    result.Issues.Add(
                        new ValidationIssue(
                            IssueType.Warning,
                            "PLURAL",
                            baseKey,
                            "Has .one but missing .other form"
                        )
                    );
                }
                else if (!targetHasOne && targetHasOther)
                {
                    result.Issues.Add(
                        new ValidationIssue(
                            IssueType.Warning,
                            "PLURAL",
                            baseKey,
                            "Has .other but missing .one form"
                        )
                    );
                }
            }
        }
    }
}
