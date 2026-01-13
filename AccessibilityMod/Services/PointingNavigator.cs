using System;
using System.Collections.Generic;
using System.Reflection;
using AccessibilityMod.Core;
using UnityAccessibilityLib;
using UnityEngine;

namespace AccessibilityMod.Services
{
    /// <summary>
    /// Handles navigation for court/trial pointing mini-games where the player
    /// must indicate a location on a map or image.
    /// </summary>
    public static class PointingNavigator
    {
        private static List<PointInfo> _points = new List<PointInfo>();
        private static int _currentIndex = -1;
        private static bool _wasRunning = false;

        public class PointInfo
        {
            public int Index;
            public float CenterX;
            public float CenterY;
            public string Description;
        }

        /// <summary>
        /// Checks if the pointing mini-game is currently active.
        /// </summary>
        public static bool IsPointingActive()
        {
            try
            {
                if (PointMiniGame.instance != null)
                {
                    return PointMiniGame.instance.is_running;
                }
            }
            catch
            {
                // Class may not exist in current context
            }
            return false;
        }

        /// <summary>
        /// Called each frame to detect when pointing mode starts/ends.
        /// </summary>
        public static void Update()
        {
            bool isRunning = IsPointingActive();

            if (isRunning && !_wasRunning)
            {
                // Pointing mode just started
                OnPointingStart();
            }
            else if (!isRunning && _wasRunning)
            {
                // Pointing mode just ended
                OnPointingEnd();
            }

            _wasRunning = isRunning;
        }

        private static void OnPointingStart()
        {
            RefreshPoints();

            if (_points.Count > 0)
            {
                string message =
                    L.Get("mode.pointing")
                    + ". "
                    + L.Get("navigation.x_target_areas", _points.Count)
                    + ". "
                    + L.Get("navigation.use_brackets_navigate")
                    + ", "
                    + L.Get("navigation.use_e_present");
                SpeechManager.Announce(message, GameTextType.Trial);
            }
            else
            {
                SpeechManager.Announce(
                    L.Get("mode.pointing") + ". Use arrow keys to move cursor, E to present.",
                    GameTextType.Trial
                );
            }
        }

        private static void OnPointingEnd()
        {
            _points.Clear();
            _currentIndex = -1;
        }

        /// <summary>
        /// Refreshes the list of target points from the game data.
        /// </summary>
        public static void RefreshPoints()
        {
            _points.Clear();
            _currentIndex = -1;

            try
            {
                if (PointMiniGame.instance == null || !PointMiniGame.instance.is_running)
                    return;

                // Use reflection to get the private converted_point_ field
                var pointsField = typeof(PointMiniGame).GetField(
                    "converted_point_",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );

                if (pointsField == null)
                {
                    AccessibilityMod.Core.AccessibilityMod.Logger?.Warning(
                        "Could not find converted_point_ field"
                    );
                    return;
                }

                var convertedPoints = pointsField.GetValue(PointMiniGame.instance) as GSPoint4[];

                if (convertedPoints == null || convertedPoints.Length == 0)
                {
                    AccessibilityMod.Core.AccessibilityMod.Logger?.Msg("No converted points found");
                    return;
                }

                for (int i = 0; i < convertedPoints.Length; i++)
                {
                    var point = convertedPoints[i];

                    // Skip empty/invalid points
                    if (point.x0 == 0 && point.y0 == 0 && point.x1 == 0 && point.y1 == 0)
                        continue;

                    // Calculate center of the quadrilateral
                    float centerX = (point.x0 + point.x1 + point.x2 + point.x3) / 4f;
                    float centerY = (point.y0 + point.y1 + point.y2 + point.y3) / 4f;

                    // Generate position description
                    string posDesc = GetPositionDescription(centerX, centerY);

                    _points.Add(
                        new PointInfo
                        {
                            Index = i,
                            CenterX = centerX,
                            CenterY = centerY,
                            Description = L.Get("pointing.area_position", i + 1, posDesc),
                        }
                    );
                }

                AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                    $"Found {_points.Count} pointing targets"
                );
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error refreshing points: {ex.Message}"
                );
            }
        }

        private static string GetPositionDescription(float x, float y)
        {
            // Get cursor area size for reference
            float areaWidth = 1920f;
            float areaHeight = 1080f;

            try
            {
                if (MiniGameCursor.instance != null)
                {
                    var areaSize = MiniGameCursor.instance.cursor_area_size;
                    areaWidth = areaSize.x;
                    areaHeight = areaSize.y;
                }
            }
            catch
            {
                // Use defaults
            }

            string horizontal =
                x < areaWidth * 0.33f ? L.Get("position.left")
                : x > areaWidth * 0.66f ? L.Get("position.right")
                : L.Get("position.center");
            string vertical =
                y < areaHeight * 0.33f ? L.Get("position.top")
                : y > areaHeight * 0.66f ? L.Get("position.bottom")
                : L.Get("position.middle");
            return $"{vertical} {horizontal}";
        }

        /// <summary>
        /// Navigate to the next target point.
        /// </summary>
        public static void NavigateNext()
        {
            if (!IsPointingActive())
            {
                SpeechManager.Announce(L.Get("pointing.not_in_mode"), GameTextType.SystemMessage);
                return;
            }

            if (_points.Count == 0)
            {
                RefreshPoints();
            }

            if (_points.Count == 0)
            {
                SpeechManager.Announce(L.Get("navigation.no_target_areas"), GameTextType.Trial);
                return;
            }

            _currentIndex = (_currentIndex + 1) % _points.Count;
            AnnounceCurrentPoint();
            MoveCursorToCurrentPoint();
        }

        /// <summary>
        /// Navigate to the previous target point.
        /// </summary>
        public static void NavigatePrevious()
        {
            if (!IsPointingActive())
            {
                SpeechManager.Announce(L.Get("pointing.not_in_mode"), GameTextType.SystemMessage);
                return;
            }

            if (_points.Count == 0)
            {
                RefreshPoints();
            }

            if (_points.Count == 0)
            {
                SpeechManager.Announce(L.Get("navigation.no_target_areas"), GameTextType.Trial);
                return;
            }

            // When starting from -1 (no selection), go to the last item
            if (_currentIndex < 0)
            {
                _currentIndex = _points.Count - 1;
            }
            else
            {
                _currentIndex = (_currentIndex - 1 + _points.Count) % _points.Count;
            }
            AnnounceCurrentPoint();
            MoveCursorToCurrentPoint();
        }

        /// <summary>
        /// Announce the currently selected point.
        /// </summary>
        public static void AnnounceCurrentPoint()
        {
            if (_points.Count == 0 || _currentIndex >= _points.Count)
            {
                SpeechManager.Announce(L.Get("navigation.no_point_selected"), GameTextType.Trial);
                return;
            }

            var point = _points[_currentIndex];
            SpeechManager.Announce(point.Description, GameTextType.Trial);
        }

        /// <summary>
        /// Announce all available target points.
        /// </summary>
        public static void AnnounceAllPoints()
        {
            if (!IsPointingActive())
            {
                SpeechManager.Announce(L.Get("pointing.not_in_mode"), GameTextType.SystemMessage);
                return;
            }

            if (_points.Count == 0)
            {
                RefreshPoints();
            }

            if (_points.Count == 0)
            {
                SpeechManager.Announce(L.Get("navigation.no_target_areas"), GameTextType.Trial);
                return;
            }

            string summary = L.Get("navigation.x_target_areas", _points.Count) + ": ";
            var descriptions = new List<string>();
            foreach (var point in _points)
            {
                descriptions.Add(point.Description);
            }
            summary += string.Join(", ", descriptions.ToArray());

            SpeechManager.Announce(summary, GameTextType.Trial);
        }

        /// <summary>
        /// Move the cursor to the currently selected point.
        /// </summary>
        private static void MoveCursorToCurrentPoint()
        {
            if (_points.Count == 0 || _currentIndex >= _points.Count)
                return;

            try
            {
                var point = _points[_currentIndex];

                // The converted points are in game coordinates
                // We need to convert to cursor position coordinates
                // Account for background offset
                float bgOffsetX = 0;
                try
                {
                    if (bgCtrl.instance != null)
                    {
                        bgOffsetX = bgCtrl.instance.bg_pos_x;
                    }
                }
                catch
                {
                    // Ignore
                }

                // Convert point coordinates to cursor position
                // Based on PointMiniGame.GetCursorRect():
                // cursor rect = (cursor_position.x - 8 + bg_pos_x, cursor_position.y - 8, 16, 16)
                // So cursor_position.x = point_x - bg_pos_x + 8
                float cursorX = point.CenterX - bgOffsetX;
                float cursorY = point.CenterY;

                if (MiniGameCursor.instance != null)
                {
                    MiniGameCursor.instance.cursor_position = new Vector3(cursorX, cursorY, 0f);
                    AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                        $"Moved cursor to ({cursorX}, {cursorY})"
                    );
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error moving cursor: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Get the number of target points.
        /// </summary>
        public static int GetPointCount()
        {
            if (_points.Count == 0 && IsPointingActive())
            {
                RefreshPoints();
            }
            return _points.Count;
        }
    }
}
