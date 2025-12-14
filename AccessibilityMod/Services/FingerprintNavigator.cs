using System;
using System.Collections.Generic;
using System.Reflection;
using AccessibilityMod.Core;
using UnityEngine;

namespace AccessibilityMod.Services
{
    /// <summary>
    /// Provides accessibility support for the fingerprinting mini-game.
    /// Helps with fingerprint location selection and suspect matching.
    /// </summary>
    public static class FingerprintNavigator
    {
        private static bool _wasActive = false;
        private static bool _wasInComparison = false;
        private static bool _wasInSelection = false;
        private static int _lastCompCursor = -1;
        private static int _currentLocationIndex = -1;
        private static int _locationCount = 0;

        // Fingerprint location offsets and scales per game (from FingerMiniGame)
        private static readonly Vector2[] SelectFingerOffset = new Vector2[]
        {
            new Vector2(270f, -30f),
            new Vector2(270f, -30f),
            new Vector2(100f, -100f),
        };

        private static readonly Vector2[] SelectFingerScale = new Vector2[]
        {
            new Vector2(5.75f, 5.75f),
            new Vector2(5.75f, 5.75f),
            new Vector2(6.75f, 6.75f),
        };

        // Character names in English (indexed by character index)
        // Note: indices 1 and 2 are swapped from the Japanese constant names
        private static readonly string[] CharacterNames = new string[]
        {
            "Ema Skye", // 0
            "Mike Meekins", // 1 (SW_HUMAN_TOMOE in Japanese)
            "Jake Marshall", // 2 (SW_HUMAN_ZAIMON in Japanese)
            "Lana Skye", // 3
            "Damon Gant", // 4
            "Bruce Goodman", // 5
            "Damon Gant", // 6
            "Dick Gumshoe", // 7
        };

        // Maps display position to character index (from human_idx_tbl)
        private static readonly int[] DisplayToCharacter = new int[] { 6, 5, 2, 4, 7, 1, 0, 3 };

        // Correct character index for each game (from finger_info[].correct)
        private static readonly int[] CorrectAnswers = new int[] { 7, 2, 0 }; // Gumshoe, Jake Marshall, Ema

        /// <summary>
        /// Checks if the fingerprint mini-game is currently active.
        /// </summary>
        public static bool IsFingerprintActive()
        {
            try
            {
                if (FingerMiniGame.instance != null)
                {
                    return FingerMiniGame.instance.is_running;
                }
            }
            catch
            {
                // Class may not exist
            }
            return false;
        }

        /// <summary>
        /// Checks if we're in the selection phase (choosing fingerprint location).
        /// </summary>
        public static bool IsInSelectionPhase()
        {
            try
            {
                if (FingerMiniGame.instance == null || !FingerMiniGame.instance.is_running)
                    return false;

                // Check main_root_ is NOT active (powder phase)
                var mainRootField = typeof(FingerMiniGame).GetField(
                    "main_root_",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );

                // And comp_main_root_ is NOT active (comparison phase)
                var compRootField = typeof(FingerMiniGame).GetField(
                    "comp_main_root_",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );

                if (mainRootField != null && compRootField != null)
                {
                    var mainRoot = mainRootField.GetValue(FingerMiniGame.instance) as GameObject;
                    var compRoot = compRootField.GetValue(FingerMiniGame.instance) as GameObject;

                    // In selection phase if neither main nor comp root is active
                    bool mainActive = mainRoot != null && mainRoot.activeSelf;
                    bool compActive = compRoot != null && compRoot.activeSelf;

                    return !mainActive && !compActive;
                }
            }
            catch
            {
                // Ignore
            }
            return false;
        }

        /// <summary>
        /// Checks if we're in the powder phase (applying powder to fingerprint).
        /// </summary>
        public static bool IsInPowderPhase()
        {
            try
            {
                if (FingerMiniGame.instance == null || !FingerMiniGame.instance.is_running)
                    return false;

                // Check if main_root_ is active (powder phase)
                var mainRootField = typeof(FingerMiniGame).GetField(
                    "main_root_",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );

                if (mainRootField != null)
                {
                    var mainRoot = mainRootField.GetValue(FingerMiniGame.instance) as GameObject;
                    return mainRoot != null && mainRoot.activeSelf;
                }
            }
            catch
            {
                // Ignore
            }
            return false;
        }

        /// <summary>
        /// Checks if we're in the comparison phase (matching fingerprints to suspects).
        /// </summary>
        public static bool IsInComparisonPhase()
        {
            try
            {
                if (FingerMiniGame.instance == null || !FingerMiniGame.instance.is_running)
                    return false;

                // Check if comp_main_root_ is active
                var compRootField = typeof(FingerMiniGame).GetField(
                    "comp_main_root_",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );

                if (compRootField != null)
                {
                    var compRoot = compRootField.GetValue(FingerMiniGame.instance) as GameObject;
                    if (compRoot != null)
                    {
                        return compRoot.activeSelf;
                    }
                }
            }
            catch
            {
                // Ignore
            }
            return false;
        }

        private static bool _wasInPowder = false;

        // Cursor position tracking for powder phase
        private static Vector3 _lastCursorPos = Vector3.zero;
        private static string _lastGridPosition = "";
        private static bool _wasAtLeftEdge = false;
        private static bool _wasAtRightEdge = false;
        private static bool _wasAtTopEdge = false;
        private static bool _wasAtBottomEdge = false;

        /// <summary>
        /// Called each frame to detect mode changes.
        /// </summary>
        public static void Update()
        {
            bool isActive = IsFingerprintActive();
            bool isInComparison = IsInComparisonPhase();
            bool isInSelection = IsInSelectionPhase();
            bool isInPowder = IsInPowderPhase();

            if (isActive && !_wasActive)
            {
                OnFingerprintStart();
            }
            else if (!isActive && _wasActive)
            {
                OnFingerprintEnd();
            }

            // Check for selection phase entry
            if (isInSelection && !_wasInSelection)
            {
                OnSelectionStart();
            }
            else if (!isInSelection && _wasInSelection)
            {
                _currentLocationIndex = -1;
            }

            // Check for powder phase entry
            if (isInPowder && !_wasInPowder)
            {
                OnPowderStart();
                ResetCursorTracking();
            }

            // Track cursor position during powder phase
            if (isInPowder)
            {
                UpdateCursorFeedback();
            }

            // Check for comparison phase entry
            if (isInComparison && !_wasInComparison)
            {
                OnComparisonStart();
            }
            else if (!isInComparison && _wasInComparison)
            {
                _lastCompCursor = -1;
            }

            _wasActive = isActive;
            _wasInComparison = isInComparison;
            _wasInSelection = isInSelection;
            _wasInPowder = isInPowder;
        }

        private static void ResetCursorTracking()
        {
            _lastCursorPos = Vector3.zero;
            _lastGridPosition = "";
            _wasAtLeftEdge = false;
            _wasAtRightEdge = false;
            _wasAtTopEdge = false;
            _wasAtBottomEdge = false;
        }

        private static void UpdateCursorFeedback()
        {
            try
            {
                if (MiniGameCursor.instance == null)
                    return;

                Vector3 pos = MiniGameCursor.instance.cursor_position;
                Vector2 areaSize = MiniGameCursor.instance.cursor_area_size;

                // Only process if position changed significantly
                if (Vector3.Distance(pos, _lastCursorPos) < 5f)
                    return;

                _lastCursorPos = pos;

                // Calculate grid position (3x3 grid)
                string horizontal = GetHorizontalPosition(pos.x, areaSize.x);
                string vertical = GetVerticalPosition(pos.y, areaSize.y);
                string gridPos = $"{vertical} {horizontal}";

                // Check edges
                float edgeThreshold = 20f;
                bool atLeftEdge = pos.x <= edgeThreshold;
                bool atRightEdge = pos.x >= areaSize.x - edgeThreshold - 100f; // Account for guide area
                bool atTopEdge = pos.y <= edgeThreshold;
                bool atBottomEdge = pos.y >= areaSize.y - edgeThreshold - 100f;

                // Build announcement
                string announcement = "";

                // Announce edge hits
                if (atLeftEdge && !_wasAtLeftEdge)
                {
                    announcement = "Left edge";
                }
                else if (atRightEdge && !_wasAtRightEdge)
                {
                    announcement = "Right edge";
                }
                else if (atTopEdge && !_wasAtTopEdge)
                {
                    announcement = "Top edge";
                }
                else if (atBottomEdge && !_wasAtBottomEdge)
                {
                    announcement = "Bottom edge";
                }
                // Announce grid position changes
                else if (gridPos != _lastGridPosition && !string.IsNullOrEmpty(_lastGridPosition))
                {
                    announcement = gridPos;
                }

                _wasAtLeftEdge = atLeftEdge;
                _wasAtRightEdge = atRightEdge;
                _wasAtTopEdge = atTopEdge;
                _wasAtBottomEdge = atBottomEdge;
                _lastGridPosition = gridPos;

                if (!string.IsNullOrEmpty(announcement))
                {
                    SpeechManager.Announce(announcement, TextType.Investigation);
                }
            }
            catch
            {
                // Ignore errors
            }
        }

        private static string GetHorizontalPosition(float x, float width)
        {
            float third = width / 3f;
            if (x < third)
                return "left";
            else if (x > third * 2f)
                return "right";
            else
                return "center";
        }

        private static string GetVerticalPosition(float y, float height)
        {
            float third = height / 3f;
            if (y < third)
                return "Top";
            else if (y > third * 2f)
                return "Bottom";
            else
                return "Middle";
        }

        private static void OnSelectionStart()
        {
            _currentLocationIndex = -1;
            _locationCount = GetFingerprintLocationCount();
        }

        private static void OnPowderStart()
        {
            SpeechManager.Announce(
                "Powder phase. Move cursor with arrow keys while pressing Enter to spread powder across the area. Press E to blow when done.",
                TextType.Investigation
            );
        }

        private static void OnFingerprintStart()
        {
            int count = GetFingerprintLocationCount();
            SpeechManager.Announce(
                $"Fingerprint examination. {count} location{(count != 1 ? "s" : "")} to examine. Use [ and ] to navigate, Enter to examine. Press H for hint.",
                TextType.Investigation
            );
        }

        private static void OnFingerprintEnd()
        {
            _lastCompCursor = -1;
        }

        private static void OnComparisonStart()
        {
            _lastCompCursor = -1;
            SpeechManager.Announce(
                "Fingerprint comparison. Use Left/Right to select suspect, E to compare. Press H for hint.",
                TextType.Investigation
            );
        }

        /// <summary>
        /// Announces a hint for the current phase.
        /// </summary>
        public static void AnnounceHint()
        {
            if (!IsFingerprintActive())
            {
                SpeechManager.Announce("Not in fingerprint mode", TextType.SystemMessage);
                return;
            }

            if (IsInComparisonPhase())
            {
                AnnounceComparisonHint();
            }
            else if (IsInPowderPhase())
            {
                AnnouncePowderHint();
            }
            else if (IsInSelectionPhase())
            {
                AnnounceSelectionHint();
            }
            else
            {
                SpeechManager.Announce(
                    "Use [ and ] to navigate fingerprint locations, Enter to examine.",
                    TextType.Investigation
                );
            }
        }

        private static void AnnounceSelectionHint()
        {
            int count = GetFingerprintLocationCount();
            SpeechManager.Announce(
                $"{count} fingerprint location{(count != 1 ? "s" : "")} to examine. Use [ and ] to navigate between locations, Enter to examine the selected area.",
                TextType.Investigation
            );
        }

        private static void AnnouncePowderHint()
        {
            SpeechManager.Announce(
                "Move cursor with arrow keys while pressing Enter to spread powder across the area. Press E to blow away excess and reveal the print.",
                TextType.Investigation
            );
        }

        private static void AnnounceComparisonHint()
        {
            SpeechManager.Announce(
                "Use Left/Right to select suspect, E to compare.",
                TextType.Investigation
            );
        }

        /// <summary>
        /// Announces the currently selected suspect during comparison.
        /// </summary>
        public static void AnnounceCurrentSuspect()
        {
            if (!IsInComparisonPhase())
                return;

            try
            {
                var cursorField = typeof(FingerMiniGame).GetField(
                    "comp_cursor_",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );

                if (cursorField != null)
                {
                    int cursor = (int)cursorField.GetValue(FingerMiniGame.instance);

                    if (cursor != _lastCompCursor)
                    {
                        _lastCompCursor = cursor;

                        int charIndex = DisplayToCharacter[cursor];
                        string name = CharacterNames[charIndex];

                        SpeechManager.Announce(name, TextType.Investigation);
                    }
                }
            }
            catch
            {
                // Ignore
            }
        }

        /// <summary>
        /// Gets a description of the current state.
        /// </summary>
        public static void AnnounceState()
        {
            if (!IsFingerprintActive())
            {
                SpeechManager.Announce("Not in fingerprint mode", TextType.SystemMessage);
                return;
            }

            if (IsInComparisonPhase())
            {
                try
                {
                    var cursorField = typeof(FingerMiniGame).GetField(
                        "comp_cursor_",
                        BindingFlags.NonPublic | BindingFlags.Instance
                    );

                    if (cursorField != null)
                    {
                        int cursor = (int)cursorField.GetValue(FingerMiniGame.instance);
                        int charIndex = DisplayToCharacter[cursor];
                        string name = CharacterNames[charIndex];

                        SpeechManager.Announce(
                            $"Fingerprint comparison. {name} selected. Press H for hint.",
                            TextType.Investigation
                        );
                        return;
                    }
                }
                catch
                {
                    // Fall through
                }

                SpeechManager.Announce(
                    "Fingerprint comparison phase. Press H for hint.",
                    TextType.Investigation
                );
            }
            else if (IsInPowderPhase())
            {
                SpeechManager.Announce(
                    "Powder phase. Move cursor with arrow keys while pressing Enter to apply powder. Press E to blow when done.",
                    TextType.Investigation
                );
            }
            else if (IsInSelectionPhase())
            {
                int count = GetFingerprintLocationCount();
                string locationInfo =
                    _currentLocationIndex >= 0
                        ? $"Location {_currentLocationIndex + 1} of {count}. "
                        : $"{count} location{(count != 1 ? "s" : "")}. ";
                SpeechManager.Announce(
                    $"Selection phase. {locationInfo}Use [ and ] to navigate, Enter to examine. Press H for hint.",
                    TextType.Investigation
                );
            }
            else
            {
                SpeechManager.Announce(
                    "Fingerprint examination. Press H for hint.",
                    TextType.Investigation
                );
            }
        }

        /// <summary>
        /// Gets the number of fingerprint locations for the current game.
        /// </summary>
        public static int GetFingerprintLocationCount()
        {
            try
            {
                if (FingerMiniGame.instance == null)
                    return 0;

                int gameId = FingerMiniGame.instance.game_id;

                // finger_info[gameId].tbl_num contains the count
                // Game 0: 6 locations, Game 1: 2 locations, Game 2: 5 locations
                switch (gameId)
                {
                    case 0:
                        return 6;
                    case 1:
                        return 2;
                    case 2:
                        return 5;
                    default:
                        return 0;
                }
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Navigate to the next fingerprint location.
        /// </summary>
        public static void NavigateNext()
        {
            if (!IsFingerprintActive())
            {
                SpeechManager.Announce("Not in fingerprint mode", TextType.SystemMessage);
                return;
            }

            if (!IsInSelectionPhase())
            {
                SpeechManager.Announce(
                    "Navigation only available in selection phase",
                    TextType.SystemMessage
                );
                return;
            }

            int count = GetFingerprintLocationCount();
            if (count == 0)
            {
                SpeechManager.Announce("No fingerprint locations found", TextType.Investigation);
                return;
            }

            _currentLocationIndex = (_currentLocationIndex + 1) % count;
            NavigateToCurrentLocation();
        }

        /// <summary>
        /// Navigate to the previous fingerprint location.
        /// </summary>
        public static void NavigatePrevious()
        {
            if (!IsFingerprintActive())
            {
                SpeechManager.Announce("Not in fingerprint mode", TextType.SystemMessage);
                return;
            }

            if (!IsInSelectionPhase())
            {
                SpeechManager.Announce(
                    "Navigation only available in selection phase",
                    TextType.SystemMessage
                );
                return;
            }

            int count = GetFingerprintLocationCount();
            if (count == 0)
            {
                SpeechManager.Announce("No fingerprint locations found", TextType.Investigation);
                return;
            }

            _currentLocationIndex =
                _currentLocationIndex <= 0 ? count - 1 : _currentLocationIndex - 1;
            NavigateToCurrentLocation();
        }

        /// <summary>
        /// Navigate to the current location and announce it.
        /// </summary>
        private static void NavigateToCurrentLocation()
        {
            try
            {
                int gameId = FingerMiniGame.instance.game_id;
                int count = GetFingerprintLocationCount();

                if (_currentLocationIndex < 0 || _currentLocationIndex >= count)
                    return;

                // Get the fingerprint location center
                Vector2 center = GetFingerprintLocationCenter(gameId, _currentLocationIndex);

                // Move the cursor
                if (MiniGameCursor.instance != null)
                {
                    MiniGameCursor.instance.cursor_position = new Vector3(center.x, center.y, 0f);
                }

                // Announce the location
                string positionDesc = GetPositionDescription(center.x, center.y);
                SpeechManager.Announce(
                    $"Location {_currentLocationIndex + 1} of {count} ({positionDesc}). Press Enter to examine.",
                    TextType.Investigation
                );
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error navigating to fingerprint location: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Gets the center position of a fingerprint location.
        /// </summary>
        private static Vector2 GetFingerprintLocationCenter(int gameId, int locationIndex)
        {
            // Fingerprint inspect data coordinates (from finger_bg0aX_tbl)
            // These are the center coordinates for each fingerprint location
            // Format: x0,y0,x1,y1,x2,y2,x3,y3 form a quadrilateral

            // Game 0 (6 locations) - approximate centers from INSPECT_DATA
            float[][] game0Centers = new float[][]
            {
                new float[] { 77f, 103f }, // Location 1
                new float[] { 98f, 53f }, // Location 2
                new float[] { 124f, 45f }, // Location 3
                new float[] { 147f, 53f }, // Location 4
                new float[] { 163f, 77f }, // Location 5
                new float[] { 58f, 134f }, // Location 6
            };

            // Game 1 (2 locations)
            float[][] game1Centers = new float[][]
            {
                new float[] { 97f, 57f }, // Location 1
                new float[] { 123f, 43f }, // Location 2
            };

            // Game 2 (5 locations)
            float[][] game2Centers = new float[][]
            {
                new float[] { 69f, 119f }, // Location 1
                new float[] { 87f, 49f }, // Location 2
                new float[] { 166f, 42f }, // Location 3
                new float[] { 193f, 72f }, // Location 4
                new float[] { 128f, 31f }, // Location 5
            };

            float[] center;
            Vector2 offset;
            Vector2 scale;

            switch (gameId)
            {
                case 0:
                    center = game0Centers[locationIndex];
                    offset = SelectFingerOffset[0];
                    scale = SelectFingerScale[0];
                    break;
                case 1:
                    center = game1Centers[locationIndex];
                    offset = SelectFingerOffset[1];
                    scale = SelectFingerScale[1];
                    break;
                case 2:
                    center = game2Centers[locationIndex];
                    offset = SelectFingerOffset[2];
                    scale = SelectFingerScale[2];
                    break;
                default:
                    return Vector2.zero;
            }

            // Convert to screen coordinates (same formula as MiniGameGSPoint4Hit.ConvertPoint)
            float screenX = offset.x + center[0] * scale.x;
            float screenY = offset.y + center[1] * scale.y;

            return new Vector2(screenX, screenY);
        }

        /// <summary>
        /// Gets a position description for the given coordinates.
        /// </summary>
        private static string GetPositionDescription(float x, float y)
        {
            // Cursor area is typically 1920x1080 or similar
            float areaWidth = 1920f;
            float areaHeight = 1080f;

            string horizontal =
                x < areaWidth * 0.33f ? "left"
                : x > areaWidth * 0.66f ? "right"
                : "center";
            string vertical =
                y < areaHeight * 0.33f ? "top"
                : y > areaHeight * 0.66f ? "bottom"
                : "middle";

            return $"{vertical} {horizontal}";
        }
    }
}
