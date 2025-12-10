using System;
using System.Collections.Generic;

namespace AccessibilityMod.Services
{
    /// <summary>
    /// Service for mapping speaker sprite IDs to character names.
    /// Each game (GS1, GS2, GS3) has its own frame02 sprite sheet with different indices.
    /// </summary>
    public static class CharacterNameService
    {
        private static Dictionary<int, string> _nameCache = new Dictionary<int, string>();
        private static bool _initialized = false;
        private static TitleId _cachedTitle = TitleId.GS1;

        // GS1 sprite index to character name mapping
        // Verified from GalleryActionStudioCtrl.CHARACTER_TABLE
        private static readonly Dictionary<int, string> GS1_NAMES = new Dictionary<int, string>
        {
            // Main characters
            { 2, "Phoenix Wright" },
            { 3, "Police Officer" },
            { 4, "Maya Fey" },
            { 5, "Mia Fey" }, // Channeled by Maya
            { 7, "Mia Fey" },
            { 8, "Judge" },
            { 9, "Miles Edgeworth" },
            { 10, "Winston Payne" },
            { 12, "Marvin Grossberg" },
            // Episode 1-4 witnesses/characters
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
            { 34, "Parrot" },
            { 36, "Caretaker" },
            { 37, "Bailiff" },
            { 38, "Teacher" },
            { 39, "Miles Edgeworth" },
            // Rise from the Ashes (episode 5)
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
            { 5, "Miles Edgeworth" },
            { 6, "Mia Fey" },
            { 7, "Judge" },
            { 8, "Miles Edgeworth" },
            { 9, "Winston Payne" },
            { 10, "Dick Gumshoe" },
            { 11, "Phone" },
            { 12, "Dustin Prince" },
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

        // GS3 speaker IDs - must be filled in by playing the game
        // The Japanese code constants (scenario_GS3.cs) do NOT match the English version
        // Add entries as: { speakerId, "Character Name" }
        private static readonly Dictionary<int, string> GS3_NAMES = new Dictionary<int, string>
        {
            // Fill in as you encounter characters - the mod will log unknown IDs
        };

        public static void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;
            AccessibilityMod.Core.AccessibilityMod.Logger?.Msg("CharacterNameService initialized");
        }

        /// <summary>
        /// Get character name from sprite ID
        /// </summary>
        /// <param name="spriteId">The sprite ID from the name_plate call</param>
        /// <returns>Character name or empty string if unknown</returns>
        public static string GetName(int spriteId)
        {
            if (!_initialized)
                Initialize();

            if (spriteId <= 0)
                return "";

            // Check if game changed and clear cache if so
            try
            {
                if (GSStatic.global_work_ != null)
                {
                    TitleId currentGame = GSStatic.global_work_.title;
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
                TitleId currentGame = _cachedTitle;

                // Get name from game-specific dictionary
                Dictionary<int, string> nameDict = null;
                switch (currentGame)
                {
                    case TitleId.GS1:
                        nameDict = GS1_NAMES;
                        break;
                    case TitleId.GS2:
                        nameDict = GS2_NAMES;
                        break;
                    case TitleId.GS3:
                        nameDict = GS3_NAMES;
                        break;
                    default:
                        nameDict = GS1_NAMES;
                        break;
                }

                if (nameDict != null && nameDict.ContainsKey(spriteId))
                {
                    name = nameDict[spriteId];
                }
                else if (spriteId > 0)
                {
                    // Log unknown speaker IDs so they can be identified during gameplay
                    // For GS3, this is expected since the mapping must be built manually
                    if (currentGame == TitleId.GS3)
                    {
                        AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                            $"GS3 speaker ID {spriteId} - add to CharacterNameService.GS3_NAMES"
                        );
                        name = $"Speaker {spriteId}";
                    }
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

        private static string GetCacheKey(int spriteId)
        {
            try
            {
                if (GSStatic.global_work_ != null)
                {
                    return $"{GSStatic.global_work_.title}_{spriteId}";
                }
            }
            catch { }
            return spriteId.ToString();
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
