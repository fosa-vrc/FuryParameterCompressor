// Source: nadena.dev.modular-avatar/Runtime/ModularAvatarSyncParameterSequence.cs
// Changes:
//   - Base class changed from AvatarTagComponent to MonoBehaviour + IEditorOnly,
//     so that MA's "Purge ModularAvatar components" pass (LateTransformPluginDefinition) does not destroy this component before our pass,
//     which deliberately runs at the very end of the Transforming phase.
//   - Platform enum kept as-is (PC/Android/iOS) but the default is PC instead of Android.
//   - Dropped the obsolete `Parameters` field, dropped Reset()/RuntimeUtil.IsResetFromInspector().
//   - Added writeDefaults (Auto/ForceOn/ForceOff, "Auto" mirrors MA Merge Animator's matchAvatarWriteDefaults)
//     and parameterWhitelist (parameters excluded from compression), which have no equivalent in the Modular Avatar original.

using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;
using VRC.SDKBase;

namespace FOSA.FPC
{
    [AddComponentMenu("FOSA/Fury Parameter Compressor")]
    [DisallowMultipleComponent]
    [PublicAPI]
    public class FuryParameterCompressor : MonoBehaviour, IEditorOnly
    {
        /// <summary>
        ///     The platform whose solver result is authoritative.
        ///     Secondary platforms replay the recorded result instead of solving again,
        ///     so that parameters line up across platforms.
        /// </summary>
        [Serializable]
        [PublicAPI]
        public enum Platform
        {
            PC,
            Android,
            iOS
        }

        /// <summary>
        ///     How Write Defaults is set on the generated compressor layer.
        /// </summary>
        [Serializable]
        [PublicAPI]
        public enum WriteDefaultsMode
        {
            /// <summary>
            ///     Match the rest of the FX controller (see MA Merge Animator's "Match Avatar Write Defaults").
            ///     If the avatar mixes both settings, Write Defaults is turned off.
            /// </summary>
            Auto,

            ForceOn,
            ForceOff
        }

        public Platform PrimaryPlatform = Platform.PC;

        public WriteDefaultsMode writeDefaults = WriteDefaultsMode.Auto;

        /// <summary>
        ///     Parameters listed here are never compressed.
        ///     They keep syncing at full speed and keep costing their full bit count.
        /// </summary>
        public List<string> parameterWhitelist = new List<string>();

        /// <summary>
        ///     The whitelist as the build actually applies it:
        ///     blank entries dropped, everything else matched verbatim.
        ///     Names are deliberately NOT trimmed - VRChat allows parameter names with leading or trailing whitespace,
        ///     and trimming would make such a parameter impossible to whitelist at all.
        ///     The inspector keeps the raw list editable, duplicates and all.
        /// </summary>
        public IEnumerable<string> EffectiveParameterWhitelist =>
            (parameterWhitelist ?? Enumerable.Empty<string>()).Where(n => !string.IsNullOrEmpty(n));
    }
}
