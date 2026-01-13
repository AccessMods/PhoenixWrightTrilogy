using System;
using System.Collections.Generic;
using System.Reflection;
using AccessibilityMod.Core;
using UnityAccessibilityLib;

namespace AccessibilityMod.Services
{
    /// <summary>
    /// Handles accessibility for the bug sweeper (Tanchiki) minigame where the player
    /// scans for listening devices using a signal detector.
    /// </summary>
    public static class BugSweeperNavigator
    {
        private static bool _wasActive = false;
        private static int _lastSignalLevel = 0;
        private static int _lastTargetIndex = -1;
        private static float _lastBgPosX = 0f;

        // Separate checked target lists for each side (persist across minigame activations)
        private static HashSet<int> _checkedTargetsLeft = new HashSet<int>();
        private static HashSet<int> _checkedTargetsRight = new HashSet<int>();

        // Cache for radio_wave_ field access
        private static FieldInfo _radioWaveField = null;

        // Cached target counts per side (calculated once per scene)
        private static int _leftSideTargetCount = 0;
        private static int _rightSideTargetCount = 0;
        private static bool _targetCountsCalculated = false;

        /// <summary>
        /// Checks if the bug sweeper minigame is currently active.
        /// </summary>
        public static bool IsBugSweeperActive()
        {
            try
            {
                if (TanchikiMiniGame.instance == null)
                    return false;

                // Check if radio wave indicator is active (indicates minigame is running)
                if (_radioWaveField == null)
                {
                    _radioWaveField = typeof(TanchikiMiniGame).GetField(
                        "radio_wave_",
                        BindingFlags.NonPublic | BindingFlags.Instance
                    );
                }

                if (_radioWaveField != null)
                {
                    var radioWave =
                        _radioWaveField.GetValue(TanchikiMiniGame.instance) as TanchikiRadioWave;
                    if (radioWave != null && radioWave.active)
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // Class may not exist in current context
            }
            return false;
        }

        /// <summary>
        /// Called each frame to detect mode changes and state updates.
        /// </summary>
        public static void Update()
        {
            bool isActive = IsBugSweeperActive();

            if (isActive && !_wasActive)
            {
                OnBugSweeperStart();
            }
            else if (!isActive && _wasActive)
            {
                OnBugSweeperEnd();
            }
            else if (isActive)
            {
                // Check for signal level changes
                int currentLevel = GetSignalLevel();
                if (currentLevel != _lastSignalLevel && currentLevel > 0)
                {
                    OnSignalLevelChanged(currentLevel);
                    _lastSignalLevel = currentLevel;
                }

                // Check for target hover changes (when level reaches 5)
                if (currentLevel == 5)
                {
                    int targetIndex = GetCurrentTargetIndex();
                    if (targetIndex != _lastTargetIndex && targetIndex >= 0)
                    {
                        OnTargetHover(targetIndex);
                        _lastTargetIndex = targetIndex;
                    }
                }
                else
                {
                    // Reset target tracking when not on a target
                    _lastTargetIndex = -1;
                }

                // Track background position changes
                try
                {
                    _lastBgPosX = bgCtrl.instance.bg_pos_x;
                }
                catch { }
            }

            _wasActive = isActive;
        }

        private static void OnBugSweeperStart()
        {
            try
            {
                _lastBgPosX = bgCtrl.instance.bg_pos_x;
            }
            catch
            {
                _lastBgPosX = 0f;
            }

            _lastSignalLevel = 0;
            _lastTargetIndex = -1;
            // Note: Do NOT clear checked target lists - minigame reactivates after each check

            // Calculate target counts if not already done
            CalculateTargetCounts();

            string targetInfo = "";
            if (IsScrollableBackground())
            {
                int targetCount = GetCurrentSideTargetCount();
                string side = IsOnRightSide() ? L.Get("position.right") : L.Get("position.left");
                targetInfo = L.Get("bug_sweeper.targets_on_side", targetCount, side);
            }
            else
            {
                int totalTargets =
                    TanchikiMiniGame.find_target != null
                        ? TanchikiMiniGame.find_target.Length - 1
                        : 0;
                targetInfo = L.Get("bug_sweeper.targets_to_find", totalTargets);
            }

            string scrollHint = IsScrollableBackground() ? L.Get("bug_sweeper.pan_hint") : "";
            string message =
                L.Get("mode.bug_sweeper")
                + ". "
                + targetInfo
                + " "
                + L.Get("bug_sweeper.move_to_scan")
                + scrollHint;
            SpeechManager.Announce(message, GameTextType.Investigation);
        }

        private static void OnBugSweeperEnd()
        {
            // Minimal cleanup - preserve checked lists across activations
            _lastSignalLevel = 0;
            _lastTargetIndex = -1;
        }

        /// <summary>
        /// Check if the current background is scrollable.
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
        /// Calculate and cache the number of targets on each side.
        /// Targets are split based on their center X coordinate relative to the background midpoint.
        /// </summary>
        private static void CalculateTargetCounts()
        {
            if (_targetCountsCalculated)
                return;

            _leftSideTargetCount = 0;
            _rightSideTargetCount = 0;

            try
            {
                var targets = TanchikiMiniGame.find_target;
                if (targets == null || targets.Length <= 1)
                    return;

                // Find the X coordinate range to determine the midpoint
                float minX = float.MaxValue;
                float maxX = float.MinValue;

                // Exclude the last target (it's the "no hit" fallback area)
                for (int i = 0; i < targets.Length - 1; i++)
                {
                    var target = targets[i];
                    float centerX = (target.x0 + target.x1 + target.x2 + target.x3) / 4f;
                    if (centerX < minX)
                        minX = centerX;
                    if (centerX > maxX)
                        maxX = centerX;
                }

                // Midpoint for splitting left/right
                float midX = (minX + maxX) / 2f;

                // Count targets on each side
                for (int i = 0; i < targets.Length - 1; i++)
                {
                    var target = targets[i];
                    float centerX = (target.x0 + target.x1 + target.x2 + target.x3) / 4f;

                    if (centerX < midX)
                    {
                        _leftSideTargetCount++;
                    }
                    else
                    {
                        _rightSideTargetCount++;
                    }
                }

                _targetCountsCalculated = true;

#if DEBUG
                AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                    $"[BugSweeper] Target counts - Left: {_leftSideTargetCount}, Right: {_rightSideTargetCount}, MidX: {midX}"
                );
#endif
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error calculating target counts: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Get the total number of targets on the current side.
        /// </summary>
        private static int GetCurrentSideTargetCount()
        {
            if (!_targetCountsCalculated)
                CalculateTargetCounts();

            if (!IsScrollableBackground())
            {
                // Non-scrollable: return total targets
                var targets = TanchikiMiniGame.find_target;
                return targets != null ? targets.Length - 1 : 0;
            }

            return IsOnRightSide() ? _rightSideTargetCount : _leftSideTargetCount;
        }

        /// <summary>
        /// Get the current signal level (1-5) from the radio wave indicator.
        /// </summary>
        private static int GetSignalLevel()
        {
            try
            {
                if (TanchikiMiniGame.instance == null)
                    return 0;

                // Cache the field info
                if (_radioWaveField == null)
                {
                    _radioWaveField = typeof(TanchikiMiniGame).GetField(
                        "radio_wave_",
                        BindingFlags.NonPublic | BindingFlags.Instance
                    );
                }

                if (_radioWaveField != null)
                {
                    var radioWave =
                        _radioWaveField.GetValue(TanchikiMiniGame.instance) as TanchikiRadioWave;
                    if (radioWave != null)
                    {
                        return radioWave.level;
                    }
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error getting signal level: {ex.Message}"
                );
            }
            return 0;
        }

        /// <summary>
        /// Get the current target index from hit detection.
        /// Returns -1 if not over a target.
        /// </summary>
        private static int GetCurrentTargetIndex()
        {
            try
            {
                if (TanchikiMiniGame.instance == null)
                    return -1;

                int result = TanchikiMiniGame.instance.hit_check();
                // hit_check returns find_target.Length - 1 when not on a target
                if (result >= 0 && result < TanchikiMiniGame.find_target.Length - 1)
                {
                    return result;
                }
            }
            catch { }
            return -1;
        }

        /// <summary>
        /// Determine which side of the scrollable background we're on.
        /// </summary>
        private static bool IsOnRightSide()
        {
            try
            {
                // When bg_pos_x is low (< 500), we're viewing the right side
                return bgCtrl.instance.bg_pos_x < 500f;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Called when signal level changes.
        /// </summary>
        private static void OnSignalLevelChanged(int level)
        {
            string description = GetSignalDescription(level);
            SpeechManager.Announce(description, GameTextType.Investigation);
        }

        /// <summary>
        /// Get human-readable description for signal level.
        /// </summary>
        private static string GetSignalDescription(int level)
        {
            switch (level)
            {
                case 1:
                    return L.Get("bug_sweeper.signal_very_weak");
                case 2:
                    return L.Get("bug_sweeper.signal_weak");
                case 3:
                    return L.Get("bug_sweeper.signal_moderate");
                case 4:
                    return L.Get("bug_sweeper.signal_strong");
                case 5:
                    return L.Get("bug_sweeper.target_found");
                default:
                    return L.Get("bug_sweeper.signal_none");
            }
        }

        /// <summary>
        /// Called when hovering over a target (signal level 5).
        /// </summary>
        private static void OnTargetHover(int targetIndex)
        {
            bool isChecked = IsTargetChecked(targetIndex);
            if (isChecked)
            {
                SpeechManager.Announce(
                    L.Get("bug_sweeper.already_checked"),
                    GameTextType.Investigation
                );
            }
            else
            {
                SpeechManager.Announce(
                    L.Get("bug_sweeper.press_enter_check"),
                    GameTextType.Investigation
                );
            }
        }

        /// <summary>
        /// Check if a target has been checked on the current side.
        /// </summary>
        private static bool IsTargetChecked(int targetIndex)
        {
            if (IsScrollableBackground())
            {
                if (IsOnRightSide())
                {
                    return _checkedTargetsRight.Contains(targetIndex);
                }
                else
                {
                    return _checkedTargetsLeft.Contains(targetIndex);
                }
            }
            else
            {
                // Non-scrollable background - use left set as default
                return _checkedTargetsLeft.Contains(targetIndex);
            }
        }

        /// <summary>
        /// Mark a specific target as checked.
        /// Called when player confirms a target selection.
        /// </summary>
        /// <param name="targetIndex">The target index from hit_check result</param>
        public static void MarkTargetChecked(int targetIndex)
        {
            if (targetIndex < 0)
                return;

            if (IsScrollableBackground())
            {
                if (IsOnRightSide())
                {
                    _checkedTargetsRight.Add(targetIndex);
#if DEBUG
                    AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                        $"[BugSweeper] Marked target {targetIndex} as checked on RIGHT side"
                    );
#endif
                }
                else
                {
                    _checkedTargetsLeft.Add(targetIndex);
#if DEBUG
                    AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                        $"[BugSweeper] Marked target {targetIndex} as checked on LEFT side"
                    );
#endif
                }
            }
            else
            {
                _checkedTargetsLeft.Add(targetIndex);
#if DEBUG
                AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                    $"[BugSweeper] Marked target {targetIndex} as checked"
                );
#endif
            }
        }

        /// <summary>
        /// Announce current state for I key press.
        /// </summary>
        public static void AnnounceState()
        {
            if (!IsBugSweeperActive())
            {
                SpeechManager.Announce(
                    L.Get("bug_sweeper.not_in_mode"),
                    GameTextType.SystemMessage
                );
                return;
            }

            int level = GetSignalLevel();
            string levelDesc = GetSignalDescription(level);

            int checkedCount = IsScrollableBackground()
                ? (IsOnRightSide() ? _checkedTargetsRight.Count : _checkedTargetsLeft.Count)
                : _checkedTargetsLeft.Count;

            int totalCount = GetCurrentSideTargetCount();

            string checkedInfo;
            if (IsScrollableBackground())
            {
                string side = IsOnRightSide() ? L.Get("position.right") : L.Get("position.left");
                checkedInfo = L.Get(
                    "bug_sweeper.checked_x_of_y_side",
                    checkedCount,
                    totalCount,
                    side
                );
            }
            else
            {
                checkedInfo = L.Get("bug_sweeper.checked_x_of_y", checkedCount, totalCount);
            }

            string scrollHint = IsScrollableBackground() ? " " + L.Get("luminol.pan_hint") : "";

            string message = L.Get("bug_sweeper.state", levelDesc, checkedInfo, scrollHint);
            SpeechManager.Announce(message, GameTextType.Investigation);
        }

        /// <summary>
        /// Clear all checked targets (for new game/chapter).
        /// </summary>
        public static void ClearCheckedTargets()
        {
            _checkedTargetsLeft.Clear();
            _checkedTargetsRight.Clear();
            _targetCountsCalculated = false;
            _leftSideTargetCount = 0;
            _rightSideTargetCount = 0;
        }
    }
}
