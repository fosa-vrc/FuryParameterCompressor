// Source: com.vrcfury.vrcfury/Editor-Common/Utils/Controller/VFABool.cs
// Changes:
//   - Dropped AsFloat() (VFAFloat is not part of this port) and the unused `def` field.

using UnityEditor.Animations;

namespace FOSA.FPC.Utils.Controller {
    internal class VFABool : VFAParam {
        public VFABool(string name) : base(name) {
        }

        public VFCondition IsTrue() {
            return Is(true);
        }
        public VFCondition IsFalse() {
            return Is(false);
        }
        public VFCondition Is(bool state) {
            return new VFCondition(new AnimatorCondition { mode = state ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, parameter = this, threshold = 0 });
        }
    }
}
