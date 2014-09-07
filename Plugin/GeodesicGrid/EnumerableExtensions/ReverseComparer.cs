using System.Collections.Generic;

namespace GeodesicGrid.EnumerableExtensions
{
    internal class ReverseComparer<T> : IComparer<T>
    {
        private readonly IComparer<T> comparer;

        public ReverseComparer(IComparer<T> comparer)
        {
            this.comparer = comparer;
        }

        public int Compare(T x, T y)
        {
            int comparison = comparer.Compare(x, y);
            if (comparison < 0) { return 1; }
            else if (comparison > 0) { return -1; }
            else { return 0; }
        }
    }
}
