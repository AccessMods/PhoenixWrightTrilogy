using System;
using AccessibilityMod.Core;
using AccessibilityMod.Patches;

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
                    SpeechManager.Announce("Music player", TextType.Menu);
                    return;
                }

                var state = GalleryOrchestraPatches.GetCurrentState(instance);

                // Build announcement
                string announcement = "Music player";

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
                    announcement += $". Track {state.CurrentSongIndex + 1}: {state.SongTitle}";
                }

                // Play state
                announcement += state.IsPlaying ? ". Playing" : ". Stopped";

                // Play mode
                if (!Net35Extensions.IsNullOrWhiteSpace(state.PlayModeName))
                {
                    announcement += $". Mode: {state.PlayModeName}";
                }

                SpeechManager.Announce(announcement, TextType.Menu);
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error announcing orchestra state: {ex.Message}"
                );
                SpeechManager.Announce("Music player", TextType.Menu);
            }
        }

        /// <summary>
        /// Announces the full list of controls for the music player.
        /// </summary>
        public static void AnnounceHelp()
        {
            string help =
                "Music player controls: "
                + "Up and Down select tracks. "
                + "Left and Right jump by four tracks. "
                + "Z and X change albums. "
                + "J and N cycle play modes. "
                + "Tab skips to next track, Q to previous. "
                + "Enter plays or stops. "
                + "I announces current state. "
                + "Backspace exits.";

            SpeechManager.Announce(help, TextType.Menu);
        }
    }
}
