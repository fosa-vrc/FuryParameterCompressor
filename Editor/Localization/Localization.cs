// Source: nadena.dev.modular-avatar/Editor/Localization/Localization.cs
// Changes:
//   - The localization folder is resolved from the package root (FPCPackageUtils) instead of an asset GUID,
//     so that no .meta GUID needs to be kept in sync.
//   - Dropped UIElementLocalizer (MA-internal); NDMF's Localizer.LocalizeUIElements covers it.
//   - Dropped MA's G() GUIContent helper: the whole GUI is UIElements,
//     so tooltips come from NDMF's "<key>:tooltip" convention instead of MA's IMGUI "<key>.tooltip".
//   - Dropped the "Reload localizations" menu item and ShowLanguageUI (MA UI menu constants are internal).

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using nadena.dev.ndmf.localization;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace FOSA.FPC
{
    [InitializeOnLoad] // ensure that we register languages with NDMF on domain load
    internal static class Localization
    {
        public static event Action OnLangChange;

        // NDMF's Localizer constructor calls its loader immediately,
        // so this is reached from the static constructor of an [InitializeOnLoad] class.
        // FPCPackageUtils resolves the package through PackageInfo (which doesn't touch the AssetDatabase)
        // and retries its AssetDatabase fallback later rather than caching a failure, so it is safe to read this early.
        private static string LocalizationPathRoot => Path.Combine(FPCPackageUtils.ResolvedPath, "Editor", "Localization");

        private static ImmutableList<string>
            SupportedLanguages = new string[] {"en-US", "zh-Hans"}.ToImmutableList();

        public static Localizer L { get; }

        static Localization()
        {
            Localizer localizer = new Localizer(SupportedLanguages[0], () =>
            {
                List<(string, Func<string, string>)> languages = new List<(string, Func<string, string>)>();

                foreach (var lang in SupportedLanguages)
                {
                    languages.Add((lang, LanguageLookup(lang)));
                }

                return languages;
            });

            L = localizer;

            LanguagePrefs.RegisterLanguageChangeCallback(typeof(Localization), _ => OnLangChange?.Invoke());
        }

        private static Func<string,string> LanguageLookup(string lang)
        {
            var filename = Path.Combine(LocalizationPathRoot, lang + ".json");

            try
            {
                var langData = File.ReadAllText(filename);
                var langMap = JsonConvert.DeserializeObject<Dictionary<string, string>>(langData);

                // return langMap.GetValueOrDefault; - Unity 2019 doesn't have this extension method
                return key =>
                {
                    if (langMap.TryGetValue(key, out var val)) return val;
                    else return null;
                };
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to load language file " + filename);
                Debug.LogException(e);
                return (k) => null;
            }
        }

        public static string S(string key)
        {
            return S(key, key);
        }

        public static string S_f(string key, params object[] format)
        {
            try
            {
                return string.Format(S(key, key), format);
            }
            catch (FormatException)
            {
                return S(key, key) + "(" + string.Join(", ", format) + ")";
            }
        }

        public static string S(string key, string defValue)
        {
            if (L.TryGetLocalizedString(key, out var val))
            {
                return val;
            }
            else
            {
                return defValue;
            }
        }
    }
}
