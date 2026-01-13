using System;
using System.Collections;
using AccessibilityMod.Patches;
using UnityAccessibilityLib;
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
                        SpeechManager.Announce(announcement, GameTextType.MenuChoice);
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
                        SpeechManager.Announce(optionText, GameTextType.Menu);
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
                        SpeechManager.Announce(optionText, GameTextType.Menu);
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
                        SpeechManager.Announce(optionText, GameTextType.Menu);
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
            // Determine animation style based on game language (matches judgmentCtrl logic)
            Language language = GSStatic.global_work_.language;
            Language layoutType = GSUtility.GetLanguageLayoutType(language);

            if (layoutType == Language.JAPAN)
            {
                // Japanese/Korean/Chinese layout: Characters appear together with two sound hits
                // Sound at 0.1s and 0.35s, announce at first sound
                yield return new WaitForSeconds(0.1f);
                string langPrefix = GetJapaneseLayoutLanguagePrefix(language);
                string verdictKey = isNotGuilty
                    ? "verdict." + langPrefix + ".not_guilty"
                    : "verdict." + langPrefix + ".guilty";
                SpeechManager.Announce(Services.L.Get(verdictKey), GameTextType.Trial);
            }
            else if (language == Language.Pt_BR)
            {
                // Portuguese: All letters spelled out sequentially at 10-frame intervals
                yield return StartCoroutine(VerdictPortugueseCoroutine(isNotGuilty));
            }
            else
            {
                // USA/Default layout (includes French, German, Spanish)
                yield return StartCoroutine(VerdictUSAStyleCoroutine(isNotGuilty, language));
            }
        }

        private IEnumerator VerdictUSAStyleCoroutine(bool isNotGuilty, Language language)
        {
            if (isNotGuilty)
            {
                // Not Guilty: Two parts with pause between them
                // First sound at frame 20 (~0.33s), second at frame 90 (~1.5s)
                string part1Key,
                    part2Key;

                switch (language)
                {
                    case Language.FRANCE:
                        part1Key = "verdict.fr.not_guilty_part1";
                        part2Key = "verdict.fr.not_guilty_part2";
                        break;
                    case Language.GERMAN:
                        part1Key = "verdict.de.not_guilty_part1";
                        part2Key = "verdict.de.not_guilty_part2";
                        break;
                    case Language.ES_419:
                        part1Key = "verdict.es.not_guilty_part1";
                        part2Key = "verdict.es.not_guilty_part2";
                        break;
                    default: // USA and fallback
                        part1Key = "verdict.en.not_guilty_part1";
                        part2Key = "verdict.en.not_guilty_part2";
                        break;
                }

                yield return new WaitForSeconds(0.33f);
                SpeechManager.Announce(Services.L.Get(part1Key), GameTextType.Trial);

                yield return new WaitForSeconds(1.17f); // 1.5s - 0.33s
                SpeechManager.Announce(Services.L.Get(part2Key), GameTextType.Trial);
            }
            else
            {
                // Guilty: Letters spelled out sequentially at 10-frame intervals
                string lettersKey;

                switch (language)
                {
                    case Language.FRANCE:
                        lettersKey = "verdict.fr.guilty_letters";
                        break;
                    case Language.GERMAN:
                        lettersKey = "verdict.de.guilty_letters";
                        break;
                    case Language.ES_419:
                        lettersKey = "verdict.es.guilty_letters";
                        break;
                    default: // USA and fallback
                        lettersKey = "verdict.en.guilty_letters";
                        break;
                }

                yield return StartCoroutine(SpellLettersCoroutine(lettersKey));
            }
        }

        private IEnumerator VerdictPortugueseCoroutine(bool isNotGuilty)
        {
            string lettersKey;

            if (isNotGuilty)
            {
                lettersKey = "verdict.pt.not_guilty_letters";
            }
            else
            {
                // Determine grammatical gender for Portuguese guilty verdict
                bool isFeminine = GetPortugueseGrammaticalGenderIsFeminine();
                lettersKey = isFeminine
                    ? "verdict.pt.guilty_feminine_letters"
                    : "verdict.pt.guilty_masculine_letters";
            }

            yield return StartCoroutine(SpellLettersCoroutine(lettersKey));
        }

        private IEnumerator SpellLettersCoroutine(string lettersKey)
        {
            string[] letters = Services.L.Get(lettersKey).Split('-');
            yield return new WaitForSeconds(0.17f); // First sound at frame 10

            for (int i = 0; i < letters.Length; i++)
            {
                // Skip empty entries (spaces in "N-O-N- -C-O-U-P-A-B-L-E")
                string letter = letters[i].Trim();
                if (!Net35Extensions.IsNullOrWhiteSpace(letter))
                {
                    SpeechManager.Announce(letter, GameTextType.Trial);
                }

                if (i < letters.Length - 1)
                {
                    yield return new WaitForSeconds(0.17f); // 10 frames between (~0.17s at 60fps)
                }
            }
        }

        /// <summary>
        /// Gets the localization key prefix for Japanese-layout languages
        /// </summary>
        private string GetJapaneseLayoutLanguagePrefix(Language language)
        {
            switch (language)
            {
                case Language.KOREA:
                    return "ko";
                case Language.CHINA_S:
                case Language.CHINA_T:
                    return "zh";
                default: // JAPAN and fallback
                    return "ja";
            }
        }

        /// <summary>
        /// Determines grammatical gender for Portuguese verdict (matches judgmentCtrl.GetGrammaticalGender)
        /// </summary>
        private bool GetPortugueseGrammaticalGenderIsFeminine()
        {
            try
            {
                TitleId title = GSStatic.global_work_.title;
                int story = GSStatic.global_work_.story;
                int scenario = GSStatic.global_work_.scenario;

                switch (title)
                {
                    case TitleId.GS1:
                        switch (story)
                        {
                            case 0:
                                return false; // Masculine
                            case 1:
                                return scenario != 4; // Feminine except scenario 4
                            case 2:
                                return false;
                            case 3:
                                return false;
                            case 4:
                                return true; // Feminine
                        }
                        break;
                    case TitleId.GS2:
                        switch (story)
                        {
                            case 0:
                                return true; // Feminine
                            case 1:
                                return true;
                            case 2:
                                return false;
                            case 3:
                                return false;
                        }
                        break;
                    case TitleId.GS3:
                        switch (story)
                        {
                            case 0:
                                return false;
                            case 1:
                                return false;
                            case 2:
                                return true; // Feminine
                            case 3:
                                return false;
                            case 4:
                                return true; // Feminine
                        }
                        break;
                }
            }
            catch
            {
                // Fallback to feminine on error
            }

            return true; // Default to feminine (matches game default)
        }

        /// <summary>
        /// Schedules a delayed announcement. If called again before the delay expires,
        /// the previous announcement is cancelled.
        /// </summary>
        public void ScheduleDelayedAnnouncement(
            float delaySeconds,
            Func<string> getTextFunc,
            int textType
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
            int textType,
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
