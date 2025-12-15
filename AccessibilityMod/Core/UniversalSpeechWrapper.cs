using System;
using System.Runtime.InteropServices;
using AccessibilityMod.Services;

namespace AccessibilityMod.Core
{
    /// <summary>
    /// Low-level P/Invoke wrapper for the UniversalSpeech library.
    /// Provides direct access to screen reader output with SAPI fallback.
    /// </summary>
    public static class UniversalSpeechWrapper
    {
        private const string DLL_NAME = "UniversalSpeech.dll";

        // P/Invoke declarations for UniversalSpeech
        [DllImport(
            DLL_NAME,
            CallingConvention = CallingConvention.Cdecl,
            CharSet = CharSet.Unicode
        )]
        private static extern int speechSay(
            [MarshalAs(UnmanagedType.LPWStr)] string str,
            int interrupt
        );

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int speechStop();

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern int speechSetValue(int what, int value);

        // Constants from UniversalSpeech.h
        private const int SP_ENABLE_NATIVE_SPEECH = 0xFFFF;

        private static bool _initialized = false;

        /// <summary>
        /// Initialize the speech system. Enables SAPI fallback if no screen reader is available.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized)
                return;

            try
            {
                // Enable native speech engines (SAPI) as fallback
                speechSetValue(SP_ENABLE_NATIVE_SPEECH, 1);
                _initialized = true;
                AccessibilityMod.Logger?.Msg("UniversalSpeech initialized");
                SpeechManager.Announce(L.Get("system.initialized"));
            }
            catch (DllNotFoundException ex)
            {
                AccessibilityMod.Logger?.Error($"UniversalSpeech.dll not found: {ex.Message}");
                AccessibilityMod.Logger?.Error(
                    "Ensure UniversalSpeech.dll is in the game directory"
                );
            }
            catch (Exception ex)
            {
                AccessibilityMod.Logger?.Error(
                    $"Failed to initialize UniversalSpeech: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Speak the given text directly.
        /// </summary>
        /// <param name="text">The text to speak</param>
        /// <param name="interrupt">Whether to interrupt current speech</param>
        public static void Speak(string text, bool interrupt = false)
        {
            if (Net35Extensions.IsNullOrWhiteSpace(text))
                return;

            try
            {
                speechSay(text, interrupt ? 1 : 0);
            }
            catch (DllNotFoundException)
            {
                // DLL not found - already logged in Initialize
            }
            catch (Exception ex)
            {
                AccessibilityMod.Logger?.Error($"Speech error: {ex.Message}");
            }
        }

        /// <summary>
        /// Stop any currently playing speech.
        /// </summary>
        public static void Stop()
        {
            try
            {
                speechStop();
            }
            catch (DllNotFoundException)
            {
                // DLL not found - silently ignore
            }
            catch (Exception ex)
            {
                AccessibilityMod.Logger?.Error($"Failed to stop speech: {ex.Message}");
            }
        }
    }
}
