using System;
using System.Collections.Generic;
using System.Reflection;
using AccessibilityMod.Core;
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

            SpeechManager.Announce(
                L.Get("dying_message.puzzle_start", _dotCount),
                TextType.Investigation
            );
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
                SpeechManager.Announce(L.Get("dying_message.not_in_mode"), TextType.SystemMessage);
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
                SpeechManager.Announce(L.Get("dying_message.not_in_mode"), TextType.SystemMessage);
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
                    TextType.Investigation
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
        /// </summary>
        private static string GetDotDescription(int index)
        {
            try
            {
                switch (GSStatic.global_work_.language)
                {
                    case Language.JAPAN:
                    case Language.CHINA_S:
                    case Language.CHINA_T:
                    case Language.KOREA:
                        // For non-English, just return position
                        return L.Get("dying_message.position", index + 1);
                    default:
                        if (index >= 0 && index < DotDescriptions_US.Length)
                        {
                            return DotDescriptions_US[index];
                        }
                        return L.Get("dying_message.position", index + 1);
                }
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
                SpeechManager.Announce(L.Get("dying_message.not_in_mode"), TextType.SystemMessage);
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
                    case Language.KOREA:
                        hint = L.Get("dying_message.hint_lines_drawn", lineCount);
                        break;
                    default:
                        // English hint
                        hint = GetEnglishHint(lineCount);
                        break;
                }

                SpeechManager.Announce(hint, TextType.Investigation);
            }
            catch
            {
                SpeechManager.Announce(
                    L.Get("dying_message.hint_fallback"),
                    TextType.Investigation
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
                return L.Get("dying_message.hint_continue_e");
            }
            else if (lineCount < 6)
            {
                return L.Get("dying_message.hint_draw_a");
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
                SpeechManager.Announce(L.Get("dying_message.not_in_mode"), TextType.SystemMessage);
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
                    TextType.Investigation
                );
            }
            catch
            {
                SpeechManager.Announce(L.Get("dying_message.hint_generic"), TextType.Investigation);
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
                TextType.Investigation
            );
        }

        /// <summary>
        /// Called when a line is deleted.
        /// </summary>
        public static void OnLineDeleted()
        {
            SpeechManager.Announce(L.Get("dying_message.line_removed"), TextType.Investigation);
        }

        /// <summary>
        /// Called when line drawing is started from a dot.
        /// </summary>
        public static void OnLineStarted(int dotIndex)
        {
            string desc = GetDotDescription(dotIndex);
            SpeechManager.Announce(
                L.Get("dying_message.line_started", dotIndex + 1, desc),
                TextType.Investigation
            );
        }

        /// <summary>
        /// Called when line drawing is cancelled.
        /// </summary>
        public static void OnLineCancelled()
        {
            SpeechManager.Announce(L.Get("dying_message.line_cancelled"), TextType.Investigation);
        }
    }
}
