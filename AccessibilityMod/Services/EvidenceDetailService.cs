using System;
using System.Collections.Generic;
using System.IO;
using UnityAccessibilityLib;

namespace AccessibilityMod.Services
{
    /// <summary>
    /// Service for providing accessibility descriptions for evidence detail views.
    /// The game displays evidence details as images with no text, so this service
    /// provides hand-written descriptions keyed by game and detail_id.
    /// Supports hot-reload from external text files in UserData folder.
    /// </summary>
    public static class EvidenceDetailService
    {
        private static bool _initialized = false;

        // Page separator in text files
        private const string PAGE_SEPARATOR = "===";

        // Override dictionaries loaded from external text files
        private static Dictionary<int, DetailDescription> _gs1Overrides =
            new Dictionary<int, DetailDescription>();
        private static Dictionary<int, DetailDescription> _gs2Overrides =
            new Dictionary<int, DetailDescription>();
        private static Dictionary<int, DetailDescription> _gs3Overrides =
            new Dictionary<int, DetailDescription>();

        private static string EvidenceDetailsFolder
        {
            get
            {
                // Use localized folder path
                return Path.Combine(LocalizationService.GetLanguageFolder(), "EvidenceDetails");
            }
        }

        private static string EnglishEvidenceDetailsFolder
        {
            get { return Path.Combine(LocalizationService.GetEnglishFolder(), "EvidenceDetails"); }
        }

        /// <summary>
        /// Represents accessibility descriptions for an evidence detail view.
        /// </summary>
        public class DetailDescription
        {
            public string[] Pages { get; private set; }

            public DetailDescription(params string[] pages)
            {
                Pages = pages ?? new string[0];
            }

            public string GetPage(int pageIndex)
            {
                if (Pages == null || pageIndex < 0 || pageIndex >= Pages.Length)
                    return null;
                return Pages[pageIndex];
            }

            public int PageCount
            {
                get { return Pages != null ? Pages.Length : 0; }
            }
        }

        public static void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;
            LoadOverridesFromFiles();
            AccessibilityMod.Core.AccessibilityMod.Logger?.Msg("EvidenceDetailService initialized");
        }

        /// <summary>
        /// Reload evidence detail overrides from external text files.
        /// Call this to hot-reload changes without restarting the game.
        /// </summary>
        public static void ReloadFromFiles()
        {
            LoadOverridesFromFiles();
            AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                "EvidenceDetailService reloaded from files"
            );
        }

        private static void LoadOverridesFromFiles()
        {
            _gs1Overrides.Clear();
            _gs2Overrides.Clear();
            _gs3Overrides.Clear();

            try
            {
                string baseFolder = EvidenceDetailsFolder;
                string englishFolder = EnglishEvidenceDetailsFolder;

                // Create English folder structure if needed (as the base/fallback)
                EnsureFolderStructure(englishFolder);

                // Load override files from each game folder with fallback to English
                LoadGameFolderWithFallback("GS1", baseFolder, englishFolder, _gs1Overrides);
                LoadGameFolderWithFallback("GS2", baseFolder, englishFolder, _gs2Overrides);
                LoadGameFolderWithFallback("GS3", baseFolder, englishFolder, _gs3Overrides);

                int totalOverrides =
                    _gs1Overrides.Count + _gs2Overrides.Count + _gs3Overrides.Count;
                if (totalOverrides > 0)
                {
                    AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                        $"Loaded {totalOverrides} evidence detail overrides from text files"
                    );
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Warning(
                    $"Error loading evidence detail overrides: {ex.Message}"
                );
            }
        }

        private static void LoadGameFolderWithFallback(
            string gameFolderName,
            string primaryBase,
            string fallbackBase,
            Dictionary<int, DetailDescription> target
        )
        {
            // Try primary (current language) folder first
            string primaryPath = Path.Combine(primaryBase, gameFolderName);
            if (Directory.Exists(primaryPath) && HasTextFiles(primaryPath))
            {
                LoadGameFolder(primaryPath, target);
                return;
            }

            // Fall back to English folder
            string fallbackPath = Path.Combine(fallbackBase, gameFolderName);
            if (Directory.Exists(fallbackPath))
            {
                LoadGameFolder(fallbackPath, target);
            }
        }

        private static bool HasTextFiles(string folderPath)
        {
            try
            {
                string[] files = Directory.GetFiles(folderPath, "*.txt");
                // Check if any non-underscore files exist
                foreach (string file in files)
                {
                    string fileName = Path.GetFileNameWithoutExtension(file);
                    if (!fileName.StartsWith("_"))
                        return true;
                }
            }
            catch { }
            return false;
        }

        private static void EnsureFolderStructure(string baseFolder)
        {
            try
            {
                string[] gameFolders = { "GS1", "GS2", "GS3" };
                foreach (string game in gameFolders)
                {
                    string gameFolder = Path.Combine(baseFolder, game);
                    if (!Directory.Exists(gameFolder))
                    {
                        Directory.CreateDirectory(gameFolder);
                        AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                            $"Created folder: {gameFolder}"
                        );
                    }
                }

                // Create a sample/readme file in GS1 folder
                string samplePath = Path.Combine(Path.Combine(baseFolder, "GS1"), "_README.txt");
                if (!File.Exists(samplePath))
                {
                    string sampleContent =
                        @"Evidence Detail Override Files
==============================

Place text files in this folder to override evidence detail descriptions.
Each file should be named with the detail ID (e.g., 9.txt for detail ID 9).

File format:
- Plain text content for each page
- Separate multiple pages with === on its own line

Example (save as 9.txt):
---
Case Summary:
12/28, 2001
Elevator, District Court.
Air in elevator was oxygen depleted at time of incident.
===
Victim Data:
Gregory Edgeworth (Age 35)
Defense attorney.
===
Suspect Data:
Yanni Yogi (Age 37)
Court bailiff.
---

Press F5 in-game to reload after making changes.
";
                    File.WriteAllText(samplePath, sampleContent);
                    AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                        "Created sample README in EvidenceDetails/GS1"
                    );
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Warning(
                    $"Error creating folder structure: {ex.Message}"
                );
            }
        }

        private static void LoadGameFolder(
            string folderPath,
            Dictionary<int, DetailDescription> target
        )
        {
            if (!Directory.Exists(folderPath))
                return;

            try
            {
                string[] files = Directory.GetFiles(folderPath, "*.txt");
                foreach (string filePath in files)
                {
                    string fileName = Path.GetFileNameWithoutExtension(filePath);

                    // Skip files starting with underscore (like _README.txt)
                    if (fileName.StartsWith("_"))
                        continue;

                    int detailId;
                    if (!int.TryParse(fileName, out detailId))
                    {
                        AccessibilityMod.Core.AccessibilityMod.Logger?.Warning(
                            $"Skipping {Path.GetFileName(filePath)} - filename must be a number"
                        );
                        continue;
                    }

                    string content = File.ReadAllText(filePath);
                    string[] pages = SplitIntoPages(content);

                    if (pages.Length > 0)
                    {
                        target[detailId] = new DetailDescription(pages);
                    }
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Warning(
                    $"Error loading folder {folderPath}: {ex.Message}"
                );
            }
        }

        private static string[] SplitIntoPages(string content)
        {
            if (string.IsNullOrEmpty(content))
                return new string[0];

            // Split by the page separator
            var pages = new List<string>();
            string[] parts = content.Split(
                new string[] { PAGE_SEPARATOR },
                StringSplitOptions.None
            );

            foreach (string part in parts)
            {
                string trimmed = part.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    pages.Add(trimmed);
                }
            }

            return pages.ToArray();
        }

        private static Dictionary<int, DetailDescription> GetDictionary()
        {
            if (!_initialized)
                Initialize();

            TitleId currentGame = TitleId.GS1;
            try
            {
                if (GSStatic.global_work_ != null)
                {
                    currentGame = GSStatic.global_work_.title;
                }
            }
            catch { }

            switch (currentGame)
            {
                case TitleId.GS1:
                    return _gs1Overrides;
                case TitleId.GS2:
                    return _gs2Overrides;
                case TitleId.GS3:
                    return _gs3Overrides;
                default:
                    return _gs1Overrides;
            }
        }

        /// <summary>
        /// Get the description for an evidence detail view.
        /// </summary>
        /// <param name="detailId">The detail_id from piceData (index into status_ext_bg_tbl)</param>
        /// <param name="pageIndex">Zero-based page index</param>
        /// <returns>Description text, or null if not available</returns>
        public static string GetDescription(int detailId, int pageIndex = 0)
        {
            try
            {
                var dict = GetDictionary();
                if (dict != null && dict.ContainsKey(detailId))
                {
                    return dict[detailId].GetPage(pageIndex);
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Warning(
                    $"Error getting evidence detail description: {ex.Message}"
                );
            }

            return null;
        }

        /// <summary>
        /// Check if a description exists for the given detail.
        /// </summary>
        public static bool HasDescription(int detailId)
        {
            try
            {
                var dict = GetDictionary();
                return dict != null && dict.ContainsKey(detailId);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get the number of pages with descriptions for a detail.
        /// </summary>
        public static int GetDescriptionPageCount(int detailId)
        {
            try
            {
                var dict = GetDictionary();
                if (dict != null && dict.ContainsKey(detailId))
                {
                    return dict[detailId].PageCount;
                }
            }
            catch { }

            return 0;
        }
    }
}
