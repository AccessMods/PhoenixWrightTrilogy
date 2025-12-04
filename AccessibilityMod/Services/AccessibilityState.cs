using System;
using AccessibilityMod.Core;

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
                else if (IsInPointingMode())
                {
                    stateInfo = "Pointing mode";
                    int pointCount = PointingNavigator.GetPointCount();
                    if (pointCount > 0)
                    {
                        stateInfo += $". {pointCount} target areas. Use [ and ] to navigate.";
                    }
                    else
                    {
                        stateInfo += ". Use arrow keys to move cursor.";
                    }
                }
                else if (IsInInvestigationMode())
                {
                    stateInfo = "Investigation mode";
                    int hotspotCount = HotspotNavigator.GetHotspotCount();
                    int unexaminedCount = HotspotNavigator.GetUnexaminedCount();
                    if (hotspotCount > 0)
                    {
                        stateInfo +=
                            $". {hotspotCount} points of interest, {unexaminedCount} unexamined";
                    }
                }
                else if (IsInTrialMode())
                {
                    stateInfo = "Trial mode";
                    AnnounceLifeGauge();
                }
                else if (CurrentMode != GameMode.Unknown)
                {
                    stateInfo = $"{CurrentMode} mode";
                }
                else
                {
                    stateInfo = "Current state unknown";
                }

                ClipboardManager.Announce(stateInfo, TextType.SystemMessage);
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error announcing state: {ex.Message}"
                );
                ClipboardManager.Announce(
                    "Unable to determine current state",
                    TextType.SystemMessage
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
                    string message = $"Life gauge: {percentage} percent";

                    if (percentage <= 20)
                    {
                        message += " - DANGER!";
                    }

                    ClipboardManager.Announce(message, TextType.Trial);
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error getting life gauge: {ex.Message}"
                );
            }
        }
    }
}
