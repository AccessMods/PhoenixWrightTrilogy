using System;
using System.Collections.Generic;
using System.Reflection;
using AccessibilityMod.Core;
using UnityAccessibilityLib;
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

        // Localization keys for suspects in comparison phase, indexed by display position (left to right)
        private static readonly string[] ComparisonCharacterKeys = new string[]
        {
            "fingerprint.character.damon_gant", // Position 0
            "fingerprint.character.mike_meekins", // Position 1
            "fingerprint.character.jake_marshall", // Position 2
            "fingerprint.character.bruce_goodman", // Position 3
            "fingerprint.character.dick_gumshoe", // Position 4
            "fingerprint.character.lana_skye", // Position 5
            "fingerprint.character.ema_skye", // Position 6
            "fingerprint.character.angel_starr", // Position 7
        };

        /// <summary>
        /// Gets the localized character name for a comparison display position.
        /// </summary>
        public static string GetComparisonCharacterName(int displayPosition)
        {
            if (displayPosition >= 0 && displayPosition < ComparisonCharacterKeys.Length)
            {
                return L.Get(ComparisonCharacterKeys[displayPosition]);
            }
            return null;
        }

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
                    announcement = L.Get("position.left_edge");
                }
                else if (atRightEdge && !_wasAtRightEdge)
                {
                    announcement = L.Get("position.right_edge");
                }
                else if (atTopEdge && !_wasAtTopEdge)
                {
                    announcement = L.Get("position.top_edge");
                }
                else if (atBottomEdge && !_wasAtBottomEdge)
                {
                    announcement = L.Get("position.bottom_edge");
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
                    SpeechManager.Announce(announcement, GameTextType.Investigation);
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
                return L.Get("position.left");
            else if (x > third * 2f)
                return L.Get("position.right");
            else
                return L.Get("position.center");
        }

        private static string GetVerticalPosition(float y, float height)
        {
            float third = height / 3f;
            if (y < third)
                return L.Get("position.top");
            else if (y > third * 2f)
                return L.Get("position.bottom");
            else
                return L.Get("position.middle");
        }

        private static void OnSelectionStart()
        {
            _currentLocationIndex = -1;
            _locationCount = GetFingerprintLocationCount();
        }

        private static void OnPowderStart()
        {
            SpeechManager.Announce(L.Get("fingerprint.powder_phase"), GameTextType.Investigation);
        }

        private static void OnFingerprintStart()
        {
            int count = GetFingerprintLocationCount();
            SpeechManager.Announce(L.Get("fingerprint.start", count), GameTextType.Investigation);
        }

        private static void OnFingerprintEnd()
        {
            _lastCompCursor = -1;
        }

        private static void OnComparisonStart()
        {
            _lastCompCursor = -1;
            SpeechManager.Announce(
                L.Get("fingerprint.comparison_start"),
                GameTextType.Investigation
            );
        }

        /// <summary>
        /// Announces a hint for the current phase.
        /// </summary>
        public static void AnnounceHint()
        {
            if (!IsFingerprintActive())
            {
                SpeechManager.Announce(
                    L.Get("fingerprint.not_in_mode"),
                    GameTextType.SystemMessage
                );
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
                    L.Get("fingerprint.navigation_hint"),
                    GameTextType.Investigation
                );
            }
        }

        private static void AnnounceSelectionHint()
        {
            int count = GetFingerprintLocationCount();
            SpeechManager.Announce(
                L.Get("fingerprint.selection_hint", count),
                GameTextType.Investigation
            );
        }

        private static void AnnouncePowderHint()
        {
            // Try to get regional coverage info
            var coverageInfo = GetRegionalCoverageInfo();
            string regionHint = coverageInfo?.GetRegionHint();

            if (!string.IsNullOrEmpty(regionHint))
            {
                // Show which regions need more powder
                SpeechManager.Announce(regionHint, GameTextType.Investigation);
            }
            else
            {
                // Standard hint
                SpeechManager.Announce(
                    L.Get("fingerprint.powder_hint"),
                    GameTextType.Investigation
                );
            }
        }

        /// <summary>
        /// Holds regional coverage information for the fingerprint powder phase.
        /// </summary>
        private class RegionalCoverageInfo
        {
            public int[,] RegionPercentages; // 3x3 grid
            public int MaskWidth;
            public int MaskHeight;

            /// <summary>
            /// Gets a hint about which regions need more powder, or null if no specific regions stand out.
            /// </summary>
            public string GetRegionHint()
            {
                // Find the highest coverage among all regions
                int maxCoverage = 0;
                for (int row = 0; row < 3; row++)
                {
                    for (int col = 0; col < 3; col++)
                    {
                        if (RegionPercentages[row, col] > maxCoverage)
                            maxCoverage = RegionPercentages[row, col];
                    }
                }

                // If max coverage is too low, we don't have enough data to give useful regional hints
                if (maxCoverage < 30)
                    return null;

                // Find regions that are significantly behind the best region
                var lowCoverageRegions = new List<string>();

                for (int row = 0; row < 3; row++)
                {
                    for (int col = 0; col < 3; col++)
                    {
                        int regionPct = RegionPercentages[row, col];
                        // A region needs attention if it's noticeably behind the best region
                        // Use tight threshold (8 points) to catch small differences at high coverage
                        if (regionPct < 98 && regionPct < maxCoverage - 8)
                        {
                            // Note: row 0 is bottom of screen (mask Y starts from bottom)
                            string vertical =
                                row == 0 ? L.Get("position.bottom")
                                : row == 1 ? L.Get("position.middle")
                                : L.Get("position.top");
                            string horizontal =
                                col == 0 ? L.Get("position.left")
                                : col == 1 ? L.Get("position.center")
                                : L.Get("position.right");
                            lowCoverageRegions.Add($"{vertical} {horizontal}");
                        }
                    }
                }

                if (lowCoverageRegions.Count == 0)
                {
                    // All regions are fairly even
                    return L.Get("fingerprint.powder_coverage_even");
                }
                else if (lowCoverageRegions.Count <= 3)
                {
                    // Report specific regions
                    string regions = string.Join(", ", lowCoverageRegions.ToArray());
                    return L.Get("fingerprint.powder_coverage_regions", regions);
                }
                else
                {
                    // Too many low regions - not useful to list them all
                    return null;
                }
            }
        }

        /// <summary>
        /// Analyzes regional powder coverage to help users find areas needing more powder.
        /// </summary>
        private static RegionalCoverageInfo GetRegionalCoverageInfo()
        {
            try
            {
                if (FingerMiniGame.instance == null || !FingerMiniGame.instance.is_running)
                    return null;

                // Get mask data via reflection
                var maskDataField = typeof(FingerMiniGame).GetField(
                    "mask_data_",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );
                var maskWidthField = typeof(FingerMiniGame).GetField(
                    "mask_width_",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );
                var maskHeightField = typeof(FingerMiniGame).GetField(
                    "mask_height_",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );
                var tempTextureField = typeof(FingerMiniGame).GetField(
                    "temporary_texture_",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );
                var renderTextureField = typeof(FingerMiniGame).GetField(
                    "render_texture_",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );

                if (
                    maskDataField == null
                    || maskWidthField == null
                    || maskHeightField == null
                    || tempTextureField == null
                )
                    return null;

                byte[] maskData = maskDataField.GetValue(FingerMiniGame.instance) as byte[];
                int maskWidth = (int)maskWidthField.GetValue(FingerMiniGame.instance);
                int maskHeight = (int)maskHeightField.GetValue(FingerMiniGame.instance);
                var tempTexture = tempTextureField.GetValue(FingerMiniGame.instance) as Texture2D;
                var renderTexture =
                    renderTextureField?.GetValue(FingerMiniGame.instance) as RenderTexture;

                if (maskData == null || tempTexture == null || maskWidth <= 0 || maskHeight <= 0)
                    return null;

                // Read current pixel data from render texture for regional analysis
                Color32[] pixels = null;
                if (renderTexture != null)
                {
                    // Save current render target
                    var previousRT = RenderTexture.active;
                    RenderTexture.active = renderTexture;

                    // Read pixels into a temporary texture
                    var screenRect = new Rect(
                        0f,
                        0f,
                        systemCtrl.instance.ScreenWidth,
                        systemCtrl.instance.ScreenHeight
                    );
                    tempTexture.ReadPixels(
                        screenRect,
                        0,
                        (int)((float)tempTexture.height - screenRect.height)
                    );
                    pixels = tempTexture.GetPixels32();

                    // Restore render target
                    RenderTexture.active = previousRT;
                }
                else
                {
                    // Fall back to reading from temp texture directly
                    pixels = tempTexture.GetPixels32();
                }

                if (pixels == null)
                    return null;

                // Calculate 3x3 grid coverage for regional breakdown
                int[,] regionRequired = new int[3, 3];
                int[,] regionCovered = new int[3, 3];

                int cellWidth = maskWidth / 3;
                int cellHeight = maskHeight / 3;

                int screenWidth = (int)systemCtrl.instance.ScreenWidth;
                int screenHeight = (int)systemCtrl.instance.ScreenHeight;
                int endX = System.Math.Min(screenWidth, maskWidth);
                int endY = System.Math.Min(screenHeight, maskHeight);

                int textureYOffset =
                    (int)((float)tempTexture.height - screenHeight) * tempTexture.width;

                for (int y = 0; y < endY; y++)
                {
                    int row = System.Math.Min(y / cellHeight, 2);
                    int maskYIndex = y * maskWidth;
                    int pixelYIndex = textureYOffset + y * tempTexture.width;

                    for (int x = 0; x < endX; x++)
                    {
                        int col = System.Math.Min(x / cellWidth, 2);
                        int maskIndex = maskYIndex + x;
                        int pixelIndex = pixelYIndex + x;

                        if (maskIndex >= maskData.Length || pixelIndex >= pixels.Length)
                            continue;

                        byte maskValue = maskData[maskIndex];
                        if (maskValue > 0)
                        {
                            regionRequired[row, col]++;

                            if (pixels[pixelIndex].a >= maskValue)
                            {
                                regionCovered[row, col]++;
                            }
                        }
                    }
                }

                var info = new RegionalCoverageInfo
                {
                    RegionPercentages = new int[3, 3],
                    MaskWidth = maskWidth,
                    MaskHeight = maskHeight,
                };

                for (int row = 0; row < 3; row++)
                {
                    for (int col = 0; col < 3; col++)
                    {
                        if (regionRequired[row, col] > 0)
                        {
                            info.RegionPercentages[row, col] = (int)(
                                (float)regionCovered[row, col] / regionRequired[row, col] * 100f
                            );
                        }
                        else
                        {
                            info.RegionPercentages[row, col] = 100; // No mask data = fully covered
                        }
                    }
                }

                return info;
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error getting regional coverage: {ex.Message}"
                );
                return null;
            }
        }

        private static void AnnounceComparisonHint()
        {
            SpeechManager.Announce(
                L.Get("fingerprint.comparison_hint"),
                GameTextType.Investigation
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

                        string name = GetComparisonCharacterName(cursor);
                        if (name != null)
                        {
                            SpeechManager.Announce(name, GameTextType.Investigation);
                        }
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
                SpeechManager.Announce(
                    L.Get("fingerprint.not_in_mode"),
                    GameTextType.SystemMessage
                );
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
                        string name = GetComparisonCharacterName(cursor);

                        if (name != null)
                        {
                            SpeechManager.Announce(
                                L.Get("fingerprint.comparison_state", name),
                                GameTextType.Investigation
                            );
                            return;
                        }
                    }
                }
                catch
                {
                    // Fall through
                }

                SpeechManager.Announce(
                    L.Get("fingerprint.comparison_phase"),
                    GameTextType.Investigation
                );
            }
            else if (IsInPowderPhase())
            {
                SpeechManager.Announce(
                    L.Get("fingerprint.powder_state"),
                    GameTextType.Investigation
                );
            }
            else if (IsInSelectionPhase())
            {
                int count = GetFingerprintLocationCount();
                string locationInfo =
                    _currentLocationIndex >= 0
                        ? L.Get("fingerprint.location_x_of_y", _currentLocationIndex + 1, count)
                            + " "
                        : L.Get("fingerprint.location_count", count) + " ";
                SpeechManager.Announce(
                    L.Get("fingerprint.selection_state", locationInfo),
                    GameTextType.Investigation
                );
            }
            else
            {
                SpeechManager.Announce(
                    L.Get("fingerprint.examination_state"),
                    GameTextType.Investigation
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
                SpeechManager.Announce(
                    L.Get("fingerprint.not_in_mode"),
                    GameTextType.SystemMessage
                );
                return;
            }

            if (!IsInSelectionPhase())
            {
                SpeechManager.Announce(
                    L.Get("fingerprint.selection_only"),
                    GameTextType.SystemMessage
                );
                return;
            }

            int count = GetFingerprintLocationCount();
            if (count == 0)
            {
                SpeechManager.Announce(
                    L.Get("fingerprint.no_locations"),
                    GameTextType.Investigation
                );
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
                SpeechManager.Announce(
                    L.Get("fingerprint.not_in_mode"),
                    GameTextType.SystemMessage
                );
                return;
            }

            if (!IsInSelectionPhase())
            {
                SpeechManager.Announce(
                    L.Get("fingerprint.selection_only"),
                    GameTextType.SystemMessage
                );
                return;
            }

            int count = GetFingerprintLocationCount();
            if (count == 0)
            {
                SpeechManager.Announce(
                    L.Get("fingerprint.no_locations"),
                    GameTextType.Investigation
                );
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
                    L.Get(
                        "fingerprint.location_announce",
                        _currentLocationIndex + 1,
                        count,
                        positionDesc
                    ),
                    GameTextType.Investigation
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
                x < areaWidth * 0.33f ? L.Get("position.left")
                : x > areaWidth * 0.66f ? L.Get("position.right")
                : L.Get("position.center");
            string vertical =
                y < areaHeight * 0.33f ? L.Get("position.top")
                : y > areaHeight * 0.66f ? L.Get("position.bottom")
                : L.Get("position.middle");

            return string.Format("{0} {1}", vertical, horizontal);
        }
    }
}
