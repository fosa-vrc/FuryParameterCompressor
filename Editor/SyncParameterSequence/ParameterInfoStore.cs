// Source: nadena.dev.modular-avatar/Editor/SyncParameterSequence/ParameterInfoStore.cs
// Changes:
//   - The library folder was changed to Library/net.fosa.fury-parameter-compressor/AvatarParameterInfo.
//     Nothing is ever written outside the project (in particular, nothing goes to LocalAppData,
//     unlike VRCFury's ParameterPlatformAlignmentService which wrote to %LOCALAPPDATA%/VRCFury/DesktopSyncData).
//   - UpdateRecordForPlatform became UpdatePrimaryRecord: alignment only ever reads PrimaryPlatformRecord,
//     so MA's per-secondary-platform record list was dropped along with the branch that maintained it.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace FOSA.FPC.SyncParameterSequence
{
    internal interface IParameterInfoStore
    {
        AvatarRecord GetRecordForBlueprintId(string blueprintId);
        AvatarRecord UpdatePrimaryRecord(string blueprintId, ParameterInfoRecord record);
    }

    internal abstract class AbstractParameterInfoStore : IParameterInfoStore
    {
        protected abstract void Store(string blueprintId, string serialized);
        protected abstract string? Load(string blueprintId);

        // Explicit nulls in a hand-edited (or truncated-but-still-parseable) record must not overwrite the non-null defaults -
        // empty lists and strings - that the version and hash gates dereference.
        // Ignoring them keeps such a record on the graceful "record invalid" path instead of throwing a NullReferenceException mid-pass.
        private static readonly JsonSerializerSettings DeserializeSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        };

        private static AvatarRecord? Deserialize(string serialized)
        {
            var record = JsonConvert.DeserializeObject<AvatarRecord>(serialized, DeserializeSettings);

            // NullValueHandling above only covers null property *values*;
            // null *elements* inside the arrays would still reach ComputeSolutionHash and NRE.
            // Dropping them changes the hash input, so a record tampered with this way fails the hash gate gracefully.
            var primary = record?.PrimaryPlatformRecord;
            if (primary != null)
            {
                primary.WantedParameters?.RemoveAll(p => p == null);
                primary.ActualParameters?.RemoveAll(p => p == null);
                primary.CompressedParameters?.RemoveAll(n => n == null);
            }

            return record;
        }

        public AvatarRecord GetRecordForBlueprintId(string blueprintId)
        {
            try
            {
                var serialized = Load(blueprintId);
                return serialized == null
                    ? new AvatarRecord()
                    : Deserialize(serialized) ?? new AvatarRecord();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return new AvatarRecord();
            }
        }

        public AvatarRecord UpdatePrimaryRecord(string blueprintId, ParameterInfoRecord record)
        {
            // Reading the existing file is separated from writing the new one on purpose:
            // a corrupt existing record (e.g. truncated by an editor crash mid-write) must not prevent the write,
            // because overwriting it here is the only way it can ever heal -
            // secondary platforms would otherwise fail on it forever with no rebuild able to fix it.
            AvatarRecord avatarRecord;
            try
            {
                var current = Load(blueprintId);
                avatarRecord = current == null
                    ? new AvatarRecord()
                    : Deserialize(current) ?? new AvatarRecord();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                avatarRecord = new AvatarRecord();
            }

            try
            {
                avatarRecord.PrimaryPlatformRecord = record;

                Store(blueprintId, JsonConvert.SerializeObject(avatarRecord));
                return avatarRecord;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return avatarRecord;
            }
        }
    }

    internal sealed class ParameterInfoStore : AbstractParameterInfoStore
    {
        public static IParameterInfoStore Instance { get; } = new ParameterInfoStore();

        private const string LibraryFolderPath = "Library/net.fosa.fury-parameter-compressor/AvatarParameterInfo";

        private ParameterInfoStore()
        {
        }

        private void EnsureFolderExists()
        {
            if (!Directory.Exists(LibraryFolderPath))
            {
                Directory.CreateDirectory(LibraryFolderPath);
            }
        }

        protected override void Store(string blueprintId, string serialized)
        {
            try
            {
                EnsureFolderExists();
                var path = Path.Combine(LibraryFolderPath, blueprintId);
                File.WriteAllText(path, serialized);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        protected override string? Load(string blueprintId)
        {
            try
            {
                EnsureFolderExists();
                var path = Path.Combine(LibraryFolderPath, blueprintId);
                if (!File.Exists(path)) return null;
                return File.ReadAllText(path);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return null;
            }
        }
    }
}
