using System;
using System.Collections.Generic;
using System.IO;
using AccessibilityMod.Utilities;

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

        // GS1 sprite index to character name mapping
        // Verified from GalleryActionStudioCtrl.CHARACTER_TABLE
        private static readonly Dictionary<int, string> GS1_NAMES = new Dictionary<int, string>
        {
            { 2, "Phoenix Wright" },
            { 3, "Police Officer" },
            { 4, "Maya Fey" },
            { 5, "Mia Fey" }, // Channeled by Maya
            { 7, "Mia Fey" },
            { 8, "Judge" },
            { 9, "Miles Edgeworth" },
            { 10, "Winston Payne" },
            { 11, "Interphone" },
            { 12, "Marvin Grossberg" },
            { 13, "Phone" },
            { 16, "Penny Nichols" },
            { 17, "Wendy Oldbag" },
            { 18, "Sal Manella" },
            { 20, "Dick Gumshoe" },
            { 21, "Redd White" },
            { 22, "April May" },
            { 23, "Bellboy" },
            { 24, "Dee Vasquez" },
            { 25, "Larry Butz" },
            { 26, "Frank Sahwit" },
            { 27, "Will Powers" },
            { 28, "Cody Hackins" },
            { 31, "Lotta Hart" },
            { 32, "Yanni Yogi" },
            { 33, "Manfred von Karma" },
            { 34, "Polly" },
            { 36, "Caretaker" },
            { 37, "Bailiff" },
            { 38, "Teacher" },
            { 39, "Miles Edgeworth" },
            { 43, "Chief of Detectives" },
            { 44, "Ema Skye" },
            { 45, "Lana Skye" },
            { 46, "Jake Marshall" },
            { 47, "Mike Meekins" },
            { 48, "Bruce Goodman" },
            { 49, "Damon Gant" },
            { 50, "Angel Starr" },
            { 52, "Police Officer" },
            { 53, "Police Patrolman" },
        };

        // GS2 sprite index to character name mapping
        private static readonly Dictionary<int, string> GS2_NAMES = new Dictionary<int, string>
        {
            { 3, "Phoenix Wright" },
            { 4, "Maya Fey" },
            { 6, "Mia Fey" },
            { 7, "Judge" },
            { 8, "Miles Edgeworth" },
            { 9, "Winston Payne" },
            { 10, "Dick Gumshoe" },
            { 11, "Phone" },
            { 13, "Bailiff" },
            { 14, "Franziska von Karma" },
            { 15, "Franziska von Karma" },
            { 16, "Richard Wellington" },
            { 17, "Maggey Byrde" },
            { 19, "Ini Miney" },
            { 20, "Pearl Fey" },
            { 21, "Morgan Fey" },
            { 22, "Director Hotti" },
            { 23, "Turner Grey" },
            { 24, "Lotta Hart" },
            { 26, "Nurse" },
            { 27, "Mimi Miney" },
            { 28, "Regina Berry" },
            { 29, "Max" },
            { 30, "Ben" },
            { 31, "Moe" },
            { 32, "Acro" },
            { 33, "Trilo" },
            { 34, "Money the Monkey" },
            { 35, "Matt Engarde" },
            { 36, "Adrian Andrews" },
            { 37, "Shelly de Killer" },
            { 39, "Wendy Oldbag" },
            { 40, "Will Powers" },
            { 46, "Russell Berry" },
            { 47, "Bellboy" },
            { 48, "PA Notice" },
            { 50, "Chief" },
            { 52, "Guard" },
            { 53, "Shoe" },
            { 54, "John Doe" },
        };

        // GS3 raw speaker IDs from message_work.speaker_id
        // These are the unique identifiers for each character, NOT the sprite indices
        private static readonly Dictionary<int, string> GS3_NAMES = new Dictionary<int, string>
        {
            { 3, "Phoenix Wright" },
            { 4, "Maya Fey" },
            { 5, "Judge" },
            { 6, "Dick Gumshoe" },
            { 7, "Mia Fey" },
            { 9, "Winston Payne" },
            { 11, "Dahlia Hawthorne" },
            { 12, "Ron DeLite" },
            { 13, "Luke Atmey" },
            { 14, "Desirée DeLite" },
            { 15, "Larry Butz" },
            { 16, "Adrian Andrews" },
            { 17, "Godot" },
            { 18, "Maggey Byrde" },
            { 19, "Jean Armstrong" },
            { 20, "Furio Tigre" },
            { 21, "Viola" },
            { 23, "Victor Kudo" },
            { 24, "Lisa Basil" },
            { 27, "Terry Fawles" },
            { 30, "Pearl Fey" },
            { 42, "Marvin Grossberg" },
            { 47, "Phone" },
            { 48, "Bailiff" },
            { 49, "Canadian Judge" },
            { 50, "Doug Swallow" },
            { 51, "Announcer" },
            { 52, "Mask☆DeMasque" },
            { 54, "Buzzer" },
            { 58, "Old Man" },
            { 59, "Chief" },
            { 60, "Detective" },
            { 61, "The Tiger" },
            { 62, "Programmer" },
        };

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
                    AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                        $"  Loaded override: {kvp.Key} = {kvp.Value}"
                    );
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

                // Get override dictionary and default dictionary for current game
                Dictionary<int, string> overrideDict = null;
                Dictionary<int, string> nameDict = null;
                switch (currentGame)
                {
                    case TitleId.GS1:
                        overrideDict = _gs1Overrides;
                        nameDict = GS1_NAMES;
                        break;
                    case TitleId.GS2:
                        overrideDict = _gs2Overrides;
                        nameDict = GS2_NAMES;
                        break;
                    case TitleId.GS3:
                        overrideDict = _gs3Overrides;
                        nameDict = GS3_NAMES;
                        break;
                    default:
                        overrideDict = _gs1Overrides;
                        nameDict = GS1_NAMES;
                        break;
                }

                // Check overrides first (from external JSON files)
                if (overrideDict != null && overrideDict.ContainsKey(spriteId))
                {
                    name = overrideDict[spriteId];
                }
                // Fall back to hardcoded defaults
                else if (nameDict != null && nameDict.ContainsKey(spriteId))
                {
                    name = nameDict[spriteId];
                }
                else if (spriteId > 2)
                {
                    // Log unknown speaker IDs
                    AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                        $"{currentGame} sprite ID {spriteId} - add {{ {spriteId}, \"NAME\" }} to {currentGame}_NAMES"
                    );
                    name = $"Speaker {spriteId}";
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
