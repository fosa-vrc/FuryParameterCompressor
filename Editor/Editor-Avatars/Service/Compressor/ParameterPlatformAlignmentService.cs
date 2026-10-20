// Source: com.vrcfury.vrcfury/Editor-Avatars/Service/Compressor/ParameterPlatformAlignmentService.cs
// Source: nadena.dev.modular-avatar/Editor/SyncParameterSequence/ParameterInfoRegistry.cs
// Source: nadena.dev.modular-avatar/Editor/SyncParameterSequence/SyncParameterSequencePass.cs
// Changes:
//   - DI replaced by constructor args.
//   - VRCFury matched parameters across platforms by their *source*
//     (object path + original parameter name, via ParameterSourceService) and guessed path aliases.
//     That was replaced wholesale by Modular Avatar's name-based matching, as requested.
//   - Nothing is written outside the project: VRCFury's %LOCALAPPDATA%/VRCFury/DesktopSyncData JSON was replaced by ParameterInfoStore
//     (Library/net.fosa.fury-parameter-compressor/AvatarParameterInfo), modelled on MA's ParameterInfoStore.
//   - The "primary platform" is PC/Android/iOS chosen on the component instead of VRCFury's implicit "desktop is primary",
//     matching ModularAvatarSyncParameterSequence.
//   - Every VRCFury EditorUtility.DisplayDialog warning became a hard NDMF console error,
//     per the requirement that a secondary-platform build must abort when it cannot align.
//     The error keys are Modular Avatar's error.syncparamsequence.* keys,
//     plus a new solution_mismatch key for the tool-version / solution-hash check that VRCFury did via its saveVersion/vrcfuryVersion fields.
//     The one exception is NDMF's "apply on play" processing, where the same messages are reported as warnings and the caller solves locally -
//     VRCFury's original fallback - so that an avatar stays testable in play mode before its primary platform has ever been built.
//     Uploads still abort.
//   - Records now carry the tool version and the solver result (slots + compressed parameter list + hash)
//     so a secondary platform replays the primary's solution instead of solving again.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FOSA.FPC.ErrorReporting;
using FOSA.FPC.SyncParameterSequence;
using FOSA.FPC.Utils;
using JetBrains.Annotations;
using nadena.dev.ndmf;
using UnityEditor;
using VRC.Core;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace FOSA.FPC.Service.Compressor {
    internal class ParameterPlatformAlignmentService {
        private readonly VRCAvatarDescriptor desc;
        private readonly FuryParameterCompressor component;
        private readonly IParameterInfoStore store;

        // Set by Unity test code / tooling to pretend we're building for another platform.
        // Mirrors MA's SyncParameterSequencePass.OverrideBuildTarget.
        internal static BuildTarget? OverrideBuildTarget;

        public ParameterPlatformAlignmentService(
            VRCAvatarDescriptor desc,
            FuryParameterCompressor component,
            IParameterInfoStore store = null
        ) {
            this.desc = desc;
            this.component = component;
            this.store = store ?? ParameterInfoStore.Instance;
        }

        public static BuildTarget ActiveBuildTarget =>
            OverrideBuildTarget ?? EditorUserBuildSettings.activeBuildTarget;

        // Source: nadena.dev.modular-avatar/Editor/SyncParameterSequence/SyncParameterSequencePass.cs
        private static FuryParameterCompressor.Platform? PlatformFromBuildTarget(BuildTarget buildTarget) {
            switch (buildTarget) {
                case BuildTarget.Android: return FuryParameterCompressor.Platform.Android;
                case BuildTarget.iOS: return FuryParameterCompressor.Platform.iOS;
                case BuildTarget.StandaloneWindows64: return FuryParameterCompressor.Platform.PC;
                case BuildTarget.StandaloneLinux64: return FuryParameterCompressor.Platform.PC; // for CI
                default: return null;
            }
        }

        private static FuryParameterCompressor.Platform? CurrentPlatform => PlatformFromBuildTarget(ActiveBuildTarget);

        // True when this build's platform is the one whose solver result is authoritative,
        // or when the current build target isn't one we track
        // (in which case we behave like a standalone build: solve normally, record nothing).
        public bool IsPrimaryPlatform => CurrentPlatform == null || CurrentPlatform == component.PrimaryPlatform;

        public bool IsPlatformTracked => CurrentPlatform != null;

        /// <summary>
        ///     True when this run is NDMF's "apply on play" processing rather than an upload build. NDMF
        ///     drives that from a runtime component's Awake/Start (see NDMF's ApplyOnPlay), so play mode has
        ///     already begun by the time any pass runs; an upload build runs the VRChat SDK preprocess hook
        ///     in edit mode, where this is false.
        /// </summary>
        // Fully qualified: this file deliberately doesn't import UnityEngine,
        // to keep it free of ambiguities against UnityEditor / VRC.Core.
        public static bool IsPlayModeBuild => UnityEngine.Application.isPlaying;

        // Alignment failures abort an upload build, but only warn in play mode:
        // NDMF applies the whole pipeline on entering play mode,
        // so aborting there would make an avatar completely untestable until its primary platform has been built once.
        // The caller falls back to solving locally, which is what VRCFury's AlignForMobile did on every failure.
        private void ReportAlignmentFailure(string key, params object[] args) {
            var severity = IsPlayModeBuild ? ErrorSeverity.NonFatal : ErrorSeverity.Error;
            BuildReport.ReportingObject(component, () => BuildReport.Log(severity, key, args));
        }

        [CanBeNull]
        internal static string GetBlueprintId(VRCAvatarDescriptor descriptor) {
            var pipelineManager = descriptor.GetComponent<PipelineManager>();
            var blueprintId = pipelineManager != null ? pipelineManager.blueprintId : null;
            return string.IsNullOrEmpty(blueprintId) ? null : blueprintId;
        }

        // Source: nadena.dev.modular-avatar/Editor/SyncParameterSequence/ParameterInfoRegistry.cs
        private static List<ParameterDefinition> ConvertParameters(VRCExpressionParameters.Parameter[] parameters) {
            var parameterInfo = parameters ?? Array.Empty<VRCExpressionParameters.Parameter>();

            var convertedInfo = parameterInfo
                .Where(p => p.networkSynced)
                .Select(ParameterDefinition.FromVRC)
                .ToList();
            return convertedInfo;
        }

        // Source: nadena.dev.modular-avatar/Editor/SyncParameterSequence/SyncParameterSequencePass.cs
        public void WarnIfPrimaryPlatformChanged() {
            // MA returns before this warning when the current build target isn't one it tracks.
            if (!IsPlatformTracked) return;

            var blueprintId = GetBlueprintId(desc);
            if (blueprintId == null) return;

            var record = store.GetRecordForBlueprintId(blueprintId);
            if (record.PrimaryPlatformRecord == null) return;

            var previousPlatform = PlatformFromBuildTarget(record.PrimaryPlatformRecord.Target);
            if (previousPlatform != component.PrimaryPlatform) {
                BuildReport.ReportingObject(component, () =>
                    BuildReport.Log(ErrorSeverity.NonFatal,
                        "validation.syncparamsequence.primary_platform_changed",
                        component.PrimaryPlatform,
                        // Same unit as {0}: the Platform, not the raw BuildTarget.
                        // Fall back to the BuildTarget only if the record holds one we can't map (hand-edited file).
                        (object)previousPlatform ?? record.PrimaryPlatformRecord.Target));
            }
        }

        // Runs on a secondary platform.
        // Merges in the primary platform's parameters and returns the primary platform's recorded solution,
        // so that both platforms compress identically.
        // Returns null (and reports a fatal error) if the build cannot be aligned.

        // Source: nadena.dev.modular-avatar/Editor/SyncParameterSequence/ParameterInfoRegistry.cs
        // The verification steps and the decision replay added.
        [CanBeNull]
        public ParameterCompressorSolverOutput AlignForSecondary() {
            var paramz = desc.expressionParameters;
            var blueprintId = GetBlueprintId(desc);

            if (blueprintId == null) {
                ReportAlignmentFailure("error.syncparamsequence.no_primary_record",
                    ActiveBuildTarget, component.PrimaryPlatform);
                return null;
            }

            var current = store.GetRecordForBlueprintId(blueprintId);
            var primary = current.PrimaryPlatformRecord;
            if (primary == null) {
                ReportAlignmentFailure("error.syncparamsequence.no_primary_record",
                    ActiveBuildTarget, component.PrimaryPlatform);
                return null;
            }

            if (primary.SaveVersion != ParameterInfoRecord.CurrentSaveVersion || primary.ToolVersion != FPCPackageUtils.Version) {
                ReportAlignmentFailure("error.syncparamsequence.solution_mismatch",
                    primary.Target, primary.ToolVersion, FPCPackageUtils.Version);
                return null;
            }

            if (primary.SolutionHash != primary.ComputeSolutionHash()) {
                ReportAlignmentFailure("error.syncparamsequence.solution_mismatch",
                    primary.Target, primary.ToolVersion, FPCPackageUtils.Version);
                return null;
            }

            // Merge in anything from the primary platform
            var parameters = paramz.parameters.ToList();
            var knownParameters = BuildParameterLookup(paramz.parameters);
            var failed = false;

            foreach (var parameter in primary.WantedParameters) {
                var toVRC = parameter.ToVRC();
                // BuildParameterLookup keys null names as "", so look them up the same way.
                var lookupKey = parameter.Name ?? string.Empty;
                if (knownParameters.TryGetValue(lookupKey, out var existing)) {
                    if (existing.valueType != toVRC.valueType) {
                        failed = true;
                        ReportAlignmentFailure("error.syncparamsequence.type_mismatch",
                            parameter.Name,
                            existing.valueType, ActiveBuildTarget,
                            toVRC.valueType, primary.Target);
                    }

                    knownParameters.Remove(lookupKey);
                } else {
                    parameters.Add(toVRC);
                }
            }

            foreach (var parameter in knownParameters.Keys.OrderBy(k => k, StringComparer.Ordinal)) {
                if (!knownParameters[parameter].IsNetworkSynced()) continue;

                failed = true;
                ReportAlignmentFailure("error.syncparamsequence.unregistered_parameter", parameter, primary.Target);
            }

            // Applied even when the merge failed, like MA (whose LogFatal reports but doesn't abort):
            // the play-mode fallback then solves on top of whatever the merge managed to do,
            // which can only bring it closer to what the real upload would contain.
            paramz.parameters = parameters.ToArray();
            paramz.SortByName();

            if (failed) return null;

            // Verify that, after merging, we're looking at exactly the parameter set the primary platform solved for.
            // If not, replaying its solution would produce a differently-shaped avatar.
            // This runs before the budget check below,
            // so that a divergent parameter set is reported as such instead of surfacing as a confusing "over budget" error.
            var check = new ParameterInfoRecord {
                SaveVersion = primary.SaveVersion,
                ToolVersion = primary.ToolVersion,
                NumberSlots = primary.NumberSlots,
                BoolSlots = primary.BoolSlots,
                WantedParameters = ConvertParameters(paramz.parameters),
                CompressedParameters = primary.CompressedParameters
            };
            if (check.ComputeSolutionHash() != primary.SolutionHash) {
                // The type is already checked during the merge above,
                // so what usually differs here is a parameter's default value or its "synced" flag.
                // Name it, or the user has nothing to go on.
                var difference = DescribeFirstDifference(primary.WantedParameters, check.WantedParameters);
                ReportAlignmentFailure("error.syncparamsequence.parameter_set_mismatch",
                    primary.Target, difference);
                return null;
            }

            // BuildParameterLookup rather than ToDictionary:
            // it normalises null names and tolerates duplicates,
            // so a record with an empty parameter name surfaces as an FPC error below instead of an opaque ArgumentException out of the dictionary.
            var byName = BuildParameterLookup(paramz.parameters);
            var compress = new List<VRCExpressionParameters.Parameter>();
            foreach (var name in primary.CompressedParameters) {
                if (!byName.TryGetValue(name ?? string.Empty, out var param)) {
                    ReportAlignmentFailure("error.syncparamsequence.unregistered_parameter", name, primary.Target);
                    return null;
                }

                compress.Add(param);
            }

            var decision = new OptimizationDecision {
                boolSlots = primary.BoolSlots,
                numberSlots = primary.NumberSlots,
                compress = compress
            };

            // A record whose slot counts don't match its compressed parameter types would divide by zero inside GetBatches,
            // and a negative count would throw from Enumerable.Range in BuildLayer (after quietly loosening the cost safety net below).
            // The solution hash covers both fields, so this is only reachable with a hand-edited record whose hash was recomputed to match -
            // report it as an unusable record rather than crashing.
            var needsNumberSlots = compress.Any(p => p.valueType != VRCExpressionParameters.ValueType.Bool);
            var needsBoolSlots = compress.Any(p => p.valueType == VRCExpressionParameters.ValueType.Bool);
            if (primary.NumberSlots < 0 || primary.BoolSlots < 0
                || (needsNumberSlots && primary.NumberSlots <= 0)
                || (needsBoolSlots && primary.BoolSlots <= 0)) {
                ReportAlignmentFailure("error.syncparamsequence.solution_mismatch",
                    primary.Target, primary.ToolVersion, FPCPackageUtils.Version);
                return null;
            }

            // The budget check has to look at the cost *after* compression.
            // MA's NormalizeParameters compares the merged cost directly, but MA never compresses -
            // for us, "the merged set is over budget" is the normal case and the whole reason the compressor exists,
            // so comparing it directly would fail every secondary build of an avatar that actually needs compressing.
            // With the hash check above this is only a safety net:
            // the same parameter set plus the same decision necessarily reproduces the primary platform's (already valid) final cost.
            // An empty decision has to bypass GetFinalCost entirely:
            // it unconditionally adds GetIndexBitCount(), which is 1 even for zero batches,
            // but with nothing to compress BuildLayer never runs and no FPC/SyncIndex* parameter is ever created.
            // Charging that phantom bit here would fail every secondary build of an avatar
            // that sits at exactly the limit and needs no compression at all.
            var mergedCost = paramz.parameters.CalcTotalCost();
            var finalCost = decision.compress.Any() ? decision.GetFinalCost(mergedCost) : mergedCost;
            if (finalCost > VRCExpressionParametersExtensions.GetMaxCost()) {
                ReportAlignmentFailure("error.syncparamsequence.cost_exceeded",
                    primary.Target, finalCost, VRCExpressionParametersExtensions.GetMaxCost());
                return null;
            }

            return new ParameterCompressorSolverOutput {
                decision = decision
            };
        }

        // The generated FPC/* slot parameters are created *after* the solution hash is computed, so the hash cannot possibly cover them.
        // Their names are uniquified against the animator controllers as well as the expression parameters,
        // and the controllers can legitimately differ between platforms -
        // so a name collision that only exists on one platform would silently produce two avatars with different synced parameter sets.
        // Comparing the final synced set against the primary platform's record closes that gap.
        // Source: nadena.dev.modular-avatar/Editor/SyncParameterSequence/ParameterInfoRecord.cs
        // (ActualParametersComparer, which MA uses for the same "are these two builds consistent?" test).
        public void VerifyFinalParametersMatchPrimary(VRCExpressionParameters.Parameter[] parametersAfterCompression) {
            if (IsPrimaryPlatform) return;

            var blueprintId = GetBlueprintId(desc);
            if (blueprintId == null) return;

            var primary = store.GetRecordForBlueprintId(blueprintId).PrimaryPlatformRecord;
            if (primary == null) return;

            var actual = new ParameterInfoRecord {
                ActualParameters = ConvertParameters(parametersAfterCompression)
            };
            if (ParameterInfoRecord.ActualParametersComparer.Equals(actual, primary)) return;

            var difference = DescribeFirstDifference(primary.ActualParameters, actual.ActualParameters);
            ReportAlignmentFailure("error.syncparamsequence.final_mismatch", primary.Target, difference);
        }

        // Both lists are sorted by name, but they can differ in length:
        // a parameter that is synced on the primary platform but not here simply drops out of the local list.
        // Walking by index would then pair everything after the first difference with the wrong partner and the message would accuse an innocent parameter,
        // so the diff is keyed by name.
        private static string DescribeFirstDifference(List<ParameterDefinition> expected, List<ParameterDefinition> actual) {
            var actualByName = new Dictionary<string, ParameterDefinition>(StringComparer.Ordinal);
            foreach (var p in actual) actualByName[p.Name ?? ""] = p;

            // Missing here, or present on both sides but with a different type / default value.
            foreach (var e in expected) {
                if (!actualByName.TryGetValue(e.Name ?? "", out var a)) return Describe(e) + " != " + Describe(null);
                if (!ParameterDefinition.CriticalValueComparer.Equals(e, a)) return Describe(e) + " != " + Describe(a);
            }

            // Present here but not on the primary platform.
            var expectedNames = new HashSet<string>(expected.Select(p => p.Name ?? ""), StringComparer.Ordinal);
            foreach (var a in actual) {
                if (!expectedNames.Contains(a.Name ?? "")) return Describe(null) + " != " + Describe(a);
            }

            // Same parameters, different order.
            // The solution hash is order-sensitive (it concatenates WantedParameters in list order) while this diff is not,
            // so the caller would otherwise print an empty difference.
            // Both sides sort with the same ordinal comparison over unique names,
            // so this should be unreachable.
            return Localization.S("compressor.diff.order_only");
        }

        // Includes the default value: types are already checked during the merge,
        // so when this diff fires the difference usually IS the default value -
        // without it both sides of the "A != B" message would print identically in the most common case.
        private static string Describe(ParameterDefinition p) {
            return p == null
                ? "-"
                : $"{p.Name} ({p.ValueType}, default {p.DefaultValue.ToString(CultureInfo.InvariantCulture)})";
        }

        // Records the primary platform's parameters and solution, so that other platforms can be aligned against it.
        // Source: VRCFury's SaveToDiskAfterBuild, rewritten on top of MA's store.
        // Unlike VRCFury we do not wait for the blueprint ID to be assigned after upload;
        // like MA we simply skip avatars that don't have one yet.
        // Secondary platforms record nothing: MA keeps a per-secondary-platform record,
        // but alignment here only ever reads the primary one, so storing them would be write-only data.
        public void SaveRecord(
            OptimizationDecision decision,
            VRCExpressionParameters.Parameter[] parametersBeforeCompression,
            VRCExpressionParameters.Parameter[] parametersAfterCompression
        ) {
            if (!IsPrimaryPlatform) return;

            var blueprintId = GetBlueprintId(desc);
            if (blueprintId == null) return;

            var record = new ParameterInfoRecord {
                Target = ActiveBuildTarget,
                SaveVersion = ParameterInfoRecord.CurrentSaveVersion,
                ToolVersion = FPCPackageUtils.Version,
                NumberSlots = decision.numberSlots,
                BoolSlots = decision.boolSlots,
                CompressedParameters = decision.compress.Select(p => p.name).ToList(),
                WantedParameters = ConvertParameters(parametersBeforeCompression),
                ActualParameters = ConvertParameters(parametersAfterCompression)
            };
            record.SolutionHash = record.ComputeSolutionHash();

            store.UpdatePrimaryRecord(blueprintId, record);
        }

        // Source: nadena.dev.modular-avatar/Editor/SyncParameterSequence/ParameterInfoRegistry.cs
        private static Dictionary<string, VRCExpressionParameters.Parameter> BuildParameterLookup(
            IEnumerable<VRCExpressionParameters.Parameter> parameters
        ) {
            var lookup = new Dictionary<string, VRCExpressionParameters.Parameter>(StringComparer.Ordinal);

            foreach (var parameter in parameters) {
                var name = parameter.name ?? string.Empty;
                lookup[name] = parameter;
            }

            return lookup;
        }
    }
}
