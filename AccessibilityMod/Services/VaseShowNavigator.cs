using System;
using AccessibilityMod.Core;
using UnityAccessibilityLib;
using UnityEngine;

namespace AccessibilityMod.Services
{
    /// <summary>
    /// Navigator for the "unstable jar" rotation minigame (VaseShowMiniGame)
    /// Player must rotate a jar to match the Blue Badger silhouette
    /// </summary>
    public static class VaseShowNavigator
    {
        // Target angles from decompiled code
        // Answer 1: X=60, Y=0, Z=180 (or Z=-180)
        // Answer 2: X=120, Y=180 (or Y=-180), Z=0
        private static readonly Vector3 Answer1 = new Vector3(60f, 0f, 180f);
        private static readonly Vector3 Answer2 = new Vector3(120f, 180f, 0f);
        private static readonly Vector3 SafeRange = new Vector3(5f, 3f, 3f);

        private static Vector3 _lastAnnouncedRotation = Vector3.zero;
        private static bool _wasActive = false;

        /// <summary>
        /// Check if VaseShowMiniGame is currently active
        /// </summary>
        public static bool IsActive()
        {
            try
            {
                var instance = VaseShowMiniGame.instance;
                if (instance == null)
                    return false;

                // Check if the body is active
                var bodyField = typeof(VaseShowMiniGame).GetField(
                    "body_",
                    System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Instance
                );
                if (bodyField != null)
                {
                    var body = bodyField.GetValue(instance) as GameObject;
                    return body != null && body.activeSelf;
                }
            }
            catch
            {
                // Ignore errors
            }
            return false;
        }

        /// <summary>
        /// Get the current rotation of the vase
        /// </summary>
        public static Vector3 GetCurrentRotation()
        {
            try
            {
                var instance = VaseShowMiniGame.instance;
                if (instance == null)
                    return Vector3.zero;

                var vaseRotateField = typeof(VaseShowMiniGame).GetField(
                    "vase_rotate_",
                    System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Instance
                );
                if (vaseRotateField != null)
                {
                    return (Vector3)vaseRotateField.GetValue(instance);
                }
            }
            catch
            {
                // Ignore errors
            }
            return Vector3.zero;
        }

        /// <summary>
        /// Update method called each frame to track state changes
        /// </summary>
        public static void Update()
        {
            bool isActive = IsActive();

            // Announce when entering the minigame
            if (isActive && !_wasActive)
            {
                AnnounceEntry();
            }

            _wasActive = isActive;
        }

        /// <summary>
        /// Announce entry into the minigame
        /// </summary>
        private static void AnnounceEntry()
        {
            SpeechManager.Announce(L.Get("vase_show.start"), GameTextType.Menu);
            _lastAnnouncedRotation = Vector3.zero;
        }

        /// <summary>
        /// Announce current rotation state
        /// </summary>
        public static void AnnounceState()
        {
            if (!IsActive())
                return;

            Vector3 rotation = GetCurrentRotation();

            // Normalize angles to -180 to 180 range for clearer reporting
            float x = NormalizeAngle(rotation.x);
            float y = NormalizeAngle(rotation.y);
            float z = NormalizeAngle(rotation.z);

            string message = L.Get("vase_show.rotation_xyz", (int)x, (int)y, (int)z) + " ";

            // Calculate distance to both answers
            float dist1 = GetDistanceToAnswer(rotation, Answer1);
            float dist2 = GetDistanceToAnswer(rotation, Answer2);

            float closestDist = Math.Min(dist1, dist2);
            int answerNum = dist1 <= dist2 ? 1 : 2;

            if (closestDist < 10)
            {
                message += L.Get("vase_show.very_close") + " ";
            }
            else if (closestDist < 30)
            {
                message += L.Get("vase_show.getting_closer") + " ";
            }

            // Check which axes are correct
            Vector3 target = answerNum == 1 ? Answer1 : Answer2;
            bool xCorrect = IsAxisCorrect(rotation.x, target.x, SafeRange.x);
            bool yCorrect = IsAxisCorrect(rotation.y, target.y, SafeRange.y);
            bool zCorrect = IsAxisCorrect(rotation.z, target.z, SafeRange.z);

            if (xCorrect && yCorrect && zCorrect)
            {
                message += L.Get("vase_show.all_aligned");
            }
            else
            {
                if (xCorrect)
                    message += L.Get("vase_show.x_correct") + " ";
                if (yCorrect)
                    message += L.Get("vase_show.y_correct") + " ";
                if (zCorrect)
                    message += L.Get("vase_show.z_correct") + " ";
            }

            SpeechManager.Announce(message, GameTextType.Menu);
            _lastAnnouncedRotation = rotation;
        }

        /// <summary>
        /// Announce hint for solving the puzzle
        /// </summary>
        public static void AnnounceHint()
        {
            if (!IsActive())
                return;

            Vector3 rotation = GetCurrentRotation();

            // Calculate distance to both answers
            float dist1 = GetDistanceToAnswer(rotation, Answer1);
            float dist2 = GetDistanceToAnswer(rotation, Answer2);

            // Choose closer answer
            Vector3 target = dist1 <= dist2 ? Answer1 : Answer2;
            int answerNum = dist1 <= dist2 ? 1 : 2;

            string hint =
                L.Get("vase_show.target", answerNum, (int)target.x, (int)target.y, (int)target.z)
                + " ";

            // Current normalized
            float x = NormalizeAngle(rotation.x);
            float y = NormalizeAngle(rotation.y);
            float z = NormalizeAngle(rotation.z);

            hint += L.Get("vase_show.current", (int)x, (int)y, (int)z) + " ";

            // Calculate needed adjustments
            float xDiff = GetAngleDifference(x, target.x);
            float yDiff = GetAngleDifference(y, target.y);
            float zDiff = GetAngleDifference(z, NormalizeAngle(target.z));

            // Provide specific guidance
            if (Math.Abs(xDiff) > SafeRange.x)
            {
                string direction = xDiff > 0 ? L.Get("key.h") : L.Get("key.n");
                hint += L.Get("vase_show.x_needs", (int)Math.Abs(xDiff), direction) + " ";
            }
            else
            {
                hint += L.Get("vase_show.x_aligned") + " ";
            }

            if (Math.Abs(yDiff) > SafeRange.y)
            {
                string direction = yDiff < 0 ? L.Get("key.m") : L.Get("key.b");
                hint += L.Get("vase_show.y_needs", (int)Math.Abs(yDiff), direction) + " ";
            }
            else
            {
                hint += L.Get("vase_show.y_aligned") + " ";
            }

            if (Math.Abs(zDiff) > SafeRange.z)
            {
                string direction = zDiff > 0 ? L.Get("key.r") : L.Get("key.q");
                hint += L.Get("vase_show.z_needs", (int)Math.Abs(zDiff), direction) + " ";
            }
            else
            {
                hint += L.Get("vase_show.z_aligned") + " ";
            }

            SpeechManager.Announce(hint, GameTextType.Menu);
        }

        /// <summary>
        /// Normalize angle to -180 to 180 range
        /// </summary>
        private static float NormalizeAngle(float angle)
        {
            while (angle > 180f)
                angle -= 360f;
            while (angle < -180f)
                angle += 360f;
            return angle;
        }

        /// <summary>
        /// Get the shortest angular difference between two angles
        /// </summary>
        private static float GetAngleDifference(float current, float target)
        {
            float diff = target - current;
            while (diff > 180f)
                diff -= 360f;
            while (diff < -180f)
                diff += 360f;
            return diff;
        }

        /// <summary>
        /// Check if an axis is within the safe range of the target
        /// </summary>
        private static bool IsAxisCorrect(float current, float target, float range)
        {
            float diff = Math.Abs(
                GetAngleDifference(NormalizeAngle(current), NormalizeAngle(target))
            );
            return diff <= range;
        }

        /// <summary>
        /// Calculate total distance to an answer (considering angle wrapping)
        /// </summary>
        private static float GetDistanceToAnswer(Vector3 current, Vector3 target)
        {
            float xDiff = Math.Abs(GetAngleDifference(NormalizeAngle(current.x), target.x));
            float yDiff = Math.Abs(
                GetAngleDifference(NormalizeAngle(current.y), NormalizeAngle(target.y))
            );
            float zDiff = Math.Abs(
                GetAngleDifference(NormalizeAngle(current.z), NormalizeAngle(target.z))
            );

            return xDiff + yDiff + zDiff;
        }

        /// <summary>
        /// Reset state
        /// </summary>
        public static void Reset()
        {
            _wasActive = false;
            _lastAnnouncedRotation = Vector3.zero;
        }
    }
}
