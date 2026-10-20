// Source: com.vrcfury.vrcfury/Editor-Avatars/VRCFPackageUtils.cs
// Changes:
//   - The package is located through PackageInfo.FindForAssembly instead of an asset GUID,
//     and the [VFInit] hook that pushed the version into VRCFury's upgradeable components was dropped.
//   - Added AssetPath / ResolvedPath, the two roots the rest of the package uses to reach its own files.
//     Upstream only ever needed a version number.
//   - GetVersionFromId's id parameter was dropped: it is only ever called for this package,
//     and the path now comes from the resolved package root rather than being built from the id.

using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace FOSA.FPC {
    internal static class FPCPackageUtils {
        private const string PackageId = "net.fosa.fury-parameter-compressor";
        private const string EditorAsmdefName = PackageId + ".editor";
        private const string FallbackAssetPath = "Packages/" + PackageId;

        /// <summary>
        ///     The package root as the AssetDatabase addresses it ("Packages/{id}" for an installed package,
        ///     "Assets/..." if the folder was dropped into Assets instead). Use this for AssetDatabase.LoadAssetAtPath.
        /// </summary>
        public static string AssetPath {
            get {
                Resolve();
                return assetPath ?? FallbackAssetPath;
            }
        }

        /// <summary>
        ///     The package root as an absolute filesystem path. Use this for System.IO,
        ///     which cannot see the AssetDatabase's virtual "Packages/{id}" paths -
        ///     on disk the folder may be named anything at all (a git clone into Packages/ rarely matches the package id).
        /// </summary>
        public static string ResolvedPath {
            get {
                Resolve();
                return resolvedPath ?? FallbackAssetPath;
            }
        }

        public static string Version {
            get {
                if (version != null) return version;

                var loaded = LoadVersion(Path.Combine(ResolvedPath, "package.json"));
                // Don't cache a failed lookup: it may just mean the path wasn't resolvable yet.
                if (loaded != UnknownVersion) version = loaded;
                return loaded;
            }
        }

        private const string UnknownVersion = "???";

        private static string assetPath;
        private static string resolvedPath;
        private static string version;

        private static void Resolve() {
            if (assetPath != null) return;

            var package = PackageInfo.FindForAssembly(typeof(FPCPackageUtils).Assembly);
            if (package != null) {
                assetPath = package.assetPath;
                resolvedPath = package.resolvedPath;
                return;
            }

            // Not registered with the package manager - the folder was placed under Assets/.
            // Locate ourselves through our own assembly definition instead.
            // This is the only branch that touches the AssetDatabase,
            // and it can be reached from [InitializeOnLoad] (Localization's static constructor loads its json eagerly),
            // so a failure is deliberately left uncached:
            // the callers fall back to FallbackAssetPath for now and we try again once the AssetDatabase is ready.
            var root = FindRootByAsmdef();
            if (root == null) return;

            assetPath = root;
            resolvedPath = Path.GetFullPath(root);
        }

        private static string FindRootByAsmdef() {
            try {
                foreach (var guid in AssetDatabase.FindAssets(EditorAsmdefName + " t:AssemblyDefinitionAsset")) {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!path.EndsWith("/" + EditorAsmdefName + ".asmdef", StringComparison.Ordinal)) continue;

                    // <root>/Editor/<id>.editor.asmdef -> <root>
                    var editorDir = Path.GetDirectoryName(path);
                    var parent = editorDir == null ? null : Path.GetDirectoryName(editorDir);
                    if (!string.IsNullOrEmpty(parent)) return parent.Replace('\\', '/');
                }
            } catch (Exception) {
                // AssetDatabase isn't usable yet; try again on the next call.
            }

            return null;
        }

        private static string LoadVersion(string packageJsonPath) {
            try {
                if (!string.IsNullOrEmpty(packageJsonPath) && File.Exists(packageJsonPath)) {
                    var text = File.ReadAllText(packageJsonPath);
                    return JsonUtility.FromJson<PackageManifestData>(text).version;
                }
            } catch (Exception) {
                // ignored
            }
            return UnknownVersion;
        }

        private class PackageManifestData {
            public string version;
        }
    }
}
