using System;
using AccessibilityMod.Core;
using AccessibilityMod.Services;
using HarmonyLib;
using UnityAccessibilityLib;

namespace AccessibilityMod.Patches
{
    /// <summary>
    /// Harmony patches for the bug sweeper (Tanchiki) minigame accessibility.
    /// </summary>
    [HarmonyPatch]
    public static class BugSweeperPatches
    {
        // Track if we already processed this target to avoid duplicate marking
        private static int _lastConfirmedTarget = -1;

        /// <summary>
        /// Patch TanchikiMiniGame.hit_check to detect when player confirms a target.
        /// We track when hit_check returns a valid target and the A button is pressed.
        /// </summary>
        [HarmonyPatch(typeof(TanchikiMiniGame), nameof(TanchikiMiniGame.hit_check))]
        [HarmonyPostfix]
        public static void OnHitCheck(int __result)
        {
            try
            {
                // Check if A button was just pressed and we're on a valid target
                // Use __result directly - do NOT call hit_check() again (causes stack overflow)
                if (padCtrl.instance != null && padCtrl.instance.GetKeyDown(KeyType.A))
                {
                    // hit_check returns find_target.Length - 1 when NOT on a target
                    // Any other valid index means we're on a target
                    var targets = TanchikiMiniGame.find_target;
                    if (targets != null && __result >= 0 && __result < targets.Length - 1)
                    {
                        // Avoid duplicate processing for the same target
                        if (__result != _lastConfirmedTarget)
                        {
                            _lastConfirmedTarget = __result;
                            BugSweeperNavigator.MarkTargetChecked(__result);
#if DEBUG
                            AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                                $"[BugSweeper] Target {__result} confirmed with A button"
                            );
#endif
                        }
                    }
                }
                else
                {
                    // Reset when A is not pressed so we can detect the next confirmation
                    _lastConfirmedTarget = -1;
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in BugSweeper hit_check patch: {ex.Message}"
                );
            }
        }
    }
}
