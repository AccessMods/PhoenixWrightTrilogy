using System;
using System.Runtime.InteropServices;

namespace AccessibilityMod.Core
{
    /// <summary>
    /// Speech output manager using the UniversalSpeech library.
    /// Provides screen reader output with SAPI fallback.
    /// </summary>
    public static class SpeechManager
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

        // Repeat functionality
        private static string _currentSpeaker = "";
        private static string _currentText = "";
        private static TextType _currentType = TextType.Dialogue;

        // Duplicate prevention
        private static string _lastOutputMessage = "";
        private static DateTime _lastOutputTime = DateTime.MinValue;
        private const double DuplicateWindowSeconds = 0.5;

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
                AccessibilityMod.Logger?.Msg("SpeechManager initialized with UniversalSpeech");
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
                AccessibilityMod.Logger?.Error($"Failed to initialize SpeechManager: {ex.Message}");
            }
        }

        /// <summary>
        /// Output text with optional speaker name. Handles formatting, duplicate prevention, and repeat storage.
        /// </summary>
        public static void Output(
            string speaker,
            string text,
            TextType textType = TextType.Dialogue
        )
        {
            if (Net35Extensions.IsNullOrWhiteSpace(text))
                return;

            string formattedText = FormatText(speaker, text, textType);

            // Duplicate prevention - skip if same text within window
            DateTime now = DateTime.UtcNow;
            if (
                formattedText == _lastOutputMessage
                && (now - _lastOutputTime).TotalSeconds < DuplicateWindowSeconds
            )
            {
                return;
            }

            _lastOutputMessage = formattedText;
            _lastOutputTime = now;

            // Store for repeat functionality
            if (textType == TextType.Dialogue || textType == TextType.Narrator)
            {
                _currentSpeaker = speaker ?? "";
                _currentText = text;
                _currentType = textType;
            }

            // Output via speech
            Speak(formattedText);
            AccessibilityMod.Logger?.Msg($"[{textType}] {formattedText}");

#if DEBUG
            AccessibilityMod.Logger?.Msg(Environment.StackTrace);
#endif
        }

        /// <summary>
        /// Announce text without a speaker name.
        /// </summary>
        public static void Announce(string text, TextType textType = TextType.SystemMessage)
        {
            Output("", text, textType);
        }

        /// <summary>
        /// Repeat the last dialogue or narrator text.
        /// </summary>
        public static void RepeatLast()
        {
            if (!Net35Extensions.IsNullOrWhiteSpace(_currentText))
            {
                string formattedText = FormatText(_currentSpeaker, _currentText, _currentType);
                Speak(formattedText);
                AccessibilityMod.Logger?.Msg($"Repeating: '{formattedText}'");
            }
            else
            {
                AccessibilityMod.Logger?.Msg("Nothing to repeat");
            }
        }

        /// <summary>
        /// Speak the given text directly. Does not interrupt current speech by default.
        /// </summary>
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

        private static string FormatText(string speaker, string text, TextType textType)
        {
            text = TextCleaner.Clean(text);

            switch (textType)
            {
                case TextType.Dialogue:
                    if (!Net35Extensions.IsNullOrWhiteSpace(speaker))
                        return $"{speaker}: {text}";
                    return text;
                case TextType.Menu:
                case TextType.MenuChoice:
                    return text;
                default:
                    return text;
            }
        }
    }

    public enum TextType
    {
        Dialogue,
        Narrator,
        Menu,
        MenuChoice,
        Investigation,
        Evidence,
        SystemMessage,
        Trial,
        PsycheLock,
    }
}
