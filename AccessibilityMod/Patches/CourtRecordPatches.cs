using System;
using System.Text;
using AccessibilityMod.Core;
using AccessibilityMod.Services;
using HarmonyLib;
using UnityAccessibilityLib;
using UnityEngine.UI;
using L = AccessibilityMod.Services.L;

namespace AccessibilityMod.Patches
{
    [HarmonyPatch]
    public static class CourtRecordPatches
    {
        private static int _lastRecordCursor = -1;
        private static int _lastRecordType = -1;

        // Detail view state tracking
        private static int _currentDetailId = -1;
        private static int _currentDetailPageCount = 0;

        #region Court Record Open/Close

        // Court record opened - announce tab type and first item
        [HarmonyPostfix]
        [HarmonyPatch(typeof(recordListCtrl), "noteOpen")]
        public static void NoteOpen_Postfix(recordListCtrl __instance, int mode)
        {
            try
            {
                string tabName =
                    mode == 0 ? L.Get("court_record.evidence") : L.Get("court_record.profiles");

                // Get item count for current tab
                int itemCount = 0;
                if (__instance.record_data_ != null && mode < __instance.record_data_.Count)
                {
                    itemCount = __instance.record_data_[mode].cursor_num_;
                }

                string message = L.Get("court_record.opened", tabName, itemCount);
                SpeechManager.Announce(message, GameTextType.Menu);

                // Reset tracking
                _lastRecordCursor = -1;
                _lastRecordType = mode;
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in NoteOpen patch: {ex.Message}"
                );
            }
        }

        #endregion

        #region Tab Switching

        // Called when changing from Evidence to Profiles or vice versa
        [HarmonyPostfix]
        [HarmonyPatch(typeof(recordListCtrl), "ChangeRecord")]
        public static void ChangeRecord_Postfix(recordListCtrl __instance)
        {
            try
            {
                int recordType = __instance.record_type;

                // Only announce if we actually switched tabs (not initial open)
                if (_lastRecordType != -1 && _lastRecordType != recordType)
                {
                    string tabName =
                        recordType == 0
                            ? L.Get("court_record.evidence")
                            : L.Get("court_record.profiles");

                    int itemCount = 0;
                    if (
                        __instance.record_data_ != null
                        && recordType < __instance.record_data_.Count
                    )
                    {
                        itemCount = __instance.record_data_[recordType].cursor_num_;
                    }

                    string message = L.Get("court_record.tab_items", tabName, itemCount);
                    SpeechManager.Announce(message, GameTextType.Menu);
                }

                _lastRecordType = recordType;
                _lastRecordCursor = -1; // Reset cursor to force announcement of first item
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in ChangeRecord patch: {ex.Message}"
                );
            }
        }

        #endregion

        #region Cursor Movement

        // Called when cursor moves or selection changes
        [HarmonyPostfix]
        [HarmonyPatch(typeof(recordListCtrl), "cursorRecord")]
        public static void CursorRecord_Postfix(recordListCtrl __instance, int in_type, int in_no)
        {
            try
            {
                // Don't announce if court record isn't actually open (e.g. during game loading)
                if (!__instance.is_open)
                    return;

                // Avoid duplicate announcements from initial open
                if (_lastRecordCursor == in_no && _lastRecordType == in_type)
                    return;

                _lastRecordCursor = in_no;
                _lastRecordType = in_type;

                // Get current pice data
                piceData currentItem = __instance.current_pice_;
                if (currentItem == null)
                    return;

                // Build announcement with name and description
                StringBuilder sb = new StringBuilder();
                sb.Append(currentItem.name);

                // Add description lines
                string comment0 = currentItem.comment00;
                string comment1 = currentItem.comment01;
                string comment2 = currentItem.comment02;

                if (!Net35Extensions.IsNullOrWhiteSpace(comment0))
                {
                    sb.Append(". ").Append(comment0);
                }
                if (!Net35Extensions.IsNullOrWhiteSpace(comment1))
                {
                    sb.Append(" ").Append(comment1);
                }
                if (!Net35Extensions.IsNullOrWhiteSpace(comment2))
                {
                    sb.Append(" ").Append(comment2);
                }

                // Check if item has details available
                if (currentItem.detail_id != 0 || currentItem.obj_id != 0)
                {
                    sb.Append(" - ").Append(L.Get("court_record.press_for_details"));
                }

                SpeechManager.Announce(sb.ToString(), GameTextType.Menu);
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in CursorRecord patch: {ex.Message}"
                );
            }
        }

        #endregion


        #region Detail View

        // Announce when viewing evidence/profile details
        [HarmonyPostfix]
        [HarmonyPatch(typeof(recordDetailCtrl), "CoroutineViewDetail")]
        public static void ViewDetail_Postfix(int in_id)
        {
            try
            {
                // Store detail ID for page navigation
                _currentDetailId = in_id;

                // Get the current item from the record list
                var recordList = recordListCtrl.instance;
                if (recordList == null)
                    return;

                piceData currentItem = recordList.current_pice_;
                if (currentItem == null)
                    return;

                // Get detail data to check page count
                var detailDataList = piceDataCtrl.instance.status_ext_bg_tbl;
                int pageCount = 1;
                uint bgId = 0;
                if (detailDataList != null && in_id >= 0 && in_id < detailDataList.Count)
                {
                    var detailData = detailDataList[in_id];
                    if (detailData != null)
                    {
                        bgId = detailData.bg_id;
                        if (detailData.page_num > 0)
                        {
                            pageCount = (int)detailData.page_num;
                        }
                    }
                }
                _currentDetailPageCount = pageCount;

                // Log detail info for adding descriptions
                string gameName = "GS1";
                try
                {
                    gameName = GSStatic.global_work_.title.ToString();
                }
                catch { }

                bool hasDescription = EvidenceDetailService.HasDescription(in_id);
                AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                    $"[EvidenceDetail] Game: {gameName}, DetailID: {in_id}, BgID: {bgId}, "
                        + $"Item: \"{currentItem.name}\", Pages: {pageCount}, HasDescription: {hasDescription}"
                );

                // Build announcement
                StringBuilder sb = new StringBuilder();
                sb.Append(currentItem.name).Append(". ");

                // Try to get accessibility description
                string description = EvidenceDetailService.GetDescription(in_id, 0);
                if (!Net35Extensions.IsNullOrWhiteSpace(description))
                {
                    sb.Append(description);
                }
                else
                {
                    sb.Append(L.Get("court_record.no_description", in_id));
                }

                // Add page info if multi-page
                if (pageCount > 1)
                {
                    sb.Append(" ").Append(L.Get("court_record.page_x_of_y", 1, pageCount));
                    sb.Append(" ").Append(L.Get("court_record.page_navigation_hint"));
                }

                sb.Append(" ").Append(L.Get("court_record.close_hint"));
                SpeechManager.Announce(sb.ToString(), GameTextType.Menu);
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in ViewDetail patch: {ex.Message}"
                );
            }
        }

        // Announce page changes in detail view
        [HarmonyPostfix]
        [HarmonyPatch(typeof(recordDetailCtrl), "ChengPage")]
        public static void ChengPage_Postfix(int page_num)
        {
            try
            {
                if (_currentDetailId < 0)
                    return;

                StringBuilder sb = new StringBuilder();
                sb.Append(L.Get("court_record.page_x_of_y", page_num + 1, _currentDetailPageCount))
                    .Append(" ");

                // Try to get accessibility description for this page
                string description = EvidenceDetailService.GetDescription(
                    _currentDetailId,
                    page_num
                );
                if (!Net35Extensions.IsNullOrWhiteSpace(description))
                {
                    sb.Append(description);
                }

                SpeechManager.Announce(sb.ToString(), GameTextType.Menu);
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in ChengPage patch: {ex.Message}"
                );
            }
        }

        #endregion

        #region Evidence Added Popup

        /// <summary>
        /// Hook when evidence is added to the court record and the popup plays.
        /// This announces the evidence name along with its full description to match the visual display.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(picePlateCtrl), "playPice")]
        public static void PlayPice_Postfix(picePlateCtrl __instance)
        {
            try
            {
                // Get the evidence name from the icon_name text
                string name = __instance.icon_name?.text;
                if (Net35Extensions.IsNullOrWhiteSpace(name))
                    return;

                // Build announcement with name and description
                StringBuilder sb = new StringBuilder();
                sb.Append(name);

                // Get description lines from the comment
                var comment = __instance.comment;
                if (comment?.line_ != null)
                {
                    sb.Append(".");
                    foreach (var line in comment.line_)
                    {
                        if (line != null && !Net35Extensions.IsNullOrWhiteSpace(line.text))
                        {
                            sb.Append($" {line.text}");
                        }
                    }
                }

                SpeechManager.Announce(sb.ToString(), GameTextType.Menu);
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in PlayPice patch: {ex.Message}"
                );
            }
        }

        #endregion

        #region State Reset

        // Reset state when court record closes
        public static void ResetState()
        {
            _lastRecordCursor = -1;
            _lastRecordType = -1;
            _currentDetailId = -1;
            _currentDetailPageCount = 0;
        }

        #endregion
    }
}
