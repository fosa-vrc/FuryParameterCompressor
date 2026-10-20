// Source: nadena.dev.modular-avatar/Editor/ParamsUsage/ParamsUsageWindow.cs
// Source: nadena.dev.modular-avatar/Editor/Inspector/Parameters/AvatarParametersEditor.cs
// Changes:
//   - Layout lives in ParameterPickerWindow.uxml / FPCStyles.uss and is localized through the "ndmf-tr" class, matching MA.
//     Nothing is styled with `opacity`, which would make UI Toolkit render the subtree to an intermediate texture and blur the text.
//   - Selection is purely additive: deselecting an entry here never removes anything from the whitelist,
//     it only means "don't add it". Removing is done in the component's list.
//   - The window targets the component directly (rather than taking a callback) so that it survives assembly reloads,
//     and is opened through GetWindow so repeated clicks focus it instead of stacking up copies.

using System;
using System.Collections.Generic;
using System.Linq;
using nadena.dev.ndmf;
using nadena.dev.ndmf.runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FOSA.FPC.Inspector
{
    internal class ParameterPickerWindow : EditorWindow
    {
        // "Close After Add" is a user preference, not per-window state:
        // without persistence it silently resets to the UXML default every time the window is opened.
        private const string CloseOnAddPrefKey = "net.fosa.fury-parameter-compressor.picker.closeOnAdd";

        [SerializeField] private FuryParameterCompressor _target;

        private readonly List<string> _names = new List<string>();
        private readonly HashSet<string> _existing = new HashSet<string>();

        private ListView _listView;
        private Toggle _closeOnAddToggle;

        private void OnEnable()
        {
            Localization.OnLangChange += UpdateTitle;
            // Also covers domain reloads, where the serialized titleContent would otherwise keep whatever language the window was opened in.
            UpdateTitle();
        }

        private void OnDisable()
        {
            Localization.OnLangChange -= UpdateTitle;
        }

        // The body re-localizes itself through NDMF's per-element callbacks ("ndmf-tr"),
        // but the window title is plain EditorWindow state and has to be refreshed by hand.
        private void UpdateTitle()
        {
            titleContent = new GUIContent(Localization.S("fpc.picker.title"));
        }

        // GetWindow rather than CreateInstance:
        // clicking Pick repeatedly should focus the window that is already open instead of stacking up copies.
        // GetWindow only runs CreateGUI the first time, so a window that is already built has to be re-collected by hand -
        // the avatar's parameters or the whitelist may well have changed since it was opened.
        public static void Open(FuryParameterCompressor target)
        {
            var window = GetWindow<ParameterPickerWindow>(true, Localization.S("fpc.picker.title"), true);
            window.minSize = new Vector2(340, 300);
            window._target = target;

            // Null until CreateGUI has run; when it hasn't, CreateGUI collects for itself.
            if (window._listView != null) window.Refresh();
        }

        private void CreateGUI()
        {
            CollectNames();

            var root = UIElementsUtil.Clone("ParameterPickerWindow");
            root.style.flexGrow = 1;
            rootVisualElement.Add(root);

            // SelectionType.Multiple is what makes Ctrl-click / Shift-click selection work.
            _listView = root.Q<ListView>("PickerList");
            _listView.itemsSource = _names;
            _listView.selectionType = SelectionType.Multiple;
            _listView.fixedItemHeight = 20;
            _listView.makeItem = () => new Label();
            _listView.bindItem = (element, index) =>
            {
                ((Label)element).text = _names[index];
            };
            _closeOnAddToggle = root.Q<Toggle>("chk-close-on-add");
            _closeOnAddToggle.value = EditorPrefs.GetBool(CloseOnAddPrefKey, true);
            _closeOnAddToggle.RegisterValueChangedCallback(evt => EditorPrefs.SetBool(CloseOnAddPrefKey, evt.newValue));
            root.Q<Button>("btn-select-all").clicked += SelectAll;
            root.Q<Button>("btn-clear").clicked += () => _listView.ClearSelection();
            root.Q<Button>("btn-invert").clicked += InvertSelection;
            root.Q<Button>("btn-refresh").clicked += Refresh;
            root.Q<Button>("btn-add").clicked += () =>
            {
                AddSelected();
            };
            root.Q<Button>("btn-cancel").clicked += Close;
            _listView.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter) return;
                AddSelected();
                // TrickleDown + StopPropagation, because the ListView's own keyboard navigation
                // (a manipulator on its inner scroll container, below us in the propagation path)
                // treats Return as "submit" and scrolls the focused row into view -
                // which after the Rebuild inside Refresh() means jumping to the end of the list, unlike clicking Add.
                // Intercepting on the way down runs before it can.
                evt.PreventDefault();
                evt.StopPropagation();
            }, TrickleDown.TrickleDown);
        }

        // Source: MA's ParamsUsageWindow, which enumerates the avatar's parameters through NDMF's introspection API -
        // so Modular Avatar components (and anything else registered with NDMF) are included.
        private void CollectNames()
        {
            _names.Clear();
            _existing.Clear();
            if (_target == null) return;

            var avatarRoot = RuntimeUtil.FindAvatarInParents(_target.transform);
            if (avatarRoot == null) return;

            foreach (var name in _target.EffectiveParameterWhitelist)
            {
                _existing.Add(name);
            }
            _names.AddRange(ParameterInfo.ForUI.GetParametersForObject(avatarRoot.gameObject)
                .Where(p => p.Namespace == ParameterNamespace.Animator)
                .Where(p => !p.IsAnimatorOnly && p.WantSynced && !p.IsHidden)
                .Select(p => p.EffectiveName)
                .Where(n => !string.IsNullOrEmpty(n) && !_existing.Contains(n))
                .Distinct()
                .OrderBy(n => n, StringComparer.Ordinal));
        }

        private void Refresh()
        {
            // Rebuild can move the scroller (keyboard-triggered adds); restore it so the list stays where the user was.
            // scrollOffset lives on the ListView's inner ScrollView - ListView itself only exposes ScrollTo*.
            // Deferred one tick: the scroll range is only valid after the rebuilt list has been laid out;
            // the ScrollView clamps if it shrank.
            var scroller = _listView.Q<ScrollView>();
            var scrollOffset = scroller?.scrollOffset ?? default;
            CollectNames();
            _listView.itemsSource = _names;
            _listView.ClearSelection();
            _listView.Rebuild();
            if (scroller != null)
            {
                _listView.schedule.Execute(() => scroller.scrollOffset = scrollOffset);
            }
        }

        private void SelectAll()
        {
            _listView.SetSelection(Enumerable.Range(0, _names.Count));
        }

        private void InvertSelection()
        {
            var selected = new HashSet<int>(_listView.selectedIndices);
            _listView.SetSelection(Enumerable.Range(0, _names.Count).Where(i => !selected.Contains(i)));
        }

        // Appends every selected parameter. Nothing is ever removed here,
        // and no de-duplication is done - the whitelist is allowed to contain duplicates,
        // which are only collapsed at build time.
        private void AddSelected()
        {
            if (_target == null) return;

            var selected = _listView.selectedIndices
                .Where(i => i >= 0 && i < _names.Count)
                .OrderBy(i => i)
                .Select(i => _names[i])
                .ToList();
            if (selected.Count == 0) return;

            using var serializedObject = new SerializedObject(_target);
            var list = serializedObject.FindProperty(nameof(FuryParameterCompressor.parameterWhitelist));

            foreach (var name in selected)
            {
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).stringValue = name;
            }

            serializedObject.ApplyModifiedProperties();
            if (_closeOnAddToggle.value) Close();
            else Refresh();
        }
    }
}
