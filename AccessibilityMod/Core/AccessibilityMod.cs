using System;
using AccessibilityMod.Services;
using MelonLoader;
using UnityEngine;

namespace AccessibilityMod.Core
{
    public class AccessibilityMod : MelonMod
    {
        public static MelonLogger.Instance Logger { get; private set; }
        public static AccessibilityMod Instance { get; private set; }

        private static bool _isInitialized = false;

        public override void OnInitializeMelon()
        {
            Instance = this;
            Logger = LoggerInstance;
            Logger.Msg("Phoenix Wright Accessibility Mod initializing...");
        }

        private void InitializeAccessibility()
        {
            if (_isInitialized)
                return;

            try
            {
                // Initialize localization first (needed by other services)
                LocalizationService.Initialize();

                // Create the coroutine runner for clipboard processing
                GameObject managerObject = new GameObject("AccessibilityMod_CoroutineRunner");
                managerObject.AddComponent<CoroutineRunner>();

                Logger.Msg("Accessibility systems initialized successfully");
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to initialize accessibility systems: {ex.Message}");
            }
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            Logger.Msg($"Scene loaded: {sceneName} (Index: {buildIndex})");

            // Initialize on first scene load when Unity is ready
            InitializeAccessibility();
        }

        public override void OnUpdate()
        {
            try
            {
                // Update navigators to detect mode changes
                PointingNavigator.Update();
                LuminolNavigator.Update();
                VasePuzzleNavigator.Update();
                FingerprintNavigator.Update();
                VideoTapeNavigator.Update();
                VaseShowNavigator.Update();
                DyingMessageNavigator.Update();
                BugSweeperNavigator.Update();

                HandleInput();
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in OnUpdate: {ex.Message}");
            }
        }

        private void HandleInput()
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

            // 3D evidence examination mode (GS1 Episode 5+)
            if (AccessibilityState.IsIn3DEvidenceMode())
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
            // Luminol spray mode (GS1 Episode 5)
            else if (AccessibilityState.IsInLuminolMode())
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
            // Vase puzzle mode (GS1 Episode 5)
            else if (AccessibilityState.IsInVasePuzzleMode())
            {
                // H - Get hint for current step
                if (Input.GetKeyDown(KeyCode.H))
                {
                    VasePuzzleNavigator.AnnounceHint();
                }
            }
            // Fingerprint mode (GS1 Episode 5)
            else if (AccessibilityState.IsInFingerprintMode())
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
            // Video tape examination mode (GS1 Episode 5)
            else if (AccessibilityState.IsInVideoTapeMode())
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
            // Orchestra music player mode
            else if (AccessibilityState.IsInOrchestraMode())
            {
                // F1 - Announce controls help
                if (Input.GetKeyDown(KeyCode.F1))
                {
                    GalleryOrchestraNavigator.AnnounceHelp();
                }
            }
            // Dying message mode (GS1 Episode 5 - connect the dots)
            else if (AccessibilityState.IsInDyingMessageMode())
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
            // Bug sweeper mode (GS2/GS3 - scan for listening devices)
            else if (AccessibilityState.IsInBugSweeperMode())
            {
                // H - Announce current state/hint
                if (Input.GetKeyDown(KeyCode.H))
                {
                    BugSweeperNavigator.AnnounceState();
                }
            }
            // Vase show rotation mode (GS1 Episode 5 - unstable jar)
            else if (AccessibilityState.IsInVaseShowMode())
            {
                // G - Get hint for rotation (H is used for X-axis rotation in this puzzle)
                if (Input.GetKeyDown(KeyCode.G))
                {
                    VaseShowNavigator.AnnounceHint();
                }
            }
            // Pointing mode navigation (court maps, etc.)
            else if (AccessibilityState.IsInPointingMode())
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
            // Investigation mode hotspot navigation
            else if (AccessibilityState.IsInInvestigationMode())
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
            // H - Announce life gauge (in trial, but not in pointing mode)
            else if (Input.GetKeyDown(KeyCode.H) && AccessibilityState.IsInTrialMode())
            {
                AccessibilityState.AnnounceLifeGauge();
            }
        }

        /// <summary>
        /// Reload configuration files for hot-reload during development.
        /// Press F5 in-game to trigger this.
        /// </summary>
        private void ReloadConfigFiles()
        {
            try
            {
                // Reload localization first (may affect other services' paths)
                LocalizationService.ReloadFromFiles();
                CharacterNameService.ReloadFromFiles();
                EvidenceDetailService.ReloadFromFiles();
                SpeechManager.Announce(L.Get("system.config_reloaded"));
                Logger.Msg("Configuration files reloaded via F5");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error reloading config files: {ex.Message}");
                SpeechManager.Announce(L.Get("system.config_reload_error"));
            }
        }

        public override void OnDeinitializeMelon()
        {
            if (CoroutineRunner.Instance != null)
            {
                UnityEngine.Object.Destroy(CoroutineRunner.Instance.gameObject);
            }

            Logger.Msg("Phoenix Wright Accessibility Mod deinitialized.");
        }
    }
}
