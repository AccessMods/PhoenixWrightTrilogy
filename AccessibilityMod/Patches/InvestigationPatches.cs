using System;
using AccessibilityMod.Core;
using AccessibilityMod.Services;
using AccessibilityMod.Utilities;
using HarmonyLib;
using UnityAccessibilityLib;

namespace AccessibilityMod.Patches
{
    [HarmonyPatch]
    public static class InvestigationPatches
    {
        private static bool _wasInInvestigation = false;
        private static int _lastCursorSprite = -1;
        private static float _lastCursorPosX = float.NaN;
        private static float _lastCursorPosY = float.NaN;

        // Hook when investigation mode starts
        [HarmonyPostfix]
        [HarmonyPatch(typeof(inspectCtrl), "play")]
        public static void Play_Postfix(inspectCtrl __instance)
        {
            try
            {
                _wasInInvestigation = true;
                _lastCursorSprite = -1;
                _lastCursorPosX = float.NaN;
                _lastCursorPosY = float.NaN;

                AccessibilityState.SetMode(AccessibilityState.GameMode.Investigation);
                // Note: OnInvestigationStart() is now called from HotspotNavigator.Update()
                // which reliably detects mode transitions via AccessibilityState.IsInInvestigationMode()
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in Investigation Play patch: {ex.Message}"
                );
            }
        }

        // Hook when investigation mode ends
        [HarmonyPostfix]
        [HarmonyPatch(typeof(inspectCtrl), "end")]
        public static void End_Postfix(inspectCtrl __instance)
        {
            try
            {
                if (_wasInInvestigation)
                {
                    _wasInInvestigation = false;
                    _lastCursorSprite = -1;
                    _lastCursorPosX = float.NaN;
                    _lastCursorPosY = float.NaN;
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in Investigation End patch: {ex.Message}"
                );
            }
        }

        // Hook cursor sprite changes to detect hovering over hotspots
        // The cursor sprite number indicates:
        // 0 = Normal cursor (no hotspot)
        // 1 = Hotspot detected (unexamined)
        // 3 = Hotspot detected (already examined)
        [HarmonyPostfix]
        [HarmonyPatch(typeof(inspectCtrl), "Set_InspectCursor", MethodType.Normal)]
        public static void Set_InspectCursor_Postfix(inspectCtrl __instance)
        {
            try
            {
                if (!__instance.is_play)
                    return;

                // Get current cursor sprite number via reflection or by checking the sprite
                int currentSprite = GetCursorSpriteNumber(__instance);

                // Get current cursor position
                float currentPosX = __instance.pos_x;
                float currentPosY = __instance.pos_y;

                // Check if cursor position has changed (indicating user moved the cursor with arrow keys)
                bool cursorMoved = false;
                if (!float.IsNaN(_lastCursorPosX) && !float.IsNaN(_lastCursorPosY))
                {
                    // Consider it moved if position changed by more than 0.1 pixels (to handle floating point precision)
                    cursorMoved =
                        Math.Abs(currentPosX - _lastCursorPosX) > 0.1f
                        || Math.Abs(currentPosY - _lastCursorPosY) > 0.1f;
                }

                // Check for edge collision: if arrow key is pressed but cursor didn't move, we hit an edge
                if (!float.IsNaN(_lastCursorPosX) && !float.IsNaN(_lastCursorPosY) && !cursorMoved)
                {
                    bool directionKeyPressed = false;
                    try
                    {
                        // Check if any arrow key is being held down
                        if (padCtrl.instance != null)
                        {
                            directionKeyPressed =
                                padCtrl.instance.GetKey(KeyType.Up)
                                || padCtrl.instance.GetKey(KeyType.Down)
                                || padCtrl.instance.GetKey(KeyType.Left)
                                || padCtrl.instance.GetKey(KeyType.Right);
                        }
                    }
                    catch { }

                    if (directionKeyPressed)
                    {
                        // Hit an edge - announce it every time (not just once)
                        SpeechManager.Announce(
                            L.Get("investigation.edge"),
                            GameTextType.Investigation
                        );
                    }
                }

                // Update position tracking
                _lastCursorPosX = currentPosX;
                _lastCursorPosY = currentPosY;

                if (currentSprite != _lastCursorSprite)
                {
                    int previousSprite = _lastCursorSprite;
                    _lastCursorSprite = currentSprite;

                    switch (currentSprite)
                    {
                        case 1:
                            // Hovering over unexamined hotspot
                            SpeechManager.Announce(
                                L.Get("investigation.point_of_interest"),
                                GameTextType.Investigation
                            );
                            break;
                        case 3:
                            // Hovering over already examined hotspot
                            SpeechManager.Announce(
                                L.Get("investigation.already_examined"),
                                GameTextType.Investigation
                            );
                            break;
                        case 0:
                            // Leaving a hotspot area -> announce ONLY if cursor actually moved (not just scene switch)
                            if ((previousSprite == 1 || previousSprite == 3) && cursorMoved)
                            {
                                SpeechManager.Announce(
                                    L.Get("investigation.Out_of_point_of_interest"),
                                    GameTextType.Investigation
                                );
                            }
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in Set_InspectCursor patch: {ex.Message}"
                );
            }
        }

        private static int GetCursorSpriteNumber(inspectCtrl instance)
        {
            try
            {
                // Access the private cursor_ field to get its sprite number
                var cursorField = typeof(inspectCtrl).GetField(
                    "cursor_",
                    System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Instance
                );

                if (cursorField != null)
                {
                    var cursor = cursorField.GetValue(instance) as AssetBundleSprite;
                    if (cursor != null)
                    {
                        return cursor.sprite_no_;
                    }
                }
            }
            catch { }
            return 0;
        }
    }

    // Move menu patches
    [HarmonyPatch]
    public static class MovePatches
    {
        private static int _lastMoveCursor = -1;
        private static int _lastCursorNum = -1;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(moveCtrl), "setting")]
        public static void Setting_Postfix(moveCtrl __instance, int in_num, int in_cursor_no)
        {
            try
            {
                _lastMoveCursor = in_cursor_no;
                _lastCursorNum = in_num;

                // Use delayed announcement to ensure text is populated
                var ctrl = __instance;
                int cursor = in_cursor_no;
                CoroutineRunner.Instance?.ScheduleDelayedAnnouncement(
                    0.1f,
                    () =>
                    {
                        string locationName = GetLocationName(ctrl, cursor);
                        if (
                            Net35Extensions.IsNullOrWhiteSpace(locationName)
                            || locationName == "New Text"
                        )
                            return null;
                        return L.Get("menu.move_prefix") + locationName;
                    },
                    GameTextType.Menu
                );
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in Move Setting patch: {ex.Message}"
                );
            }
        }

        // Hook cursor navigation to announce location changes
        [HarmonyPostfix]
        [HarmonyPatch(typeof(moveCtrl), "move_set_thumbnail_image")]
        public static void MoveThumbnail_Postfix(moveCtrl __instance)
        {
            try
            {
                // Get cursor_no_ via reflection
                var cursorNoField = typeof(moveCtrl).GetField(
                    "cursor_no_",
                    System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Instance
                );

                if (cursorNoField == null)
                    return;

                int currentCursor = (int)cursorNoField.GetValue(__instance);

                // Only announce if cursor changed (avoid duplicate from setting())
                if (currentCursor != _lastMoveCursor)
                {
                    _lastMoveCursor = currentCursor;

                    // Use delayed announcement to ensure text is populated
                    var ctrl = __instance;
                    int cursor = currentCursor;
                    CoroutineRunner.Instance?.ScheduleDelayedAnnouncement(
                        0.05f,
                        () =>
                        {
                            string locationName = GetLocationName(ctrl, cursor);
                            if (
                                Net35Extensions.IsNullOrWhiteSpace(locationName)
                                || locationName == "New Text"
                            )
                                return null;
                            return locationName;
                        },
                        GameTextType.Menu
                    );
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in Move Thumbnail patch: {ex.Message}"
                );
            }
        }

        private static string GetLocationName(moveCtrl ctrl, int index)
        {
            try
            {
                // Access select_list to get location name
                var selectListField = typeof(moveCtrl).GetField(
                    "select_list_",
                    System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Instance
                );

                if (selectListField != null)
                {
                    var selectList = selectListField.GetValue(ctrl) as System.Collections.IList;
                    if (selectList != null && index >= 0 && index < selectList.Count)
                    {
                        var item = selectList[index];
                        var textField = item.GetType().GetField("text_");
                        if (textField != null)
                        {
                            var textComponent = textField.GetValue(item) as UnityEngine.UI.Text;
                            if (textComponent != null)
                            {
                                return textComponent.text;
                            }
                        }
                    }
                }
            }
            catch { }
            return $"Location {index + 1}";
        }
    }
}
