using System;
using System.Collections.Generic;
using System.Reflection;
using AccessibilityMod.Core;
using AccessibilityMod.Services;
using HarmonyLib;
using UnityAccessibilityLib;
using UnityEngine.UI;

namespace AccessibilityMod.Patches
{
    [HarmonyPatch]
    public static class SaveLoadPatches
    {
        private static int _lastSlotCursor = -1;
        private static int _lastSaveOptionCursor = -1;

        // Hook messageBoxCtrl.OpenWindow to announce generic message boxes
        [HarmonyPostfix]
        [HarmonyPatch(typeof(messageBoxCtrl), "OpenWindow")]
        public static void MessageBox_OpenWindow_Postfix(messageBoxCtrl __instance)
        {
            try
            {
                string message = GetMessageBoxText(__instance);
                if (!Net35Extensions.IsNullOrWhiteSpace(message))
                {
                    SpeechManager.Announce(message, GameTextType.Menu);
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in MessageBox OpenWindow patch: {ex.Message}"
                );
            }
        }

        private static string GetMessageBoxText(messageBoxCtrl instance)
        {
            try
            {
                var textListField = typeof(messageBoxCtrl).GetField(
                    "text_list_",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );
                if (textListField == null)
                    return null;

                var textList = textListField.GetValue(instance) as Text[];
                if (textList == null)
                    return null;

                var parts = new List<string>();
                foreach (var text in textList)
                {
                    if (text != null && !Net35Extensions.IsNullOrWhiteSpace(text.text))
                    {
                        parts.Add(text.text);
                    }
                }

                return parts.Count > 0 ? string.Join(" ", parts.ToArray()) : null;
            }
            catch
            {
                return null;
            }
        }

        // Hook SavePriorConfirmation.OpenConfirmation to announce pre-save dialogs
        [HarmonyPostfix]
        [HarmonyPatch(typeof(SavePriorConfirmation), "OpenConfirmation")]
        public static void SavePriorConfirmation_OpenConfirmation_Postfix(
            SavePriorConfirmation __instance
        )
        {
            try
            {
                string message = GetSavePriorConfirmationText(__instance);
                if (!Net35Extensions.IsNullOrWhiteSpace(message))
                {
                    SpeechManager.Announce(message, GameTextType.Menu);
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in SavePriorConfirmation patch: {ex.Message}"
                );
            }
        }

        private static string GetSavePriorConfirmationText(SavePriorConfirmation instance)
        {
            try
            {
                var textField = typeof(SavePriorConfirmation).GetField(
                    "confirmation_text_",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );
                if (textField == null)
                    return null;

                var textArray = textField.GetValue(instance) as Text[];
                if (textArray == null)
                    return null;

                var parts = new List<string>();
                foreach (var text in textArray)
                {
                    if (text != null && !Net35Extensions.IsNullOrWhiteSpace(text.text))
                    {
                        parts.Add(text.text);
                    }
                }

                return parts.Count > 0 ? string.Join(" ", parts.ToArray()) : null;
            }
            catch
            {
                return null;
            }
        }

        // Hook episodeReleaseCtrl.play to announce episode unlock messages
        [HarmonyPostfix]
        [HarmonyPatch(typeof(episodeReleaseCtrl), "play")]
        public static void EpisodeRelease_Play_Postfix()
        {
            try
            {
                string message = TextDataCtrl.GetText(TextDataCtrl.SaveTextID.ADD_NEW_EPISODE);
                if (!Net35Extensions.IsNullOrWhiteSpace(message))
                {
                    SpeechManager.Announce(message, GameTextType.Dialogue);
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in EpisodeRelease play patch: {ex.Message}"
                );
            }
        }

        // Hook optionSave cursor changes (Save/Load selection in Options menu)
        [HarmonyPostfix]
        [HarmonyPatch(typeof(optionSave), "ChangeValue")]
        public static void OptionSave_ChangeValue_Postfix(optionSave __instance)
        {
            try
            {
                int cursorNum = GetOptionSaveCursor(__instance);
                if (cursorNum != _lastSaveOptionCursor && cursorNum >= 0)
                {
                    _lastSaveOptionCursor = cursorNum;
                    string optionText = GetOptionSaveText(__instance, cursorNum);
                    if (!Net35Extensions.IsNullOrWhiteSpace(optionText))
                    {
                        SpeechManager.Announce(optionText, GameTextType.Menu);
                    }
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in OptionSave ChangeValue patch: {ex.Message}"
                );
            }
        }

        // Also announce when first entering the Save/Load option
        [HarmonyPostfix]
        [HarmonyPatch(typeof(optionSave), "SelectEntry")]
        public static void OptionSave_SelectEntry_Postfix(optionSave __instance)
        {
            try
            {
                _lastSaveOptionCursor = 0; // Reset to Save (index 0)
                string optionText = GetOptionSaveText(__instance, 0);
                if (!Net35Extensions.IsNullOrWhiteSpace(optionText))
                {
                    SpeechManager.Announce(optionText, GameTextType.Menu);
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in OptionSave SelectEntry patch: {ex.Message}"
                );
            }
        }

        private static int GetOptionSaveCursor(optionSave instance)
        {
            try
            {
                var field = typeof(optionSave).GetField(
                    "cursor_num_",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );
                if (field != null)
                {
                    return (int)field.GetValue(instance);
                }
            }
            catch { }
            return -1;
        }

        private static string GetOptionSaveText(optionSave instance, int cursorNum)
        {
            try
            {
                var field = typeof(optionSave).GetField(
                    "select_plate_",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );
                if (field == null)
                    return null;

                var selectPlate = field.GetValue(instance) as System.Collections.IList;
                if (selectPlate == null || cursorNum < 0 || cursorNum >= selectPlate.Count)
                    return null;

                var item = selectPlate[cursorNum];
                var textField = item.GetType().GetField("text_");
                if (textField == null)
                    return null;

                var textComponent = textField.GetValue(item) as Text;
                return textComponent?.text;
            }
            catch
            {
                return null;
            }
        }

        // Reset tracking before save/load UI opens so cursor position is announced
        [HarmonyPrefix]
        [HarmonyPatch(typeof(SaveLoadUICtrl), "Open")]
        public static void Open_Prefix()
        {
            _lastSlotCursor = -1;
        }

        // Hook when save/load UI opens
        [HarmonyPostfix]
        [HarmonyPatch(typeof(SaveLoadUICtrl), "Open")]
        public static void Open_Postfix(SaveLoadUICtrl __instance)
        {
            try
            {
                var slotType = GetSlotType(__instance);
                string typeName = slotType == 0 ? L.Get("save_load.save") : L.Get("save_load.load");

                SpeechManager.Announce(L.Get("save_load.menu_opened", typeName), GameTextType.Menu);
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in SaveLoad Open patch: {ex.Message}"
                );
            }
        }

        // Hook save/load confirmation window to announce message
        [HarmonyPostfix]
        [HarmonyPatch(typeof(SaveConfirmationWindow), "OpenWindow")]
        public static void SaveConfirmation_OpenWindow_Postfix(
            SaveConfirmationWindow __instance,
            bool is_confirmation
        )
        {
            try
            {
                // Only announce if showing confirmation dialog (not direct save/load)
                if (!is_confirmation)
                    return;

                string message = GetSaveConfirmationMessage(__instance);
                if (!Net35Extensions.IsNullOrWhiteSpace(message))
                {
                    SpeechManager.Announce(message, GameTextType.Menu);
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in SaveConfirmation OpenWindow patch: {ex.Message}"
                );
            }
        }

        private static string GetSaveConfirmationMessage(SaveConfirmationWindow instance)
        {
            try
            {
                // Get the window_ field
                var windowField = typeof(SaveConfirmationWindow).GetField(
                    "window_",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );
                if (windowField == null)
                    return null;

                var window = windowField.GetValue(instance);
                if (window == null)
                    return null;

                // Get the confirmation_text_ field from the window
                var textField = window.GetType().GetField("confirmation_text_");
                if (textField == null)
                    return null;

                var textComponent = textField.GetValue(window) as Text;
                return textComponent?.text;
            }
            catch
            {
                return null;
            }
        }

        // Hook cursor changes in save/load via UpdateCursorPosition
        [HarmonyPostfix]
        [HarmonyPatch(typeof(SaveLoadUICtrl), "UpdateCursorPosition")]
        public static void UpdateCursorPosition_Postfix(SaveLoadUICtrl __instance)
        {
            try
            {
                int currentSlot = GetCurrentSlot(__instance);
                if (currentSlot != _lastSlotCursor && currentSlot >= 0)
                {
                    _lastSlotCursor = currentSlot;
                    AnnounceSlot(__instance, currentSlot);
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in SaveLoad UpdateCursorPosition patch: {ex.Message}"
                );
            }
        }

        private static int GetCurrentSlot(SaveLoadUICtrl ctrl)
        {
            try
            {
                var field = typeof(SaveLoadUICtrl).GetField(
                    "select_num_",
                    System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Instance
                );
                if (field != null)
                {
                    return (int)field.GetValue(ctrl);
                }
            }
            catch { }
            return -1;
        }

        private static void AnnounceSlot(SaveLoadUICtrl ctrl, int slotNo)
        {
            try
            {
                // Check if slot has data
                bool hasData = GSStatic.save_data[slotNo].in_data > 0;

                if (!hasData)
                {
                    SpeechManager.Announce(
                        L.Get("save_load.slot_no_data", slotNo + 1),
                        GameTextType.Menu
                    );
                    return;
                }

                // Try to get detailed save data via reflection
                string slotInfo = L.Get("save_load.slot_number", slotNo + 1);
                try
                {
                    var slotListField = typeof(SaveLoadUICtrl).GetField(
                        "slot_list_",
                        System.Reflection.BindingFlags.NonPublic
                            | System.Reflection.BindingFlags.Instance
                    );

                    if (slotListField != null)
                    {
                        var slotArray = slotListField.GetValue(ctrl) as SaveSlot[];
                        if (slotArray != null && slotNo >= 0 && slotNo < slotArray.Length)
                        {
                            var saveSlot = slotArray[slotNo];

                            // Access the slot_ property which contains the display data
                            var slotData = saveSlot.slot;
                            if (slotData != null)
                            {
                                // Build comprehensive announcement
                                var parts = new System.Collections.Generic.List<string>();
                                parts.Add(L.Get("save_load.slot_number", slotNo + 1));

                                // Get time/date
                                if (
                                    slotData.time_ != null
                                    && !string.IsNullOrEmpty(slotData.time_.text)
                                )
                                {
                                    // Remove newlines from date/time (game stores as "date\ntime")
                                    string timeText = slotData.time_.text.Replace("\n", " ");
                                    parts.Add(timeText);
                                }

                                // Get game title
                                if (
                                    slotData.title_ != null
                                    && !string.IsNullOrEmpty(slotData.title_.text)
                                )
                                {
                                    parts.Add(slotData.title_.text);
                                }

                                // Get scenario/episode
                                if (
                                    slotData.scenario_ != null
                                    && !string.IsNullOrEmpty(slotData.scenario_.text)
                                )
                                {
                                    // Replace full-width space with regular space
                                    string scenarioText = slotData.scenario_.text.Replace(
                                        "\u3000",
                                        " "
                                    );
                                    parts.Add(scenarioText);
                                }

                                // Get progress/day
                                if (
                                    slotData.progress != null
                                    && !string.IsNullOrEmpty(slotData.progress.text)
                                )
                                {
                                    parts.Add(slotData.progress.text);
                                }

                                slotInfo = string.Join(", ", parts.ToArray());
                            }
                        }
                    }
                }
                catch (Exception innerEx)
                {
                    AccessibilityMod.Core.AccessibilityMod.Logger?.Warning(
                        $"Could not read detailed slot data: {innerEx.Message}"
                    );
                }

                SpeechManager.Announce(slotInfo, GameTextType.Menu);
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error announcing slot: {ex.Message}"
                );
            }
        }

        private static int GetSlotType(SaveLoadUICtrl ctrl)
        {
            try
            {
                var field = typeof(SaveLoadUICtrl).GetField(
                    "slot_type_",
                    System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Instance
                );
                if (field != null)
                {
                    return (int)field.GetValue(ctrl);
                }
            }
            catch { }
            return 0;
        }
    }

    [HarmonyPatch]
    public static class OptionPatches
    {
        private static int _lastCategory = -1;
        private static int _lastOptionIndex = -1;

        // Reflection cache
        private static FieldInfo _currentNumField;
        private static FieldInfo _availableOptionField;
        private static FieldInfo _optionTitleField;

        private static readonly BindingFlags NonPublicInstance =
            BindingFlags.NonPublic | BindingFlags.Instance;

        // Tooltip delay in seconds
        private const float TooltipDelay = 2.0f;

        // Reset tracking before options menu opens so category is announced
        [HarmonyPrefix]
        [HarmonyPatch(typeof(optionCtrl), "Open")]
        public static void OptionCtrl_Open_Prefix()
        {
            _lastCategory = -1;
            _lastOptionIndex = -1;
        }

        // Hook when options menu category changes
        [HarmonyPostfix]
        [HarmonyPatch(typeof(optionCtrl), "ChangeCategory")]
        public static void ChangeCategory_Postfix(optionCtrl __instance, optionCtrl.Category cat)
        {
            try
            {
                int categoryInt = (int)cat;
                if (categoryInt != _lastCategory)
                {
                    _lastCategory = categoryInt;
                    _lastOptionIndex = -1; // Reset option index when category changes

                    // Cancel any pending tooltip from previous category/option
                    CoroutineRunner.Instance?.CancelDelayedAnnouncement();

                    string categoryName = GetCategoryName(cat);
                    SpeechManager.Announce(
                        L.Get("save_load.options_category", categoryName),
                        GameTextType.Menu
                    );

                    // Schedule delayed tooltip announcement for category
                    ScheduleCategoryTooltipAnnouncement(__instance, cat);
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in Options ChangeCategory patch: {ex.Message}"
                );
            }
        }

        // Hook when navigating between options within a category
        [HarmonyPostfix]
        [HarmonyPatch(typeof(optionCtrl), "SelectItemSet")]
        public static void SelectItemSet_Postfix(optionCtrl __instance)
        {
            try
            {
                int currentIndex = GetCurrentOptionIndex(__instance);
                if (currentIndex == _lastOptionIndex || currentIndex < 0)
                    return;

                _lastOptionIndex = currentIndex;

                var options = GetAvailableOptions(__instance);
                if (options == null || currentIndex >= options.Count)
                    return;

                var currentOption = options[currentIndex];
                string optionName = GetOptionName(currentOption);
                string currentValue = GetOptionValue(currentOption);

                string message;
                if (!Net35Extensions.IsNullOrWhiteSpace(currentValue))
                    message = $"{optionName}: {currentValue}";
                else
                    message = optionName;

                SpeechManager.Announce(message, GameTextType.Menu);

                // Schedule delayed tooltip announcement
                ScheduleTooltipAnnouncement(__instance);
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in Options SelectItemSet patch: {ex.Message}"
                );
            }
        }

        private static void ScheduleTooltipAnnouncement(optionCtrl instance)
        {
            try
            {
                if (CoroutineRunner.Instance == null)
                    return;

                CoroutineRunner.Instance.ScheduleDelayedAnnouncement(
                    TooltipDelay,
                    () => GetTooltipText(instance),
                    GameTextType.Menu
                );
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error scheduling tooltip: {ex.Message}"
                );
            }
        }

        private static void ScheduleCategoryTooltipAnnouncement(
            optionCtrl instance,
            optionCtrl.Category category
        )
        {
            try
            {
                if (CoroutineRunner.Instance == null)
                    return;

                CoroutineRunner.Instance.ScheduleDelayedAnnouncement(
                    TooltipDelay,
                    () => GetCategoryTooltipText(instance, category),
                    GameTextType.Menu
                );
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error scheduling category tooltip: {ex.Message}"
                );
            }
        }

        private static string GetTooltipText(optionCtrl instance)
        {
            try
            {
                // Get current option index to determine which tooltip to read
                int currentIndex = GetCurrentOptionIndex(instance);
                if (currentIndex < 0)
                    return null;

                // Check if the current option is optionStory (has special warning text)
                var options = GetAvailableOptions(instance);
                if (options != null && currentIndex < options.Count)
                {
                    var currentOption = options[currentIndex];
                    string storyWarning = GetStoryModeWarningText(currentOption);
                    if (!Net35Extensions.IsNullOrWhiteSpace(storyWarning))
                        return storyWarning;
                }

                // Get begin_num_ to calculate actual OptionItem
                var beginNumField = typeof(optionCtrl).GetField("begin_num_", NonPublicInstance);
                if (beginNumField == null)
                    return null;

                int beginNum = (int)beginNumField.GetValue(instance);
                int optionItemIndex = currentIndex + beginNum;

                // Get the tooltip text ID from option_description_ array
                var optionDescField = typeof(optionCtrl).GetField(
                    "option_description_",
                    NonPublicInstance
                );
                if (optionDescField == null)
                    return null;

                var optionDescArray =
                    optionDescField.GetValue(instance) as TextDataCtrl.OptionTextID[];
                if (optionDescArray == null || optionItemIndex >= optionDescArray.Length)
                    return null;

                TextDataCtrl.OptionTextID textId = optionDescArray[optionItemIndex];

                // Get ALL lines of tooltip text using GetTexts
                string[] allLines = TextDataCtrl.GetTexts(textId);
                if (allLines == null || allLines.Length == 0)
                    return null;

                // Get the current key type for button prompts
                string keyName = GetCurrentKeyName(instance);

                // Join all lines and replace button placeholders
                var parts = new List<string>();
                foreach (string line in allLines)
                {
                    if (!Net35Extensions.IsNullOrWhiteSpace(line))
                    {
                        string processedLine = line;
                        if (!Net35Extensions.IsNullOrWhiteSpace(keyName))
                        {
                            processedLine = ReplaceButtonPlaceholder(processedLine, keyName);
                        }
                        parts.Add(processedLine);
                    }
                }

                return parts.Count > 0 ? string.Join(" ", parts.ToArray()) : null;
            }
            catch
            {
                return null;
            }
        }

        // optionStory has special warning_text that contains the detailed Story Mode description
        private static string GetStoryModeWarningText(optionItem option)
        {
            try
            {
                // Check if this is an optionStory instance
                if (option == null || option.GetType().Name != "optionStory")
                    return null;

                // Get the warning_text field
                var warningTextField = option
                    .GetType()
                    .GetField("warning_text", BindingFlags.NonPublic | BindingFlags.Instance);
                if (warningTextField == null)
                    return null;

                var warningTextList = warningTextField.GetValue(option) as List<Text>;
                if (warningTextList == null || warningTextList.Count == 0)
                    return null;

                // Collect all non-empty warning text lines
                var parts = new List<string>();
                foreach (var textComponent in warningTextList)
                {
                    if (
                        textComponent != null
                        && !Net35Extensions.IsNullOrWhiteSpace(textComponent.text)
                    )
                    {
                        parts.Add(textComponent.text);
                    }
                }

                return parts.Count > 0 ? string.Join(" ", parts.ToArray()) : null;
            }
            catch
            {
                return null;
            }
        }

        private static string GetCategoryTooltipText(
            optionCtrl instance,
            optionCtrl.Category category
        )
        {
            try
            {
                // Get the tooltip text ID from option_category_description_ array
                var catDescField = typeof(optionCtrl).GetField(
                    "option_category_description_",
                    NonPublicInstance
                );
                if (catDescField == null)
                    return null;

                var catDescArray = catDescField.GetValue(instance) as TextDataCtrl.OptionTextID[];
                if (catDescArray == null || (int)category >= catDescArray.Length)
                    return null;

                TextDataCtrl.OptionTextID textId = catDescArray[(int)category];

                // Get ALL lines of tooltip text using GetTexts
                string[] allLines = TextDataCtrl.GetTexts(textId);
                if (allLines == null || allLines.Length == 0)
                    return null;

                // Get the key type for button prompts based on category
                string keyName = GetCategoryKeyName(category);

                // Join all lines and replace button placeholders
                var parts = new List<string>();
                foreach (string line in allLines)
                {
                    if (!Net35Extensions.IsNullOrWhiteSpace(line))
                    {
                        string processedLine = line;
                        if (!Net35Extensions.IsNullOrWhiteSpace(keyName))
                        {
                            processedLine = ReplaceButtonPlaceholder(processedLine, keyName);
                        }
                        parts.Add(processedLine);
                    }
                }

                return parts.Count > 0 ? string.Join(" ", parts.ToArray()) : null;
            }
            catch
            {
                return null;
            }
        }

        private static string GetCategoryKeyName(optionCtrl.Category category)
        {
            try
            {
                // Based on game's keyIconSet(Category category) method:
                // Only KEYCONFIG and LANGUAGE categories have button prompts (A button)
                KeyType keyType;
                switch (category)
                {
                    case optionCtrl.Category.KEYCONFIG:
                    case optionCtrl.Category.LANGUAGE:
                        keyType = KeyType.A;
                        break;
                    default:
                        return null;
                }

                return GetKeyTypeName(keyType);
            }
            catch
            {
                return null;
            }
        }

        private static string ReplaceButtonPlaceholder(string text, string keyName)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // First check if the original placeholder is still there (【】)
            if (text.Contains("【】"))
            {
                if (!string.IsNullOrEmpty(keyName))
                    text = text.Replace("【】", $"[{keyName}]");
                else
                    text = text.Replace("【】", "");
                return text;
            }

            // The game replaces 【】 with multiple spaces to make room for an icon
            // Full-width space: \u3000, used 3 times for CJK languages
            // Half-width spaces used 9 times for other languages

            if (string.IsNullOrEmpty(keyName))
                return text;

            // Replace sequences of full-width ideographic spaces (\u3000)
            if (text.IndexOf('\u3000') >= 0)
            {
                text = System.Text.RegularExpressions.Regex.Replace(
                    text,
                    "[\u3000]+",
                    $" [{keyName}] "
                );
            }

            // Replace sequences of 3 or more regular spaces
            // Pattern: look for 3+ consecutive spaces
            text = System.Text.RegularExpressions.Regex.Replace(text, " {3,}", $" [{keyName}] ");

            return text;
        }

        private static string GetCurrentKeyName(optionCtrl instance)
        {
            try
            {
                // Get current option index
                int currentIndex = GetCurrentOptionIndex(instance);
                if (currentIndex < 0)
                    return null;

                // Get begin_num_ to calculate actual OptionItem
                var beginNumField = typeof(optionCtrl).GetField("begin_num_", NonPublicInstance);
                if (beginNumField == null)
                    return null;

                int beginNum = (int)beginNumField.GetValue(instance);
                int optionItemIndex = currentIndex + beginNum;

                // Get KeyType for this option based on game's mapping
                KeyType keyType = GetKeyTypeForOption(optionItemIndex);
                if (keyType == KeyType.None)
                    return null;

                return GetKeyTypeName(keyType);
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"GetCurrentKeyName error: {ex.Message}"
                );
                return null;
            }
        }

        // Maps OptionItem index to KeyType based on game's keyIconSet logic
        private static KeyType GetKeyTypeForOption(int optionItemIndex)
        {
            // OptionItem enum values:
            // SKIP = 3, LANGUAGE = 7, VOICE_LANG = 8
            switch (optionItemIndex)
            {
                case 3: // SKIP - has B button prompt
                    return KeyType.B;

                case 7: // LANGUAGE - has A button prompt
                case 8: // VOICE_LANG - has A button prompt
                    return KeyType.A;

                // Most options don't have button prompts
                default:
                    return KeyType.None;
            }
        }

        private static string GetKeyTypeName(KeyType keyType)
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

        private static string GetKeyCodeName(UnityEngine.KeyCode keyCode)
        {
            switch (keyCode)
            {
                case UnityEngine.KeyCode.Space:
                    return "Space";
                case UnityEngine.KeyCode.Return:
                    return "Enter";
                case UnityEngine.KeyCode.Escape:
                    return "Escape";
                case UnityEngine.KeyCode.Tab:
                    return "Tab";
                case UnityEngine.KeyCode.Backspace:
                    return "Backspace";
                case UnityEngine.KeyCode.Delete:
                    return "Delete";
                case UnityEngine.KeyCode.Insert:
                    return "Insert";
                case UnityEngine.KeyCode.Home:
                    return "Home";
                case UnityEngine.KeyCode.End:
                    return "End";
                case UnityEngine.KeyCode.PageUp:
                    return "Page Up";
                case UnityEngine.KeyCode.PageDown:
                    return "Page Down";
                case UnityEngine.KeyCode.UpArrow:
                    return "Up Arrow";
                case UnityEngine.KeyCode.DownArrow:
                    return "Down Arrow";
                case UnityEngine.KeyCode.LeftArrow:
                    return "Left Arrow";
                case UnityEngine.KeyCode.RightArrow:
                    return "Right Arrow";
                case UnityEngine.KeyCode.LeftShift:
                    return "Left Shift";
                case UnityEngine.KeyCode.RightShift:
                    return "Right Shift";
                case UnityEngine.KeyCode.LeftControl:
                    return "Left Ctrl";
                case UnityEngine.KeyCode.RightControl:
                    return "Right Ctrl";
                case UnityEngine.KeyCode.LeftAlt:
                    return "Left Alt";
                case UnityEngine.KeyCode.RightAlt:
                    return "Right Alt";
                default:
                    // For letters and numbers, just return the key name
                    string name = keyCode.ToString();
                    // Handle "Alpha1" -> "1", etc.
                    if (name.StartsWith("Alpha"))
                        return name.Substring(5);
                    // Handle "Keypad1" -> "Numpad 1", etc.
                    if (name.StartsWith("Keypad"))
                        return "Numpad " + name.Substring(6);
                    return name;
            }
        }

        // Hook value changes for gauge-type options (BGM, SE)
        [HarmonyPostfix]
        [HarmonyPatch(typeof(optionBgm), "ChangeValue")]
        public static void OptionBgm_ChangeValue_Postfix(optionItem __instance)
        {
            AnnounceOptionValue(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(optionSe), "ChangeValue")]
        public static void OptionSe_ChangeValue_Postfix(optionItem __instance)
        {
            AnnounceOptionValue(__instance);
        }

        // Hook value changes for toggle-type options
        [HarmonyPostfix]
        [HarmonyPatch(typeof(optionToggleItem), "ChangeValue")]
        public static void OptionToggle_ChangeValue_Postfix(optionItem __instance)
        {
            AnnounceOptionValue(__instance);
        }

        // Hook value changes for skip option
        [HarmonyPostfix]
        [HarmonyPatch(typeof(optionSkip), "ChangeValue")]
        public static void OptionSkip_ChangeValue_Postfix(optionItem __instance)
        {
            AnnounceOptionValue(__instance);
        }

        // Hook value changes for window transparency option
        [HarmonyPostfix]
        [HarmonyPatch(typeof(optionWindow), "ChangeValue")]
        public static void OptionWindow_ChangeValue_Postfix(optionItem __instance)
        {
            AnnounceOptionValue(__instance);
        }

        // Hook value changes for vibration option (extends optionItem directly)
        [HarmonyPostfix]
        [HarmonyPatch(typeof(optionVibration), "ChangeValue")]
        public static void OptionVibration_ChangeValue_Postfix(optionItem __instance)
        {
            AnnounceOptionValue(__instance);
        }

        // Hook value changes for select-type options (language, resolution, window mode, auto-speed)
        [HarmonyPostfix]
        [HarmonyPatch(typeof(optionSelectItem), "ChangeValue")]
        public static void OptionSelect_ChangeValue_Postfix(optionItem __instance)
        {
            AnnounceOptionValue(__instance);
        }

        private static void AnnounceOptionValue(optionItem item)
        {
            try
            {
                string value = GetOptionValue(item);
                if (!Net35Extensions.IsNullOrWhiteSpace(value))
                {
                    SpeechManager.Announce(value, GameTextType.Menu);
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error announcing option value: {ex.Message}"
                );
            }
        }

        private static int GetCurrentOptionIndex(optionCtrl ctrl)
        {
            try
            {
                if (_currentNumField == null)
                    _currentNumField = typeof(optionCtrl).GetField(
                        "current_num_",
                        NonPublicInstance
                    );

                if (_currentNumField != null)
                    return (int)_currentNumField.GetValue(ctrl);
            }
            catch { }
            return -1;
        }

        private static List<optionItem> GetAvailableOptions(optionCtrl ctrl)
        {
            try
            {
                if (_availableOptionField == null)
                    _availableOptionField = typeof(optionCtrl).GetField(
                        "available_option_",
                        NonPublicInstance
                    );

                if (_availableOptionField != null)
                    return _availableOptionField.GetValue(ctrl) as List<optionItem>;
            }
            catch { }
            return null;
        }

        private static string GetOptionName(optionItem item)
        {
            try
            {
                if (_optionTitleField == null)
                    _optionTitleField = typeof(optionItem).GetField(
                        "option_title_",
                        NonPublicInstance
                    );

                if (_optionTitleField != null)
                {
                    var titleText = _optionTitleField.GetValue(item) as Text;
                    if (titleText != null && !Net35Extensions.IsNullOrWhiteSpace(titleText.text))
                        return titleText.text;
                }
            }
            catch { }
            return L.Get("save_load.unknown_option");
        }

        private static string GetOptionValue(optionItem item)
        {
            try
            {
                Type itemType = item.GetType();

                // Try gauge type pattern first (optionBgm, optionSe)
                // These have gauge_ field with count_text_.text
                var gaugeField = FindField(itemType, "gauge_");
                if (gaugeField != null)
                {
                    var gauge = gaugeField.GetValue(item);
                    if (gauge != null)
                    {
                        var countTextField = gauge.GetType().GetField("count_text_");
                        if (countTextField != null)
                        {
                            var countText = countTextField.GetValue(gauge) as Text;
                            if (
                                countText != null
                                && !Net35Extensions.IsNullOrWhiteSpace(countText.text)
                            )
                            {
                                return countText.text;
                            }
                        }
                    }
                }

                // Try toggle/select type pattern (optionToggleItem, optionSkip, optionShake, optionWindow, etc.)
                // These have select_text_[] array and setting_value_ index
                var selectTextField = FindField(itemType, "select_text_");
                var settingValueField = FindField(itemType, "setting_value_");

                if (selectTextField != null && settingValueField != null)
                {
                    var selectTexts = selectTextField.GetValue(item) as string[];
                    int settingValue = (int)settingValueField.GetValue(item);
                    if (
                        selectTexts != null
                        && settingValue >= 0
                        && settingValue < selectTexts.Length
                    )
                    {
                        return selectTexts[settingValue];
                    }
                }

                // Fallback: try to get setting_value_ as a number
                if (settingValueField != null)
                {
                    int settingValue = (int)settingValueField.GetValue(item);
                    return settingValue.ToString();
                }
            }
            catch { }
            return null;
        }

        // Helper to find a field in a type or its base types
        private static FieldInfo FindField(Type type, string fieldName)
        {
            while (type != null && type != typeof(object))
            {
                var field = type.GetField(
                    fieldName,
                    BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance
                );
                if (field != null)
                    return field;
                type = type.BaseType;
            }
            return null;
        }

        private static string GetCategoryName(optionCtrl.Category category)
        {
            switch (category)
            {
                case optionCtrl.Category.SAVE_LOAD:
                    return L.Get("save_load.category_save_load");
                case optionCtrl.Category.SOUND:
                    return L.Get("save_load.category_sound");
                case optionCtrl.Category.GAME:
                    return L.Get("save_load.category_game");
                case optionCtrl.Category.LANGUAGE:
                    return L.Get("save_load.category_language");
                case optionCtrl.Category.PC:
                    return L.Get("save_load.category_display");
                case optionCtrl.Category.KEYCONFIG:
                    return L.Get("save_load.category_keyconfig");
                case optionCtrl.Category.STORY:
                    return L.Get("save_load.category_story");
                case optionCtrl.Category.CREDIT:
                    return L.Get("save_load.category_credits");
                case optionCtrl.Category.PRIVACY:
                    return L.Get("save_load.category_privacy");
                default:
                    return category.ToString();
            }
        }
    }
}
