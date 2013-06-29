using System;
using System.Collections.Generic;

namespace Kethane
{
    internal static class EnumerableExtensions
    {
        public static T WithMax<T>(this IEnumerable<T> sequence, Func<T, float> selector)
        {
            T best = default(T);
            var bestValue = float.NegativeInfinity;
            foreach (var element in sequence)
            {
                var value = selector(element);
                if (value > bestValue)
                {
                    best = element;
                    bestValue = value;
                }
            }
            return best;
        }

        public static T WithMin<T>(this IEnumerable<T> sequence, Func<T, float> selector)
        {
            T best = default(T);
            var bestValue = float.PositiveInfinity;
            foreach (var element in sequence)
            {
                var value = selector(element);
                if (value < bestValue)
                {
                    best = element;
                    bestValue = value;
                }
            }
            return best;
        }

        public static T WithMax<T>(this IEnumerable<T> sequence, Func<T, double> selector)
        {
            T best = default(T);
            var bestValue = double.NegativeInfinity;
            foreach (var element in sequence)
            {
                var value = selector(element);
                if (value > bestValue)
                {
                    best = element;
                    bestValue = value;
                }
            }
            return best;
        }

        public static T WithMin<T>(this IEnumerable<T> sequence, Func<T, double> selector)
        {
            T best = default(T);
            var bestValue = double.PositiveInfinity;
            foreach (var element in sequence)
            {
                var value = selector(element);
                if (value < bestValue)
                {
                    best = element;
                    bestValue = value;
                }
            }
            return best;
        }

        public static IEnumerable<T> Append<T>(this IEnumerable<T> sequence, T element)
        {
            return new AppendIterator<T>(sequence, element);
        }

        public static IEnumerable<T> Prepend<T>(this IEnumerable<T> sequence, T element)
        {
            return new PrependIterator<T>(sequence, element);
        }

        internal class AppendIterator<T> : AppendPrependIterator<T>
        {
            internal AppendIterator(IEnumerable<T> sequence, T element) : base(sequence, element) { }
        }

        internal class PrependIterator<T> : AppendPrependIterator<T>
        {
            internal PrependIterator(IEnumerable<T> sequence, T element) : base(sequence, element) { }
        }

        internal class AppendPrependIterator<T> : IEnumerable<T>
        {
            private readonly T element;
            private readonly IEnumerable<T> sequence;

            internal AppendPrependIterator(IEnumerable<T> sequence, T element)
            {
                this.element = element;
                this.sequence = sequence;
            }

            public IEnumerator<T> GetEnumerator()
            {
                var appends = new Stack<AppendIterator<T>>();
                IEnumerable<T> seq = this;

                do
                {
                    if (seq is PrependIterator<T>)
                    {
                        yield return ((PrependIterator<T>)seq).element;
                    }
                    else // (seq is AppendIterator<T>)
                    {
                        appends.Push((AppendIterator<T>)seq);
                    }
                    seq = ((AppendPrependIterator<T>)seq).sequence;
                } while (seq is AppendPrependIterator<T>);

                foreach (var e in seq) { yield return e; }
                while (appends.Count > 0) { yield return appends.Pop().element; }
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
    }
}
