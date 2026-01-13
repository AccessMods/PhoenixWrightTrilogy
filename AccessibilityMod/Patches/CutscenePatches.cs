using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using AccessibilityMod.Core;
using HarmonyLib;
using UnityAccessibilityLib;

namespace AccessibilityMod.Patches
{
    /// <summary>
    /// Patches for capturing opening narration text during animated intro sequences.
    /// The opening narration uses PcViewCtrl (sprite-based text) instead of messageBoardCtrl.
    /// Text is decoded from GSDemo_gs3_op4.dsp.moji_code_ array and output line-by-line.
    /// </summary>
    [HarmonyPatch]
    public static class CutscenePatches
    {
        // Track opening state
        private static bool _isOpeningActive = false;
        private static List<string> _openingLines = new List<string>();
        private static int _lastOutputLine = -1;
        private static string _lastDecodedFullText = ""; // Track full decoded text to prevent duplicate outputs
        private static int _pendingType = -1; // Type to decode on first PcViewNext (deferred decoding)

        // Cached reflection info for accessing private fields
        private static FieldInfo _dspField = null;
        private static FieldInfo _mojiCodeField = null;
        private static FieldInfo _pcViewLineField = null;
        private static bool _reflectionInitialized = false;

        #region Harmony Patches

        /// <summary>
        /// Hook PcViewCtrl.PcViewInitialize to detect opening start and decode all text.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PcViewCtrl), "PcViewInitialize")]
        public static void PcViewInitialize_Postfix(PcViewCtrl __instance, int type)
        {
            try
            {
                // For GS3 episode 4 opening, SetOp4SysMess (which loads moji_code_) is called
                // AFTER PcViewInitialize for type 1. So we defer decoding to the first PcViewNext
                // call when the text is guaranteed to be loaded.
                _isOpeningActive = true;
                _lastOutputLine = -1;
                _pendingType = type;
                _openingLines.Clear();

                AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                    $"Opening narration initialized (type {type}), deferring text decode"
                );
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in PcViewInitialize patch: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Hook PcViewCtrl.PcViewNext to detect line completion.
        /// Called each time a character is revealed.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PcViewCtrl), "PcViewNext")]
        public static void PcViewNext_Postfix(PcViewCtrl __instance)
        {
            try
            {
                if (!_isOpeningActive)
                    return;

                // Deferred decoding: decode on first PcViewNext call when text is guaranteed to be loaded
                if (_pendingType >= 0)
                {
                    int type = _pendingType;
                    _pendingType = -1;

                    bool hasNewText = DecodeOpeningText(type);
                    if (hasNewText && _openingLines.Count > 0)
                    {
                        OutputLine(0);
                        _lastOutputLine = 0;

                        AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                            $"Opening narration started (type {type}), decoded {_openingLines.Count} lines"
                        );
                    }
                    return;
                }

                int currentLine = GetPcViewLine(__instance);
                if (currentLine < 0)
                    return;

                // When line advances, output the new line
                if (currentLine > _lastOutputLine)
                {
                    OutputLine(currentLine);
                    _lastOutputLine = currentLine;
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in PcViewNext patch: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Get the current line value from PcViewCtrl using reflection.
        /// </summary>
        private static int GetPcViewLine(PcViewCtrl instance)
        {
            try
            {
                if (_pcViewLineField == null)
                {
                    _pcViewLineField = typeof(PcViewCtrl).GetField(
                        "line",
                        BindingFlags.NonPublic | BindingFlags.Instance
                    );
                }

                if (_pcViewLineField != null)
                {
                    return (int)_pcViewLineField.GetValue(instance);
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error getting PcViewCtrl.line: {ex.Message}"
                );
            }

            return -1;
        }

        /// <summary>
        /// Hook PcViewCtrl.EndView to cleanup when opening ends.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PcViewCtrl), "EndView")]
        public static void EndView_Postfix()
        {
            try
            {
                if (_isOpeningActive)
                {
                    AccessibilityMod.Core.AccessibilityMod.Logger?.Msg("Opening narration ended");
                }

                _isOpeningActive = false;
                _openingLines.Clear();
                _lastOutputLine = -1;
                _lastDecodedFullText = ""; // Reset for next opening
                _pendingType = -1;
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in EndView patch: {ex.Message}"
                );
            }
        }

        #endregion

        #region GS1/GS2 Opening Text Hooks

        // Track last output text to prevent duplicates
        private static string _lastDemoText = "";

        /// <summary>
        /// Hook MessageSystem.ClearText to capture text before it's cleared during auto-advancing openings.
        /// This catches GS1/GS2 opening narration which uses messageBoardCtrl but doesn't show the arrow.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MessageSystem), "ClearText")]
        public static void ClearText_Prefix(MessageWork message_work)
        {
            try
            {
                // Check if we're in a demo sequence (opening/cutscene)
                // op_flg's lower nibble indicates demo mode: 1 = active demo
                if (message_work == null || (message_work.op_flg & 0x0F) == 0)
                    return;

                // Skip if it's GS3 opening (handled by PcViewCtrl hooks)
                if (message_work.op_no == 3 || message_work.op_no == 4)
                    return;

                // Skip if there's an active speaker - DialoguePatches will handle it
                // This prevents duplicate output for auto-advancing dialogue that has a speaker
                if (message_work.speaker_id > 0)
                    return;

                // Capture current text from messageBoardCtrl before it's cleared
                CaptureMessageBoardText();
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in ClearText prefix: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Capture and output text from messageBoardCtrl.line_list.
        /// </summary>
        private static void CaptureMessageBoardText()
        {
            try
            {
                var ctrl = messageBoardCtrl.instance;
                if (ctrl == null || ctrl.line_list == null)
                    return;

                // Combine all lines
                StringBuilder sb = new StringBuilder();
                foreach (var line in ctrl.line_list)
                {
                    if (line != null && !Net35Extensions.IsNullOrWhiteSpace(line.text))
                    {
                        if (sb.Length > 0)
                            sb.Append(" ");
                        sb.Append(line.text);
                    }
                }

                string text = sb.ToString().Trim();

                // Skip empty or duplicate text
                if (Net35Extensions.IsNullOrWhiteSpace(text) || text == _lastDemoText)
                    return;

                // Skip if DialoguePatches already announced this text (via arrow hook)
                // This prevents double-reading regular dialogue that has no speaker
                if (text == DialoguePatches._lastAnnouncedText)
                    return;

                _lastDemoText = text;

                // Output as narrator (no speaker name for opening narration)
                SpeechManager.Output("", text, GameTextType.Narrator);

                AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                    $"Opening text captured: {text}"
                );
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error capturing message board text: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Hook GSDemo.Exit to reset demo text tracking when demo step completes.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GSDemo), "Exit")]
        public static void Exit_Postfix()
        {
            // Don't reset immediately - there might be text to capture
            // The duplicate prevention will handle repeated text
        }

        #endregion

        #region Text Decoding

        /// <summary>
        /// Initialize reflection to access private GSDemo_gs3_op4.dsp structure.
        /// </summary>
        private static bool InitializeReflection()
        {
            if (_reflectionInitialized)
                return _dspField != null && _mojiCodeField != null;

            _reflectionInitialized = true;

            try
            {
                // Get the GSDemo_gs3_op4 type
                var gs3Op4Type = typeof(PcViewCtrl).Assembly.GetType("GSDemo_gs3_op4");
                if (gs3Op4Type == null)
                {
                    AccessibilityMod.Core.AccessibilityMod.Logger?.Warning(
                        "Could not find GSDemo_gs3_op4 type"
                    );
                    return false;
                }

                // Get the private static dsp field
                _dspField = gs3Op4Type.GetField(
                    "dsp",
                    BindingFlags.NonPublic | BindingFlags.Static
                );
                if (_dspField == null)
                {
                    AccessibilityMod.Core.AccessibilityMod.Logger?.Warning(
                        "Could not find dsp field in GSDemo_gs3_op4"
                    );
                    return false;
                }

                // Get the DSP struct type and moji_code_ field
                var dspType = _dspField.FieldType;
                _mojiCodeField = dspType.GetField(
                    "moji_code_",
                    BindingFlags.Public | BindingFlags.Instance
                );
                if (_mojiCodeField == null)
                {
                    AccessibilityMod.Core.AccessibilityMod.Logger?.Warning(
                        "Could not find moji_code_ field in DSP struct"
                    );
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error initializing reflection: {ex.Message}"
                );
                return false;
            }
        }

        /// <summary>
        /// Decode opening text from the dsp.moji_code_ array.
        /// Character codes > 128 represent text (subtract 128 to get actual char).
        /// Returns true if new text was decoded, false if it was duplicate/empty.
        /// </summary>
        private static bool DecodeOpeningText(int type)
        {
            _openingLines.Clear();

            if (!InitializeReflection())
                return false;

            try
            {
                // Get the dsp struct value
                var dsp = _dspField.GetValue(null);
                if (dsp == null)
                    return false;

                // Get the moji_code_ array
                var mojiCodes = (ushort[])_mojiCodeField.GetValue(dsp);
                if (mojiCodes == null)
                    return false;

                // Decode all characters into a single string
                StringBuilder fullText = new StringBuilder();
                foreach (ushort code in mojiCodes)
                {
                    if (code == 0 || code == 65535)
                        continue; // Skip empty/end markers

                    if (code > 128)
                    {
                        // Text character: subtract 128 to get actual char
                        char c = (char)(code - 128);
                        // Convert full-width ASCII to half-width
                        c = ConvertFullWidthToHalfWidth(c);
                        fullText.Append(c);
                    }
                    // Skip control codes (code <= 128 and != 0)
                }

                if (fullText.Length == 0)
                    return false;

                string fullTextStr = fullText.ToString();

                // Check if this is the same text we already decoded (prevents re-reading
                // when PcViewInitialize is called with different types but same moji_code_)
                if (fullTextStr == _lastDecodedFullText)
                {
                    AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                        $"Opening text unchanged for type {type}, skipping duplicate output"
                    );
                    return false;
                }

                _lastDecodedFullText = fullTextStr;

                // Insert spaces at line boundaries and store as single output
                string formattedText = InsertLineBreakSpaces(fullTextStr, type);
                _openingLines.Add(formattedText.Trim());
                return true;
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error decoding opening text: {ex.Message}"
                );
                return false;
            }
        }

        /// <summary>
        /// Convert full-width ASCII characters (U+FF01-U+FF5E) to half-width (U+0021-U+007E).
        /// Also converts full-width space (U+3000) to regular space.
        /// </summary>
        private static char ConvertFullWidthToHalfWidth(char c)
        {
            // Full-width ASCII: U+FF01 (！) to U+FF5E (～) maps to U+0021 (!) to U+007E (~)
            if (c >= '\uFF01' && c <= '\uFF5E')
            {
                return (char)(c - 0xFEE0);
            }
            // Full-width space U+3000 to regular space
            if (c == '\u3000')
            {
                return ' ';
            }
            return c;
        }

        /// <summary>
        /// Insert spaces at line boundaries where the game would visually wrap text.
        /// The game stores text without spaces at line breaks since the visual break serves as separator.
        /// </summary>
        private static string InsertLineBreakSpaces(string text, int type)
        {
            int[] charCounts = GetCharCountsForLanguage(type);
            if (charCounts == null || charCounts.Length == 0)
                return text;

            StringBuilder result = new StringBuilder();
            int textIndex = 0;

            for (
                int lineIndex = 0;
                lineIndex < charCounts.Length && textIndex < text.Length;
                lineIndex++
            )
            {
                int lineLength = Math.Min(charCounts[lineIndex], text.Length - textIndex);
                if (lineLength > 0)
                {
                    string line = text.Substring(textIndex, lineLength);

                    // Add space before this line if needed (not first line, and no existing space)
                    if (
                        result.Length > 0
                        && !char.IsWhiteSpace(result[result.Length - 1])
                        && !char.IsWhiteSpace(line[0])
                    )
                    {
                        result.Append(' ');
                    }

                    result.Append(line);
                    textIndex += lineLength;
                }
            }

            // Append any remaining text
            if (textIndex < text.Length)
            {
                string remaining = text.Substring(textIndex);
                if (
                    result.Length > 0
                    && !char.IsWhiteSpace(result[result.Length - 1])
                    && !char.IsWhiteSpace(remaining[0])
                )
                {
                    result.Append(' ');
                }
                result.Append(remaining);
            }

            return result.ToString();
        }

        /// <summary>
        /// Get character counts per line based on current language and opening type.
        /// These are derived from FontImageData.font_count arrays in PcViewCtrl.
        /// </summary>
        private static int[] GetCharCountsForLanguage(int type)
        {
            try
            {
                var language = GSStatic.global_work_.language;

                // Type 0: First part of opening (defendant info card)
                // Type 1: Second part (narrative text)
                if (type == 0)
                {
                    switch (language)
                    {
                        case Language.JAPAN:
                            return new int[] { 9, 8, 6 };
                        case Language.USA:
                            return new int[] { 18, 26, 24 };
                        case Language.FRANCE:
                            return new int[] { 20, 34, 25 };
                        case Language.GERMAN:
                            return new int[] { 18, 25, 20 };
                        case Language.KOREA:
                            return new int[] { 11, 8, 6 };
                        case Language.CHINA_S:
                        case Language.CHINA_T:
                            return new int[] { 9, 8, 6 };
                        default:
                            return new int[] { 18, 26, 24 };
                    }
                }
                else // type 1
                {
                    switch (language)
                    {
                        case Language.JAPAN:
                            return new int[] { 9, 11, 11, 11, 17 };
                        case Language.USA:
                            return new int[] { 22, 18, 18, 18, 28, 19, 11 };
                        case Language.FRANCE:
                            return new int[] { 20, 30, 13, 20, 34, 25, 18 };
                        case Language.GERMAN:
                            return new int[] { 26, 28, 14, 18, 25, 20, 20 };
                        case Language.KOREA:
                            return new int[] { 12, 15, 12, 14, 19 };
                        case Language.CHINA_S:
                            return new int[] { 9, 11, 13, 12, 11 };
                        case Language.CHINA_T:
                            return new int[] { 9, 13, 13, 12, 12 };
                        default:
                            return new int[] { 22, 18, 18, 18, 28, 19, 11 };
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region Output

        /// <summary>
        /// Output a specific line to the clipboard.
        /// </summary>
        private static void OutputLine(int lineIndex)
        {
            if (lineIndex >= 0 && lineIndex < _openingLines.Count)
            {
                string line = _openingLines[lineIndex];
                if (!Net35Extensions.IsNullOrWhiteSpace(line))
                {
                    SpeechManager.Output("", line, GameTextType.Narrator);
                }
            }
        }

        #endregion
    }
}
