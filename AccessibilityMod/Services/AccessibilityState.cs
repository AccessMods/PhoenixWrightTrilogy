using System;
using AccessibilityMod.Core;
using UnityAccessibilityLib;

namespace AccessibilityMod.Services
{
    public static class AccessibilityState
    {
        public enum GameMode
        {
            Unknown,
            MainMenu,
            Investigation,
            Trial,
            Dialogue,
            Menu,
            Gallery,
            Options,
        }

        public static GameMode CurrentMode { get; private set; } = GameMode.Unknown;

        public static void SetMode(GameMode mode)
        {
            if (CurrentMode != mode)
            {
                CurrentMode = mode;
                AccessibilityMod.Core.AccessibilityMod.Logger?.Msg($"Game mode changed to: {mode}");
            }
        }

        public static bool IsInInvestigationMode()
        {
            try
            {
                // Check if inspectCtrl is active
                if (inspectCtrl.instance != null && inspectCtrl.instance.is_play)
                {
                    return true;
                }
            }
            catch
            {
                // Class may not exist in current context
            }
            return false;
        }

        public static bool IsIn3DEvidenceMode()
        {
            try
            {
                // Check if 3D evidence examination is active (GS1 Episode 5+)
                if (
                    scienceInvestigationCtrl.instance != null
                    && scienceInvestigationCtrl.instance.is_play
                )
                {
                    return true;
                }
            }
            catch
            {
                // Class may not exist in current context
            }
            return false;
        }

        public static bool IsInTrialMode()
        {
            try
            {
                // Check if we're in a trial/court scene
                if (GSStatic.global_work_ != null)
                {
                    // Check for trial-related states
                    var r = GSStatic.global_work_.r;
                    // Trial states are typically 4 (questioning) or 7 (testimony)
                    return r.no_0 == 4 || r.no_0 == 7;
                }
            }
            catch
            {
                // Class may not exist in current context
            }
            return false;
        }

        public static bool IsInPointingMode()
        {
            return PointingNavigator.IsPointingActive();
        }

        public static bool IsInLuminolMode()
        {
            return LuminolNavigator.IsLuminolActive();
        }

        public static bool IsInVasePuzzleMode()
        {
            return VasePuzzleNavigator.IsVasePuzzleActive();
        }

        public static bool IsInFingerprintMode()
        {
            return FingerprintNavigator.IsFingerprintActive();
        }

        public static bool IsInVideoTapeMode()
        {
            return VideoTapeNavigator.IsVideoTapeActive();
        }

        public static bool IsInVaseShowMode()
        {
            return VaseShowNavigator.IsActive();
        }

        public static bool IsInDyingMessageMode()
        {
            return DyingMessageNavigator.IsActive();
        }

        public static bool IsInBugSweeperMode()
        {
            return BugSweeperNavigator.IsBugSweeperActive();
        }

        public static bool IsInOrchestraMode()
        {
            return GalleryOrchestraNavigator.IsOrchestraActive();
        }

        public static bool IsInCourtRecordMode()
        {
            try
            {
                var ctrl = recordListCtrl.instance;
                if (ctrl != null && ctrl.body_active && ctrl.is_open)
                {
                    return true;
                }
            }
            catch
            {
                // Class may not exist in current context
            }
            return false;
        }

        public static bool IsInEvidenceDetailsMode()
        {
            try
            {
                var ctrl = recordListCtrl.instance;
                if (ctrl != null && ctrl.detail_open)
                {
                    return true;
                }
            }
            catch
            {
                // Class may not exist in current context
            }
            return false;
        }

        public static void AnnounceCurrentState()
        {
            try
            {
                string stateInfo = "";

                if (IsIn3DEvidenceMode())
                {
                    // Delegate to Evidence3DNavigator for detailed state
                    Evidence3DNavigator.AnnounceState();
                    return;
                }
                else if (IsInLuminolMode())
                {
                    // Delegate to LuminolNavigator for detailed state
                    LuminolNavigator.AnnounceState();
                    return;
                }
                else if (IsInVasePuzzleMode())
                {
                    // Delegate to VasePuzzleNavigator for detailed state
                    VasePuzzleNavigator.AnnounceCurrentState();
                    return;
                }
                else if (IsInFingerprintMode())
                {
                    // Delegate to FingerprintNavigator for detailed state
                    FingerprintNavigator.AnnounceState();
                    return;
                }
                else if (IsInVideoTapeMode())
                {
                    // Delegate to VideoTapeNavigator for detailed state
                    VideoTapeNavigator.AnnounceState();
                    return;
                }
                else if (IsInVaseShowMode())
                {
                    // Delegate to VaseShowNavigator for detailed state
                    VaseShowNavigator.AnnounceState();
                    return;
                }
                else if (IsInDyingMessageMode())
                {
                    // Delegate to DyingMessageNavigator for detailed state
                    DyingMessageNavigator.AnnounceState();
                    return;
                }
                else if (IsInBugSweeperMode())
                {
                    // Delegate to BugSweeperNavigator for detailed state
                    BugSweeperNavigator.AnnounceState();
                    return;
                }
                else if (IsInOrchestraMode())
                {
                    // Delegate to GalleryOrchestraNavigator for detailed state
                    GalleryOrchestraNavigator.AnnounceState();
                    return;
                }
                else if (IsInEvidenceDetailsMode())
                {
                    // Announce evidence details state
                    stateInfo = L.Get("court_record.details_view");
                }
                else if (IsInCourtRecordMode())
                {
                    // Announce court record state
                    try
                    {
                        var ctrl = recordListCtrl.instance;
                        string tabName =
                            ctrl.record_type == 0
                                ? L.Get("court_record.evidence")
                                : L.Get("court_record.profiles");
                        int itemCount = 0;
                        if (ctrl.record_data_ != null && ctrl.record_type < ctrl.record_data_.Count)
                        {
                            itemCount = ctrl.record_data_[ctrl.record_type].cursor_num_;
                        }
                        stateInfo = L.Get("court_record.state", tabName, itemCount);
                    }
                    catch
                    {
                        stateInfo = L.Get("mode.court_record");
                    }
                }
                else if (IsInPointingMode())
                {
                    stateInfo = L.Get("mode.pointing");
                    int pointCount = PointingNavigator.GetPointCount();
                    if (pointCount > 0)
                    {
                        stateInfo +=
                            ". "
                            + L.Get("navigation.x_target_areas", pointCount)
                            + ". "
                            + L.Get("navigation.use_brackets_navigate");
                    }
                }
                else if (IsInInvestigationMode())
                {
                    int hotspotCount = HotspotNavigator.GetHotspotCount();
                    int unexaminedCount = HotspotNavigator.GetUnexaminedCount();
                    if (hotspotCount > 0)
                    {
                        // Check if this scene supports Q-switch (left/right panning)
                        bool shouldShowSide = false;
                        string side = "";
                        try
                        {
                            int bgNo = -1;
                            float bgPosX = 0f;
                            bool canSlide = false;
                            float effectiveWidth = 1920f;
                            try
                            {
                                if (bgCtrl.instance != null)
                                {
                                    bgNo = bgCtrl.instance.bg_no;
                                    bgPosX = bgCtrl.instance.bg_pos_x;
                                }
                            }
                            catch { }
                            try
                            {
                                canSlide = GSMain_TanteiPart.IsBGSlide(bgNo);
                            }
                            catch { }

                            // Calculate effective width from hotspot data
                            try
                            {
                                if (GSStatic.inspect_data_ != null)
                                {
                                    float maxX = 1920f;
                                    for (int i = 0; i < GSStatic.inspect_data_.Length; i++)
                                    {
                                        var data = GSStatic.inspect_data_[i];
                                        if (data == null || data.place == uint.MaxValue)
                                            break;
                                        if (data.place == 254)
                                            continue;
                                        float centerX =
                                            (data.x0 + data.x1 + data.x2 + data.x3) / 4f;
                                        if (centerX > maxX)
                                            maxX = centerX;
                                    }
                                    effectiveWidth = maxX;
                                }
                            }
                            catch { }

                            shouldShowSide = canSlide && effectiveWidth > 1920f;

                            if (shouldShowSide)
                            {
                                // Determine which side we're currently on
                                // bg_pos_x < 960 means left side (showing X coordinates 0-1920)
                                // bg_pos_x >= 960 means right side (showing X coordinates 1920+)
                                side =
                                    bgPosX < 960f
                                        ? L.Get("investigation.side_left")
                                        : L.Get("investigation.side_right");
                            }
                        }
                        catch { }

                        if (shouldShowSide)
                        {
                            // Use format with side information
                            stateInfo = L.Get(
                                "investigation.state_with_side",
                                side,
                                hotspotCount,
                                unexaminedCount
                            );
                        }
                        else
                        {
                            // Use original format without side
                            stateInfo = L.Get("mode.investigation");
                            stateInfo +=
                                ". "
                                + L.Get(
                                    "investigation.mode_entry_with_unexamined",
                                    hotspotCount,
                                    unexaminedCount
                                );
                        }
                    }
                    else
                    {
                        stateInfo = L.Get("mode.investigation");
                    }
                }
                else if (IsInTrialMode())
                {
                    stateInfo = L.Get("mode.trial");
                    AnnounceLifeGauge();
                }
                else if (CurrentMode != GameMode.Unknown)
                {
                    var modeKey = GetModeLocalizationKey(CurrentMode);
                    stateInfo =
                        modeKey != null ? L.Get(modeKey) : L.Get("system.unable_to_read_state");
                }
                else
                {
                    stateInfo = L.Get("system.unable_to_read_state");
                }

                SpeechManager.Announce(stateInfo, GameTextType.SystemMessage);
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error announcing state: {ex.Message}"
                );
                SpeechManager.Announce(
                    L.Get("system.unable_to_read_state"),
                    GameTextType.SystemMessage
                );
            }
        }

        public static void AnnounceLifeGauge()
        {
            try
            {
                if (GSStatic.global_work_ != null)
                {
                    int health;
                    int maxHealth;

                    // GS1 uses 'rest' system (0-5 scale)
                    // GS2/GS3 use 'gauge_hp' system (0-80 scale)
                    if (GSStatic.global_work_.title == TitleId.GS1)
                    {
                        health = GSStatic.global_work_.rest;
                        maxHealth = 5;
                    }
                    else
                    {
                        health = GSStatic.global_work_.gauge_hp;
                        maxHealth = 80;
                    }

                    // Convert to percentage for consistent announcement
                    int percentage = (int)((float)health / maxHealth * 100);
                    string message;

                    if (percentage <= 20)
                    {
                        message = L.Get("trial.life_gauge_danger", percentage);
                    }
                    else
                    {
                        message = L.Get("trial.life_gauge", percentage);
                    }

                    SpeechManager.Announce(message, GameTextType.Trial);
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error getting life gauge: {ex.Message}"
                );
            }
        }

        private static string GetModeLocalizationKey(GameMode mode)
        {
            switch (mode)
            {
                case GameMode.MainMenu:
                    return "mode.main_menu";
                case GameMode.Investigation:
                    return "mode.investigation";
                case GameMode.Trial:
                    return "mode.trial";
                case GameMode.Dialogue:
                    return "mode.dialogue";
                case GameMode.Menu:
                    return "mode.menu";
                case GameMode.Gallery:
                    return "mode.gallery";
                case GameMode.Options:
                    return "mode.options";
                default:
                    return null;
            }
        }
    }
}
