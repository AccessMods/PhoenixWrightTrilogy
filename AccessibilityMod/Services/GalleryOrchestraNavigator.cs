using System;
using AccessibilityMod.Core;
using AccessibilityMod.Patches;
using UnityAccessibilityLib;

namespace AccessibilityMod.Services
{
    /// <summary>
    /// Provides state query methods for the Gallery Orchestra (music player).
    /// Used by AccessibilityState for "I" key announcements.
    /// </summary>
    public static class GalleryOrchestraNavigator
    {
        /// <summary>
        /// Returns whether the Orchestra music player is currently active.
        /// </summary>
        public static bool IsOrchestraActive()
        {
            return GalleryOrchestraPatches.IsOrchestraActive;
        }

        /// <summary>
        /// Announces the current state of the Orchestra player.
        /// Called when user presses the "I" key while in Orchestra mode.
        /// </summary>
        public static void AnnounceState()
        {
            try
            {
                // Find the active Orchestra instance
                var instance = UnityEngine.Object.FindObjectOfType<GalleryOrchestraCtrl>();
                if (instance == null)
                {
                    SpeechManager.Announce(L.Get("orchestra.music_player"), GameTextType.Menu);
                    return;
                }

                var state = GalleryOrchestraPatches.GetCurrentState(instance);

                // Build announcement
                string announcement = L.Get("orchestra.music_player");

                // Album name
                if (!Net35Extensions.IsNullOrWhiteSpace(state.AlbumName))
                {
                    announcement += $": {state.AlbumName}";
                }

                // Current song
                if (
                    !Net35Extensions.IsNullOrWhiteSpace(state.SongTitle)
                    && state.CurrentSongIndex >= 0
                )
                {
                    announcement +=
                        ". "
                        + L.Get("orchestra.track", state.CurrentSongIndex + 1, state.SongTitle);
                }

                // Play state
                announcement += state.IsPlaying
                    ? ". " + L.Get("orchestra.playing")
                    : ". " + L.Get("orchestra.stopped");

                // Play mode
                if (!Net35Extensions.IsNullOrWhiteSpace(state.PlayModeName))
                {
                    announcement += ". " + L.Get("orchestra.mode", state.PlayModeName);
                }

                SpeechManager.Announce(announcement, GameTextType.Menu);
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error announcing orchestra state: {ex.Message}"
                );
                SpeechManager.Announce(L.Get("orchestra.music_player"), GameTextType.Menu);
            }
        }

        /// <summary>
        /// Announces the full list of controls for the music player.
        /// </summary>
        public static void AnnounceHelp()
        {
            SpeechManager.Announce(L.Get("orchestra.controls_help"), GameTextType.Menu);
        }
    }
}
