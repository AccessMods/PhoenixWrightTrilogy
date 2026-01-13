using System;
using System.Reflection;
using AccessibilityMod.Core;
using AccessibilityMod.Services;
using HarmonyLib;
using UnityAccessibilityLib;

namespace AccessibilityMod.Patches
{
    [HarmonyPatch]
    public static class GalleryPatches
    {
        private static int _lastAnnouncedIndex = -1;
        private static bool _isGalleryActive = false;

        /// <summary>
        /// Returns whether the Gallery menu is currently active.
        /// </summary>
        public static bool IsGalleryActive => _isGalleryActive;

        #region Gallery Menu Patches

        /// <summary>
        /// Announce gallery entry when PlayAsync starts.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GalleryCtrl), "Play", new Type[0])]
        public static void Play_Postfix()
        {
            try
            {
                _isGalleryActive = true;
                _lastAnnouncedIndex = -1;
                SpeechManager.Announce(L.Get("gallery.opened"), GameTextType.Menu);
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in Gallery Play patch: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Announce menu item when selection changes.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GalleryCtrl), "SelectItem", new Type[] { typeof(int), typeof(bool) })]
        public static void SelectItem_Postfix(GalleryCtrl __instance, int idx, bool immediate)
        {
            try
            {
                // Don't announce if this is the same item (immediate=true means confirming selection)
                if (idx == _lastAnnouncedIndex && !immediate)
                    return;

                // If immediate is true and same index, it means confirming - don't re-announce
                if (immediate)
                    return;

                _lastAnnouncedIndex = idx;

                // Get the item name from the game's text system
                string itemName = GetGalleryItemName(idx);
                if (!Net35Extensions.IsNullOrWhiteSpace(itemName))
                {
                    SpeechManager.Announce(itemName, GameTextType.Menu);
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in Gallery SelectItem patch: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Reset state when gallery closes.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GalleryCtrl), "End")]
        public static void End_Postfix()
        {
            try
            {
                _isGalleryActive = false;
                _lastAnnouncedIndex = -1;
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in Gallery OnForceClose patch: {ex.Message}"
                );
            }
        }

        #endregion

        #region Helper Methods

        private static string GetGalleryItemName(int idx)
        {
            try
            {
                // ContentKind enum: Orchestra=1, ArtLibrary=2, ActionStudioSelector=3
                // idx is 0-based, so ContentKind = idx + 1
                GalleryCtrl.ContentKind contentKind = (GalleryCtrl.ContentKind)(idx + 1);

                // Get the TABLE dictionary via reflection
                var tableField = typeof(GalleryCtrl).GetField(
                    "TABLE",
                    BindingFlags.NonPublic | BindingFlags.Static
                );
                if (tableField == null)
                    return GetFallbackItemName(idx);

                var table = tableField.GetValue(null) as System.Collections.IDictionary;
                if (table == null || !table.Contains(contentKind))
                    return GetFallbackItemName(idx);

                // Get TableData for this content kind
                var tableData = table[contentKind];
                if (tableData == null)
                    return GetFallbackItemName(idx);

                // Get ButtonText property (it's a GalleryTextID)
                var buttonTextProp = tableData.GetType().GetProperty("ButtonText");
                if (buttonTextProp == null)
                    return GetFallbackItemName(idx);

                var buttonTextId = buttonTextProp.GetValue(tableData, null);
                if (buttonTextId == null)
                    return GetFallbackItemName(idx);

                // Use TextDataCtrl.GetText to get the localized name
                return TextDataCtrl.GetText((TextDataCtrl.GalleryTextID)buttonTextId, 0);
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error getting gallery item name: {ex.Message}"
                );
                return GetFallbackItemName(idx);
            }
        }

        private static string GetFallbackItemName(int idx)
        {
            switch (idx)
            {
                case 0:
                    return L.Get("gallery.music_player");
                case 1:
                    return L.Get("gallery.art_library");
                case 2:
                    return L.Get("gallery.action_studio");
                default:
                    return $"Item {idx + 1}";
            }
        }

        #endregion
    }
}
