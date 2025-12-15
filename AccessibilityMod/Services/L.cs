namespace AccessibilityMod.Services
{
    /// <summary>
    /// Convenience alias for LocalizationService.
    /// Provides short-form access to localized strings: L.Get("key") instead of LocalizationService.Get("key")
    /// </summary>
    public static class L
    {
        /// <summary>
        /// Get a localized string by key.
        /// </summary>
        public static string Get(string key)
        {
            return LocalizationService.Get(key);
        }

        /// <summary>
        /// Get a localized string with format arguments.
        /// Example: L.Get("navigation.point_x_of_y", 1, 5) returns "Point 1 of 5"
        /// </summary>
        public static string Get(string key, params object[] args)
        {
            return LocalizationService.Get(key, args);
        }
    }
}
