using System;
using AccessibilityMod.Core;
using HarmonyLib;
using UnityAccessibilityLib;

namespace AccessibilityMod.Patches
{
    [HarmonyPatch]
    public static class VerdictPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(judgmentCtrl), "judgment")]
        public static void judgment_Postfix(int in_type)
        {
            try
            {
                // in_type == 0 means Not Guilty, otherwise Guilty
                CoroutineRunner.Instance?.StartVerdictAnnouncement(in_type == 0);
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in judgment patch: {ex.Message}"
                );
            }
        }
    }
}
