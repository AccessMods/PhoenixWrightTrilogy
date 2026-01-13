using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using AccessibilityMod.Core;
using UnityAccessibilityLib;
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
                    SpeechManager.Announce(
                        L.Get("evidence_3d.zoom_percent", zoomPercent),
                        GameTextType.Menu
                    );
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

#if DEBUG
                AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                    $"[3DNav] Scanning for colliders on '{modelParent.name}', layer={modelParent.layer}"
                );
#endif

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
                    // Log detailed information about the collider
                    var bounds = collider.bounds;
                    AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                        $"[3DNav] Found collider '{colliderName}': index={index}, enabled={collider.enabled}, layer={collider.gameObject.layer}, "
                            + $"bounds center=({bounds.center.x:F2}, {bounds.center.y:F2}, {bounds.center.z:F2}), "
                            + $"size=({bounds.size.x:F2}, {bounds.size.y:F2}, {bounds.size.z:F2})"
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
                    $"[3DEvidence] Found {_hotspots.Count} hotspots total"
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
                SpeechManager.Announce(L.Get("evidence_3d.no_hotspots"), GameTextType.Menu);
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
                SpeechManager.Announce(L.Get("evidence_3d.no_hotspots"), GameTextType.Menu);
                return;
            }

            // When starting from -1 (no selection), go to the last item
            if (_currentHotspotIndex < 0)
            {
                _currentHotspotIndex = _hotspots.Count - 1;
            }
            else
            {
                _currentHotspotIndex =
                    (_currentHotspotIndex - 1 + _hotspots.Count) % _hotspots.Count;
            }
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

                // IMPORTANT: The game uses the private camera_ field (NOT science_camera_) for hit detection
                // We must use the same camera to match the game's raycast behavior
                var cameraField = typeof(scienceInvestigationCtrl).GetField(
                    "camera_",
                    System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Instance
                );
                Camera camera = cameraField?.GetValue(ctrl) as Camera;

                // Get ray_adjust_x_ and ray_radius_ for accurate hit detection simulation
                var rayAdjustField = typeof(scienceInvestigationCtrl).GetField(
                    "ray_adjust_x_",
                    System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Instance
                );
                float rayAdjustX = 0f;
                if (rayAdjustField != null)
                {
                    rayAdjustX = (float)rayAdjustField.GetValue(ctrl);
                }

                var rayRadiusField = typeof(scienceInvestigationCtrl).GetField(
                    "ray_radius_",
                    System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Instance
                );
                float rayRadius = 0f;
                if (rayRadiusField != null)
                {
                    rayRadius = (float)rayRadiusField.GetValue(ctrl);
                }

#if DEBUG
                AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                    $"[3DNav] Using camera_: {(camera != null ? camera.name : "null")}, ray_adjust_x_: {rayAdjustX}, ray_radius_: {rayRadius}"
                );
#endif

                if (camera == null)
                {
                    // Fall back to science_camera if camera_ is not available
                    camera = ctrl.science_camera;
#if DEBUG
                    AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                        "[3DNav] WARNING: camera_ is null, falling back to science_camera"
                    );
#endif
                }

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

                // Get the layer mask for the evidence object (game uses this for raycasting)
                int evidenceLayer = 1 << manager.gameObject.layer;

                // Full 360-degree rotation search with finer steps
                // This is more thorough than just checking offsets from the "ideal" rotation
                // since the ideal calculation may have sign issues or not account for camera position

#if DEBUG
                AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                    $"[3DNav] Starting rotation search for collider '{hotspot.ColliderName}', layer={manager.gameObject.layer}"
                );
#endif

                // First, try a comprehensive rotation search
                // Step through horizontal angles in 30-degree increments, vertical in 15-degree increments
                for (float testH = -180f; testH <= 180f && !foundRotation; testH += 30f)
                {
                    for (float testV = -60f; testV <= 60f && !foundRotation; testV += 15f)
                    {
                        manager.SetRotate(testH, testV);

                        // Test from screen center using the same raycast the game uses
                        if (camera != null)
                        {
                            Vector3 screenPos = new Vector3(
                                Screen.width / 2f + rayAdjustX,
                                Screen.height / 2f,
                                0
                            );
                            Ray ray = camera.ScreenPointToRay(screenPos);
                            RaycastHit hit;

                            // Use SphereCast like the game does
                            if (
                                Physics.SphereCast(
                                    ray,
                                    rayRadius,
                                    out hit,
                                    camera.farClipPlane,
                                    evidenceLayer
                                )
                            )
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
                                        $"[3DNav] Found rotation at H={testH:F1}, V={testV:F1}"
                                    );
#endif
                                }
                            }
                        }
                    }
                }

                // If we didn't find it at screen center, try a grid search at each rotation
                if (!foundRotation && camera != null)
                {
#if DEBUG
                    AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                        "[3DNav] Screen center search failed, trying grid search at each rotation"
                    );
#endif

                    for (float testH = -180f; testH <= 180f && !foundRotation; testH += 45f)
                    {
                        for (float testV = -45f; testV <= 45f && !foundRotation; testV += 22.5f)
                        {
                            manager.SetRotate(testH, testV);

                            // Search in a grid pattern across the screen
                            for (int sy = -3; sy <= 3 && !foundRotation; sy++)
                            {
                                for (int sx = -3; sx <= 3 && !foundRotation; sx++)
                                {
                                    float screenX = Screen.width / 2f + sx * 80;
                                    float screenY = Screen.height / 2f + sy * 80;

                                    Vector3 screenPos = new Vector3(
                                        screenX + rayAdjustX,
                                        screenY,
                                        0
                                    );
                                    Ray ray = camera.ScreenPointToRay(screenPos);
                                    RaycastHit hit;

                                    if (
                                        Physics.SphereCast(
                                            ray,
                                            rayRadius,
                                            out hit,
                                            camera.farClipPlane,
                                            evidenceLayer
                                        )
                                    )
                                    {
                                        if (hit.collider == hotspot.Collider)
                                        {
                                            bestH = testH;
                                            bestV = testV;
                                            bestScreenPos = new Vector2(screenX, screenY);
                                            foundRotation = true;
#if DEBUG
                                            AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                                                $"[3DNav] Found at H={testH:F1}, V={testV:F1}, screen ({sx * 80}, {sy * 80})"
                                            );
#endif
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                // Last resort: try with regular Raycast instead of SphereCast
                // in case there's something unusual about the sphere cast parameters
                if (!foundRotation && camera != null)
                {
#if DEBUG
                    AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                        "[3DNav] SphereCast search failed, trying regular Raycast"
                    );
#endif

                    for (float testH = -180f; testH <= 180f && !foundRotation; testH += 30f)
                    {
                        for (float testV = -60f; testV <= 60f && !foundRotation; testV += 15f)
                        {
                            manager.SetRotate(testH, testV);

                            for (int sy = -2; sy <= 2 && !foundRotation; sy++)
                            {
                                for (int sx = -2; sx <= 2 && !foundRotation; sx++)
                                {
                                    float screenX = Screen.width / 2f + sx * 100;
                                    float screenY = Screen.height / 2f + sy * 100;

                                    Ray ray = camera.ScreenPointToRay(
                                        new Vector3(screenX, screenY, 0)
                                    );
                                    RaycastHit hit;

                                    // Try with ALL layers since evidenceLayer might be wrong
                                    if (Physics.Raycast(ray, out hit, camera.farClipPlane))
                                    {
                                        if (hit.collider == hotspot.Collider)
                                        {
                                            bestH = testH;
                                            bestV = testV;
                                            bestScreenPos = new Vector2(screenX, screenY);
                                            foundRotation = true;
#if DEBUG
                                            AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                                                $"[3DNav] Found via Raycast at H={testH:F1}, V={testV:F1}, screen ({sx * 100}, {sy * 100})"
                                            );
#endif
                                        }
#if DEBUG
                                        else
                                        {
                                            // Log what we're actually hitting to help debug
                                            if (testH == 0 && testV == 0 && sx == 0 && sy == 0)
                                            {
                                                AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                                                    $"[3DNav] At center (0,0), hit: '{hit.collider.name}' on layer {hit.collider.gameObject.layer}"
                                                );
                                            }
                                        }
#endif
                                    }
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

                    // Keep warning visible in release builds for troubleshooting
                    AccessibilityMod.Core.AccessibilityMod.Logger?.Warning(
                        $"[3DNav] No hit found after exhaustive search for '{hotspot.ColliderName}', using fallback position"
                    );

#if DEBUG
                    AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                        $"[3DNav] Fallback details: rotation H={bestH:F1}, V={bestV:F1}, "
                            + $"bounds center=({boundsCenter.x:F2}, {boundsCenter.y:F2}, {boundsCenter.z:F2}), "
                            + $"screen pos=({bestScreenPos.x:F1}, {bestScreenPos.y:F1})"
                    );

                    // Log additional debug info about why we might not be finding it
                    AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                        $"[3DNav] Debug: collider.enabled={hotspot.Collider.enabled}, "
                            + $"collider.gameObject.activeInHierarchy={hotspot.Collider.gameObject.activeInHierarchy}, "
                            + $"collider.gameObject.layer={hotspot.Collider.gameObject.layer}, "
                            + $"evidenceLayer mask={evidenceLayer}, "
                            + $"camera.cullingMask={camera?.cullingMask}"
                    );
#endif
                }

                // Apply the best rotation we found
                manager.SetRotate(bestH, bestV);

                // Move cursor to the position we found during rotation search
                // The game sets cursor_.transform.localPosition, so we need to convert screen coords to local coords
                if (camera != null)
                {
                    var cursorField = typeof(scienceInvestigationCtrl).GetField(
                        "cursor_",
                        System.Reflection.BindingFlags.NonPublic
                            | System.Reflection.BindingFlags.Instance
                    );

                    // Get cursor_init_pos_z_ for correct Z positioning
                    var cursorInitZField = typeof(scienceInvestigationCtrl).GetField(
                        "cursor_init_pos_z_",
                        System.Reflection.BindingFlags.NonPublic
                            | System.Reflection.BindingFlags.Instance
                    );
                    float cursorInitZ = -2700f; // Default value from decompiled code
                    if (cursorInitZField != null)
                    {
                        cursorInitZ = (float)cursorInitZField.GetValue(ctrl);
                    }

                    if (cursorField != null)
                    {
                        var cursor = cursorField.GetValue(ctrl) as AssetBundleSprite;
                        if (cursor != null)
                        {
                            // Calculate the distance from camera to cursor for proper ScreenToWorldPoint
                            float distanceFromCamera = Vector3.Distance(
                                camera.transform.position,
                                cursor.transform.position
                            );

                            // Convert screen position to world position
                            Vector3 cursorWorldPos = camera.ScreenToWorldPoint(
                                new Vector3(bestScreenPos.x, bestScreenPos.y, distanceFromCamera)
                            );

                            // Set world position, preserving Z
                            cursor.transform.position = new Vector3(
                                cursorWorldPos.x,
                                cursorWorldPos.y,
                                cursor.transform.position.z
                            );

                            // Also set the local Z to the game's expected value
                            Vector3 cursorLocalPos = cursor.transform.localPosition;
                            cursorLocalPos.z = cursorInitZ;
                            cursor.transform.localPosition = cursorLocalPos;

#if DEBUG
                            AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                                $"[3DNav] Cursor moved to screen: ({bestScreenPos.x:F1}, {bestScreenPos.y:F1}), local: ({cursor.transform.localPosition.x:F1}, {cursor.transform.localPosition.y:F1}, {cursor.transform.localPosition.z:F1})"
                            );

                            // Verify the hit detection will work by doing the same raycast the game does
                            Vector3 verifyScreenPos = camera.WorldToScreenPoint(
                                cursor.transform.position
                            );
                            verifyScreenPos.x += rayAdjustX;
                            Ray verifyRay = camera.ScreenPointToRay(verifyScreenPos);
                            RaycastHit verifyHit;
                            bool willHit = Physics.SphereCast(
                                verifyRay,
                                rayRadius,
                                out verifyHit,
                                camera.farClipPlane,
                                evidenceLayer
                            );
                            AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                                $"[3DNav] Verification raycast: willHit={willHit}, hitCollider={(willHit ? verifyHit.collider.name : "none")}, targetCollider={hotspot.ColliderName}"
                            );
                            if (!willHit || verifyHit.collider != hotspot.Collider)
                            {
                                // If verification failed, log what happened
                                if (willHit && verifyHit.collider != null)
                                {
                                    // We hit something else, the hotspot might be occluded
                                    AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                                        $"[3DNav] WARNING: Cursor raycast hit different collider: {verifyHit.collider.name}"
                                    );
                                }
                            }
#endif
                        }
                    }
                }

#if DEBUG
                // Check current hit state (won't update until next frame's ChangeGuideIconSprite call)
                int hitIndex = ctrl.hit_point_index;
                AccessibilityMod.Core.AccessibilityMod.Logger?.Msg(
                    $"[3DNav] hit_point_index (immediate, may not be updated yet): {hitIndex}"
                );
#endif

                // Announce the hotspot
                string message = L.Get(
                    "evidence_3d.hotspot_x_of_y",
                    _currentHotspotIndex + 1,
                    _hotspots.Count
                );
                SpeechManager.Announce(message, GameTextType.Menu);
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error navigating to hotspot: {ex.Message}\n{ex.StackTrace}"
                );
                // Still announce even if navigation failed
                SpeechManager.Announce(hotspot.Name, GameTextType.Menu);
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

                string hotspotStatus = overHotspot
                    ? L.Get("evidence_3d.on_hotspot")
                    : L.Get("evidence_3d.no_hotspot");
                string message = L.Get("evidence_3d.state", zoomLevel, hotspotStatus, hotspotCount);

                if (overHotspot)
                {
                    message += " " + L.Get("evidence_3d.press_enter");
                }
                else
                {
                    message += " " + L.Get("evidence_3d.use_brackets");
                }

                SpeechManager.Announce(message, GameTextType.Menu);
            }
            catch (Exception ex)
            {
                AccessibilityMod.Core.AccessibilityMod.Logger?.Error(
                    $"Error announcing 3D state: {ex.Message}"
                );
                SpeechManager.Announce(L.Get("evidence_3d.unable_to_read"), GameTextType.Menu);
            }
        }

        /// <summary>
        /// Announces just the zoom level.
        /// </summary>
        public static void AnnounceZoom()
        {
            string zoomLevel = GetZoomLevel();
            SpeechManager.Announce(
                L.Get("evidence_3d.zoom_percent", int.Parse(zoomLevel.Replace("%", ""))),
                GameTextType.Menu
            );
        }

        /// <summary>
        /// Announces whether a hotspot is under the cursor.
        /// </summary>
        public static void AnnounceHotspot()
        {
            if (IsOverHotspot())
            {
                SpeechManager.Announce(L.Get("evidence_3d.hotspot_detected"), GameTextType.Menu);
            }
            else
            {
                SpeechManager.Announce(L.Get("evidence_3d.no_hotspot_cursor"), GameTextType.Menu);
            }
        }

        #endregion
    }
}
