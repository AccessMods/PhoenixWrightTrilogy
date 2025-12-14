using AccessibilityMod.Core;
using AccessibilityMod.Services;
using HarmonyLib;

namespace AccessibilityMod.Patches
{
    /// <summary>
    /// Harmony patches for the Luminol spray mini-game.
    /// </summary>
    [HarmonyPatch]
    public static class LuminolPatches
    {
        /// <summary>
        /// Patch for when a bloodstain's discovery animation completes.
        /// The state changes from Discovery to Discovered when the animation ends.
        /// </summary>
        [HarmonyPatch(typeof(luminolBloodstain), "WaitAnum", MethodType.Enumerator)]
        [HarmonyPostfix]
        public static void OnBloodstainAnimComplete(luminolBloodstain __instance)
        {
            // This runs on each yield, check if state just changed to Discovered
            if (__instance.state_ == BloodstainState.Discovered)
            {
                // The announcement is handled by checking state in the navigator
            }
        }

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
                    $"Hit! {remaining} more spray{(remaining != 1 ? "s" : "")} needed.",
                    TextType.Investigation
                );
            }
            else if (__instance.state_ == BloodstainState.Discovery)
            {
                // Full discovery - animation starting
                SpeechManager.Announce("Blood trace found!", TextType.Investigation);
            }
        }
    }
}
