using System;
using System.Reflection;
using AccessibilityMod.Core;

namespace AccessibilityMod.Services
{
    /// <summary>
    /// Provides accessibility hints for the vase/jar puzzle mini-game.
    /// The puzzle requires selecting the correct fragment and rotating it to the correct orientation.
    /// </summary>
    public static class VasePuzzleNavigator
    {
        private static bool _wasActive = false;

        // The solution sequence - piece index for each step (all require angle_id = 0)
        // From VasePuzzleUtil.puzzle_correct_data
        private static readonly int[] SolutionPieceOrder = { 4, 3, 5, 0, 7, 2, 1, 6 };

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
            ClipboardManager.Announce(
                "Vase puzzle. Use Left/Right to select pieces, Q/R to rotate. Press H for hint, E to combine.",
                TextType.Investigation
            );
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
                ClipboardManager.Announce("Not in vase puzzle", TextType.SystemMessage);
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
                    ClipboardManager.Announce(
                        "Unable to read puzzle state",
                        TextType.SystemMessage
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
                    ClipboardManager.Announce("Unable to read pieces", TextType.SystemMessage);
                    return;
                }

                // Check if puzzle is complete
                if (puzzleStep >= SolutionPieceOrder.Length)
                {
                    ClipboardManager.Announce("Puzzle complete!", TextType.Investigation);
                    return;
                }

                // Get the correct piece for this step
                int correctPieceIndex = SolutionPieceOrder[puzzleStep];
                int piecesRemaining = SolutionPieceOrder.Length - puzzleStep;

                // Build the hint message
                string hint = $"{piecesRemaining} pieces remaining. ";

                // Check if they're on the correct piece
                if (currentCursor == correctPieceIndex)
                {
                    hint += "Correct piece selected. ";

                    // Check rotation
                    int currentAngle = pieces[currentCursor].angle_id;
                    if (currentAngle == 0)
                    {
                        hint += "Rotation correct. Press E to combine.";
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
                            hint +=
                                $"Press R {rightPresses} time{(rightPresses != 1 ? "s" : "")} to rotate.";
                        }
                        else
                        {
                            hint +=
                                $"Press Q {leftPresses} time{(leftPresses != 1 ? "s" : "")} to rotate.";
                        }
                    }
                }
                else
                {
                    // Tell them which piece to select
                    // Pieces are displayed 1-8 but indexed 0-7
                    int displayNumber = correctPieceIndex + 1;
                    int currentDisplay = currentCursor + 1;

                    // Calculate direction to move
                    int distance = correctPieceIndex - currentCursor;

                    // Account for used pieces when calculating moves
                    hint += $"Select piece {displayNumber}. ";

                    if (distance > 0)
                    {
                        hint += $"Press Right to navigate.";
                    }
                    else
                    {
                        hint += $"Press Left to navigate.";
                    }
                }

                ClipboardManager.Announce(hint, TextType.Investigation);
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error getting vase puzzle hint: {ex.Message}"
                );
                ClipboardManager.Announce("Unable to get hint", TextType.SystemMessage);
            }
        }

        /// <summary>
        /// Announces the current state - which piece is selected and its rotation.
        /// </summary>
        public static void AnnounceCurrentState()
        {
            if (!IsVasePuzzleActive())
            {
                ClipboardManager.Announce("Not in vase puzzle", TextType.SystemMessage);
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
                    ClipboardManager.Announce(
                        "Unable to read puzzle state",
                        TextType.SystemMessage
                    );
                    return;
                }

                int currentCursor = (int)cursorField.GetValue(VasePuzzleMiniGame.instance);
                var pieces = piecesField.GetValue(VasePuzzleMiniGame.instance) as PiecesStatus[];
                int puzzleStep = (int)stepField.GetValue(VasePuzzleMiniGame.instance);

                if (pieces == null || currentCursor >= pieces.Length)
                {
                    ClipboardManager.Announce("Unable to read pieces", TextType.SystemMessage);
                    return;
                }

                int piecesRemaining = SolutionPieceOrder.Length - puzzleStep;
                int displayNumber = currentCursor + 1;
                int rotation = pieces[currentCursor].angle_id * 90;
                bool isUsed = pieces[currentCursor].used;

                string state = $"Piece {displayNumber}";
                if (isUsed)
                {
                    state += " (already placed)";
                }
                else
                {
                    state += $", rotated {rotation} degrees";
                }
                state += $". {piecesRemaining} pieces remaining.";

                ClipboardManager.Announce(state, TextType.Investigation);
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error getting vase puzzle state: {ex.Message}"
                );
                ClipboardManager.Announce("Unable to read state", TextType.SystemMessage);
            }
        }
    }
}
