using System;
using AccessibilityMod.Core;
using AccessibilityMod.Services;
using HarmonyLib;
using UnityAccessibilityLib;

namespace AccessibilityMod.Patches
{
    /// <summary>
    /// Patches for 3D evidence examination mode (GS1 Episode 5+)
    /// Handles entry/exit announcements, hotspot detection, and zoom tracking
    /// </summary>
    [HarmonyPatch]
    public static class ScienceInvestigationPatches
    {
        private static int _lastHitPointIndex = -1;
        private static bool _wasPlaying = false;

        /// <summary>
        /// Announce when 3D evidence examination starts
        /// StateMainCoroutine is called after initialization is complete
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(scienceInvestigationCtrl), "StateMainCoroutine")]
        public static void StateMainCoroutine_Postfix(scienceInvestigationCtrl __instance)
        {
            try
            {
                if (!__instance.is_play)
                    return;

                // Reset tracking
                _lastHitPointIndex = -1;
                _wasPlaying = true;

                // Initialize the 3D navigator (zoom tracking, hotspot discovery)
                Evidence3DNavigator.OnEnter3DMode();

                // Get evidence name and hotspot count
                string evidenceName = Evidence3DNavigator.GetCurrentEvidenceName();
                int hotspotCount = Evidence3DNavigator.GetHotspotCount();

                string message = L.Get("evidence_3d.opened", evidenceName, hotspotCount);

                SpeechManager.Announce(message, GameTextType.Menu);

                AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                    $"[3DEvidence] Entered 3D examination mode for: {evidenceName}, {hotspotCount} hotspots"
                );
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in StateMainCoroutine patch: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Announce when exiting 3D evidence examination
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(scienceInvestigationCtrl), "StateCloseCoroutine")]
        public static void StateCloseCoroutine_Postfix(scienceInvestigationCtrl __instance)
        {
            try
            {
                if (_wasPlaying)
                {
                    _wasPlaying = false;
                    _lastHitPointIndex = -1;

                    // Clean up 3D navigator state
                    Evidence3DNavigator.OnExit3DMode();

                    SpeechManager.Announce(L.Get("evidence_3d.exited"), GameTextType.Menu);

                    AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                        "[3DEvidence] Exited 3D examination mode"
                    );
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in StateCloseCoroutine patch: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Auto-announce when cursor moves over a hotspot
        /// ChangeGuideIconSprite is called during input handling to update cursor icon
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(scienceInvestigationCtrl), "ChangeGuideIconSprite")]
        public static void ChangeGuideIconSprite_Postfix(scienceInvestigationCtrl __instance)
        {
            try
            {
                if (!__instance.is_play)
                    return;

                int currentHitIndex = __instance.hit_point_index;

                // Check if we moved onto a hotspot (from no hotspot or different hotspot)
                if (currentHitIndex != -1 && currentHitIndex != _lastHitPointIndex)
                {
                    SpeechManager.Announce(L.Get("evidence_3d.hotspot"), GameTextType.Menu);
                }

                _lastHitPointIndex = currentHitIndex;
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in ChangeGuideIconSprite patch: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Track zoom level changes during input processing
        /// UpdateSystemInput handles X/Y button presses for zoom
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(scienceInvestigationCtrl), "UpdateSystemInput")]
        public static void UpdateSystemInput_Postfix(scienceInvestigationCtrl __instance)
        {
            try
            {
                if (!__instance.is_play)
                    return;

                // Check for zoom changes and announce if changed
                Evidence3DNavigator.CheckAndAnnounceZoomChange();
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in UpdateSystemInput patch: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Reset state when needed
        /// </summary>
        public static void ResetState()
        {
            _lastHitPointIndex = -1;
            _wasPlaying = false;
        }
    }
}
