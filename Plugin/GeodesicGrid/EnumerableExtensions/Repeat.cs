using System;
using System.Collections.Generic;

namespace GeodesicGrid.EnumerableExtensions
{
    public static class Repeat
    {
        public static IEnumerable<T> RepeatFirst<T>(this IEnumerable<T> sequence)
        {
            if (sequence == null) { throw new ArgumentNullException("sequence"); }
            return repeatFirstImpl(sequence, true);
        }

        public static IEnumerable<T> RepeatFirstOrEmpty<T>(this IEnumerable<T> sequence)
        {
            if (sequence == null) { throw new ArgumentNullException("sequence"); }
            return repeatFirstImpl(sequence, false);
        }

        private static IEnumerable<T> repeatFirstImpl<T>(IEnumerable<T> sequence, bool throwOnEmpty)
        {
            using (var enumerator = sequence.GetEnumerator())
            {
                if (!enumerator.MoveNext())
                {
                    if (throwOnEmpty) { throw new InvalidOperationException("Sequence is empty"); }
                    yield break;
                }

                T first = enumerator.Current;
                yield return first;

                while (enumerator.MoveNext())
                {
                    yield return enumerator.Current;
                }

                yield return first;
            }
        }
    }
}
