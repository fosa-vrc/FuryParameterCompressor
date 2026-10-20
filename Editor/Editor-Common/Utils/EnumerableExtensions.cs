// Source: com.vrcfury.vrcfury/Editor-Common/Utils/EnumerableExtensions.cs
// Changes:
//   - Trimmed to the members the compressor actually uses (NotNull, Chunk, Join).
//   - NotNull() no longer uses `dynamic` (which is unavailable without Microsoft.CSharp);
//     the compressor only ever calls it on strings.

using System.Collections.Generic;
using System.Linq;

namespace FOSA.FPC.Utils {
    internal static class EnumerableExtensions {
        public static IEnumerable<T> NotNull<T>(this IEnumerable<T> source) where T : class {
            return source.Where(e => e != null);
        }

        public static IEnumerable<IEnumerable<T>> Chunk<T>(this IEnumerable<T> source, int chunkSize) {
            return source
                .Select((e,i) => (e,i))
                .GroupBy(x => x.i / chunkSize)
                .Select(g => g.Select(x => x.e));
        }

        public static string Join(this IEnumerable<string> source, string separator) {
            return string.Join(separator, source);
        }
        public static string Join(this IEnumerable<string> source, char separator) {
            return string.Join(separator, source);
        }
    }
}
