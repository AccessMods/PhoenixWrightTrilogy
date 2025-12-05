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
            { 0, "Phoenix Wright" },
            { 1, "Mia Fey" },
            { 2, "Maya Fey" },
            { 3, "Pearl Fey" },
            { 4, "Dick Gumshoe" },
            { 5, "Miles Edgeworth" },
            { 6, "Larry Butz" },
            { 7, "Franziska von Karma" },
            { 8, "Judge" },
            { 9, "Winston Payne" },
            { 10, "Maggey Byrde" },
            { 11, "Richard Wellington" },
            { 12, "Dustin Prince" },
            { 13, "Morgan Fey" },
            { 14, "Turner Grey" },
            { 15, "Ini Miney" },
            { 16, "Mimi Miney" },
            { 17, "Lotta Hart" },
            { 18, "Director Hotti" },
            { 19, "Maximillion Galactica" },
            { 20, "Regina Berry" },
            { 21, "Ben/Trilo" },
            { 22, "Moe" },
            { 23, "Acro" },
            { 24, "Russell Berry" },
            { 25, "Matt Engarde" },
            { 26, "Juan Corrida" },
            { 27, "Adrian Andrews" },
            { 28, "Celeste Inpax" },
            { 29, "Shelly de Killer" },
            { 30, "Will Powers" },
            { 31, "Wendy Oldbag" },
        };

        // GS3 uses a remapping table - these are the OUTPUT sprite indices
        // The input speaker IDs are remapped via name_id_tbl first
        private static readonly Dictionary<int, string> GS3_NAMES = new Dictionary<int, string>
        {
            { 0, "Phoenix Wright" },
            { 1, "Mia Fey" },
            { 2, "Maya Fey" },
            { 3, "Miles Edgeworth" },
            { 4, "Dick Gumshoe" },
            { 5, "Larry Butz" },
            { 6, "Phoenix Wright" }, // Young Phoenix
            { 7, "Pearl Fey" },
            { 8, "Franziska von Karma" },
            { 9, "Judge" },
            { 10, "Winston Payne" },
            { 11, "Lotta Hart" },
            { 12, "Will Powers" },
            { 13, "Wendy Oldbag" },
            { 14, "Cody Hackins" },
            { 15, "Dahlia Hawthorne" },
            { 16, "Viola Cadaverini" },
            { 17, "Furio Tigre" },
            { 18, "Jean Armstrong" },
            { 19, "Victor Kudo" },
            { 20, "Maya Fey" },
            { 21, "Marvin Grossberg" },
            { 22, "Terry Fawles" },
            { 23, "Valerie Hawthorne" },
            { 24, "Doug Swallow" },
            { 25, "Gregory Edgeworth" },
            { 26, "Polly" },
            { 27, "Glen Elg" },
            { 28, "Lisa Basil" },
            { 29, "Mask*DeMasque" },
            { 30, "Luke Atmey" },
            { 31, "Ron DeLite" },
            { 32, "Desirée DeLite" },
            { 33, "Kane Bullard" },
            { 34, "Adrian Andrews" },
            { 35, "Bikini" },
            { 36, "Iris" },
            { 37, "Elise Deauxnim" },
            { 38, "Larry Butz" },
            { 39, "Morgan Fey" },
            { 40, "Godot" },
            { 41, "Dahlia Hawthorne" },
            { 42, "Sister Iris" },
            { 43, "Misty Fey" },
            { 44, "Young Miles Edgeworth" },
            { 45, "Young Larry" },
            { 46, "Judge's Brother" },
            { 47, "Maggey Byrde" },
            { 48, "Young Mia" },
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
