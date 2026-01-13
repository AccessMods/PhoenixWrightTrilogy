using System.Reflection;
using AccessibilityMod.Core;
using AccessibilityMod.Services;
using HarmonyLib;
using UnityAccessibilityLib;

namespace AccessibilityMod.Patches
{
    /// <summary>
    /// Harmony patches for the fingerprint mini-game.
    /// </summary>
    [HarmonyPatch]
    public static class FingerprintPatches
    {
        private static int _lastCursor = -1;

        /// <summary>
        /// Patch for UpdateCompCursor to announce suspect changes.
        /// </summary>
        [HarmonyPatch(typeof(FingerMiniGame), "UpdateCompCursor")]
        [HarmonyPostfix]
        public static void OnUpdateCompCursor(FingerMiniGame __instance)
        {
            try
            {
                var cursorField = typeof(FingerMiniGame).GetField(
                    "comp_cursor_",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );

                if (cursorField != null)
                {
                    int cursor = (int)cursorField.GetValue(__instance);

                    if (cursor != _lastCursor)
                    {
                        _lastCursor = cursor;

                        string name = FingerprintNavigator.GetComparisonCharacterName(cursor);
                        if (name != null)
                        {
                            SpeechManager.Announce(name, GameTextType.Investigation);
                        }
                    }
                }
            }
            catch
            {
                // Ignore errors
            }
        }

        /// <summary>
        /// Patch for CheckClear to announce progress and success.
        /// </summary>
        [HarmonyPatch(typeof(FingerMiniGame), "CheckClear")]
        [HarmonyPostfix]
        public static void OnCheckClear(FingerMiniGame __instance, bool __result)
        {
            try
            {
                if (__result)
                {
                    SpeechManager.Announce(
                        L.Get("fingerprint.revealed"),
                        GameTextType.Investigation
                    );
                }
                else
                {
                    // Get current score and threshold to show progress
                    var scoreField = typeof(FingerMiniGame).GetField(
                        "score_",
                        BindingFlags.NonPublic | BindingFlags.Instance
                    );

                    if (scoreField != null)
                    {
                        int score = (int)scoreField.GetValue(__instance);
                        int threshold = GetThreshold(__instance);

                        if (threshold > 0)
                        {
                            int percentage = (int)((float)score / threshold * 100f);
                            percentage = System.Math.Min(percentage, 99); // Cap at 99% if not complete

                            if (percentage == 0)
                            {
                                SpeechManager.Announce(
                                    L.Get("fingerprint.no_powder"),
                                    GameTextType.Investigation
                                );
                            }
                            else
                            {
                                SpeechManager.Announce(
                                    L.Get("fingerprint.percent_keep_applying", percentage),
                                    GameTextType.Investigation
                                );
                            }
                        }
                        else
                        {
                            SpeechManager.Announce(
                                L.Get("fingerprint.not_enough"),
                                GameTextType.Investigation
                            );
                        }
                    }
                }
            }
            catch
            {
                // Ignore errors
            }
        }

        /// <summary>
        /// Gets the threshold for fingerprint completion using reflection.
        /// </summary>
        private static int GetThreshold(FingerMiniGame instance)
        {
            try
            {
                // Get game_id_ and finger_index_
                var gameIdField = typeof(FingerMiniGame).GetField(
                    "game_id_",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );
                var fingerIndexField = typeof(FingerMiniGame).GetField(
                    "finger_index_",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );

                if (gameIdField == null || fingerIndexField == null)
                    return 0;

                int gameId = (int)gameIdField.GetValue(instance);
                int fingerIndex = (int)fingerIndexField.GetValue(instance);

                // Get finger_info static field
                var fingerInfoField = typeof(FingerMiniGame).GetField(
                    "finger_info",
                    BindingFlags.NonPublic | BindingFlags.Static
                );

                // Get chk_inf_finger static field
                var chkInfFingerField = typeof(FingerMiniGame).GetField(
                    "chk_inf_finger",
                    BindingFlags.NonPublic | BindingFlags.Static
                );

                if (fingerInfoField == null || chkInfFingerField == null)
                    return 0;

                // Get finger_info[game_id_].ptbl[finger_index_].init_finger
                var fingerInfo = fingerInfoField.GetValue(null) as System.Array;
                if (fingerInfo == null)
                    return 0;

                var fingerInfoEntry = fingerInfo.GetValue(gameId);
                var ptblField = fingerInfoEntry.GetType().GetField("ptbl");
                if (ptblField == null)
                    return 0;

                var ptbl = ptblField.GetValue(fingerInfoEntry) as System.Array;
                if (ptbl == null || fingerIndex >= ptbl.Length)
                    return 0;

                var fingerTbl = ptbl.GetValue(fingerIndex);
                var initFingerField = fingerTbl.GetType().GetField("init_finger");
                if (initFingerField == null)
                    return 0;

                int initFinger = System.Convert.ToInt32(initFingerField.GetValue(fingerTbl));

                // Get chk_inf_finger[init_finger].ptbl[0].min_cnt
                var chkInfFinger = chkInfFingerField.GetValue(null) as System.Array;
                if (chkInfFinger == null || initFinger >= chkInfFinger.Length)
                    return 0;

                var chkInfo = chkInfFinger.GetValue(initFinger);
                var chkPtblField = chkInfo.GetType().GetField("ptbl");
                if (chkPtblField == null)
                    return 0;

                var chkPtbl = chkPtblField.GetValue(chkInfo) as System.Array;
                if (chkPtbl == null || chkPtbl.Length == 0)
                    return 0;

                var chkTbl = chkPtbl.GetValue(0);
                var minCntField = chkTbl.GetType().GetField("min_cnt");
                if (minCntField == null)
                    return 0;

                int minCnt = System.Convert.ToInt32(minCntField.GetValue(chkTbl));

                // Threshold is min_cnt * 15 (same as CheckClear calculation)
                return minCnt * 15;
            }
            catch
            {
                // Fall back to a reasonable estimate if reflection fails
                return 300000;
            }
        }

        /// <summary>
        /// Reset last cursor when starting comparison.
        /// </summary>
        [HarmonyPatch(typeof(FingerMiniGame), "CompMainCoroutine", MethodType.Enumerator)]
        [HarmonyPostfix]
        public static void OnCompMainCoroutine()
        {
            // Reset cursor tracking when entering comparison
            _lastCursor = -1;
        }
    }
}
