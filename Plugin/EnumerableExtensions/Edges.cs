using System;
using System.Collections.Generic;

namespace Kethane.EnumerableExtensions
{
    public static class EdgesEx
    {
        public static IEnumerable<Pair<T>> Edges<T>(this IEnumerable<T> sequence)
        {
            if (sequence == null) { throw new ArgumentNullException("sequence"); }
            return edgesImpl(sequence);
        }

        public static IEnumerable<Pair<T>> EdgesCircular<T>(this IEnumerable<T> sequence)
        {
            return sequence.RepeatFirstOrEmpty().Edges();
        }

        private static IEnumerable<Pair<T>> edgesImpl<T>(IEnumerable<T> sequence)
        {
            using (var enumerator = sequence.GetEnumerator())
            {
                if (!enumerator.MoveNext()) { yield break; }
                T previous = enumerator.Current;
                while (enumerator.MoveNext())
                {
                    T current = enumerator.Current;
                    yield return new Pair<T>(previous, current);
                    previous = current;
                }
            }
        }
    }
}
