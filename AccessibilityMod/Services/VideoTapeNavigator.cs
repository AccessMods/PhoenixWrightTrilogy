using System;
using System.Collections.Generic;
using System.Reflection;
using AccessibilityMod.Core;
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
        /// Checks if the video tape examination is currently active.
        /// </summary>
        public static bool IsVideoTapeActive()
        {
            try
            {
                if (ConfrontWithMovie.instance != null)
                {
                    var controller = ConfrontWithMovie.instance.movie_controller;
                    return controller != null && controller.is_play;
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
                    SpeechManager.Announce("Playing", TextType.Investigation);
                }
                else if (!isPlaying && _wasPlaying)
                {
                    int targetCount = GetActiveTargetCount();
                    string targetInfo =
                        targetCount > 0
                            ? $", {targetCount} target{(targetCount != 1 ? "s" : "")}"
                            : "";
                    SpeechManager.Announce(
                        $"Paused at frame {currentFrame}{targetInfo}",
                        TextType.Investigation
                    );
                }

                // Check for new targets appearing while playing (only announce first appearance)
                int currentTargetCount = GetActiveTargetCount();
                if (currentTargetCount > 0 && _lastTargetCount == 0)
                {
                    SpeechManager.Announce(
                        $"Target available! Pause with Backspace.",
                        TextType.Investigation
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

            SpeechManager.Announce(
                "Video tape examination. Backspace to play/pause, Enter to fast forward, J to rewind, E to present. Press H for hint.",
                TextType.Investigation
            );
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
                SpeechManager.Announce("Not in video tape mode", TextType.SystemMessage);
                return;
            }

            int frame = GetCurrentFrame();
            bool playing = IsPlaying();
            int targets = GetActiveTargetCount();
            bool overTarget = IsCursorOverTarget();

            string state = playing ? "Playing" : "Paused";
            string targetInfo =
                targets > 0
                    ? $"{targets} target{(targets > 1 ? "s" : "")} available"
                    : "No targets";

            if (overTarget)
            {
                int targetNo = GetCursorTarget();
                targetInfo += $", cursor on target {targetNo + 1}";
            }

            SpeechManager.Announce(
                $"{state}, frame {frame}. {targetInfo}.",
                TextType.Investigation
            );
        }

        /// <summary>
        /// Announces a hint for the video tape examination.
        /// </summary>
        public static void AnnounceHint()
        {
            if (!IsVideoTapeActive())
            {
                SpeechManager.Announce("Not in video tape mode", TextType.SystemMessage);
                return;
            }

            // Get which examination this is based on atari_no
            int atariNo = GetAtariNo();
            int frame = GetCurrentFrame();

            string hint;
            switch (atariNo)
            {
                case 0:
                    // First examination - find the lit locker
                    hint =
                        "First viewing: Find Goodman's locker lit up (open). "
                        + "Pause when you hear target available, press ] to select it, then E to present. "
                        + $"Current frame: {frame}.";
                    break;
                case 1:
                    // Second examination - find the falling object
                    hint =
                        "Second viewing: Something falls from the locker. "
                        + "A wrong target appears around frame 460. The correct falling object is around frame 490. "
                        + "Fast forward with Enter past 460, pause with Backspace around 490, "
                        + "press ] to cycle through targets until you find the falling object, then E to present. "
                        + $"Current frame: {frame}.";
                    break;
                case 2:
                    // Third examination
                    hint =
                        "Third viewing: The correct target is around frame 1360. "
                        + "Fast forward with Enter, pause with Backspace around 1360, "
                        + "press ] to select the target, then E to present. "
                        + $"Current frame: {frame}.";
                    break;
                case 3:
                    // Fourth examination
                    hint =
                        "Fourth viewing: The correct target is around frame 900. "
                        + "Fast forward with Enter, pause with Backspace around 900, "
                        + "press ] to select the target, then E to present. "
                        + $"Current frame: {frame}.";
                    break;
                default:
                    hint =
                        "Pause when you hear target available, press ] to select it, then E to present. "
                        + $"Current frame: {frame}.";
                    break;
            }

            SpeechManager.Announce(hint, TextType.Investigation);
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
                SpeechManager.Announce("Not in video tape mode", TextType.SystemMessage);
                return;
            }

            if (IsPlaying())
            {
                SpeechManager.Announce(
                    "Pause the video first with Backspace",
                    TextType.Investigation
                );
                return;
            }

            try
            {
                var activeTargets = GetActiveTargets();

                if (activeTargets.Count == 0)
                {
                    SpeechManager.Announce(
                        "No targets available at this frame",
                        TextType.Investigation
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
                SpeechManager.Announce("Not in video tape mode", TextType.SystemMessage);
                return;
            }

            if (IsPlaying())
            {
                SpeechManager.Announce(
                    "Pause the video first with Backspace",
                    TextType.Investigation
                );
                return;
            }

            try
            {
                var activeTargets = GetActiveTargets();

                if (activeTargets.Count == 0)
                {
                    SpeechManager.Announce(
                        "No targets available at this frame",
                        TextType.Investigation
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
                    $"Target {index + 1} of {total}. Press E to present.",
                    TextType.Investigation
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
                        $"Target {index + 1} of {total}. Press E to present.",
                        TextType.Investigation
                    );
                }
                else
                {
                    SpeechManager.Announce(
                        $"Target {index + 1} of {total}. Cursor positioned but may need adjustment. Use arrow keys to fine-tune, then E to present.",
                        TextType.Investigation
                    );
                }
            }
        }
    }
}
