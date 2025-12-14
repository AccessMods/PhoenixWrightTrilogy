using System;
using AccessibilityMod.Core;
using AccessibilityMod.Services;
using HarmonyLib;

namespace AccessibilityMod.Patches
{
    [HarmonyPatch]
    public static class LifeGaugePatches
    {
        private static int _lastHealth = -1;

        // Hook life gauge state changes
        [HarmonyPostfix]
        [HarmonyPatch(typeof(lifeGaugeCtrl), "ActionLifeGauge")]
        public static void ActionLifeGauge_Postfix(
            lifeGaugeCtrl __instance,
            lifeGaugeCtrl.Gauge_State _state
        )
        {
            try
            {
                switch (_state)
                {
                    case lifeGaugeCtrl.Gauge_State.LIFE_ON:
                    case lifeGaugeCtrl.Gauge_State.LIFE_ON_MOMENT:
                        // Life gauge became visible
                        AccessibilityState.SetMode(AccessibilityState.GameMode.Trial);
                        break;

                    case lifeGaugeCtrl.Gauge_State.DAMAGE:
                        // Only announce if gauge is actually visible (not during loading)
                        if (__instance.body_active)
                        {
                            AnnounceCurrentHealth();
                        }
                        break;

                    case lifeGaugeCtrl.Gauge_State.UPDATE_REST:
                        // Only announce if gauge is actually visible (not during loading)
                        if (__instance.body_active)
                        {
                            AnnounceCurrentHealth();
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in LifeGauge ActionLifeGauge patch: {ex.Message}"
                );
            }
        }

        private static void AnnounceCurrentHealth()
        {
            try
            {
                if (GSStatic.global_work_ == null)
                    return;

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

                // Convert to percentage
                int percentage = (int)((float)health / maxHealth * 100);

                if (percentage != _lastHealth)
                {
                    _lastHealth = percentage;

                    string message = $"Health: {percentage} percent";

                    if (percentage <= 20)
                    {
                        message += " - DANGER!";
                    }
                    else if (percentage <= 0)
                    {
                        message = "Game Over!";
                    }

                    SpeechManager.Announce(message, TextType.Trial);
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error announcing health: {ex.Message}"
                );
            }
        }
    }
}
