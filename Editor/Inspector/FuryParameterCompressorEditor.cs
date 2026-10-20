// Source: nadena.dev.modular-avatar/Editor/Inspector/Parameters/AvatarParametersEditor.cs
// Source: nadena.dev.modular-avatar/Editor/ParamsUsage/ParamsUsageWindow.cs
// Changes:
//   - Layout lives in FuryParameterCompressorEditor.uxml / FPCStyles.uss and is localized through the "ndmf-tr" class,
//     matching MA. Nothing is styled with `opacity`,
//     which would make UI Toolkit render the subtree to an intermediate texture and blur the text.
//   - Unlike MA's ImportValues, importing does not skip names that are already present:
//     the whitelist deliberately allows duplicates, which are only collapsed at build time.
//   - Adds Pick / Delete Selected / Clear buttons; MA relies on the Delete key alone.

using System.Linq;
using FOSA.FPC.Plugin;
using FOSA.FPC.SyncParameterSequence;
using FOSA.FPC.Utils;
using nadena.dev.ndmf;
using nadena.dev.ndmf.runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace FOSA.FPC.Inspector
{
    [CustomEditor(typeof(FuryParameterCompressor))]
    internal class FuryParameterCompressorEditor : FPCEditorBase
    {
        private ListView _whitelistView;

        protected override VisualElement CreateInnerInspectorGUI()
        {
            var root = UIElementsUtil.Clone("FuryParameterCompressorEditor");
            var component = (FuryParameterCompressor)target;
            var avatarRoot = RuntimeUtil.FindAvatarInParents(component.transform);

            root.Q<HelpBox>("VrcFuryWarning").SetVisible(DetectConflictsPass.VrcFuryPresent);
            root.Q<HelpBox>("MaSyncWarning").SetVisible(avatarRoot != null &&
                MaSyncParameterSequenceDetector.Exists(avatarRoot.gameObject));

            SetupWhitelistView(root);

            root.Q<Button>("btn-add").clicked += () => Append("");

            var pick = root.Q<Button>("btn-pick");
            pick.clicked += () => ParameterPickerWindow.Open(component);
            pick.SetEnabled(avatarRoot != null);

            root.Q<Button>("btn-delete").clicked += DeleteSelected;
            root.Q<Button>("btn-clear").clicked += ClearAll;

            SetupImportField(root);

            var usage = root.Q<Label>("usage");
            if (avatarRoot != null)
            {
                // GetMaxCost() rather than MAX_PARAMETER_COST directly (which is what MA Information uses):
                // it clamps the nonsense value some modified VRCSDKs report,
                // so this figure can't contradict the one the build reports.
                usage.text = Localization.S_f("fpc.usage",
                    ParameterInfo.ForUI.GetParametersForObject(avatarRoot.gameObject).Sum(p => p.BitUsage),
                    VRCExpressionParametersExtensions.GetMaxCost());
            }
            else
            {
                usage.SetVisible(false);
            }

            root.Bind(serializedObject);
            return root;
        }

        // Source: nadena.dev.modular-avatar/Editor/Inspector/Parameters/AvatarParametersEditor.cs
        // SelectionType.Multiple is what makes Ctrl-click / Shift-click selection work;
        // the reorder handles declared in the UXML also give each row a click target that isn't the text field.
        private void SetupWhitelistView(VisualElement root)
        {
            _whitelistView = root.Q<ListView>("Whitelist");
            _whitelistView.showBoundCollectionSize = false;
            _whitelistView.selectionType = SelectionType.Multiple;
            _whitelistView.makeItem = () =>
            {
                var row = new VisualElement();
                row.AddToClassList("fpc-whitelist-row");
                var field = new TextField();
                field.AddToClassList("fpc-whitelist-field");
                field.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
                // A keypress inside the field arrives with the field's inner text-input element as its target (not the TextField itself),
                // so the ListView handler below cannot tell "Delete pressed while typing" apart from "Delete pressed on the selection".
                // Stop it from bubbling up here instead, the same way MA's ParameterConfigDrawer does;
                // StopPropagation doesn't affect the text editing itself.
                field.RegisterCallback<KeyDownEvent>(evt =>
                {
                    if (evt.keyCode == KeyCode.Delete && evt.modifiers == EventModifiers.FunctionKey)
                        evt.StopPropagation();
                });
                var deleteBtn = new Button { text = "-" };
                deleteBtn.AddToClassList("fpc-whitelist-delete-btn");
                row.Add(field);
                row.Add(deleteBtn);
                return row;
            };
            _whitelistView.bindItem = (element, index) =>
            {
                var list = WhitelistProperty();
                if (index < 0 || index >= list.arraySize) return;

                var prop = list.GetArrayElementAtIndex(index);
                var field = element.Q<TextField>();
                field.BindProperty(prop);
                var deleteBtn = element.Q<Button>();
                deleteBtn.clickable = new Clickable(() => Delete(index));
            };
            // The physical Delete key carries the FunctionKey modifier (the same check MA uses);
            // keypresses inside a row's text field never get here - the field stops them in makeItem above.
            _whitelistView.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Delete || evt.modifiers != EventModifiers.FunctionKey) return;
                DeleteSelected();
                evt.StopPropagation();
            }, TrickleDown.NoTrickleDown);
        }

        // Source: nadena.dev.modular-avatar/Editor/Inspector/Parameters/AvatarParametersEditor.cs
        // an object field that imports every parameter from a VRCExpressionParameters asset and then resets itself.
        // Unlike MA we do not skip names that are already in the list.
        private void SetupImportField(VisualElement root)
        {
            var import = root.Q<ObjectField>("p_import");
            import.objectType = typeof(VRCExpressionParameters);
            import.allowSceneObjects = false;

            import.RegisterValueChangedCallback(evt =>
            {
                var source = evt.newValue as VRCExpressionParameters;
                import.SetValueWithoutNotify(null);
                if (source == null || source.parameters == null) return;

                serializedObject.Update();
                var list = WhitelistProperty();
                foreach (var parameter in source.parameters)
                {
                    if (string.IsNullOrEmpty(parameter.name)) continue;
                    AppendTo(list, parameter.name);
                }

                serializedObject.ApplyModifiedProperties();
                _whitelistView.RefreshItems();
            });
        }

        private SerializedProperty WhitelistProperty()
        {
            return serializedObject.FindProperty(nameof(FuryParameterCompressor.parameterWhitelist));
        }

        private static void AppendTo(SerializedProperty list, string value)
        {
            list.InsertArrayElementAtIndex(list.arraySize);
            list.GetArrayElementAtIndex(list.arraySize - 1).stringValue = value;
        }

        private void Append(string value)
        {
            serializedObject.Update();
            AppendTo(WhitelistProperty(), value);
            serializedObject.ApplyModifiedProperties();
            _whitelistView.RefreshItems();
        }

        private void Delete(int index)
        {
            var list = WhitelistProperty();
            if (list == null) return;
            int targetIndex = index - 1;
            _whitelistView.ClearSelection();
            serializedObject.Update();
            // The row index was captured at bind time;
            // an Undo or an external edit can have shrunk the array since (the same guard bindItem has).
            if (index < 0 || index >= list.arraySize) return;
            list.DeleteArrayElementAtIndex(index);
            serializedObject.ApplyModifiedProperties();
            int newSize = list.arraySize;
            if (newSize > 0)
            {
                targetIndex = Mathf.Clamp(targetIndex, 0, newSize - 1);
                _whitelistView.SetSelection(targetIndex);
            }
            _whitelistView.RefreshItems();
        }

        private void DeleteSelected()
        {
            var indices = _whitelistView.selectedIndices.ToList();
            if (indices.Count == 0) return;
            var list = WhitelistProperty();
            if (list == null) return;
            int minIndex = indices.Min();
            int targetIndex = minIndex - 1;
            _whitelistView.ClearSelection();
            serializedObject.Update();
            // Selection indices can be stale if an Undo or an external edit shrunk the array since the ListView last rebound;
            // deleting in descending order keeps the remaining ones valid.
            foreach (var index in indices.Where(i => i >= 0 && i < list.arraySize).OrderByDescending(i => i))
            {
                list.DeleteArrayElementAtIndex(index);
            }
            serializedObject.ApplyModifiedProperties();
            int newSize = list.arraySize;
            if (newSize > 0)
            {
                targetIndex = Mathf.Clamp(targetIndex, 0, newSize - 1);
                _whitelistView.SetSelection(targetIndex);
            }
            _whitelistView.RefreshItems();
        }

        private void ClearAll()
        {
            if (WhitelistProperty().arraySize == 0) return;

            var confirmed = EditorUtility.DisplayDialog(
                Localization.S("fpc.parameter_whitelist.clear_confirm.title"),
                Localization.S("fpc.parameter_whitelist.clear_confirm.message"),
                Localization.S("fpc.dialog.ok"),
                Localization.S("fpc.dialog.cancel"));
            if (!confirmed) return;

            _whitelistView.ClearSelection();
            serializedObject.Update();
            WhitelistProperty().ClearArray();
            serializedObject.ApplyModifiedProperties();
            _whitelistView.RefreshItems();
        }
    }
}
