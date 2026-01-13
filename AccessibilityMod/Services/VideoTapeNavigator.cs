using System;
using System.Collections.Generic;
using System.Reflection;
using AccessibilityMod.Core;
using UnityAccessibilityLib;
using UnityEngine;

namespace AccessibilityMod.Services
{
    /// <summary>
    /// Provides accessibility support for the video tape examination minigame.
    /// Helps with frame navigation, play/pause state, and target detection.
    /// </summary>
    public static class VideoTapeNavigator
    {
        private static bool _wasActive = false;
        private static bool _wasPlaying = false;
        private static int _lastFrame = -1;
        private static int _lastTargetCount = 0;
        private static int _currentTargetIndex = -1;

        /// <summary>
        /// Checks if the video tape examination is currently active in interactive mode.
        /// Returns false when the video is playing non-interactively (e.g., after presenting evidence).
        /// </summary>
        public static bool IsVideoTapeActive()
        {
            try
            {
                if (ConfrontWithMovie.instance != null)
                {
                    var controller = ConfrontWithMovie.instance.movie_controller;
                    if (controller == null || !controller.is_play)
                        return false;

                    // Check if we're in interactive mode using multiple indicators:
                    // 1. Cursor must be enabled and its game object active
                    // 2. Collision player must be enabled (for target detection)
                    // 3. Must not be in auto_play mode or detail mode
                    var cursor = ConfrontWithMovie.instance.cursor;
                    if (cursor == null || !cursor.enabled || !cursor.gameObject.activeSelf)
                        return false;

                    var collisionPlayer = ConfrontWithMovie.instance.collision_player;
                    if (collisionPlayer == null || !collisionPlayer.enabled)
                        return false;

                    // Check we're not in auto-play or detail mode
                    if (
                        ConfrontWithMovie.instance.auto_play
                        || ConfrontWithMovie.instance.IsDetailing
                    )
                        return false;

                    return true;
                }
            }
            catch
            {
                // Class may not exist or not be loaded
            }
            return false;
        }

        /// <summary>
        /// Checks if the video is currently playing (vs paused).
        /// </summary>
        public static bool IsPlaying()
        {
            try
            {
                if (ConfrontWithMovie.instance == null)
                    return false;

                // pSmt.change_flag: 1 = playing, 0 = paused
                var pSmtField = typeof(ConfrontWithMovie).GetField(
                    "pSmt",
                    BindingFlags.Public | BindingFlags.Instance
                );

                if (pSmtField != null)
                {
                    var pSmt = pSmtField.GetValue(ConfrontWithMovie.instance);
                    var changeFlagField = pSmt.GetType().GetField("change_flag");
                    if (changeFlagField != null)
                    {
                        byte changeFlag = (byte)changeFlagField.GetValue(pSmt);
                        return changeFlag == 1;
                    }
                }
            }
            catch
            {
                // Ignore errors
            }
            return false;
        }

        /// <summary>
        /// Gets the current frame number.
        /// </summary>
        public static int GetCurrentFrame()
        {
            try
            {
                if (ConfrontWithMovie.instance != null)
                {
                    var controller = ConfrontWithMovie.instance.movie_controller;
                    if (controller != null)
                    {
                        return controller.Frame;
                    }
                }
            }
            catch
            {
                // Ignore errors
            }
            return 0;
        }

        /// <summary>
        /// Gets the number of currently active targets (collision areas).
        /// </summary>
        public static int GetActiveTargetCount()
        {
            try
            {
                if (ConfrontWithMovie.instance == null)
                    return 0;

                var collisionPlayer = ConfrontWithMovie.instance.collision_player;
                if (collisionPlayer == null)
                    return 0;

                // Get rects_for_serve via IRectHolder interface
                var rectsProperty = typeof(MovieCollisionPlayer).GetProperty("Rects");
                if (rectsProperty != null)
                {
                    var rects =
                        rectsProperty.GetValue(collisionPlayer, null) as IEnumerable<RectTransform>;
                    if (rects != null)
                    {
                        int count = 0;
                        foreach (var rect in rects)
                        {
                            if (rect != null && rect.gameObject.activeSelf)
                                count++;
                        }
                        return count;
                    }
                }
            }
            catch
            {
                // Ignore errors
            }
            return 0;
        }

        /// <summary>
        /// Checks if the cursor is currently over a target.
        /// </summary>
        public static bool IsCursorOverTarget()
        {
            try
            {
                if (ConfrontWithMovie.instance == null)
                    return false;

                var cursor = ConfrontWithMovie.instance.cursor;
                if (cursor == null)
                    return false;

                int collidedNo = cursor.GetCollidedNo();
                // Returns 4 if not over any target (based on code)
                return collidedNo < 4;
            }
            catch
            {
                // Ignore errors
            }
            return false;
        }

        /// <summary>
        /// Gets the target number the cursor is over (0-3), or -1 if none.
        /// </summary>
        public static int GetCursorTarget()
        {
            try
            {
                if (ConfrontWithMovie.instance == null)
                    return -1;

                var cursor = ConfrontWithMovie.instance.cursor;
                if (cursor == null)
                    return -1;

                int collidedNo = cursor.GetCollidedNo();
                return collidedNo < 4 ? collidedNo : -1;
            }
            catch
            {
                // Ignore errors
            }
            return -1;
        }

        /// <summary>
        /// Called each frame to detect state changes.
        /// </summary>
        public static void Update()
        {
            bool isActive = IsVideoTapeActive();
            bool isPlaying = IsPlaying();

            if (isActive && !_wasActive)
            {
                OnVideoTapeStart();
            }
            else if (!isActive && _wasActive)
            {
                OnVideoTapeEnd();
            }

            if (isActive)
            {
                int currentFrame = GetCurrentFrame();

                // Check for play/pause changes
                if (isPlaying && !_wasPlaying)
                {
                    SpeechManager.Announce(L.Get("video_tape.playing"), GameTextType.Investigation);
                }
                else if (!isPlaying && _wasPlaying)
                {
                    int targetCount = GetActiveTargetCount();
                    string targetInfo =
                        targetCount > 0
                            ? ", " + L.GetPlural("video_tape.targets_available", targetCount)
                            : "";
                    SpeechManager.Announce(
                        L.Get("video_tape.paused_at_frame", currentFrame, targetInfo),
                        GameTextType.Investigation
                    );
                }

                // Check for new targets appearing while playing (only announce first appearance)
                int currentTargetCount = GetActiveTargetCount();
                if (currentTargetCount > 0 && _lastTargetCount == 0)
                {
                    SpeechManager.Announce(
                        L.Get("video_tape.target_available"),
                        GameTextType.Investigation
                    );
                }
                _lastTargetCount = currentTargetCount;
                _lastFrame = currentFrame;
            }

            _wasActive = isActive;
            _wasPlaying = isPlaying;
        }

        private static void OnVideoTapeStart()
        {
            _lastFrame = -1;
            _lastTargetCount = 0;
            _currentTargetIndex = -1;

            SpeechManager.Announce(L.Get("video_tape.start"), GameTextType.Investigation);
        }

        private static void OnVideoTapeEnd()
        {
            _lastFrame = -1;
            _lastTargetCount = 0;
        }

        /// <summary>
        /// Announces the current state.
        /// </summary>
        public static void AnnounceState()
        {
            if (!IsVideoTapeActive())
            {
                SpeechManager.Announce(L.Get("video_tape.not_in_mode"), GameTextType.SystemMessage);
                return;
            }

            int frame = GetCurrentFrame();
            bool playing = IsPlaying();
            int targets = GetActiveTargetCount();
            bool overTarget = IsCursorOverTarget();

            string state = playing
                ? L.Get("video_tape.state_playing")
                : L.Get("video_tape.state_paused");
            string targetInfo =
                targets > 0
                    ? L.GetPlural("video_tape.targets_available", targets)
                    : L.Get("video_tape.no_targets");

            if (overTarget)
            {
                int targetNo = GetCursorTarget();
                targetInfo += L.Get("video_tape.cursor_on_target", targetNo + 1);
            }

            SpeechManager.Announce(
                L.Get("video_tape.state", state, frame, targetInfo),
                GameTextType.Investigation
            );
        }

        /// <summary>
        /// Announces a hint for the video tape examination.
        /// </summary>
        public static void AnnounceHint()
        {
            if (!IsVideoTapeActive())
            {
                SpeechManager.Announce(L.Get("video_tape.not_in_mode"), GameTextType.SystemMessage);
                return;
            }

            // Get which examination this is based on atari_no
            int atariNo = GetAtariNo();
            int frame = GetCurrentFrame();

            string hint;
            switch (atariNo)
            {
                case 0:
                    hint = L.Get("video_tape.hint_first", frame);
                    break;
                case 1:
                    hint = L.Get("video_tape.hint_second", frame);
                    break;
                case 2:
                    hint = L.Get("video_tape.hint_third", frame);
                    break;
                case 3:
                    hint = L.Get("video_tape.hint_fourth", frame);
                    break;
                default:
                    hint = L.Get("video_tape.hint_generic", frame);
                    break;
            }

            SpeechManager.Announce(hint, GameTextType.Investigation);
        }

        /// <summary>
        /// Gets the current atari_no (which examination phase).
        /// </summary>
        private static int GetAtariNo()
        {
            try
            {
                if (ConfrontWithMovie.instance == null)
                    return 0;

                var pSmtField = typeof(ConfrontWithMovie).GetField(
                    "pSmt",
                    BindingFlags.Public | BindingFlags.Instance
                );

                if (pSmtField != null)
                {
                    var pSmt = pSmtField.GetValue(ConfrontWithMovie.instance);
                    var atariNoField = pSmt.GetType().GetField("atari_no");
                    if (atariNoField != null)
                    {
                        return (byte)atariNoField.GetValue(pSmt);
                    }
                }
            }
            catch
            {
                // Ignore errors
            }
            return 0;
        }

        /// <summary>
        /// Navigates cursor to the next active target.
        /// </summary>
        public static void NavigateToNextTarget()
        {
            if (!IsVideoTapeActive())
            {
                SpeechManager.Announce(L.Get("video_tape.not_in_mode"), GameTextType.SystemMessage);
                return;
            }

            if (IsPlaying())
            {
                SpeechManager.Announce(L.Get("video_tape.pause_first"), GameTextType.Investigation);
                return;
            }

            try
            {
                var activeTargets = GetActiveTargets();

                if (activeTargets.Count == 0)
                {
                    SpeechManager.Announce(
                        L.Get("video_tape.no_targets_at_frame"),
                        GameTextType.Investigation
                    );
                    return;
                }

                _currentTargetIndex = (_currentTargetIndex + 1) % activeTargets.Count;
                NavigateToTarget(
                    activeTargets[_currentTargetIndex],
                    _currentTargetIndex,
                    activeTargets.Count
                );
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error navigating to target: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Navigates cursor to the previous active target.
        /// </summary>
        public static void NavigateToPreviousTarget()
        {
            if (!IsVideoTapeActive())
            {
                SpeechManager.Announce(L.Get("video_tape.not_in_mode"), GameTextType.SystemMessage);
                return;
            }

            if (IsPlaying())
            {
                SpeechManager.Announce(L.Get("video_tape.pause_first"), GameTextType.Investigation);
                return;
            }

            try
            {
                var activeTargets = GetActiveTargets();

                if (activeTargets.Count == 0)
                {
                    SpeechManager.Announce(
                        L.Get("video_tape.no_targets_at_frame"),
                        GameTextType.Investigation
                    );
                    return;
                }

                _currentTargetIndex =
                    _currentTargetIndex <= 0 ? activeTargets.Count - 1 : _currentTargetIndex - 1;
                NavigateToTarget(
                    activeTargets[_currentTargetIndex],
                    _currentTargetIndex,
                    activeTargets.Count
                );
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error navigating to target: {ex.Message}"
                );
            }
        }

        private static List<RectTransform> GetActiveTargets()
        {
            List<RectTransform> activeTargets = new List<RectTransform>();

            if (ConfrontWithMovie.instance == null)
                return activeTargets;

            var collisionPlayer = ConfrontWithMovie.instance.collision_player;
            if (collisionPlayer == null)
                return activeTargets;

            var rectsProperty = typeof(MovieCollisionPlayer).GetProperty("Rects");
            if (rectsProperty == null)
                return activeTargets;

            var rects = rectsProperty.GetValue(collisionPlayer, null) as IEnumerable<RectTransform>;
            if (rects == null)
                return activeTargets;

            foreach (var rect in rects)
            {
                if (rect != null && rect.gameObject.activeSelf)
                    activeTargets.Add(rect);
            }

            return activeTargets;
        }

        private static void NavigateToTarget(RectTransform target, int index, int total)
        {
            var cursor = ConfrontWithMovie.instance.cursor;
            if (cursor == null)
                return;

            // Get the target's center in world space, then convert to the cursor's local space
            // The target rect's position is its center
            Vector3 targetWorldPos = target.position;

            // Convert to the cursor's parent space
            Transform cursorParent = cursor.transform.parent;
            Vector3 localPos;
            if (cursorParent != null)
            {
                localPos = cursorParent.InverseTransformPoint(targetWorldPos);
            }
            else
            {
                localPos = targetWorldPos;
            }

            // The cursor's touch_rect has a collider offset of (-30, 30)
            // To make the touch point hit the target center, offset the cursor position
            localPos.x += 30f;
            localPos.y -= 30f;

            cursor.transform.localPosition = localPos;

            // Verify collision
            int collidedNo = cursor.GetCollidedNo();
            if (collidedNo < 4)
            {
                SpeechManager.Announce(
                    L.Get("video_tape.target_x_of_y", index + 1, total),
                    GameTextType.Investigation
                );
            }
            else
            {
                // Collision not detected, try without offset
                localPos.x -= 30f;
                localPos.y += 30f;
                cursor.transform.localPosition = localPos;

                collidedNo = cursor.GetCollidedNo();
                if (collidedNo < 4)
                {
                    SpeechManager.Announce(
                        L.Get("video_tape.target_x_of_y", index + 1, total),
                        GameTextType.Investigation
                    );
                }
                else
                {
                    SpeechManager.Announce(
                        L.Get("video_tape.target_needs_adjustment", index + 1, total),
                        GameTextType.Investigation
                    );
                }
            }
        }
    }
}
