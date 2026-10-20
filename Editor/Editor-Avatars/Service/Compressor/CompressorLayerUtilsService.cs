// Source: com.vrcfury.vrcfury/Editor-Avatars/Service/Compressor/CompressorLayerUtilsService.cs
// Changes:
//   - DI replaced by constructor args.
//   - MakeParam also registers the matching animator parameter on the FX controller (VRCFury's ControllerManager did this implicitly),
//     and uses a deterministic uniquifier ("FPC/<name>", then "_1", "_2", ...) instead of VRCFury's randomly prefixed MakeUniqueParamName,
//     so that two builds of the same avatar produce byte-identical output.
//   - Added Always()/IsLocal() (Source: com.vrcfury.vrcfury/Editor-Avatars/Utils/ControllerManager.cs).
//     VRCFury's True() uses a random parameter name; ours is deterministic.
//   - FixWd() now honours the component's Write Defaults option.
//     The "Auto" mode uses AnalyzeLayerWriteDefaults / IsWriteDefaultsRequiredLayer,
//     ported from Packages/nadena.dev.modular-avatar/Editor/MergeAnimatorProcessor.cs,
//     which is what MA Merge Animator's "Match Avatar Write Defaults" uses.
//     A mixed avatar resolves to Write Defaults OFF, which matches VRCFury's original behaviour.

using System;
using System.Collections.Generic;
using System.Linq;
using FOSA.FPC.Utils;
using FOSA.FPC.Utils.Controller;
using nadena.dev.ndmf.animator;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace FOSA.FPC.Service.Compressor {
    internal class CompressorLayerUtilsService {
        private const string ParamPrefix = "FPC/";

        private readonly VRCExpressionParameters paramz;
        private readonly VirtualAnimatorController fx;
        private readonly FuryParameterCompressor.WriteDefaultsMode wdMode;
        private readonly HashSet<string> usedNames;

        public CompressorLayerUtilsService(
            VRCExpressionParameters paramz,
            AnimatorServicesContext asc,
            VirtualAnimatorController fx,
            FuryParameterCompressor.WriteDefaultsMode wdMode
        ) {
            this.paramz = paramz;
            this.fx = fx;
            this.wdMode = wdMode;

            usedNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in paramz.parameters) usedNames.Add(p.name);
            foreach (var controller in asc.ControllerContext.GetAllControllers()) {
                foreach (var name in controller.Parameters.Keys) usedNames.Add(name);
            }
        }

        public string MakeUniqueParamName(string name) {
            var baseName = ParamPrefix + name;
            var candidate = baseName;
            var i = 0;
            while (!usedNames.Add(candidate)) {
                i++;
                candidate = baseName + "_" + i;
            }
            return candidate;
        }

        public VRCExpressionParameters.Parameter MakeParam(string name, VRCExpressionParameters.ValueType type, bool synced) {
            var param = new VRCExpressionParameters.Parameter {
                name = MakeUniqueParamName(name),
                valueType = type,
                saved = false
            };
            param.SetNetworkSynced(synced);
            paramz.Add(param);
            SetAnimatorParameter(param.name, ToAnimatorType(type), 0);
            return param;
        }

        // A synced expression parameter of type Bool, usable directly as a transition condition.
        public VFABool MakeSyncedBool(string name) {
            return new VFABool(MakeParam(name, VRCExpressionParameters.ValueType.Bool, true).name);
        }

        // VRCFury: ControllerManager.True()/Always(). Animator-only, always true.
        private VFABool trueParam;
        public VFCondition Always() {
            if (trueParam == null) {
                var name = MakeUniqueParamName("True");
                SetAnimatorParameter(name, AnimatorControllerParameterType.Bool, 1);
                trueParam = new VFABool(name);
            }
            return trueParam.IsTrue();
        }

        // VRCFury: ControllerManager.IsLocal(). This is a VRChat-provided parameter,
        // so it keeps its exact name and is only declared on the controller if it isn't already.
        public VFABool IsLocal() {
            if (!fx.Parameters.ContainsKey("IsLocal")) {
                SetAnimatorParameter("IsLocal", AnimatorControllerParameterType.Bool, 0);
                usedNames.Add("IsLocal");
            }
            return new VFABool("IsLocal");
        }

        private void SetAnimatorParameter(string name, AnimatorControllerParameterType type, float defaultValue) {
            var acp = new AnimatorControllerParameter {
                name = name,
                type = type
            };
            switch (type) {
                case AnimatorControllerParameterType.Bool: acp.defaultBool = defaultValue != 0; break;
                case AnimatorControllerParameterType.Int: acp.defaultInt = (int)defaultValue; break;
                default: acp.defaultFloat = defaultValue; break;
            }
            fx.SetParameter(name, acp);
        }

        private static AnimatorControllerParameterType ToAnimatorType(VRCExpressionParameters.ValueType type) {
            switch (type) {
                case VRCExpressionParameters.ValueType.Bool: return AnimatorControllerParameterType.Bool;
                case VRCExpressionParameters.ValueType.Int: return AnimatorControllerParameterType.Int;
                case VRCExpressionParameters.ValueType.Float: return AnimatorControllerParameterType.Float;
                default: throw new Exception("Unknown type?");
            }
        }

        public void FixWd(VirtualLayer layer) {
            bool wd;
            switch (wdMode) {
                case FuryParameterCompressor.WriteDefaultsMode.ForceOn:
                    wd = true;
                    break;
                case FuryParameterCompressor.WriteDefaultsMode.ForceOff:
                    wd = false;
                    break;
                default:
                    wd = AnalyzeLayerWriteDefaults(fx, layer) ?? false;
                    break;
            }

            if (layer.StateMachine == null) return;
            foreach (var state in layer.StateMachine.AllStates()) {
                state.WriteDefaultValues = wd;
            }
        }

        // Source: nadena.dev.modular-avatar/Editor/MergeAnimatorProcessor.cs
        // Changed to skip the layer we just generated.
        private static bool? AnalyzeLayerWriteDefaults(VirtualAnimatorController controller, VirtualLayer exclude) {
            bool? writeDefaults = null;

            var wdStateCounter = controller.Layers
                .Where(l => l != exclude)
                .Where(l => l.StateMachine != null)
                .Where(l => !IsWriteDefaultsRequiredLayer(l))
                .SelectMany(l => l.StateMachine!.AllStates())
                .Select(s => s.WriteDefaultValues)
                .GroupBy(b => b)
                .ToDictionary(g => g.Key, g => g.Count());

            if (wdStateCounter.Count == 1) writeDefaults = wdStateCounter.First().Key;
            return writeDefaults;
        }

        // Source: nadena.dev.modular-avatar/Editor/MergeAnimatorProcessor.cs
        private static bool IsWriteDefaultsRequiredLayer(VirtualLayer virtualLayer) {
            if (virtualLayer.BlendingMode == AnimatorLayerBlendingMode.Additive) return true;
            var sm = virtualLayer.StateMachine;
            if (sm == null) return false;

            if (sm.StateMachines.Count != 0) return false;
            if (sm.States.Count != 1) return false;
            if (sm.AnyStateTransitions.Count != 0) return false;
            if (sm.DefaultState?.Transitions?.Count != 0) return false; // also checks for nullity
            if (sm.DefaultState.Motion is not VirtualBlendTree) return false;

            return sm.DefaultState.Motion.AllReachableNodes()
                .OfType<VirtualBlendTree>()
                .Any(bt => bt.BlendType == BlendTreeType.Direct);
        }
    }
}
