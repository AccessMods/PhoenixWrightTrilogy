using System;
using System.Text;
using System.Text.RegularExpressions;
using AccessibilityMod.Core;
using AccessibilityMod.Services;
using HarmonyLib;
using UnityEngine;

namespace AccessibilityMod.Patches
{
    /// <summary>
    /// Patches for capturing and announcing dialogue text.
    /// Uses multiple hook points for robust text capture:
    /// - Arrow appearance (normal dialogue)
    /// - Guide icon changes (indicates UI ready for input)
    /// - Message board state changes
    /// All hooks use centralized duplicate detection to prevent double announcements.
    /// </summary>
    [HarmonyPatch]
    public static class DialoguePatches
    {
        // Internal so CutscenePatches can check for duplicates
        internal static string _lastAnnouncedText = "";
        private static int _lastSpeakerId = -1;

        // Regex for detecting button placeholders (multiple spaces or full-width spaces)
        private static readonly Regex SpacePlaceholderRegex = new Regex(
            @"[\u3000]+| {3,}",
            RegexOptions.Compiled
        );

        #region Arrow Hooks

        /// <summary>
        /// Hook when arrow state changes. The right arrow (type 0) appearing typically
        /// means text is complete and waiting for player input.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(messageBoardCtrl), "arrow")]
        public static void Arrow_Postfix(messageBoardCtrl __instance, bool in_arrow, int in_type)
        {
            try
            {
                // Only handle right arrow (type 0) when message board is active
                if (in_type != 0 || !__instance.body_active)
                    return;

                if (in_arrow)
                {
                    // Normal case: arrow appearing means text is complete
                    TryOutputDialogue();
                }
                else if (IsInCrossExaminationMode())
                {
                    // Cross-examination: on the last testimony line, the arrow is hidden
                    // but we still need to read the dialogue.
                    TryOutputDialogue();
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in Arrow patch: {ex.Message}"
                );
            }
        }

        #endregion

        #region Guide Hooks

        /// <summary>
        /// Hook when guide icons are set. This is called when the game shows
        /// input options (like Press/Present during cross-examination, or
        /// normal dialogue options). This catches cases where the arrow
        /// might not be shown.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(guideCtrl), "guideIconSet")]
        public static void GuideIconSet_Postfix(
            guideCtrl __instance,
            bool in_guide,
            guideCtrl.GuideType in_type
        )
        {
            try
            {
                // Only process when setting active guide types that indicate text is ready
                if (!in_guide)
                    return;

                // Only output if the game is actually waiting for input, not during text display
                if (IsDialogueGuideType(in_type) && IsWaitingForInput())
                {
                    TryOutputDialogue();
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in GuideIconSet patch: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Hook when guide changes via animation. This catches guide transitions
        /// that might indicate new text is ready.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(guideCtrl), "changeGuide", new Type[] { typeof(guideCtrl.GuideType) })]
        public static void ChangeGuide_Postfix(guideCtrl __instance, guideCtrl.GuideType in_type)
        {
            try
            {
                // Only output if the game is actually waiting for input, not during text display
                if (IsDialogueGuideType(in_type) && IsWaitingForInput())
                {
                    TryOutputDialogue();
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in ChangeGuide patch: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Check if this guide type indicates dialogue is ready to be read.
        /// </summary>
        private static bool IsDialogueGuideType(guideCtrl.GuideType guideType)
        {
            switch (guideType)
            {
                case guideCtrl.GuideType.HOUTEI: // Normal court dialogue
                case guideCtrl.GuideType.QUESTIONING: // Cross-examination
                case guideCtrl.GuideType.PSYLOCK: // Psyche-Lock sequences
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Check if the game is waiting for player input (text display is complete).
        /// This prevents triggering during letter-by-letter text display.
        /// </summary>
        private static bool IsWaitingForInput()
        {
            try
            {
                if (GSStatic.message_work_ == null)
                    return false;

                var status = GSStatic.message_work_.status;

                // RT_WAIT means the game is waiting for player to press a button
                // LOOP means we're in cross-examination loop (also waiting for input)
                return (status & MessageSystem.Status.RT_WAIT) != 0
                    || (status & MessageSystem.Status.LOOP) != 0;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Message Board Hooks

        /// <summary>
        /// Hook BEFORE message board closes to capture any pending dialogue.
        /// This catches cases where the board closes (e.g., court record opens)
        /// before ClearText is called.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(messageBoardCtrl), "board")]
        public static void Board_Prefix(messageBoardCtrl __instance, bool in_board, bool in_mes)
        {
            try
            {
                // Only capture when board is about to close and is currently active
                if (!in_board && __instance.body_active)
                {
                    // Capture any text before the board closes
                    TryOutputDialogue();
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in Board prefix patch: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Hook when message board opens/closes. Reset tracking when closed.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(messageBoardCtrl), "board")]
        public static void Board_Postfix(messageBoardCtrl __instance, bool in_board, bool in_mes)
        {
            try
            {
                if (!in_board)
                {
                    // Dialogue window closed - reset tracking
                    _lastAnnouncedText = "";
                    _lastSpeakerId = -1;
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in Board patch: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Hook when speaker name changes. Track the current speaker ID.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(messageBoardCtrl), "name_plate")]
        public static void NamePlate_Postfix(
            messageBoardCtrl __instance,
            bool in_name,
            int in_name_no,
            int in_pos
        )
        {
            try
            {
                if (in_name && in_name_no > 0)
                {
                    AccessibilityMod.Core.AccessibilityMod.Logger?.Msg($"Speaker ID: {in_name_no}");
                }

                if (in_name && in_name_no != _lastSpeakerId)
                {
                    _lastSpeakerId = in_name_no;
                }
                else if (!in_name)
                {
                    _lastSpeakerId = -1;
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in NamePlate patch: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Hook LoadMsgSet for saved message restoration (loading a save).
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(messageBoardCtrl), "LoadMsgSet")]
        public static void LoadMsgSet_Postfix(messageBoardCtrl __instance)
        {
            try
            {
                if (__instance.body_active)
                {
                    TryOutputDialogue();
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in LoadMsgSet patch: {ex.Message}"
                );
            }
        }

        #endregion

        #region MessageSystem Hooks

        /// <summary>
        /// Hook ClearText to capture dialogue before it's cleared.
        /// This serves as a safety net for auto-advancing dialogue where other hooks
        /// might miss the text. ClearText is always called before displaying the next
        /// message, making it a reliable last-chance capture point.
        ///
        /// Auto-advancing can happen via:
        /// - code 7: Script-specified auto-advance
        /// - RT_GO status: After certain events complete
        /// - NEXT_MESSAGE status: Programmatic advancement
        ///
        /// The duplicate detection in TryOutputDialogue prevents double announcements
        /// if other hooks already captured the text.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MessageSystem), "ClearText")]
        public static void ClearText_Prefix(MessageWork message_work)
        {
            try
            {
                // Only capture if message board is active and has text
                var ctrl = messageBoardCtrl.instance;
                if (ctrl == null || !ctrl.body_active)
                    return;

                // Always try to capture - duplicate detection will prevent double announcements
                // This ensures we catch auto-advancing dialogue that other hooks might miss
                TryOutputDialogue();
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in ClearText patch: {ex.Message}"
                );
            }
        }

        #endregion

        #region SubWindow Hooks

        /// <summary>
        /// Hook SubWindow.SetReq to capture dialogue when any window opens as an overlay.
        /// Many game modes (evidence presentation, profiles, minigames) open via SetReq
        /// without closing the message board or clearing text. This universal hook captures
        /// dialogue before any non-EXIT request opens a window.
        /// Duplicate detection prevents double announcements if other hooks already captured.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(SubWindow), "SetReq")]
        public static void SetReq_Prefix(SubWindow __instance, SubWindow.Req req)
        {
            try
            {
                // Skip EXIT requests - those close windows, not open them
                // Also skip NONE and IDLE which don't change anything
                if (
                    req == SubWindow.Req.NONE
                    || req == SubWindow.Req.IDLE
                    || req.ToString().Contains("EXIT")
                )
                {
                    return;
                }

                // Capture dialogue before any window opens
                var ctrl = messageBoardCtrl.instance;
                if (ctrl != null && ctrl.body_active)
                {
                    TryOutputDialogue();
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in SetReq patch: {ex.Message}"
                );
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Check if we're in cross-examination mode (testimony with LOOP status).
        /// During cross-examination, the last testimony line doesn't show the forward arrow.
        /// </summary>
        private static bool IsInCrossExaminationMode()
        {
            try
            {
                // r.no_0 == 7 indicates testimony/questioning mode
                // LOOP status (0x8) indicates we're in the cross-examination loop
                return GSStatic.global_work_ != null
                    && GSStatic.global_work_.r.no_0 == 7
                    && GSStatic.message_work_ != null
                    && (GSStatic.message_work_.status & MessageSystem.Status.LOOP) != 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Central method for attempting to output dialogue.
        /// Handles duplicate detection and all the logic for extracting and announcing text.
        /// </summary>
        private static void TryOutputDialogue()
        {
            try
            {
                var ctrl = messageBoardCtrl.instance;
                if (ctrl == null || !ctrl.body_active)
                    return;

                // Get text from line_list
                string text = CombineLines(ctrl);

                if (Net35Extensions.IsNullOrWhiteSpace(text) || text == _lastAnnouncedText)
                    return;

                _lastAnnouncedText = text;

                // Replace button placeholders with actual key names
                text = ReplaceButtonPlaceholders(ctrl, text);

                // Get speaker name
                string speakerName = "";
                if (_lastSpeakerId > 0)
                {
                    speakerName = CharacterNameService.GetName(_lastSpeakerId);
                }

                // Also try to get from GSStatic if available
                if (Net35Extensions.IsNullOrWhiteSpace(speakerName))
                {
                    try
                    {
                        if (GSStatic.message_work_ != null && GSStatic.message_work_.speaker_id > 0)
                        {
                            speakerName = CharacterNameService.GetName(
                                GSStatic.message_work_.speaker_id
                            );
                        }
                    }
                    catch { }
                }

                ClipboardManager.Output(speakerName, text, TextType.Dialogue);
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error outputting dialogue: {ex.Message}"
                );
            }
        }

        private static string ReplaceButtonPlaceholders(messageBoardCtrl ctrl, string text)
        {
            try
            {
                // Check if text contains placeholder patterns
                if (!SpacePlaceholderRegex.IsMatch(text))
                    return text;

                // Get the key icon from the message board
                var keyIcon = ctrl.msg_key_icon;
                if (keyIcon == null || keyIcon.key_icon == null)
                    return text;

                // Find the first active key icon and get its key type
                KeyType activeKeyType = KeyType.None;
                foreach (var icon in keyIcon.key_icon)
                {
                    if (icon != null && icon.icon_active_)
                    {
                        activeKeyType = icon.icon_key_type_;
                        break;
                    }
                }

                // Get the key name
                string keyName = null;
                if (activeKeyType != KeyType.None)
                {
                    keyName = GetKeyName(activeKeyType);
                }

                // Replace the placeholder
                if (!string.IsNullOrEmpty(keyName))
                {
                    text = SpacePlaceholderRegex.Replace(text, $" [{keyName}] ");
                }

                return text;
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error replacing button placeholders: {ex.Message}"
                );
                return text;
            }
        }

        private static string GetKeyName(KeyType keyType)
        {
            // Check if using controller or keyboard
            bool isController = keyGuideBase.keyguid_pad_;

            if (isController)
            {
                // Controller button names (Xbox style)
                switch (keyType)
                {
                    case KeyType.A:
                        return "A";
                    case KeyType.B:
                        return "B";
                    case KeyType.X:
                        return "X";
                    case KeyType.Y:
                        return "Y";
                    case KeyType.L:
                        return "LB";
                    case KeyType.R:
                        return "RB";
                    case KeyType.ZL:
                        return "LT";
                    case KeyType.ZR:
                        return "RT";
                    case KeyType.Start:
                        return "Menu";
                    case KeyType.Select:
                        return "View";
                    case KeyType.StickL:
                        return "Left Stick";
                    case KeyType.StickR:
                        return "Right Stick";
                    case KeyType.Record:
                        return "RB";
                    default:
                        return keyType.ToString();
                }
            }
            else
            {
                // Get keyboard binding
                try
                {
                    var keyCode = padCtrl.instance.GetKeyCode(keyType);
                    return GetKeyCodeName(keyCode);
                }
                catch
                {
                    return keyType.ToString();
                }
            }
        }

        private static string GetKeyCodeName(KeyCode keyCode)
        {
            switch (keyCode)
            {
                case KeyCode.Space:
                    return "Space";
                case KeyCode.Return:
                    return "Enter";
                case KeyCode.Escape:
                    return "Escape";
                case KeyCode.Tab:
                    return "Tab";
                case KeyCode.Backspace:
                    return "Backspace";
                case KeyCode.Delete:
                    return "Delete";
                case KeyCode.Insert:
                    return "Insert";
                case KeyCode.Home:
                    return "Home";
                case KeyCode.End:
                    return "End";
                case KeyCode.PageUp:
                    return "Page Up";
                case KeyCode.PageDown:
                    return "Page Down";
                case KeyCode.UpArrow:
                    return "Up Arrow";
                case KeyCode.DownArrow:
                    return "Down Arrow";
                case KeyCode.LeftArrow:
                    return "Left Arrow";
                case KeyCode.RightArrow:
                    return "Right Arrow";
                case KeyCode.LeftShift:
                    return "Left Shift";
                case KeyCode.RightShift:
                    return "Right Shift";
                case KeyCode.LeftControl:
                    return "Left Ctrl";
                case KeyCode.RightControl:
                    return "Right Ctrl";
                case KeyCode.LeftAlt:
                    return "Left Alt";
                case KeyCode.RightAlt:
                    return "Right Alt";
                default:
                    string name = keyCode.ToString();
                    if (name.StartsWith("Alpha"))
                        return name.Substring(5);
                    if (name.StartsWith("Keypad"))
                        return "Numpad " + name.Substring(6);
                    return name;
            }
        }

        private static string CombineLines(messageBoardCtrl ctrl)
        {
            if (ctrl.line_list == null || ctrl.line_list.Count == 0)
                return "";

            StringBuilder sb = new StringBuilder();
            foreach (var line in ctrl.line_list)
            {
                if (line != null && !Net35Extensions.IsNullOrWhiteSpace(line.text))
                {
                    if (sb.Length > 0)
                        sb.Append(" ");
                    sb.Append(line.text);
                }
            }
            return sb.ToString();
        }

        #endregion
    }
}
