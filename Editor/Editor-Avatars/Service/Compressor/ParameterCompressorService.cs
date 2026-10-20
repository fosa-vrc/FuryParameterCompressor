// Source: com.vrcfury.vrcfury/Editor-Avatars/Service/Compressor/ParameterCompressorService.cs
// Changes:
//   - VRCFury's [VFService]/[VFAutowired] DI was replaced by constructor injection of NDMF's BuildContext plus the avatar's component.
//   - VRCFury's ParamsService copy-on-write dance was replaced by Modular Avatar's approach (ParameterInfoRegistry.NormalizeParameters):
//     make sure the descriptor points at a build-time temporary VRCExpressionParameters, then mutate it directly.
//     Parameters are deep-cloned so the user's original asset is never touched,
//     and sorted by name so the solver is deterministic.
//   - "Desktop is primary" was replaced by the component's PrimaryPlatform setting.
//   - fx.UpgradeWrongParamTypes() -> NDMF's AnimatorServicesContext.HarmonizeParameterTypes().
//   - SaveAssetsService.Run() was dropped; NDMF serialises build-time assets itself.
//   - CreateDebugInfo no longer adds a VRCFuryDebugInfo component; the same information is reported to the NDMF console (as Information),
//     and the legacy-compressor warning is gone with the legacy compressor.
//   - A secondary platform that cannot be aligned aborts the build, except under NDMF's "apply on play" processing,
//     where it falls back to solving locally (VRCFury's original behaviour on every failure)
//     so the avatar stays testable in play mode. Play mode never writes a record.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using FOSA.FPC.ErrorReporting;
using FOSA.FPC.Utils;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using nadena.dev.ndmf.util;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace FOSA.FPC.Service.Compressor {
    // Main entrypoint for compressing parameters to get them under the VRC limit
    internal class ParameterCompressorService {
        private readonly BuildContext context;
        private readonly AnimatorServicesContext asc;
        private readonly VRCAvatarDescriptor desc;
        private readonly FuryParameterCompressor component;

        public const float BATCH_TIME = 0.1f;

        public ParameterCompressorService(BuildContext context, VRCAvatarDescriptor desc, FuryParameterCompressor component) {
            this.context = context;
            this.desc = desc;
            this.component = component;
            asc = context.Extension<AnimatorServicesContext>();
        }

        public void Apply() {
            EnsureWritableParameters();
            var paramz = desc.expressionParameters;
            // Sorted before solving so the solver is deterministic.
            // Duplicates are deliberately NOT removed yet:
            // upstream VRCFury solves on the original,
            // possibly-duplicated list and only de-duplicates the copy it writes back,
            // so the solver has to see the duplicates too.
            // The sort is stable, so duplicate names keep their relative order.
            paramz.SortByName();

            var platformAlignmentService = new ParameterPlatformAlignmentService(desc, component);
            platformAlignmentService.WarnIfPrimaryPlatformChanged();

            ParameterCompressorSolverOutput decisionWithInfo;
            var alignedToPrimary = false;
            if (platformAlignmentService.IsPrimaryPlatform) {
                decisionWithInfo = Solve();
                paramz.RemoveDuplicates();
            } else {
                // Alignment (like upstream's AlignForMobile) always works on the de-duplicated set.
                paramz.RemoveDuplicates();
                decisionWithInfo = platformAlignmentService.AlignForSecondary();
                alignedToPrimary = decisionWithInfo != null;
                if (!alignedToPrimary) {
                    // An upload build cannot be aligned, so it stops here; the error was already reported.
                    if (!ParameterPlatformAlignmentService.IsPlayModeBuild) return;

                    // In play mode the failure was reported as a warning instead.
                    // Solve locally so the avatar stays testable, the way VRCFury's AlignForMobile always fell back.
                    // Whatever the merge managed to do before failing is kept:
                    // it can only make this closer to what the real upload would contain.
                    ReportPlayModeFallback();
                    decisionWithInfo = Solve();
                }
            }

            // Snapshot of the de-duplicated pre-compression set, which is what other platforms have to reproduce.
            // (Upstream likewise records its de-duplicated clone, not the duplicated list the solver saw.)
            var parametersBeforeCompression = paramz.parameters.CloneAll();
            var originalCost = paramz.parameters.CalcTotalCost();

            var decision = decisionWithInfo.decision;
            if (decision.compress.Any()) {
                // `|| fx == null` is not redundant:
                // AnimatorServicesContext.Controllers is a filtered dictionary view
                // whose TryGetValue succeeds for any registered key and resolves the value lazily,
                // and that resolution can yield null - NDMF's own GetAllControllers() filters nulls out for exactly this reason.
                // Without the null check we'd skip the error below and NRE inside BuildLayer instead.
                if (!asc.ControllerContext.Controllers.TryGetValue(VRCAvatarDescriptor.AnimLayerType.FX, out var fx) || fx == null) {
                    // Every VRChat avatar has an FX controller
                    // (NDMF substitutes VRChat's default one when the descriptor leaves the layer at its default),
                    // so this should be unreachable.
                    // Report it anyway: the expression parameters have already been replaced
                    // (and, on a secondary platform, merged),
                    // so returning silently would leave a half-applied avatar behind and -
                    // because the build produced no console entry at all -
                    // the previous build's report still on screen.
                    BuildReport.ReportingObject(component, () =>
                        BuildReport.LogFatal("error.compressor.no_fx_controller"));
                    return;
                }

                var layerUtilsService = new CompressorLayerUtilsService(paramz, asc, fx, component.writeDefaults);
                new ParameterCompressorLayerService(layerUtilsService, fx).BuildLayer(decision);

                var compressNames = decision.compress.Select(p => p.name).ToImmutableHashSet();
                foreach (var param in paramz.parameters.Where(p => compressNames.Contains(p.name))) {
                    param.SetNetworkSynced(false);
                }
            }

            var newCost = paramz.parameters.CalcTotalCost();

            // Closes the one gap the solution hash cannot cover:
            // the generated FPC/* slot names, which are created after the hash input was snapshotted.
            // Only meaningful when we actually replayed the primary platform's solution -
            // a play-mode fallback solved locally and is expected to differ.
            if (alignedToPrimary) {
                platformAlignmentService.VerifyFinalParametersMatchPrimary(paramz.parameters);
            }

            // Don't record a solution that the build already rejected, and never record one from play mode:
            // a record is the reference every other platform aligns against, so it should only ever come from a real build.
            if (platformAlignmentService.IsPlatformTracked
                && context.Successful
                && !ParameterPlatformAlignmentService.IsPlayModeBuild) {
                platformAlignmentService.SaveRecord(decision, parametersBeforeCompression, paramz.parameters);
            }

            if (!decision.compress.Any()) {
                ReportNoCompressionNeeded(originalCost);
                return;
            }

            asc.HarmonizeParameterTypes();
            CreateDebugInfo(decisionWithInfo, originalCost, newCost);
        }

        private ParameterCompressorSolverOutput Solve() {
            // The inspector deliberately allows blank and duplicate entries;
            // they're collapsed here, at the only point where the whitelist actually means anything.
            var whitelist = component.EffectiveParameterWhitelist.ToImmutableHashSet();
            return new ParameterCompressorSolverService(context, asc, desc, whitelist).GetParamsToOptimize();
        }

        // Play mode only.
        // The alignment failure itself was already reported (as a warning);
        // this explains what we did about it, since the resulting avatar may not match what an upload would produce.
        private void ReportPlayModeFallback() {
            BuildReport.ReportingObject(component, () =>
                BuildReport.Log(ErrorSeverity.NonFatal, "warning.compressor.play_mode_fallback",
                    component.PrimaryPlatform));
        }

        // Reported even though there is nothing to do, on purpose.
        // NDMF only refreshes the error report window when the new build produced at least one entry
        // (BuildContext: `if (!Application.isBatchMode && _report.Errors.Count > 0) ErrorReportWindow.ShowReport(_report)`),
        // so a build that reports nothing at all would leave the *previous* build's failure on screen.
        private void ReportNoCompressionNeeded(int cost) {
            if (!context.Successful) return;

            BuildReport.ReportingObject(component, () =>
                BuildReport.Log(ErrorSeverity.Information, "info.compressor.not_needed",
                    FormatBitsPlural(cost),
                    FormatBitsPlural(VRCExpressionParametersExtensions.GetMaxCost())));
        }

        // Source: nadena.dev.modular-avatar/Editor/SyncParameterSequence/ParameterInfoRegistry.cs
        // Extended to deep-clone the individual parameters,
        // since unlike MA we mutate them (networkSynced) and must not write back into the user's asset.
        private void EnsureWritableParameters() {
            VRCExpressionParameters newExpressionParametersAsset;
            if (desc.expressionParameters == null) {
                newExpressionParametersAsset = ScriptableObject.CreateInstance<VRCExpressionParameters>();
                newExpressionParametersAsset.name = "FPC Expression Parameters";
                newExpressionParametersAsset.parameters = new VRCExpressionParameters.Parameter[] { };
            } else if (context.IsTemporaryAsset(desc.expressionParameters)) {
                newExpressionParametersAsset = desc.expressionParameters;
                newExpressionParametersAsset.parameters =
                    (newExpressionParametersAsset.parameters ?? new VRCExpressionParameters.Parameter[] { }).CloneAll();
            } else {
                newExpressionParametersAsset = ScriptableObject.CreateInstance<VRCExpressionParameters>();
                newExpressionParametersAsset.name = desc.expressionParameters.name;
                newExpressionParametersAsset.parameters =
                    (desc.expressionParameters.parameters ?? new VRCExpressionParameters.Parameter[] { }).CloneAll();
            }
            desc.expressionParameters = newExpressionParametersAsset;
        }

        private void CreateDebugInfo(ParameterCompressorSolverOutput decisionWithInfo, int originalCost, int newCost) {
            var options = decisionWithInfo.options;
            var decision = decisionWithInfo.decision;
            var types = options.FormatTypes();

            var paramWarnings = decisionWithInfo.FormatWarnings();

            var batchCount = decision.GetBatchCount();
            // account for the extra frame hack needed in the layer generator,
            // which can add half a frame per batch. Assume 30fps.
            var minSyncTime = batchCount * (BATCH_TIME + (1 / 30f) * 0.5f);
            // Assume we just missed to the batch, so it has to do 2 full loops
            var maxSyncTime = minSyncTime * 2;

            BuildReport.ReportingObject(component, () =>
                BuildReport.Log(ErrorSeverity.Information, "info.compressor.result",
                    FormatBitsPlural(originalCost),
                    FormatBitsPlural(newCost),
                    string.IsNullOrEmpty(types) ? "-" : types,
                    minSyncTime.ToString("N1", CultureInfo.InvariantCulture),
                    maxSyncTime.ToString("N1", CultureInfo.InvariantCulture),
                    decision.boolSlots,
                    decision.numberSlots,
                    batchCount
                ));

            if (!string.IsNullOrEmpty(paramWarnings)) {
                BuildReport.ReportingObject(component, () =>
                    BuildReport.Log(ErrorSeverity.Information, "info.compressor.unsynced_suggestions", paramWarnings));
            }
        }

        public static string FormatBitsPlural(int numBits) {
            return numBits + " bit" + (numBits != 1 ? "s" : "");
        }
    }
}
