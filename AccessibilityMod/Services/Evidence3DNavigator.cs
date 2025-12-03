using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using AccessibilityMod.Core;
using UnityEngine;

namespace AccessibilityMod.Services
{
    /// <summary>
    /// Service for tracking and announcing 3D evidence examination state.
    /// Used in GS1 Episode 5+ for examining objects like the wallet.
    /// </summary>
    public static class Evidence3DNavigator
    {
        // Zoom tracking
        private static float _lastZoomLevel = 1.0f;
        private static bool _isTracking = false;

        // Hotspot navigation
        private static List<HotspotInfo> _hotspots = new List<HotspotInfo>();
        private static int _currentHotspotIndex = -1;

        private class HotspotInfo
        {
            public int Index;
            public Vector3 WorldPosition;
            public string Name;
        }

        #region Initialization

        /// <summary>
        /// Called when entering 3D evidence mode to reset state.
        /// </summary>
        public static void OnEnter3DMode()
        {
            _lastZoomLevel = GetCurrentZoomFloat();
            _isTracking = true;
            _currentHotspotIndex = -1;
            RefreshHotspots();
        }

        /// <summary>
        /// Called when exiting 3D evidence mode.
        /// </summary>
        public static void OnExit3DMode()
        {
            _isTracking = false;
            _hotspots.Clear();
            _currentHotspotIndex = -1;
        }

        #endregion

        #region Zoom Tracking

        /// <summary>
        /// Gets the current zoom level as a float.
        /// </summary>
        private static float GetCurrentZoomFloat()
        {
            try
            {
                if (scienceInvestigationCtrl.instance != null)
                {
                    var manager = scienceInvestigationCtrl.instance.evidence_manager;
                    if (manager != null)
                    {
                        return manager.scale_ratio;
                    }
                }
            }
            catch { }
            return 1.0f;
        }

        /// <summary>
        /// Checks for zoom level changes and announces them.
        /// Should be called regularly during 3D mode.
        /// </summary>
        public static void CheckAndAnnounceZoomChange()
        {
            if (!_isTracking)
                return;

            try
            {
                float currentZoom = GetCurrentZoomFloat();

                // Round to 1 decimal place for comparison (avoid floating point noise)
                float roundedCurrent = (float)Math.Round(currentZoom, 1);
                float roundedLast = (float)Math.Round(_lastZoomLevel, 1);

                if (Math.Abs(roundedCurrent - roundedLast) >= 0.05f)
                {
                    _lastZoomLevel = currentZoom;

                    // Convert to percentage for clearer announcement
                    int zoomPercent = (int)(currentZoom * 100);
                    ClipboardManager.Announce($"Zoom {zoomPercent}%", TextType.Menu);
                }
            }
            catch { }
        }

        /// <summary>
        /// Gets the current zoom level as a formatted string.
        /// </summary>
        public static string GetZoomLevel()
        {
            float zoom = GetCurrentZoomFloat();
            int zoomPercent = (int)(zoom * 100);
            return $"{zoomPercent}%";
        }

        #endregion

        #region Hotspot Navigation

        /// <summary>
        /// Refreshes the list of hotspots on the current 3D evidence.
        /// </summary>
        public static void RefreshHotspots()
        {
            _hotspots.Clear();

            try
            {
                if (scienceInvestigationCtrl.instance == null)
                    return;

                var manager = scienceInvestigationCtrl.instance.evidence_manager;
                if (manager == null)
                    return;

                // Get the model parent where collision meshes are
                var modelParent = manager.gameObject;
                if (modelParent == null)
                    return;

                // Find all mesh colliders (these are the hotspots)
                var colliders = modelParent.GetComponentsInChildren<MeshCollider>(true);

                Regex numberRegex = new Regex(@"(\d+)", RegexOptions.Singleline);
                Regex nukiRegex = new Regex(@"(nuki|nuke)", RegexOptions.IgnoreCase);

                foreach (var collider in colliders)
                {
                    // Skip "nuki" meshes (these are exclusion zones)
                    if (nukiRegex.IsMatch(collider.gameObject.name))
                        continue;

                    // Try to extract hotspot number from name
                    Match match = numberRegex.Match(collider.gameObject.name);
                    int index = 0;
                    if (match.Success)
                    {
                        index = int.Parse(match.Groups[1].Value) - 1; // Convert to 0-based
                    }

                    // Get center of the mesh bounds
                    Vector3 center = collider.bounds.center;

                    _hotspots.Add(
                        new HotspotInfo
                        {
                            Index = index,
                            WorldPosition = center,
                            Name = $"Hotspot {_hotspots.Count + 1}",
                        }
                    );
                }

                // Sort by index
                _hotspots.Sort((a, b) => a.Index.CompareTo(b.Index));

                // Rename after sorting
                for (int i = 0; i < _hotspots.Count; i++)
                {
                    _hotspots[i].Name = $"Hotspot {i + 1}";
                }

                AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                    $"[3DEvidence] Found {_hotspots.Count} hotspots"
                );
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error refreshing 3D hotspots: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Navigate to the next hotspot.
        /// </summary>
        public static void NavigateNext()
        {
            if (_hotspots.Count == 0)
            {
                RefreshHotspots();
            }

            if (_hotspots.Count == 0)
            {
                ClipboardManager.Announce("No hotspots found", TextType.Menu);
                return;
            }

            _currentHotspotIndex = (_currentHotspotIndex + 1) % _hotspots.Count;
            NavigateToCurrentHotspot();
        }

        /// <summary>
        /// Navigate to the previous hotspot.
        /// </summary>
        public static void NavigatePrevious()
        {
            if (_hotspots.Count == 0)
            {
                RefreshHotspots();
            }

            if (_hotspots.Count == 0)
            {
                ClipboardManager.Announce("No hotspots found", TextType.Menu);
                return;
            }

            _currentHotspotIndex = (_currentHotspotIndex - 1 + _hotspots.Count) % _hotspots.Count;
            NavigateToCurrentHotspot();
        }

        /// <summary>
        /// Move cursor to the current hotspot and announce it.
        /// </summary>
        private static void NavigateToCurrentHotspot()
        {
            if (_currentHotspotIndex < 0 || _currentHotspotIndex >= _hotspots.Count)
                return;

            var hotspot = _hotspots[_currentHotspotIndex];

            try
            {
                // Get the camera used for 3D evidence
                Camera camera = scienceInvestigationCtrl.instance.science_camera;
                if (camera == null)
                    return;

                // Convert world position to screen position
                Vector3 screenPos = camera.WorldToScreenPoint(hotspot.WorldPosition);

                // Get the cursor sprite via reflection
                var cursorField = typeof(scienceInvestigationCtrl).GetField(
                    "cursor_",
                    System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Instance
                );

                if (cursorField != null)
                {
                    var cursor =
                        cursorField.GetValue(scienceInvestigationCtrl.instance)
                        as AssetBundleSprite;
                    if (cursor != null)
                    {
                        // Convert screen position back to world position for cursor
                        Vector3 cursorWorldPos = camera.ScreenToWorldPoint(
                            new Vector3(screenPos.x, screenPos.y, cursor.transform.position.z)
                        );

                        // Update cursor position
                        cursor.transform.position = new Vector3(
                            cursorWorldPos.x,
                            cursorWorldPos.y,
                            cursor.transform.position.z
                        );
                    }
                }

                // Announce the hotspot
                string message = $"{hotspot.Name} of {_hotspots.Count}";
                ClipboardManager.Announce(message, TextType.Menu);
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error navigating to hotspot: {ex.Message}"
                );
                // Still announce even if cursor move failed
                ClipboardManager.Announce(hotspot.Name, TextType.Menu);
            }
        }

        /// <summary>
        /// Gets the number of hotspots found.
        /// </summary>
        public static int GetHotspotCount()
        {
            return _hotspots.Count;
        }

        #endregion

        #region Evidence Info

        /// <summary>
        /// Gets the name of the evidence currently being examined.
        /// Tries to get it from the court record, falls back to generic name.
        /// </summary>
        public static string GetCurrentEvidenceName()
        {
            try
            {
                // Try to get from court record's current item
                if (
                    recordListCtrl.instance != null
                    && recordListCtrl.instance.current_pice_ != null
                )
                {
                    string name = recordListCtrl.instance.current_pice_.name;
                    if (!Net35Extensions.IsNullOrWhiteSpace(name))
                    {
                        return name;
                    }
                }
            }
            catch { }

            // Fallback: try to get from poly object ID
            try
            {
                if (scienceInvestigationCtrl.instance != null)
                {
                    int objId = scienceInvestigationCtrl.instance.poly_obj_id;
                    return GetEvidenceNameByObjId(objId);
                }
            }
            catch { }

            return "Evidence";
        }

        /// <summary>
        /// Maps poly object IDs to evidence names for fallback.
        /// </summary>
        private static string GetEvidenceNameByObjId(int objId)
        {
            // Common 3D evidence items in GS1 Episode 5
            switch (objId)
            {
                case 1:
                    return "Briefcase";
                case 2:
                    return "Wallet";
                case 3:
                    return "Letter";
                case 8:
                    return "Cell Phone";
                case 9:
                    return "Cell Phone (open)";
                case 12:
                    return "Syringe";
                case 27:
                    return "Photo Album";
                case 29:
                    return "Envelope";
                default:
                    return $"Evidence (item {objId})";
            }
        }

        #endregion

        #region State Queries

        /// <summary>
        /// Checks if the cursor is currently over a hotspot.
        /// </summary>
        public static bool IsOverHotspot()
        {
            try
            {
                if (scienceInvestigationCtrl.instance != null)
                {
                    return scienceInvestigationCtrl.instance.hit_point_index != -1;
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Gets the current hotspot index (-1 if not over any).
        /// </summary>
        public static int GetCurrentHotspotIndex()
        {
            try
            {
                if (scienceInvestigationCtrl.instance != null)
                {
                    return scienceInvestigationCtrl.instance.hit_point_index;
                }
            }
            catch { }
            return -1;
        }

        #endregion

        #region Announcements

        /// <summary>
        /// Announces the current state of 3D evidence examination.
        /// Called when user presses I key in 3D mode.
        /// </summary>
        public static void AnnounceState()
        {
            try
            {
                string zoomLevel = GetZoomLevel();
                bool overHotspot = IsOverHotspot();
                int hotspotCount = _hotspots.Count;

                string hotspotStatus = overHotspot ? "On hotspot" : "No hotspot";
                string message =
                    $"Zoom: {zoomLevel}. {hotspotStatus}. {hotspotCount} hotspots total.";

                if (overHotspot)
                {
                    message += " Press A to examine.";
                }
                else
                {
                    message += " Use [ and ] to navigate hotspots.";
                }

                ClipboardManager.Announce(message, TextType.Menu);
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error announcing 3D state: {ex.Message}"
                );
                ClipboardManager.Announce("Unable to read 3D examination state", TextType.Menu);
            }
        }

        /// <summary>
        /// Announces just the zoom level.
        /// </summary>
        public static void AnnounceZoom()
        {
            string zoomLevel = GetZoomLevel();
            ClipboardManager.Announce($"Zoom: {zoomLevel}", TextType.Menu);
        }

        /// <summary>
        /// Announces whether a hotspot is under the cursor.
        /// </summary>
        public static void AnnounceHotspot()
        {
            if (IsOverHotspot())
            {
                ClipboardManager.Announce("Hotspot detected, press A to examine", TextType.Menu);
            }
            else
            {
                ClipboardManager.Announce("No hotspot under cursor", TextType.Menu);
            }
        }

        #endregion
    }
}
