// Source: nadena.dev.modular-avatar/Editor/Inspector/UIElementsUtil.cs
// Changes:
//   - MA holds its UXML/USS in [SerializeField] fields wired up through the script's .meta file.
//     We load them from the resolved package root instead, the same way Localization.cs finds its json,
//     so the package carries no asset GUID references.
//   - MA's UpdateWhileAttached helper is not used here and was dropped.

using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FOSA.FPC.Inspector
{
    internal static class UIElementsUtil
    {
        private static string UIPath => FPCPackageUtils.AssetPath + "/Editor/Inspector";

        /// <summary>
        ///     Clones the named UXML file, attaches the shared stylesheet,
        ///     and localizes every element tagged with "ndmf-tr"
        ///     (NDMF's convention: the element's text/label attribute holds the localization key,
        ///     and "{key}:tooltip" becomes its tooltip).
        /// </summary>
        public static VisualElement Clone(string name)
        {
            var uxmlPath = $"{UIPath}/{name}.uxml";
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);

            VisualElement root;
            if (uxml != null)
            {
                root = uxml.CloneTree();
            }
            else
            {
                // The caller's root.Q<T>("...") lookups still return null on this substitute root,
                // so a NullReferenceException follows shortly - unavoidable here -
                // but the error logged above names the real cause (a broken install), which the bare NRE would not.
                Debug.LogError($"[FPC] Failed to load UI layout at {uxmlPath}. Is the package installed correctly?");
                root = new VisualElement();
                root.Add(new HelpBox($"Failed to load {uxmlPath}", HelpBoxMessageType.Error));
            }

            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>($"{UIPath}/FPCStyles.uss");
            if (uss != null) root.styleSheets.Add(uss);

            Localization.L.LocalizeUIElements(root);

            return root;
        }

        public static void SetVisible(this VisualElement element, bool visible)
        {
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
