using System;
using System.Reflection;
using AccessibilityMod.Core;
using UnityAccessibilityLib;

namespace AccessibilityMod.Services
{
    /// <summary>
    /// Provides accessibility hints for the vase/jar puzzle mini-game.
    /// The puzzle requires selecting the correct fragment and rotating it to the correct orientation.
    /// </summary>
    public static class VasePuzzleNavigator
    {
        private static bool _wasActive = false;

        // The solution sequence for the 8-piece puzzle - piece index for each step (all require angle_id = 0)
        // From VasePuzzleUtil.puzzle_correct_data indices 0-7
        private static readonly int[] SolutionPieceOrder8 = { 4, 3, 5, 0, 7, 2, 1, 6 };

        // The 1-piece puzzle only has piece 0 which needs angle_id = 0
        // From VasePuzzleUtil.puzzle_correct_data index 8
        private static readonly int[] SolutionPieceOrder1 = { 0 };

        /// <summary>
        /// Gets the solution array for the current puzzle variant based on pieces count.
        /// </summary>
        private static int[] GetSolutionPieceOrder(int piecesCount)
        {
            return piecesCount == 1 ? SolutionPieceOrder1 : SolutionPieceOrder8;
        }

        /// <summary>
        /// Checks if the vase puzzle mini-game is currently active.
        /// </summary>
        public static bool IsVasePuzzleActive()
        {
            try
            {
                if (VasePuzzleMiniGame.instance != null)
                {
                    // Check if we're in the "free" state where input is accepted
                    var procField = typeof(VasePuzzleMiniGame).GetField(
                        "proc_id_",
                        BindingFlags.NonPublic | BindingFlags.Instance
                    );

                    if (procField != null)
                    {
                        var procValue = procField.GetValue(VasePuzzleMiniGame.instance);
                        // Proc.free = 3 is when player can interact
                        // Proc.none = 0 means inactive
                        int procId = Convert.ToInt32(procValue);
                        return procId != 0; // Active if not "none"
                    }
                }
            }
            catch
            {
                // Class may not exist
            }
            return false;
        }

        /// <summary>
        /// Called each frame to detect mode changes.
        /// </summary>
        public static void Update()
        {
            bool isActive = IsVasePuzzleActive();

            if (isActive && !_wasActive)
            {
                OnPuzzleStart();
            }
            else if (!isActive && _wasActive)
            {
                OnPuzzleEnd();
            }

            _wasActive = isActive;
        }

        private static void OnPuzzleStart()
        {
            // Try to detect which puzzle variant is active
            try
            {
                var piecesField = typeof(VasePuzzleMiniGame).GetField(
                    "pieces_status_",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );

                if (piecesField != null)
                {
                    var pieces =
                        piecesField.GetValue(VasePuzzleMiniGame.instance) as PiecesStatus[];
                    if (pieces != null && pieces.Length == 1)
                    {
                        SpeechManager.Announce(
                            L.Get("vase.start_final_piece"),
                            GameTextType.Investigation
                        );
                        return;
                    }
                }
            }
            catch
            {
                // Fall through to default message
            }

            SpeechManager.Announce(L.Get("vase.start"), GameTextType.Investigation);
        }

        private static void OnPuzzleEnd()
        {
            // Nothing special needed
        }

        /// <summary>
        /// Announces a hint for the current puzzle step.
        /// Tells the player which piece to select and how to rotate it.
        /// </summary>
        public static void AnnounceHint()
        {
            if (!IsVasePuzzleActive())
            {
                SpeechManager.Announce(L.Get("vase.not_in_mode"), GameTextType.SystemMessage);
                return;
            }

            try
            {
                // Get current step
                var stepField = typeof(VasePuzzleMiniGame).GetField(
                    "puzzle_step_",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );

                var restartField = typeof(VasePuzzleMiniGame).GetField(
                    "restert_step_",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );

                var cursorField = typeof(VasePuzzleMiniGame).GetField(
                    "icon_cursor_",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );

                var piecesField = typeof(VasePuzzleMiniGame).GetField(
                    "pieces_status_",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );

                if (stepField == null || cursorField == null || piecesField == null)
                {
                    SpeechManager.Announce(
                        L.Get("vase.unable_read_state"),
                        GameTextType.SystemMessage
                    );
                    return;
                }

                int puzzleStep = (int)stepField.GetValue(VasePuzzleMiniGame.instance);
                int restartStep =
                    restartField != null
                        ? (int)restartField.GetValue(VasePuzzleMiniGame.instance)
                        : 0;
                int currentCursor = (int)cursorField.GetValue(VasePuzzleMiniGame.instance);
                var pieces = piecesField.GetValue(VasePuzzleMiniGame.instance) as PiecesStatus[];

                if (pieces == null)
                {
                    SpeechManager.Announce(
                        L.Get("vase.unable_read_pieces"),
                        GameTextType.SystemMessage
                    );
                    return;
                }

                // Get the solution array for this puzzle variant
                int[] solutionPieceOrder = GetSolutionPieceOrder(pieces.Length);

                // Check if puzzle is complete
                if (puzzleStep >= solutionPieceOrder.Length)
                {
                    SpeechManager.Announce(L.Get("vase.complete"), GameTextType.Investigation);
                    return;
                }

                // Get the correct piece for this step
                int correctPieceIndex = solutionPieceOrder[puzzleStep];
                int piecesRemaining = solutionPieceOrder.Length - puzzleStep;

                // Build the hint message
                string hint = L.GetPlural("vase.pieces_remaining", piecesRemaining) + " ";

                // Check if they're on the correct piece
                if (currentCursor == correctPieceIndex)
                {
                    hint += L.Get("vase.correct_piece") + " ";

                    // Check rotation
                    int currentAngle = pieces[currentCursor].angle_id;
                    if (currentAngle == 0)
                    {
                        hint += L.Get("vase.rotation_correct");
                    }
                    else
                    {
                        // Calculate rotations needed
                        // Target is always 0
                        // rotateRight decrements: 3→2→1→0
                        // rotateLeft increments: 1→2→3→0
                        int rightPresses = currentAngle; // Direct path with R
                        int leftPresses = 4 - currentAngle; // Wrap around with Q

                        if (rightPresses <= leftPresses)
                        {
                            hint += L.GetPlural("vase.press_r_times", rightPresses);
                        }
                        else
                        {
                            hint += L.GetPlural("vase.press_q_times", leftPresses);
                        }
                    }
                }
                else
                {
                    // Tell them which piece to select
                    // Pieces are displayed 1-8 but indexed 0-7
                    int displayNumber = correctPieceIndex + 1;

                    // Calculate direction to move
                    int distance = correctPieceIndex - currentCursor;

                    // Account for used pieces when calculating moves
                    hint += L.Get("vase.select_piece", displayNumber) + " ";

                    if (distance > 0)
                    {
                        hint += L.Get("vase.press_right");
                    }
                    else
                    {
                        hint += L.Get("vase.press_left");
                    }
                }

                SpeechManager.Announce(hint, GameTextType.Investigation);
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error getting vase puzzle hint: {ex.Message}"
                );
                SpeechManager.Announce(L.Get("vase.unable_get_hint"), GameTextType.SystemMessage);
            }
        }

        /// <summary>
        /// Announces the current state - which piece is selected and its rotation.
        /// </summary>
        public static void AnnounceCurrentState()
        {
            if (!IsVasePuzzleActive())
            {
                SpeechManager.Announce(L.Get("vase.not_in_mode"), GameTextType.SystemMessage);
                return;
            }

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

                var stepField = typeof(VasePuzzleMiniGame).GetField(
                    "puzzle_step_",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );

                if (cursorField == null || piecesField == null || stepField == null)
                {
                    SpeechManager.Announce(
                        L.Get("vase.unable_read_state"),
                        GameTextType.SystemMessage
                    );
                    return;
                }

                int currentCursor = (int)cursorField.GetValue(VasePuzzleMiniGame.instance);
                var pieces = piecesField.GetValue(VasePuzzleMiniGame.instance) as PiecesStatus[];
                int puzzleStep = (int)stepField.GetValue(VasePuzzleMiniGame.instance);

                if (pieces == null || currentCursor >= pieces.Length)
                {
                    SpeechManager.Announce(
                        L.Get("vase.unable_read_pieces"),
                        GameTextType.SystemMessage
                    );
                    return;
                }

                // Get the solution array for this puzzle variant
                int[] solutionPieceOrder = GetSolutionPieceOrder(pieces.Length);
                int piecesRemaining = solutionPieceOrder.Length - puzzleStep;
                int displayNumber = currentCursor + 1;
                int rotation = pieces[currentCursor].angle_id * 90;
                bool isUsed = pieces[currentCursor].used;

                string state = L.Get("vase.piece_number", displayNumber);
                if (isUsed)
                {
                    state += " " + L.Get("vase.already_placed");
                }
                else
                {
                    state += ", " + L.Get("vase.rotated_degrees", rotation);
                }
                state += ". " + L.GetPlural("vase.pieces_remaining", piecesRemaining);

                SpeechManager.Announce(state, GameTextType.Investigation);
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error getting vase puzzle state: {ex.Message}"
                );
                SpeechManager.Announce(L.Get("vase.unable_read_state"), GameTextType.SystemMessage);
            }
        }
    }
}
