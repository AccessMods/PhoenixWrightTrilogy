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

                HandleInput();
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in OnUpdate: {ex.Message}");
            }
        }

        private void HandleInput()
        {
            // R - Repeat last output (disabled in vase puzzle mode since R is rotate)
            if (Input.GetKeyDown(KeyCode.R) && !AccessibilityState.IsInVasePuzzleMode())
            {
                ClipboardManager.RepeatLast();
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

        public override void OnDeinitializeMelon()
        {
            if (CoroutineRunner.Instance != null)
            {
                CoroutineRunner.Instance.StopClipboardProcessor();
                UnityEngine.Object.Destroy(CoroutineRunner.Instance.gameObject);
            }

            Logger.Msg("Phoenix Wright Accessibility Mod deinitialized.");
        }
    }
}
