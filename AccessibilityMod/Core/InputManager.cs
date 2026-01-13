using System;
using AccessibilityMod.Patches;
using AccessibilityMod.Services;
using UnityAccessibilityLib;
using UnityEngine;

namespace AccessibilityMod.Core
{
    /// <summary>
    /// Handles all keyboard input for the accessibility mod.
    /// </summary>
    public static class InputManager
    {
        /// <summary>
        /// Process input each frame. Called from AccessibilityMod.OnUpdate().
        /// </summary>
        public static void ProcessInput()
        {
            // F5 - Hot-reload configuration files
            if (Input.GetKeyDown(KeyCode.F5))
            {
                ReloadConfigFiles();
            }

            // R - Repeat last output (disabled in vase puzzle, vase show, and court record modes since R has other functions)
            if (
                Input.GetKeyDown(KeyCode.R)
                && !AccessibilityState.IsInVasePuzzleMode()
                && !AccessibilityState.IsInVaseShowMode()
                && !AccessibilityState.IsInCourtRecordMode()
            )
            {
                SpeechManager.RepeatLast();
            }

            // I - Announce current context/state
            if (Input.GetKeyDown(KeyCode.I))
            {
                AccessibilityState.AnnounceCurrentState();
            }

            ProcessModeSpecificInput();
        }

        private static void ProcessModeSpecificInput()
        {
            // 3D evidence examination mode (GS1 Episode 5+)
            if (AccessibilityState.IsIn3DEvidenceMode())
            {
                Handle3DEvidenceInput();
            }
            // Luminol spray mode (GS1 Episode 5)
            else if (AccessibilityState.IsInLuminolMode())
            {
                HandleLuminolInput();
            }
            // Vase puzzle mode (GS1 Episode 5)
            else if (AccessibilityState.IsInVasePuzzleMode())
            {
                HandleVasePuzzleInput();
            }
            // Fingerprint mode (GS1 Episode 5)
            else if (AccessibilityState.IsInFingerprintMode())
            {
                HandleFingerprintInput();
            }
            // Video tape examination mode (GS1 Episode 5)
            else if (AccessibilityState.IsInVideoTapeMode())
            {
                HandleVideoTapeInput();
            }
            // Orchestra music player mode
            else if (AccessibilityState.IsInOrchestraMode())
            {
                HandleOrchestraInput();
            }
            // Dying message mode (GS1 Episode 5 - connect the dots)
            else if (AccessibilityState.IsInDyingMessageMode())
            {
                HandleDyingMessageInput();
            }
            // Bug sweeper mode (GS2/GS3 - scan for listening devices)
            else if (AccessibilityState.IsInBugSweeperMode())
            {
                HandleBugSweeperInput();
            }
            // Vase show rotation mode (GS1 Episode 5 - unstable jar)
            else if (AccessibilityState.IsInVaseShowMode())
            {
                HandleVaseShowInput();
            }
            // Pointing mode navigation (court maps, etc.)
            else if (AccessibilityState.IsInPointingMode())
            {
                HandlePointingInput();
            }
            // Investigation mode hotspot navigation
            else if (AccessibilityState.IsInInvestigationMode())
            {
                HandleInvestigationInput();
            }
            // H - Announce life gauge (in trial, but not in pointing mode)
            else if (Input.GetKeyDown(KeyCode.H) && AccessibilityState.IsInTrialMode())
            {
                AccessibilityState.AnnounceLifeGauge();
            }
        }

        private static void Handle3DEvidenceInput()
        {
            if (Input.GetKeyDown(KeyCode.LeftBracket))
            {
                Evidence3DNavigator.NavigatePrevious();
            }

            if (Input.GetKeyDown(KeyCode.RightBracket))
            {
                Evidence3DNavigator.NavigateNext();
            }
        }

        private static void HandleLuminolInput()
        {
            if (Input.GetKeyDown(KeyCode.LeftBracket))
            {
                LuminolNavigator.NavigatePrevious();
            }

            if (Input.GetKeyDown(KeyCode.RightBracket))
            {
                LuminolNavigator.NavigateNext();
            }
        }

        private static void HandleVasePuzzleInput()
        {
            // H - Get hint for current step
            if (Input.GetKeyDown(KeyCode.H))
            {
                VasePuzzleNavigator.AnnounceHint();
            }
        }

        private static void HandleFingerprintInput()
        {
            // [ and ] - Navigate fingerprint locations during selection phase
            if (Input.GetKeyDown(KeyCode.LeftBracket))
            {
                FingerprintNavigator.NavigatePrevious();
            }

            if (Input.GetKeyDown(KeyCode.RightBracket))
            {
                FingerprintNavigator.NavigateNext();
            }

            // H - Get hint for current phase
            if (Input.GetKeyDown(KeyCode.H))
            {
                FingerprintNavigator.AnnounceHint();
            }
        }

        private static void HandleVideoTapeInput()
        {
            // [ and ] - Navigate to targets when paused
            if (Input.GetKeyDown(KeyCode.LeftBracket))
            {
                VideoTapeNavigator.NavigateToPreviousTarget();
            }

            if (Input.GetKeyDown(KeyCode.RightBracket))
            {
                VideoTapeNavigator.NavigateToNextTarget();
            }

            // H - Get hint for current viewing
            if (Input.GetKeyDown(KeyCode.H))
            {
                VideoTapeNavigator.AnnounceHint();
            }
        }

        private static void HandleOrchestraInput()
        {
            // F1 - Announce controls help
            if (Input.GetKeyDown(KeyCode.F1))
            {
                GalleryOrchestraNavigator.AnnounceHelp();
            }
        }

        private static void HandleDyingMessageInput()
        {
            // [ and ] - Navigate between dots
            if (Input.GetKeyDown(KeyCode.LeftBracket))
            {
                DyingMessageNavigator.NavigatePrevious();
            }

            if (Input.GetKeyDown(KeyCode.RightBracket))
            {
                DyingMessageNavigator.NavigateNext();
            }

            // H - Get hint for spelling EMA
            if (Input.GetKeyDown(KeyCode.H))
            {
                DyingMessageNavigator.AnnounceHint();
            }
        }

        private static void HandleBugSweeperInput()
        {
            // H - Announce current state/hint
            if (Input.GetKeyDown(KeyCode.H))
            {
                BugSweeperNavigator.AnnounceState();
            }
        }

        private static void HandleVaseShowInput()
        {
            // G - Get hint for rotation (H is used for X-axis rotation in this puzzle)
            if (Input.GetKeyDown(KeyCode.G))
            {
                VaseShowNavigator.AnnounceHint();
            }
        }

        private static void HandlePointingInput()
        {
            if (Input.GetKeyDown(KeyCode.LeftBracket))
            {
                PointingNavigator.NavigatePrevious();
            }

            if (Input.GetKeyDown(KeyCode.RightBracket))
            {
                PointingNavigator.NavigateNext();
            }

            // H - List all target areas
            if (Input.GetKeyDown(KeyCode.H))
            {
                PointingNavigator.AnnounceAllPoints();
            }
        }

        private static void HandleInvestigationInput()
        {
            if (Input.GetKeyDown(KeyCode.LeftBracket))
            {
                HotspotNavigator.NavigatePrevious();
            }

            if (Input.GetKeyDown(KeyCode.RightBracket))
            {
                HotspotNavigator.NavigateNext();
            }

            // U - Next unexamined hotspot
            if (Input.GetKeyDown(KeyCode.U))
            {
                HotspotNavigator.NavigateToNextUnexamined();
            }

            // H - List all hotspots
            if (Input.GetKeyDown(KeyCode.H))
            {
                HotspotNavigator.AnnounceAllHotspots();
            }
        }

        private static void ReloadConfigFiles()
        {
            try
            {
                // Reload localization first (may affect other services' paths)
                LocalizationService.ReloadFromFiles();
                CharacterNameService.ReloadFromFiles();
                EvidenceDetailService.ReloadFromFiles();
                StaffRollPatches.ReloadData();
                SpeechManager.Announce(L.Get("system.config_reloaded"));
                AccessibilityMod.Logger.Msg("Configuration files reloaded via F5");
            }
            catch (Exception ex)
            {
                AccessibilityMod.Logger.Error($"Error reloading config files: {ex.Message}");
                SpeechManager.Announce(L.Get("system.config_reload_error"));
            }
        }
    }
}
