using System.Reflection;
using AccessibilityMod.Core;
using AccessibilityMod.Services;
using HarmonyLib;
using UnityAccessibilityLib;

namespace AccessibilityMod.Patches
{
    /// <summary>
    /// Harmony patches for the vase/jar puzzle mini-game.
    /// </summary>
    [HarmonyPatch]
    public static class VasePuzzlePatches
    {
        private static int _lastPuzzleStep = -1;

        /// <summary>
        /// Patch for when a piece is successfully combined.
        /// </summary>
        [HarmonyPatch(typeof(VasePuzzleMiniGame), "_proc_union_success")]
        [HarmonyPostfix]
        public static void OnUnionSuccess(VasePuzzleMiniGame __instance)
        {
            try
            {
                // Get the puzzle step to see how many remain
                var stepField = typeof(VasePuzzleMiniGame).GetField(
                    "puzzle_step_",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );

                var piecesField = typeof(VasePuzzleMiniGame).GetField(
                    "pieces_status_",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );

                if (stepField != null && piecesField != null)
                {
                    int puzzleStep = (int)stepField.GetValue(__instance);
                    var pieces = piecesField.GetValue(__instance) as PiecesStatus[];

                    // Only announce once per step change
                    if (puzzleStep != _lastPuzzleStep && pieces != null)
                    {
                        _lastPuzzleStep = puzzleStep;

                        int totalPieces = pieces.Length; // Use actual piece count (8 for first puzzle, 1 for second)
                        int remaining = totalPieces - puzzleStep - 1; // -1 because step hasn't incremented yet in success

                        if (remaining <= 0)
                        {
                            SpeechManager.Announce(
                                L.Get("vase.puzzle_complete"),
                                GameTextType.Investigation
                            );
                        }
                        else
                        {
                            SpeechManager.Announce(
                                L.Get("vase.piece_placed", remaining),
                                GameTextType.Investigation
                            );
                        }
                    }
                }
            }
            catch
            {
                // Ignore errors in patch
            }
        }

        /// <summary>
        /// Patch for when a piece fails to combine.
        /// </summary>
        [HarmonyPatch(typeof(VasePuzzleMiniGame), "_proc_union_failure")]
        [HarmonyPostfix]
        public static void OnUnionFailure()
        {
            // This gets called every frame during failure animation, so track state
            // We'll just let the user know via the hint system instead
        }

        /// <summary>
        /// Patch for the assemble attempt to announce wrong piece/rotation.
        /// </summary>
        [HarmonyPatch(typeof(VasePuzzleMiniGame), "_assemble_vase")]
        [HarmonyPostfix]
        public static void OnAssembleAttempt(VasePuzzleMiniGame __instance)
        {
            try
            {
                // Get proc_id_ to see what state we transitioned to
                var procField = typeof(VasePuzzleMiniGame).GetField(
                    "proc_id_",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );

                if (procField != null)
                {
                    var procValue = procField.GetValue(__instance);
                    int procId = System.Convert.ToInt32(procValue);

                    // Proc.union_failure = 5
                    if (procId == 5)
                    {
                        SpeechManager.Announce(
                            L.Get("vase.wrong_piece"),
                            GameTextType.Investigation
                        );
                    }
                }
            }
            catch
            {
                // Ignore errors in patch
            }
        }

        /// <summary>
        /// Patch for when piece selection changes to announce the new piece (touch/click).
        /// </summary>
        [HarmonyPatch(typeof(VasePuzzleMiniGame), "SetCursorIndex")]
        [HarmonyPostfix]
        public static void OnCursorChange(VasePuzzleMiniGame __instance, int i)
        {
            try
            {
                var piecesField = typeof(VasePuzzleMiniGame).GetField(
                    "pieces_status_",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );

                if (piecesField != null)
                {
                    var pieces = piecesField.GetValue(__instance) as PiecesStatus[];
                    if (pieces != null && i < pieces.Length)
                    {
                        int displayNumber = i + 1;
                        int rotation = pieces[i].angle_id * 90;
                        SpeechManager.Announce(
                            L.Get("vase.piece_rotation", displayNumber, rotation),
                            GameTextType.Investigation
                        );
                    }
                }
            }
            catch
            {
                // Ignore errors
            }
        }

        /// <summary>
        /// Patch for navigating to previous piece with arrow keys.
        /// </summary>
        [HarmonyPatch(typeof(VasePuzzleMiniGame), "_prevCursor")]
        [HarmonyPostfix]
        public static void OnPrevCursor(VasePuzzleMiniGame __instance)
        {
            AnnounceCursorPosition(__instance);
        }

        /// <summary>
        /// Patch for navigating to next piece with arrow keys.
        /// </summary>
        [HarmonyPatch(typeof(VasePuzzleMiniGame), "_nextCursor")]
        [HarmonyPostfix]
        public static void OnNextCursor(VasePuzzleMiniGame __instance)
        {
            AnnounceCursorPosition(__instance);
        }

        /// <summary>
        /// Helper to announce current cursor position after navigation.
        /// </summary>
        private static void AnnounceCursorPosition(VasePuzzleMiniGame instance)
        {
            try
            {
                var cursorField = typeof(VasePuzzleMiniGame).GetField(
                    "icon_cursor_",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );

                var piecesField = typeof(VasePuzzleMiniGame).GetField(
                    "pieces_status_",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );

                if (cursorField != null && piecesField != null)
                {
                    int cursor = (int)cursorField.GetValue(instance);
                    var pieces = piecesField.GetValue(instance) as PiecesStatus[];

                    if (pieces != null && cursor < pieces.Length)
                    {
                        int displayNumber = cursor + 1;
                        int rotation = pieces[cursor].angle_id * 90;
                        SpeechManager.Announce(
                            L.Get("vase.piece_rotation", displayNumber, rotation),
                            GameTextType.Investigation
                        );
                    }
                }
            }
            catch
            {
                // Ignore errors
            }
        }
    }
}
