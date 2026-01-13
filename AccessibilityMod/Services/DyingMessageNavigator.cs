using System;
using System.Collections.Generic;
using System.Reflection;
using AccessibilityMod.Core;
using UnityAccessibilityLib;
using UnityEngine;

namespace AccessibilityMod.Services
{
    /// <summary>
    /// Provides accessibility support for the Dying Message (connect the dots) minigame.
    /// Helps navigate between dots and provides hints for spelling "EMA".
    /// </summary>
    public static class DyingMessageNavigator
    {
        private static bool _wasActive = false;
        private static int _currentDotIndex = -1;
        private static int _dotCount = 0;

        // Dot descriptions for English version (12 dots spelling EMA)
        // Based on checkDieMessage_us() validation logic
        private static readonly string[] DotDescriptions_US = new string[]
        {
            "E top-left", // 0 - connects to 1 (top) and 5 (down)
            "E top-right", // 1
            "M top-right", // 2 - connects to 3 and 9
            "M middle-left", // 3
            "A top", // 4 - connects to 7, 8, 10, 11
            "E bottom-left", // 5 - connects to 6
            "E bottom-right", // 6
            "A middle-left", // 7 - connects to 8, 10
            "A middle-right", // 8 - connects to 11
            "M bottom", // 9
            "A bottom-left", // 10
            "A bottom-right", // 11
        };

        // Required connections for English EMA
        // Format: each entry is [from, to] - order doesn't matter
        private static readonly int[][] RequiredConnections_US = new int[][]
        {
            new int[] { 0, 1 }, // E top horizontal
            new int[] { 0, 5 }, // E left vertical
            new int[] { 5, 6 }, // E middle horizontal
            new int[] { 2, 3 }, // M left diagonal
            new int[] { 2, 9 }, // M right diagonal
            new int[] { 4, 10 }, // A left side
            new int[] { 4, 11 }, // A right side
            new int[] { 7, 8 }, // A crossbar
        };

        // Japanese/Chinese dot descriptions for 茜 (Akane) - 15 dots
        private static readonly string[] DotDescriptions_JP = new string[]
        {
            "茜 top center", // 0 - connects to 5
            "艹 left", // 1 - connects to 6
            "艹 right", // 2 - connects to 4
            "西 top-right", // 3 - connects to 7 or 9
            "艹 center", // 4
            "茜 middle-left", // 5
            "艹 bottom", // 6
            "西 upper-inner", // 7 - connects to 9, 12
            "西 center", // 8 - connects to 10
            "西 left", // 9
            "西 bottom-left", // 10 - connects to 14
            "西 right", // 11 - connects to 13
            "西 inner-bottom", // 12
            "西 bottom-right", // 13 - connects to 14
            "茜 bottom", // 14
        };

        // Required connections for Japanese/Chinese 茜 (Akane) - 9 required connections
        private static readonly int[][] RequiredConnections_JP = new int[][]
        {
            new int[] { 0, 5 }, // 艹 left vertical
            new int[] { 1, 6 }, // 艹 right vertical
            new int[] { 2, 4 }, // 艹 connecting stroke
            new int[] { 3, 9 }, // 西 left diagonal (alt: 3-7 + 7-9)
            new int[] { 7, 12 }, // 西 inner stroke
            new int[] { 8, 10 }, // 西 bottom-left
            new int[] { 10, 14 }, // 西 bottom stroke
            new int[] { 11, 13 }, // 西 right side
            new int[] { 13, 14 }, // 西 bottom-right
        };

        // Korean dot descriptions (15 dots) - position-based
        private static readonly string[] DotDescriptions_KR = new string[]
        {
            "upper-left 1", // 0
            "lower-left 1", // 1
            "upper-left 2", // 2
            "lower-left 2", // 3
            "upper-center 1", // 4
            "lower-center 1", // 5
            "center 1", // 6
            "bottom 1", // 7
            "center 2", // 8
            "lower-center 2", // 9
            "lower-center 3", // 10
            "upper-right", // 11
            "center-right", // 12
            "lower-right 1", // 13
            "lower-right 2", // 14
        };

        // Required connections for Korean - 7 required connections (8th optional)
        private static readonly int[][] RequiredConnections_KR = new int[][]
        {
            new int[] { 0, 1 }, // Stroke 1
            new int[] { 2, 3 }, // Stroke 2
            new int[] { 4, 5 }, // Stroke 3
            new int[] { 6, 7 }, // Stroke 4
            new int[] { 8, 9 }, // Stroke 5
            new int[] { 9, 10 }, // Stroke 6
            new int[] { 11, 12 }, // Stroke 7
            // 13-14 is optional
        };

        /// <summary>
        /// Checks if the dying message minigame is active.
        /// </summary>
        public static bool IsActive()
        {
            try
            {
                if (DyingMessageMiniGame.instance == null)
                    return false;

                // body_active alone is unreliable - also check the game state
                if (!DyingMessageMiniGame.instance.body_active)
                    return false;

                // Check SwDiemesProcState_ field to ensure we're in an active state
                var stateField = typeof(DyingMessageMiniGame).GetField(
                    "SwDiemesProcState_",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );
                if (stateField != null)
                {
                    var state = stateField.GetValue(DyingMessageMiniGame.instance);
                    // sw_die_mes_none = 0, anything else means active
                    return Convert.ToInt32(state) != 0;
                }
            }
            catch
            {
                // Class may not exist
            }
            return false;
        }

        /// <summary>
        /// Called each frame to detect mode changes.
        /// </summary>
        public static void Update()
        {
            bool isActive = IsActive();

            if (isActive && !_wasActive)
            {
                OnStart();
            }
            else if (!isActive && _wasActive)
            {
                OnEnd();
            }

            _wasActive = isActive;
        }

        private static void OnStart()
        {
            _currentDotIndex = -1;
            _dotCount = GetDotCount();

            string startMessage;
            switch (GSStatic.global_work_.language)
            {
                case Language.JAPAN:
                case Language.CHINA_S:
                case Language.CHINA_T:
                    startMessage = L.Get("dying_message.puzzle_start_jp", _dotCount);
                    break;
                case Language.KOREA:
                    startMessage = L.Get("dying_message.puzzle_start_kr", _dotCount);
                    break;
                default:
                    startMessage = L.Get("dying_message.puzzle_start", _dotCount);
                    break;
            }

            SpeechManager.Announce(startMessage, GameTextType.Investigation);
        }

        private static void OnEnd()
        {
            _currentDotIndex = -1;
        }

        /// <summary>
        /// Gets the number of dots based on the current language.
        /// </summary>
        public static int GetDotCount()
        {
            try
            {
                // English has 12 dots, Japanese/Korean have 15
                switch (GSStatic.global_work_.language)
                {
                    case Language.JAPAN:
                    case Language.CHINA_S:
                    case Language.CHINA_T:
                    case Language.KOREA:
                        return 15;
                    default:
                        return 12; // English/other
                }
            }
            catch
            {
                return 12;
            }
        }

        /// <summary>
        /// Navigate to the next dot.
        /// </summary>
        public static void NavigateNext()
        {
            if (!IsActive())
            {
                SpeechManager.Announce(
                    L.Get("dying_message.not_in_mode"),
                    GameTextType.SystemMessage
                );
                return;
            }

            int count = GetDotCount();
            if (count == 0)
                return;

            _currentDotIndex = (_currentDotIndex + 1) % count;
            NavigateToCurrentDot();
        }

        /// <summary>
        /// Navigate to the previous dot.
        /// </summary>
        public static void NavigatePrevious()
        {
            if (!IsActive())
            {
                SpeechManager.Announce(
                    L.Get("dying_message.not_in_mode"),
                    GameTextType.SystemMessage
                );
                return;
            }

            int count = GetDotCount();
            if (count == 0)
                return;

            _currentDotIndex = _currentDotIndex <= 0 ? count - 1 : _currentDotIndex - 1;
            NavigateToCurrentDot();
        }

        /// <summary>
        /// Navigate to the current dot and announce it.
        /// </summary>
        private static void NavigateToCurrentDot()
        {
            try
            {
                if (DyingMessageUtil.instance == null)
                    return;

                int count = GetDotCount();
                if (_currentDotIndex < 0 || _currentDotIndex >= count)
                    return;

                // Get the dot position from draw_point_
                var drawPointField = typeof(DyingMessageUtil).GetField(
                    "draw_point_",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );

                if (drawPointField != null)
                {
                    var drawPoints = drawPointField.GetValue(DyingMessageUtil.instance) as Array;
                    if (drawPoints != null && _currentDotIndex < drawPoints.Length)
                    {
                        var point = drawPoints.GetValue(_currentDotIndex);
                        var lxField = point.GetType().GetField("lx");
                        var lyField = point.GetType().GetField("ly");

                        if (lxField != null && lyField != null)
                        {
                            int lx = Convert.ToInt32(lxField.GetValue(point));
                            int ly = Convert.ToInt32(lyField.GetValue(point));

                            // Move cursor to this position
                            if (DyingMessageUtil.instance.cursor != null)
                            {
                                DyingMessageUtil.instance.cursor.cursor_position = new Vector3(
                                    lx,
                                    ly,
                                    0f
                                );
                            }
                        }
                    }
                }

                // Announce the dot
                string description = GetDotDescription(_currentDotIndex);
                SpeechManager.Announce(
                    L.Get("dying_message.dot_description", _currentDotIndex + 1, description),
                    GameTextType.Investigation
                );
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error navigating to dot: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Gets a description for the given dot index.
        /// Prioritises localised strings, falling back to hardcoded descriptions.
        /// </summary>
        private static string GetDotDescription(int index)
        {
            try
            {
                // Try localised string first (e.g., "dying_message.dot_0")
                string locKey = "dying_message.dot_" + index;
                string localised = L.Get(locKey);

                // If localisation returned the key itself, it's not defined - use fallback
                if (localised != locKey)
                {
                    return localised;
                }

                // Fall back to hardcoded descriptions
                switch (GSStatic.global_work_.language)
                {
                    case Language.JAPAN:
                    case Language.CHINA_S:
                    case Language.CHINA_T:
                        if (index >= 0 && index < DotDescriptions_JP.Length)
                        {
                            return DotDescriptions_JP[index];
                        }
                        break;
                    case Language.KOREA:
                        if (index >= 0 && index < DotDescriptions_KR.Length)
                        {
                            return DotDescriptions_KR[index];
                        }
                        break;
                    default:
                        if (index >= 0 && index < DotDescriptions_US.Length)
                        {
                            return DotDescriptions_US[index];
                        }
                        break;
                }

                return L.Get("dying_message.position", index + 1);
            }
            catch
            {
                return L.Get("dying_message.position", index + 1);
            }
        }

        /// <summary>
        /// Announces a hint for which connections to make.
        /// </summary>
        public static void AnnounceHint()
        {
            if (!IsActive())
            {
                SpeechManager.Announce(
                    L.Get("dying_message.not_in_mode"),
                    GameTextType.SystemMessage
                );
                return;
            }

            try
            {
                // Get current line count
                var lineListField = typeof(DyingMessageUtil).GetField(
                    "line_list_",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );

                int lineCount = 0;
                if (lineListField != null && DyingMessageUtil.instance != null)
                {
                    var lineList = lineListField.GetValue(DyingMessageUtil.instance);
                    if (lineList != null)
                    {
                        var countProp = lineList.GetType().GetProperty("Count");
                        if (countProp != null)
                        {
                            lineCount = (int)countProp.GetValue(lineList, null);
                        }
                    }
                }

                string hint;
                switch (GSStatic.global_work_.language)
                {
                    case Language.JAPAN:
                    case Language.CHINA_S:
                    case Language.CHINA_T:
                        hint = GetJapaneseChineseHint(lineCount);
                        break;
                    case Language.KOREA:
                        hint = GetKoreanHint(lineCount);
                        break;
                    default:
                        hint = GetEnglishHint(lineCount);
                        break;
                }

                SpeechManager.Announce(hint, GameTextType.Investigation);
            }
            catch
            {
                SpeechManager.Announce(
                    L.Get("dying_message.hint_fallback"),
                    GameTextType.Investigation
                );
            }
        }

        private static string GetEnglishHint(int lineCount)
        {
            if (lineCount == 0)
            {
                return L.Get("dying_message.hint_start");
            }
            else if (lineCount < 3)
            {
                return L.Get("dying_message.hint_draw_e");
            }
            else if (lineCount < 5)
            {
                return L.Get("dying_message.hint_draw_m");
            }
            else if (lineCount < 8)
            {
                return L.Get("dying_message.hint_draw_a");
            }
            else
            {
                return L.Get("dying_message.hint_done", lineCount);
            }
        }

        private static string GetJapaneseChineseHint(int lineCount)
        {
            // 茜 (Akane) requires 9 lines
            if (lineCount == 0)
            {
                return L.Get("dying_message.hint_jp_start");
            }
            else if (lineCount < 3)
            {
                return L.Get("dying_message.hint_jp_grass_radical");
            }
            else if (lineCount < 6)
            {
                return L.Get("dying_message.hint_jp_west_component");
            }
            else if (lineCount < 9)
            {
                return L.Get("dying_message.hint_jp_almost_done", lineCount);
            }
            else
            {
                return L.Get("dying_message.hint_done", lineCount);
            }
        }

        private static string GetKoreanHint(int lineCount)
        {
            // Korean requires 7 lines (8th optional)
            if (lineCount == 0)
            {
                return L.Get("dying_message.hint_kr_start");
            }
            else if (lineCount < 4)
            {
                return L.Get("dying_message.hint_kr_continue", lineCount);
            }
            else if (lineCount < 7)
            {
                return L.Get("dying_message.hint_kr_almost_done", lineCount);
            }
            else
            {
                return L.Get("dying_message.hint_done", lineCount);
            }
        }

        /// <summary>
        /// Announces the current state.
        /// </summary>
        public static void AnnounceState()
        {
            if (!IsActive())
            {
                SpeechManager.Announce(
                    L.Get("dying_message.not_in_mode"),
                    GameTextType.SystemMessage
                );
                return;
            }

            try
            {
                // Check if in line-drawing state
                var stateField = typeof(DyingMessageUtil).GetField(
                    "stete_",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );

                string stateStr = L.Get("dying_message.state_ready");
                if (stateField != null && DyingMessageUtil.instance != null)
                {
                    var state = stateField.GetValue(DyingMessageUtil.instance);
                    if (state.ToString() == "Line")
                    {
                        // Get start point
                        var startField = typeof(DyingMessageUtil).GetField(
                            "linepoint_start_index_",
                            BindingFlags.NonPublic | BindingFlags.Instance
                        );
                        if (startField != null)
                        {
                            int startIndex = (int)startField.GetValue(DyingMessageUtil.instance);
                            string startDesc = GetDotDescription(startIndex);
                            stateStr = L.Get(
                                "dying_message.state_drawing_from",
                                startIndex + 1,
                                startDesc
                            );
                        }
                        else
                        {
                            stateStr = L.Get("dying_message.state_drawing");
                        }
                    }
                }

                // Get line count
                var lineListField = typeof(DyingMessageUtil).GetField(
                    "line_list_",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );

                int lineCount = 0;
                if (lineListField != null && DyingMessageUtil.instance != null)
                {
                    var lineList = lineListField.GetValue(DyingMessageUtil.instance);
                    if (lineList != null)
                    {
                        var countProp = lineList.GetType().GetProperty("Count");
                        if (countProp != null)
                        {
                            lineCount = (int)countProp.GetValue(lineList, null);
                        }
                    }
                }

                int count = GetDotCount();
                string locationInfo =
                    _currentDotIndex >= 0
                        ? L.Get("dying_message.state_at_dot", _currentDotIndex + 1, count)
                        : "";

                SpeechManager.Announce(
                    L.Get("dying_message.state", locationInfo, lineCount, stateStr),
                    GameTextType.Investigation
                );
            }
            catch
            {
                SpeechManager.Announce(
                    L.Get("dying_message.hint_generic"),
                    GameTextType.Investigation
                );
            }
        }

        /// <summary>
        /// Called when a line is created.
        /// </summary>
        public static void OnLineCreated(int from, int to)
        {
            string fromDesc = GetDotDescription(from);
            string toDesc = GetDotDescription(to);
            SpeechManager.Announce(
                L.Get("dying_message.connected", from + 1, fromDesc, to + 1, toDesc),
                GameTextType.Investigation
            );
        }

        /// <summary>
        /// Called when a line is deleted.
        /// </summary>
        public static void OnLineDeleted()
        {
            SpeechManager.Announce(L.Get("dying_message.line_removed"), GameTextType.Investigation);
        }

        /// <summary>
        /// Called when line drawing is started from a dot.
        /// </summary>
        public static void OnLineStarted(int dotIndex)
        {
            string desc = GetDotDescription(dotIndex);
            SpeechManager.Announce(
                L.Get("dying_message.line_started", dotIndex + 1, desc),
                GameTextType.Investigation
            );
        }

        /// <summary>
        /// Called when line drawing is cancelled.
        /// </summary>
        public static void OnLineCancelled()
        {
            SpeechManager.Announce(
                L.Get("dying_message.line_cancelled"),
                GameTextType.Investigation
            );
        }
    }
}
