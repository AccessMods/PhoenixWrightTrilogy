using System;
using AccessibilityMod.Core;
using HarmonyLib;

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
        /// Gets the total number of locks for a psyche-lock by index.
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

                return psylockData.size;
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

                string lockWord = _totalLocks == 1 ? "Psyche-Lock" : "Psyche-Locks";
                string message = $"{_totalLocks} {lockWord}";
                ClipboardManager.Announce(message, TextType.Menu);

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
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GSPsylock), "PsylockDisp_unlock")]
        public static void PsylockDisp_unlock_Postfix()
        {
            try
            {
                // The unlock method decrements psy.rest, so we need to get the current value
                // after decrement. We track this based on our stored total.
                int remaining = GetRemainingLocks();

                // Avoid duplicate announcements
                if (remaining == _lastAnnouncedLockCount)
                    return;

                _lastAnnouncedLockCount = remaining;

                string message;
                if (remaining <= 0)
                {
                    // All locks broken - this will be followed by unlock_message
                    message = "Lock broken!";
                }
                else
                {
                    string lockWord = remaining == 1 ? "lock" : "locks";
                    message = $"Lock broken! {remaining} {lockWord} remaining";
                }

                ClipboardManager.Announce(message, TextType.Menu);

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
                ClipboardManager.Announce(
                    "All Psyche-Locks broken! Secret unlocked!",
                    TextType.Menu
                );
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

        // Patch for when psyche-lock reappears (after backing out and selecting again)
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GSPsylock), "PsylockDisp_redisp")]
        public static void PsylockDisp_redisp_Postfix()
        {
            try
            {
                int remaining = GetRemainingLocks();
                if (remaining > 0 && remaining != _lastAnnouncedLockCount)
                {
                    _lastAnnouncedLockCount = remaining;
                    string lockWord = remaining == 1 ? "Psyche-Lock" : "Psyche-Locks";
                    string message = $"{remaining} {lockWord} remaining";
                    ClipboardManager.Announce(message, TextType.Menu);
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in PsylockDisp_redisp patch: {ex.Message}"
                );
            }
        }
    }
}
