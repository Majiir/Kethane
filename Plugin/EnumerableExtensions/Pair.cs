
namespace Kethane.EnumerableExtensions
{
    public struct Pair<T>
    {
        public T First { get; private set; }
        public T Second { get; private set; }

        public Pair(T first, T second)
            : this()
        {
            First = first;
            Second = second;
        }
    }
}
