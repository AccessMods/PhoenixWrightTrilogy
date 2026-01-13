using System;
using System.Collections.Generic;
using System.IO;
using AccessibilityMod.Core;
using AccessibilityMod.Services;
using HarmonyLib;
using UnityAccessibilityLib;

namespace AccessibilityMod.Patches
{
    /// <summary>
    /// Patches for capturing staff roll credits during story endings.
    /// The staff roll displays credit images (sprites) rather than text,
    /// so we need to announce text descriptions from a data file.
    /// </summary>
    [HarmonyPatch]
    public static class StaffRollPatches
    {
        // Cached staff roll text data per language
        private static Dictionary<string, List<string>> _staffRollData =
            new Dictionary<string, List<string>>();

        // Track last announced index to prevent duplicates
        private static int _lastAnnouncedIndex = -1;

        // Track if staff roll is active
        private static bool _isStaffRollActive = false;

        #region Harmony Patches

        /// <summary>
        /// Hook staffrollCtrl.init() to detect when staff roll sequence starts.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(staffrollCtrl), "init")]
        public static void Init_Postfix(staffrollCtrl __instance)
        {
            try
            {
                _isStaffRollActive = true;
                _lastAnnouncedIndex = -1;

                // Load staff roll data for current language if not cached
                EnsureDataLoaded();

                AccessibilityMod.Core.AccessibilityMod.Logger?.Msg("Staff roll initialized");
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in staffrollCtrl.init patch: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Hook staffrollCtrl.play() to announce each staff credit as it appears.
        /// This is called when message code 64 triggers the next staff image.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(staffrollCtrl), "play")]
        public static void Play_Postfix(staffrollCtrl __instance)
        {
            try
            {
                if (!_isStaffRollActive)
                    return;

                // Get current index via reflection
                int currentIndex = GetCurrentIndex(__instance);
                if (currentIndex < 0 || currentIndex == _lastAnnouncedIndex)
                    return;

                _lastAnnouncedIndex = currentIndex;

                // Get the text for this staff roll entry
                string text = GetStaffRollText(currentIndex);
                if (!Net35Extensions.IsNullOrWhiteSpace(text))
                {
                    SpeechManager.Output("", text, GameTextType.Credits);
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in staffrollCtrl.play patch: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Hook staffrollCtrl.end() to detect when staff roll sequence ends.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(staffrollCtrl), "end")]
        public static void End_Postfix()
        {
            try
            {
                _isStaffRollActive = false;
                _lastAnnouncedIndex = -1;

                AccessibilityMod.Core.AccessibilityMod.Logger?.Msg("Staff roll ended");
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in staffrollCtrl.end patch: {ex.Message}"
                );
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Get the current_index_ field from staffrollCtrl via reflection.
        /// </summary>
        private static int GetCurrentIndex(staffrollCtrl instance)
        {
            try
            {
                var field = typeof(staffrollCtrl).GetField(
                    "current_index_",
                    System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Instance
                );
                if (field != null)
                {
                    return (int)field.GetValue(instance);
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error getting current_index_: {ex.Message}"
                );
            }
            return -1;
        }

        /// <summary>
        /// Get the text content for a specific staff roll index.
        /// </summary>
        private static string GetStaffRollText(int index)
        {
            try
            {
                string langCode = LocalizationService.GetLanguageFolderName();
                if (_staffRollData.ContainsKey(langCode) && index < _staffRollData[langCode].Count)
                {
                    return _staffRollData[langCode][index];
                }

                // Fall back to English
                if (
                    langCode != "en"
                    && _staffRollData.ContainsKey("en")
                    && index < _staffRollData["en"].Count
                )
                {
                    return _staffRollData["en"][index];
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error getting staff roll text for index {index}: {ex.Message}"
                );
            }
            return null;
        }

        /// <summary>
        /// Ensure staff roll data is loaded for the current language.
        /// </summary>
        private static void EnsureDataLoaded()
        {
            string langCode = LocalizationService.GetLanguageFolderName();

            if (!_staffRollData.ContainsKey(langCode))
            {
                LoadStaffRollData(langCode);
            }

            // Also ensure English fallback is loaded
            if (langCode != "en" && !_staffRollData.ContainsKey("en"))
            {
                LoadStaffRollData("en");
            }
        }

        /// <summary>
        /// Load staff roll data from the data file.
        /// Format: One line per staff roll entry, in order.
        /// Lines starting with # are comments and are ignored.
        /// Empty lines represent staff entries with no text (silent entries).
        /// </summary>
        private static void LoadStaffRollData(string langCode)
        {
            try
            {
                // Get path to UserData/AccessibilityMod/<langCode>/StaffRoll.txt
                string gameDir = AppDomain.CurrentDomain.BaseDirectory;
                string basePath = Path.Combine(
                    Path.Combine(Path.Combine(gameDir, "UserData"), "AccessibilityMod"),
                    langCode
                );
                string filePath = Path.Combine(basePath, "StaffRoll.txt");

                if (!File.Exists(filePath))
                {
                    AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                        $"Staff roll data file not found: {filePath}"
                    );
                    _staffRollData[langCode] = new List<string>();
                    return;
                }

                var lines = new List<string>();
                using (var reader = new StreamReader(filePath, System.Text.Encoding.UTF8))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        string trimmed = line.Trim();

                        // Skip comment lines
                        if (trimmed.StartsWith("#"))
                            continue;

                        // Add the line (empty lines become silent entries)
                        lines.Add(trimmed);
                    }
                }

                _staffRollData[langCode] = lines;

                AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                    $"Loaded {lines.Count} staff roll entries for {langCode}"
                );
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error loading staff roll data for {langCode}: {ex.Message}"
                );
                _staffRollData[langCode] = new List<string>();
            }
        }

        /// <summary>
        /// Reload staff roll data (called on F5 hot-reload).
        /// </summary>
        public static void ReloadData()
        {
            _staffRollData.Clear();
            EnsureDataLoaded();
        }

        #endregion
    }
}
