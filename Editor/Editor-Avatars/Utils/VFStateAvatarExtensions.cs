// Source: com.vrcfury.vrcfury/Editor-Avatars/Utils/VFStateAvatarExtensions.cs
// Changes:
//   - Operates on NDMF's VirtualState instead of VRCFury's VFState;
//     behaviours are created with ScriptableObject.CreateInstance
//     (the same way Modular Avatar's MMDRelayPass creates VRCAnimatorLayerControl behaviours) and appended to VirtualState.Behaviours.
//   - Dropped DrivesRandom/DrivesDelta and the VRCSDK_HAS_DRIVER_COPY guard (unused / this package requires a VRCSDK that has parameter driver Copy).
//   - Added TransitionsTo/AddTransition helpers replacing VRCFury's VFTransitionBuilder DSL:
//     a VFCondition is a disjunction of conjunctions, so each disjunct becomes its own transition,
//     which is exactly what VFTransitionBuilder did.

using System.Collections.Immutable;
using System.Linq;
using FOSA.FPC.Utils.Controller;
using nadena.dev.ndmf.animator;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace FOSA.FPC.Utils {
    internal static class VFStateAvatarExtensions {
        private static VRCAvatarParameterDriver GetDriver(this VirtualState state) {
            var exists = state.Behaviours
                .OfType<VRCAvatarParameterDriver>()
                .FirstOrDefault(b => b != null && !b.localOnly);
            if (exists != null) {
                return exists;
            }
            var driver = ScriptableObject.CreateInstance<VRCAvatarParameterDriver>();
            driver.name = "Parameter Driver";
            driver.localOnly = false;
            state.Behaviours = state.Behaviours.Add(driver);
            return driver;
        }

        private static VirtualState AddDriverParameter(
            this VirtualState state,
            string param,
            System.Action<VRC_AvatarParameterDriver.Parameter> edit
        ) {
            var driver = state.GetDriver();
            var p = new VRC_AvatarParameterDriver.Parameter {
                name = param,
                type = VRC_AvatarParameterDriver.ChangeType.Set
            };
            edit?.Invoke(p);
            driver.parameters.Add(p);
            return state;
        }

        public static VirtualState Drives(this VirtualState state, VFABool param, bool value) {
            return state.AddDriverParameter(param.Name(), p => p.value = value ? 1 : 0);
        }

        public static VirtualState DrivesCopy(this VirtualState state, string from, string to, float fromMin = 0, float fromMax = 0, float toMin = 0, float toMax = 0) {
            var driver = state.GetDriver();
            var p = new VRC_AvatarParameterDriver.Parameter {
                name = to,
                source = from,
                type = VRC_AvatarParameterDriver.ChangeType.Copy
            };

            if (fromMin != 0 || fromMax != 0) {
                p.sourceMin = fromMin;
                p.sourceMax = fromMax;
                p.destMin = toMin;
                p.destMax = toMax;
                p.convertRange = true;
            }

            driver.parameters.Add(p);
            return state;
        }

        // Adds one transition per disjunct of `when`.
        // A null `when` means "always" (VRCFury's `.When()` with no arguments);
        // a VFCondition with no disjuncts means "never",
        // i.e. no transition at all (VRCFury's VFCondition.Never()).
        // `exitTime` of less than zero means "no exit time".
        public static void TransitionsTo(this VirtualState from, VirtualState to, VFCondition when = null, float exitTime = -1) {
            if (when == null) {
                from.Transitions = from.Transitions.Add(MakeTransition(to, null, exitTime));
                return;
            }

            foreach (var conjunction in when.transitions) {
                from.Transitions = from.Transitions.Add(MakeTransition(to, conjunction, exitTime));
            }
        }

        private static VirtualStateTransition MakeTransition(VirtualState to, AnimatorCondition[] conditions, float exitTime) {
            var transition = VirtualStateTransition.Create();
            transition.SetDestination(to);
            transition.Duration = 0;
            transition.HasFixedDuration = true;
            transition.CanTransitionToSelf = false;
            transition.ExitTime = exitTime >= 0 ? exitTime : (float?)null;
            if (conditions != null && conditions.Length > 0) {
                transition.Conditions = conditions.ToImmutableList();
            }
            return transition;
        }
    }
}
