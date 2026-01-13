using System;
using System.Collections.Generic;
using AccessibilityMod.Core;
using AccessibilityMod.Services;
using HarmonyLib;
using UnityAccessibilityLib;

namespace AccessibilityMod.Patches
{
    [HarmonyPatch]
    public static class MenuPatches
    {
        private static int _lastTanteiCursor = -1;
        private static int _lastSelectCursor = -1;
        private static int _lastTitleSelectCursor = -1;
        private static string[] _tanteiMenuOptions = new string[4];
        private static List<string> _selectOptions = new List<string>();

        // Exposed for CoroutineRunner to access checkmark status
        internal static List<bool> SelectOptionsRead = new List<bool>();
        internal static List<bool> SelectOptionsPsyLock = new List<bool>();
        internal static bool IsTalkMenu = false;

        // Main menu tracking
        private static int _lastSeriesTitle = -1;

        // Scenario selection tracking (Play Title episode selection)
        private static int _lastScenarioEpisode = -1;

        #region Start Screen Patch

        // Start screen - announce "Press Enter to Start" text when game finishes loading
        [HarmonyPostfix]
        [HarmonyPatch(typeof(startCtrl), "inputTextSet")]
        public static void StartCtrl_InputTextSet_Postfix(startCtrl __instance)
        {
            try
            {
                SpeechManager.Announce(L.Get("system.loaded"), GameTextType.SystemMessage);
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in StartCtrl_InputTextSet patch: {ex.Message}"
                );
            }
        }

        #endregion

        #region Main Title Menu Patches

        // Main title menu initialization - announce when main menu appears
        [HarmonyPostfix]
        [HarmonyPatch(typeof(titleSelectPlate), "playCursor")]
        public static void TitleSelectPlate_PlayCursor_Postfix(
            titleSelectPlate __instance,
            int in_type
        )
        {
            try
            {
                // Check if this is an optionCtrl confirmation dialog
                if (TryAnnounceOptionCtrlConfirmation(__instance))
                    return;

                // Check if this is the exit game confirmation dialog
                if (TryAnnounceExitGameDialog(__instance))
                    return;

                // Check if this is the main menu (from mainTitleCtrl)
                var mainTitle = mainTitleCtrl.instance;
                if (mainTitle == null)
                    return;

                var field = typeof(mainTitleCtrl).GetField(
                    "select_plate_",
                    System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Instance
                );
                if (field == null)
                    return;

                var selectPlate = field.GetValue(mainTitle) as titleSelectPlate;
                if (selectPlate != __instance)
                    return;

                // Get the menu options and announce
                string optionText = GetMainMenuOptionText(mainTitle, __instance.cursor_no);
                if (!Net35Extensions.IsNullOrWhiteSpace(optionText))
                {
                    string message = L.Get("menu.main_menu", optionText);
                    SpeechManager.Announce(message, GameTextType.Menu);
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in TitleSelectPlate_PlayCursor patch: {ex.Message}"
                );
            }
        }

        private static bool TryAnnounceOptionCtrlConfirmation(titleSelectPlate instance)
        {
            try
            {
                var optionInstance = optionCtrl.instance;
                if (optionInstance == null || !optionInstance.is_open)
                    return false;

                // Check if this is the confirmation_select_ from optionCtrl
                var confirmField = typeof(optionCtrl).GetField(
                    "confirmation_select_",
                    System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Instance
                );
                if (confirmField == null)
                    return false;

                var confirmSelect = confirmField.GetValue(optionInstance) as titleSelectPlate;
                if (confirmSelect != instance)
                    return false;

                // This is a confirmation dialog - schedule delayed announcement
                // The message is set AFTER playCursor() in some cases, so we need to wait
                CoroutineRunner.Instance?.ScheduleDelayedAnnouncement(
                    0.1f, // Small delay to let the message be set
                    () => GetOptionCtrlConfirmationMessage(optionInstance),
                    GameTextType.Menu
                );
                return true;
            }
            catch { }
            return false;
        }

        private static string GetOptionCtrlConfirmationMessage(optionCtrl instance)
        {
            try
            {
                var field = typeof(optionCtrl).GetField(
                    "title_back_text_",
                    System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Instance
                );
                if (field == null)
                    return null;

                var textList = field.GetValue(instance) as List<UnityEngine.UI.Text>;
                if (textList == null || textList.Count == 0)
                    return null;

                // Combine both lines of the message
                string line1 = textList.Count > 0 ? textList[0]?.text : null;
                string line2 = textList.Count > 1 ? textList[1]?.text : null;

                string message = "";
                if (!Net35Extensions.IsNullOrWhiteSpace(line1))
                    message = line1;
                if (!Net35Extensions.IsNullOrWhiteSpace(line2))
                    message = Net35Extensions.IsNullOrWhiteSpace(message)
                        ? line2
                        : message + " " + line2;

                return message;
            }
            catch
            {
                return null;
            }
        }

        private static string GetMainMenuOptionText(mainTitleCtrl mainTitle, int index)
        {
            try
            {
                var field = typeof(mainTitleCtrl).GetField(
                    "select_text_",
                    System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Instance
                );
                if (field == null)
                    return null;

                var selectText = field.GetValue(mainTitle) as titleSelectPlate.ButtonParam[][];
                if (selectText == null)
                    return null;

                var typeField = typeof(mainTitleCtrl).GetField(
                    "select_type_",
                    System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Instance
                );
                if (typeField == null)
                    return null;

                int selectType = (int)typeField.GetValue(mainTitle);
                if (selectType < 0 || selectType >= selectText.Length)
                    return null;

                var options = selectText[selectType];
                if (options == null || index < 0 || index >= options.Length)
                    return null;

                return options[index].message_;
            }
            catch
            {
                return null;
            }
        }

        // Generic titleSelectPlate cursor navigation - announces selection text when cursor moves
        // Used for yes/no confirmation dialogs and other titleSelectPlate instances
        [HarmonyPostfix]
        [HarmonyPatch(typeof(titleSelectPlate), "updateCursorPosition")]
        public static void TitleSelectPlate_UpdateCursorPosition_Postfix(
            titleSelectPlate __instance,
            int idx
        )
        {
            try
            {
                // Skip if cursor hasn't changed
                if (idx == _lastTitleSelectCursor)
                    return;

                _lastTitleSelectCursor = idx;

                // Skip if this is the main menu's titleSelectPlate - PlayCursor handles it
                var mainTitle = mainTitleCtrl.instance;
                if (mainTitle != null)
                {
                    var field = typeof(mainTitleCtrl).GetField(
                        "select_plate_",
                        System.Reflection.BindingFlags.NonPublic
                            | System.Reflection.BindingFlags.Instance
                    );
                    if (field != null)
                    {
                        var mainSelectPlate = field.GetValue(mainTitle) as titleSelectPlate;
                        if (mainSelectPlate == __instance)
                            return;
                    }
                }

                // Get the button text at the current cursor position
                string optionText = GetTitleSelectPlateText(__instance, idx);
                if (!Net35Extensions.IsNullOrWhiteSpace(optionText))
                {
                    SpeechManager.Announce(optionText, GameTextType.Menu);
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in TitleSelectPlate_UpdateCursorPosition patch: {ex.Message}"
                );
            }
        }

        private static string GetTitleSelectPlateText(titleSelectPlate instance, int idx)
        {
            try
            {
                var field = typeof(titleSelectPlate).GetField(
                    "select_list_",
                    System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Instance
                );
                if (field == null)
                    return null;

                var selectList = field.GetValue(instance) as System.Collections.IList;
                if (selectList == null || idx < 0 || idx >= selectList.Count)
                    return null;

                var item = selectList[idx];

                // Get the text_ field from the SelectPlate item
                var textField = item.GetType().GetField("text_");
                if (textField == null)
                    return null;

                var textComponent = textField.GetValue(item) as UnityEngine.UI.Text;
                return textComponent?.text;
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region Series Selection Patches

        // Series title selection - announce when entering series selection
        [HarmonyPostfix]
        [HarmonyPatch(typeof(seriesTitleSelectCtrl), "Play")]
        public static void SeriesSelect_Play_Postfix(seriesTitleSelectCtrl __instance)
        {
            try
            {
                _lastSeriesTitle = -1;
                string message = L.Get("menu.select_game");
                SpeechManager.Announce(message, GameTextType.Menu);
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in SeriesSelect_Play patch: {ex.Message}"
                );
            }
        }

        // Series title change - announce when changing between GS1, GS2, GS3
        [HarmonyPostfix]
        [HarmonyPatch(typeof(seriesTitleSelectCtrl), "changeTitle")]
        public static void SeriesSelect_ChangeTitle_Postfix(
            seriesTitleSelectCtrl __instance,
            TitleId title_id
        )
        {
            try
            {
                int titleIndex = (int)title_id;
                if (titleIndex == _lastSeriesTitle)
                    return;

                _lastSeriesTitle = titleIndex;
                string titleName = GetGameTitleName(title_id);
                SpeechManager.Announce(titleName, GameTextType.Menu);
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in SeriesSelect_ChangeTitle patch: {ex.Message}"
                );
            }
        }

        private static string GetGameTitleName(TitleId titleId)
        {
            switch (titleId)
            {
                case TitleId.GS1:
                    return L.Get("menu.game_title.gs1");
                case TitleId.GS2:
                    return L.Get("menu.game_title.gs2");
                case TitleId.GS3:
                    return L.Get("menu.game_title.gs3");
                default:
                    return L.Get("menu.game_x", (int)titleId + 1);
            }
        }

        #endregion

        #region Scenario Selection Patches (Play Title Episode Selection)

        // Scenario selection - announce when entering episode selection via "Play Title"
        [HarmonyPostfix]
        [HarmonyPatch(typeof(scenarioSelectCtrl), "Play")]
        public static void ScenarioSelect_Play_Postfix(scenarioSelectCtrl __instance)
        {
            try
            {
                _lastScenarioEpisode = -1;
                string message = L.Get("menu.episode_selection");
                SpeechManager.Announce(message, GameTextType.Menu);
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in ScenarioSelect_Play patch: {ex.Message}"
                );
            }
        }

        // Scenario selection - announce episode name when arrows update (after navigation)
        [HarmonyPostfix]
        [HarmonyPatch(typeof(scenarioSelectCtrl), "ArrowOn")]
        public static void ScenarioSelect_ArrowOn_Postfix(scenarioSelectCtrl __instance)
        {
            try
            {
                var currentNumField = typeof(scenarioSelectCtrl).GetField(
                    "current_num_",
                    System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Instance
                );
                if (currentNumField == null)
                    return;

                int currentNum = (int)currentNumField.GetValue(__instance);

                if (currentNum == _lastScenarioEpisode)
                    return;

                _lastScenarioEpisode = currentNum;

                string episodeName = GetScenarioEpisodeName(currentNum);
                SpeechManager.Announce(episodeName, GameTextType.Menu);
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in ScenarioSelect_ArrowOn patch: {ex.Message}"
                );
            }
        }

        // Scenario selection - announce confirmation dialog message
        [HarmonyPostfix]
        [HarmonyPatch(typeof(scenarioSelectCtrl), "SetMessage")]
        public static void ScenarioSelect_SetMessage_Postfix(
            scenarioSelectCtrl __instance,
            int in_title,
            int in_story
        )
        {
            try
            {
                var startTextField = typeof(scenarioSelectCtrl).GetField(
                    "start_text_",
                    System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Instance
                );
                if (startTextField == null)
                    return;

                var startText = startTextField.GetValue(__instance) as List<UnityEngine.UI.Text>;
                if (startText == null || startText.Count == 0)
                    return;

                string message = "";
                foreach (var text in startText)
                {
                    if (text != null && !Net35Extensions.IsNullOrWhiteSpace(text.text))
                    {
                        if (!Net35Extensions.IsNullOrWhiteSpace(message))
                            message += " ";
                        message += text.text;
                    }
                }

                if (!Net35Extensions.IsNullOrWhiteSpace(message))
                {
                    SpeechManager.Announce(message, GameTextType.Menu);
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in ScenarioSelect_SetMessage patch: {ex.Message}"
                );
            }
        }

        // Scenario selection - reset state on exit
        [HarmonyPostfix]
        [HarmonyPatch(typeof(scenarioSelectCtrl), "End")]
        public static void ScenarioSelect_End_Postfix()
        {
            _lastScenarioEpisode = -1;
        }

        private static string GetScenarioEpisodeName(int episodeIndex)
        {
            try
            {
                TitleId title = GSStatic.global_work_.title;
                TextDataCtrl.TitleTextID textId;

                switch (title)
                {
                    case TitleId.GS1:
                        textId = TextDataCtrl.TitleTextID.GS1_SCENARIO_NAME;
                        break;
                    case TitleId.GS2:
                        textId = TextDataCtrl.TitleTextID.GS2_SCENARIO_NAME;
                        break;
                    case TitleId.GS3:
                        textId = TextDataCtrl.TitleTextID.GS3_SCENARIO_NAME;
                        break;
                    default:
                        return L.Get("menu.episode_x", episodeIndex + 1);
                }

                string episodeTitle = TextDataCtrl.GetText(textId, episodeIndex);
                return L.Get("menu.episode_x_title", episodeIndex + 1, episodeTitle);
            }
            catch
            {
                return L.Get("menu.episode_x", episodeIndex + 1);
            }
        }

        #endregion

        #region Exit Game Dialogue Patch

        // Hook titleSelectPlate.playCursor more specifically for exit game dialog
        // Called from TitleSelectPlate_PlayCursor_Postfix when playCursor is invoked
        private static bool TryAnnounceExitGameDialog(titleSelectPlate instance)
        {
            try
            {
                var mainTitle = mainTitleCtrl.instance;
                if (mainTitle == null)
                    return false;

                // Check if this is the game_end_select_ from mainTitleCtrl
                var gameEndField = typeof(mainTitleCtrl).GetField(
                    "game_end_select_",
                    System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Instance
                );
                if (gameEndField == null)
                    return false;

                var gameEndSelect = gameEndField.GetValue(mainTitle) as titleSelectPlate;
                if (gameEndSelect != instance)
                    return false;

                // This is the exit game confirmation dialog
                // Schedule delayed announcement because the message text is set AFTER playCursor()
                CoroutineRunner.Instance?.ScheduleDelayedAnnouncement(
                    0.1f, // Small delay to let the message be set
                    () => GetExitGameDialogMessage(mainTitle),
                    GameTextType.Menu
                );
                return true;
            }
            catch { }
            return false;
        }

        private static string GetExitGameDialogMessage(mainTitleCtrl mainTitle)
        {
            try
            {
                var messageField = typeof(mainTitleCtrl).GetField(
                    "message_text_list_",
                    System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Instance
                );
                if (messageField == null)
                    return null;

                var messageList = messageField.GetValue(mainTitle) as List<UnityEngine.UI.Text>;
                if (messageList == null || messageList.Count == 0)
                    return null;

                string message = "";
                foreach (var text in messageList)
                {
                    if (text != null && !Net35Extensions.IsNullOrWhiteSpace(text.text))
                    {
                        if (!Net35Extensions.IsNullOrWhiteSpace(message))
                            message += " ";
                        message += text.text;
                    }
                }

                return Net35Extensions.IsNullOrWhiteSpace(message) ? null : message;
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region Chapter Jump Patches

        // Chapter jump warning - announce trophy warning message
        [HarmonyPostfix]
        [HarmonyPatch(typeof(ChapterJumpInMenuCtrl), "WarningInit")]
        public static void ChapterJump_WarningInit_Postfix(ChapterJumpInMenuCtrl __instance)
        {
            try
            {
                var startTextField = typeof(ChapterJumpInMenuCtrl).GetField(
                    "start_text_",
                    System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Instance
                );
                if (startTextField == null)
                    return;

                var startText = startTextField.GetValue(__instance) as List<UnityEngine.UI.Text>;
                if (startText == null || startText.Count == 0)
                    return;

                string message = "";
                foreach (var text in startText)
                {
                    if (text != null && !Net35Extensions.IsNullOrWhiteSpace(text.text))
                    {
                        if (!Net35Extensions.IsNullOrWhiteSpace(message))
                            message += " ";
                        message += text.text;
                    }
                }

                if (!Net35Extensions.IsNullOrWhiteSpace(message))
                {
                    SpeechManager.Announce(message, GameTextType.Menu);
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in ChapterJump_WarningInit patch: {ex.Message}"
                );
            }
        }

        // Chapter jump confirmation - announce play confirmation message
        [HarmonyPostfix]
        [HarmonyPatch(typeof(ChapterJumpInMenuCtrl), "EnterConfirm")]
        public static void ChapterJump_EnterConfirm_Postfix(ChapterJumpInMenuCtrl __instance)
        {
            try
            {
                var startTextField = typeof(ChapterJumpInMenuCtrl).GetField(
                    "start_text_",
                    System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Instance
                );
                if (startTextField == null)
                    return;

                var startText = startTextField.GetValue(__instance) as List<UnityEngine.UI.Text>;
                if (startText == null || startText.Count == 0)
                    return;

                string message = "";
                foreach (var text in startText)
                {
                    if (text != null && !Net35Extensions.IsNullOrWhiteSpace(text.text))
                    {
                        if (!Net35Extensions.IsNullOrWhiteSpace(message))
                            message += " ";
                        message += text.text;
                    }
                }

                if (!Net35Extensions.IsNullOrWhiteSpace(message))
                {
                    SpeechManager.Announce(message, GameTextType.Menu);
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in ChapterJump_EnterConfirm patch: {ex.Message}"
                );
            }
        }

        // Chapter jump menu - announce when entering chapter selection
        [HarmonyPostfix]
        [HarmonyPatch(typeof(ChapterJumpCtrl), "Play")]
        public static void ChapterJump_Play_Postfix()
        {
            try
            {
                string message = L.Get("menu.chapter_selection");
                SpeechManager.Announce(message, GameTextType.Menu);
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in ChapterJump_Play patch: {ex.Message}"
                );
            }
        }

        // Chapter jump episode change - announce episode name
        [HarmonyPostfix]
        [HarmonyPatch(typeof(ChapterJumpInMenuCtrl), "EpisodeDispRefresh")]
        public static void ChapterJump_EpisodeRefresh_Postfix(
            ChapterJumpInMenuCtrl __instance,
            int position
        )
        {
            try
            {
                // Get the actual episode name from the game's text data
                string episodeName = GetScenarioEpisodeName(position);
                SpeechManager.Announce(episodeName, GameTextType.Menu);
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in ChapterJump_EpisodeRefresh patch: {ex.Message}"
                );
            }
        }

        // GeneralSelectPlateCtrl playCursor - announce initial chapter selection
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GeneralSelectPlateCtrl), "playCursor")]
        public static void GeneralSelectPlate_PlayCursor_Postfix(GeneralSelectPlateCtrl __instance)
        {
            try
            {
                int cursorNo = __instance.cursor_no;
                string optionText = GetGeneralSelectOptionText(__instance, cursorNo);
                if (!Net35Extensions.IsNullOrWhiteSpace(optionText))
                {
                    SpeechManager.Announce(optionText, GameTextType.Menu);
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in GeneralSelectPlate_PlayCursor patch: {ex.Message}"
                );
            }
        }

        private static string GetGeneralSelectOptionText(
            GeneralSelectPlateCtrl selectPlate,
            int index
        )
        {
            try
            {
                var field = typeof(GeneralSelectPlateCtrl).GetField(
                    "select_list_",
                    System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Instance
                );
                if (field == null)
                    return null;

                var selectList = field.GetValue(selectPlate) as System.Collections.IList;
                if (selectList == null || index < 0 || index >= selectList.Count)
                    return null;

                var item = selectList[index];
                var textField = item.GetType().GetField("text_");
                if (textField == null)
                    return null;

                var textComponent = textField.GetValue(item) as UnityEngine.UI.Text;
                return textComponent?.text;
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region Detective Menu Patches

        // Detective menu setup
        [HarmonyPostfix]
        [HarmonyPatch(typeof(tanteiMenu), "setMenu")]
        public static void SetMenu_Postfix(tanteiMenu __instance, int in_type)
        {
            try
            {
                // Get menu options text
                _tanteiMenuOptions[0] = TextDataCtrl.GetText(TextDataCtrl.CommonTextID.INSPECT);
                _tanteiMenuOptions[1] = TextDataCtrl.GetText(TextDataCtrl.CommonTextID.ROOM_MOVE);
                _tanteiMenuOptions[2] = TextDataCtrl.GetText(TextDataCtrl.CommonTextID.TALK);
                _tanteiMenuOptions[3] = TextDataCtrl.GetText(TextDataCtrl.CommonTextID.TUKITUKE);

                _lastTanteiCursor = __instance.cursor_no;

                string currentOption = GetTanteiOption(__instance.cursor_no, in_type);
                string message = L.Get("menu.menu", currentOption);
                SpeechManager.Announce(message, GameTextType.Menu);

                AccessibilityState.SetMode(AccessibilityState.GameMode.Menu);
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in SetMenu patch: {ex.Message}"
                );
            }
        }

        // Detective menu cursor movement
        [HarmonyPostfix]
        [HarmonyPatch(typeof(tanteiMenu), "cursor")]
        public static void Cursor_Postfix(tanteiMenu __instance, bool is_right)
        {
            try
            {
                if (__instance.cursor_no != _lastTanteiCursor)
                {
                    _lastTanteiCursor = __instance.cursor_no;
                    string option = GetTanteiOption(__instance.cursor_no, __instance.setting);
                    SpeechManager.Announce(option, GameTextType.Menu);
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in Cursor patch: {ex.Message}"
                );
            }
        }

        private static string GetTanteiOption(int cursorNo, int menuType)
        {
            if (menuType == 0)
            {
                // 2-option menu: Examine, Move
                return cursorNo == 0 ? _tanteiMenuOptions[0] : _tanteiMenuOptions[1];
            }
            else
            {
                // 4-option menu: Examine, Move, Talk, Present
                if (cursorNo >= 0 && cursorNo < 4)
                    return _tanteiMenuOptions[cursorNo];
            }
            return L.Get("menu.unknown");
        }

        #endregion

        #region Selection Plate Patches (Dialogue Choices)

        // Selection plate text setting (choices/talk options)
        [HarmonyPostfix]
        [HarmonyPatch(typeof(selectPlateCtrl), "setText")]
        public static void SetText_Postfix(int index, string text)
        {
            try
            {
                // Ensure list is big enough
                while (_selectOptions.Count <= index)
                {
                    _selectOptions.Add("");
                }
                _selectOptions[index] = text;

                // Also expand read/psylock tracking lists
                while (SelectOptionsRead.Count <= index)
                {
                    SelectOptionsRead.Add(false);
                }
                while (SelectOptionsPsyLock.Count <= index)
                {
                    SelectOptionsPsyLock.Add(false);
                }
                // Reset read state when text is set (will be updated by setRead if this is a talk menu)
                SelectOptionsRead[index] = false;
                SelectOptionsPsyLock[index] = false;
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in SetText patch: {ex.Message}"
                );
            }
        }

        // Selection plate read state setting (talk menu checkmarks)
        [HarmonyPostfix]
        [HarmonyPatch(typeof(selectPlateCtrl), "setRead")]
        public static void SetRead_Postfix(int index, bool is_read, bool is_psylooc)
        {
            try
            {
                IsTalkMenu = true;

                // Ensure list is big enough
                while (SelectOptionsRead.Count <= index)
                {
                    SelectOptionsRead.Add(false);
                }
                while (SelectOptionsPsyLock.Count <= index)
                {
                    SelectOptionsPsyLock.Add(false);
                }

                SelectOptionsRead[index] = is_read;
                SelectOptionsPsyLock[index] = is_psylooc;
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in SetRead patch: {ex.Message}"
                );
            }
        }

        // Selection plate cursor activation
        [HarmonyPostfix]
        [HarmonyPatch(typeof(selectPlateCtrl), "playCursor")]
        public static void PlayCursor_Postfix(selectPlateCtrl __instance, int in_type)
        {
            try
            {
                _lastSelectCursor = __instance.cursor_no;

                if (__instance.cursor_no >= 0 && __instance.cursor_no < _selectOptions.Count)
                {
                    string currentOption = _selectOptions[__instance.cursor_no];
                    if (!Net35Extensions.IsNullOrWhiteSpace(currentOption))
                    {
                        string announcement = FormatSelectOptionAnnouncement(
                            currentOption,
                            __instance.cursor_no
                        );
                        SpeechManager.Announce(announcement, GameTextType.MenuChoice);
                    }
                }

                AccessibilityState.SetMode(AccessibilityState.GameMode.Menu);
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in PlayCursor patch: {ex.Message}"
                );
            }
        }

        // Selection plate cursor position change (touch input only)
        [HarmonyPostfix]
        [HarmonyPatch(typeof(selectPlateCtrl), "SetCursorNo")]
        public static void SetCursorNo_Postfix(selectPlateCtrl __instance, int in_cursor_no)
        {
            try
            {
                if (in_cursor_no != _lastSelectCursor && __instance.body_active)
                {
                    _lastSelectCursor = in_cursor_no;
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in SetCursorNo patch: {ex.Message}"
                );
            }
        }

        // Selection end - clear stored options
        [HarmonyPostfix]
        [HarmonyPatch(typeof(selectPlateCtrl), "end")]
        public static void End_Postfix()
        {
            try
            {
                _selectOptions.Clear();
                SelectOptionsRead.Clear();
                SelectOptionsPsyLock.Clear();
                IsTalkMenu = false;
                _lastSelectCursor = -1;
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in End patch: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Formats the announcement for a select option, including checkmark status for talk menus.
        /// </summary>
        internal static string FormatSelectOptionAnnouncement(string optionText, int index)
        {
            // Only add read status for talk menus (where setRead was called)
            if (!IsTalkMenu)
            {
                return optionText;
            }

            // Check if we have read status for this index
            bool isRead = index < SelectOptionsRead.Count && SelectOptionsRead[index];
            bool hasPsyLock = index < SelectOptionsPsyLock.Count && SelectOptionsPsyLock[index];

            if (hasPsyLock)
            {
                // Topic has a psyche-lock (secret that needs to be unlocked)
                return optionText + " " + L.Get("psyche_lock.topic_locked");
            }
            else if (isRead)
            {
                // Topic has been discussed (checkmark)
                return optionText + " " + L.Get("menu.topic_discussed");
            }

            return optionText;
        }

        #endregion
    }
}
