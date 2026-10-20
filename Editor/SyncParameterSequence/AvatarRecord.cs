// Source: nadena.dev.modular-avatar/Editor/SyncParameterSequence/AvatarRecord.cs
// Changes:
//   - Namespace (nadena.dev.modular_avatar.core.editor.SyncParameterSequence -> FOSA.FPC.SyncParameterSequence).
//   - MA's IsConsistent property was dropped.
//     Nothing here uses it (the equivalent check is ParameterPlatformAlignmentService.VerifyFinalParametersMatchPrimary),
//     and as a public getter Newtonsoft serialised it into the record as a dead field -
//     one that throws while writing if any record's ActualParameters is null, which the store's catch would swallow,
//     silently dropping the save.
//   - MA's SecondaryPlatformRecords list was dropped too. MA reads it;
//     we never do - alignment only ever consults PrimaryPlatformRecord -
//     so it was write-only data that grew the stored json with one dead entry per platform built.
//     Records written before this change still load: Newtonsoft ignores json properties that no longer exist on the type.

#nullable enable

namespace FOSA.FPC.SyncParameterSequence
{
    internal class AvatarRecord
    {
        public ParameterInfoRecord? PrimaryPlatformRecord { get; set; }
    }
}
