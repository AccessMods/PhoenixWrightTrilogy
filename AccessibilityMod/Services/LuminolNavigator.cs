using System;
using System.Collections.Generic;
using System.Reflection;
using AccessibilityMod.Core;
using UnityAccessibilityLib;
using UnityEngine;

namespace AccessibilityMod.Services
{
    /// <summary>
    /// Handles navigation for the Luminol spray mini-game where the player
    /// must find blood traces by spraying luminol on the scene.
    /// </summary>
    public static class LuminolNavigator
    {
        private static List<HotspotInfo> _hotspots = new List<HotspotInfo>();
        private static int _currentIndex = -1;
        private static bool _wasActive = false;
        private static float _lastBgPosX = 0f;
        private static bool _wasInAcquiredState = false;
        private static FieldInfo _stateField = null;

        public class HotspotInfo
        {
            public int Index;
            public float CenterX;
            public float CenterY;
            public string Description;
            public bool IsDiscovered;
            public bool IsOnRightSide; // True if on right side (visible when bg_pos_x < 500)
            public luminolBloodstain Bloodstain;
        }

        /// <summary>
        /// Checks if the Luminol mini-game is currently active.
        /// </summary>
        public static bool IsLuminolActive()
        {
            try
            {
                if (luminolMiniGame.instance != null)
                {
                    // is_end is false when the mini-game is running
                    return !luminolMiniGame.instance.is_end;
                }
            }
            catch
            {
                // Class may not exist in current context
            }
            return false;
        }

        /// <summary>
        /// Called each frame to detect when luminol mode starts/ends.
        /// </summary>
        public static void Update()
        {
            bool isActive = IsLuminolActive();

            if (isActive && !_wasActive)
            {
                // Luminol mode just started
                OnLuminolStart();
            }
            else if (!isActive && _wasActive)
            {
                // Luminol mode just ended
                OnLuminolEnd();
            }
            else if (isActive)
            {
                // Check if scroll position changed - refresh if so
                try
                {
                    float currentBgPosX = bgCtrl.instance.bg_pos_x;
                    if (Math.Abs(currentBgPosX - _lastBgPosX) > 100f)
                    {
                        _lastBgPosX = currentBgPosX;
                        RefreshHotspots();
                    }
                }
                catch { }

                // Check if we entered the "Acquired" state (waiting for Enter to examine)
                CheckAcquiredState();
            }

            _wasActive = isActive;
        }

        /// <summary>
        /// Checks if the luminol minigame entered the Acquired state and announces the prompt.
        /// </summary>
        private static void CheckAcquiredState()
        {
            try
            {
                if (luminolMiniGame.instance == null)
                    return;

                // Cache the state field via reflection
                if (_stateField == null)
                {
                    _stateField = typeof(luminolMiniGame).GetField(
                        "state",
                        BindingFlags.NonPublic | BindingFlags.Instance
                    );
                }

                if (_stateField == null)
                    return;

                var stateValue = _stateField.GetValue(luminolMiniGame.instance);
                bool isInAcquiredState =
                    stateValue != null && (luminolState)stateValue == luminolState.Acquired;

                if (isInAcquiredState && !_wasInAcquiredState)
                {
                    // Just entered Acquired state - announce prompt
                    SpeechManager.Announce(
                        L.Get("navigation.press_enter_examine"),
                        GameTextType.Investigation
                    );
                }

                _wasInAcquiredState = isInAcquiredState;
            }
            catch
            {
                // Silently ignore errors reading state
            }
        }

        private static void OnLuminolStart()
        {
            try
            {
                _lastBgPosX = bgCtrl.instance.bg_pos_x;
            }
            catch
            {
                _lastBgPosX = 0f;
            }

            RefreshHotspots();

            if (_hotspots.Count > 0)
            {
                int undiscoveredCount = 0;
                foreach (var h in _hotspots)
                {
                    if (!h.IsDiscovered)
                        undiscoveredCount++;
                }

                string scrollHint = IsScrollableBackground() ? " " + L.Get("luminol.pan_hint") : "";
                string message =
                    L.Get("luminol.mode_entry", undiscoveredCount)
                    + " "
                    + L.Get("navigation.use_brackets_navigate")
                    + ", "
                    + L.Get("luminol.spray_key_hint")
                    + scrollHint;
                SpeechManager.Announce(message, GameTextType.Investigation);
            }
            else
            {
                SpeechManager.Announce(
                    L.Get("mode.luminol") + ". " + L.Get("luminol.controls_hint_generic"),
                    GameTextType.Investigation
                );
            }
        }

        private static void OnLuminolEnd()
        {
            _hotspots.Clear();
            _currentIndex = -1;
            _wasInAcquiredState = false;
        }

        /// <summary>
        /// Check if the current background is scrollable (has left/right panning).
        /// </summary>
        private static bool IsScrollableBackground()
        {
            try
            {
                int bgNo = bgCtrl.instance.bg_no;
                return GSMain_TanteiPart.IsBGSlide(bgNo);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Refreshes the list of hotspots from the game data.
        /// Uses blood_ list to get ALL bloodstains, not just active ones.
        /// </summary>
        public static void RefreshHotspots()
        {
            _hotspots.Clear();
            _currentIndex = -1;

            try
            {
                if (luminolMiniGame.instance == null || luminolMiniGame.instance.is_end)
                    return;

                // Get blood_ field (ALL bloodstains) via reflection
                var bloodField = typeof(luminolMiniGame).GetField(
                    "blood_",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );

                List<luminolBloodstain> allBlood = null;
                if (bloodField != null)
                {
                    allBlood =
                        bloodField.GetValue(luminolMiniGame.instance) as List<luminolBloodstain>;
                }

                if (allBlood == null || allBlood.Count == 0)
                {
#if DEBUG
                    AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                        "[Luminol] No bloodstains found in blood_ list"
                    );
#endif
                    return;
                }

                // Get blood_parent_right_ and blood_parent_left_ to determine which side
                var rightParentField = typeof(luminolMiniGame).GetField(
                    "blood_parent_right_",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );
                var leftParentField = typeof(luminolMiniGame).GetField(
                    "blood_parent_left_",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );

                Transform rightParent = null;
                Transform leftParent = null;
                if (rightParentField != null)
                    rightParent = rightParentField.GetValue(luminolMiniGame.instance) as Transform;
                if (leftParentField != null)
                    leftParent = leftParentField.GetValue(luminolMiniGame.instance) as Transform;

                for (int i = 0; i < allBlood.Count; i++)
                {
                    var bloodstain = allBlood[i];
                    if (bloodstain == null)
                        continue;

                    // Get position from transform
                    Vector3 localPos = bloodstain.transform.localPosition;

                    // Determine which side this bloodstain is on
                    bool isOnRightSide = false;
                    if (bloodstain.transform.parent == rightParent)
                    {
                        isOnRightSide = true;
                    }

                    // Convert local position to cursor coordinates
                    // The bloodstain local position uses the format from luminolTable
                    // pos_x is positive, pos_y is negative (inverted Y)
                    float centerX = localPos.x;
                    float centerY = -localPos.y; // Invert Y for cursor coordinates

                    // Check if already discovered
                    bool isDiscovered = bloodstain.state_ != BloodstainState.Undiscovered;

                    // Generate position description
                    string posDesc = GetPositionDescription(centerX, centerY);
                    string sideDesc = "";
                    if (IsScrollableBackground())
                    {
                        sideDesc = isOnRightSide
                            ? ", " + L.Get("position.right_side")
                            : ", " + L.Get("position.left_side");
                    }
                    string status = isDiscovered
                        ? " (" + L.Get("luminol.blood_trace_found") + ")"
                        : "";

                    string description =
                        L.Get("luminol.blood_trace_position", i + 1, posDesc + sideDesc) + status;
                    _hotspots.Add(
                        new HotspotInfo
                        {
                            Index = i,
                            CenterX = centerX,
                            CenterY = centerY,
                            Description = description,
                            IsDiscovered = isDiscovered,
                            IsOnRightSide = isOnRightSide,
                            Bloodstain = bloodstain,
                        }
                    );
                }

#if DEBUG
                AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                    $"[Luminol] Found {_hotspots.Count} blood traces total"
                );
#endif
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error refreshing luminol hotspots: {ex.Message}"
                );
            }
        }

        private static string GetPositionDescription(float x, float y)
        {
            // Screen is 1920x1080
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
            return $"{vertical} {horizontal}";
        }

        /// <summary>
        /// Navigate to the next undiscovered hotspot.
        /// </summary>
        public static void NavigateNext()
        {
            if (!IsLuminolActive())
            {
                SpeechManager.Announce(
                    L.Get("system.not_in_mode", L.Get("mode.luminol")),
                    GameTextType.SystemMessage
                );
                return;
            }

            if (_hotspots.Count == 0)
            {
                RefreshHotspots();
            }

            if (_hotspots.Count == 0)
            {
                SpeechManager.Announce(L.Get("luminol.no_traces"), GameTextType.Investigation);
                return;
            }

            // Find next undiscovered hotspot, wrapping around
            int startIndex = _currentIndex;
            bool foundUndiscovered = false;

            // First try to find an undiscovered one
            for (int i = 0; i < _hotspots.Count; i++)
            {
                int idx = (startIndex + 1 + i) % _hotspots.Count;
                if (!_hotspots[idx].IsDiscovered)
                {
                    _currentIndex = idx;
                    foundUndiscovered = true;
                    break;
                }
            }

            // If all are discovered, just go to next
            if (!foundUndiscovered)
            {
                _currentIndex = (_currentIndex + 1) % _hotspots.Count;
            }

            NavigateToCurrentHotspot();
        }

        /// <summary>
        /// Navigate to the previous undiscovered hotspot.
        /// </summary>
        public static void NavigatePrevious()
        {
            if (!IsLuminolActive())
            {
                SpeechManager.Announce(
                    L.Get("system.not_in_mode", L.Get("mode.luminol")),
                    GameTextType.SystemMessage
                );
                return;
            }

            if (_hotspots.Count == 0)
            {
                RefreshHotspots();
            }

            if (_hotspots.Count == 0)
            {
                SpeechManager.Announce(L.Get("luminol.no_traces"), GameTextType.Investigation);
                return;
            }

            // Find previous undiscovered hotspot, wrapping around
            int startIndex = _currentIndex < 0 ? 0 : _currentIndex;
            bool foundUndiscovered = false;

            // First try to find an undiscovered one
            for (int i = 0; i < _hotspots.Count; i++)
            {
                int idx = (startIndex - 1 - i + _hotspots.Count * 2) % _hotspots.Count;
                if (!_hotspots[idx].IsDiscovered)
                {
                    _currentIndex = idx;
                    foundUndiscovered = true;
                    break;
                }
            }

            // If all are discovered, just go to previous
            if (!foundUndiscovered)
            {
                // When starting from -1 (no selection), go to the last item
                if (_currentIndex < 0)
                {
                    _currentIndex = _hotspots.Count - 1;
                }
                else
                {
                    _currentIndex = (_currentIndex - 1 + _hotspots.Count) % _hotspots.Count;
                }
            }

            NavigateToCurrentHotspot();
        }

        /// <summary>
        /// Navigate to the current hotspot, scrolling if necessary.
        /// </summary>
        private static void NavigateToCurrentHotspot()
        {
            if (_hotspots.Count == 0 || _currentIndex < 0 || _currentIndex >= _hotspots.Count)
                return;

            var hotspot = _hotspots[_currentIndex];

            // Check if we need to scroll to the other side
            bool needsScroll = false;
            try
            {
                if (IsScrollableBackground())
                {
                    float bgPosX = bgCtrl.instance.bg_pos_x;
                    bool viewingRightSide = bgPosX < 500f;

                    if (hotspot.IsOnRightSide && !viewingRightSide)
                    {
                        needsScroll = true;
                    }
                    else if (!hotspot.IsOnRightSide && viewingRightSide)
                    {
                        needsScroll = true;
                    }
                }
            }
            catch { }

            if (needsScroll)
            {
                // Tell user to scroll, then announce hotspot
                string scrollDir = hotspot.IsOnRightSide
                    ? L.Get("position.right")
                    : L.Get("position.left");
                SpeechManager.Announce(
                    hotspot.Description + ". " + L.Get("luminol.pan_direction_first", scrollDir),
                    GameTextType.Investigation
                );
            }
            else
            {
                // On correct side, move cursor directly
                AnnounceCurrentHotspot();
                MoveCursorToCurrentHotspot();
            }
        }

        /// <summary>
        /// Announce the currently selected hotspot.
        /// </summary>
        public static void AnnounceCurrentHotspot()
        {
            if (_hotspots.Count == 0 || _currentIndex < 0 || _currentIndex >= _hotspots.Count)
            {
                SpeechManager.Announce(
                    L.Get("navigation.no_point_selected"),
                    GameTextType.Investigation
                );
                return;
            }

            var hotspot = _hotspots[_currentIndex];
            SpeechManager.Announce(hotspot.Description, GameTextType.Investigation);
        }

        /// <summary>
        /// Move the cursor to the currently selected hotspot.
        /// </summary>
        private static void MoveCursorToCurrentHotspot()
        {
            if (_hotspots.Count == 0 || _currentIndex < 0 || _currentIndex >= _hotspots.Count)
                return;

            try
            {
                var hotspot = _hotspots[_currentIndex];

                // Get cursor_position_ field via reflection
                var cursorPosField = typeof(luminolMiniGame).GetField(
                    "cursor_position_",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );

                if (cursorPosField != null)
                {
                    // Set cursor position
                    Vector3 newPos = new Vector3(hotspot.CenterX, hotspot.CenterY, 0f);
                    cursorPosField.SetValue(luminolMiniGame.instance, newPos);

                    // Call UpdateCursorPosition to apply the change
                    var updateMethod = typeof(luminolMiniGame).GetMethod(
                        "UpdateCursorPosition",
                        BindingFlags.NonPublic | BindingFlags.Instance
                    );

                    if (updateMethod != null)
                    {
                        updateMethod.Invoke(luminolMiniGame.instance, null);
                    }

#if DEBUG
                    AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                        $"[Luminol] Moved cursor to ({hotspot.CenterX}, {hotspot.CenterY})"
                    );
#endif
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error moving luminol cursor: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Called when a bloodstain is discovered to update state and announce.
        /// </summary>
        public static void OnBloodstainDiscovered(int index)
        {
            // Refresh to update discovered state
            RefreshHotspots();

            // Count remaining
            int remaining = 0;
            foreach (var h in _hotspots)
            {
                if (!h.IsDiscovered)
                    remaining++;
            }

            string message =
                remaining > 0
                    ? L.Get("luminol.trace_discovered", remaining)
                    : L.Get("luminol.all_discovered");

            SpeechManager.Announce(message, GameTextType.Investigation);
        }

        /// <summary>
        /// Gets the number of hotspots.
        /// </summary>
        public static int GetHotspotCount()
        {
            if (_hotspots.Count == 0 && IsLuminolActive())
            {
                RefreshHotspots();
            }
            return _hotspots.Count;
        }

        /// <summary>
        /// Gets the number of undiscovered hotspots.
        /// </summary>
        public static int GetUndiscoveredCount()
        {
            if (_hotspots.Count == 0 && IsLuminolActive())
            {
                RefreshHotspots();
            }

            int count = 0;
            foreach (var h in _hotspots)
            {
                if (!h.IsDiscovered)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// Announce current state for I key press.
        /// </summary>
        public static void AnnounceState()
        {
            if (!IsLuminolActive())
            {
                SpeechManager.Announce(
                    L.Get("system.not_in_mode", L.Get("mode.luminol")),
                    GameTextType.SystemMessage
                );
                return;
            }

            RefreshHotspots();

            int total = _hotspots.Count;
            int remaining = GetUndiscoveredCount();

            string scrollHint = IsScrollableBackground() ? " " + L.Get("luminol.pan_hint") : "";
            string message =
                L.Get("mode.luminol")
                + ". "
                + L.Get("luminol.traces_remaining", remaining, total)
                + " "
                + L.Get("navigation.use_brackets_navigate")
                + "."
                + scrollHint;
            SpeechManager.Announce(message, GameTextType.Investigation);
        }
    }
}
