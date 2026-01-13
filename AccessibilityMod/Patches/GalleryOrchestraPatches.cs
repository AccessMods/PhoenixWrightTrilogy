using System;
using System.Collections.Generic;
using System.Reflection;
using AccessibilityMod.Core;
using AccessibilityMod.Services;
using HarmonyLib;
using UnityAccessibilityLib;

namespace AccessibilityMod.Patches
{
    [HarmonyPatch]
    public static class GalleryOrchestraPatches
    {
        // Track state to avoid duplicate announcements
        private static int _lastAnnouncedSongIndex = -1;
        private static int _lastAnnouncedTitle = -1;
        private static int _lastAnnouncedPlayMode = -1;
        private static bool _isOrchestraActive = false;

        // Reflection cache
        private static FieldInfo _currentSelectTitleField;
        private static FieldInfo _currentSelectIndexField;
        private static FieldInfo _playModeField;
        private static FieldInfo _albumTableField;
        private static FieldInfo _playingMusicDataField;
        private static PropertyInfo _isPlayingMusicProperty;
        private static FieldInfo _isExecutingField;

        // Cached type for nested AlbumTableData
        private static Type _albumTableDataType;
        private static FieldInfo _albumDataNameTextField;
        private static FieldInfo _albumDataIdentifierField;

        /// <summary>
        /// Returns whether the Orchestra music player is currently active.
        /// Checks actual game state, not just our tracking flag.
        /// </summary>
        public static bool IsOrchestraActive
        {
            get
            {
                try
                {
                    // First check our flag - if false, definitely not active
                    if (!_isOrchestraActive)
                        return false;

                    // Verify with actual game state - check if instance exists
                    var instance = UnityEngine.Object.FindObjectOfType<GalleryOrchestraCtrl>();
                    if (instance == null)
                    {
                        _isOrchestraActive = false;
                        return false;
                    }

                    // Check if the orchestra was initialized but is no longer executing.
                    // This detects normal close (B button) which sets m_IsExecuting = false
                    // but doesn't trigger OnForceClose. During opening animation, both
                    // IsInit and m_IsExecuting are false, so we only clear when IsInit is true.
                    if (instance.IsInit)
                    {
                        EnsureReflectionCache();
                        if (_isExecutingField != null)
                        {
                            var isExecuting = (bool)_isExecutingField.GetValue(instance);
                            if (!isExecuting)
                            {
                                _isOrchestraActive = false;
                                return false;
                            }
                        }
                    }

                    return true;
                }
                catch
                {
                    return _isOrchestraActive;
                }
            }
        }

        #region Initialization

        private static void EnsureReflectionCache()
        {
            if (_currentSelectTitleField != null)
                return;

            var orchestraType = typeof(GalleryOrchestraCtrl);
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var staticFlags = BindingFlags.NonPublic | BindingFlags.Static;

            _currentSelectTitleField = orchestraType.GetField("m_CurrentSelectTitle", flags);
            _currentSelectIndexField = orchestraType.GetField("m_CurrentSelectIndex", flags);
            _playModeField = orchestraType.GetField("m_PlayMode", flags);
            _albumTableField = orchestraType.GetField("ALBUM_TABLE", staticFlags);
            _playingMusicDataField = orchestraType.GetField("m_PlayingMusicData", flags);
            _isPlayingMusicProperty = orchestraType.GetProperty(
                "IsPlayingMusic",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            _isExecutingField = orchestraType.GetField("m_IsExecuting", flags);

            // Get nested AlbumTableData type
            _albumTableDataType = orchestraType.GetNestedType(
                "AlbumTableData",
                BindingFlags.NonPublic
            );
            if (_albumTableDataType != null)
            {
                _albumDataNameTextField =
                    _albumTableDataType.GetProperty("NameText")?.GetGetMethod(true)?.ReturnType
                    != null
                        ? null
                        : null; // We'll use GetProperty directly
                _albumDataIdentifierField =
                    _albumTableDataType.GetProperty("Identifer")?.GetGetMethod(true)?.ReturnType
                    != null
                        ? null
                        : null;
            }
        }

        #endregion

        #region Song Selection Patch

        /// <summary>
        /// Separate patch class for SelectSong since CursorSelectMode is a private enum.
        /// Uses HarmonyTargetMethod to resolve the method via reflection.
        /// </summary>
        [HarmonyPatch]
        public static class SelectSongPatch
        {
            [HarmonyTargetMethod]
            public static MethodBase TargetMethod()
            {
                var orchestraType = typeof(GalleryOrchestraCtrl);
                var cursorSelectModeType = orchestraType.GetNestedType(
                    "CursorSelectMode",
                    BindingFlags.NonPublic
                );

                if (cursorSelectModeType == null)
                    return null;

                return orchestraType.GetMethod(
                    "SelectSong",
                    BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    new Type[] { typeof(int), cursorSelectModeType, typeof(bool), typeof(bool) },
                    null
                );
            }

            [HarmonyPostfix]
            public static void Postfix(GalleryOrchestraCtrl __instance, int index, bool can_play_se)
            {
                try
                {
                    // Only announce if song index changed
                    if (index == _lastAnnouncedSongIndex)
                        return;

                    _lastAnnouncedSongIndex = index;

                    string songTitle = GetSongTitle(__instance, index);
                    if (!Net35Extensions.IsNullOrWhiteSpace(songTitle))
                    {
                        string announcement = L.Get("orchestra.track", index + 1, songTitle);
                        SpeechManager.Announce(announcement, GameTextType.Menu);
                    }
                }
                catch (Exception ex)
                {
                    AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                        $"Error in SelectSong patch: {ex.Message}"
                    );
                }
            }
        }

        #endregion

        #region Album/Page Change Patch

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GalleryOrchestraCtrl), "UpdatePage")]
        public static void UpdatePage_Postfix(
            GalleryOrchestraCtrl __instance,
            GalleryOrchestraCtrl.Title title
        )
        {
            try
            {
                int titleInt = (int)title;
                if (titleInt == _lastAnnouncedTitle)
                    return;

                _lastAnnouncedTitle = titleInt;

                // Reset song index tracking when album changes
                _lastAnnouncedSongIndex = -1;

                string albumName = GetAlbumName(title);
                if (!Net35Extensions.IsNullOrWhiteSpace(albumName))
                {
                    SpeechManager.Announce(albumName, GameTextType.Menu);
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in UpdatePage patch: {ex.Message}"
                );
            }
        }

        #endregion

        #region Play Mode Change Patch

        [HarmonyPostfix]
        [HarmonyPatch(
            typeof(GalleryOrchestraCtrl),
            "UpdatePlayMode",
            new Type[] { typeof(GalleryOrchestraCtrl.PlayMode) }
        )]
        public static void UpdatePlayMode_Postfix(
            GalleryOrchestraCtrl __instance,
            GalleryOrchestraCtrl.PlayMode mode
        )
        {
            try
            {
                int modeInt = (int)mode;
                if (modeInt == _lastAnnouncedPlayMode)
                    return;

                _lastAnnouncedPlayMode = modeInt;

                string modeName = GetPlayModeName(mode);
                if (!Net35Extensions.IsNullOrWhiteSpace(modeName))
                {
                    string announcement = L.Get("orchestra.play_mode", modeName);
                    SpeechManager.Announce(announcement, GameTextType.Menu);
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in UpdatePlayMode patch: {ex.Message}"
                );
            }
        }

        #endregion

        #region Playback State Patches

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GalleryOrchestraCtrl), "PlayMusic")]
        public static void PlayMusic_Postfix(
            GalleryOrchestraCtrl __instance,
            int playlist_index,
            bool manual_select
        )
        {
            try
            {
                // Get the song title from the playing music data
                EnsureReflectionCache();

                if (_playingMusicDataField == null)
                    return;

                var playingData = _playingMusicDataField.GetValue(__instance);
                if (playingData == null)
                    return;

                string songTitle = GetSongTitleFromAlbumData(playingData);
                if (!Net35Extensions.IsNullOrWhiteSpace(songTitle))
                {
                    string announcement = L.Get("orchestra.now_playing", songTitle);
                    SpeechManager.Announce(announcement, GameTextType.Menu);
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in PlayMusic patch: {ex.Message}"
                );
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GalleryOrchestraCtrl), "StopMusic")]
        public static void StopMusic_Postfix(bool manual_select)
        {
            try
            {
                // Only announce manual stops
                if (manual_select)
                {
                    SpeechManager.Announce(L.Get("orchestra.stopped"), GameTextType.Menu);
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in StopMusic patch: {ex.Message}"
                );
            }
        }

        #endregion

        #region Orchestra Lifecycle Patches

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GalleryOrchestraCtrl), "Play", new Type[0])]
        public static void Play_Postfix(GalleryOrchestraCtrl __instance)
        {
            try
            {
                _isOrchestraActive = true;
                ResetState();
                SpeechManager.Announce(L.Get("orchestra.opened"), GameTextType.Menu);
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in Orchestra Play patch: {ex.Message}"
                );
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GalleryOrchestraCtrl), "OnForceClose")]
        public static void OnForceClose_Postfix()
        {
            try
            {
                _isOrchestraActive = false;
                ResetState();
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in Orchestra OnForceClose patch: {ex.Message}"
                );
            }
        }

        private static void ResetState()
        {
            _lastAnnouncedSongIndex = -1;
            _lastAnnouncedTitle = -1;
            _lastAnnouncedPlayMode = -1;
        }

        #endregion

        #region Helper Methods

        private static string GetSongTitle(GalleryOrchestraCtrl instance, int index)
        {
            try
            {
                EnsureReflectionCache();

                if (_currentSelectTitleField == null || _albumTableField == null)
                    return null;

                // Get current title (album)
                var titleObj = _currentSelectTitleField.GetValue(instance);
                if (titleObj == null)
                    return null;

                var title = (GalleryOrchestraCtrl.Title)titleObj;

                // Get ALBUM_TABLE
                var albumTable = _albumTableField.GetValue(null) as System.Collections.IDictionary;
                if (albumTable == null || !albumTable.Contains(title))
                    return null;

                // Get the array of AlbumTableData for this title
                var albumDataArray = albumTable[title] as Array;
                if (albumDataArray == null || index < 0 || index >= albumDataArray.Length)
                    return null;

                var albumData = albumDataArray.GetValue(index);
                return GetSongTitleFromAlbumData(albumData);
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error getting song title: {ex.Message}"
                );
                return null;
            }
        }

        private static string GetSongTitleFromAlbumData(object albumData)
        {
            if (albumData == null)
                return null;

            try
            {
                // Get NameText property (it's a GalleryTextID enum)
                var nameTextProp = albumData.GetType().GetProperty("NameText");
                if (nameTextProp == null)
                    return null;

                var nameTextId = nameTextProp.GetValue(albumData, null);
                if (nameTextId == null)
                    return null;

                // Use TextDataCtrl.GetText to get the localized song name
                return TextDataCtrl.GetText((TextDataCtrl.GalleryTextID)nameTextId, 0);
            }
            catch
            {
                return null;
            }
        }

        private static string GetAlbumName(GalleryOrchestraCtrl.Title title)
        {
            try
            {
                // Album names are stored with MUSIC_ALBUM_TITLE text ID
                // Index is 0-based corresponding to Title enum value minus 1
                int albumIndex = (int)title - 1;
                if (albumIndex < 0)
                    albumIndex = 0;

                return TextDataCtrl.GetText(
                    TextDataCtrl.GalleryTextID.MUSIC_ALBUM_TITLE,
                    albumIndex
                );
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error getting album name: {ex.Message}"
                );

                // Fallback to hardcoded names
                switch (title)
                {
                    case GalleryOrchestraCtrl.Title.Series1_InGame:
                        return "Phoenix Wright: Ace Attorney";
                    case GalleryOrchestraCtrl.Title.Series2_InGame:
                        return "Justice for All";
                    case GalleryOrchestraCtrl.Title.Series3_InGame:
                        return "Trials and Tribulations";
                    case GalleryOrchestraCtrl.Title.Album_Jazz:
                        return "Jazz Album";
                    case GalleryOrchestraCtrl.Title.Album_Piano:
                        return "Piano Album";
                    default:
                        return $"Album {(int)title}";
                }
            }
        }

        private static string GetPlayModeName(GalleryOrchestraCtrl.PlayMode mode)
        {
            try
            {
                TextDataCtrl.GalleryTextID textId;

                switch (mode)
                {
                    case GalleryOrchestraCtrl.PlayMode.Normal:
                        textId = TextDataCtrl.GalleryTextID.MUSIC_ONESHOT;
                        break;
                    case GalleryOrchestraCtrl.PlayMode.Repeat:
                        textId = TextDataCtrl.GalleryTextID.MUSIC_REPEAT_ALWAYS;
                        break;
                    case GalleryOrchestraCtrl.PlayMode.AlbumRepeat:
                        textId = TextDataCtrl.GalleryTextID.MUSIC_ALBUM_REPEAT;
                        break;
                    case GalleryOrchestraCtrl.PlayMode.AlbumShuffle:
                        textId = TextDataCtrl.GalleryTextID.MUSIC_ALBUM_SHUFFLE;
                        break;
                    case GalleryOrchestraCtrl.PlayMode.AllRepeat:
                        textId = TextDataCtrl.GalleryTextID.MUSIC_ALL_REPEAT;
                        break;
                    case GalleryOrchestraCtrl.PlayMode.AllShuffle:
                        textId = TextDataCtrl.GalleryTextID.MUSIC_ALL_SHUFFLE;
                        break;
                    default:
                        return mode.ToString();
                }

                return TextDataCtrl.GetText(textId, 0);
            }
            catch
            {
                // Fallback to enum name
                return mode.ToString();
            }
        }

        #endregion

        #region State Query Methods (for Navigator)

        /// <summary>
        /// Gets the current state information for the Orchestra player.
        /// </summary>
        public static OrchestraState GetCurrentState(GalleryOrchestraCtrl instance)
        {
            var state = new OrchestraState();

            try
            {
                EnsureReflectionCache();

                if (instance == null)
                    return state;

                // Get current title
                if (_currentSelectTitleField != null)
                {
                    var titleObj = _currentSelectTitleField.GetValue(instance);
                    if (titleObj != null)
                    {
                        state.CurrentTitle = (GalleryOrchestraCtrl.Title)titleObj;
                        state.AlbumName = GetAlbumName(state.CurrentTitle);
                    }
                }

                // Get current song index
                if (_currentSelectIndexField != null)
                {
                    var indexObj = _currentSelectIndexField.GetValue(instance);
                    if (indexObj != null)
                    {
                        state.CurrentSongIndex = (int)indexObj;
                        state.SongTitle = GetSongTitle(instance, state.CurrentSongIndex);
                    }
                }

                // Get play mode
                if (_playModeField != null)
                {
                    var modeObj = _playModeField.GetValue(instance);
                    if (modeObj != null)
                    {
                        state.PlayMode = (GalleryOrchestraCtrl.PlayMode)modeObj;
                        state.PlayModeName = GetPlayModeName(state.PlayMode);
                    }
                }

                // Get playing state
                if (_isPlayingMusicProperty != null)
                {
                    var playingObj = _isPlayingMusicProperty.GetValue(instance, null);
                    if (playingObj != null)
                    {
                        state.IsPlaying = (bool)playingObj;
                    }
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error getting orchestra state: {ex.Message}"
                );
            }

            return state;
        }

        public class OrchestraState
        {
            public GalleryOrchestraCtrl.Title CurrentTitle { get; set; }
            public string AlbumName { get; set; }
            public int CurrentSongIndex { get; set; } = -1;
            public string SongTitle { get; set; }
            public GalleryOrchestraCtrl.PlayMode PlayMode { get; set; }
            public string PlayModeName { get; set; }
            public bool IsPlaying { get; set; }
        }

        #endregion
    }
}
