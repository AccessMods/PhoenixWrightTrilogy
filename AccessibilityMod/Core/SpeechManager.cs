using System;

namespace AccessibilityMod.Core
{
    /// <summary>
    /// High-level speech output manager.
    /// Handles formatting, duplicate prevention, and repeat functionality.
    /// </summary>
    public static class SpeechManager
    {
        // Repeat functionality
        private static string _currentSpeaker = "";
        private static string _currentText = "";
        private static TextType _currentType = TextType.Dialogue;

        // Duplicate prevention
        private static string _lastOutputMessage = "";
        private static DateTime _lastOutputTime = DateTime.MinValue;
        private const double DuplicateWindowSeconds = 0.5;

        /// <summary>
        /// Initialize the speech system.
        /// </summary>
        public static void Initialize()
        {
            UniversalSpeechWrapper.Initialize();
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
            UniversalSpeechWrapper.Speak(formattedText);
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
                UniversalSpeechWrapper.Speak(formattedText);
                AccessibilityMod.Logger?.Msg($"Repeating: '{formattedText}'");
            }
            else
            {
                AccessibilityMod.Logger?.Msg("Nothing to repeat");
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
