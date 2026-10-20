// Source: nadena.dev.modular-avatar/Runtime/ModularAvatarSyncParameterSequence.cs
// Changes:
//   - New file with no direct upstream counterpart.
//     It exists so that Modular Avatar is an *optional* dependency of this package:
//     this is the only place that names an MA type, and everything in it lives behind FPC_MODULAR_AVATAR
//     (set by a versionDefine in the editor asmdef).
//   - With MA absent, an MA Sync Parameter Sequence component cannot be on the avatar in the first place,
//     so both entry points answer "not present" and the mutual-exclusion check simply never fires.
//     Nothing else about the build changes.

using UnityEngine;
#if FPC_MODULAR_AVATAR
using nadena.dev.modular_avatar.core;
#endif
using Object = UnityEngine.Object;

namespace FOSA.FPC.SyncParameterSequence
{
    /// <summary>
    ///     Finds Modular Avatar's own cross-platform parameter alignment component. Fury Parameter
    ///     Compressor does that job itself, so the two are mutually exclusive - see DetectConflictsPass.
    /// </summary>
    internal static class MaSyncParameterSequenceDetector
    {
        /// <summary>
        ///     Every MA Sync Parameter Sequence component on the avatar, for use as NDMF error context
        ///     objects. Always empty when Modular Avatar is not installed.
        /// </summary>
        internal static Object[] Find(GameObject avatarRoot)
        {
#if FPC_MODULAR_AVATAR
            if (avatarRoot == null) return System.Array.Empty<Object>();
            return avatarRoot.GetComponentsInChildren<ModularAvatarSyncParameterSequence>(true);
#else
            return System.Array.Empty<Object>();
#endif
        }

        internal static bool Exists(GameObject avatarRoot)
        {
#if FPC_MODULAR_AVATAR
            return avatarRoot.GetComponentInChildren<ModularAvatarSyncParameterSequence>(true) != null;
#else
            return false;
#endif
        }
    }
}
