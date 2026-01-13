using System.Reflection;
using AccessibilityMod.Core;
using AccessibilityMod.Services;
using HarmonyLib;
using UnityAccessibilityLib;

namespace AccessibilityMod.Patches
{
    /// <summary>
    /// Harmony patches for the safe keypad mini-game in GS1 Episode 5.
    /// </summary>
    [HarmonyPatch]
    public static class SafeKeypadPatches
    {
        private static int _lastCursorX = -1;
        private static int _lastCursorY = -1;

        // Button layout matches the game's layout:
        // Row 0: 1, 2, 3
        // Row 1: 4, 5, 6
        // Row 2: 7, 8, 9
        // Row 3: 0, Back, Back
        private static readonly int[,] ButtonLayout = new int[4, 3]
        {
            { 1, 2, 3 },
            { 4, 5, 6 },
            { 7, 8, 9 },
            { 0, 10, 10 },
        };

        /// <summary>
        /// Patch for cursor update to announce the current button.
        /// </summary>
        [HarmonyPatch(typeof(KinkoMiniGame), "CursorUpDate")]
        [HarmonyPostfix]
        public static void OnCursorUpdate(KinkoMiniGame __instance)
        {
            try
            {
                var cursorXField = typeof(KinkoMiniGame).GetField(
                    "cursor_x",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );
                var cursorYField = typeof(KinkoMiniGame).GetField(
                    "cursor_y",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );

                if (cursorXField != null && cursorYField != null)
                {
                    int cursorX = (int)cursorXField.GetValue(__instance);
                    int cursorY = (int)cursorYField.GetValue(__instance);

                    // Only announce if cursor position changed
                    if (cursorX != _lastCursorX || cursorY != _lastCursorY)
                    {
                        _lastCursorX = cursorX;
                        _lastCursorY = cursorY;

                        int buttonValue = ButtonLayout[cursorY, cursorX];
                        string buttonName =
                            buttonValue == 10 ? L.Get("safe_keypad.back") : buttonValue.ToString();

                        SpeechManager.Announce(buttonName, GameTextType.Menu);
                    }
                }
            }
            catch
            {
                // Ignore errors in patch
            }
        }

        /// <summary>
        /// Patch for when the keypad initializes to reset cursor tracking.
        /// </summary>
        [HarmonyPatch(typeof(KinkoMiniGame), "Init")]
        [HarmonyPostfix]
        public static void OnInit()
        {
            _lastCursorX = -1;
            _lastCursorY = -1;
            SpeechManager.Announce(L.Get("safe_keypad.opened"), GameTextType.Menu);
        }

        /// <summary>
        /// Patch for when a number is entered.
        /// </summary>
        [HarmonyPatch(typeof(KinkoMiniGame), "SetNumber")]
        [HarmonyPostfix]
        public static void OnSetNumber(int num)
        {
            try
            {
                if (num == 10)
                {
                    SpeechManager.Announce(L.Get("safe_keypad.deleted"), GameTextType.Menu);
                }
                else
                {
                    // Number entered - the game plays a sound, we just confirm
                    SpeechManager.Announce(L.Get("safe_keypad.entered", num), GameTextType.Menu);
                }
            }
            catch
            {
                // Ignore errors
            }
        }
    }
}
