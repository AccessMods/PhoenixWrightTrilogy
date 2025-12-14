using System;
using System.Collections;
using AccessibilityMod.Patches;
using UnityEngine;

namespace AccessibilityMod.Core
{
    public class CoroutineRunner : MonoBehaviour
    {
        public static CoroutineRunner Instance { get; private set; }

        // Delayed announcement support
        private Coroutine _delayedAnnouncementCoroutine;
        private int _delayedAnnouncementId = 0;

        // Menu cursor tracking
        private int _lastSelectPlateCursor = -1;
        private bool _lastSelectPlateActive = false;
        private int _lastTitleSelectCursor = -1;
        private bool _lastTitleSelectActive = false;
        private int _lastSeriesSelectCursor = -1;
        private bool _lastSeriesSelectActive = false;
        private int _lastGeneralSelectCursor = -1;
        private bool _lastGeneralSelectActive = false;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                SpeechManager.Initialize();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        void Update()
        {
            TrackMenuCursors();
        }

        private void TrackMenuCursors()
        {
            // Track selectPlateCtrl (dialogue choices, talk options)
            TrackSelectPlate();

            // Track titleSelectPlate (main menu, confirmations)
            TrackTitleSelectPlate();

            // Track seriesTitleSelectCtrl (Play Title / Select Episode)
            TrackSeriesSelectPlate();

            // Track GeneralSelectPlateCtrl (chapter selection)
            TrackGeneralSelectPlate();
        }

        private void TrackSelectPlate()
        {
            try
            {
                var instance = selectPlateCtrl.instance;
                if (instance == null)
                    return;

                bool isActive = instance.body_active;
                int cursorNo = instance.cursor_no;

                // Detect when menu becomes active
                if (isActive && !_lastSelectPlateActive)
                {
                    _lastSelectPlateCursor = cursorNo;
                    _lastSelectPlateActive = true;
                }
                // Detect cursor change while active
                else if (isActive && cursorNo != _lastSelectPlateCursor)
                {
                    string optionText = GetSelectPlateOptionText(cursorNo);
                    if (!Net35Extensions.IsNullOrWhiteSpace(optionText))
                    {
                        // Format with checkmark status from MenuPatches
                        string announcement = MenuPatches.FormatSelectOptionAnnouncement(
                            optionText,
                            cursorNo
                        );
                        SpeechManager.Announce(announcement, TextType.MenuChoice);
                    }
                    _lastSelectPlateCursor = cursorNo;
                }
                // Detect when menu becomes inactive
                else if (!isActive && _lastSelectPlateActive)
                {
                    _lastSelectPlateActive = false;
                    _lastSelectPlateCursor = -1;
                }
            }
            catch { }
        }

        private string GetSelectPlateOptionText(int index)
        {
            try
            {
                var instance = selectPlateCtrl.instance;
                if (instance == null)
                    return null;

                // Access the select_list_ field via reflection or by reading the text directly
                var field = typeof(selectPlateCtrl).GetField(
                    "select_list_",
                    System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Instance
                );
                if (field == null)
                    return null;

                var selectList = field.GetValue(instance) as System.Collections.IList;
                if (selectList == null || index < 0 || index >= selectList.Count)
                    return null;

                var selectPlate = selectList[index];
                var textField = selectPlate.GetType().GetField("text_");
                if (textField == null)
                    return null;

                var textComponent = textField.GetValue(selectPlate) as UnityEngine.UI.Text;
                return textComponent?.text;
            }
            catch
            {
                return null;
            }
        }

        private void TrackTitleSelectPlate()
        {
            try
            {
                // Check mainTitleCtrl's select_plate_
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
                if (selectPlate == null)
                    return;

                bool isActive = selectPlate.body_active;
                int cursorNo = selectPlate.cursor_no;

                // Detect cursor change while active
                if (isActive && _lastTitleSelectActive && cursorNo != _lastTitleSelectCursor)
                {
                    string optionText = GetTitleSelectPlateOptionText(mainTitle, cursorNo);
                    if (!Net35Extensions.IsNullOrWhiteSpace(optionText))
                    {
                        SpeechManager.Announce(optionText, TextType.Menu);
                    }
                    _lastTitleSelectCursor = cursorNo;
                }
                // Detect when menu becomes active
                else if (isActive && !_lastTitleSelectActive)
                {
                    _lastTitleSelectCursor = cursorNo;
                    _lastTitleSelectActive = true;
                }
                // Detect when menu becomes inactive
                else if (!isActive && _lastTitleSelectActive)
                {
                    _lastTitleSelectActive = false;
                    _lastTitleSelectCursor = -1;
                }
            }
            catch { }
        }

        private string GetTitleSelectPlateOptionText(mainTitleCtrl mainTitle, int index)
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

        private void TrackSeriesSelectPlate()
        {
            try
            {
                // Check seriesTitleSelectCtrl's select_plate_
                var seriesCtrl = seriesTitleSelectCtrl.instance;
                if (seriesCtrl == null)
                    return;

                var field = typeof(seriesTitleSelectCtrl).GetField(
                    "select_plate_",
                    System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Instance
                );
                if (field == null)
                    return;

                var selectPlate = field.GetValue(seriesCtrl) as titleSelectPlate;
                if (selectPlate == null)
                    return;

                bool isActive = selectPlate.body_active;
                int cursorNo = selectPlate.cursor_no;

                // Detect cursor change while active
                if (isActive && _lastSeriesSelectActive && cursorNo != _lastSeriesSelectCursor)
                {
                    // Options are: Play Title (0), Select Episode (1)
                    string optionText =
                        cursorNo == 0
                            ? TextDataCtrl.GetText(TextDataCtrl.TitleTextID.PLAY_TITLE)
                            : TextDataCtrl.GetText(TextDataCtrl.TitleTextID.SELECT_EPISODE);

                    if (!Net35Extensions.IsNullOrWhiteSpace(optionText))
                    {
                        SpeechManager.Announce(optionText, TextType.Menu);
                    }
                    _lastSeriesSelectCursor = cursorNo;
                }
                // Detect when menu becomes active
                else if (isActive && !_lastSeriesSelectActive)
                {
                    _lastSeriesSelectCursor = cursorNo;
                    _lastSeriesSelectActive = true;
                }
                // Detect when menu becomes inactive
                else if (!isActive && _lastSeriesSelectActive)
                {
                    _lastSeriesSelectActive = false;
                    _lastSeriesSelectCursor = -1;
                }
            }
            catch { }
        }

        private void TrackGeneralSelectPlate()
        {
            try
            {
                // Check ChapterJumpInMenuCtrl's select_plate
                var chapterJump = ChapterJumpCtrl.instance;
                if (chapterJump == null)
                    return;

                var menuField = typeof(ChapterJumpCtrl).GetField(
                    "menuCtrl",
                    System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Instance
                );
                if (menuField == null)
                    return;

                var menuCtrl = menuField.GetValue(chapterJump);
                if (menuCtrl == null)
                    return;

                var plateField = menuCtrl
                    .GetType()
                    .GetField(
                        "select_plate",
                        System.Reflection.BindingFlags.NonPublic
                            | System.Reflection.BindingFlags.Instance
                    );
                if (plateField == null)
                    return;

                var selectPlate = plateField.GetValue(menuCtrl) as GeneralSelectPlateCtrl;
                if (selectPlate == null)
                    return;

                bool isActive = selectPlate.body_active;
                int cursorNo = selectPlate.cursor_no;

                // Detect cursor change while active
                if (isActive && _lastGeneralSelectActive && cursorNo != _lastGeneralSelectCursor)
                {
                    string optionText = GetGeneralSelectPlateOptionText(selectPlate, cursorNo);
                    if (!Net35Extensions.IsNullOrWhiteSpace(optionText))
                    {
                        SpeechManager.Announce(optionText, TextType.Menu);
                    }
                    _lastGeneralSelectCursor = cursorNo;
                }
                // Detect when menu becomes active
                else if (isActive && !_lastGeneralSelectActive)
                {
                    _lastGeneralSelectCursor = cursorNo;
                    _lastGeneralSelectActive = true;
                }
                // Detect when menu becomes inactive
                else if (!isActive && _lastGeneralSelectActive)
                {
                    _lastGeneralSelectActive = false;
                    _lastGeneralSelectCursor = -1;
                }
            }
            catch { }
        }

        private string GetGeneralSelectPlateOptionText(
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

        /// <summary>
        /// Starts the verdict announcement sequence timed to match the sound effects.
        /// </summary>
        public void StartVerdictAnnouncement(bool isNotGuilty)
        {
            StartCoroutine(VerdictAnnouncementCoroutine(isNotGuilty));
        }

        private IEnumerator VerdictAnnouncementCoroutine(bool isNotGuilty)
        {
            if (isNotGuilty)
            {
                // Match the two sound effect hits from judgmentCtrl.CoroutineUSA
                // First sound at frame 20 (~0.33s), second at frame 90 (~1.5s)
                yield return new WaitForSeconds(0.33f);
                SpeechManager.Announce("Not", TextType.Trial);

                yield return new WaitForSeconds(1.17f); // 1.5s - 0.33s
                SpeechManager.Announce("Guilty", TextType.Trial);
            }
            else
            {
                // Spell out G-U-I-L-T-Y matching 6 sound effects at 10-frame intervals
                string[] letters = { "G", "U", "I", "L", "T", "Y" };
                yield return new WaitForSeconds(0.17f); // First sound at frame 10

                for (int i = 0; i < letters.Length; i++)
                {
                    SpeechManager.Announce(letters[i], TextType.Trial);
                    if (i < letters.Length - 1)
                    {
                        yield return new WaitForSeconds(0.17f); // 10 frames between
                    }
                }
            }
        }

        /// <summary>
        /// Schedules a delayed announcement. If called again before the delay expires,
        /// the previous announcement is cancelled.
        /// </summary>
        public void ScheduleDelayedAnnouncement(
            float delaySeconds,
            Func<string> getTextFunc,
            TextType textType
        )
        {
            // Cancel any pending delayed announcement
            CancelDelayedAnnouncement();

            _delayedAnnouncementId++;
            _delayedAnnouncementCoroutine = StartCoroutine(
                DelayedAnnouncementCoroutine(
                    delaySeconds,
                    getTextFunc,
                    textType,
                    _delayedAnnouncementId
                )
            );
        }

        /// <summary>
        /// Cancels any pending delayed announcement.
        /// </summary>
        public void CancelDelayedAnnouncement()
        {
            if (_delayedAnnouncementCoroutine != null)
            {
                StopCoroutine(_delayedAnnouncementCoroutine);
                _delayedAnnouncementCoroutine = null;
            }
            _delayedAnnouncementId++;
        }

        private IEnumerator DelayedAnnouncementCoroutine(
            float delaySeconds,
            Func<string> getTextFunc,
            TextType textType,
            int announcementId
        )
        {
            yield return new WaitForSeconds(delaySeconds);

            // Check if this announcement is still valid (wasn't cancelled)
            if (announcementId == _delayedAnnouncementId)
            {
                try
                {
                    string text = getTextFunc();
                    if (!Net35Extensions.IsNullOrWhiteSpace(text))
                    {
                        SpeechManager.Announce(text, textType);
                    }
                }
                catch (Exception ex)
                {
                    AccessibilityMod.Logger?.Error($"Error in delayed announcement: {ex.Message}");
                }
            }

            _delayedAnnouncementCoroutine = null;
        }
    }
}
