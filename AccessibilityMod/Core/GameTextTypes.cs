using UnityAccessibilityLib;

namespace AccessibilityMod.Core
{
    /// <summary>
    /// Game-specific text types for Phoenix Wright: Ace Attorney Trilogy.
    /// Extends the base TextType constants from MelonAccessibilityLib.
    /// </summary>
    public static class GameTextType
    {
        // Base types from library (for convenience)
        public const int Dialogue = TextType.Dialogue;
        public const int Narrator = TextType.Narrator;
        public const int Menu = TextType.Menu;
        public const int MenuChoice = TextType.MenuChoice;
        public const int System = TextType.System;
        public const int SystemMessage = TextType.System; // Alias for compatibility

        // Game-specific types
        public const int Investigation = TextType.CustomBase + 1;
        public const int Evidence = TextType.CustomBase + 2;
        public const int Trial = TextType.CustomBase + 3;
        public const int PsycheLock = TextType.CustomBase + 4;
        public const int Credits = TextType.CustomBase + 5;
    }
}
