using System;
using AccessibilityMod.Core;
using AccessibilityMod.Services;
using HarmonyLib;
using UnityAccessibilityLib;

namespace AccessibilityMod.Patches
{
    [HarmonyPatch]
    public static class PsycheLockPatches
    {
        // Track the last announced lock count to avoid duplicate announcements
        private static int _lastAnnouncedLockCount = -1;
        private static int _totalLocks = 0;

        /// <summary>
        /// Gets the number of remaining locks from the current psyche-lock challenge.
        /// </summary>
        public static int GetRemainingLocks()
        {
            try
            {
                var globalWork = GSStatic.global_work_;
                if (globalWork == null)
                    return 0;

                int psyNo = globalWork.psy_no;
                if (psyNo < 0 || psyNo >= globalWork.psylock.Length)
                    return 0;

                var psylockData = globalWork.psylock[psyNo];
                if (psylockData == null || (psylockData.status & 1) == 0)
                    return 0;

                return psylockData.level;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Gets the remaining number of locks for a psyche-lock by index.
        /// Uses 'level' which decrements as locks are broken, not 'size' which is the original count.
        /// </summary>
        public static int GetTotalLocks(int psyIndex)
        {
            try
            {
                var globalWork = GSStatic.global_work_;
                if (globalWork == null)
                    return 0;

                if (psyIndex < 0 || psyIndex >= globalWork.psylock.Length)
                    return 0;

                var psylockData = globalWork.psylock[psyIndex];
                if (psylockData == null || (psylockData.status & 1) == 0)
                    return 0;

                // Use 'level' not 'size' - level decrements as locks break
                return psylockData.level;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Gets the lock count for the current room and character.
        /// Used by talk menu to show lock count for locked topics.
        /// </summary>
        public static int GetCurrentLockCount()
        {
            try
            {
                var globalWork = GSStatic.global_work_;
                if (globalWork == null)
                    return 0;

                ushort room = (ushort)globalWork.Room;
                ushort characterId = (ushort)AnimationSystem.Instance.IdlingCharacterMasked;

                int psyIndex = GSPsylock.is_on_psylock_flag_in_room(room, characterId);
                if (psyIndex >= 0)
                {
                    return GetTotalLocks(psyIndex);
                }
                return 0;
            }
            catch
            {
                return 0;
            }
        }

        // Patch for when psyche-lock display is initialized - announces total lock count
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GSPsylock), "PsylockDisp_init")]
        public static void PsylockDisp_init_Postfix(int _level)
        {
            try
            {
                // Store total locks for later reference
                _totalLocks = _level > 5 ? 5 : _level;
                _lastAnnouncedLockCount = _totalLocks;

                string message = L.GetPlural("psyche_lock.total", _totalLocks);
                SpeechManager.Announce(message, GameTextType.Menu);

                AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                    $"Psyche-Lock initialized with {_totalLocks} locks"
                );
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in PsylockDisp_init patch: {ex.Message}"
                );
            }
        }

        // Patch for when a lock is broken - announces remaining locks
        // Note: PsylockDisp_unlock() just sets the state machine to unlock mode.
        // The actual decrement of psy.rest happens in psylock_move_lock_unlock()
        // on the next frame. So we need to subtract 1 from the current count.
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GSPsylock), "PsylockDisp_unlock")]
        public static void PsylockDisp_unlock_Postfix()
        {
            try
            {
                // GetRemainingLocks() returns the current value before decrement,
                // so we subtract 1 to get the value after the lock breaks
                int remaining = GetRemainingLocks() - 1;

                // Avoid duplicate announcements
                if (remaining == _lastAnnouncedLockCount)
                    return;

                _lastAnnouncedLockCount = remaining;

                string message;
                if (remaining <= 0)
                {
                    // All locks broken - this will be followed by unlock_message
                    message = L.Get("psyche_lock.broken");
                }
                else
                {
                    message = L.GetPlural("psyche_lock.broken_remaining", remaining);
                }

                SpeechManager.Announce(message, GameTextType.Menu);

                AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                    $"Psyche-Lock broken, {remaining} remaining"
                );
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in PsylockDisp_unlock patch: {ex.Message}"
                );
            }
        }

        // Patch for when all locks are broken and the unlock message appears
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GSPsylock), "PsylockDisp_unlock_message")]
        public static void PsylockDisp_unlock_message_Postfix()
        {
            try
            {
                SpeechManager.Announce(L.Get("psyche_lock.all_broken"), GameTextType.Menu);
                _lastAnnouncedLockCount = -1;
                _totalLocks = 0;

                AccessibilityMod.Core.AccessibilityMod.Logger?.Msg("All Psyche-Locks broken");
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in PsylockDisp_unlock_message patch: {ex.Message}"
                );
            }
        }

        // Patch for when psyche-lock is cleared/cancelled
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GSPsylock), "PsylockDisp_clear_all")]
        public static void PsylockDisp_clear_all_Postfix()
        {
            try
            {
                // Reset tracking state when psyche-lock ends
                _lastAnnouncedLockCount = -1;
                _totalLocks = 0;
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in PsylockDisp_clear_all patch: {ex.Message}"
                );
            }
        }
    }
}
