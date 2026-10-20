// Source: nadena.dev.modular-avatar/Editor/ErrorReporting/ErrorLog.cs
// Changes:
//   - Uses this package's Localizer instead of MA's.
//   - Dropped the unused Path constant, LogException's default-arg variant is kept,
//     and the obsolete RemapPaths stub was removed.

using System;
using nadena.dev.ndmf;

namespace FOSA.FPC.ErrorReporting
{
    internal class BuildReport
    {
        internal static void Log(ErrorSeverity severity, string code, params object[] objects)
        {
            ErrorReport.ReportError(Localization.L, severity, code, objects);
        }

        internal static void LogFatal(string code, params object[] objects)
        {
            ErrorReport.ReportError(Localization.L, ErrorSeverity.Error, code, objects);
        }

        internal static void LogException(Exception e, string additionalStackTrace = "")
        {
            ErrorReport.ReportException(e, additionalStackTrace);
        }

        internal static T ReportingObject<T>(UnityEngine.Object obj, Func<T> action)
        {
            return ErrorReport.WithContextObject(obj, action);
        }

        internal static void ReportingObject(UnityEngine.Object obj, Action action)
        {
            ErrorReport.WithContextObject(obj, action);
        }
    }
}
