using System;
using System.Collections.Generic;
using System.Linq;
using AccessibilityMod.Core;
using UnityEngine;

namespace AccessibilityMod.Services
{
    public static class HotspotNavigator
    {
        private static List<HotspotInfo> _hotspots = new List<HotspotInfo>();
        private static int _currentIndex = 0;

        public class HotspotInfo
        {
            public uint MessageId;
            public int DataIndex;
            public float CenterX;
            public float CenterY;
            public bool IsExamined;
            public string Description;
        }

        public static void RefreshHotspots()
        {
            // Remember the current hotspot's identity before clearing
            uint? previousMessageId = null;
            int? previousDataIndex = null;
            if (_hotspots.Count > 0 && _currentIndex < _hotspots.Count)
            {
                previousMessageId = _hotspots[_currentIndex].MessageId;
                previousDataIndex = _hotspots[_currentIndex].DataIndex;
            }

            _hotspots.Clear();

            try
            {
                if (GSStatic.inspect_data_ == null)
                {
                    AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                        "No inspection data available"
                    );
                    return;
                }

                for (int i = 0; i < GSStatic.inspect_data_.Length; i++)
                {
                    var data = GSStatic.inspect_data_[i];

                    // End of list marker - place is max value
                    if (data == null || data.place == uint.MaxValue)
                        break;

                    // Skip disabled hotspots (place 254)
                    if (data.place == 254)
                        continue;

                    // Calculate center of quadrilateral
                    float centerX = (data.x0 + data.x1 + data.x2 + data.x3) / 4f;
                    float centerY = (data.y0 + data.y1 + data.y2 + data.y3) / 4f;

                    // Check if already examined
                    bool examined = false;
                    try
                    {
                        ushort inspectNo = inspectCtrl.GetNextInspectNumber((uint)data.message);
                        examined = GSStatic.global_work_.inspect_readed_[0, inspectNo] == 1;
                    }
                    catch { }

                    // Generate position description
                    string posDesc = GetPositionDescription(centerX, centerY);

                    _hotspots.Add(
                        new HotspotInfo
                        {
                            MessageId = data.message,
                            DataIndex = i,
                            CenterX = centerX,
                            CenterY = centerY,
                            IsExamined = examined,
                            Description = $"Point {i + 1} ({posDesc})",
                        }
                    );
                }

                // Sort by position: top-to-bottom, then left-to-right
                _hotspots = _hotspots.OrderBy(h => h.CenterY).ThenBy(h => h.CenterX).ToList();

                // Reassign descriptions after sorting
                for (int i = 0; i < _hotspots.Count; i++)
                {
                    var h = _hotspots[i];
                    string posDesc = GetPositionDescription(h.CenterX, h.CenterY);
                    h.Description = $"Point {i + 1} ({posDesc})";
                }

                // Restore position to previously selected hotspot if it still exists
                _currentIndex = 0;
                if (previousMessageId.HasValue && _hotspots.Count > 0)
                {
                    for (int i = 0; i < _hotspots.Count; i++)
                    {
                        if (_hotspots[i].MessageId == previousMessageId.Value)
                        {
                            _currentIndex = i;
                            break;
                        }
                    }
                }

                AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                    $"Found {_hotspots.Count} hotspots, position restored to {_currentIndex + 1}"
                );
            }
            catch (Exception ex)
            {
                _currentIndex = 0;
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error refreshing hotspots: {ex.Message}"
                );
            }
        }

        private static string GetPositionDescription(float x, float y)
        {
            // Assuming 1920x1080 resolution
            string horizontal =
                x < 640 ? "left"
                : x > 1280 ? "right"
                : "center";
            string vertical =
                y < 360 ? "top"
                : y > 720 ? "bottom"
                : "middle";
            return $"{vertical} {horizontal}";
        }

        public static void NavigateNext()
        {
            if (_hotspots.Count == 0)
            {
                RefreshHotspots();
            }

            if (_hotspots.Count == 0)
            {
                SpeechManager.Announce("No points of interest found", TextType.Investigation);
                return;
            }

            _currentIndex = (_currentIndex + 1) % _hotspots.Count;
            AnnounceCurrentHotspot();
            MoveCursorToCurrentHotspot();
        }

        public static void NavigatePrevious()
        {
            if (_hotspots.Count == 0)
            {
                RefreshHotspots();
            }

            if (_hotspots.Count == 0)
            {
                SpeechManager.Announce("No points of interest found", TextType.Investigation);
                return;
            }

            _currentIndex = (_currentIndex - 1 + _hotspots.Count) % _hotspots.Count;
            AnnounceCurrentHotspot();
            MoveCursorToCurrentHotspot();
        }

        public static void NavigateToNextUnexamined()
        {
            if (_hotspots.Count == 0)
            {
                RefreshHotspots();
            }

            // Refresh examined status
            RefreshExaminedStatus();

            var unexamined = _hotspots.Where(h => !h.IsExamined).ToList();

            if (unexamined.Count == 0)
            {
                SpeechManager.Announce("All points examined", TextType.Investigation);
                return;
            }

            // Find next unexamined after current index
            int startIndex = _currentIndex;
            for (int i = 1; i <= _hotspots.Count; i++)
            {
                int checkIndex = (startIndex + i) % _hotspots.Count;
                if (!_hotspots[checkIndex].IsExamined)
                {
                    _currentIndex = checkIndex;
                    AnnounceCurrentHotspot();
                    MoveCursorToCurrentHotspot();
                    return;
                }
            }

            SpeechManager.Announce("All points examined", TextType.Investigation);
        }

        private static void RefreshExaminedStatus()
        {
            try
            {
                foreach (var hotspot in _hotspots)
                {
                    ushort inspectNo = inspectCtrl.GetNextInspectNumber(hotspot.MessageId);
                    hotspot.IsExamined = GSStatic.global_work_.inspect_readed_[0, inspectNo] == 1;
                }
            }
            catch { }
        }

        public static void AnnounceCurrentHotspot()
        {
            if (_hotspots.Count == 0 || _currentIndex >= _hotspots.Count)
            {
                SpeechManager.Announce("No point selected", TextType.Investigation);
                return;
            }

            var hotspot = _hotspots[_currentIndex];
            string status = hotspot.IsExamined ? " (examined)" : "";
            string message = $"{hotspot.Description}{status}";

            SpeechManager.Announce(message, TextType.Investigation);
        }

        public static void AnnounceAllHotspots()
        {
            if (_hotspots.Count == 0)
            {
                RefreshHotspots();
            }

            RefreshExaminedStatus();

            if (_hotspots.Count == 0)
            {
                SpeechManager.Announce("No points of interest", TextType.Investigation);
                return;
            }

            int examined = _hotspots.Count(h => h.IsExamined);
            int unexamined = _hotspots.Count - examined;

            string summary =
                $"{_hotspots.Count} points of interest. {examined} examined, {unexamined} remaining.";

            // List unexamined ones
            var unexaminedList = _hotspots.Where(h => !h.IsExamined).ToList();
            if (unexaminedList.Count > 0 && unexaminedList.Count <= 5)
            {
                summary += " Unexamined: ";
                summary += string.Join(", ", unexaminedList.Select(h => h.Description).ToArray());
            }

            SpeechManager.Announce(summary, TextType.Investigation);
        }

        private static void MoveCursorToCurrentHotspot()
        {
            if (_hotspots.Count == 0 || _currentIndex >= _hotspots.Count)
                return;

            try
            {
                var hotspot = _hotspots[_currentIndex];

                // Convert from background coordinates to screen coordinates
                // Account for background scroll position
                float bgOffsetX = 0;
                try
                {
                    if (bgCtrl.instance != null)
                    {
                        bgOffsetX = bgCtrl.instance.bg_pos_x;
                    }
                }
                catch { }

                // Calculate screen position
                // Game uses 1920x1080 coordinate system, cursor is centered
                float screenX = hotspot.CenterX - bgOffsetX - 960f;
                float screenY = 540f - hotspot.CenterY;

                // Update cursor position in inspectCtrl
                if (inspectCtrl.instance != null)
                {
                    inspectCtrl.instance.pos_x_ = screenX;
                    inspectCtrl.instance.pos_y_ = screenY;

                    // Update the cursor visual position using reflection (cursor_ is private)
                    var cursorField = typeof(inspectCtrl).GetField(
                        "cursor_",
                        System.Reflection.BindingFlags.NonPublic
                            | System.Reflection.BindingFlags.Instance
                    );
                    if (cursorField != null)
                    {
                        var cursor =
                            cursorField.GetValue(inspectCtrl.instance) as AssetBundleSprite;
                        if (cursor != null)
                        {
                            cursor.transform.localPosition = new Vector3(screenX, screenY, -1f);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error moving cursor: {ex.Message}"
                );
            }
        }

        public static int GetHotspotCount()
        {
            if (_hotspots.Count == 0)
            {
                RefreshHotspots();
            }
            return _hotspots.Count;
        }

        public static int GetUnexaminedCount()
        {
            RefreshExaminedStatus();
            return _hotspots.Count(h => !h.IsExamined);
        }

        public static void OnInvestigationStart()
        {
            RefreshHotspots();

            if (_hotspots.Count > 0)
            {
                int unexamined = _hotspots.Count(h => !h.IsExamined);
                string message = $"Investigation mode. {_hotspots.Count} points of interest";
                if (unexamined < _hotspots.Count)
                {
                    message += $", {unexamined} unexamined";
                }
                message +=
                    ". Use [ and ] to navigate, U to jump to next unexamined point, H to list all.";
                SpeechManager.Announce(message, TextType.Investigation);
            }
            else
            {
                SpeechManager.Announce(
                    "Investigation mode. No points of interest found.",
                    TextType.Investigation
                );
            }
        }
    }
}
