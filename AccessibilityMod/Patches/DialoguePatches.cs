using System;
using System.Text;
using System.Text.RegularExpressions;
using AccessibilityMod.Core;
using AccessibilityMod.Services;
using HarmonyLib;
using UnityAccessibilityLib;
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
                    TryOutputDialogue("Arrow");
                }
                else if (IsInCrossExaminationMode())
                {
                    // Cross-examination: on the last testimony line, the arrow is hidden
                    // but we still need to read the dialogue.
                    TryOutputDialogue("Arrow_CrossExam");
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
                    TryOutputDialogue("GuideIconSet");
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
                    TryOutputDialogue("ChangeGuide");
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
        /// Hook when message board opens/closes. Reset speaker tracking when closed.
        /// Note: We intentionally do NOT reset _lastAnnouncedText here because the board
        /// can close and reopen briefly during cross-examination (when pressing statements),
        /// and we don't want to re-announce the same text.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(messageBoardCtrl), "board")]
        public static void Board_Postfix(messageBoardCtrl __instance, bool in_board, bool in_mes)
        {
            try
            {
                if (!in_board)
                {
                    // Dialogue window closed - reset speaker tracking only
                    // Keep _lastAnnouncedText to prevent re-announcing same text after brief closes
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

        #endregion

        #region Fade Hooks

        /// <summary>
        /// Hook fadeCtrl.play to capture transition text before screen fades to black.
        /// Transition text like "To be continued" appears without arrows and the scene
        /// transitions immediately after. By hooking the fade, we catch this text at
        /// the moment the screen starts going dark.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(
            typeof(fadeCtrl),
            "play",
            typeof(uint),
            typeof(uint),
            typeof(uint),
            typeof(uint)
        )]
        public static void FadePlay_Prefix(uint in_status)
        {
            try
            {
                // Status 1 = FADE_IN (screen going black)
                // This is when episode transitions typically occur
                if (in_status != 1)
                    return;

                var ctrl = messageBoardCtrl.instance;
                if (ctrl == null || !ctrl.body_active)
                    return;

                // Transition text never has a speaker name plate
                bool namePlateVisible = false;
                try
                {
                    namePlateVisible = ctrl.sprite_name != null && ctrl.sprite_name.active;
                }
                catch { }

                if (namePlateVisible)
                    return;

                string text = CombineLines(ctrl);
                string cleanedText = TextCleaner.Clean(text);

                if (!Net35Extensions.IsNullOrWhiteSpace(cleanedText) && text != _lastAnnouncedText)
                {
                    _lastAnnouncedText = text;
                    SpeechManager.Output("", text, GameTextType.Narrator);
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in FadePlay patch: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Hook when speaker name changes. Track the current speaker ID as fallback.
        /// The primary source for speaker ID is now message_work.speaker_id in TryOutputDialogue.
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
                    TryOutputDialogue("LoadMsgSet");
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

                // Skip when message board text is stale (court record, evidence details, 3D evidence)
                if (IsInStaleDialogueMode())
                    return;

                // Always try to capture - duplicate detection will prevent double announcements
                // This ensures we catch auto-advancing dialogue that other hooks might miss
                TryOutputDialogue("ClearText");
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
                    || req == SubWindow.Req.STATUS_SETU
                    || req.ToString().Contains("EXIT")
                )
                {
                    return;
                }

                // Skip when message board text is stale (court record, evidence details, 3D evidence)
                if (IsInStaleDialogueMode())
                    return;

                // Capture dialogue before any window opens
                var ctrl = messageBoardCtrl.instance;
                if (ctrl != null && ctrl.body_active)
                {
                    TryOutputDialogue("SetReq");
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
        /// Check if we're in a mode where the message board text is stale and shouldn't
        /// be captured. This includes court record, evidence details, and 3D evidence modes.
        /// </summary>
        private static bool IsInStaleDialogueMode()
        {
            return AccessibilityState.IsInCourtRecordMode()
                || AccessibilityState.IsInEvidenceDetailsMode()
                || AccessibilityState.IsIn3DEvidenceMode();
        }

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
        /// <param name="source">The name of the hook that triggered this call (for debug logging)</param>
        private static void TryOutputDialogue(string source = "unknown")
        {
            try
            {
                var ctrl = messageBoardCtrl.instance;
                if (ctrl == null || !ctrl.body_active)
                    return;

                // Get text from line_list
                string text = CombineLines(ctrl);

                if (Net35Extensions.IsNullOrWhiteSpace(text))
                    return;

                if (text == _lastAnnouncedText)
                {
#if DEBUG
                    string preview = text.Length > 40 ? text.Substring(0, 40) + "..." : text;
                    AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                        $"[DialoguePatches:{source}] Skipped duplicate: \"{preview}\""
                    );
#endif
                    return;
                }

                _lastAnnouncedText = text;

                // Replace button placeholders with actual key names
                text = ReplaceButtonPlaceholders(ctrl, text);

                // Get speaker name only if the name plate is actually visible
                // This is the authoritative check - speaker IDs persist between messages,
                // but the name plate visibility accurately reflects whether a speaker is shown
                string speakerName = "";
                bool namePlateVisible = false;
                try
                {
                    namePlateVisible = ctrl.sprite_name != null && ctrl.sprite_name.active;
                }
                catch { }

                if (namePlateVisible)
                {
                    int speakerId = 0;

                    // GS3 uses different ID systems: name_plate receives one ID format while
                    // message_work_.speaker_id uses another. The GS3_NAMES dictionary is keyed
                    // by message_work_.speaker_id values, so we must use that for GS3.
                    // For GS1/GS2, prefer _lastSpeakerId from name_plate calls since
                    // message_work_.speaker_id can be stale in certain modes (e.g., 3D examination).
                    bool isGS3 = false;
                    try
                    {
                        isGS3 =
                            GSStatic.global_work_ != null
                            && GSStatic.global_work_.title == TitleId.GS3;
                    }
                    catch { }

                    if (isGS3)
                    {
                        // For GS3, always use message_work_.speaker_id (dictionary keys match this)
                        try
                        {
                            if (GSStatic.message_work_ != null)
                            {
                                speakerId = GSStatic.message_work_.speaker_id & 0x7F;
                            }
                        }
                        catch { }
                    }
                    else
                    {
                        // For GS1/GS2, prefer _lastSpeakerId from name_plate calls
                        // since message_work_.speaker_id can be stale during 3D examination
                        if (_lastSpeakerId > 0)
                        {
                            speakerId = _lastSpeakerId;
                        }

                        // Fallback to message_work_.speaker_id if name_plate wasn't called
                        if (speakerId <= 0)
                        {
                            try
                            {
                                if (GSStatic.message_work_ != null)
                                {
                                    speakerId = GSStatic.message_work_.speaker_id & 0x7F;
                                }
                            }
                            catch { }
                        }
                    }

                    if (speakerId > 0)
                    {
                        speakerName = CharacterNameService.GetName(speakerId);
                    }
                }

#if DEBUG
                string textPreview = text.Length > 50 ? text.Substring(0, 50) + "..." : text;
                AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                    $"[DialoguePatches:{source}] Announcing: speaker=\"{speakerName}\", text=\"{textPreview}\""
                );
#endif

                SpeechManager.Output(speakerName, text, GameTextType.Dialogue);
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
                        return L.Get("gamepad.lb");
                    case KeyType.R:
                        return L.Get("gamepad.rb");
                    case KeyType.ZL:
                        return L.Get("gamepad.lt");
                    case KeyType.ZR:
                        return L.Get("gamepad.rt");
                    case KeyType.Start:
                        return L.Get("gamepad.menu");
                    case KeyType.Select:
                        return L.Get("gamepad.view");
                    case KeyType.StickL:
                        return L.Get("gamepad.left_stick");
                    case KeyType.StickR:
                        return L.Get("gamepad.right_stick");
                    case KeyType.Record:
                        return L.Get("gamepad.rb");
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
                    return L.Get("key.space");
                case KeyCode.Return:
                    return L.Get("key.enter");
                case KeyCode.Escape:
                    return L.Get("key.escape");
                case KeyCode.Tab:
                    return L.Get("key.tab");
                case KeyCode.Backspace:
                    return L.Get("key.backspace");
                case KeyCode.Delete:
                    return L.Get("key.delete");
                case KeyCode.Insert:
                    return "Insert";
                case KeyCode.Home:
                    return L.Get("key.home");
                case KeyCode.End:
                    return L.Get("key.end");
                case KeyCode.PageUp:
                    return L.Get("key.page_up");
                case KeyCode.PageDown:
                    return L.Get("key.page_down");
                case KeyCode.UpArrow:
                    return L.Get("key.up_arrow");
                case KeyCode.DownArrow:
                    return L.Get("key.down_arrow");
                case KeyCode.LeftArrow:
                    return L.Get("key.left_arrow");
                case KeyCode.RightArrow:
                    return L.Get("key.right_arrow");
                case KeyCode.LeftShift:
                    return L.Get("key.left_shift");
                case KeyCode.RightShift:
                    return L.Get("key.right_shift");
                case KeyCode.LeftControl:
                    return L.Get("key.left_ctrl");
                case KeyCode.RightControl:
                    return L.Get("key.right_ctrl");
                case KeyCode.LeftAlt:
                    return L.Get("key.left_alt");
                case KeyCode.RightAlt:
                    return L.Get("key.right_alt");
                default:
                    string name = keyCode.ToString();
                    if (name.StartsWith("Alpha"))
                        return name.Substring(5);
                    if (name.StartsWith("Keypad"))
                        return L.Get("key.numpad_x", name.Substring(6));
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
