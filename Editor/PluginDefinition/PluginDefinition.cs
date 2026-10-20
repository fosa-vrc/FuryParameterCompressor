// Source: nadena.dev.modular-avatar/Editor/PluginDefinition/PluginDefinition.cs
// Source: nadena.dev.modular-avatar/Editor/SyncParameterSequence/SyncParameterSequencePass.cs
// Changes:
//   - VRCFury ran its compressor unconditionally as part of its own build pipeline.
//     Here the work only happens when the avatar carries a FuryParameterCompressor component,
//     and the component is removed when we're done.
//   - The Optimizing sequence declares AfterPlugin() against every NDMF plugin found in the project,
//     so that compression runs as late as possible -
//     after Modular Avatar and after any third-party plugin the user happens to have installed.
//     These are weak ordering constraints, so a plugin that doesn't run in this phase costs nothing.
//   - Conflict detection runs in the Resolving phase, because MA destroys its own components
//     (including ModularAvatarSyncParameterSequence) before the end of Optimizing.
//   - Modular Avatar is an optional dependency. The one MA type this pass needs is reached through
//     MaSyncParameterSequenceDetector, which is guarded by the FPC_MODULAR_AVATAR versionDefine and
//     reports "nothing found" when MA is not installed.

using System;
using System.Collections.Generic;
using System.Linq;
using FOSA.FPC.ErrorReporting;
using FOSA.FPC.Plugin;
using FOSA.FPC.Service.Compressor;
using FOSA.FPC.SyncParameterSequence;
using nadena.dev.ndmf;
using nadena.dev.ndmf.animator;
using Object = UnityEngine.Object;
using nadena.dev.ndmf.vrchat;

[assembly: ExportsPlugin(typeof(PluginDefinition))]

namespace FOSA.FPC.Plugin
{
    [RunsOnPlatforms(WellKnownPlatforms.VRChatAvatar30)]
    internal class PluginDefinition : Plugin<PluginDefinition>
    {
        public override string QualifiedName => "net.fosa.fury-parameter-compressor";
        public override string DisplayName => "Fury Parameter Compressor";

        protected override void OnUnhandledException(Exception e)
        {
            BuildReport.LogException(e);
        }

        protected override void Configure()
        {
            InPhase(BuildPhase.Resolving).Run(DetectConflictsPass.Instance);

            var seq = InPhase(BuildPhase.Optimizing);

            // Run as late as we possibly can in the Optimizing phase, after every other plugin:
            // parameters have to be final before we can decide what to compress.
            foreach (var qualifiedName in OtherPluginQualifiedNames())
            {
                seq.AfterPlugin(qualifiedName);
            }

            seq.WithRequiredExtension(typeof(AnimatorServicesContext), s => s.Run(ParameterCompressorPass.Instance));

            // Belt-and-braces removal on every NDMF platform.
            // On VRChat builds ParameterCompressorPass already destroyed the component in its finally block and this is a no-op;
            // on other platforms (e.g. Resonite) both passes above are skipped by their [RunsOnPlatforms] gate,
            // and FuryParameterCompressor only implements VRChat's IEditorOnly, which nothing else strips there.
            // MA purges its own components with a RunsOnAllPlatforms pass for the same reason.
            seq.Run(RemoveComponentPass.Instance);
        }

        // Every NDMF plugin in the project except ourselves.
        // Mirrors NDMF's internal PluginResolver.FindPluginTypes(), which is not public API.
        // Cached: Configure() is called for every PluginResolver NDMF builds (each build, plus editor UI queries),
        // and the answer cannot change without a domain reload clearing the static anyway.
        private static string[] cachedOtherPluginQualifiedNames;

        private static IEnumerable<string> OtherPluginQualifiedNames()
        {
            if (cachedOtherPluginQualifiedNames != null) return cachedOtherPluginQualifiedNames;

            var names = new SortedSet<string>(StringComparer.Ordinal);

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                object[] exports;
                try
                {
                    exports = assembly.GetCustomAttributes(typeof(ExportsPlugin), false);
                }
                catch (Exception)
                {
                    continue;
                }

                foreach (var export in exports.OfType<ExportsPlugin>())
                {
                    try
                    {
                        var plugin = export.PluginType.GetConstructor(Type.EmptyTypes)?.Invoke(Array.Empty<object>()) as PluginBase;
                        if (plugin == null) continue;
                        if (plugin.QualifiedName == Instance.QualifiedName) continue;
                        names.Add(plugin.QualifiedName);
                    }
                    catch (Exception)
                    {
                        // A plugin we can't instantiate is a plugin we can't order ourselves against.
                    }
                }
            }

            cachedOtherPluginQualifiedNames = names.ToArray();
            return cachedOtherPluginQualifiedNames;
        }
    }

    // Set up in the Resolving phase, before Modular Avatar removes its own components.
    internal class CompressorBuildState
    {
        public bool Skip;
    }

    [RunsOnPlatforms(WellKnownPlatforms.VRChatAvatar30)]
    internal class DetectConflictsPass : Pass<DetectConflictsPass>
    {
        public override string DisplayName => "Check for conflicts";

#if FPC_VRCFURY
        private static readonly bool VrcFuryInstalled = true;
#else
        // The versionDefines check only sees VRCFury installed as a package;
        // a copy dropped under Assets/ still compiles its "VRCFury" runtime assembly, so probe for that as a fallback.
        private static readonly bool VrcFuryInstalled =
            AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "VRCFury");
#endif

        // Source: com.vrcfury.vrcfury/Editor-Avatars/Menu/CompressorMenuItem.cs
        // VRCFury's "When avatar is over parameter limit" setting: 0 = Compress, 1 = Ask, 2 = Fail.
        // A pref that was never set reads as 0, the same default VRCFury uses.
        private const string VrcFuryCompressorPref = "com.vrcfury.parameterCompressor";

        /// <summary>
        ///     True when VRCFury is installed and set to compress parameters itself, so it takes over from us.
        ///     In Ask / Fail mode VRCFury doesn't compress silently, so we do it ourselves.
        ///     Also read by FuryParameterCompressorEditor.
        ///     Not cached: the pref can be changed from VRCFury's menu at any time without a domain reload.
        /// </summary>
        internal static bool VrcFuryPresent =>
            VrcFuryInstalled && UnityEditor.EditorPrefs.GetInt(VrcFuryCompressorPref, 0) == 0;

        protected override void Execute(BuildContext context)
        {
            var components = context.AvatarRootObject.GetComponentsInChildren<FuryParameterCompressor>(true);
            if (components.Length == 0) return;

            var state = context.GetState<CompressorBuildState>();

            if (components.Length > 1)
            {
                BuildReport.LogFatal("error.singleton", "Fury Parameter Compressor", components.Cast<object>().ToArray());
                state.Skip = true;
                return;
            }

            var component = components[0];

            if (VrcFuryPresent)
            {
                // VRCFury has its own parameter compressor; let it do the work.
                BuildReport.ReportingObject(component, () =>
                    BuildReport.Log(ErrorSeverity.NonFatal, "warning.compressor.vrcfury_present"));
                state.Skip = true;
                return;
            }

            var maSyncParams = MaSyncParameterSequenceDetector.Find(context.AvatarRootObject);
            if (maSyncParams.Length > 0)
            {
                BuildReport.ReportingObject(component, () =>
                    BuildReport.LogFatal("error.compressor.ma_sync_parameter_sequence", maSyncParams.Cast<object>().ToArray()));
                state.Skip = true;
            }
        }
    }

    [RunsOnAllPlatforms]
    internal class RemoveComponentPass : Pass<RemoveComponentPass>
    {
        public override string DisplayName => "Remove component";

        protected override void Execute(BuildContext context)
        {
            foreach (var component in context.AvatarRootObject.GetComponentsInChildren<FuryParameterCompressor>(true))
            {
                Object.DestroyImmediate(component);
            }
        }
    }

    [RunsOnPlatforms(WellKnownPlatforms.VRChatAvatar30)]
    internal class ParameterCompressorPass : Pass<ParameterCompressorPass>
    {
        public override string DisplayName => "Compress parameters";

        protected override void Execute(BuildContext context)
        {
            var components = context.AvatarRootObject.GetComponentsInChildren<FuryParameterCompressor>(true);
            if (components.Length == 0) return;

            try
            {
                if (context.GetState<CompressorBuildState>().Skip) return;

                var desc = context.VRChatAvatarDescriptor();
                if (desc == null) return;

                var component = components[0];
                BuildReport.ReportingObject(component, () => new ParameterCompressorService(context, desc, component).Apply());
            }
            finally
            {
                foreach (var component in components)
                {
                    Object.DestroyImmediate(component);
                }
            }
        }
    }
}
