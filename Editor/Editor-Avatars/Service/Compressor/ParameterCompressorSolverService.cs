// Source: com.vrcfury.vrcfury/Editor-Avatars/Service/Compressor/ParameterCompressorSolverService.cs
// Changes:
//   - VRCFury's [VFService]/[VFAutowired] dependency injection was replaced by plain constructor
//     injection of NDMF's BuildContext / AnimatorServicesContext / VRCAvatarDescriptor.
//   - ControllersService.GetAllUsedControllers() -> AnimatorServicesContext.ControllerContext.GetAllControllers(),
//     and ControllerManager.RewriteParameters(includeWrites: false) was replaced by CollectReadParameters(),
//     which walks NDMF's Virtual* animator graph - including synced-layer behaviour overrides,
//     which upstream saw because it flattened them onto the cloned states.
//   - The hand-written VRCContactReceiver/VRCPhysBone/VRCRaycast scans were replaced by NDMF's
//     ParameterInfo introspection API (which also covers Modular Avatar components).
//     PhysBone-style prefixes expand to the union of the PhysBone and Raycast suffixes,
//     so no VRCSDK version guard is needed - this list only feeds a "you may not want to sync this" hint.
//   - useBadPriorityMethod / the legacy compressor branch was removed.
//   - CompressorMenuItem (Ask/Compress/Fail) and its EditorUtility.DisplayDialog prompt were removed: adding the component is the opt-in.
//     Failures are reported to the NDMF console via BuildReport instead of VRCFury's ExceptionService/SneakyException.
//   - Added the component's parameter whitelist, and made the pass deterministic:
//     parameters are pre-sorted by the caller, the warning buckets are built from ordered lists rather than HashSets,
//     and the "FT/" prefix test is ordinal instead of culture-sensitive.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using FOSA.FPC.ErrorReporting;
using FOSA.FPC.Utils;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Dynamics.Contact.Components;
using VRC.SDKBase;

namespace FOSA.FPC.Service.Compressor {
    // Decides what parameters need to be compressed for the optimal performance
    internal class ParameterCompressorSolverService {
        private readonly BuildContext context;
        private readonly AnimatorServicesContext asc;
        private readonly VRCAvatarDescriptor desc;
        private readonly ISet<string> whitelist;

        // PhysBone and Raycast prefixes both expand into a set of suffixed parameters.
        // We union both sets, since this is only used to hint that a parameter probably shouldn't be network synced.
        private static readonly string[] PrefixSuffixes = {
            "_IsGrabbed", "_IsPosed", "_Angle", "_Stretch", "_Squish",
            "_Hit", "_Ratio", "_Distance"
        };

        public ParameterCompressorSolverService(
            BuildContext context,
            AnimatorServicesContext asc,
            VRCAvatarDescriptor desc,
            ISet<string> whitelist
        ) {
            this.context = context;
            this.asc = asc;
            this.desc = desc;
            this.whitelist = whitelist;
        }

        private VRCExpressionParameters paramz => desc.expressionParameters;
        private VRCExpressionsMenu menuReadOnly => desc.expressionsMenu;

        public ParameterCompressorSolverOutput GetParamsToOptimize() {
            var originalCost = paramz.parameters.CalcTotalCost();
            var maxCost = VRCExpressionParametersExtensions.GetMaxCost();
            if (originalCost <= maxCost) {
                return new ParameterCompressorSolverOutput();
            }

            var drivenParams = new HashSet<string>();
            var addDrivenParams = new HashSet<string>();

            foreach (var drivenParam in GetAllParameterDrivers().SelectMany(driver => driver.parameters)) {
                // Null entries can't be produced by Unity's serializer, but an earlier pass can insert them programmatically;
                // upstream's reader skips them too.
                if (drivenParam == null) continue;
                drivenParams.Add(drivenParam.name);
                if (drivenParam.type == VRC_AvatarParameterDriver.ChangeType.Add) addDrivenParams.Add(drivenParam.name);
            }

            var attemptOptions = new Func<ParamSelectionOptions>[] {
                () => new ParamSelectionOptions { allowedMenuTypes = new [] { VRCExpressionsMenu.Control.ControlType.RadialPuppet } },
                () => new ParamSelectionOptions { allowedMenuTypes = new [] { VRCExpressionsMenu.Control.ControlType.Toggle } },
                () => new ParamSelectionOptions { allowedMenuTypes = new [] { VRCExpressionsMenu.Control.ControlType.RadialPuppet, VRCExpressionsMenu.Control.ControlType.Toggle } },
                () => new ParamSelectionOptions { allowedMenuTypes = new [] { VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet, VRCExpressionsMenu.Control.ControlType.FourAxisPuppet } },
                () => new ParamSelectionOptions { allowedMenuTypes = new [] { VRCExpressionsMenu.Control.ControlType.RadialPuppet, VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet, VRCExpressionsMenu.Control.ControlType.FourAxisPuppet } },
                () => new ParamSelectionOptions { allowedMenuTypes = new [] { VRCExpressionsMenu.Control.ControlType.Toggle, VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet, VRCExpressionsMenu.Control.ControlType.FourAxisPuppet } },
                () => new ParamSelectionOptions { allowedMenuTypes = new [] { VRCExpressionsMenu.Control.ControlType.RadialPuppet, VRCExpressionsMenu.Control.ControlType.Toggle, VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet, VRCExpressionsMenu.Control.ControlType.FourAxisPuppet } },
            };

            var bestCost = originalCost;
            var bestDecision = new OptimizationDecision();
            var bestParameterOptions = new ParamSelectionOptions();
            var bestWasSuccess = false;
            var bestTime = 0f;
            foreach (var attemptOptionFunc in attemptOptions) {
                var options = attemptOptionFunc.Invoke();
                var decision = GetParamsToOptimize(paramz, options.allowedMenuTypes.ToImmutableHashSet(), addDrivenParams, originalCost);
                var cost = decision.GetFinalCost(originalCost);
                var batchCount = decision.GetBatchCount();
                var syncTime = batchCount * ParameterCompressorService.BATCH_TIME;
                if (bestWasSuccess) {
                    // If we already have a working solution,
                    // Only accept a more aggressive option if it cuts the sync time at least in half
                    if (syncTime > bestTime / 2) continue;
                    // Don't switch from a working solution to a non-working one
                    if (cost > maxCost) continue;
                } else {
                    // If we don't have a working solution yet, just try to find the lowest bits possible
                    if (cost >= bestCost) continue;
                }
                bestCost = cost;
                bestDecision = decision;
                bestParameterOptions = options;
                bestWasSuccess = cost <= maxCost;
                bestTime = syncTime;

                // If sync time is less than 1s, don't need to try any more aggressive options
                if (bestWasSuccess && syncTime <= 1) break;
            }

            var controllerUsedParams = CollectReadParameters();
            var contactParams = CollectComponentParameters();

            var allMenuParamsStr = GetParamsUsedInMenu(null);
            var buttonMenuParamsStr = GetParamsUsedInMenu(new [] {VRCExpressionsMenu.Control.ControlType.Button,VRCExpressionsMenu.Control.ControlType.SubMenu}.ToImmutableHashSet());

            var remainingSyncedParams = paramz.parameters.Where(p => p.IsNetworkSynced()).ToList();
            var warnUnusedParams = TakeFrom(remainingSyncedParams, p => !controllerUsedParams.Contains(p.name));
            var warnContactParams = TakeFrom(remainingSyncedParams, p => contactParams.Contains(p.name));
            var warnButtonParams = TakeFrom(remainingSyncedParams, p => buttonMenuParamsStr.Contains(p.name));
            var warnOscOnlyParams = TakeFrom(remainingSyncedParams, p => !allMenuParamsStr.Contains(p.name) && !drivenParams.Contains(p.name) && !p.name.StartsWith("FT/", StringComparison.Ordinal));

            var decisionWithInfo = new ParameterCompressorSolverOutput {
                decision = bestDecision,
                warnUnusedParams = warnUnusedParams,
                warnContactParams = warnContactParams,
                warnButtonParams = warnButtonParams,
                warnOscOnlyParams = warnOscOnlyParams,
                options = bestParameterOptions
            };

            if (bestWasSuccess) {
                return decisionWithInfo;
            }

            var paramWarnings = decisionWithInfo.FormatWarnings();
            if (!string.IsNullOrEmpty(paramWarnings)) paramWarnings = "\n\n" + paramWarnings;

            // {0} original cost, {1} limit, {2} best achievable cost,
            // {3} the "these params probably should not be synced" hints (blank-line prefixed, or empty).
            BuildReport.LogFatal("error.compressor.out_of_space",
                originalCost, maxCost, bestCost, paramWarnings);

            return new ParameterCompressorSolverOutput();
        }

        private static IList<VRCExpressionParameters.Parameter> TakeFrom(
            List<VRCExpressionParameters.Parameter> source,
            Func<VRCExpressionParameters.Parameter, bool> predicate
        ) {
            var taken = source.Where(predicate).ToList();
            source.RemoveAll(p => predicate(p));
            return taken;
        }

        private IEnumerable<VRCAvatarParameterDriver> GetAllParameterDrivers() {
            foreach (var controller in asc.ControllerContext.GetAllControllers()) {
                foreach (var node in controller.AllReachableNodes()) {
                    IEnumerable<StateMachineBehaviour> behaviours;
                    switch (node) {
                        case VirtualState state: behaviours = state.Behaviours; break;
                        case VirtualStateMachine sm: behaviours = sm.Behaviours; break;
                        // A synced layer keeps its per-state behaviour overrides on the layer itself, not on the states;
                        // upstream saw those because it flattened them onto the cloned states (VFController.ApplySyncedLayerOverrides).
                        case VirtualLayer layer:
                            behaviours = layer.SyncedLayerBehaviourOverrides.Values.SelectMany(list => list);
                            break;
                        default: continue;
                    }
                    foreach (var driver in behaviours.OfType<VRCAvatarParameterDriver>()) {
                        if (driver == null || driver.parameters == null) continue;
                        yield return driver;
                    }
                }
            }
        }

        // Collects every parameter that is *read* by any animator on this avatar.
        // Writes (parameter driver targets, animated animator properties) intentionally don't count,
        // which mirrors VRCFury's RewriteParameters(includeWrites: false).
        private ISet<string> CollectReadParameters() {
            var used = new HashSet<string>();

            void Add(string name) {
                if (!string.IsNullOrEmpty(name)) used.Add(name);
            }

            // A Copy driver reads its source parameter, and a PlayAudio behaviour reads the parameter that selects which clip to play.
            // Behaviours live on states *and* on (sub-)state machines - NDMF's own behaviour walks handle both,
            // and so does GetAllParameterDrivers above.
            void AddBehaviourReads(IEnumerable<StateMachineBehaviour> behaviours) {
                foreach (var driver in behaviours.OfType<VRCAvatarParameterDriver>()) {
                    if (driver == null || driver.parameters == null) continue;
                    foreach (var p in driver.parameters) {
                        if (p == null) continue;
                        if (p.type == VRC_AvatarParameterDriver.ChangeType.Copy) Add(p.source);
                    }
                }
                foreach (var playAudio in behaviours.OfType<VRCAnimatorPlayAudio>()) {
                    if (playAudio == null) continue;
                    Add(playAudio.ParameterName);
                }
            }

            foreach (var controller in asc.ControllerContext.GetAllControllers()) {
                foreach (var node in controller.AllReachableNodes()) {
                    switch (node) {
                        case VirtualTransitionBase transition:
                            foreach (var condition in transition.Conditions) Add(condition.parameter);
                            break;
                        case VirtualState state:
                            Add(state.SpeedParameter);
                            Add(state.TimeParameter);
                            Add(state.CycleOffsetParameter);
                            Add(state.MirrorParameter);
                            AddBehaviourReads(state.Behaviours);
                            break;
                        case VirtualStateMachine stateMachine:
                            AddBehaviourReads(stateMachine.Behaviours);
                            break;
                        // See GetAllParameterDrivers: synced-layer behaviour overrides live on the layer, not on the states.
                        case VirtualLayer layer:
                            AddBehaviourReads(layer.SyncedLayerBehaviourOverrides.Values.SelectMany(list => list));
                            break;
                        case VirtualBlendTree tree:
                            // Like VRCFury's VFTree.RewriteParameters, each field only counts for the blend types that actually use it:
                            // Unity leaves stale values (usually "Blend") behind on the fields the current type ignores.
                            if (tree.BlendType != BlendTreeType.Direct) {
                                Add(tree.BlendParameter);
                                if (tree.BlendType != BlendTreeType.Simple1D) Add(tree.BlendParameterY);
                            } else {
                                foreach (var child in tree.Children) Add(child.DirectBlendParameter);
                            }
                            break;
                    }
                }
            }

            return used;
        }

        // Parameters which are generated by a component
        // (contacts, physbones, raycasts, and anything else registered with NDMF's parameter introspection API)
        // rather than driven from a menu.
        private ISet<string> CollectComponentParameters() {
            var result = new HashSet<string>();

            foreach (var provided in ParameterInfo.ForContext(context)
                         .GetParametersForObject(context.AvatarRootObject)) {
                if (provided.Namespace == ParameterNamespace.PhysBonesPrefix) {
                    foreach (var suffix in PrefixSuffixes) result.Add(provided.EffectiveName + suffix);
                } else if (provided.Source is VRCContactReceiver) {
                    result.Add(provided.EffectiveName);
                }
            }

            return result;
        }

        public class ParamSelectionOptions {
            public IList<VRCExpressionsMenu.Control.ControlType> allowedMenuTypes = new List<VRCExpressionsMenu.Control.ControlType>();

            public string FormatTypes() {
                return MenuTypePriority.Where(t => allowedMenuTypes.Contains(t)).Select(t => t.ToString()).Join(", ");
            }
        }

        private static readonly IList<VRCExpressionsMenu.Control.ControlType> MenuTypePriority = new[] {
            // Make sure these are in priority order, since it matters if a param is used by multiple menu item types
            VRCExpressionsMenu.Control.ControlType.RadialPuppet,
            VRCExpressionsMenu.Control.ControlType.Toggle,
            VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet,
            VRCExpressionsMenu.Control.ControlType.FourAxisPuppet,
            VRCExpressionsMenu.Control.ControlType.Button,
            VRCExpressionsMenu.Control.ControlType.SubMenu,
        };

        private ISet<string> GetParamsUsedInMenu(ISet<VRCExpressionsMenu.Control.ControlType> allowedMenuTypes) {
            var paramNameToMenuType = new Dictionary<string, VRCExpressionsMenu.Control.ControlType>();
            void AttemptToAdd(VRCExpressionsMenu.Control.Parameter param, VRCExpressionsMenu.Control.ControlType menuType) {
                if (param == null) return;
                if (string.IsNullOrEmpty(param.name)) return;
                var menuTypePriority = MenuTypePriority.IndexOf(menuType);
                if (menuTypePriority < 0) return;
                if (paramNameToMenuType.TryGetValue(param.name, out var oldMenuType)) {
                    var oldMenuTypePriority = MenuTypePriority.IndexOf(oldMenuType);
                    if (menuTypePriority < oldMenuTypePriority) return;
                }
                paramNameToMenuType[param.name] = menuType;
            }

            if (menuReadOnly != null) {
                menuReadOnly.ForEachMenu(ForEachItem: (control, list) => {
                    if (control.type == VRCExpressionsMenu.Control.ControlType.RadialPuppet) {
                        AttemptToAdd(control.parameter, VRCExpressionsMenu.Control.ControlType.SubMenu);
                        AttemptToAdd(control.GetSubParameter(0), control.type);
                    } else if (control.type == VRCExpressionsMenu.Control.ControlType.Button) {
                        AttemptToAdd(control.parameter, control.type);
                    } else if (control.type == VRCExpressionsMenu.Control.ControlType.Toggle) {
                        AttemptToAdd(control.parameter, control.type);
                    } else if (control.type == VRCExpressionsMenu.Control.ControlType.FourAxisPuppet) {
                        AttemptToAdd(control.parameter, VRCExpressionsMenu.Control.ControlType.SubMenu);
                        AttemptToAdd(control.GetSubParameter(0), control.type);
                        AttemptToAdd(control.GetSubParameter(1), control.type);
                        AttemptToAdd(control.GetSubParameter(2), control.type);
                        AttemptToAdd(control.GetSubParameter(3), control.type);
                    } else if (control.type == VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet) {
                        AttemptToAdd(control.parameter, VRCExpressionsMenu.Control.ControlType.SubMenu);
                        AttemptToAdd(control.GetSubParameter(0), control.type);
                        AttemptToAdd(control.GetSubParameter(1), control.type);
                    } else if (control.type == VRCExpressionsMenu.Control.ControlType.SubMenu) {
                        AttemptToAdd(control.parameter, VRCExpressionsMenu.Control.ControlType.SubMenu);
                    }

                    return VRCExpressionsMenuExtensions.ForEachMenuItemResult.Continue;
                });
            }
            return paramNameToMenuType
                .Where(pair => allowedMenuTypes == null || allowedMenuTypes.Contains(pair.Value))
                .Select(pair => pair.Key)
                .ToImmutableHashSet();
        }

        private OptimizationDecision GetParamsToOptimize(
            VRCExpressionParameters paramz,
            ISet<VRCExpressionsMenu.Control.ControlType> allowedMenuTypes,
            ISet<string> addDriven,
            int originalCost
        ) {
            var eligible = new List<VRCExpressionParameters.Parameter>();
            var usedInMenu = GetParamsUsedInMenu(allowedMenuTypes);

            foreach (var param in paramz.parameters) {
                if (!param.IsNetworkSynced()) continue;
                if (whitelist.Contains(param.name)) continue;
                if (!usedInMenu.Contains(param.name)) continue;
                if (addDriven.Contains(param.name) && !allowedMenuTypes.Contains(VRCExpressionsMenu.Control.ControlType.FourAxisPuppet)) continue;
                eligible.Add(param);
            }

            var decision = new OptimizationDecision {
                compress = eligible
            };
            decision.Optimize(originalCost);

            return decision;
        }
    }
}
