// Source: nadena.dev.modular-avatar/Editor/Inspector/MAEditorBase.cs
// Changes:
//   - Only the part that rebuilds the inspector when the editor language changes was kept.
//     MA's logo element, its IMGUI fallback path and its UseDefaultMargins handling are not needed here.

using UnityEditor;
using UnityEngine.UIElements;

namespace FOSA.FPC.Inspector
{
    internal abstract class FPCEditorBase : Editor
    {
        private VisualElement _visualElement;

        protected abstract VisualElement CreateInnerInspectorGUI();

        public sealed override VisualElement CreateInspectorGUI()
        {
            if (_visualElement == null)
            {
                _visualElement = new VisualElement();
                Localization.OnLangChange += RebuildUI;
            }
            else
            {
                _visualElement.Clear();
            }

            _visualElement.Add(CreateInnerInspectorGUI());

            return _visualElement;
        }

        private void RebuildUI()
        {
            CreateInspectorGUI();
        }

        protected virtual void OnDestroy()
        {
            Localization.OnLangChange -= RebuildUI;
        }
    }
}
