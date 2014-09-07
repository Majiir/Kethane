using System;
using System.Collections.Generic;

namespace GeodesicGrid.EnumerableExtensions
{
    public static class MinMaxBy
    {
        public static T MinBy<T, TKey>(this IEnumerable<T> sequence, Func<T, TKey> selector)
        {
            TKey key;
            return MinBy(sequence, selector, out key);
        }

        public static T MaxBy<T, TKey>(this IEnumerable<T> sequence, Func<T, TKey> selector)
        {
            TKey key;
            return MaxBy(sequence, selector, out key);
        }

        public static T MinBy<T, TKey>(this IEnumerable<T> sequence, Func<T, TKey> selector, IComparer<TKey> comparer)
        {
            TKey key;
            return MinBy(sequence, selector, out key, comparer);
        }

        public static T MaxBy<T, TKey>(this IEnumerable<T> sequence, Func<T, TKey> selector, IComparer<TKey> comparer)
        {
            TKey key;
            return MaxBy(sequence, selector, out key, comparer);
        }

        public static T MinBy<T, TKey>(this IEnumerable<T> sequence, Func<T, TKey> selector, out TKey minKey)
        {
            return minByImpl(sequence, selector, out minKey, Comparer<TKey>.Default);
        }

        public static T MaxBy<T, TKey>(this IEnumerable<T> sequence, Func<T, TKey> selector, out TKey maxKey)
        {
            return minByImpl(sequence, selector, out maxKey, new ReverseComparer<TKey>(Comparer<TKey>.Default));
        }

        public static T MinBy<T, TKey>(this IEnumerable<T> sequence, Func<T, TKey> selector, out TKey minKey, IComparer<TKey> comparer)
        {
            if (comparer == null) { throw new ArgumentNullException("comparer"); }
            return minByImpl(sequence, selector, out minKey, comparer);
        }

        public static T MaxBy<T, TKey>(this IEnumerable<T> sequence, Func<T, TKey> selector, out TKey maxKey, IComparer<TKey> comparer)
        {
            if (comparer == null) { throw new ArgumentNullException("comparer"); }
            return minByImpl(sequence, selector, out maxKey, new ReverseComparer<TKey>(comparer));
        }

        public static T MinByOrDefault<T, TKey>(this IEnumerable<T> sequence, Func<T, TKey> selector)
        {
            TKey key;
            return MinByOrDefault(sequence, selector, out key);
        }

        public static T MaxByOrDefault<T, TKey>(this IEnumerable<T> sequence, Func<T, TKey> selector)
        {
            TKey key;
            return MaxByOrDefault(sequence, selector, out key);
        }

        public static T MinByOrDefault<T, TKey>(this IEnumerable<T> sequence, Func<T, TKey> selector, IComparer<TKey> comparer)
        {
            TKey key;
            return MinByOrDefault(sequence, selector, out key, comparer);
        }

        public static T MaxByOrDefault<T, TKey>(this IEnumerable<T> sequence, Func<T, TKey> selector, IComparer<TKey> comparer)
        {
            TKey key;
            return MaxByOrDefault(sequence, selector, out key, comparer);
        }

        public static T MinByOrDefault<T, TKey>(this IEnumerable<T> sequence, Func<T, TKey> selector, out TKey minKey)
        {
            return minByOrDefaultImpl(sequence, selector, out minKey, Comparer<TKey>.Default);
        }

        public static T MaxByOrDefault<T, TKey>(this IEnumerable<T> sequence, Func<T, TKey> selector, out TKey maxKey)
        {
            return minByOrDefaultImpl(sequence, selector, out maxKey, new ReverseComparer<TKey>(Comparer<TKey>.Default));
        }

        public static T MinByOrDefault<T, TKey>(this IEnumerable<T> sequence, Func<T, TKey> selector, out TKey minKey, IComparer<TKey> comparer)
        {
            if (comparer == null) { throw new ArgumentNullException("comparer"); }
            return minByOrDefaultImpl(sequence, selector, out minKey, comparer);
        }

        public static T MaxByOrDefault<T, TKey>(this IEnumerable<T> sequence, Func<T, TKey> selector, out TKey maxKey, IComparer<TKey> comparer)
        {
            if (comparer == null) { throw new ArgumentNullException("comparer"); }
            return minByOrDefaultImpl(sequence, selector, out maxKey, new ReverseComparer<TKey>(comparer));
        }

        private static T minByImpl<T, TKey>(IEnumerable<T> sequence, Func<T, TKey> selector, out TKey key, IComparer<TKey> comparer)
        {
            return minByOrEmpty(sequence, selector, out key, comparer, throwOnEmpty<T>);
        }

        private static T minByOrDefaultImpl<T, TKey>(IEnumerable<T> sequence, Func<T, TKey> selector, out TKey key, IComparer<TKey> comparer)
        {
            return minByOrEmpty(sequence, selector, out key, comparer, () => default(T));
        }

        private static T throwOnEmpty<T>()
        {
            throw new InvalidOperationException("Sequence is empty");
        }

        private static T minByOrEmpty<T, TKey>(IEnumerable<T> sequence, Func<T, TKey> selector, out TKey key, IComparer<TKey> comparer, Func<T> emptyHandler)
        {
            if (sequence == null) { throw new ArgumentNullException("sequence"); }
            if (selector == null) { throw new ArgumentNullException("selector"); }

            using (var enumerator = sequence.GetEnumerator())
            {
                if (!enumerator.MoveNext())
                {
                    T value = emptyHandler();
                    key = selector(value);
                    return value;
                }

                T min = enumerator.Current;
                TKey minKey = selector(min);

                while (enumerator.MoveNext())
                {
                    T current = enumerator.Current;
                    TKey currentKey = selector(current);
                    if (comparer.Compare(currentKey, minKey) < 0)
                    {
                        min = current;
                        minKey = currentKey;
                    }
                }

                key = minKey;
                return min;
            }
        }
    }
}
