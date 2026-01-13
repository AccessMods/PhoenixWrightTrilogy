using System;
using System.Collections.Generic;
using System.Text;
using AccessibilityMod.Core;
using UnityAccessibilityLib;

namespace AccessibilityMod.Utilities
{
    /// <summary>
    /// Minimal JSON parser for .NET 3.5 compatibility.
    /// Handles simple dictionaries needed for hot-reload configuration.
    /// </summary>
    public static class SimpleJsonParser
    {
        /// <summary>
        /// Parse a JSON object into a dictionary with int keys and string values.
        /// Example: {"5": "Mia Fey", "8": "Judge"}
        /// </summary>
        public static Dictionary<int, string> ParseIntStringDictionary(string json)
        {
            var result = new Dictionary<int, string>();
            if (Net35Extensions.IsNullOrWhiteSpace(json))
                return result;

            json = json.Trim();
            if (!json.StartsWith("{") || !json.EndsWith("}"))
                return result;

            // Remove outer braces
            json = json.Substring(1, json.Length - 2).Trim();
            if (string.IsNullOrEmpty(json))
                return result;

            int pos = 0;
            while (pos < json.Length)
            {
                // Skip whitespace
                while (pos < json.Length && char.IsWhiteSpace(json[pos]))
                    pos++;

                if (pos >= json.Length)
                    break;

                // Parse key (expect quoted string)
                if (json[pos] != '"')
                    break;

                string keyStr = ParseString(json, ref pos);
                if (keyStr == null)
                    break;

                // Skip whitespace and colon
                while (pos < json.Length && char.IsWhiteSpace(json[pos]))
                    pos++;

                if (pos >= json.Length || json[pos] != ':')
                    break;
                pos++; // skip colon

                // Skip whitespace
                while (pos < json.Length && char.IsWhiteSpace(json[pos]))
                    pos++;

                if (pos >= json.Length)
                    break;

                // Try to parse key as integer - skip non-integer keys (like "_comment")
                int key;
                bool isValidKey = int.TryParse(keyStr, out key);

                // Parse value (expect quoted string)
                if (json[pos] != '"')
                    break;

                string value = ParseString(json, ref pos);
                if (value == null)
                    break;

                // Only add if key was a valid integer
                if (isValidKey)
                {
                    result[key] = value;
                }

                // Skip whitespace and comma
                while (pos < json.Length && char.IsWhiteSpace(json[pos]))
                    pos++;

                if (pos < json.Length && json[pos] == ',')
                    pos++;
            }

            return result;
        }

        /// <summary>
        /// Parse a JSON object into a dictionary with string keys and string values.
        /// Example: {"mode.investigation": "Investigation mode", "mode.trial": "Trial mode"}
        /// Keys starting with underscore (like "_comment") are skipped.
        /// </summary>
        public static Dictionary<string, string> ParseStringDictionary(string json)
        {
            var result = new Dictionary<string, string>();
            if (Net35Extensions.IsNullOrWhiteSpace(json))
                return result;

            json = json.Trim();
            if (!json.StartsWith("{") || !json.EndsWith("}"))
                return result;

            // Remove outer braces
            json = json.Substring(1, json.Length - 2).Trim();
            if (string.IsNullOrEmpty(json))
                return result;

            int pos = 0;
            while (pos < json.Length)
            {
                // Skip whitespace
                while (pos < json.Length && char.IsWhiteSpace(json[pos]))
                    pos++;

                if (pos >= json.Length)
                    break;

                // Parse key (expect quoted string)
                if (json[pos] != '"')
                    break;

                string key = ParseString(json, ref pos);
                if (key == null)
                    break;

                // Skip whitespace and colon
                while (pos < json.Length && char.IsWhiteSpace(json[pos]))
                    pos++;

                if (pos >= json.Length || json[pos] != ':')
                    break;
                pos++; // skip colon

                // Skip whitespace
                while (pos < json.Length && char.IsWhiteSpace(json[pos]))
                    pos++;

                if (pos >= json.Length)
                    break;

                // Parse value (expect quoted string)
                if (json[pos] != '"')
                    break;

                string value = ParseString(json, ref pos);
                if (value == null)
                    break;

                // Skip keys starting with underscore (comments)
                if (!key.StartsWith("_"))
                {
                    result[key] = value;
                }

                // Skip whitespace and comma
                while (pos < json.Length && char.IsWhiteSpace(json[pos]))
                    pos++;

                if (pos < json.Length && json[pos] == ',')
                    pos++;
            }

            return result;
        }

        private static string ParseString(string json, ref int pos)
        {
            if (pos >= json.Length || json[pos] != '"')
                return null;

            pos++; // skip opening quote
            var sb = new StringBuilder();

            while (pos < json.Length)
            {
                char c = json[pos];

                if (c == '"')
                {
                    pos++; // skip closing quote
                    return sb.ToString();
                }

                if (c == '\\' && pos + 1 < json.Length)
                {
                    pos++;
                    char escaped = json[pos];
                    switch (escaped)
                    {
                        case '"':
                            sb.Append('"');
                            break;
                        case '\\':
                            sb.Append('\\');
                            break;
                        case 'n':
                            sb.Append('\n');
                            break;
                        case 'r':
                            sb.Append('\r');
                            break;
                        case 't':
                            sb.Append('\t');
                            break;
                        default:
                            sb.Append(escaped);
                            break;
                    }
                    pos++;
                }
                else
                {
                    sb.Append(c);
                    pos++;
                }
            }

            return null; // unterminated string
        }
    }
}
