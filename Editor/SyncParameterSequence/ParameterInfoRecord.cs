// Source: nadena.dev.modular-avatar/Editor/SyncParameterSequence/ParameterInfoRecord.cs
// Changes:
//   - Added the fields needed to replay a compression solution on a secondary platform and to validate it:
//     SaveVersion, ToolVersion, NumberSlots, BoolSlots, CompressedParameters, SolutionHash.
//   - Added ComputeSolutionHash(), a deterministic SHA-256 over the canonical form of the record.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;

namespace FOSA.FPC.SyncParameterSequence
{
    internal class ParameterInfoRecord
    {
        /// <summary>
        ///     Bumped whenever the on-disk format or the solver output changes in a way that makes older records unusable.
        /// </summary>
        public const int CurrentSaveVersion = 1;

        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        public List<ParameterDefinition> WantedParameters { get; set; } = new();
        public List<ParameterDefinition> ActualParameters { get; set; } = new();

        public BuildTarget Target { get; set; } = BuildTarget.NoTarget;

        public int SaveVersion { get; set; } = CurrentSaveVersion;

        /// <summary>
        ///     The version of this package that produced the record.
        ///     Secondary platforms refuse to replay a solution produced by a different version.
        /// </summary>
        public string ToolVersion { get; set; } = "";

        public int NumberSlots { get; set; }
        public int BoolSlots { get; set; }

        /// <summary>
        ///     The names of the parameters that were compressed, in solver order.
        ///     Secondary platforms replay exactly this list rather than solving again.
        /// </summary>
        public List<string> CompressedParameters { get; set; } = new();

        public string SolutionHash { get; set; } = "";

        /// <summary>
        ///     A deterministic fingerprint of the solver input
        ///     (the parameters the avatar wanted synced before compression) plus the solver output.
        ///     Secondary platforms recompute this after merging in the primary platform's parameters
        ///     and abort the build if it doesn't match.
        /// </summary>
        public string ComputeSolutionHash()
        {
            var sb = new StringBuilder();
            sb.Append(ToolVersion).Append('\u0002');
            sb.Append(SaveVersion.ToString(CultureInfo.InvariantCulture)).Append('\u0002');
            sb.Append(NumberSlots.ToString(CultureInfo.InvariantCulture)).Append('\u0002');
            sb.Append(BoolSlots.ToString(CultureInfo.InvariantCulture)).Append('\u0002');
            foreach (var p in WantedParameters)
            {
                sb.Append(p.Canonical()).Append('\u0003');
            }

            sb.Append('\u0002');
            foreach (var name in CompressedParameters)
            {
                sb.Append(name).Append('\u0003');
            }

            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
            return string.Concat(hash.Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));
        }

        private sealed class ActualParametersEqualityComparer : IEqualityComparer<ParameterInfoRecord>
        {
            public bool Equals(ParameterInfoRecord x, ParameterInfoRecord y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (x is null) return false;
                if (y is null) return false;
                if (x.GetType() != y.GetType()) return false;
                return x.ActualParameters.SequenceEqual(y.ActualParameters, ParameterDefinition.CriticalValueComparer);
            }

            public int GetHashCode(ParameterInfoRecord obj)
            {
                // We're not going to be hashing with this comparer
                return 0;
            }
        }

        public static IEqualityComparer<ParameterInfoRecord> ActualParametersComparer { get; } =
            new ActualParametersEqualityComparer();
    }
}
