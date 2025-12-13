using System.Text;
using System.Text.RegularExpressions;

namespace AccessibilityMod.Core
{
    public static class TextCleaner
    {
        // Unity rich text tags
        private static readonly Regex ColorTagRegex = new Regex(
            @"<color[^>]*>|</color>",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );
        private static readonly Regex SizeTagRegex = new Regex(
            @"<size[^>]*>|</size>",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );
        private static readonly Regex BoldTagRegex = new Regex(
            @"<b>|</b>",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );
        private static readonly Regex ItalicTagRegex = new Regex(
            @"<i>|</i>",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );
        private static readonly Regex MaterialTagRegex = new Regex(
            @"<material[^>]*>|</material>",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );
        private static readonly Regex QuadTagRegex = new Regex(
            @"<quad[^>]*>",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );

        // Generic tag cleaner for any remaining tags
        private static readonly Regex GenericTagRegex = new Regex(
            @"<[^>]+>",
            RegexOptions.Compiled
        );

        // Whitespace normalization
        private static readonly Regex MultipleSpacesRegex = new Regex(
            @"\s+",
            RegexOptions.Compiled
        );

        public static string Clean(string input)
        {
            if (Net35Extensions.IsNullOrWhiteSpace(input))
                return string.Empty;

            string cleaned = input;

            // Remove Unity rich text tags
            cleaned = ColorTagRegex.Replace(cleaned, "");
            cleaned = SizeTagRegex.Replace(cleaned, "");
            cleaned = BoldTagRegex.Replace(cleaned, "");
            cleaned = ItalicTagRegex.Replace(cleaned, "");
            cleaned = MaterialTagRegex.Replace(cleaned, "");
            cleaned = QuadTagRegex.Replace(cleaned, "");

            // Remove any remaining HTML-style tags
            cleaned = GenericTagRegex.Replace(cleaned, "");

            // Handle escape sequences
            cleaned = UnescapeText(cleaned);

            // Normalize whitespace
            cleaned = MultipleSpacesRegex.Replace(cleaned, " ");

            return cleaned.Trim();
        }

        private static string UnescapeText(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            StringBuilder sb = new StringBuilder(input);

            sb.Replace("\\n", "\n");
            sb.Replace("\\r", "\r");
            sb.Replace("\\t", "\t");
            sb.Replace("\\\"", "\"");
            sb.Replace("\\'", "'");
            sb.Replace("\\\\", "\\");

            // Replace special characters that TTS can't read
            sb.Replace("☓", "X"); // Multiplication sign used in resolution (e.g., 1920☓1080)

            return sb.ToString();
        }

        public static string CombineLines(params string[] lines)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var line in lines)
            {
                if (!Net35Extensions.IsNullOrWhiteSpace(line))
                {
                    if (sb.Length > 0)
                        sb.Append(" ");
                    sb.Append(Clean(line));
                }
            }
            return sb.ToString();
        }
    }
}
