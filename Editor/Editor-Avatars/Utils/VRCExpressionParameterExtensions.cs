// Source: com.vrcfury.vrcfury/Editor-Avatars/Utils/VRCExpressionParameterExtensions.cs
// Changes:
//   - Dropped the ReflectionHelper-based `networkSynced` lookup (and SupportsUnsynced):
//     this package requires a VRCSDK that has the field, so it is accessed directly,
//     the same way Modular Avatar does in ParameterInfoRegistry.ConvertParameters.
//   - Clone() goes through a JsonUtility round-trip instead of VRCFury's UnitySerializationUtils.
//     Both are field-agnostic on purpose: EnsureWritableParameters clones the user's parameters into the build-time asset,
//     so an explicit field-by-field copy would silently drop any field a future VRCSDK adds to Parameter (networkSynced itself was added this way).
//   - Dropped IsSameAs: it only existed to serve the asset-level VRCExpressionParameters.IsSameAs, which this port doesn't use.

using System.Linq;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace FOSA.FPC.Utils {
    internal static class VRCExpressionParameterExtensions {
        public static bool IsNetworkSynced(this VRCExpressionParameters.Parameter param) {
            return param.networkSynced;
        }

        public static void SetNetworkSynced(this VRCExpressionParameters.Parameter param, bool networkSynced) {
            param.networkSynced = networkSynced;
        }

        public static VRCExpressionParameters.Parameter Clone(this VRCExpressionParameters.Parameter param) {
            return JsonUtility.FromJson<VRCExpressionParameters.Parameter>(JsonUtility.ToJson(param));
        }

        // Named CloneAll rather than Clone
        // because Array.Clone() would win name resolution against an extension method and return a shallow copy.
        public static VRCExpressionParameters.Parameter[] CloneAll(this VRCExpressionParameters.Parameter[] parameters) {
            return parameters.Select(Clone).ToArray();
        }

        public static int TypeCost(this VRCExpressionParameters.Parameter param) {
            return VRCExpressionParameters.TypeCost(param.valueType);
        }
    }
}
