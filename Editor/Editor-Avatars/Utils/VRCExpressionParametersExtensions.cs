// Source: com.vrcfury.vrcfury/Editor-Avatars/Utils/VRCExpressionParametersExtensions.cs
// Changes:
//   - GetMaxCost() dropped the DexProtect adjustment (VRCFury-specific integration).
//   - Dropped RewriteParameters/IsSameAs (unused here) and all .Dirty() calls:
//     under NDMF we only ever mutate the build-time temporary VRCExpressionParameters asset.
//   - Added SortByName/CalcTotalCost helpers used to make the solver deterministic.

using System;
using System.Collections.Generic;
using System.Linq;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace FOSA.FPC.Utils {
    internal static class VRCExpressionParametersExtensions {
        public static int GetMaxCost() {
            var maxBits = VRCExpressionParameters.MAX_PARAMETER_COST;
            if (maxBits > 9999) {
                // Some modified versions of the VRChat SDK have a broken value for this
                maxBits = 256;
            }
            return maxBits;
        }

        public static void RemoveDuplicates(this VRCExpressionParameters paramz) {
            var seenParams = new HashSet<string>();
            paramz.parameters = paramz.parameters.Where(p => seenParams.Add(p.name)).ToArray();
        }

        // Sorts parameters by name using an ordinal comparison,
        // so that the solver sees the same input regardless of the order components happened to be merged in.
        // Mirrors Modular Avatar's ParameterInfoRegistry.NormalizeParameters, but with a stable sort:
        // the solver runs before duplicates are removed,
        // so duplicate names must keep their relative order or the sort itself would be a source of nondeterminism.
        public static void SortByName(this VRCExpressionParameters paramz) {
            paramz.parameters = paramz.parameters.OrderBy(p => p.name, StringComparer.Ordinal).ToArray();
        }

        public static void Add(this VRCExpressionParameters paramz, VRCExpressionParameters.Parameter param) {
            var exists = paramz.Get(param.name);
            if (exists != null) {
                if (param.valueType != exists.valueType) {
                    throw new Exception(
                        $"FPC tried to create expression parameter {param.name} with type {param.valueType}," +
                        $" but parameter already exists with type {exists.valueType}");
                }
                return;
            }
            paramz.parameters = paramz.parameters.Concat(new [] {param}).ToArray();
        }

        public static VRCExpressionParameters.Parameter Get(this VRCExpressionParameters paramz, string name) {
            return paramz.parameters.FirstOrDefault(p => p.name == name);
        }

        public static int CalcTotalCost(this IEnumerable<VRCExpressionParameters.Parameter> parameters) {
            return parameters.Where(p => p.IsNetworkSynced()).Sum(p => p.TypeCost());
        }
    }
}
