// Source: com.vrcfury.vrcfury/Editor-Avatars/Service/Compressor/ParameterCompressorSolverOutput.cs
// Changes:
//   - The hardcoded English warning strings were moved into the localization files.
//     The en-US values are VRCFury's original wording with "VRCFury" replaced by the name of this tool;
//     zh-Hans is a translation of the same text.
//   - FormatWarnings deliberately no longer truncates the lists
//     (upstream capped them at 20 entries in the failure message and 100 in the debug info):
//     the NDMF console scrolls, and a cut-off list would hide exactly the parameters the user is being asked to review.

using System.Collections.Generic;
using System.Linq;
using FOSA.FPC.Utils;
using JetBrains.Annotations;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace FOSA.FPC.Service.Compressor {
    internal class ParameterCompressorSolverOutput {
        public OptimizationDecision decision = new OptimizationDecision();
        public ParameterCompressorSolverService.ParamSelectionOptions options = new ParameterCompressorSolverService.ParamSelectionOptions();
        public IList<VRCExpressionParameters.Parameter> warnUnusedParams = new List<VRCExpressionParameters.Parameter>();
        public IList<VRCExpressionParameters.Parameter> warnContactParams = new List<VRCExpressionParameters.Parameter>();
        public IList<VRCExpressionParameters.Parameter> warnButtonParams = new List<VRCExpressionParameters.Parameter>();
        public IList<VRCExpressionParameters.Parameter> warnOscOnlyParams = new List<VRCExpressionParameters.Parameter>();

        public string FormatWarnings() {
            var lines = new [] {
                FormatWarnings(Localization.S("compressor.warn.unused"), warnUnusedParams),
                FormatWarnings(Localization.S("compressor.warn.contact"), warnContactParams),
                FormatWarnings(Localization.S("compressor.warn.button"), warnButtonParams),
                FormatWarnings(Localization.S("compressor.warn.osconly"), warnOscOnlyParams),
            }.NotNull().Join("\n\n");
            if (!string.IsNullOrEmpty(lines)) {
                return Localization.S("compressor.warn.header") + "\n\n" + lines;
            }
            return "";
        }

        [CanBeNull]
        private static string FormatWarnings(string title, IList<VRCExpressionParameters.Parameter> list) {
            if (list.Count == 0) return null;
            return title + "\n" + list.Select(p => $"{p.name} ({ParameterCompressorService.FormatBitsPlural(p.TypeCost())})").Join("\n");
        }
    }
}
