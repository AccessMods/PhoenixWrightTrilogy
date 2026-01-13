using AccessibilityMod.Core;
using AccessibilityMod.Services;
using HarmonyLib;
using UnityAccessibilityLib;

namespace AccessibilityMod.Patches
{
    /// <summary>
    /// Harmony patches for the Luminol spray mini-game.
    /// </summary>
    [HarmonyPatch]
    public static class LuminolPatches
    {
        // Note: The "Press Enter to examine" prompt after blood discovery is handled
        // in LuminolNavigator.CheckAcquiredState() by monitoring the minigame state.

        /// <summary>
        /// Patch for when blood starts appearing (spray hit).
        /// </summary>
        [HarmonyPatch(typeof(luminolBloodstain), nameof(luminolBloodstain.AppearBlood))]
        [HarmonyPostfix]
        public static void OnBloodAppear(luminolBloodstain __instance)
        {
            // Announce partial hit (needs 3 sprays total)
            if (
                __instance.state_ == BloodstainState.Undiscovered
                && __instance.discovery_count_ > 0
            )
            {
                // Still needs more sprays
                int remaining = __instance.discovery_count_;
                SpeechManager.Announce(
                    L.GetPlural("luminol.hit_more_needed", remaining),
                    GameTextType.Investigation
                );
            }
            else if (__instance.state_ == BloodstainState.Discovery)
            {
                // Full discovery - animation starting
                SpeechManager.Announce(L.Get("luminol.blood_found"), GameTextType.Investigation);
            }
        }
    }
}
