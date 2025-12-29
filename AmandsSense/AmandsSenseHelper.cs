// ============================================================================
// AmandsSenseHelper - Reflection utilities for accessing EFT internal APIs
// ============================================================================
// This class provides safe reflection-based access to internal EFT methods
// that are not exposed through the public API.
// ============================================================================

using System;
using System.Linq;
using System.Reflection;
using EFT;
using JsonType;
using SPT.Reflection.Utils;
using UnityEngine;

namespace AmandsSense
{
    /// <summary>
    /// Helper class providing reflection-based access to internal EFT methods.
    /// These methods are used for localization, role identification, and other
    /// functionality not exposed through the public API.
    /// </summary>
    public static class AmandsSenseHelper
    {
        // Localization reflection cache
        private static Type _localizedType;
        private static MethodInfo _localizedMethod;

        // Role identification reflection cache
        private static Type _roleType;
        private static MethodInfo _getScavRoleKeyMethod;
        private static MethodInfo _isFollowerMethod;
        private static MethodInfo _countAsBossForStatisticsMethod;
        private static MethodInfo _isBossMethod;

        // Transliteration reflection cache
        private static Type _transliterateType;
        private static MethodInfo _transliterateMethod;

        // Upscaler detection
        private static MethodInfo _usesFsr2UpscalerMethod;

        // Color conversion reflection cache
        private static Type _toColorType;
        private static MethodInfo _toColorMethod;

        /// <summary>
        /// Indicates whether the FSR2 upscaler detection method was found.
        /// </summary>
        public static bool UsesFsr2UpscalerMethodFound { get; private set; }

        /// <summary>
        /// Indicates whether the helper has been successfully initialized.
        /// </summary>
        public static bool IsInitialized { get; private set; }

        /// <summary>
        /// Initializes the reflection cache for all helper methods.
        /// Must be called before using any other methods in this class.
        /// </summary>
        public static void Init()
        {
            try
            {
                InitializeLocalization();
                InitializeRoleIdentification();
                InitializeTransliteration();
                InitializeUpscalerDetection();
                InitializeColorConversion();

                IsInitialized = true;
                AmandsSensePlugin.Log?.LogDebug("AmandsSenseHelper initialized successfully");
            }
            catch (Exception ex)
            {
                AmandsSensePlugin.Log?.LogError($"AmandsSenseHelper initialization failed: {ex.Message}");
                IsInitialized = false;
            }
        }

        private static void InitializeLocalization()
        {
            _localizedType = PatchConstants.EftTypes
                .SingleOrDefault(x => x.GetMethod("ParseLocalization", BindingFlags.Static | BindingFlags.Public) != null);

            if (_localizedType == null)
            {
                AmandsSensePlugin.Log?.LogWarning("Localization type not found");
                return;
            }

            _localizedMethod = _localizedType.GetMethods()
                .FirstOrDefault(x =>
                    x.Name == "Localized" &&
                    x.GetParameters().Length == 2 &&
                    x.GetParameters()[0].ParameterType == typeof(string) &&
                    x.GetParameters()[1].ParameterType == typeof(EStringCase));
        }

        private static void InitializeRoleIdentification()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public;

            _roleType = PatchConstants.EftTypes
                .SingleOrDefault(x =>
                    x.GetMethod("IsBoss", flags) != null &&
                    x.GetMethod("Init", flags) != null);

            if (_roleType == null)
            {
                AmandsSensePlugin.Log?.LogWarning("Role type not found");
                return;
            }

            _isBossMethod = _roleType.GetMethod("IsBoss", flags);
            _isFollowerMethod = _roleType.GetMethod("IsFollower", flags);
            _countAsBossForStatisticsMethod = _roleType.GetMethod("CountAsBossForStatistics", flags);
            _getScavRoleKeyMethod = _roleType.GetMethod("GetScavRoleKey", flags);
        }

        private static void InitializeTransliteration()
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;

            _transliterateType = PatchConstants.EftTypes
                .SingleOrDefault(x => x.GetMethods(flags).Any(t => t.Name == "Transliterate"));

            if (_transliterateType == null)
            {
                AmandsSensePlugin.Log?.LogWarning("Transliterate type not found");
                return;
            }

            _transliterateMethod = _transliterateType.GetMethods(flags)
                .SingleOrDefault(x => x.Name == "Transliterate" && x.GetParameters().Length == 1);
        }

        private static void InitializeUpscalerDetection()
        {
            _usesFsr2UpscalerMethod = typeof(SSAA)
                .GetMethod("UsesFSR2Upscaler", BindingFlags.Public | BindingFlags.Instance);
            UsesFsr2UpscalerMethodFound = _usesFsr2UpscalerMethod != null;
        }

        private static void InitializeColorConversion()
        {
            _toColorType = PatchConstants.EftTypes
                .SingleOrDefault(x => x.GetMethod("ToColor", BindingFlags.Static | BindingFlags.Public) != null);

            if (_toColorType == null)
            {
                AmandsSensePlugin.Log?.LogWarning("ToColor type not found");
                return;
            }

            _toColorMethod = _toColorType.GetMethods()
                .FirstOrDefault(x => x.Name == "ToColor");
        }

        /// <summary>
        /// Gets the localized string for the given ID.
        /// </summary>
        /// <param name="id">The localization key.</param>
        /// <param name="case">The string case to apply.</param>
        /// <returns>The localized string, or the ID if localization fails.</returns>
        public static string Localized(string id, EStringCase @case)
        {
            if (_localizedMethod == null || string.IsNullOrEmpty(id))
                return id ?? string.Empty;

            try
            {
                return (string)_localizedMethod.Invoke(null, new object[] { id, @case });
            }
            catch
            {
                return id;
            }
        }

        /// <summary>
        /// Checks if the given spawn type is a boss.
        /// </summary>
        /// <param name="role">The spawn type to check.</param>
        /// <returns>True if the spawn type is a boss, false otherwise.</returns>
        public static bool IsBoss(WildSpawnType role)
        {
            if (_isBossMethod == null)
                return false;

            try
            {
                return (bool)_isBossMethod.Invoke(null, new object[] { role });
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if the given spawn type is a follower.
        /// </summary>
        /// <param name="role">The spawn type to check.</param>
        /// <returns>True if the spawn type is a follower, false otherwise.</returns>
        public static bool IsFollower(WildSpawnType role)
        {
            if (_isFollowerMethod == null)
                return false;

            try
            {
                return (bool)_isFollowerMethod.Invoke(null, new object[] { role });
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if the given spawn type counts as a boss for statistics.
        /// </summary>
        /// <param name="role">The spawn type to check.</param>
        /// <returns>True if the spawn type counts as a boss for statistics, false otherwise.</returns>
        public static bool CountAsBossForStatistics(WildSpawnType role)
        {
            if (_countAsBossForStatisticsMethod == null)
                return false;

            try
            {
                return (bool)_countAsBossForStatisticsMethod.Invoke(null, new object[] { role });
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the localization key for the given scav role.
        /// </summary>
        /// <param name="role">The spawn type.</param>
        /// <returns>The localization key, or an empty string if not found.</returns>
        public static string GetScavRoleKey(WildSpawnType role)
        {
            if (_getScavRoleKeyMethod == null)
                return string.Empty;

            try
            {
                return (string)_getScavRoleKeyMethod.Invoke(null, new object[] { role });
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Transliterates the given text (converts Cyrillic to Latin characters).
        /// </summary>
        /// <param name="text">The text to transliterate.</param>
        /// <returns>The transliterated text, or the original text if transliteration fails.</returns>
        public static string Transliterate(string text)
        {
            if (_transliterateMethod == null || string.IsNullOrEmpty(text))
                return text ?? string.Empty;

            try
            {
                return (string)_transliterateMethod.Invoke(null, new object[] { text });
            }
            catch
            {
                return text;
            }
        }

        /// <summary>
        /// Checks if FSR2 upscaler is currently in use.
        /// </summary>
        /// <returns>True if FSR2 upscaler is in use, false otherwise.</returns>
        public static bool UsesFsr2Upscaler()
        {
            if (!UsesFsr2UpscalerMethodFound || _usesFsr2UpscalerMethod == null)
                return false;

            try
            {
                return (bool)_usesFsr2UpscalerMethod.Invoke(null, Array.Empty<object>());
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Converts a TaxonomyColor to a Unity Color.
        /// </summary>
        /// <param name="taxonomyColor">The taxonomy color to convert.</param>
        /// <returns>The corresponding Unity Color, or white if conversion fails.</returns>
        public static Color ToColor(TaxonomyColor taxonomyColor)
        {
            if (_toColorMethod == null)
                return Color.white;

            try
            {
                return (Color)_toColorMethod.Invoke(null, new object[] { taxonomyColor });
            }
            catch
            {
                return Color.white;
            }
        }
    }
}
