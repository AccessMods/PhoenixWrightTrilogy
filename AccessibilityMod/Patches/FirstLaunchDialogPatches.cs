using System;
using System.Collections.Generic;
using System.Reflection;
using AccessibilityMod.Core;
using AccessibilityMod.Services;
using HarmonyLib;
using UnityAccessibilityLib;
using UnityEngine;
using UnityEngine.UI;

namespace AccessibilityMod.Patches
{
    /// <summary>
    /// Patches for first-launch dialogs: Terms of Service / Privacy Policy and Release Notes.
    /// These dialogs appear when a new player launches the game for the first time.
    /// </summary>
    [HarmonyPatch]
    public static class FirstLaunchDialogPatches
    {
        // Track the active Terms of Service dialog instance for cursor monitoring
        private static DialogTermsOfServiceCtrl _termsInstance;
        private static int _lastTermsCursor = -1;

        // Reflection cache
        private static FieldInfo _cursorNoField;
        private static FieldInfo _selectListField;
        private static FieldInfo _messageTextField;
        private static FieldInfo _releaseNoteMessageField;

        private static readonly BindingFlags NonPublicInstance =
            BindingFlags.NonPublic | BindingFlags.Instance;

        /// <summary>
        /// Called from AccessibilityMod.OnUpdate() to check for cursor changes in the Terms dialog.
        /// </summary>
        public static void Update()
        {
            if (_termsInstance == null)
                return;

            try
            {
                // Check if the instance is still valid (not destroyed)
                if (_termsInstance.gameObject == null)
                {
                    _termsInstance = null;
                    _lastTermsCursor = -1;
                    return;
                }

                int currentCursor = GetTermsCursor();
                if (currentCursor != _lastTermsCursor && currentCursor >= 0)
                {
                    _lastTermsCursor = currentCursor;
                    AnnounceTermsOption(currentCursor);
                }
            }
            catch (Exception)
            {
                // Instance was destroyed
                _termsInstance = null;
                _lastTermsCursor = -1;
            }
        }

        #region DialogTermsOfServiceCtrl Patches

        /// <summary>
        /// Hook DialogTermsOfServiceCtrl.Initialize to announce when the dialog opens
        /// and start tracking cursor changes.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(DialogTermsOfServiceCtrl), "Initialize")]
        public static void TermsOfService_Initialize_Postfix(DialogTermsOfServiceCtrl __instance)
        {
            try
            {
                // Store reference for cursor tracking
                _termsInstance = __instance;
                _lastTermsCursor = -1;

                // Get the message content
                string messageContent = GetTermsMessageContent(__instance);

                // Announce dialog title and content
                string announcement = L.Get("first_launch.terms_dialog_opened");
                if (!Net35Extensions.IsNullOrWhiteSpace(messageContent))
                {
                    announcement += ". " + messageContent;
                }
                SpeechManager.Announce(announcement, GameTextType.Menu);

                // Announce the default cursor position (starts at 1 = Privacy Policy / Detail)
                // Use delayed announcement so it comes after the dialog opened message
                CoroutineRunner.Instance?.ScheduleDelayedAnnouncement(
                    0.5f,
                    () =>
                    {
                        int cursor = GetTermsCursor();
                        if (cursor >= 0)
                        {
                            _lastTermsCursor = cursor;
                            return GetTermsOptionText(cursor);
                        }
                        return null;
                    },
                    GameTextType.Menu
                );
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in TermsOfService Initialize patch: {ex.Message}"
                );
            }
        }

        private static string GetTermsMessageContent(DialogTermsOfServiceCtrl instance)
        {
            try
            {
                if (_messageTextField == null)
                    _messageTextField = typeof(DialogTermsOfServiceCtrl).GetField(
                        "message_text_",
                        NonPublicInstance
                    );

                if (_messageTextField == null)
                    return null;

                var messageText = _messageTextField.GetValue(instance) as Text;
                if (messageText != null && !Net35Extensions.IsNullOrWhiteSpace(messageText.text))
                {
                    return messageText.text;
                }
            }
            catch { }
            return null;
        }

        private static int GetTermsCursor()
        {
            if (_termsInstance == null)
                return -1;

            try
            {
                if (_cursorNoField == null)
                    _cursorNoField = typeof(DialogTermsOfServiceCtrl).GetField(
                        "cursor_no_",
                        NonPublicInstance
                    );

                if (_cursorNoField != null)
                    return (int)_cursorNoField.GetValue(_termsInstance);
            }
            catch { }
            return -1;
        }

        private static string GetTermsOptionText(int cursorNo)
        {
            if (_termsInstance == null)
                return null;

            try
            {
                if (_selectListField == null)
                    _selectListField = typeof(DialogTermsOfServiceCtrl).GetField(
                        "select_list_",
                        NonPublicInstance
                    );

                if (_selectListField == null)
                    return null;

                var selectList =
                    _selectListField.GetValue(_termsInstance)
                    as List<optionSelectPlate.SelectPlate>;
                if (selectList == null || cursorNo < 0 || cursorNo >= selectList.Count)
                    return null;

                var selectPlate = selectList[cursorNo];
                if (selectPlate.text_ != null && !string.IsNullOrEmpty(selectPlate.text_.text))
                {
                    return selectPlate.text_.text;
                }
            }
            catch { }
            return null;
        }

        private static void AnnounceTermsOption(int cursorNo)
        {
            string optionText = GetTermsOptionText(cursorNo);
            if (!Net35Extensions.IsNullOrWhiteSpace(optionText))
            {
                SpeechManager.Announce(optionText, GameTextType.Menu);
            }
        }

        #endregion

        #region DialogReleaseNoteCtrl Patches

        /// <summary>
        /// Hook DialogReleaseNoteCtrl.Awake to announce when the release notes dialog opens.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(DialogReleaseNoteCtrl), "Awake")]
        public static void ReleaseNote_Awake_Postfix(DialogReleaseNoteCtrl __instance)
        {
            try
            {
                // Get the message content
                string messageContent = GetReleaseNoteContent(__instance);

                // Announce dialog title and content
                string announcement = L.Get("first_launch.release_notes_opened");
                if (!Net35Extensions.IsNullOrWhiteSpace(messageContent))
                {
                    announcement += ". " + messageContent;
                }
                SpeechManager.Announce(announcement, GameTextType.Menu);
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in ReleaseNote Awake patch: {ex.Message}"
                );
            }
        }

        private static string GetReleaseNoteContent(DialogReleaseNoteCtrl instance)
        {
            try
            {
                if (_releaseNoteMessageField == null)
                    _releaseNoteMessageField = typeof(DialogReleaseNoteCtrl).GetField(
                        "m_MessageText",
                        NonPublicInstance
                    );

                if (_releaseNoteMessageField == null)
                    return null;

                var messageText = _releaseNoteMessageField.GetValue(instance) as Text;
                if (messageText != null && !Net35Extensions.IsNullOrWhiteSpace(messageText.text))
                {
                    return messageText.text;
                }
            }
            catch { }
            return null;
        }

        #endregion
    }
}
