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
            public MeshCollider Collider; // Reference to actual collider for fresh position data
            public string Name;
            public string ColliderName; // For debugging
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
                    string colliderName = collider.gameObject.name;

                    // Skip "nuki" meshes (these are exclusion zones)
                    if (nukiRegex.IsMatch(colliderName))
                    {
#if DEBUG
                        AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                            $"[3DNav] Skipping nuki mesh: {colliderName}"
                        );
#endif
                        continue;
                    }

                    // Try to extract hotspot number from name
                    Match match = numberRegex.Match(colliderName);
                    int index = 0;
                    if (match.Success)
                    {
                        index = int.Parse(match.Groups[1].Value) - 1; // Convert to 0-based
                    }

#if DEBUG
                    AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                        $"[3DNav] Found collider '{colliderName}': index={index}"
                    );
#endif

                    _hotspots.Add(
                        new HotspotInfo
                        {
                            Index = index,
                            Collider = collider,
                            Name = $"Hotspot {_hotspots.Count + 1}",
                            ColliderName = colliderName,
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

#if DEBUG
                AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                    $"[3DEvidence] Found {_hotspots.Count} hotspots"
                );
#endif
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
        /// Rotates the evidence so the hotspot faces the camera for reliable selection.
        /// </summary>
        private static void NavigateToCurrentHotspot()
        {
            if (_currentHotspotIndex < 0 || _currentHotspotIndex >= _hotspots.Count)
                return;

            var hotspot = _hotspots[_currentHotspotIndex];

            try
            {
                var ctrl = scienceInvestigationCtrl.instance;
                var manager = ctrl.evidence_manager;
                if (manager == null)
                {
#if DEBUG
                    AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                        "[3DNav] ERROR: manager is null"
                    );
#endif
                    return;
                }

                if (hotspot.Collider == null)
                {
#if DEBUG
                    AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                        "[3DNav] ERROR: collider is null"
                    );
#endif
                    return;
                }

#if DEBUG
                AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                    $"[3DNav] Navigating to hotspot {_currentHotspotIndex}: '{hotspot.ColliderName}'"
                );
#endif

                // Get operete_trans_ which is the actual transform that SetRotate modifies
                var opereteField = typeof(evidenceObjectManager).GetField(
                    "operete_trans_",
                    System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Instance
                );

                Transform opereteTrans = null;
                if (opereteField != null)
                {
                    opereteTrans = opereteField.GetValue(manager) as Transform;
                }

                if (opereteTrans == null)
                {
#if DEBUG
                    AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                        "[3DNav] WARNING: couldn't get operete_trans_, using gameObject.transform"
                    );
#endif
                    opereteTrans = manager.gameObject.transform;
                }

                // First, reset rotation to identity so we can calculate from a known state
                manager.SetRotate(0, 0);

                // Get the collider's position relative to the operete transform (in its local space at identity rotation)
                Vector3 colliderWorldPos = hotspot.Collider.bounds.center;
                Vector3 localPos = opereteTrans.InverseTransformPoint(colliderWorldPos);

#if DEBUG
                AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                    $"[3DNav] Collider world pos (at identity): ({colliderWorldPos.x:F3}, {colliderWorldPos.y:F3}, {colliderWorldPos.z:F3})"
                );
                AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                    $"[3DNav] Local pos relative to operete_trans: ({localPos.x:F3}, {localPos.y:F3}, {localPos.z:F3})"
                );
#endif

                // Search for a rotation that makes the hotspot visible and hittable
                // Try the calculated ideal rotation first, then search nearby angles
                Camera camera = ctrl.science_camera;

                float bestH = 0,
                    bestV = 0;
                Vector2 bestScreenPos = new Vector2(Screen.width / 2f, Screen.height / 2f);
                bool foundRotation = false;

                // Calculate initial rotation estimate
                float idealH = Mathf.Atan2(localPos.x, localPos.z) * Mathf.Rad2Deg;
                float distXZ = Mathf.Sqrt(localPos.x * localPos.x + localPos.z * localPos.z);
                float idealV = Mathf.Atan2(-localPos.y, distXZ) * Mathf.Rad2Deg;

                // Clamp vertical to reasonable range (extreme angles often don't work)
                idealV = Mathf.Clamp(idealV, -60f, 60f);

#if DEBUG
                AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                    $"[3DNav] Ideal rotation: H={idealH:F1}, V={idealV:F1}"
                );
#endif

                // Search pattern: try ideal first, then offsets
                float[] hOffsets = { 0, -15, 15, -30, 30, -45, 45, -60, 60, 180, 165, -165 };
                float[] vOffsets = { 0, -15, 15, -30, 30, -45, 45 };

                foreach (float vOff in vOffsets)
                {
                    if (foundRotation)
                        break;

                    foreach (float hOff in hOffsets)
                    {
                        if (foundRotation)
                            break;

                        float testH = idealH + hOff;
                        float testV = Mathf.Clamp(idealV + vOff, -75f, 75f);

                        manager.SetRotate(testH, testV);

                        // Raycast from screen center to see if we hit our target
                        if (camera != null)
                        {
                            Ray ray = camera.ScreenPointToRay(
                                new Vector3(Screen.width / 2f, Screen.height / 2f, 0)
                            );
                            RaycastHit hit;
                            if (Physics.Raycast(ray, out hit, 100f))
                            {
                                if (hit.collider == hotspot.Collider)
                                {
                                    bestH = testH;
                                    bestV = testV;
                                    bestScreenPos = new Vector2(
                                        Screen.width / 2f,
                                        Screen.height / 2f
                                    );
                                    foundRotation = true;
#if DEBUG
                                    AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                                        $"[3DNav] Found rotation at H={testH:F1}, V={testV:F1} (offset H={hOff}, V={vOff})"
                                    );
#endif
                                }
                            }
                        }
                    }
                }

                // If center didn't work, try a wider screen search at the ideal rotation
                if (!foundRotation && camera != null)
                {
                    manager.SetRotate(idealH, idealV);

                    // Search in a grid pattern
                    for (int sy = -2; sy <= 2 && !foundRotation; sy++)
                    {
                        for (int sx = -2; sx <= 2 && !foundRotation; sx++)
                        {
                            float screenX = Screen.width / 2f + sx * 100;
                            float screenY = Screen.height / 2f + sy * 100;

                            Ray ray = camera.ScreenPointToRay(new Vector3(screenX, screenY, 0));
                            RaycastHit hit;
                            if (Physics.Raycast(ray, out hit, 100f))
                            {
                                if (hit.collider == hotspot.Collider)
                                {
                                    bestH = idealH;
                                    bestV = idealV;
                                    bestScreenPos = new Vector2(screenX, screenY);
                                    foundRotation = true;
#if DEBUG
                                    AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                                        $"[3DNav] Found at ideal rotation, screen offset ({sx * 100}, {sy * 100})"
                                    );
#endif
                                }
                            }
                        }
                    }
                }

                if (!foundRotation)
                {
                    // Last resort: use calculated rotation and bounds center
                    bestH = idealH;
                    bestV = idealV;
                    manager.SetRotate(bestH, bestV);

                    Vector3 boundsCenter = hotspot.Collider.bounds.center;
                    if (camera != null)
                    {
                        Vector3 screenPos3D = camera.WorldToScreenPoint(boundsCenter);
                        bestScreenPos = new Vector2(screenPos3D.x, screenPos3D.y);
                    }

#if DEBUG
                    AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                        $"[3DNav] No hit found, using ideal rotation H={bestH:F1}, V={bestV:F1}, bounds center"
                    );
#endif
                }

                // Apply the best rotation we found
                manager.SetRotate(bestH, bestV);

                // Move cursor to the position we found during rotation search
                if (camera != null)
                {
                    var cursorField = typeof(scienceInvestigationCtrl).GetField(
                        "cursor_",
                        System.Reflection.BindingFlags.NonPublic
                            | System.Reflection.BindingFlags.Instance
                    );

                    if (cursorField != null)
                    {
                        var cursor = cursorField.GetValue(ctrl) as AssetBundleSprite;
                        if (cursor != null)
                        {
                            Vector3 cursorWorldPos = camera.ScreenToWorldPoint(
                                new Vector3(
                                    bestScreenPos.x,
                                    bestScreenPos.y,
                                    cursor.transform.position.z
                                )
                            );

                            cursor.transform.position = new Vector3(
                                cursorWorldPos.x,
                                cursorWorldPos.y,
                                cursor.transform.position.z
                            );

#if DEBUG
                            AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                                $"[3DNav] Cursor moved to screen: ({bestScreenPos.x:F1}, {bestScreenPos.y:F1})"
                            );
#endif
                        }
                    }
                }

#if DEBUG
                // Check current hit state
                int hitIndex = ctrl.hit_point_index;
                AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                    $"[3DNav] hit_point_index (immediate): {hitIndex}"
                );
#endif

                // Announce the hotspot
                string message = $"{hotspot.Name} of {_hotspots.Count}";
                ClipboardManager.Announce(message, TextType.Menu);
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error navigating to hotspot: {ex.Message}\n{ex.StackTrace}"
                );
                // Still announce even if navigation failed
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
