using System;
using System.Collections.Generic;
using System.IO;
using AccessibilityMod.Utilities;
using UnityAccessibilityLib;

namespace AccessibilityMod.Services
{
    /// <summary>
    /// Service for mapping speaker IDs to character names.
    /// Supports hot-reload from external JSON files in UserData folder.
    /// </summary>
    public static class CharacterNameService
    {
        private static Dictionary<int, string> _nameCache = new Dictionary<int, string>();
        private static bool _initialized = false;
        private static TitleId _cachedTitle = TitleId.GS1;

        // Override dictionaries loaded from external JSON files
        private static Dictionary<int, string> _gs1Overrides = new Dictionary<int, string>();
        private static Dictionary<int, string> _gs2Overrides = new Dictionary<int, string>();
        private static Dictionary<int, string> _gs3Overrides = new Dictionary<int, string>();

        private static string ConfigFolder
        {
            get
            {
                // Use localized folder path
                return LocalizationService.GetLanguageFolder();
            }
        }

        private static string EnglishConfigFolder
        {
            get { return LocalizationService.GetEnglishFolder(); }
        }

        public static void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;
            LoadOverridesFromFiles();
            AccessibilityMod.Core.AccessibilityMod.Logger?.Msg("CharacterNameService initialized");
        }

        /// <summary>
        /// Reload character name overrides from external JSON files.
        /// Call this to hot-reload changes without restarting the game.
        /// </summary>
        public static void ReloadFromFiles()
        {
            LoadOverridesFromFiles();
            ClearCache();
            AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                "CharacterNameService reloaded from files"
            );
        }

        private static void LoadOverridesFromFiles()
        {
            _gs1Overrides.Clear();
            _gs2Overrides.Clear();
            _gs3Overrides.Clear();

            try
            {
                string folder = ConfigFolder;
                string englishFolder = EnglishConfigFolder;

                // Create English folder structure if needed (as the base/fallback)
                if (!Directory.Exists(englishFolder))
                {
                    Directory.CreateDirectory(englishFolder);
                }

                // Create sample files in English folder if they don't exist
                CreateSampleConfigFilesIfMissing(englishFolder);

                // Load override files with fallback to English
                LoadOverrideFileWithFallback(
                    "GS1_Names.json",
                    folder,
                    englishFolder,
                    _gs1Overrides
                );
                LoadOverrideFileWithFallback(
                    "GS2_Names.json",
                    folder,
                    englishFolder,
                    _gs2Overrides
                );
                LoadOverrideFileWithFallback(
                    "GS3_Names.json",
                    folder,
                    englishFolder,
                    _gs3Overrides
                );

                int totalOverrides =
                    _gs1Overrides.Count + _gs2Overrides.Count + _gs3Overrides.Count;
                if (totalOverrides > 0)
                {
                    AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                        $"Loaded {totalOverrides} character name overrides from config files"
                    );
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Warning(
                    $"Error loading character name overrides: {ex.Message}"
                );
            }
        }

        private static void LoadOverrideFileWithFallback(
            string fileName,
            string primaryFolder,
            string fallbackFolder,
            Dictionary<int, string> target
        )
        {
            // Try primary (current language) folder first
            string primaryPath = Path.Combine(primaryFolder, fileName);
            if (File.Exists(primaryPath))
            {
                LoadOverrideFile(primaryPath, target);
                return;
            }

            // Fall back to English folder
            string fallbackPath = Path.Combine(fallbackFolder, fileName);
            if (File.Exists(fallbackPath))
            {
                LoadOverrideFile(fallbackPath, target);
            }
        }

        private static void LoadOverrideFile(string filePath, Dictionary<int, string> target)
        {
            if (!File.Exists(filePath))
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                    $"Config file not found: {filePath}"
                );
                return;
            }

            try
            {
                string json = File.ReadAllText(filePath);
                AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                    $"Loading {Path.GetFileName(filePath)}, {json.Length} bytes"
                );
                var parsed = SimpleJsonParser.ParseIntStringDictionary(json);
                AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                    $"Parsed {parsed.Count} entries from {Path.GetFileName(filePath)}"
                );
                foreach (var kvp in parsed)
                {
                    target[kvp.Key] = kvp.Value;
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Warning(
                    $"Error parsing {Path.GetFileName(filePath)}: {ex.Message}"
                );
            }
        }

        private static void CreateSampleConfigFilesIfMissing(string folder)
        {
            try
            {
                string sampleContent =
                    @"{
    ""_comment"": ""Add character name overrides here. Keys are sprite IDs, values are names."",
    ""_example"": ""To add a mapping: remove the underscore prefix and set the ID and name"",
    ""_5"": ""Example Character Name""
}";
                string[] files = { "GS1_Names.json", "GS2_Names.json", "GS3_Names.json" };
                foreach (string file in files)
                {
                    string path = Path.Combine(folder, file);
                    if (!File.Exists(path))
                    {
                        File.WriteAllText(path, sampleContent);
                        AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                            $"Created sample config: {file}"
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Warning(
                    $"Error creating sample config files: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Get character name from sprite ID
        /// </summary>
        /// <param name="spriteId">The sprite ID (for GS3, this is the actual sprite index after remapping)</param>
        /// <returns>Character name or empty string if unknown</returns>
        public static string GetName(int spriteId)
        {
            if (!_initialized)
                Initialize();

            if (spriteId <= 0)
                return "";

            TitleId currentGame = TitleId.GS1;

            // Get current game and clear cache if changed
            try
            {
                if (GSStatic.global_work_ != null)
                {
                    currentGame = GSStatic.global_work_.title;
                    if (currentGame != _cachedTitle)
                    {
                        _nameCache.Clear();
                        _cachedTitle = currentGame;
                    }
                }
            }
            catch { }

            // Check cache first
            if (_nameCache.ContainsKey(spriteId))
            {
                var cached = _nameCache[spriteId];
                if (cached != null)
                    return cached;
            }

            try
            {
                string name = "";

                // Get dictionary for current game
                Dictionary<int, string> nameDict = null;
                switch (currentGame)
                {
                    case TitleId.GS1:
                        nameDict = _gs1Overrides;
                        break;
                    case TitleId.GS2:
                        nameDict = _gs2Overrides;
                        break;
                    case TitleId.GS3:
                        nameDict = _gs3Overrides;
                        break;
                    default:
                        nameDict = _gs1Overrides;
                        break;
                }

                // Look up name from JSON files
                if (nameDict != null && nameDict.ContainsKey(spriteId))
                {
                    name = nameDict[spriteId];
                }
                else if (spriteId > 2)
                {
                    // Log unknown speaker IDs
                    AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                        $"{currentGame} sprite ID {spriteId} - add \"{spriteId}\": \"NAME\" to {currentGame}_Names.json"
                    );
                }

                // Cache the result
                _nameCache[spriteId] = name;
                return name;
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Warning(
                    $"Error getting character name for ID {spriteId}: {ex.Message}"
                );
            }

            return "";
        }

        /// <summary>
        /// Clear the name cache (useful when game changes)
        /// </summary>
        public static void ClearCache()
        {
            _nameCache.Clear();
        }
    }
}
