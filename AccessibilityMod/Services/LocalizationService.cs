using System;
using System.Collections.Generic;
using System.IO;
using AccessibilityMod.Core;
using AccessibilityMod.Utilities;
using UnityAccessibilityLib;

namespace AccessibilityMod.Services
{
    /// <summary>
    /// Centralized service for localized string management.
    /// Detects game language and loads appropriate translations with fallback to English.
    /// </summary>
    public static class LocalizationService
    {
        private static readonly Dictionary<Language, string> LanguageFolderMap = new Dictionary<
            Language,
            string
        >
        {
            { Language.USA, "en" },
            { Language.JAPAN, "ja" },
            { Language.FRANCE, "fr" },
            { Language.GERMAN, "de" },
            { Language.KOREA, "ko" },
            { Language.CHINA_S, "zh-Hans" },
            { Language.CHINA_T, "zh-Hant" },
            { Language.Pt_BR, "pt-BR" },
            { Language.ES_419, "es" },
        };

        private static Dictionary<string, string> _strings = new Dictionary<string, string>();
        private static Dictionary<string, string> _fallbackStrings =
            new Dictionary<string, string>();
        private static Language _currentLanguage = Language.USA;
        private static Language _loadedLanguage = Language.USA;
        private static bool _initialized = false;

        private static string BaseFolder
        {
            get
            {
                string gameDir = AppDomain.CurrentDomain.BaseDirectory;
                return Path.Combine(Path.Combine(gameDir, "UserData"), "AccessibilityMod");
            }
        }

        /// <summary>
        /// Initialize the localization service. Call this during mod startup.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;
            DetectLanguage();
            LoadStrings();
            AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                $"LocalizationService initialized for language: {_currentLanguage} ({GetLanguageFolderName()})"
            );
        }

        /// <summary>
        /// Reload all localization files. Call this on F5 hot-reload.
        /// </summary>
        public static void ReloadFromFiles()
        {
            DetectLanguage();
            LoadStrings();
            AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                $"LocalizationService reloaded for language: {_currentLanguage} ({GetLanguageFolderName()})"
            );
        }

        /// <summary>
        /// Get the current game language.
        /// </summary>
        public static Language GetCurrentLanguage()
        {
            return _currentLanguage;
        }

        /// <summary>
        /// Get the folder name for the current language (e.g., "en", "ja", "fr").
        /// </summary>
        public static string GetLanguageFolderName()
        {
            string folderName;
            if (LanguageFolderMap.TryGetValue(_currentLanguage, out folderName))
                return folderName;
            return "en"; // Default to English
        }

        /// <summary>
        /// Get the full path to the current language folder.
        /// </summary>
        public static string GetLanguageFolder()
        {
            return Path.Combine(BaseFolder, GetLanguageFolderName());
        }

        /// <summary>
        /// Get the full path to the English (fallback) language folder.
        /// </summary>
        public static string GetEnglishFolder()
        {
            return Path.Combine(BaseFolder, "en");
        }

        /// <summary>
        /// Get a localized string by key.
        /// Falls back to English if not found in current language, then to the key itself.
        /// </summary>
        public static string Get(string key)
        {
            if (!_initialized)
                Initialize();

            // Check if game language changed since last load
            CheckLanguageChange();

            if (Net35Extensions.IsNullOrWhiteSpace(key))
                return "";

            // Try current language first
            string value;
            if (_strings.TryGetValue(key, out value))
                return value;

            // Try English fallback
            if (_fallbackStrings.TryGetValue(key, out value))
                return value;

            // Log missing key and return key as fallback
            AccessibilityMod.Core.AccessibilityMod.Logger?.Warning(
                $"Missing localization key: {key}"
            );
            return KeyToReadable(key);
        }

        /// <summary>
        /// Get a localized string with format arguments.
        /// Example: Get("navigation.point_x_of_y", 1, 5) returns "Point 1 of 5"
        /// </summary>
        public static string Get(string key, params object[] args)
        {
            string template = Get(key);
            if (args == null || args.Length == 0)
                return template;

            try
            {
                return string.Format(template, args);
            }
            catch (FormatException)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Warning(
                    $"Format error for key '{key}' with {args.Length} args"
                );
                return template;
            }
        }

        /// <summary>
        /// Get a localized string with proper singular/plural form based on count.
        /// Uses CLDR convention: "{key}.one" for singular (count == 1), "{key}.other" for plural.
        /// Falls back to base key if plural variants aren't found.
        /// Example: GetPlural("vase.pieces_remaining", 1) returns "1 piece remaining"
        ///          GetPlural("vase.pieces_remaining", 3) returns "3 pieces remaining"
        /// </summary>
        public static string GetPlural(string key, int count, params object[] extraArgs)
        {
            if (!_initialized)
                Initialize();

            CheckLanguageChange();

            if (Net35Extensions.IsNullOrWhiteSpace(key))
                return "";

            // Determine which plural form to use (English: one vs other)
            string pluralKey = count == 1 ? key + ".one" : key + ".other";

            // Try to get the plural-specific string
            string template;
            if (
                _strings.TryGetValue(pluralKey, out template)
                || _fallbackStrings.TryGetValue(pluralKey, out template)
            )
            {
                // Found plural variant
            }
            else
            {
                // Fall back to base key for backward compatibility
                template = Get(key);
            }

            // Build args array with count as first argument
            object[] args;
            if (extraArgs == null || extraArgs.Length == 0)
            {
                args = new object[] { count };
            }
            else
            {
                args = new object[extraArgs.Length + 1];
                args[0] = count;
                Array.Copy(extraArgs, 0, args, 1, extraArgs.Length);
            }

            try
            {
                return string.Format(template, args);
            }
            catch (FormatException)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Warning(
                    $"Format error for plural key '{pluralKey}' with count {count}"
                );
                return template;
            }
        }

        private static void DetectLanguage()
        {
            try
            {
                if (GSStatic.global_work_ != null)
                {
                    _currentLanguage = GSStatic.global_work_.language;
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Warning(
                    $"Error detecting language: {ex.Message}. Defaulting to English."
                );
                _currentLanguage = Language.USA;
            }
        }

        private static void CheckLanguageChange()
        {
            try
            {
                if (GSStatic.global_work_ != null)
                {
                    Language gameLanguage = GSStatic.global_work_.language;
                    if (gameLanguage != _loadedLanguage)
                    {
                        AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                            $"Language changed from {_loadedLanguage} to {gameLanguage}, reloading localization"
                        );
                        _currentLanguage = gameLanguage;
                        LoadStrings();

                        // Reload character names and evidence details for new language
                        CharacterNameService.ReloadFromFiles();
                        EvidenceDetailService.ReloadFromFiles();
                    }
                }
            }
            catch { }
        }

        private static void LoadStrings()
        {
            _strings.Clear();
            _fallbackStrings.Clear();

            // Always load English as fallback first
            string englishFolder = GetEnglishFolder();
            LoadStringsFromFolder(englishFolder, _fallbackStrings);

            // Load current language (if not English)
            if (_currentLanguage != Language.USA)
            {
                string langFolder = GetLanguageFolder();
                LoadStringsFromFolder(langFolder, _strings);
            }
            else
            {
                // If English, copy fallback to main strings
                foreach (var kvp in _fallbackStrings)
                {
                    _strings[kvp.Key] = kvp.Value;
                }
            }

            // Track which language we loaded
            _loadedLanguage = _currentLanguage;

            AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                $"Loaded {_strings.Count} strings for {GetLanguageFolderName()}, {_fallbackStrings.Count} fallback strings"
            );
        }

        private static void LoadStringsFromFolder(string folder, Dictionary<string, string> target)
        {
            string stringsFile = Path.Combine(folder, "strings.json");

            if (!File.Exists(stringsFile))
            {
                // Create folder and sample file if English folder doesn't exist
                if (folder.EndsWith("en"))
                {
                    CreateEnglishStringsFile(folder);
                }
                return;
            }

            try
            {
                string json = File.ReadAllText(stringsFile);
                var parsed = SimpleJsonParser.ParseStringDictionary(json);
                foreach (var kvp in parsed)
                {
                    target[kvp.Key] = kvp.Value;
                }
                AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                    $"Loaded {parsed.Count} strings from {stringsFile}"
                );
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Warning(
                    $"Error loading strings from {stringsFile}: {ex.Message}"
                );
            }
        }

        private static void CreateEnglishStringsFile(string folder)
        {
            try
            {
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                string stringsFile = Path.Combine(folder, "strings.json");
                if (!File.Exists(stringsFile))
                {
                    // Create a minimal sample file - the full strings will be added during string extraction
                    string sample =
                        @"{
    ""_comment"": ""Phoenix Wright Accessibility Mod - English strings. Do not edit keys, only values."",
    ""system.config_reloaded"": ""Configuration reloaded""
}";
                    File.WriteAllText(stringsFile, sample);
                    AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                        $"Created sample strings file: {stringsFile}"
                    );
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Warning(
                    $"Error creating sample strings file: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Convert a key like "mode.investigation" to "mode investigation" as a last-resort fallback.
        /// </summary>
        private static string KeyToReadable(string key)
        {
            if (Net35Extensions.IsNullOrWhiteSpace(key))
                return "";
            return key.Replace(".", " ").Replace("_", " ");
        }
    }
}
