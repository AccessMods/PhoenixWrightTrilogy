using System;
using System.Text;
using AccessibilityMod.Core;
using AccessibilityMod.Services;
using HarmonyLib;

namespace AccessibilityMod.Patches
{
    [HarmonyPatch]
    public static class CourtRecordPatches
    {
        private static int _lastRecordCursor = -1;
        private static int _lastRecordPage = -1;
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
                string tabName = mode == 0 ? "Evidence" : "Profiles";

                // Get item count for current tab
                int itemCount = 0;
                if (__instance.record_data_ != null && mode < __instance.record_data_.Count)
                {
                    itemCount = __instance.record_data_[mode].cursor_num_;
                }

                string message = $"Court Record: {tabName}. {itemCount} items.";
                SpeechManager.Announce(message, TextType.Menu);

                // Reset tracking
                _lastRecordCursor = -1;
                _lastRecordPage = -1;
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
                    string tabName = recordType == 0 ? "Evidence" : "Profiles";

                    int itemCount = 0;
                    if (
                        __instance.record_data_ != null
                        && recordType < __instance.record_data_.Count
                    )
                    {
                        itemCount = __instance.record_data_[recordType].cursor_num_;
                    }

                    string message = $"{tabName}. {itemCount} items.";
                    SpeechManager.Announce(message, TextType.Menu);
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
                    sb.Append(" - Press Enter for details");
                }

                SpeechManager.Announce(sb.ToString(), TextType.Menu);
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in CursorRecord patch: {ex.Message}"
                );
            }
        }

        private static int GetPageNum(recordListCtrl instance)
        {
            try
            {
                // page_num_ is a property that reads from advCtrl
                if (instance.record_type == 0)
                {
                    return advCtrl.instance.sub_window_.note_.item_page;
                }
                return advCtrl.instance.sub_window_.note_.man_page;
            }
            catch
            {
                return 0;
            }
        }

        #endregion

        #region Page Changes

        // Called when page changes (after animation)
        [HarmonyPostfix]
        [HarmonyPatch(typeof(recordListCtrl), "recordPageChange")]
        public static void RecordPageChange_Postfix(recordListCtrl __instance)
        {
            try
            {
                int pageNum = GetPageNum(__instance);
                _lastRecordPage = pageNum;
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in RecordPageChange patch: {ex.Message}"
                );
            }
        }

        private static int GetPageCount(recordListCtrl instance)
        {
            try
            {
                var field = typeof(recordListCtrl).GetField(
                    "page_cnt_",
                    System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Instance
                );
                if (field != null)
                {
                    return (int)field.GetValue(instance);
                }
            }
            catch { }
            return 1;
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
                    sb.Append($"No description. Detail ID: {in_id}. ");
                }

                // Add page info if multi-page
                if (pageCount > 1)
                {
                    sb.Append(" Page 1 of ").Append(pageCount).Append(".");
                    sb.Append(" Press left or right for pages.");
                }

                sb.Append(" Press Backspace to close.");
                SpeechManager.Announce(sb.ToString(), TextType.Menu);
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
                sb.Append("Page ").Append(page_num + 1);

                if (_currentDetailPageCount > 1)
                {
                    sb.Append(" of ").Append(_currentDetailPageCount);
                }

                sb.Append(". ");

                // Try to get accessibility description for this page
                string description = EvidenceDetailService.GetDescription(
                    _currentDetailId,
                    page_num
                );
                if (!Net35Extensions.IsNullOrWhiteSpace(description))
                {
                    sb.Append(description);
                }

                SpeechManager.Announce(sb.ToString(), TextType.Menu);
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error in ChengPage patch: {ex.Message}"
                );
            }
        }

        #endregion

        #region State Reset

        // Reset state when court record closes
        public static void ResetState()
        {
            _lastRecordCursor = -1;
            _lastRecordPage = -1;
            _lastRecordType = -1;
            _currentDetailId = -1;
            _currentDetailPageCount = 0;
        }

        #endregion
    }
}
